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

            // Add this check at the very beginning of the job driver
            Toil initialCheck = new Toil();
            initialCheck.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                if (DatingManager.IsOnDate(this.pawn) || DatingManager.IsOnDate(recipient))
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Aborting job due to existing date. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                    this.EndJobWith(JobCondition.Incompletable); // End the job immediately
                }
            };
            initialCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initialCheck;

            // New range check
            Toil rangeCheck = new Toil();
            rangeCheck.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                int maxDistance = 50; // 50x50 tiles
                if ((Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)) > maxDistance)
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Aborting job. Recipient {0} is too far from initiator {1}. Distance: {2}, Max Distance: {3}", recipient.Name.ToStringShort, this.pawn.Name.ToStringShort, (Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)), maxDistance));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            rangeCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return rangeCheck;

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
                    initiatorJob = GetBestJoyJob(this.pawn, recipient);

                    if (initiatorJob != null && initiatorJob.targetA.IsValid)
                    {
                        Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Assigning main jobs. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                        if (this.pawn.jobs != null) this.pawn.jobs.StartJob(initiatorJob, JobCondition.InterruptForced);
                        Log.Message(string.Format("[SocialInteractions] Initiator {0} assigned job {1}", this.pawn.Name.ToStringShort, initiatorJob.def.defName));

                        DatingManager.StartDate(this.pawn, recipient);

                        if (SI_InteractionDefOf.DateAccepted != null && Find.PlayLog != null)
                        {
                            Find.PlayLog.Add(new PlayLogEntry_Interaction(SI_InteractionDefOf.DateAccepted, this.pawn, recipient, null));
                        }

                        Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                        string subject = SpeechBubbleManager.GetDateSubject(this.pawn, recipient, initiatorJob.targetA.Thing != null ? initiatorJob.targetA.Thing : new LocalTargetInfo(this.pawn.Position));
                        SocialInteractions.HandleNonStoppingInteraction(this.pawn, recipient, SI_InteractionDefOf.DateAccepted, subject);

                        // Assign recipient's job immediately
                        if (recipient.jobs != null && this.pawn != null && initiatorJob.targetA.IsValid)
                        {
                            Job recipientJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, this.pawn, initiatorJob.targetA);
                            recipient.jobs.StartJob(recipientJob, JobCondition.InterruptForced);
                        }
                    }
                    else
                    {
                        Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: Falling back to FollowAndWatchInitiator. Initiator: {0}, Recipient: {1}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                        // Fallback to FollowAndWatchInitiator
                        Job initiatorWaitJob = new Job(JobDefOf.Wait, this.pawn.Position);
                        if (this.pawn.jobs != null) this.pawn.jobs.StartJob(initiatorWaitJob, JobCondition.InterruptForced);
                        Log.Message(string.Format("[SocialInteractions] Initiator {0} assigned wait job {1}", this.pawn.Name.ToStringShort, initiatorWaitJob.def.defName));

                        Job recipientFollowJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, this.pawn, this.pawn.Position);
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
                if (this.pawn == null || recipient == null) return;
                DatingManager.EndDate(this.pawn);
            };
            finalToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalToil;
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