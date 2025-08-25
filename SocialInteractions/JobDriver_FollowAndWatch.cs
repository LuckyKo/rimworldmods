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
        /// <summary>
        /// Checks if a pawn is still valid for dating activities
        /// </summary>
        /// <param name="pawn">The pawn to check</param>
        /// <returns>True if the pawn is valid for dating, false otherwise</returns>
        private bool IsPawnValidForDating(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed)
            {
                return false;
            }
            
            if (pawn.InMentalState || pawn.health == null || pawn.health.capacities == null)
            {
                return false;
            }
            
            // Check if the pawn is capable of being awake (basic health check)
            if (!pawn.health.capacities.CanBeAwake)
            {
                return false;
            }
            
            return true;
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null check to prevent NullReferenceException
            if (this.pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null in TryMakePreToilReservations.");
                return false;
            }
            
            // Use the helper method to check if the pawn is valid for dating
            if (!IsPawnValidForDating(this.pawn))
            {
                SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: pawn is not valid for dating in TryMakePreToilReservations.");
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
                
                // Also check if the follower is still on the date
                if (!DatingManager.IsOnDate(this.pawn))
                {
                    string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Date ended for follower ({0}), ending job.", pawnName));
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }
                
                // Check if the initiator has moved on to a non-joy job, and if so, advance the date
                if (initiator.jobs != null && initiator.jobs.curJob != null)
                {
                    // Check if the initiator's current job is NOT a joy job
                    bool isInitiatorDoingJoyJob = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == initiator.jobs.curJob.def)
                        {
                            isInitiatorDoingJoyJob = true;
                            break;
                        }
                    }
                    
                    // If the initiator is not doing a joy job, advance the date
                    if (!isInitiatorDoingJoyJob)
                    {
                        string initiatorName = (initiator != null && initiator.Name != null) ? initiator.Name.ToStringShort : "NULL";
                        string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator ({0}) moved to non-joy job, advancing date for follower ({1}).", initiatorName, pawnName));
                        
                        // Advance the date stage
                        DatingManager.AdvanceDateStage(this.pawn);
                        this.ReadyForNextToil(); // End the FollowAndWatch job
                        return;
                    }
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

                        // Additional check to ensure both pawns are still valid for dating
                        if (!IsPawnValidForDating(this.pawn) || !IsPawnValidForDating(initiator))
                        {
                            string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                            string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                            SLog.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: One or both pawns are no longer valid for dating. Initiator: {0}, Follower: {1}. Ending job.", initiatorName, pawnName));
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