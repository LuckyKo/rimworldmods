using System;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

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
            // Add null checks for targets at the very beginning
            if (this.job.targetA == null || this.job.targetA.Thing == null ||
                this.job.targetB == null || this.job.targetB.Thing == null)
            {
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Aborting job due to null targets. Job: {0}", this.job));
                this.EndJobWith(JobCondition.Incompletable);
                yield break; // Exit the enumerator
            }

            Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: MakeNewToils started for Initiator: {0}, Partner: {1}", this.job.targetA.Thing.LabelShort, this.job.targetB.Thing.LabelShort));

            // Start the date in DatingManager
            DatingManager.StartDate((Pawn)this.job.targetA.Thing, (Pawn)this.job.targetB.Thing);

            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator
            this.FailOnDespawnedOrNull(TargetIndex.B); // Partner

            Toil assignJoyJob = new Toil();
            assignJoyJob.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering assignJoyJob toil.");
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                if (initiator == null || partner == null) {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                System.Collections.Generic.List<System.Tuple<Thing, JoyGiverDef, IntVec3>> joySpots = DatingManager.FindJoySpotFor(initiator, partner);
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: FindJoySpotFor returned {0} joy spots.", joySpots != null ? joySpots.Count : 0));

                if (joySpots != null && joySpots.Any())
                {
                    System.Tuple<Thing, JoyGiverDef, IntVec3> chosenSpot = joySpots.First();
                    Thing joyThing = chosenSpot.Item1;
                    JoyGiverDef joyGiver = chosenSpot.Item2;
                    IntVec3 joyCell = chosenSpot.Item3;

                    if (joyGiver != null && joyGiver.jobDef != null && initiator.jobs != null)
                    {
                        Job initiatorJoyJob = JobMaker.MakeJob(joyGiver.jobDef, joyThing, joyCell);
                        initiator.jobs.StartJob(initiatorJoyJob, JobCondition.InterruptForced); // Revert to original
                        Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Initiator {0} assigned joy job {1} at {2}.", initiator.Name.ToStringShort, initiatorJoyJob.def.defName, joyCell));
                    }
                }
                else
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: No suitable joy spot found for {0} and {1}. Ending date.", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                    DatingManager.EndDate(initiator);
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            assignJoyJob.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return assignJoyJob;
            Log.Message("[SocialInteractions] JobDriver_GoOnDate: assignJoyJob completed. Attempting to yield assignPartnerJob.");

            Toil assignPartnerJob = new Toil();
            assignPartnerJob.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering assignPartnerJob toil.");
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: assignPartnerJob.initAction started for Initiator: {0}, Partner: {1}", ((Pawn)this.job.targetA.Thing).Name.ToStringShort, ((Pawn)this.job.targetB.Thing).Name.ToStringShort));
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                if (initiator.CurJob != null && initiator.CurJob.def.joyKind != null)
                {
                    Job partnerFollowJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator, initiator.CurJob.targetA.Thing);
                    partner.jobs.StartJob(partnerFollowJob, JobCondition.InterruptForced);
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner {0} assigned job {1}.", partner.Name.ToStringShort, partnerFollowJob.def.defName));
                }
                else
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Initiator {0} has no valid joy job. Ending date for {1}.", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                    DatingManager.EndDate(initiator);
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            assignPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return assignPartnerJob;

            Toil waitForJoyJob = new Toil();
            waitForJoyJob.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering waitForJoyJob toil.");
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: waitForJoyJob.initAction started for Initiator: {0}, Partner: {1}", ((Pawn)this.job.targetA.Thing).Name.ToStringShort, ((Pawn)this.job.targetB.Thing).Name.ToStringShort));
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Starting waitForJoyJob toil.");
            };
            waitForJoyJob.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                if (initiator == null || partner == null) {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: waitForJoyJob.tickAction - Initiator: {0} (Job: {1}), Partner: {2} (Job: {3})", initiator.Name.ToStringShort, initiator.CurJob != null ? initiator.CurJob.def.defName : "None", partner.Name.ToStringShort, partner.CurJob != null ? partner.CurJob.def.defName : "None"));

                if (initiator.CurJob != null && initiator.CurJob.def != null && initiator.CurJob.def.joyKind == null && partner.CurJob != null && partner.CurJob.def != SI_JobDefOf.FollowAndWatchInitiator)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Joy job finished, advancing to next toil.");
                    this.ReadyForNextToil();
                }
                else
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: waitForJoyJob not advancing. Initiator JoyKind: {0}, Partner Job: {1}", initiator.CurJob != null && initiator.CurJob.def != null ? initiator.CurJob.def.joyKind.defName : "N/A", partner.CurJob != null && partner.CurJob.def != null ? partner.CurJob.def.defName : "N/A"));
                }
            };
            waitForJoyJob.defaultCompleteMode = ToilCompleteMode.Never;
            yield return waitForJoyJob;

            Toil advanceDate = new Toil();
            advanceDate.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering advanceDate toil.");
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Initiating advanceDate toil.");
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (initiator != null)
                {
                    DatingManager.AdvanceDateStage(initiator);
                    Date date = DatingManager.GetDateWith(initiator);
                    if (date != null && date.Stage == DateStage.Joy)
                    {
                        Log.Message("[SocialInteractions] JobDriver_GoOnDate: Date could not advance to Lovin' stage. Ending date.");
                        DatingManager.EndDate(initiator);
                    }
                }
            };
            advanceDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return advanceDate;

            Toil waitForLovinJob = new Toil();
            waitForLovinJob.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering waitForLovinJob toil.");
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Starting waitForLovinJob toil.");
            };
            waitForLovinJob.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                if (initiator == null || partner == null) {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

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
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering finishDate toil.");
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Initiating finishDate toil.");
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (initiator != null) DatingManager.AdvanceDateStage(initiator);
            };
            finishDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finishDate;

            // Cleanup toil: Ensure the date is ended regardless of how the job finishes
            Toil cleanup = new Toil();
            cleanup.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: Entering cleanup toil.");
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (initiator != null)
                {
                    DatingManager.EndDate(initiator);
                }
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Job finished for Initiator: {0}", initiator.Name.ToStringShort));
            };
            cleanup.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return cleanup;
        }
    }
}