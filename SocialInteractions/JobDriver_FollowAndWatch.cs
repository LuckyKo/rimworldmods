using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_FollowAndWatch : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator
            this.FailOnDespawnedOrNull(TargetIndex.B); // Joy Spot

            Toil follow = new Toil();
            follow.initAction = () =>
            {
                if (this.pawn == null || this.job == null || this.job.targetA == null) return;
                if (this.pawn != null && this.pawn.pather != null) this.pawn.pather.StartPath(this.job.targetA, PathEndMode.Touch);
            };
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: Starting watch toil.");
            };
            watch.tickAction = () =>
            {
                // Add null and type check for initiator
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null || this.pawn == null || this.job == null || this.job.targetB == null)
                {
                    this.ReadyForNextToil(); // End the job if initiator or job targets are invalid
                    return;
                }

                Thing joySpot = this.job.targetB.Thing;

                // Continuous following: If initiator moves, update path
                if (this.pawn.IsHashIntervalTick(60) && (this.pawn.pather == null || !this.pawn.pather.Moving || this.pawn.pather.Destination != initiator.Position))
                {
                    this.pawn.pather.StartPath(initiator, PathEndMode.InteractionCell);
                }

                // End condition: initiator is no longer doing a joy job at the joy spot.
                // The job should end when the initiator's joy job at the joySpot ends.
                // Also, if the initiator is no longer on a date, the job should end.
                if (initiator.CurJob == null || initiator.CurJob.targetA.Thing != joySpot || !DatingManager.IsOnDate(initiator))
                {
                    DatingManager.AdvanceDateStage(initiator);
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }
                
                
                
                
                
            
            // Gain joy while watching
                if (initiator.CurJob != null && initiator.CurJob.def != null && initiator.CurJob.def.joyKind != null)
                {
                    // Custom joy gain for observing
                    if (this.pawn.needs != null && this.pawn.needs.joy != null && initiator.CurJob.def.joyGainRate > 0f)
                    {
                        float joyGain = initiator.CurJob.def.joyGainRate * 0.000144f; // Base joy gain per tick
                        this.pawn.needs.joy.GainJoy(joyGain, JoyKindDefOf.Social);
                    }
                }
            };
            watch.AddFinishAction(() =>
            {
                // OnDate hediffs are now handled by DatingManager
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}