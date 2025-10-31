using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System;
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
            
            // Early check: if drama feature is not enabled, skip everything else
            if (!SocialInteractions.Settings.enableDrama)
            {
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
            
            if (shouldInitiate)
            {
                // The original interaction already succeeded, so we'll trigger the badmouthing directly
                // through the InteractionWorker_Badmouthing system by calling the interaction worker directly
                
                // Directly call the interaction worker method to trigger the badmouthing/gossip interaction
                InteractionDef badmouthingDef = DefDatabase<InteractionDef>.GetNamedSilentFail("Badmouthing");
                if (badmouthingDef != null)
                {
                    // Create a new instance of the InteractionWorker_Badmouthing and call Interacted directly
                    // The interaction worker will now determine if this is gossip (shared negative opinions) 
                    // or badmouthing (one-sided negative opinions) based on the pawns' opinions of the target
                    InteractionWorker_Badmouthing badmouthingWorker = new InteractionWorker_Badmouthing();
                    
                    string letterText, letterLabel;
                    LetterDef letterDef;
                    LookTargets lookTargets;
                    
                    // Call the interaction worker's Interacted method directly
                    badmouthingWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);
                    
                    // The interaction worker will handle logging the interaction properly
                    // No need to manually add to play log here since the interaction worker handles it
                }
            }
        }

        private static bool ShouldInitiateBadmouthing(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }
            
            // Check if the initiator has traits that prevent badmouthing
            bool preventsBadmouthing = HasTraitThatPreventsBadmouthing(initiator);
            if (preventsBadmouthing)
            {
                return false; // Kind pawns and similar never do this
            }
            
            // Check if the initiator has traits that encourage badmouthing
            float badmouthingChance = SocialInteractions.Settings.baseBadmouthingChance; // Base chance from settings
            bool encouragesBadmouthing = HasTraitThatEncouragesBadmouthing(initiator);
            
            if (encouragesBadmouthing)
            {
                badmouthingChance = SocialInteractions.Settings.traitEncouragedBadmouthingChance; // Chance for trait-encouraged pawns from settings
            }
            
            // Additional chance based on relationship factors
            // If the initiator has a particularly low opinion of someone else in the colony,
            // they might be more likely to badmouth that person
            Pawn leastFavoritePawn = GetLeastFavoritePawn(initiator);
            if (leastFavoritePawn != null && leastFavoritePawn != recipient)
            {
                // If the initiator has someone they really dislike, they're more likely to badmouth
                int opinionOfLeastFavorite = 0;
                if (initiator.relations != null)
                {
                    opinionOfLeastFavorite = initiator.relations.OpinionOf(leastFavoritePawn);
                }
                
                if (opinionOfLeastFavorite < SocialInteractions.Settings.badmouthingLowOpinionThreshold) // Significantly negative opinion based on settings
                {
                    badmouthingChance += SocialInteractions.Settings.badOpinionAdditionalChance; // Additional chance from settings
                }
            }
            
            float randValue = Rand.Value;
            return randValue < badmouthingChance;
        }

        private static bool HasTraitThatPreventsBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
                
            // Kind pawns never engage in badmouthing
            Trait kindTrait = pawn.story.traits.GetTrait(TraitDefOf.Kind);
            if (kindTrait != null)
            {
                return true;
            }
            
            return false;
        }

        private static bool HasTraitThatEncouragesBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
                
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower(); // Use defName for more accuracy
                    string traitLabelDisplay = trait.Label.ToLower();
                    
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
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private static Pawn GetLeastFavoritePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null)
            {
                return null;
            }

            return SocialInteractions.GetWeightedLeastFavoritePawn(pawn);
        }
    }
}