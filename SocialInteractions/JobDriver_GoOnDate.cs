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

            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator
            this.FailOnDespawnedOrNull(TargetIndex.B); // Partner

            Toil assignJoyJob = new Toil();
            assignJoyJob.initAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Pawn partner = (Pawn)this.job.targetB.Thing;

                if (initiator == null || partner == null) {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Find a suitable joy spot for the date
                System.Collections.Generic.List<System.Tuple<Thing, JoyGiverDef, IntVec3>> joySpots = DatingManager.FindJoySpotFor(initiator, partner);

                if (joySpots != null && joySpots.Any())
                {
                    // For simplicity, pick the first available joy spot
                    System.Tuple<Thing, JoyGiverDef, IntVec3> chosenSpot = joySpots.First();
                    Thing joyThing = chosenSpot.Item1;
                    JoyGiverDef joyGiver = chosenSpot.Item2;
                    IntVec3 joyCell = chosenSpot.Item3;

                    if (joyGiver != null && joyGiver.jobDef != null && initiator.jobs != null && partner.jobs != null)
                    {
                        // Add checks for joyThing validity
                        if (joyThing == null || !joyThing.Spawned || !joyThing.Position.IsValid || joyThing.Map == null || !initiator.CanReserve(joyThing) || joyThing.Destroyed)
                        {
                            Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Invalid joyThing found. JoyThing: {0}, Spawned: {1}, Position Valid: {2}, Map: {3}, Destroyed: {4}. Ending date.", joyThing != null ? joyThing.LabelShort : "NULL", joyThing != null ? joyThing.Spawned.ToString() : "NULL", joyThing != null ? joyThing.Position.IsValid.ToString() : "NULL", joyThing != null && joyThing.Map != null ? joyThing.Map.ToString() : "NULL", joyThing != null ? joyThing.Destroyed.ToString() : "NULL"));
                            DatingManager.EndDate(initiator);
                            this.EndJobWith(JobCondition.Incompletable);
                            Messages.Message(string.Format("{0} tried to go on a date with {1}, but the joy spot was invalid.", initiator.LabelShort, partner.LabelShort), new LookTargets(initiator, partner), MessageTypeDefOf.NegativeEvent);
                            return; // Exit initAction
                        }

                        // NEW CHECK: Ensure joyThing is not destroyed and still on a map
                        if (joyThing.Destroyed || joyThing.Map == null)
                        {
                            Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: JoyThing {0} is destroyed or has no map. Ending date.", joyThing.LabelShort));
                            DatingManager.EndDate(initiator);
                            this.EndJobWith(JobCondition.Incompletable);
                            Messages.Message(string.Format("{0} tried to go on a date with {1}, but the joy spot was no longer available.", initiator.LabelShort, partner.LabelShort), new LookTargets(initiator, partner), MessageTypeDefOf.NegativeEvent);
                            return; // Exit initAction
                        }

                        Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Assigning initiator joy job. JoyGiver: {0}, JoyThing: {1}, Position: {2}, Map: {3}", joyGiver.defName, joyThing.LabelShort, joyThing.Position, joyThing.Map));
                        Log.Message(string.Format("[SocialInteractions] JoyThing Details: X={0}, Y={1}, Z={2}, MapSizeX={3}, MapSizeZ={4}, InBounds={5}, JoyCell: {6}", joyThing.Position.x, joyThing.Position.y, joyThing.Position.z, joyThing.Map.Size.x, joyThing.Map.Size.z, joyThing.Position.InBounds(joyThing.Map), joyCell));
                        // Assign joy job to initiator
                        Job initiatorJoyJob = JobMaker.MakeJob(joyGiver.jobDef, joyThing, joyCell);
                        try
                        {
                            initiator.jobs.StartJob(initiatorJoyJob, JobCondition.InterruptForced);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("[SocialInteractions] JobDriver_GoOnDate: Exception assigning initiator joy job for {0} with {1}: {2}. Ending date.", initiator.LabelShort, joyThing.LabelShort, ex.Message));
                            DatingManager.EndDate(initiator);
                            this.EndJobWith(JobCondition.Incompletable);
                            Messages.Message(string.Format("{0} tried to go on a date with {1}, but encountered an error assigning a joy job.", initiator.LabelShort, partner.LabelShort), new LookTargets(initiator, partner), MessageTypeDefOf.NegativeEvent);
                            return; // Exit initAction
                        }

                        Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Assigning partner follow job. Initiator: {0}, JoyThing: {1}", initiator.LabelShort, joyThing.LabelShort));
                        // Assign follow and watch job to partner
                        partner.jobs.StartJob(JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator, joyThing), JobCondition.InterruptForced);
                        Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner {0} assigned job {1}.", partner.LabelShort, SI_JobDefOf.FollowAndWatchInitiator.defName));

                        // Check if the partner actually took the FollowAndWatchInitiator job
                        if (partner.CurJobDef != SI_JobDefOf.FollowAndWatchInitiator)
                        {
                            Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner {0} failed to take FollowAndWatchInitiator job. Current job: {1}. Ending date.", partner.LabelShort, partner.CurJobDef != null ? partner.CurJobDef.defName : "None"));
                            DatingManager.EndDate(initiator);
                            this.EndJobWith(JobCondition.Incompletable);
                            Messages.Message(string.Format("{0} tried to go on a date with {1}, but {1} was too busy.", initiator.LabelShort, partner.LabelShort), new LookTargets(initiator, partner), MessageTypeDefOf.NegativeEvent);
                            return; // Exit initAction
                        }

                        
                    }
                    else
                    {
                        Log.Message("[SocialInteractions] JobDriver_GoOnDate: Invalid joyGiver or jobs. Ending date.");
                        DatingManager.EndDate(initiator);
                        this.EndJobWith(JobCondition.Incompletable);
                    }
                }
                else
                {
                    // If no joy spot is found, end the date
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: No suitable joy spot found. Ending date.");
                    DatingManager.EndDate(initiator);
                    this.EndJobWith(JobCondition.Incompletable);
                }
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

                if (initiator == null || partner == null) {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (initiator.CurJob != null && initiator.CurJob.def != null && initiator.CurJob.def.joyKind == null && partner.CurJob != null && partner.CurJob.def != SI_JobDefOf.FollowAndWatchInitiator)
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
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (initiator != null) DatingManager.AdvanceDateStage(initiator);
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
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                if (initiator != null)
                {
                    DatingManager.EndDate(initiator);
                }
            };
            cleanup.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return cleanup;
        }
    }
}