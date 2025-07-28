using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace SocialInteractions
{
    public class SpeechBubbleManager : GameComponent
    {
        private static Queue<SpeechBubble> speechBubbleQueue = new Queue<SpeechBubble>();
        private static bool isBubbleVisible = false;
        private static float bubbleEndTime = 0;

        public static bool isConversationActive = false;
        public static Action onConversationFinished;

        public SpeechBubbleManager(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (isBubbleVisible && Time.time >= bubbleEndTime)
            {
                isBubbleVisible = false;
                if (speechBubbleQueue.Count == 0 && isConversationActive)
                {
                    isConversationActive = false;
                    if (onConversationFinished != null)
                    {
                        onConversationFinished();
                    }
                }
            }

            if (!isBubbleVisible && speechBubbleQueue.Count > 0)
            {
                SpeechBubble bubble = speechBubbleQueue.Dequeue();
                isBubbleVisible = true;
                bubbleEndTime = Time.time + bubble.duration;
                MoteMaker.ThrowText(bubble.speaker.DrawPos, bubble.speaker.Map, bubble.text, bubble.duration);
            }
        }

        public static void Enqueue(Pawn speaker, string text, float duration)
        {
            speechBubbleQueue.Enqueue(new SpeechBubble(speaker, text, duration));
        }
    }

    public class SpeechBubble
    {
        public Pawn speaker;
        public string text;
        public float duration;

        public SpeechBubble(Pawn speaker, string text, float duration)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
        }
    }
}