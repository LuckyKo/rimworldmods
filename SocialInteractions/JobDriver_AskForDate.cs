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
            this.FailOnDespawnedOrNull(TargetIndex.A); // Recipient

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell); // Go to Recipient

            Job initiatorJob = null;

            Toil askToil = new Toil();
            askToil.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));

                float acceptanceChance = 0.5f + (recipient.relations.OpinionOf(this.pawn) / 200f);
                bool accepted = Rand.Value < acceptanceChance;
                Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Acceptance Chance: {0}, Accepted: {1}", acceptanceChance, accepted));

                if (accepted)
                {
                    initiatorJob = GetBestJoyJob(this.pawn, recipient);

                    if (initiatorJob != null && initiatorJob.targetA.IsValid)
                    {
                        Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Assigning main jobs. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                        this.pawn.jobs.StartJob(initiatorJob, JobCondition.InterruptForced);
                        Log.Message(string.Format("[SocialInteractions] Initiator {0} assigned job {1}", this.pawn.Name.ToStringShort, initiatorJob.def.defName));

                        LongEventHandler.QueueLongEvent(delegate
                        {
                            this.pawn.health.AddHediff(HediffDef.Named("OnDate"));
                            recipient.health.AddHediff(HediffDef.Named("OnDate"));
                        }, "AddOnDateHediffs", false, null);
                        DatingManager.StartDate(this.pawn, recipient);

                        Find.PlayLog.Add(new PlayLogEntry_Interaction(SI_InteractionDefOf.DateAccepted, this.pawn, recipient, null));

                        Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                        string subject = SpeechBubbleManager.GetDateSubject(this.pawn, recipient);
                        SocialInteractions.HandleNonStoppingInteraction(this.pawn, recipient, SI_InteractionDefOf.DateAccepted, subject);

                        // Assign recipient's job after a short delay
                        LongEventHandler.QueueLongEvent(delegate
                        {
                            if (recipient.jobs != null)
                            {
                                Job recipientJob = JobMaker.MakeJob(SI_InteractionDefOf.FollowAndWatchInitiator, this.pawn, initiatorJob.targetA.Thing);
                                recipient.jobs.StartJob(recipientJob, JobCondition.InterruptForced);
                                Log.Message(string.Format("[SocialInteractions] Recipient {0} assigned job {1} after delay", recipient.Name.ToStringShort, recipientJob.def.defName));
                            }
                        }, "AssignRecipientJob", false, null);
                    }
                    else
                    {
                        Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Falling back to FollowAndWatchInitiator. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                        // Fallback to FollowAndWatchInitiator
                        Job initiatorWaitJob = new Job(JobDefOf.Wait, this.pawn.Position);
                        this.pawn.jobs.StartJob(initiatorWaitJob, JobCondition.InterruptForced);
                        Log.Message(string.Format("[SocialInteractions] Initiator {0} assigned wait job {1}", this.pawn.Name.ToStringShort, initiatorWaitJob.def.defName));

                        Job recipientFollowJob = JobMaker.MakeJob(SI_InteractionDefOf.FollowAndWatchInitiator, this.pawn);
                        if (recipient.jobs != null)
                        {
                            recipient.jobs.StartJob(recipientFollowJob, JobCondition.InterruptForced);
                            Log.Message(string.Format("[SocialInteractions] Recipient {0} assigned job {1}", recipient.Name.ToStringShort, recipientFollowJob.def.defName));
                        }
                        Messages.Message(string.Format("{0} and {1} are now going on a date (fallback: following and watching).", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
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


            // Add a final action to remove the OnDate hediffs
            Toil finalToil = new Toil();
            finalToil.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Cleaning up OnDate hediffs for {0} and {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                Hediff hediff = this.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                if (hediff != null)
                {
                    this.pawn.health.RemoveHediff(hediff);
                }
                Hediff recipientHediff = recipient.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                if (recipientHediff != null)
                {
                    recipient.health.RemoveHediff(recipientHediff);
                }
                DatingManager.EndDate(this.pawn);
            };
            finalToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalToil;
        }

        private Job GetBestJoyJob(Pawn initiator, Pawn recipient)
        {
            List<JoyGiverDef> possibleJoyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                .Where(jg => jg.Worker.CanBeGivenTo(initiator) && jg.Worker.CanBeGivenTo(recipient))
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