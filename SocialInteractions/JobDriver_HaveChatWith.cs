using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace SocialInteractions
{
    public class JobDriver_HaveChatWith : JobDriver
    {
        private TargetIndex TargetInd = TargetIndex.A;
        private int chatDuration = 1800; // 30 seconds of chatting
        private int followCheckInterval = 60; // Check every second to follow target

        private bool isNegotiationMode = false; // Track if we're in negotiation mode

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetInd);
            
            // Go to the target
            yield return Toils_Goto.GotoThing(TargetInd, PathEndMode.Touch);
            
            // Chat toil - behavior differs based on settings
            Toil chatToil = ToilMaker.MakeToil("ChatToil");
            chatToil.initAction = delegate
            {
                Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                if (target != null)
                {
                    // Check if negotiation mode is enabled and target is humanlike (animals can't negotiate)
                    if (SocialInteractions.Settings.enableManualChat && 
                        SocialInteractions.Settings.llmInteractionsEnabled && 
                        SocialInteractions.Settings.enableInteractiveNegotiation &&
                        target.RaceProps.Humanlike)
                    {
                        // Open negotation dialog
                        isNegotiationMode = true;
                        
                        // We need to execute this on the main thread
                        Dialog_PawnNegotiation dialog = new Dialog_PawnNegotiation(pawn, target);
                        Find.WindowStack.Add(dialog);
                    }
                    else if (SocialInteractions.Settings.enableManualChat && SocialInteractions.Settings.llmInteractionsEnabled)
                    {
                        // Fallback for animals/non-humans: Use LLM bubbles without the dialog
                        // This restores the "Simple Mode" LLM behavior
                        SocialInteractions.HandleNonStoppingInteraction(pawn, target, SI_InteractionDefOf.ManualChat, "Having a casual chat", true, true);
                        isNegotiationMode = false;
                    }
                    else
                    {
                        // Fallback to default interaction (No LLM)
                        string text = string.Format("{0} chats with {1}.", pawn.LabelShort, target.LabelShort);
                        SpeechBubbleManager.ShowDefaultBubble(pawn, text);
                        isNegotiationMode = false;
                    }
                }
            };
            chatToil.tickAction = delegate
            {
                // Check if we should end early (dialog was closed)
                if (isNegotiationMode)
                {
                    // Check if dialog is still open
                    bool dialogOpen = Find.WindowStack.IsOpen<Dialog_PawnNegotiation>();
                    if (!dialogOpen)
                    {
                        // Dialog was closed, end the job
                        ReadyForNextToil();
                        return;
                    }
                }
                
                // Follow the target if they move
                // Previously was checked only for LLM requests, but we want it for manual chat too
                Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                if (target != null && !target.Dead && target.Spawned)
                {
                    // Check if we should update the path to follow the target
                    if (pawn.IsHashIntervalTick(followCheckInterval))
                    {
                        // If the target has moved significantly, update our path to follow them
                        float distance = (pawn.Position - target.Position).LengthHorizontal;
                        if (distance > 2f)
                        {
                            // Update path to follow target
                            pawn.pather.StartPath(target, PathEndMode.Touch);
                        }
                    }
                }
            };
            chatToil.defaultCompleteMode = ToilCompleteMode.Delay;
            chatToil.defaultDuration = chatDuration; // 30 seconds
            yield return chatToil;
            
            // End the job with outcome notification
            Toil finishToil = ToilMaker.MakeToil("FinishToil");
            finishToil.initAction = delegate
            {
                if (!isNegotiationMode)
                {
                   // Skill check based on Social level
                   // Success: 0% at level 0, rising to 20% at level 20
                   // Failure: 50% at level 0, falling to 0% at level 20
                   // Neutral: The rest
                   
                   Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                   if (target != null)
                   {
                       float socialLevel = pawn.skills.GetSkill(SkillDefOf.Social).Level;
                       float normalizedLevel = Mathf.Clamp01(socialLevel / 20f);
                       
                       float successChance = Mathf.Lerp(0f, 0.2f, normalizedLevel);
                       float failChance = Mathf.Lerp(0.5f, 0f, normalizedLevel);
                       
                       float roll = Rand.Value;
                       
                       if (roll < successChance)
                       {
                           // Success
                           Messages.Message("Negotiation Success: " + pawn.LabelShort + " convinced " + target.LabelShort + " (Social Skill " + socialLevel + ")", pawn, MessageTypeDefOf.PositiveEvent);
                           if (pawn.needs != null && pawn.needs.mood != null)
                           {
                               pawn.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationPositive, target);
                           }
                       }
                       else if (roll < successChance + failChance)
                       {
                           // Failure
                           Messages.Message("Negotiation Failed: " + pawn.LabelShort + " failed to convince " + target.LabelShort + " (Social Skill " + socialLevel + ")", pawn, MessageTypeDefOf.NegativeEvent);
                           if (pawn.needs != null && pawn.needs.mood != null)
                           {
                               pawn.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationNegative, target);
                           }
                       }
                       else
                       {
                           // Neutral
                           Messages.Message("Negotiation Neutral: " + pawn.LabelShort + " and " + target.LabelShort + " chatted without reaching a conclusion.", pawn, MessageTypeDefOf.NeutralEvent);
                       }
                   }
                }
            };
            yield return finishToil;
        }
    }
}