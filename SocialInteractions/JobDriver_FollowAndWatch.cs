using RimWorld;
using Verse;
using Verse.AI;

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
                IntVec3 watchCell = CellFinder.StandableCellNear(this.job.targetB.Thing.Position, this.job.targetB.Thing.Map, 5);
                if (!watchCell.IsValid) watchCell = this.job.targetB.Thing.Position;
                this.pawn.pather.StartPath(watchCell, PathEndMode.OnCell);
            };
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            follow.atomicWithPrevious = true;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                this.pawn.rotationTracker.FaceCell(this.job.targetA.Cell);
            };
            watch.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Thing joySpot = this.job.targetB.Thing;

                // End if initiator is not doing a job, or the job is not at the joy spot
                if (initiator.CurJob == null || initiator.CurJob.targetA.Thing != joySpot)
                {
                    this.ReadyForNextToil();
                }
                else
                {
                    // Gain joy while watching
                    JoyUtility.JoyTickCheckEnd(this.pawn, 1, JoyTickFullJoyAction.EndJob);
                }
            };
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}