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
        private static float bubbleEndTime = 0;
        private static int currentConversationId = 0;
        private static HashSet<int> activeConversations = new HashSet<int>();

        public static bool isLlmBusy = false;

        public SpeechBubbleManager(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Time.time > bubbleEndTime && speechBubbleQueue.Count > 0)
            {
                SpeechBubble bubble = speechBubbleQueue.Dequeue();
                bubbleEndTime = Time.time + bubble.duration;
                MoteMaker.ThrowText(bubble.speaker.DrawPos, bubble.speaker.Map, bubble.text, bubble.duration);

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

        public static void Enqueue(Pawn speaker, string text, float duration, bool isFirstMessage, int conversationId)
        {
            speechBubbleQueue.Enqueue(new SpeechBubble(speaker, text, duration, conversationId));
        }
    }

    public class SpeechBubble
    {
        public Pawn speaker;
        public string text;
        public float duration;
        public int conversationId;

        public SpeechBubble(Pawn speaker, string text, float duration, int conversationId)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
            this.conversationId = conversationId;
        }
    }
}