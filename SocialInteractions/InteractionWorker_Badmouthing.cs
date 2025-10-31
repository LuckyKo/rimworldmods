using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class InteractionWorker_Badmouthing : InteractionWorker
    {
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // Initialize out parameters
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = LookTargets.Invalid;

            // Add null checks to prevent exceptions
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_Badmouthing: Initiator or recipient is null, skipping interaction.");
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check if the initiator has traits that would make them avoid this interaction
            bool preventsBadmouthing = HasTraitThatPreventsBadmouthing(initiator);
            if (preventsBadmouthing)
            {
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Find the least favorite pawn in the colony for the initiator
            Pawn targetPawn = GetLeastFavoritePawn(initiator);
            if (targetPawn == null)
            {
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check that the target pawn is not the same as the recipient
            if (targetPawn == recipient)
            {
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check recipient's opinions of both the target and the initiator
            int recipientOpinionOfTarget = recipient.relations != null ? recipient.relations.OpinionOf(targetPawn) : 0;
            int recipientOpinionOfInitiator = recipient.relations != null ? recipient.relations.OpinionOf(initiator) : 0;
            
            // Check if both initiator and recipient share a negative opinion of the target (gossip scenario)
            int initiatorOpinionOfTarget = initiator.relations != null ? initiator.relations.OpinionOf(targetPawn) : 0;
            bool sharedNegativeOpinion = initiatorOpinionOfTarget <= SocialInteractions.Settings.badmouthingLowOpinionThreshold && 
                                        recipientOpinionOfTarget <= SocialInteractions.Settings.badmouthingLowOpinionThreshold;

            if (sharedNegativeOpinion)
            {
                // Gossip scenario: Both pawns share negative opinions about the target
                // This should strengthen their bond and confirm their shared views
                ApplyGossipThoughts(initiator, recipient, targetPawn);
                
                // Generate appropriate subject text for LLM with more detailed information
                string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
                string subject = string.Format("A gossip interaction where {0} and {1} bond over their shared negative opinions about {2} ({3}). This strengthens their relationship and confirms their mutual dislike.",
                    initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort, targetDescription);
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
            }
            else if (recipientOpinionOfTarget <= recipientOpinionOfInitiator)
            {
                // Original badmouthing scenario: recipient was told negative things about someone they already don't like much
                ApplyBadmouthingThoughtsToTarget(initiator, recipient, targetPawn);
                
                // Generate appropriate subject text for LLM with more detailed information
                string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
                string subject = string.Format("A badmouthing interaction where {0} speaks negatively about {1} ({2}) to {3}. {3} values {1} less than {0}, causing {3} to believe the badmouthing and think worse of {1}.",
                    initiator.LabelShort, targetPawn.LabelShort, targetDescription, recipient.LabelShort);
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
            }
            else
            {
                // Original badmouthing scenario: recipient was told negative things about someone they respect
                ApplyBadmouthingThoughtsToInitiator(initiator, recipient, targetPawn);
                
                // Generate appropriate subject text for LLM with more detailed information
                string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
                string subject = string.Format("A badmouthing interaction where {0} speaks negatively about {1} ({2}) to {3}. However, {3} respects {1} more than {0}, causing {3} to lose respect for {0} instead.",
                    initiator.LabelShort, targetPawn.LabelShort, targetDescription, recipient.LabelShort);
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
            }

            // Call the base Interacted method to create the normal log entry using XML rules
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            // Create a custom log entry that includes the target pawn information to ensure consistency
            // This ensures the target shown in the play log matches the one used in the interaction
            try
            {
                // Use the existing targetPawn variable that was already determined in the method
                // This ensures perfect consistency between the interaction and the log entry
                if (targetPawn != null && targetPawn != recipient)
                {
                    // Create a custom log entry for the badmouthing interaction that includes the target pawn
                    PlayLogEntry_Badmouthing badmouthingLogEntry = new PlayLogEntry_Badmouthing(SI_InteractionDefOf.Badmouthing, initiator, recipient, extraSentencePacks, targetPawn);
                    
                    // Add the entry to the play log to update the social history
                    if (Find.PlayLog != null)
                    {
                        Find.PlayLog.Add(badmouthingLogEntry);
                    }
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: Failed to add badmouthing to play log: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Applies thoughts for gossip scenario where both pawns share negative opinions about the target
        /// </summary>
        private void ApplyGossipThoughts(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // Both pawns get positive thoughts for bonding with someone who shares their negative opinion
            // This encourages future interactions between them (gossip partnerships)
            
            // Use our newly defined thoughts that promote bonding over shared negative opinions
            if (SI_ThoughtDefOf.BondedOverSharedDislike != null)
            {
                // Initiator bonds with recipient over shared dislike
                initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.BondedOverSharedDislike, recipient);
                // Recipient bonds with initiator over shared dislike  
                recipient.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.BondedOverSharedDislike, initiator);
            }
            else if (SI_ThoughtDefOf.FoundCommonGround != null)
            {
                // Fallback to FoundCommonGround thought if BondedOverSharedDislike is not available
                initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.FoundCommonGround, recipient);
                recipient.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.FoundCommonGround, initiator);
            }
            else
            {
                // Fallback to game's general social thoughts if custom thoughts aren't loaded
                SLog.Message("[SocialInteractions] Gossip: Custom bonding thoughts not available, using fallback");
                
                // Use existing RimWorld thoughts that promote social bonding
                ThoughtDef socialConnectionThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("IncreasedChemistry");
                if (socialConnectionThought != null)
                {
                    // Give a positive thought to both for having increased chemistry
                    initiator.needs.mood.thoughts.memories.TryGainMemory(socialConnectionThought, recipient);
                    recipient.needs.mood.thoughts.memories.TryGainMemory(socialConnectionThought, initiator);
                }
                else
                {
                    // Last fallback - use SocialRecreationPartner as a generic positive social interaction
                    ThoughtDef positiveSocialThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SocialRecreationPartner");
                    if (positiveSocialThought != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(positiveSocialThought, recipient);
                        recipient.needs.mood.thoughts.memories.TryGainMemory(positiveSocialThought, initiator);
                    }
                    else
                    {
                        SLog.Message("[SocialInteractions] Gossip: Could not find any appropriate bonding thoughts");
                    }
                }
            }
            
            // Optionally, both gain slight negative opinion of the target (reinforcing their shared dislike)
            // Only if the target is not one of the interacting pawns
            if (targetPawn != initiator && targetPawn != recipient)
            {
                // Reinforce negative opinion of the target through the interaction
                // This helps solidify the "us vs them" dynamic that promotes clique formation
                if (recipient.relations != null)
                {
                    // Apply a thought to recipient about the target to reinforce negative opinion
                    ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                    if (wasToldNegativeThingsThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
                    }
                    else
                    {
                        // No appropriate fallback thought, so skip reinforcement of target's negative opinion
                        // The primary effect is the bonding between the two interacting pawns
                    }
                }
                
                if (initiator.relations != null)
                {
                    // Apply a thought to initiator about the target to reinforce negative opinion  
                    ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                    if (wasToldNegativeThingsThought != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
                    }
                    else
                    {
                        // No appropriate fallback thought, so skip reinforcement of target's negative opinion
                        // The primary effect is the bonding between the two interacting pawns
                    }
                }
            }
        }
        
        /// <summary>
        /// Applies thoughts for original badmouthing scenario where target receives negative thoughts
        /// </summary>
        private void ApplyBadmouthingThoughtsToTarget(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // In this scenario, the recipient was told negative things about someone they already don't like much,
            // so they form an even worse opinion of that target
            // Apply the WasToldNegativeThings thought to the recipient about the target
            ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
            if (wasToldNegativeThingsThought != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
            }
            else
            {
                // Fallback to general insult thought if custom thought is not available
                ThoughtDef insultedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
                if (insultedThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(insultedThought, targetPawn);
                }
                else
                {
                    SLog.Message("[SocialInteractions] Could not find appropriate thought for badmouthing target scenario");
                }
            }
        }
        
        /// <summary>
        /// Applies thoughts for original badmouthing scenario where initiator receives negative thoughts
        /// </summary>
        private void ApplyBadmouthingThoughtsToInitiator(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // In this scenario, the recipient was told negative things about someone they respect by someone they trust less
            // This should damage the relationship with the initiator
            // Apply the HeardBadmouthing thought to the recipient about the initiator
            ThoughtDef heardBadmouthingThought = DefDatabase<ThoughtDef>.GetNamed("HeardBadmouthing");
            if (heardBadmouthingThought != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(heardBadmouthingThought, initiator);
            }
            else
            {
                // Fallback to general insult thought if custom thought is not available
                ThoughtDef insultedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
                if (insultedThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(insultedThought, initiator);
                }
                else
                {
                    SLog.Message("[SocialInteractions] Could not find appropriate thought for badmouthing initiator scenario");
                }
            }
        }

        private Pawn GetLeastFavoritePawn(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }

            return SocialInteractions.GetWeightedLeastFavoritePawn(pawn);
        }


        
        private bool HasTraitThatPreventsBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for Kind trait - Kind pawns would never engage in badmouthing
            Trait kindTrait = pawn.story.traits.GetTrait(TraitDefOf.Kind);
            if (kindTrait != null)
            {
                return true;
            }
            
            // Add other traits that would prevent badmouthing here
            // For example, traits like "Good Listener" or similar pro-social traits
            
            return false;
        }
        
        private bool HasTraitThatEncouragesBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that make badmouthing more likely
            // Jealous trait (if it exists)
            TraitDef jealousDef = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
            if (jealousDef != null)
            {
                Trait jealousTrait = pawn.story.traits.GetTrait(jealousDef);
                if (jealousTrait != null)
                {
                    return true;
                }
            }
            
            // Abrasive trait
            TraitDef abrasiveDef = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
            if (abrasiveDef != null)
            {
                Trait abrasiveTrait = pawn.story.traits.GetTrait(abrasiveDef);
                if (abrasiveTrait != null)
                {
                    return true;
                }
            }
            
            // Psychopath trait (if it exists in the game/other mods)
            TraitDef psychopathDef = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
            if (psychopathDef != null)
            {
                Trait psychopathTrait = pawn.story.traits.GetTrait(psychopathDef);
                if (psychopathTrait != null)
                {
                    return true;
                }
            }
            
            // Add other traits like "Bullying", "Rigid", etc. if they exist
            // For now, we'll include any trait that tends to make a pawn antisocial
            
            // Check for any trait that has "Dislike" or negative social interaction effects
            // We can also check for traits that increase the pawn's tendency toward negative social behavior
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    // Check if this trait affects social interactions negatively or makes them more likely to speak negatively
                    if (trait.Label.ToLower().Contains("abrasive") || 
                        trait.Label.ToLower().Contains("psychopath") || 
                        trait.Label.ToLower().Contains("jealous") ||
                        trait.Label.ToLower().Contains("mean") ||
                        trait.Label.ToLower().Contains("cold"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}