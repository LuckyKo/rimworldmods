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
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null || this.pawn == null || this.job == null || this.job.targetB == null)
                {
                    this.ReadyForNextToil();
                    return;
                }

                Thing joySpot = this.job.targetB.Thing;

                if (this.pawn.IsHashIntervalTick(60) && (this.pawn.pather == null || !this.pawn.pather.Moving || this.pawn.pather.Destination != initiator.Position))
                {
                    this.pawn.pather.StartPath(initiator, PathEndMode.InteractionCell);
                }

                if (initiator.CurJob == null || initiator.CurJob.targetA.Thing != joySpot || !DatingManager.IsOnDate(initiator))
                {
                    DatingManager.AdvanceDateStage(initiator);
                    this.ReadyForNextToil();
                    return;
                }
            
                if (this.pawn.needs != null && this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
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