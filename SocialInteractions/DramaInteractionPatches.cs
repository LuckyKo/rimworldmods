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
    /// Patch to handle all drama interactions during social interactions like Chitchat
    /// Provides a unified system for different types of drama mechanics with priority management.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class DramaInteractionHandlerPatch
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
                
            // Only consider social interactions that might be good contexts for drama
            if (intDef != InteractionDefOf.Chitchat && 
                intDef != InteractionDefOf.DisturbingChat &&
                intDef != InteractionDefOf.Insult)
                return;
                
            // Process drama interactions in priority order
            // Check for highest priority drama interaction that fits the context
            if (TryProcessBadmouthingGossip(initiator, recipient, intDef))
            {
                // Badmouthing/gossip was triggered, exit to prevent other drama interactions
                return;
            }
            
            // Add other drama interaction checks here in priority order
            // For example, enhanced chitchat insults would be checked next
            if (TryProcessEnhancedChitchatInsult(initiator, recipient, intDef))
            {
                // Enhanced chitchat insult was triggered, exit to prevent other drama interactions
                return;
            }
            
            // Additional drama interaction checks would go here as needed
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
        
        /// <summary>
        /// Attempts to process badmouthing or gossip interaction based on opinion dynamics
        /// Higher priority than other drama interactions
        /// </summary>
        private static bool TryProcessBadmouthingGossip(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Check if we should potentially replace this interaction with badmouthing/gossip
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
                    return true; // Indicate that we processed this interaction
                }
            }
            
            return false; // Indicate that we didn't process this interaction
        }

        /// <summary>
        /// Attempts to process enhanced chitchat insult interaction
        /// Lower priority than badmouthing/gossip
        /// </summary>
        private static bool TryProcessEnhancedChitchatInsult(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Check if we should potentially enhance this chitchat with an insult
            // based on traits, mood, or relationship dynamics
            bool shouldInitiate = ShouldInitiateEnhancedChitchatInsult(initiator, recipient);
            
            if (shouldInitiate)
            {
                // Instead of a full badmouthing interaction, we'll trigger our new EnhancedInsult interaction
                // This provides more nuanced insult handling with severity based on opinion
                if (intDef == InteractionDefOf.Chitchat || intDef == InteractionDefOf.DisturbingChat)
                {
                    // Check if LLM interactions are enabled for EnhancedInsult
                    if (SocialInteractions.IsLlmInteractionEnabled(SI_InteractionDefOf.EnhancedInsult))
                    {
                        // Let the EnhancedInsult interaction worker handle the interaction with severity-based subjects
                        InteractionDef enhancedInsultDef = DefDatabase<InteractionDef>.GetNamedSilentFail("EnhancedInsult");
                        if (enhancedInsultDef != null)
                        {
                            InteractionWorker_EnhancedInsult enhancedInsultWorker = new InteractionWorker_EnhancedInsult();
                            
                            string letterText, letterLabel;
                            LetterDef letterDef;
                            LookTargets lookTargets;
                            
                            // Call the interaction worker's Interacted method directly - this handles severity and subject generation
                            enhancedInsultWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);
                            
                            // The interaction worker will handle logging the interaction properly
                            return true; // Indicate that we processed this interaction with an enhanced insult
                        }
                    }
                    else
                    {
                        // If LLM is not enabled for EnhancedInsult, show a default bubble with a generic subject
                        string subject = string.Format("{0} made a negative comment to {1}", initiator.LabelShort, recipient.LabelShort);
                        SocialInteractions.HandleInteraction(initiator, recipient, intDef, subject);
                        
                        return true; // Indicate that we processed this interaction
                    }
                }
            }
            
            return false;
        }
        
        private static bool ShouldInitiateEnhancedChitchatInsult(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }
            
            // Check if the initiator has traits that prevent negative interactions
            bool preventsNegativeInteractions = HasTraitThatPreventsBadmouthing(initiator);
            if (preventsNegativeInteractions)
            {
                return false; // Kind pawns and similar never do this
            }
            
            // Base chance for enhanced chitchat insults from settings
            float insultChance = SocialInteractions.Settings.baseEnhancedChitchatInsultChance;
            
            // Modify chance based on mood using settings
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                float mood = initiator.needs.mood.CurLevelPercentage;
                // Lower mood increases chance of negative comments in conversation
                if (mood < 0.4f) // Below 40% mood
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierBad;
                }
                else if (mood > 0.8f) // Above 80% mood
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierGood;
                }
            }
            
            // Modify chance based on opinion of recipient using settings
            if (initiator.relations != null)
            {
                int opinionOfRecipient = initiator.relations.OpinionOf(recipient);
                // Lower opinion of recipient increases chance of negative comments
                if (opinionOfRecipient < -20) // Significantly negative opinion
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryNegative;
                }
                else if (opinionOfRecipient > 30) // Significantly positive opinion
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryPositive;
                }
            }
            
            // Modify chance based on traits that encourage negative interactions using settings
            if (HasTraitThatEncouragesBadmouthing(initiator))
            {
                insultChance *= SocialInteractions.Settings.enhancedChitchatInsultTraitMultiplier;
            }
            
            // Modify chance based on relationship differences
            // For example, if initiator has very different opinions about others compared to recipient
            float opinionDifferenceFactor = CalculateOpinionDifferenceFactor(initiator, recipient);
            insultChance *= opinionDifferenceFactor;
            
            float randValue = Rand.Value;
            return randValue < insultChance;
        }
        
        /// <summary>
        /// Calculates a factor based on how different the initiator's and recipient's opinions are
        /// Higher differences increase the chance of negative comments
        /// </summary>
        private static float CalculateOpinionDifferenceFactor(Pawn initiator, Pawn recipient)
        {
            if (initiator.Map == null || initiator.Map.mapPawns.FreeColonistsAndPrisoners.Count <= 1)
            {
                return 1.0f; // No difference factor if insufficient pawns
            }
            
            float totalDifference = 0f;
            int comparisonCount = 0;
            
            // Compare opinions about other pawns in the colony
            foreach (Pawn otherPawn in initiator.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (otherPawn == initiator || otherPawn == recipient)
                {
                    continue; // Skip self and the recipient
                }
                
                // Get opinions of both initiator and recipient about this other pawn
                int initiatorOpinion = initiator.relations != null ? initiator.relations.OpinionOf(otherPawn) : 0;
                int recipientOpinion = recipient.relations != null ? recipient.relations.OpinionOf(otherPawn) : 0;
                
                // Calculate the absolute difference in opinions
                float difference = Math.Abs(initiatorOpinion - recipientOpinion);
                
                // If their opinions are very different (more than 20 points), that contributes to tension
                if (difference > 20)
                {
                    totalDifference += difference / 100f; // Normalize to reasonable values
                    comparisonCount++;
                }
            }
            
            if (comparisonCount == 0)
            {
                return 1.0f; // No significant differences found
            }
            
            float averageDifference = totalDifference / comparisonCount;
            
            // Return a factor greater than 1.0 if there are significant opinion differences
            // This makes it more likely to have negative comments when pawns have very different opinions
            return 1.0f + (averageDifference * SocialInteractions.Settings.enhancedChitchatInsultOpinionDifferenceMultiplier); // Scale the impact using settings
        }
    }
}