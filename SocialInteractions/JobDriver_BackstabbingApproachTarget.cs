using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_BackstabbingApproachTarget : JobDriver
    {
        private const int BaseInteractionDuration = 300; // 5 seconds in ticks

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Thing targetThing = job.GetTarget(TargetIndex.A).Thing;
            SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Starting toil sequence for {0} to approach {1}", 
                pawn.LabelShort, targetThing != null ? targetThing.LabelShort : "null"));
                
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);
            
            // Go to the target
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            
            Toil interactionToil = new Toil();
            interactionToil.initAction = delegate
            {
                Thing targetThingInit = job.GetTarget(TargetIndex.A).Thing;
                SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Initiating backstabbing interaction for {0} with {1}", 
                    pawn.LabelShort, targetThingInit != null ? targetThingInit.LabelShort : "null"));
                    
                Pawn targetPawn = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                
                if (targetPawn == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target pawn is null, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                if (targetPawn.Dead || targetPawn.Downed)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is dead or downed, ending job", targetPawn.LabelShort));
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Additional checks for valid interaction state
                if (!CanInteractWithTarget(targetPawn))
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is not in valid state for interaction, ending job", targetPawn.LabelShort));
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: All checks passed, executing backstabbing interaction between {0} and {1}", 
                    pawn.LabelShort, targetPawn.LabelShort));
                    
                // Perform the actual backstabbing interaction
                TryStartBackstabbingInteraction(targetPawn);
            };
            
            // Add a tick action to follow the target if they move away and check valid interaction state
            interactionToil.tickAction = delegate
            {
                Pawn targetPawn = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (targetPawn != null && !targetPawn.Dead && targetPawn.Spawned)
                {
                    // Check if target is in a valid state for interaction
                    if (!CanInteractWithTarget(targetPawn))
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} became invalid during interaction, ending job", targetPawn.LabelShort));
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    
                    // Check if we should update the path to follow the target
                    if (pawn.IsHashIntervalTick(60)) // Check every second
                    {
                        // If the target has moved significantly, update our path to follow them
                        float distance = (pawn.Position - targetPawn.Position).LengthHorizontal;
                        if (distance > 2f) // If more than 2 cells away
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: {0} updating path to follow {1} (distance: {2:F1})", 
                                pawn.LabelShort, targetPawn.LabelShort, distance));
                            // Update path to follow target
                            pawn.pather.StartPath(targetPawn, PathEndMode.Touch);
                        }
                    }
                }
            };
            
            interactionToil.defaultCompleteMode = ToilCompleteMode.Delay;
            interactionToil.defaultDuration = 1800; // 30 seconds of interaction time
            interactionToil.socialMode = RandomSocialMode.Normal;
            yield return interactionToil;
        }
        
        private void TryStartBackstabbingInteraction(Pawn targetPawn)
        {
            SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Starting backstabbing interaction between {0} and {1}", 
                pawn.LabelShort, targetPawn.LabelShort));
                
            if (pawn == null || targetPawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Null pawn detected, skipping interaction");
                return;
            }
            
            // For backstabbing, we're more lenient about the CanTradeNow check since this is a strategic action
            // that should be able to interrupt temporarily busy pawns
            if (!pawn.CanTradeNow)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: {0} is temporarily busy, but this is OK for backstabbing", 
                    pawn.LabelShort));
            }
            
            if (!targetPawn.CanTradeNow)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is temporarily busy, but this is OK for backstabbing", 
                    targetPawn.LabelShort));
            }
            
            // Check if we can interact with the target (reachability check)
            if (!pawn.CanReach(targetPawn, PathEndMode.Touch, Danger.Deadly))
            {
                SLog.Warning("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Cannot reach target for interaction");
                return;
            }
            
            SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: All conditions met, executing backstabbing interaction between {0} and {1}", 
                pawn.LabelShort, targetPawn.LabelShort));
                
            // Perform the backstabbing interaction
            string letterText, letterLabel;
            LetterDef letterDef;
            LookTargets lookTargets;
            
            InteractionWorker_Backstabbing backstabbingWorker = new InteractionWorker_Backstabbing();
            
            // If we have a target passed in job.targetB, set it as the scheduled target
            if (job.GetTarget(TargetIndex.B).HasThing)
            {
                Pawn scheduledTarget = job.GetTarget(TargetIndex.B).Thing as Pawn;
                if (scheduledTarget != null)
                {
                    backstabbingWorker.ScheduledTargetPawn = scheduledTarget;
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Setting scheduled target to {0}", scheduledTarget.LabelShort));
                }
            }
            
            // This will perform the backstabbing attempt using the worker
            backstabbingWorker.Interacted(pawn, targetPawn, null, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Completed backstabbing interaction between {0} and {1}", 
                pawn.LabelShort, targetPawn.LabelShort));
        }
        
        /// <summary>
        /// Checks if the target pawn is in a valid state for interaction
        /// Hard blocks prevent backstabbing, soft blocks (busy jobs) do not
        /// Note: This method is called frequently during tickAction, so logging is minimal
        /// </summary>
        private bool CanInteractWithTarget(Pawn targetPawn)
        {
            // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Checking if {0} can interact with {1}", 
            //     pawn.LabelShort, targetPawn != null ? targetPawn.LabelShort : "null"));
                
            if (targetPawn == null)
            {
                // SLog.Message("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target is null, cannot interact");
                return false;
            }
                
            // Hard blocks - these completely prevent interaction
            if (targetPawn.Dead || targetPawn.Downed || targetPawn.Destroyed)
            {
                // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is dead/downed/destroyed, cannot interact", targetPawn.LabelShort));
                return false;
            }
            
            // Hard blocks - mental states completely prevent interaction
            if (targetPawn.InMentalState)
            {
                // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is in mental state, cannot interact", targetPawn.LabelShort));
                return false;
            }
            
            // Hard blocks - sleeping completely prevents interaction (natural interruption)
            if (targetPawn.CurJobDef == JobDefOf.LayDown || targetPawn.InBed())
            {
                // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is sleeping/in bed, cannot interact", targetPawn.LabelShort));
                return false;
            }
            
            // Soft blocks - these do NOT prevent backstabbing (strategic interruption is allowed)
            // We don't need to log these since they're expected and allowed
            // Drafted pawns can still be backstabbed
            // if (targetPawn.Drafted)
            // {
            //     SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is drafted, but this is OK for backstabbing", targetPawn.LabelShort));
            // }
            
            // Pawns busy with other jobs can still be interrupted for backstabbing
            // if (!targetPawn.CanTradeNow)
            // {
            //     SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is busy with other job, but this is OK for backstabbing", targetPawn.LabelShort));
            // }
            
            // Pawns who are not awake (but not sleeping) can still be backstabbed
            // if (!targetPawn.Awake())
            // {
            //     SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is not awake, but this is OK for backstabbing", targetPawn.LabelShort));
            // }
            
            // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingApproachTarget: Target {0} is in acceptable state for backstabbing interaction", targetPawn.LabelShort));
            return true;
        }
    }
}