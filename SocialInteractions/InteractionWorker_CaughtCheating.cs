using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class InteractionWorker_CaughtCheating : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null) return 0f;
            // Check if the initiator has a romantic partner
            Pawn partner = null;
            if (initiator.relations != null)
            {
                partner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, (p) => !p.Dead);
            }
            if (partner == null && initiator.relations != null)
            {
                partner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, (p) => !p.Dead);
            }
            if (partner == null && initiator.relations != null)
            {
                partner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, (p) => !p.Dead);
            }

            // If the initiator has a partner, and that partner is the recipient of the interaction...
            if (partner != null && partner == recipient)
            {
                // ...check if the partner is on a date with someone else.
                Pawn cheatingPartner = DatingManager.GetPartnerOnDateWith(partner);
                if (cheatingPartner != null && cheatingPartner != initiator)
                {
                    // 50% chance to notice
                    return 0.5f;
                }
            }

            return 0f;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            Pawn cheatingPartner = DatingManager.GetPartnerOnDateWith(recipient);
            if (cheatingPartner != null)
            {
                // Apply the "Caught Cheating" thought to the initiator
                if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("CaughtCheating"), recipient);
                }

                // Make the LLM call
                string subject = string.Format("{0} caught {1} cheating with {2}", initiator.Name.ToStringShort, recipient.Name.ToStringShort, cheatingPartner.Name.ToStringShort);
                InteractionDef caughtCheatingInteractionDef = DefDatabase<InteractionDef>.GetNamed("CaughtCheating");
                if (caughtCheatingInteractionDef != null)
                {
                    SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, caughtCheatingInteractionDef, subject, true);
                }
            }
        }
    }
}