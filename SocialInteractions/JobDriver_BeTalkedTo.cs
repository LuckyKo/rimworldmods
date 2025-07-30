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

        public void EndJob(JobCondition condition)
        {
            pawn.jobs.EndCurrentJob(condition);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = new Toil();
            toil.initAction = () => {
                pawn.rotationTracker.FaceCell(TargetA.Cell);
            };
            toil.tickAction = () => {
                if (pawn.needs != null && pawn.needs.joy != null)
                {
                    JoyKindDef socialJoy = DefDatabase<JoyKindDef>.GetNamed("Social", false);
                    if (socialJoy != null)
                    {
                        pawn.needs.joy.GainJoy(0.00015f, socialJoy);
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}