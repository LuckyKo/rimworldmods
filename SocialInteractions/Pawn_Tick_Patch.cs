using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Pawn_Tick_Patch
    {
        // Keep track of pawns we've already caught cheating to prevent repeated detections
        private static HashSet<string> caughtCheaters = new HashSet<string>();
        
        public static void Postfix(Pawn __instance)
        {
            Pawn pawn = __instance;
            if (pawn == null || pawn.relations == null || pawn.Map == null)
            {
                return;
            }
            
            // Only check for cheating once per second (60 ticks) instead of every tick
            // Check from the perspective of pawns who are on a date
            if (pawn.IsHashIntervalTick(60))
            {
                // Check if this pawn is on a date
                if (DatingManager.IsOnDate(pawn))
                {
                    // Check if this pawn is currently engaged in the lovin activity
                    // We need to check if the pawn is actually doing the lovin, not just pathing to the spot
                    if (pawn.CurJobDef == SI_JobDefOf.DateLovin && pawn.jobs != null && pawn.jobs.curDriver != null)
                    {
                        // Check if the pawn is actually in the lovin toil, not just pathing to the spot
                        // The lovin toil is the second toil in the JobDriver_DateLovin
                        if (pawn.jobs.curDriver.CurToilIndex >= 2) // Index 0 is Goto, Index 1 is WaitForPartner, Index 2+ is Lovin
                        {
                            // Create a unique identifier for this pawn to prevent repeated detections
                            string pawnId = pawn.ThingID;
                            
                            // Only proceed if we haven't already caught this pawn cheating
                            if (!caughtCheaters.Contains(pawnId))
                            {
                                // Get the partner this pawn is on a date with
                                Pawn datePartner = DatingManager.GetPartnerOfDateWith(pawn);
                                
                                // If we have a date partner, check if we're cheating
                                if (datePartner != null)
                                {
                                    // Check if this pawn has an official romantic partner
                                    Pawn officialPartner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
                                    if (officialPartner == null)
                                    {
                                        officialPartner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance);
                                    }
                                    if (officialPartner == null)
                                    {
                                        officialPartner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
                                    }
                                    
                                    // If the pawn has an official partner and it's not the same as the date partner, we're cheating
                                    if (officialPartner != null && officialPartner != datePartner)
                                    {
                                        // Check if the official partner is nearby (within 5 tiles)
                                        if (officialPartner.Position.InHorDistOf(pawn.Position, 5f))
                                        {
                                            // Add this pawn to the caught cheaters list to prevent repeated detections
                                            caughtCheaters.Add(pawnId);
                                            
                                            // We found a cheater! The pawn is on a date with someone other than their official partner
                                            // and their official partner is nearby to witness it
                                            SLog.Message(string.Format(
                                                "[SocialInteractions] Cheating detected: {0} is on a date with {1} but is married to {2} who is nearby", 
                                                pawn.LabelShort, datePartner.LabelShort, officialPartner.LabelShort));
                                            
                                            // Register the interaction in the social log first
                                            Find.PlayLog.Add(new PlayLogEntry_Interaction(
                                                SI_InteractionDefOf.CaughtCheating, officialPartner, pawn, null));
                                            
                                            // Trigger the special cheating event
                                            HandleCheatingEvent(officialPartner, pawn, datePartner);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Periodically clean up the caught cheaters list to prevent memory leaks
            // Clean up every 1800 ticks (30 seconds)
            if (Current.Game.tickManager.TicksGame % 1800 == 0)
            {
                caughtCheaters.Clear();
            }
        }
        
        private static void HandleCheatingEvent(Pawn angryPartner, Pawn cheater, Pawn datePartner)
        {
            // Show a top screen notice message when cheating is discovered
            string message = string.Format("{0} caught {1} cheating with {2}!", 
                angryPartner.LabelShort, cheater.LabelShort, datePartner.LabelShort);
            Messages.Message(message, new LookTargets(angryPartner, cheater), MessageTypeDefOf.NegativeEvent);

            // Store the date partner for the cheater so it can be retrieved later
            SocialInteractions.CheaterPartners[cheater.ThingID] = datePartner;

            // Ensure the angry partner goes to the cheater
            if (angryPartner.Spawned && cheater.Spawned && angryPartner.Map == cheater.Map)
            {
                // Create a job for the angry partner to go to the cheater
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, cheater);
                gotoJob.checkOverrideOnExpire = false;
                gotoJob.expiryInterval = 300; // 5 seconds
                gotoJob.collideWithPawns = true;
                
                // Start the job with InterruptForced to ensure it interrupts current activities
                angryPartner.jobs.StartJob(gotoJob, JobCondition.InterruptForced);
            }

            // Hold only the cheater in place during the dialogue
            // The angry partner will be held in place after they arrive
            // The date partner should be free to move (flee)
            HoldPawnInPlace(cheater, cheater.Position);
            
            // Instead of directly calling HandleNonStoppingInteraction, let's trigger the interaction worker
            // This will handle the thoughts and social fights, and then we can add our LLM interaction
            // The date will be ended by the InteractionWorker_CaughtCheating after the LLM interaction is triggered
            InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
            string letterText, letterLabel;
            LetterDef letterDef;
            LookTargets lookTargets;
            interactionWorker.Interacted(angryPartner, cheater, new List<RulePackDef>(), out letterText, out letterLabel, out letterDef, out lookTargets);
        }
        
        private static string GetRelationshipLabel(Pawn pawn, Pawn partner)
        {
            if (pawn.relations == null || partner == null)
                return "partner";
                
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner))
                return "spouse";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, partner))
                return "fiancee";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, partner))
                return "lover";
                
            return "partner";
        }
        
        public static void HoldPawnInPlace(Pawn pawn, IntVec3 position)
        {
            if (pawn != null && pawn.jobs != null)
            {
                // Create a job that keeps the pawn in place
                Job holdJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, position);
                holdJob.expiryInterval = 1800; // 30 seconds
                holdJob.canBashDoors = false;
                holdJob.canBashFences = false;
                holdJob.checkOverrideOnExpire = false;
                holdJob.playerForced = true; // Make it a forced job so it can interrupt other jobs
                
                // Start the job with InterruptForced to ensure it interrupts current activities
                pawn.jobs.StartJob(holdJob, JobCondition.InterruptForced);
            }
        }
    }
}