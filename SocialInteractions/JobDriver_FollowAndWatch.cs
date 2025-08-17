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
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == initiator.CurJob.def)
                        {
                            isJoyJob = true;
                            break;
                        }
                    }

                    // Log the joy job check result
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Is joy job: {0}", isJoyJob));

                    // If it's a joy job, try to have the partner join the same activity
                    // We do this even during the initial tolerance period
                    // But only if the partner's joy bar is not already satisfied
                    if (isJoyJob && this.pawn.needs != null && this.pawn.needs.joy != null && this.pawn.needs.joy.CurLevelPercentage < 0.95f)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} started a joy job and partner {1} joy bar is low ({2:P}), trying to have partner join.", 
                            initiator.Name.ToStringShort, this.pawn.Name.ToStringShort, this.pawn.needs.joy.CurLevelPercentage));
                        
                        // Try to find a joy giver that matches the initiator's job
                        JoyGiverDef matchingJoyGiver = null;
                        foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                        {
                            if (joyGiver.jobDef == initiator.CurJob.def)
                            {
                                matchingJoyGiver = joyGiver;
                                break;
                            }
                        }
                        
                        if (matchingJoyGiver != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Found matching joy giver {0} for job {1}", 
                                matchingJoyGiver.defName, initiator.CurJob.def.defName));
                            
                            // Try to give the same joy job to the partner
                            Job partnerJoyJob = matchingJoyGiver.Worker.TryGiveJob(this.pawn);
                            if (partnerJoyJob != null)
                            {
                                // Check if the partner's joy job targets the same main object as the initiator's job
                                // For most joy activities, this would be targetA (the main object/spot)
                                bool targetsMatch = true;
                                
                                // Check targetA (main object/spot)
                                if (partnerJoyJob.targetA != null && initiator.CurJob.targetA != null)
                                {
                                    // For things like meditation spots or game tables, check if they're the same thing
                                    if (partnerJoyJob.targetA.Thing != null && initiator.CurJob.targetA.Thing != null)
                                    {
                                        targetsMatch = partnerJoyJob.targetA.Thing == initiator.CurJob.targetA.Thing;
                                    }
                                    // For positions, check if they're close enough (within 7 cells)
                                    else if (partnerJoyJob.targetA.Cell.IsValid && initiator.CurJob.targetA.Cell.IsValid)
                                    {
                                        targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(initiator.CurJob.targetA.Cell) <= 7f;
                                    }
                                }
                                
                                if (targetsMatch)
                                {
                                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Successfully created joy job {0} for partner {1} targeting the same object/spot as initiator", 
                                        partnerJoyJob.def.defName, this.pawn.Name.ToStringShort));
                                    
                                    // Start the joy job for the partner
                                    this.pawn.jobs.StartJob(partnerJoyJob, JobCondition.InterruptForced);
                                    // Don't return here - let the FollowAndWatch job continue
                                    // If the partner can't actually join (e.g., all positions busy), they'll continue following
                                }
                                else
                                {
                                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Partner {0} joy job targets different object/spot than initiator. Continuing to follow.", 
                                        this.pawn.Name.ToStringShort));
                                    // Continue with the follow behavior if the targets don't match
                                }
                            }
                            else
                            {
                                SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Could not create joy job for partner {0}. Continuing to follow.", 
                                    this.pawn.Name.ToStringShort));
                                // Continue with the follow behavior if we can't join the joy activity
                            }
                        }
                        else
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Could not find matching joy giver for job {0}", 
                                initiator.CurJob.def.defName));
                        }
                        
                        // Continue with the rest of the logic even for joy jobs
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
    }
}