using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_FollowAndWatch : JobDriver
    {
        private JobDef lastKnownJobDef = null;
        private int ticksSinceStart = 0;
        private const int InitialToleranceTicks = 60; // 1 second of tolerance for job transitions
        private int? initiatorJoyJobStartTick = null; // Track when the initiator started a joy job
        private bool partnerHasStartedJoyJob = false; // Track if partner has started a joy job for current joy activity
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null check to prevent NullReferenceException
            if (this.pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null in TryMakePreilReservations.");
                return false;
            }
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator

            Toil follow = new Toil();
            follow.initAction = delegate
            {
                // Add comprehensive null checks
                if (this.pawn == null || this.job == null || this.job.targetA == null) 
                {
                    SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: follow.initAction - pawn, job, or targetA is null. Ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                if (this.pawn.pather != null) 
                {
                    Pawn initiator = this.job.targetA.Thing as Pawn;
                    if (initiator != null)
                    {
                        // Log start path attempt
                        string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                        string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} starting path to initiator {1} at {2}.", pawnName, initiatorName, initiator.Position));
                    }
                    this.pawn.pather.StartPath(this.job.targetA, PathEndMode.Touch);
                }
                else
                {
                    string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0}'s pather is null. Ending job.", pawnName));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            follow.AddFinishAction(() =>
            {
                if (this.pawn != null)
                {
                    // Simplified logging, removed attempt to access JobCondition
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: follow toil finished for {0}.", this.pawn.LabelShort));
                }
            });
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return follow;

            Toil watch = new Toil();
            watch.tickAction = () => {
                ticksSinceStart++;
                
                // Add comprehensive null checks at the beginning
                if (this.pawn == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                
                if (this.job == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_FollowAndWatch: job is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_FollowAndWatch: initiator is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }

                // The primary condition for this job to continue is that the date is still active.
                // This is indicated by the initiator having the "OnDate" hediff.
                if (!DatingManager.IsOnDate(initiator))
                {
                    string initiatorName = (initiator != null && initiator.Name != null) ? initiator.Name.ToStringShort : "NULL";
                    string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Date ended for initiator ({0}), ending job for follower ({1}).", initiatorName, pawnName));
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }

                // Pathing Logic - Continuously update path to follow the initiator
                try
                {
                    if (this.pawn.IsHashIntervalTick(SocialInteractions.Settings.jobCheckIntervalTicks))
                    {
                        // Check if pawn or pather is null before accessing
                        if (this.pawn.pather == null)
                        {
                            string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                            SLog.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0}'s pather became null during tick, ending job.", pawnName));
                            this.ReadyForNextToil();
                            return;
                        }

                        // Check if initiator is spawned and on the same map
                        if (!initiator.Spawned || initiator.Map != this.pawn.Map)
                        {
                            string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                            string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                            SLog.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} is not spawned or on a different map for follower {1}. Ending job.", initiatorName, pawnName));
                            this.ReadyForNextToil();
                            return;
                        }

                        // Check if destination is already set correctly
                        if (this.pawn.pather.Moving && this.pawn.pather.Destination == initiator.Position)
                        {
                            // Already moving to the correct destination, do nothing.
                        }
                        else
                        {
                            // Attempt to start path
                            this.pawn.pather.StartPath(initiator, PathEndMode.Touch);
                            
                            // Basic check for immediate pathing failure (heuristic)
                            if (!this.pawn.pather.Moving && this.pawn.Position.DistanceTo(initiator.Position) > 5f) 
                            {
                                string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                                string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                                SLog.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} failed to start path to {1} (dist: {2:F2}). Ending job.", pawnName, initiatorName, this.pawn.Position.DistanceTo(initiator.Position)));
                                this.ReadyForNextToil(); 
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string pawnName = (this.pawn != null) ? this.pawn.LabelShort : "NULL";
                    SLog.Error(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Exception during pathing for {0}: {1}", pawnName, ex.Message));
                    this.ReadyForNextToil(); // End job on exception
                    return;
                }

                // Face the initiator
                this.pawn.rotationTracker.FaceCell(initiator.Position);

                // Gain Joy at the same rate as the initiator would from doing a joy activity
                // This is a social joy gain since the pawn is watching their date do something enjoyable
                if (this.pawn.needs != null && this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
                }

                // Check if the initiator has started a joy job
                bool initiatorIsDoingJoyJob = false;
                if (initiator.CurJob != null)
                {
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == initiator.CurJob.def)
                        {
                            initiatorIsDoingJoyJob = true;
                            break;
                        }
                    }
                }

                // If the initiator is doing a joy job, have the partner also do a joy job
                if (initiatorIsDoingJoyJob)
                {
                    // Track when the initiator started the joy job
                    if (initiatorJoyJobStartTick == null)
                    {
                        initiatorJoyJobStartTick = ticksSinceStart;
                        partnerHasStartedJoyJob = false;
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} started joy job at tick {1}", 
                            initiator.Name.ToStringShort, ticksSinceStart));
                    }
                    
                    // Wait 180 ticks (3 seconds) after the initiator starts the joy job before having the partner join in
                    if (ticksSinceStart - initiatorJoyJobStartTick.Value >= 180 && !partnerHasStartedJoyJob)
                    {
                        // Have the partner start a joy job
                        HavePartnerStartJoyJob();
                        partnerHasStartedJoyJob = true;
                    }
                }
                else
                {
                    // If the initiator is no longer doing a joy job, reset the tracking variables
                    initiatorJoyJobStartTick = null;
                    partnerHasStartedJoyJob = false;
                }

                // Check if the partner (this pawn) is currently doing a joy job
                // If so, check if their joy need is satisfied and they should go back to following
                if (this.pawn.jobs != null && this.pawn.CurJob != null)
                {
                    // Check if the current job is a joy job
                    bool isCurrentJobJoyJob = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == this.pawn.CurJob.def)
                        {
                            isCurrentJobJoyJob = true;
                            break;
                        }
                    }
                    
                    // If the partner is doing a joy job, check if their joy need is satisfied
                    if (isCurrentJobJoyJob && this.pawn.needs != null && this.pawn.needs.joy != null)
                    {
                        // If the partner's joy need is nearly satisfied (95% or more), they should go back to following
                        if (this.pawn.needs.joy.CurLevelPercentage >= 0.95f)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Partner {0} joy need is satisfied ({1:P}), going back to following {2}.", 
                                this.pawn.Name.ToStringShort, this.pawn.needs.joy.CurLevelPercentage, initiator.Name.ToStringShort));
                            // End the joy job and continue with the follow behavior
                            this.pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                        }
                    }
                }

                // Check if the initiator has finished their joy job
                if (initiator.jobs == null || initiator.CurJob == null)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} has no current job, ending watch.", initiator.Name.ToStringShort));
                    this.ReadyForNextToil();
                    return;
                }

                // Only check job type if the job has changed
                if (lastKnownJobDef != initiator.CurJob.def)
                {
                    lastKnownJobDef = initiator.CurJob.def;
                    
                    // Log the current job for debugging
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} is now doing job {1} (defName: {2})", 
                        initiator.Name.ToStringShort, initiator.CurJob.def.defName, initiator.CurJob.def.defName));

                    // Check if the initiator's job is a joy job
                    bool isJoyJob = false;
                    JoyGiverDef initiatorJoyGiver = null;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == initiator.CurJob.def)
                        {
                            isJoyJob = true;
                            initiatorJoyGiver = joyGiver;
                            break;
                        }
                    }
                    
                    // Track when the initiator starts a joy job
                    if (isJoyJob)
                    {
                        initiatorJoyJobStartTick = ticksSinceStart;
                        lastKnownJoyJobDef = initiator.CurJob.def;
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} started joy job {1} at tick {2}", 
                            initiator.Name.ToStringShort, initiator.CurJob.def.defName, ticksSinceStart));
                    }
                    else
                    {
                        // If the initiator is no longer doing a joy job, reset the tracking variables
                        initiatorJoyJobStartTick = null;
                        lastKnownJoyJobDef = null;
                    }

                    // During the initial tolerance period, be more lenient with job checks for non-joy jobs
                    if (ticksSinceStart < SocialInteractions.Settings.initialToleranceTicks)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: In initial tolerance period ({0}/{1} ticks), being lenient with non-joy job check.", 
                            ticksSinceStart, SocialInteractions.Settings.initialToleranceTicks));
                        return; // Skip the job termination check during initial tolerance period for non-joy jobs
                    }

                    // After the initial tolerance period, enforce job type checks
                    // If it's not a joy job, check if it's a DateLovin job (which is also a valid continuation of the date)
                    if (initiator.CurJob.def != SI_JobDefOf.DateLovin)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} is no longer doing a joy job or DateLovin job, advancing date stage.", initiator.Name.ToStringShort));
                        // Advance the date stage for the initiator
                        DatingManager.AdvanceDateStage(initiator);
                        // End this job
                        this.ReadyForNextToil();
                        return;
                    }
                }
            };
            watch.AddFinishAction(() => {
                // OnDate hediffs are now handled by DatingManager
                string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Job finished for pawn {0}.", pawnName));
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
        
        private void TryHavePartnerJoinJoyActivity(Pawn initiator, JobDef joyJobDef, int joyJobStartTick)
        {
            // Check if the partner's joy need is low enough to want to join
            if (this.pawn.needs != null && this.pawn.needs.joy != null && 
                this.pawn.needs.joy.CurLevelPercentage >= 0.95f)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Partner {0}'s joy level is too high to join activity", 
                    this.pawn.Name.ToStringShort));
                return;
            }
            
            // Find the joy giver for this job def
            JoyGiverDef initiatorJoyGiver = null;
            foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
            {
                if (joyGiver.jobDef == joyJobDef)
                {
                    initiatorJoyGiver = joyGiver;
                    break;
                }
            }
            
            if (initiatorJoyGiver == null)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Could not find joy giver for job def {0}", 
                    joyJobDef.defName));
                return;
            }
            
            // Try to give the partner the same joy job as the initiator
            Job partnerJoyJob = initiatorJoyGiver.Worker.TryGiveJob(this.pawn);
            if (partnerJoyJob == null)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Partner {0} cannot do the same joy activity as initiator {1}", 
                    this.pawn.Name.ToStringShort, initiator.Name.ToStringShort));
                return;
            }
            
            // Check if the target locations match or are nearby
            bool targetsMatch = false;
            if (partnerJoyJob.targetA.Thing != null && initiator.CurJob.targetA.Thing != null)
            {
                targetsMatch = partnerJoyJob.targetA.Thing == initiator.CurJob.targetA.Thing;
            }
            else if (partnerJoyJob.targetA.Cell.IsValid && initiator.CurJob.targetA.Cell.IsValid)
            {
                targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(initiator.CurJob.targetA.Cell) <= 7f;
            }
            
            if (targetsMatch)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Partner {0} is joining initiator {1} in joy activity {2}.", 
                    this.pawn.Name.ToStringShort, initiator.Name.ToStringShort, partnerJoyJob.def.defName));
                
                // Start the joy job for the partner (this will temporarily override the follow behavior)
                this.pawn.jobs.StartJob(partnerJoyJob, JobCondition.None, null, false, true, null, JobTag.Misc, false, true);
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Target locations don't match for partner {0} and initiator {1}", 
                    this.pawn.Name.ToStringShort, initiator.Name.ToStringShort));
            }
        }
    }
}