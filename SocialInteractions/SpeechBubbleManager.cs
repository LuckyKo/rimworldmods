using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace SocialInteractions
{
    public class SpeechBubbleManager : GameComponent
    {
        private static Queue<SpeechBubble> speechBubbleQueue = new Queue<SpeechBubble>();
        private static Dictionary<Pawn, float> pawnBubbleEndTimes = new Dictionary<Pawn, float>();
        private static float nextQueuedBubbleDisplayTime = 0f;
        private static int currentConversationId = 0;
        private static HashSet<int> activeConversations = new HashSet<int>();

        public static bool isLlmBusy = false;

        public SpeechBubbleManager(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Clean up expired instant bubbles
            List<Pawn> pawnsToRemove = new List<Pawn>();
            foreach (var entry in pawnBubbleEndTimes)
            {
                if (Time.time >= entry.Value)
                {
                    pawnsToRemove.Add(entry.Key);
                }
            }
            foreach (Pawn pawn in pawnsToRemove)
            {
                pawnBubbleEndTimes.Remove(pawn);
            }

            // Process queued bubbles
            if (speechBubbleQueue.Count > 0 && Time.time >= nextQueuedBubbleDisplayTime)
            {
                SpeechBubble bubble = speechBubbleQueue.Dequeue();
                nextQueuedBubbleDisplayTime = Time.time + bubble.duration;
                if (bubble.speaker != null && bubble.speaker.Map != null)
                {
                    Vector3 currentDrawPos = bubble.speaker.DrawPos;
                    MoteMaker.ThrowText(currentDrawPos, bubble.speaker.Map, bubble.text, bubble.duration);
                }

                if (!speechBubbleQueue.Any(b => b.conversationId == bubble.conversationId))
                {
                    EndConversation(bubble.conversationId);
                }
            }
        }

        public static int StartConversation()
        {
            currentConversationId++;
            activeConversations.Add(currentConversationId);
            return currentConversationId;
        }

        public static void EndConversation(int conversationId)
        {
            activeConversations.Remove(conversationId);
            if (activeConversations.Count == 0)
            {
                isLlmBusy = false;
            }
        }

        public static bool IsConversationActive(int conversationId)
        {
            return activeConversations.Contains(conversationId);
        }

        // For LLM messages (queued)
        public static void Enqueue(Verse.Pawn speaker, string text, float duration, bool isFirstMessage, int conversationId)
        {
            speechBubbleQueue.Enqueue(new SpeechBubble(speaker, text, duration, conversationId, false));
        }

        // For instant messages (combat taunts)
        public static void EnqueueInstant(Verse.Pawn speaker, string text, float duration)
        {
            float endTime;
            if (pawnBubbleEndTimes.TryGetValue(speaker, out endTime) && Time.time < endTime)
            {
                return; // Don't enqueue if this pawn already has an active instant bubble
            }
            pawnBubbleEndTimes[speaker] = Time.time + duration; // Set bubbleEndTime for instant bubbles
            // No clearing of speechBubbleQueue here, as it's for instant display only
            if (speaker != null && speaker.Map != null)
            {
                MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, text, duration);
            }
        }
    }

    public class SpeechBubble
    {
        public Pawn speaker;
        public string text;
        public float duration;
        public int conversationId;
        public bool isInstant;

        public SpeechBubble(Pawn speaker, string text, float duration, int conversationId, bool isInstant = false)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
            this.conversationId = conversationId;
            this.isInstant = isInstant;
        }
    }
}
