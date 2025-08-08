using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JobDriver_GoOnDate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator
            this.FailOnDespawnedOrNull(TargetIndex.B); // Partner

            Toil assignJoyJob = new Toil();
            assignJoyJob.initAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                // Assign joy job to initiator
                // (This will be replaced with a proper joy job assignment)
                initiator.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 1000));

                // Assign follow and watch job to partner
                partner.jobs.StartJob(JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator, initiator.CurJob.targetA.Thing));
            };
            assignJoyJob.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return assignJoyJob;

            Toil waitForJoyJob = new Toil();
            waitForJoyJob.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Starting waitForJoyJob toil.");
            };
            waitForJoyJob.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                if (initiator.CurJob.def.joyKind == null && partner.CurJob.def != SI_JobDefOf.FollowAndWatchInitiator)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Joy job finished, advancing to next toil.");
                    this.ReadyForNextToil();
                }
            };
            waitForJoyJob.defaultCompleteMode = ToilCompleteMode.Never;
            yield return waitForJoyJob;

            Toil advanceDate = new Toil();
            advanceDate.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Initiating advanceDate toil.");
                DatingManager.AdvanceDateStage((Pawn)this.job.targetA.Thing);
            };
            advanceDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return advanceDate;

            Toil waitForLovinJob = new Toil();
            waitForLovinJob.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Starting waitForLovinJob toil.");
            };
            waitForLovinJob.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;
                Date date = DatingManager.GetDateWith(initiator);

                if (date == null)
                {
                    this.EndJobWith(JobCondition.Succeeded);
                    return;
                }

                if (date.Stage == DateStage.Lovin)
                {
                    bool initiatorDone = initiator.CurJob == null || initiator.CurJob.def != SI_JobDefOf.DateLovin;
                    bool partnerDone = partner.CurJob == null || partner.CurJob.def != SI_JobDefOf.DateLovin;

                    if (initiatorDone && partnerDone)
                    {
                        Log.Message("[SocialInteractions] JobDriver_GoOnDate: Lovin job appears finished for both pawns. Advancing to next toil.");
                        this.ReadyForNextToil();
                    }
                }
                else
                {
                    // If the date is not in the lovin' stage (e.g., they couldn't have lovin'), end the job.
                    this.EndJobWith(JobCondition.Succeeded);
                }
            };
            waitForLovinJob.defaultCompleteMode = ToilCompleteMode.Never;
            yield return waitForLovinJob;

            Toil finishDate = new Toil();
            finishDate.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Initiating finishDate toil.");
                DatingManager.AdvanceDateStage((Pawn)this.job.targetA.Thing);
            };
            finishDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finishDate;
        }
    }
}