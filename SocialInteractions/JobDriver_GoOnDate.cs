using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_GoOnDate : JobDriver
    {
        private Pawn Partner 
        {
            get { return (Pawn)this.job.targetB.Thing; }
        }
        private Thing JoySpot
        {
            get { return this.job.targetA.Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Partner, this.job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.B); // Partner

            // Start the date
            Toil startDate = new Toil();
            startDate.initAction = () => 
            {
                DatingManager.StartDate(this.pawn, this.Partner);
                Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort), new LookTargets(this.pawn, this.Partner), MessageTypeDefOf.PositiveEvent);
            };
            startDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return startDate;

            // Find a joy spot and assign jobs
            Toil findSpotAndAssign = new Toil();
            findSpotAndAssign.initAction = () =>
            {
                var joySpots = DatingManager.FindJoySpotFor(this.pawn, this.Partner);
                if (joySpots == null || !joySpots.Any())
                {
                    Log.Message("[SocialInteractions] GoOnDate: No suitable joy spot found. Ending date.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                var chosenSpot = joySpots.First();
                this.job.targetA = chosenSpot.Item1; // Set the joy spot as TargetA
                this.pawn.Reserve(this.job.targetA, this.job);

                // Tell partner to follow
                Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, this.pawn, this.job.targetA);
                this.Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);

                // LLM Interaction
                string subject = SpeechBubbleManager.GetDateSubject(this.pawn, this.Partner, this.job.targetA.Thing);
                SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateAccepted, subject);
            };
            findSpotAndAssign.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findSpotAndAssign;

            // Go to the joy spot
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Do the joy activity
            Toil doJoy = new Toil();
            doJoy.defaultCompleteMode = ToilCompleteMode.Delay;
            doJoy.defaultDuration = 4000; // Approx 1.5 game hours
            doJoy.tickAction = () =>
            {
                this.pawn.rotationTracker.FaceCell(JoySpot.InteractionCell);
                if (this.pawn.needs != null && this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
                }
                if (this.Partner.CurJobDef != SI_JobDefOf.FollowAndWatchInitiator)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            yield return doJoy;

            // Advance the date stage
            Toil advanceDate = new Toil();
            advanceDate.initAction = () => 
            {
                DatingManager.AdvanceDateStage(this.pawn);
            };
            advanceDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return advanceDate;

            // Final cleanup toil that will always run
            this.AddFinishAction(delegate
            {
                if (DatingManager.IsOnDate(this.pawn))
                {
                    DatingManager.EndDate(this.pawn);
                }
            });
        }
    }
}
