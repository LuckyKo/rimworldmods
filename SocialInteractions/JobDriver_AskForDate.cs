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
        private Thing chosenJoySpot = null;
        private JoyGiverDef assignedJoyGiverDef = null;
        private bool joySpotFound = false;

        public JobDriver_AskForDate()
        {
            Log.Message("[SocialInteractions] JobDriver_AskForDate: Constructor called.");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: TryMakePreToilReservations for {0} and {1}", this.pawn.Name.ToStringShort, ((Pawn)this.job.targetA.Thing).Name.ToStringShort));
            this.chosenJoySpot = this.job.targetB.Thing;
            return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref chosenJoySpot, "chosenJoySpot");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: MakeNewToils for {0} and {1}", this.pawn.Name.ToStringShort, ((Pawn)this.job.targetA.Thing).Name.ToStringShort));
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: MakeNewToils - JobDef: {0}, TargetA: {1}, TargetB: {2}", this.job.def.defName, this.job.targetA.Thing.def.defName, (this.job.targetB.Thing != null) ? this.job.targetB.Thing.def.defName : "null"));
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: MakeNewToils - chosenJoySpot: {0}", (this.chosenJoySpot != null) ? this.chosenJoySpot.def.defName : "null"));
            this.FailOnDespawnedOrNull(TargetIndex.A); // Recipient
            this.FailOnDespawnedOrNull(TargetIndex.B); // ChosenJoySpot

            // Toil 1: Initiator goes to recipient
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Toil 2: Determine joy spot and assign initial jobs
            Toil determineJoySpotAndAssignJobs = new Toil();
            determineJoySpotAndAssignJobs.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;

                if (this.chosenJoySpot != null)
                {
                    this.assignedJoyGiverDef = GetJoyGiverDefForSpot(this.chosenJoySpot, this.pawn, recipient);
                    if (this.assignedJoyGiverDef == null)
                    {
                        Log.Error("[SocialInteractions] AskForDate: assignedJoyGiverDef is null. Cannot assign joy jobs. Ending date.");
                        this.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        recipient.jobs.EndCurrentJob(JobCondition.Incompletable);
                        this.joySpotFound = false;
                    }
                    else
                    {
                        this.joySpotFound = true;
                        Log.Message("[SocialInteractions] JobDriver_AskForDate: joySpotFound set to true.");
                    }
                }
            };
            determineJoySpotAndAssignJobs.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return determineJoySpotAndAssignJobs;

            // Define the walk fallback label
            Toil walkFallbackLabel = Toils_General.Label();

            // Jump to fallback if no joy spot was found
            yield return Toils_Jump.JumpIf(walkFallbackLabel, () => !this.joySpotFound);

            // Toil 3: Initiator goes to the chosen joy spot
            Log.Message("[SocialInteractions] JobDriver_AskForDate: Toil 3 - Initiator going to joy spot.");
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);

            // Toil 4: Recipient goes to a cell near the chosen joy spot
            Toil recipientGoToSpot = new Toil();
            recipientGoToSpot.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                IntVec3 watchCell = CellFinder.StandableCellNear(this.chosenJoySpot.Position, this.chosenJoySpot.Map, 5);
                if (!watchCell.IsValid) watchCell = this.chosenJoySpot.Position;

                Job recipientWatchJob = JobMaker.MakeJob(JobDefOf.Goto, watchCell);
                recipient.jobs.TryTakeOrderedJob(recipientWatchJob, JobTag.Misc);
            };
            recipientGoToSpot.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return recipientGoToSpot;

            // Toil 5: Initiator starts joy activity
            Toil initiatorStartJoy = new Toil();
            initiatorStartJoy.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                JoyGiverDef currentJoyGiverDef = this.assignedJoyGiverDef;
                
                Job initiatorJoyJob = JobMaker.MakeJob(currentJoyGiverDef.jobDef, this.chosenJoySpot, recipient);
                this.pawn.jobs.TryTakeOrderedJob(initiatorJoyJob, JobTag.Misc);
                Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
            };
            initiatorStartJoy.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initiatorStartJoy;

            // Toil 6: Wait for a duration (e.g., 2 seconds) for pawns to settle
            Toil waitToSettle = new Toil();
            waitToSettle.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToSettle.defaultDuration = 120; // 2 seconds
            yield return waitToSettle;

            // Toil 7: Wait for initiator's joy job to complete
            Toil waitForInitiatorJoy = new Toil();
            waitForInitiatorJoy.defaultCompleteMode = ToilCompleteMode.Delay;
            waitForInitiatorJoy.AddEndCondition(() =>
            {
                if (this.pawn.CurJob == null || this.pawn.CurJob.def != this.assignedJoyGiverDef.jobDef)
                {
                    Log.Message(string.Format("[SocialInteractions] AskForDate: Initiator {0} finished joy job. Ending date.", this.pawn.Name.ToStringShort));
                    return JobCondition.Succeeded;
                }
                return JobCondition.Ongoing;
            });
            yield return waitForInitiatorJoy;

            // Walk fallback toils
            yield return walkFallbackLabel;
            Toil walkToil = new Toil();
            walkToil.initAction = () =>
            {
                Log.Message("[SocialInteractions] AskForDate: Chosen joy spot is null, falling back to walk.");
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                IntVec3 wanderRoot = this.pawn.Position;
                if (!RCellFinder.TryFindRandomPawnEntryCell(out wanderRoot, this.pawn.Map, 0.5f))
                {
                    wanderRoot = this.pawn.Position;
                }
                Job initiatorWalkJob = new Job(JobDefOf.GotoWander, wanderRoot);
                this.pawn.jobs.TryTakeOrderedJob(initiatorWalkJob, JobTag.Misc);

                Job recipientWalkJob = new Job(JobDefOf.Goto, wanderRoot);
                recipient.jobs.TryTakeOrderedJob(recipientWalkJob, JobTag.Misc);
                
                Messages.Message(string.Format("{0} and {1} are now going for a walk together.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                this.pawn.jobs.EndCurrentJob(JobCondition.Succeeded); // End this job, pawns will walk
            };
            walkToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return walkToil;
        }

        private JoyGiverDef GetJoyGiverDefForSpot(Thing joySpot, Pawn initiator, Pawn recipient)
        {
            Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Searching for giver for joySpot {0} for initiator {1} and recipient {2}", joySpot.def.defName, initiator.Name.ToStringShort, recipient.Name.ToStringShort));

            foreach (var giver in DefDatabase<JoyGiverDef>.AllDefsListForReading)
            {
                Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Checking giver: {0}", giver.defName));
                if (giver == null || giver.Worker == null)
                {
                    Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Skipping {0}: giver or worker is null.", (giver != null) ? giver.defName : "null"));
                    continue;
                }
                if (giver.jobDef == null)
                {
                    Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Skipping {0}: jobDef is null.", giver.defName));
                    continue;
                }
                if (giver.thingDefs == null || !giver.thingDefs.Contains(joySpot.def))
                {
                    Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Skipping {0}: thingDefs is null or does not contain joySpot.def ({1}).", giver.defName, joySpot.def.defName));
                    continue;
                }

                // Check if both pawns can be given this joy
                try
                {
                    if (!giver.Worker.CanBeGivenTo(initiator))
                    {
                        Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Skipping {0}: initiator {1} cannot be given this joy.", giver.defName, initiator.Name.ToStringShort));
                        continue;
                    }
                    if (!giver.Worker.CanBeGivenTo(recipient))
                    {
                        Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Skipping {0}: recipient {1} cannot be given this joy.", giver.defName, recipient.Name.ToStringShort));
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Exception checking CanBeGivenTo for {0}: {1}", giver.defName, ex.Message));
                    continue;
                }
                Log.Message(string.Format("[SocialInteractions] GetJoyGiverDefForSpot: Found suitable giver: {0}", giver.defName));
                return giver;
            }
            Log.Message("[SocialInteractions] GetJoyGiverDefForSpot: No suitable giver found. Returning null.");
            return null;
        }
    }
}