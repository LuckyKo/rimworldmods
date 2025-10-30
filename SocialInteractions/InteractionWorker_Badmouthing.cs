using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class InteractionWorker_Badmouthing : InteractionWorker
    {
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            string initiatorLabel = initiator != null ? initiator.LabelShort : "null";
            string recipientLabel = recipient != null ? recipient.LabelShort : "null";
            SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: Badmouthing interaction initiated: {0} -> {1}", 
                initiatorLabel, recipientLabel));

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
            SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: HasTraitThatPreventsBadmouthing({0}) = {1}", 
                initiatorLabel, preventsBadmouthing));
                
            if (preventsBadmouthing)
            {
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: {0} has a trait that prevents badmouthing, skipping interaction.", initiatorLabel));
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Find the least favorite pawn in the colony for the initiator
            Pawn targetPawn = GetLeastFavoritePawn(initiator);
            string targetLabel = targetPawn != null ? targetPawn.LabelShort : "null";
            SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: GetLeastFavoritePawn({0}) returned {1}", 
                initiatorLabel, targetLabel));
                
            if (targetPawn == null)
            {
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: {0} has no least favorite pawn to badmouth, skipping interaction.", initiatorLabel));
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check that the target pawn is not the same as the recipient
            if (targetPawn == recipient)
            {
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: {0} tried to badmouth {1} to {1}'s face, skipping interaction.", initiatorLabel, targetPawn.LabelShort));
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check recipient's opinions of both the target and the initiator
            int recipientOpinionOfTarget = recipient.relations != null ? recipient.relations.OpinionOf(targetPawn) : 0;
            int recipientOpinionOfInitiator = recipient.relations != null ? recipient.relations.OpinionOf(initiator) : 0;
            
            SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: {0}'s opinion of {1} is {2}, {0}'s opinion of {3} is {4}", 
                recipient.LabelShort, targetLabel, recipientOpinionOfTarget, initiator.LabelShort, recipientOpinionOfInitiator));

            // Determine the outcome based on opinions
            string outcome;
            if (recipientOpinionOfTarget <= recipientOpinionOfInitiator)
            {
                // Recipient values the target less than the initiator, so the badmouthing is likely to be believed/reinforce negative opinion
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: {0}'s opinion of {1} ({2}) <= {0}'s opinion of {3} ({4}), so {0} is more likely to believe the badmouthing", 
                    recipient.LabelShort, targetLabel, recipientOpinionOfTarget, initiator.LabelShort, recipientOpinionOfInitiator));
                
                // In this scenario, the recipient was told negative things about someone they already don't like much,
                // so they form an even worse opinion of that target
                // Apply the WasToldNegativeThings thought to the recipient about the target
                ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                if (wasToldNegativeThingsThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
                    SLog.Message(string.Format("[SocialInteractions] WasToldNegativeThings thought applied: {0} for {1} about {2}", 
                        wasToldNegativeThingsThought.defName, recipient.LabelShort, targetPawn.LabelShort));
                }
                else
                {
                    // Fallback to general insult thought if custom thought is not available
                    ThoughtDef insultedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
                    if (insultedThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(insultedThought, targetPawn);
                        SLog.Message(string.Format("[SocialInteractions] Fallback insult thought applied: {0} for {1} about {2}", 
                            insultedThought.defName, recipient.LabelShort, targetPawn.LabelShort));
                    }
                    else
                    {
                        SLog.Message(string.Format("[SocialInteractions] Could not find appropriate thought, badmouthing opinion change may not work correctly for {0} -> {1}", 
                            recipient.LabelShort, targetPawn.LabelShort));
                    }
                }
                
                // Generate appropriate subject text for LLM with more detailed information
                string subject = string.Format("A badmouthing interraction where {0} speaks negatively about {1} to {2}. {2} values {1} less than {0}, causing {2} to believe the badmouthing and think worse of {1}.",
                    initiator.LabelShort, targetLabel, recipient.LabelShort, recipientOpinionOfInitiator, recipientOpinionOfTarget);
                
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: Generated subject: {0}", subject));
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
                
                // Add the drama event to the chat log
                ChatLogManager.AddDramaEvent(initiator, recipient, subject, string.Format("{0} spoke negatively about {1}", initiator.LabelShort, targetLabel));
            }
            else
            {
                // Recipient values the target more than the initiator, so they lose trust in the initiator for badmouthing someone they respect
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: {0}'s opinion of {1} ({2}) > {0}'s opinion of {3} ({4}), so {0} loses trust in {3} for badmouthing {1}", 
                    recipient.LabelShort, targetLabel, recipientOpinionOfTarget, initiator.LabelShort, recipientOpinionOfInitiator));
                
                // In this scenario, the recipient was told negative things about someone they respect by someone they trust less
                // This should damage the relationship with the initiator
                // Apply the HeardBadmouthing thought to the recipient about the initiator
                ThoughtDef heardBadmouthingThought = DefDatabase<ThoughtDef>.GetNamed("HeardBadmouthing");
                if (heardBadmouthingThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(heardBadmouthingThought, initiator);
                    SLog.Message(string.Format("[SocialInteractions] HeardBadmouthing thought applied: {0} for {1} about {2}", 
                        heardBadmouthingThought.defName, recipient.LabelShort, initiator.LabelShort));
                }
                else
                {
                    // Fallback to general insult thought if custom thought is not available
                    ThoughtDef insultedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
                    if (insultedThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(insultedThought, initiator);
                        SLog.Message(string.Format("[SocialInteractions] Fallback insult thought applied: {0} for {1} about {2}", 
                            insultedThought.defName, recipient.LabelShort, initiator.LabelShort));
                    }
                    else
                    {
                        SLog.Message(string.Format("[SocialInteractions] Could not find appropriate thought, badmouthing opinion change may not work correctly for {0} -> {1}", 
                            recipient.LabelShort, initiator.LabelShort));
                    }
                }
                
                // Generate appropriate subject text for LLM with more detailed information
                string subject = string.Format("A badmouthing interraction where {0} speaks negatively about {1} to {2}. However, {2} respects {1} more than {0}, causing {2} to lose respect for {0} instead.",
                    initiator.LabelShort, targetLabel, recipient.LabelShort, recipientOpinionOfInitiator, recipientOpinionOfTarget);
                
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: Generated subject: {0}", subject));
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
                
                // Add the drama event to the chat log
                ChatLogManager.AddDramaEvent(initiator, recipient, subject, string.Format("{0} spoke negatively about {1}", initiator.LabelShort, targetLabel));
            }

            // Log outcome after processing
            outcome = string.Format("[SocialInteractions] InteractionWorker_Badmouthing: Processed badmouthing interaction between {0} and {1} about {2}", 
                initiator.LabelShort, recipient.LabelShort, targetLabel);
            SLog.Message(outcome);

            // Call the base Interacted method to create the normal log entry
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }

        private Pawn GetLeastFavoritePawn(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }

            Pawn leastFavoritePawn = null;
            int lowestOpinion = int.MaxValue;

            foreach (Pawn otherPawn in pawn.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (otherPawn == pawn)
                {
                    continue; // Skip self
                }

                int opinion = pawn.relations != null ? pawn.relations.OpinionOf(otherPawn) : 0;
                
                if (opinion < lowestOpinion)
                {
                    lowestOpinion = opinion;
                    leastFavoritePawn = otherPawn;
                }
            }

            return leastFavoritePawn;
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