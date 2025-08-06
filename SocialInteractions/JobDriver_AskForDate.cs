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

            Toil askToil = new Toil();
            askToil.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                float acceptanceChance = 0.5f + (recipient.relations.OpinionOf(this.pawn) / 200f);
                bool accepted = Rand.Value < acceptanceChance;

                if (accepted)
                {
                    Job initiatorJob = null;
                    var joyGiver = GetBestJoyGiver(this.pawn, recipient);
                    if (joyGiver != null)
                    {
                        initiatorJob = joyGiver.Worker.TryGiveJob(this.pawn);
                    }

                    if (initiatorJob != null)
                    {
                        this.pawn.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                        Job recipientJob = JobMaker.MakeJob(SI_InteractionDefOf.FollowAndWatchInitiator, this.pawn, initiatorJob.targetA.Thing);
                        if (recipient.jobs != null)
                        {
                            recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                        }

                        Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        // Fallback to walk
                        IntVec3 wanderRoot = this.pawn.Position;
                        if (!RCellFinder.TryFindRandomPawnEntryCell(out wanderRoot, this.pawn.Map, 0.5f))
                        {
                            wanderRoot = this.pawn.Position;
                        }
                        Job walkJob = new Job(JobDefOf.GotoWander, wanderRoot);
                        this.pawn.jobs.TryTakeOrderedJob(walkJob, JobTag.Misc);

                        Job recipientWalkJob = new Job(JobDefOf.Goto, wanderRoot);
                        if (recipient.jobs != null)
                        {
                            recipient.jobs.TryTakeOrderedJob(recipientWalkJob, JobTag.Misc);
                        }

                        Messages.Message(string.Format("{0} and {1} are now going for a walk together.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                    }
                }
                else
                {
                    // Date rejected
                }
            };
            askToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return askToil;
        }

        private JoyGiverDef GetBestJoyGiver(Pawn initiator, Pawn recipient)
        {
            return DefDatabase<JoyGiverDef>.AllDefsListForReading
                .Where(jg => jg.Worker.CanBeGivenTo(initiator) && jg.Worker.CanBeGivenTo(recipient))
                .InRandomOrder()
                .FirstOrDefault();
        }
    }
}