using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JobDriver_WaitForDate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            Toil waitForInitiator = new Toil();
            waitForInitiator.defaultCompleteMode = ToilCompleteMode.Never;
            waitForInitiator.initAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (initiator.CurJob.def != DefDatabase<JobDef>.GetNamed("AskForDate"))
                {
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            waitForInitiator.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (this.pawn.Position.DistanceTo(initiator.Position) < 2f)
                {
                    this.ReadyForNextToil();
                }
            };
            yield return waitForInitiator;

            Toil startOnDateJob = new Toil();
            startOnDateJob.initAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Thing joySpot = initiator.CurJob.targetB.Thing;
                this.pawn.jobs.TryTakeOrderedJob(new Job(DefDatabase<JobDef>.GetNamed("OnDate"), initiator, joySpot), JobTag.Misc);
            };
            startOnDateJob.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return startOnDateJob;
        }
    }
}