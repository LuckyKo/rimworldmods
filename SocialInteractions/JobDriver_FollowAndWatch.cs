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
            this.FailOnDespawnedOrNull(TargetIndex.A);

            Toil follow = new Toil();
            follow.initAction = () =>
            {
                this.pawn.pather.StartPath(this.job.targetA, PathEndMode.Touch);
            };
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            follow.atomicWithPrevious = true;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                this.pawn.rotationTracker.FaceCell(this.job.targetA.Cell);
            };
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = 1000;
            yield return watch;
        }
    }
}