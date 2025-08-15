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
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null check to prevent NullReferenceException
            if (this.pawn == null)
            {
                Log.Warning("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null in TryMakePreToilReservations.");
                return false;
            }
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator
            this.FailOnDespawnedOrNull(TargetIndex.B); // Joy Spot

            Toil follow = new Toil();
            follow.initAction = delegate
            {
                // Add comprehensive null checks
                if (this.pawn == null || this.job == null || this.job.targetA == null) 
                {
                    Log.Warning("[SocialInteractions] JobDriver_FollowAndWatch: follow.initAction - pawn, job, or targetA is null. Ending job.");
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
                        Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} starting path to initiator {1} at {2}.", pawnName, initiatorName, initiator.Position));
                    }
                    this.pawn.pather.StartPath(this.job.targetA, PathEndMode.Touch);
                }
                else
                {
                    string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                    Log.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0}'s pather is null. Ending job.", pawnName));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            follow.AddFinishAction(() =>
            {
                if (this.pawn != null)
                {
                    // Simplified logging, removed attempt to access JobCondition
                    Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: follow toil finished for {0}.", this.pawn.LabelShort));
                }
            });
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: Starting watch toil.");
            };
            watch.tickAction = () =>
            {
                // Add comprehensive null checks at the beginning
                if (this.pawn == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                
                if (this.job == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: job is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: initiator is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                
                if (this.job.targetB == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: job.targetB is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }

                // --- Simplified Logic ---
                // The primary condition for this job to continue is that the date is still active.
                // This is indicated by the initiator having the "OnDate" hediff.
                if (!DatingManager.IsOnDate(initiator))
                {
                    string initiatorName = (initiator != null && initiator.Name != null) ? initiator.Name.ToStringShort : "NULL";
                    string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
                    Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Date ended for initiator ({0}), ending job for follower ({1}).", initiatorName, pawnName));
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }

                // --- Pathing Logic (Simplified) ---
                try
                {
                    if (this.pawn.IsHashIntervalTick(60))
                    {
                        // Check if pawn or pather is null before accessing
                        if (this.pawn.pather == null)
                        {
                            string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                            Log.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0}'s pather became null during tick, ending job.", pawnName));
                            this.ReadyForNextToil();
                            return;
                        }

                        // Check if initiator is spawned and on the same map
                        if (!initiator.Spawned || initiator.Map != this.pawn.Map)
                        {
                            string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                            string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                            Log.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Initiator {0} is not spawned or on a different map for follower {1}. Ending job.", initiatorName, pawnName));
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
                            this.pawn.pather.StartPath(initiator, PathEndMode.InteractionCell);
                            
                            // Basic check for immediate pathing failure (heuristic)
                            if (!this.pawn.pather.Moving && this.pawn.Position.DistanceTo(initiator.Position) > 5f) 
                            {
                                string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                                string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                                Log.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} failed to start path to {1} (dist: {2:F2}). Ending job.", pawnName, initiatorName, this.pawn.Position.DistanceTo(initiator.Position)));
                                this.ReadyForNextToil(); 
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string pawnName = (this.pawn != null) ? this.pawn.LabelShort : "NULL";
                    Log.Error(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Exception during pathing for {0}: {1}", pawnName, ex.Message));
                    this.ReadyForNextToil(); // End job on exception
                    return;
                }
                // --- End Pathing Logic ---

                // Gain Joy
                if (this.pawn.needs != null && this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
                }
            };
            watch.AddFinishAction(() =>
            {
                // OnDate hediffs are now handled by DatingManager
                string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
                Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: Job finished for pawn {0}.", pawnName));
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}