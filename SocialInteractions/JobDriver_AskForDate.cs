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
        private bool accepted = false;
        private Thing chosenJoySpot = null;
        private JoyGiverDef chosenJoyGiverDef = null;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: TryMakePreToilReservations for {0} and {1}", this.pawn.Name.ToStringShort, ((Pawn)this.job.targetA.Thing).Name.ToStringShort));
            return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref accepted, "accepted", false);
            Scribe_References.Look(ref chosenJoySpot, "chosenJoySpot");
            Scribe_Defs.Look(ref chosenJoyGiverDef, "chosenJoyGiverDef");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message(string.Format("[SocialInteractions] JobDriver_AskForDate: MakeNewToils for {0} and {1}", this.pawn.Name.ToStringShort, ((Pawn)this.job.targetA.Thing).Name.ToStringShort));
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Toil 1: Make the decision
            Toil decide = new Toil();
            decide.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                float acceptanceChance = 0.5f + (recipient.relations.OpinionOf(this.pawn) / 200f);
                this.accepted = Rand.Value < acceptanceChance;
                Log.Message(string.Format("[SocialInteractions] AskForDate: {0} asks {1} on a date. Acceptance chance: {2:P2}, Accepted: {3}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, acceptanceChance, this.accepted));

                if (this.accepted)
                {
                    try
                    {
                        var potentialSpots = FindJoySpotFor(this.pawn, recipient).ToList();
                        Log.Message(string.Format("[SocialInteractions] AskForDate: Found {0} potential joy spots.", potentialSpots.Count()));
                        if (potentialSpots.Any())
                        {
                            var chosenSpotAndGiver = potentialSpots.RandomElement();
                            this.chosenJoySpot = chosenSpotAndGiver.Item1;
                            this.chosenJoyGiverDef = chosenSpotAndGiver.Item2;
                            Log.Message(string.Format("[SocialInteractions] AskForDate: Chosen spot: {0} with giver {1}", this.chosenJoySpot.def.defName, this.chosenJoyGiverDef.defName));
                        }
                        else
                        {
                            Log.Message("[SocialInteractions] AskForDate: No suitable joy spots found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("[SocialInteractions] AskForDate: Exception in FindJoySpotFor: {0}", ex.Message));
                    }
                }
            };
            decide.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return decide;

            // Toil 2: Act on the decision
            Toil act = new Toil();
            act.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                if (this.accepted)
                {
                    this.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                    recipient.jobs.EndCurrentJob(JobCondition.InterruptForced, true);

                    if (this.chosenJoySpot != null)
                    {
                        Log.Message("[SocialInteractions] AskForDate: Assigning joy jobs.");
                        // Assign joy jobs
                        Job initiatorJob = new Job(this.chosenJoyGiverDef.jobDef, this.chosenJoySpot, recipient);
                        this.pawn.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);
                        Log.Message(string.Format("[SocialInteractions] AskForDate: Initiator job: {0} on {1} with {2}", initiatorJob.def.defName, initiatorJob.targetA.Thing.def.defName, recipient.Name.ToStringShort));

                        Job recipientJob = new Job(DefDatabase<JobDef>.GetNamed("FollowAndWatch"), this.pawn);
                        recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                        Log.Message(string.Format("[SocialInteractions] AskForDate: Recipient job: {0} on {1} with {2}", recipientJob.def.defName, this.pawn.Name.ToStringShort, this.pawn.Name.ToStringShort));

                        Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Log.Message("[SocialInteractions] AskForDate: Assigning fallback walk jobs.");
                        // Assign fallback walk jobs
                        IntVec3 wanderRoot = pawn.Position;
                        if (!RCellFinder.TryFindRandomPawnEntryCell(out wanderRoot, pawn.Map, 0.5f))
                        {
                            wanderRoot = pawn.Position;
                        }
                        Job initiatorJob = new Job(JobDefOf.GotoWander, wanderRoot);
                        this.pawn.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                        Job recipientJob = new Job(DefDatabase<JobDef>.GetNamed("FollowAndWatch"), this.pawn);
                        recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                        
                        Messages.Message(string.Format("{0} and {1} are now going for a walk together.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                    }
                }
                else
                {
                    Log.Message("[SocialInteractions] AskForDate: Date rejected.");
                    this.pawn.interactions.TryInteractWith(recipient, SI_InteractionDefOf.DateRejected);
                }
            };
            act.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return act;
        }

        private IEnumerable<Tuple<Thing, JoyGiverDef>> FindJoySpotFor(Pawn pawn, Pawn partner)
        {
            // 1. Find all social joy buildings on the map
            var socialJoyBuildings = pawn.Map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.GetStatValueAbstract(StatDefOf.JoyGainFactor) > 0 && b.def.building.joyKind == JoyKindDefOf.Social);

            foreach (var building in socialJoyBuildings)
            {
                // 2. Check if both pawns can reserve and reach the building
                if (pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.None))
                {
                    // 3. Find a suitable JoyGiverDef for this building
                    var suitableJoyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                        .Where(jg =>
                            jg.joyKind == JoyKindDefOf.Social &&
                            jg.thingDefs != null &&
                            jg.thingDefs.Contains(building.def) &&
                            jg.jobDef != JobDefOf.Lovin && // Exclude Lovin
                            jg.jobDef.defName != "VisitSickPawn" && // Exclude VisitSickPawn
                            jg.jobDef.defName != "StandAndChat" // Exclude StandAndChat
                        );

                    foreach (var giver in suitableJoyGivers)
                    {
                        yield return new Tuple<Thing, JoyGiverDef>(building, giver);
                    }
                }
            }
            yield break; // Return empty if no spots found
        }
    }
}
