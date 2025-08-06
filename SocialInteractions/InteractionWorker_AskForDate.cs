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
            if (initiator.jobs != null && initiator.jobs.curJob != null && (initiator.jobs.curJob.def.defName == "AskForDate" || initiator.jobs.curJob.def.defName == "FollowAndWatchInitiator")) return 0f;
            if (recipient.jobs != null && recipient.jobs.curJob != null && (recipient.jobs.curJob.def.defName == "AskForDate" || recipient.jobs.curJob.def.defName == "FollowAndWatchInitiator")) return 0f;
            if (initiator.needs == null || initiator.needs.joy == null) return 0f;
            if (initiator.needs.joy.CurLevelPercentage > 0.8f) return 0f;
            if (initiator.ageTracker.AgeBiologicalYearsFloat < 16f || recipient.ageTracker.AgeBiologicalYearsFloat < 16f) return 0f;
            if (initiator.relations.OpinionOf(recipient) < 10) return 0f;
            return 1.0f * Rand.Value;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("AskForDate"), recipient);
            initiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
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