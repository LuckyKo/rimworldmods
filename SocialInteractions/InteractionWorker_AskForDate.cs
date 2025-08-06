using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SocialInteractions
{
    public class InteractionWorker_AskForDate : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (initiator == recipient) return 0f;
            if (initiator.jobs != null && initiator.jobs.curJob != null && initiator.jobs.curJob.def.defName == "OnDate") return 0f;
            if (recipient.jobs != null && recipient.jobs.curJob != null && recipient.jobs.curJob.def.defName == "OnDate") return 0f;
            if (initiator.ageTracker.AgeBiologicalYearsFloat < 16f || recipient.ageTracker.AgeBiologicalYearsFloat < 16f) return 0f;
            if (initiator.relations.OpinionOf(recipient) < 10) return 0f;
            return 1.0f * Rand.Value;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            Log.Message(string.Format("[SocialInteractions] InteractionWorker_AskForDate: {0} asked {1} on a date.", initiator.Name.ToStringShort, recipient.Name.ToStringShort));
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            float acceptanceChance = 0.5f + (recipient.relations.OpinionOf(initiator) / 200f);
            bool accepted = Rand.Value < acceptanceChance;
            Log.Message(string.Format("[SocialInteractions] AskForDate: {0} asks {1} on a date. Acceptance chance: {2:P2}, Accepted: {3}", initiator.Name.ToStringShort, recipient.Name.ToStringShort, acceptanceChance, accepted));

            if (accepted)
            {
                Tuple<Thing, JoyGiverDef> chosenSpotAndGiver = null;
                try
                {
                    var potentialSpots = FindJoySpotFor(initiator, recipient).ToList();
                    Log.Message(string.Format("[SocialInteractions] AskForDate: Found {0} potential joy spots.", potentialSpots.Count()));
                    if (potentialSpots.Any())
                    {
                        chosenSpotAndGiver = potentialSpots.RandomElement();
                        Log.Message(string.Format("[SocialInteractions] AskForDate: Chosen spot: {0} with giver {1}", chosenSpotAndGiver.Item1.def.defName, chosenSpotAndGiver.Item2.defName));
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

                if (chosenSpotAndGiver != null)
                {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("AskForDate"), recipient, chosenSpotAndGiver.Item1);
                    bool jobAssigned = initiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    Log.Message(string.Format("[SocialInteractions] AskForDate: Job assigned: {0}. Initiator current job: {1}", jobAssigned, (initiator.CurJob != null) ? initiator.CurJob.def.defName : "None"));
                    if (!jobAssigned)
                    {
                        Log.Error(string.Format("[SocialInteractions] AskForDate: Failed to assign job to initiator {0}. Current job: {1}", initiator.Name.ToStringShort, (initiator.CurJob != null) ? initiator.CurJob.def.defName : "None"));
                    }
                    Messages.Message(string.Format("{0} and {1} are now going on a date.", initiator.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(initiator, recipient), MessageTypeDefOf.PositiveEvent);

                }
                else
                {
                    Log.Message("[SocialInteractions] AskForDate: No suitable joy spot found, falling back to walk.");
                    // Fallback to walk if no joy spot found
                    IntVec3 wanderRoot = initiator.Position;
                    if (!RCellFinder.TryFindRandomPawnEntryCell(out wanderRoot, initiator.Map, 0.5f))
                    {
                        wanderRoot = initiator.Position;
                    }
                    Job initiatorJob = new Job(JobDefOf.GotoWander, wanderRoot);
                    initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                    Job recipientJob = new Job(JobDefOf.Goto, wanderRoot);
                    recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                    
                    Messages.Message(string.Format("{0} and {1} are now going for a walk together.", initiator.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(initiator, recipient), MessageTypeDefOf.PositiveEvent);
 // Still consider it accepted, just a walk
                }
            }
            else
            {
                Log.Message("[SocialInteractions] AskForDate: Date rejected.");
                
            }
        }

        private List<Tuple<Thing, JoyGiverDef>> FindJoySpotFor(Pawn pawn, Pawn partner)
        {
            

            List<Tuple<Thing, JoyGiverDef>> foundSpots = new List<Tuple<Thing, JoyGiverDef>>();

            // 1. Filter for suitable social JoyGiverDefs
            List<JoyGiverDef> suitableJoyGivers = new List<JoyGiverDef>();
            try
            {
                
                suitableJoyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                    .Where(jg =>
                    {
                        
                        if (jg.jobDef == null)
                        {
                            
                            return false;
                        }
                        if (jg.jobDef == JobDefOf.Lovin) { return false; }
                        if (jg.jobDef.defName == "VisitSickPawn") { return false; }
                        if (jg.jobDef.defName == "StandAndChat") { return false; }
                        if (jg.thingDefs == null || !jg.thingDefs.Any()) { return false; }
                        
                        if (jg.Worker == null)
                        {
                            
                            return false;
                        }

                        try
                        {
                            if (!jg.Worker.CanBeGivenTo(pawn)) { return false; }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception checking CanBeGivenTo for initiator on {0}: {1}", jg.defName, ex.Message));
                            return false;
                        }

                        try
                        {
                            if (!jg.Worker.CanBeGivenTo(partner)) { return false; }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception checking CanBeGivenTo for partner on {0}: {1}", jg.defName, ex.Message));
                            return false;
                        }
                        return true;
                    }).ToList();
                
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception during JoyGiverDef filtering query definition: {0}", ex.Message));
                // suitableJoyGivers will remain Enumerable.Empty<JoyGiverDef>()
            }

            

            

            try
            {
                foreach (var giver in suitableJoyGivers)
                {
                    
                    

                    // 2. For each JoyGiverDef, find compatible buildings on the map
                    if (giver.thingDefs != null)
                    {
                        foreach (var thingDef in giver.thingDefs)
                        {
                            
                            

                            var potentialBuildings = pawn.Map.listerBuildings.allBuildingsColonist
                                .Where(b =>
                                    b != null && // Ensure building is not null
                                    b.def == thingDef && // Must be the specific ThingDef for this giver
                                    b.def.GetStatValueAbstract(StatDefOf.JoyGainFactor) > 0 && // Must provide joy
                                    pawn.CanReserveAndReach(b, PathEndMode.InteractionCell, Danger.None) && // Initiator can reserve and reach
                                    partner.CanReserveAndReach(b, PathEndMode.InteractionCell, Danger.None) // Partner can reserve and reach
                                );

                            foreach (var building in potentialBuildings)
                            {
                                
                                foundSpots.Add(new Tuple<Thing, JoyGiverDef>(building, giver));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception during iteration through suitable JoyGiverDefs: {0}", ex.Message));
            }
            
            return foundSpots;
        }
    }
}