using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace SocialInteractions
{
    public class JobDriver_HaveChatWith : JobDriver
    {
        private TargetIndex TargetInd = TargetIndex.A;
        private int chatDuration = 1800; // 30 seconds of chatting
        private int followCheckInterval = 60; // Check every second to follow target
        private bool isLlmRequestSent = false; // Track if we actually sent an LLM request

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetInd);
            
            // Go to the target
            yield return Toils_Goto.GotoThing(TargetInd, PathEndMode.Touch);
            
            // Chat toil - only follow the target if we actually send an LLM request
            Toil chatToil = ToilMaker.MakeToil("ChatToil");
            chatToil.initAction = delegate
            {
                Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                if (target != null)
                {
                    // Trigger the manual chat interaction
                    // Only send LLM request if both manual chat and LLM interactions are enabled
                    if (SocialInteractions.Settings.enableManualChat && SocialInteractions.Settings.llmInteractionsEnabled)
                    {
                        SocialInteractions.HandleNonStoppingInteraction(pawn, target, SI_InteractionDefOf.ManualChat, "Having a casual chat", true, true);
                        isLlmRequestSent = true; // Mark that we sent an LLM request
                    }
                    else
                    {
                        // Fallback to default interaction
                        string text = string.Format("{0} chats with {1}.", pawn.Name.ToStringShort, target.Name.ToStringShort);
                        SpeechBubbleManager.ShowDefaultBubble(pawn, text);
                        isLlmRequestSent = false; // Mark that we didn't send an LLM request
                    }
                }
            };
            chatToil.tickAction = delegate
            {
                // Only follow the target if we actually sent an LLM request
                if (isLlmRequestSent)
                {
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
                }
            };
            chatToil.defaultCompleteMode = ToilCompleteMode.Delay;
            chatToil.defaultDuration = chatDuration; // 30 seconds
            yield return chatToil;
            
            // End the job
            yield return ToilMaker.MakeToil("FinishToil");
        }
    }
}