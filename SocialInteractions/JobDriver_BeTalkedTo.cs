using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JobDriver_BeTalkedTo : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public void EndJob()
        {
            pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = new Toil();
            toil.initAction = () => {
                pawn.rotationTracker.FaceCell(TargetA.Cell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}
