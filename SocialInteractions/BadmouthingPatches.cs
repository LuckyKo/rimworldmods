using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SocialInteractions
{
    /// <summary>
    /// Patch to potentially initiate badmouthing during social interactions
    /// based on pawn traits that influence the frequency of such interactions.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class BadmouthingInteractionHandlerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, ref bool __result)
        {
            // The __instance is the pawn whose interactions tracker is being called (the initiator)
            Pawn initiator = (Pawn)AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn").GetValue(__instance);
            
            // Debug logging to see when this is being called
            string initiatorLabel = initiator != null ? initiator.LabelShort : "null";
            string recipientLabel = recipient != null ? recipient.LabelShort : "null";
            string intDefName = intDef != null ? intDef.defName : "null";
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: TryInteractWith called: {0} -> {1}, result: {2}, intDef: {3}", 
                initiatorLabel, recipientLabel, __result, intDefName));
            
            // Early check: if drama feature is not enabled, skip everything else
            if (!SocialInteractions.Settings.enableDrama)
            {
                SLog.Message("[SocialInteractions] BadmouthingPatches: Drama feature is disabled, skipping badmouthing check");
                return;
            }
            
            // If the basic interaction didn't succeed, skip
            if (!__result)
                return;
                
            // Only consider social interactions that might be good contexts for badmouthing
            if (intDef != InteractionDefOf.Chitchat && 
                intDef != InteractionDefOf.Insult)
                return;
                
            // Check if we should potentially replace this interaction with badmouthing
            // based on traits and settings
            bool shouldInitiate = ShouldInitiateBadmouthing(initiator, recipient);
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: ShouldInitiateBadmouthing({0}, {1}) = {2}", 
                initiatorLabel, recipientLabel, shouldInitiate));
            
            if (shouldInitiate)
            {
                // The original interaction already succeeded, so we'll trigger the badmouthing directly
                // through the InteractionWorker_Badmouthing system by calling the interaction worker directly
                
                // Directly call the interaction worker method to trigger the badmouthing interaction
                InteractionDef badmouthingDef = DefDatabase<InteractionDef>.GetNamedSilentFail("Badmouthing");
                if (badmouthingDef != null)
                {
                    SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Initiating badmouthing interaction directly via the worker"));
                    
                    // Get the target pawn that will be badmouthed (same logic as in the InteractionWorker)
                    Pawn targetPawn = GetLeastFavoritePawn(initiator);
                    string targetLabel = targetPawn != null ? targetPawn.LabelShort : "someone";
                    
                    SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Badmouthing target is {0}", targetLabel));
                    
                    // Create a new instance of the InteractionWorker_Badmouthing and call Interacted directly
                    InteractionWorker_Badmouthing badmouthingWorker = new InteractionWorker_Badmouthing();
                    
                    string letterText, letterLabel;
                    LetterDef letterDef;
                    LookTargets lookTargets;
                    
                    // Call the interaction worker's Interacted method directly
                    badmouthingWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);
                    
                    // Update the social log to reflect the badmouthing interaction instead of the original one
                    // We'll create a PlayLogEntry_Interaction for the badmouthing and add it to the play log
                    try
                    {
                        // Create a custom log entry for the badmouthing interaction that includes the target pawn
                        PlayLogEntry_Badmouthing badmouthingLogEntry = new PlayLogEntry_Badmouthing(badmouthingDef, initiator, recipient, null, targetPawn);
                        
                        // Add the entry to the play log to update the social history
                        if (Find.PlayLog != null)
                        {
                            Find.PlayLog.Add(badmouthingLogEntry);
                        }
                        
                        SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Added badmouthing interaction to play log"));
                    }
                    catch (System.Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] BadmouthingPatches: Failed to add badmouthing to play log: {0}", ex.Message));
                    }
                }
            }
        }

        private static bool ShouldInitiateBadmouthing(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: ShouldInitiateBadmouthing - null initiator or recipient"));
                return false;
            }
            
            int traitCount = 0;
            if (initiator.story != null && initiator.story.traits != null)
            {
                traitCount = initiator.story.traits.allTraits.Count;
            }
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: ShouldInitiateBadmouthing - checking {0} with traits: {1}", 
                initiator.LabelShort, traitCount));
            
            // Check if the initiator has traits that prevent badmouthing
            bool preventsBadmouthing = HasTraitThatPreventsBadmouthing(initiator);
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: HasTraitThatPreventsBadmouthing({0}) = {1}", 
                initiator.LabelShort, preventsBadmouthing));
                
            if (preventsBadmouthing)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0} has trait that prevents badmouthing, returning false", initiator.LabelShort));
                return false; // Kind pawns and similar never do this
            }
            
            // Check if the initiator has traits that encourage badmouthing
            float badmouthingChance = SocialInteractions.Settings.baseBadmouthingChance; // Base chance from settings
            bool encouragesBadmouthing = HasTraitThatEncouragesBadmouthing(initiator);
            
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: HasTraitThatEncouragesBadmouthing({0}) = {1}", 
                initiator.LabelShort, encouragesBadmouthing));
            
            if (encouragesBadmouthing)
            {
                badmouthingChance = SocialInteractions.Settings.traitEncouragedBadmouthingChance; // Chance for trait-encouraged pawns from settings
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Using trait encouraged chance: {0}", badmouthingChance));
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Using base chance: {0}", badmouthingChance));
            }
            
            // Additional chance based on relationship factors
            // If the initiator has a particularly low opinion of someone else in the colony,
            // they might be more likely to badmouth that person
            Pawn leastFavoritePawn = GetLeastFavoritePawn(initiator);
            if (leastFavoritePawn != null && leastFavoritePawn != recipient)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0}'s least favorite pawn is {1}", 
                    initiator.LabelShort, leastFavoritePawn.LabelShort));
                
                // If the initiator has someone they really dislike, they're more likely to badmouth
                int opinionOfLeastFavorite = 0;
                if (initiator.relations != null)
                {
                    opinionOfLeastFavorite = initiator.relations.OpinionOf(leastFavoritePawn);
                }
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Opinion of least favorite ({0}) is {1}, threshold is {2}", 
                    leastFavoritePawn.LabelShort, opinionOfLeastFavorite, SocialInteractions.Settings.badmouthingLowOpinionThreshold));
                
                if (opinionOfLeastFavorite < SocialInteractions.Settings.badmouthingLowOpinionThreshold) // Significantly negative opinion based on settings
                {
                    SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Opinion is below threshold, adding additional chance"));
                    badmouthingChance += SocialInteractions.Settings.badOpinionAdditionalChance; // Additional chance from settings
                }
            }
            
            float randValue = Rand.Value;
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Rand.Value: {0}, badmouthingChance: {1}, result: {2}", 
                randValue, badmouthingChance, randValue < badmouthingChance));
            
            return randValue < badmouthingChance;
        }

        private static bool HasTraitThatPreventsBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: HasTraitThatPreventsBadmouthing - pawn/story/traits is null"));
                return false;
            }
                
            // Kind pawns never engage in badmouthing
            Trait kindTrait = pawn.story.traits.GetTrait(TraitDefOf.Kind);
            if (kindTrait != null)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0} has Kind trait, preventing badmouthing", pawn.LabelShort));
                return true;
            }
            
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0} does not have Kind trait", pawn.LabelShort));
            
            // Other pro-social traits that might prevent badmouthing
            // Could include traits like "Charismatic" or "Patient" depending on mod definitions
            
            return false;
        }

        private static bool HasTraitThatEncouragesBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: HasTraitThatEncouragesBadmouthing - pawn/story/traits is null"));
                return false;
            }
                
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Checking traits for {0}:", pawn.LabelShort));
            
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower(); // Use defName for more accuracy
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Checking trait: defName={0}, label={1}", traitLabel, traitLabelDisplay));
                    
                    // Check both defName and display label to catch various trait formats
                    if (traitLabel.Contains("jealous") || 
                        traitLabel.Contains("abrasive") || 
                        traitLabel.Contains("psychopath") ||
                        traitLabel.Contains("mean") ||
                        traitLabel.Contains("cold") ||
                        traitLabel.Contains("arrogant") ||
                        traitLabel.Contains("bitch") ||  // Some mods may have this trait
                        traitLabel.Contains("bully") ||
                        traitLabel.Contains("selfish") ||
                        // Also check the display label in case defName doesn't match
                        traitLabelDisplay.Contains("jealous") || 
                        traitLabelDisplay.Contains("abrasive") || 
                        traitLabelDisplay.Contains("psychopath") ||
                        traitLabelDisplay.Contains("mean") ||
                        traitLabelDisplay.Contains("cold") ||
                        traitLabelDisplay.Contains("arrogant"))
                    {
                        SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0} has trait that encourages badmouthing: {1}", pawn.LabelShort, traitLabel));
                        return true;
                    }
                }
            }
            
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0} does not have any trait that encourages badmouthing", pawn.LabelShort));
            return false;
        }
        
        private static Pawn GetLeastFavoritePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: GetLeastFavoritePawn - pawn/Map/mapPawns is null"));
                return null;
            }

            int freeColonistsAndPrisonersCount = pawn.Map.mapPawns.FreeColonistsAndPrisoners.Count;
            SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Getting least favorite pawn for {0} among {1} pawns", 
                pawn.LabelShort, freeColonistsAndPrisonersCount));
                
            Pawn leastFavoritePawn = null;
            int lowestOpinion = int.MaxValue;

            foreach (Pawn otherPawn in pawn.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (otherPawn == pawn)
                {
                    continue; // Skip self
                }

                int opinion = pawn.relations != null ? pawn.relations.OpinionOf(otherPawn) : 0;
                
                // SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0}'s opinion of {1} is {2}", pawn.LabelShort, otherPawn.LabelShort, opinion));
                
                if (opinion < lowestOpinion)
                {
                    lowestOpinion = opinion;
                    leastFavoritePawn = otherPawn;
                }
            }

            if (leastFavoritePawn != null)
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: Least favorite pawn for {0} is {1} with opinion {2}", 
                    pawn.LabelShort, leastFavoritePawn.LabelShort, lowestOpinion));
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] BadmouthingPatches: {0} has no least favorite pawn", pawn.LabelShort));
            }

            return leastFavoritePawn;
        }
    }
}