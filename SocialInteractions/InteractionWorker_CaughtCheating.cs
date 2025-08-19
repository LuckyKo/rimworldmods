using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class InteractionWorker_CaughtCheating : InteractionWorker
    {
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // End the date
            Date date = DatingManager.GetDateWith(recipient);
            if (date != null) DatingManager.EndDate(date);

            // Add a memory to the initiator (the one who caught the cheater)
            initiator.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("CaughtCheating"), recipient);

            // Add a memory to the recipient (the cheater)
            recipient.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("CaughtCheating"), initiator);

            // Add a memory to the partner (the one the recipient was cheating with)
            Pawn partner = DatingManager.GetPartnerOfDateWith(recipient);
            if (partner != null)
            {
                partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("CaughtCheating"), recipient);
                
                // After the LLM interaction, there's a chance to start a social fight
                // 75% chance to fight the cheater, 25% chance to fight the partner
                if (Rand.Chance(0.75f))
                {
                    // Start a social fight with the cheater
                    if (initiator.Faction == recipient.Faction)
                    {
                        initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, recipient);
                    }
                }
                else
                {
                    // Start a social fight with the partner
                    if (initiator.Faction == partner.Faction)
                    {
                        initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, partner);
                    }
                }
            }
            else
            {
                // If there's no partner for some reason, fight the cheater
                if (initiator.Faction == recipient.Faction)
                {
                    initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, recipient);
                }
            }

            // Call base method for any additional logic
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }
    }
}
