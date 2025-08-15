using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SocialInteractions
{
    public class JobDriver_GoOnDate : JobDriver
    {
        private Pawn Partner
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }

        private Thing JoySpot
        {
            get { return this.job.targetB.Thing; }
        }

        private JoyGiverDef joyGiverDef;
        private int joyDuration;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref this.joyGiverDef, "joyGiverDef");
            Scribe_Values.Look(ref this.joyDuration, "joyDuration", 0);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            // Add null checks to prevent NullReferenceException
            string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
            string partnerName = (this.Partner != null && this.Partner.Name != null) ? this.Partner.Name.ToStringShort : "NULL";
            Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Starting job for {0} to date {1}.", pawnName, partnerName));
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null checks to prevent NullReferenceException
            string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
            string partnerName = (this.Partner != null && this.Partner.Name != null) ? this.Partner.Name.ToStringShort : "NULL";
            Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: TryMakePreToilReservations for {0} to date {1}.", pawnName, partnerName));
            return true;
        }

        

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Partner

            Toil rangeCheck = new Toil();
            rangeCheck.initAction = () =>
            {
                Pawn recipient = this.Partner;
                // Add null checks
                if (recipient == null || this.pawn == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in rangeCheck, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                int maxDistance = 50; // 50x50 tiles
                if ((Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)) > maxDistance)
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Aborting job. Recipient {0} is too far from initiator {1}. Distance: {2}, Max Distance: {3}", recipient.Name.ToStringShort, this.pawn.Name.ToStringShort, (Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)), maxDistance));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            rangeCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return rangeCheck;

            // Go to the partner
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Ask for the date
            Toil askToil = new Toil();
            askToil.initAction = () => {
                Pawn recipient = this.Partner;
                // Add comprehensive null checks
                if (this.pawn == null || recipient == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in askToil, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                float acceptanceChance = 0.5f;
                if (recipient.relations != null)
                {
                    int opinion = recipient.relations.OpinionOf(this.pawn);
                    acceptanceChance += opinion / 200f;
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Calculating acceptance chance for {0} asking {1}. Base: 0.5, Opinion: {2}, Final: {3}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, opinion, acceptanceChance));
                }
                bool accepted = Rand.Value < acceptanceChance;
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Acceptance roll for {0} asking {1}. Rolled: {2}, Chance: {3}, Accepted: {4}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, Rand.Value, acceptanceChance, accepted));

                if (accepted)
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Date accepted between {0} and {1}.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort));
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateAccepted"), this.pawn, this.Partner, null));
                    DatingManager.StartDate(this.pawn, this.Partner);
                    Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort), new LookTargets(this.pawn, this.Partner), MessageTypeDefOf.PositiveEvent);
                    // LLM interaction will be triggered after the joy spot is found in findSpotAndAssign
                }
                else
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Date rejected between {0} and {1}.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort));
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateRejected"), this.pawn, this.Partner, null));
                    DatingManager.RejectDate(this.pawn, this.Partner);
                    SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateRejected, "date");
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            askToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return askToil;

            // Find a joy spot and assign jobs
            Toil findSpotAndAssign = new Toil();
            findSpotAndAssign.initAction = () =>
            {
                // Add null checks
                if (this.pawn == null || this.Partner == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or Partner is null in findSpotAndAssign, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: findSpotAndAssign initAction called for pawn {0}.", this.pawn != null ? this.pawn.Name.ToStringShort : "NULL"));
                var joySpots = DatingManager.FindJoySpotFor(this.pawn, this.Partner);
                if (joySpots == null || !joySpots.Any())
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                var chosenSpot = joySpots.First();
                this.job.targetB = chosenSpot.Item1; // Set the joy spot as TargetB
                this.pawn.Reserve(this.job.targetB, this.job);

                JoyGiverDef joyGiver = chosenSpot.Item2;
                IntVec3 interactionCell = chosenSpot.Item3;

                // --- Trigger LLM Interaction with correct subject ---
                // Now that we have the joy spot, we can generate a proper subject.
                string dateSubject = SpeechBubbleManager.GetDateSubject(this.pawn, this.Partner, this.job.targetB);
                SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateAccepted, dateSubject);
                // --- End LLM Interaction ---

                // The GoOnDate job itself will handle the joy activity in the subsequent toils.
                // We just need to make sure the job's targets are set correctly.
                // The initiator's job is NOT replaced here.
                this.joyGiverDef = joyGiver;

                Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, this.pawn, this.job.targetB);
                this.Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);

                this.joyDuration = Rand.Range(1000, 2000);
            };
            findSpotAndAssign.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findSpotAndAssign;

            // Go to the joy spot
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);

            // Do the joy activity
            Toil doJoy = new Toil();
            doJoy.initAction = () =>
            {
                // Add null check
                if (this.pawn == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn is null in doJoy initAction, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: doJoy init for {0}.", this.pawn.Name.ToStringShort));
                this.startTick = Find.TickManager.TicksGame;
            };
            doJoy.defaultCompleteMode = ToilCompleteMode.Never;
            doJoy.tickAction = () =>
            {
                Pawn initiator = this.pawn;
                Pawn partner = this.Partner;
                Thing joySpot = this.JoySpot;

                // Add comprehensive null checks
                if (initiator == null || partner == null || joySpot == null)
                {
                    Log.Warning("[SocialInteractions] JobDriver_GoOnDate: doJoy - initiator, partner, or joySpot is null. Ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                initiator.rotationTracker.FaceCell(joySpot.InteractionCell);

                if (this.joyGiverDef != null)
                {
                    // Manual joy gain
                    initiator.needs.joy.GainJoy(this.joyGiverDef.jobDef.joyGainRate * 0.36f / 2500f, this.joyGiverDef.joyKind);

                    // Manual skill learn
                    if (this.joyGiverDef.jobDef.joySkill != null)
                    {
                        initiator.skills.GetSkill(this.joyGiverDef.jobDef.joySkill).Learn(this.joyGiverDef.jobDef.joyXpPerTick);
                    }
                }

                // Check for time limits
                int ticksPassed = Find.TickManager.TicksGame - this.startTick;

                if (ticksPassed > this.joyDuration) // Use random joyDuration
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Max duration ({0} ticks) passed. Advancing to next stage.", this.joyDuration));
                    this.ReadyForNextToil();
                    return;
                }

                // Check if the partner is still around and on the date
                JobDef followAndWatchJobDef = SI_JobDefOf.FollowAndWatchInitiator;
                if (partner.CurJobDef != followAndWatchJobDef && !DatingManager.IsOnDate(partner))
                {
                    Log.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner ({0}) is no longer following or on the date. Ending date.", partner.LabelShort));
                    this.ReadyForNextToil();
                    return;
                }
            };
            yield return doJoy;

            // Advance the date stage
            Toil advanceDate = new Toil();
            advanceDate.initAction = () =>
            {
                // Add null check
                if (this.pawn == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn is null in advanceDate, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: advanceDate toil initAction STARTING.");
                DatingManager.WasDateStageAdvancedByJob = true; // Set the flag
                DatingManager.AdvanceDateStage(this.pawn);
                Log.Message("[SocialInteractions] JobDriver_GoOnDate: advanceDate toil initAction FINISHED.");
            };
            advanceDate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return advanceDate;
        }
    }
}