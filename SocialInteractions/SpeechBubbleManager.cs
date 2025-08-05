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
        private static Queue<Action> pendingJobs = new Queue<Action>();

        public static bool isLlmBusy = false;

        public SpeechBubbleManager(Game game)
        {
            speechBubbleQueue.Clear();
            pawnBubbleEndTimes.Clear();
            nextQueuedBubbleDisplayTime = 0f;
            currentConversationId = 0;
            activeConversations.Clear();
            isLlmBusy = false;
            pendingJobs.Clear();
        }

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
                    if (bubble.color.HasValue)
                    {
                        MoteMaker.ThrowText(bubble.speaker.DrawPos, bubble.speaker.Map, bubble.text, bubble.color.Value, bubble.duration);
                    }
                    else
                    {
                        MoteMaker.ThrowText(bubble.speaker.DrawPos, bubble.speaker.Map, bubble.text, bubble.duration);
                    }
                }

                if (!speechBubbleQueue.Any(b => b.conversationId == bubble.conversationId))
                {
                    EndConversation(bubble.conversationId);
                }
            }

            // Set isLlmBusy based on whether there are any active bubbles or conversations
            isLlmBusy = speechBubbleQueue.Count > 0 || activeConversations.Count > 0;

            // Process pending jobs
            while (pendingJobs.Count > 0)
            {
                pendingJobs.Dequeue()();
            }
        }

        public static void EnqueueJob(Action jobAction)
        {
            pendingJobs.Enqueue(jobAction);
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

        public static void Enqueue(Verse.Pawn speaker, string text, float duration, bool isFirstMessage, int conversationId, Color? color = null)
        {
            speechBubbleQueue.Enqueue(new SpeechBubble(speaker, text, duration, conversationId, false, color));
        }

        // For instant messages (combat taunts)
        public static void EnqueueInstant(Verse.Pawn speaker, string text, float duration, Color? color = null)
        {
            float endTime;
            if (pawnBubbleEndTimes.TryGetValue(speaker, out endTime) && Time.time < endTime)
            {
                return; // Don't enqueue if this pawn already has an active instant bubble
            }
            duration = Math.Max(1f, duration);
            pawnBubbleEndTimes[speaker] = Time.time + duration; // Set bubbleEndTime for instant bubbles
            // No clearing of speechBubbleQueue here, as it's for instant display only
            if (speaker != null && speaker.Map != null)
            {
                if (color.HasValue)
                {
                    MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, text, color.Value, duration);
                }
                else
                {
                    MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, text, duration);
                }
            }
        }

        // For default summary bubbles
        public static void ShowDefaultBubble(Pawn speaker, string text)
        {
            float endTime;
            if (pawnBubbleEndTimes.TryGetValue(speaker, out endTime) && Time.time < endTime)
            {
                return; // Don't show if this pawn already has an active bubble
            }
            float duration = Math.Max(1f, SocialInteractions.EstimateReadingTime(text));
            pawnBubbleEndTimes[speaker] = Time.time + duration;
            if (speaker != null && speaker.Map != null)
            {
                MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, text, new Color(0.75f, 0.75f, 0.75f), duration);
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
        public Color? color;

        public SpeechBubble(Pawn speaker, string text, float duration, int conversationId, bool isInstant = false, Color? color = null)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
            this.conversationId = conversationId;
            this.isInstant = isInstant;
            this.color = color;
        }
    }
}
