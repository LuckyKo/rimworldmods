using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Collections.Generic;
using System;

namespace SocialInteractions
{
    public class JobDriver_AskForDate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: MakeNewToils started for Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, ((Pawn)this.job.targetA.Thing).Name.ToStringShort));
            this.FailOnDespawnedOrNull(TargetIndex.A); // Recipient

            // Add this check at the very beginning of the job driver
            Toil initialCheck = new Toil();
            initialCheck.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                if (this.job.targetA == null || this.job.targetA.Thing == null)
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Aborting job due to null recipient. Job: {0}", this.job));
                    this.EndJobWith(JobCondition.Incompletable);
                    return; // Exit the initAction
                }
                if (DatingManager.IsOnDate(this.pawn) || DatingManager.IsOnDate(recipient))
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Aborting job due to existing date. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                    this.EndJobWith(JobCondition.Incompletable); // End the job immediately
                }
            };
            initialCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initialCheck;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell); // Go to Recipient

            Job initiatorJob = null;

            Toil askToil = new Toil();
            askToil.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                if (this.pawn == null || recipient == null) return;
                Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));

                float acceptanceChance = 0.5f;
                if (recipient.relations != null)
                {
                    acceptanceChance += recipient.relations.OpinionOf(this.pawn) / 200f;
                }
                bool accepted = Rand.Value < acceptanceChance;
                Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Acceptance Chance: {0}, Accepted: {1}", acceptanceChance, accepted));

                if (accepted)
                {
                    // Instead of assigning joy job directly, assign JobDriver_GoOnDate
                    JobDef goOnDateJobDef = DefDatabase<JobDef>.GetNamed("GoOnDate");
                    if (goOnDateJobDef != null)
                    {
                        Job goOnDateJob = JobMaker.MakeJob(goOnDateJobDef, this.pawn, recipient);
                        this.pawn.jobs.TryTakeOrderedJob(goOnDateJob, JobTag.Misc);
                        Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Assigned JobDriver_GoOnDate to Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));

                        // The rest of the logic (StartDate, Hediffs, Messages, SpeechBubble) should be handled by JobDriver_GoOnDate
                        // However, for immediate feedback, we can keep the message and speech bubble here.
                        Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                        string subject = SpeechBubbleManager.GetDateSubject(this.pawn, recipient, new LocalTargetInfo(this.pawn.Position)); // Simplified subject for now
                        SocialInteractions.HandleNonStoppingInteraction(this.pawn, recipient, SI_InteractionDefOf.DateAccepted, subject);
                    }
                    else
                    {
                        Log.Error("[SocialInteractions] JobDriver_AskForDate: 'GoOnDate' JobDef not found. Cannot assign date job.");
                    }
                }
                else
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Date rejected. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                    // Date rejected
                }
            };
            askToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return askToil;


            }

        private Job GetBestJoyJob(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null) return null;
            List<JoyGiverDef> possibleJoyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                .Where(jg => jg != null && jg.Worker != null && jg.Worker.CanBeGivenTo(initiator) && jg.Worker.CanBeGivenTo(recipient))
                .InRandomOrder()
                .ToList();

            foreach (JoyGiverDef joyGiver in possibleJoyGivers)
            {
                Job job = joyGiver.Worker.TryGiveJob(initiator);
                if (job != null && job.targetA.IsValid)
                {
                    return job;
                }
            }
            return null;
        }
    }
}