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
            Pawn partner = DatingManager.GetPartnerOnDateWith(recipient);
            if (partner != null)
            {
                partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDef.Named("CaughtCheating"), recipient);
            }

            // Start a social fight
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }
    }
}
