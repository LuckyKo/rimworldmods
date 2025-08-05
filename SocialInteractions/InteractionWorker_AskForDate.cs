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
            float weight = 0f;
            // Do not allow pawns to ask themselves out
            if (initiator == recipient) weight = 0f;

            // Check if either pawn is already on a date
            else if (initiator.jobs.curJob.def.defName == "OnDate" || recipient.jobs.curJob.def.defName == "OnDate") weight = 0f;

            // Check for age compatibility
            else if (initiator.ageTracker.AgeBiologicalYearsFloat < 16f || recipient.ageTracker.AgeBiologicalYearsFloat < 16f) weight = 0f;

            // Check for opinion
            else if (initiator.relations.OpinionOf(recipient) < 10) weight = 0f;

            // Add a random factor
            else weight = 1.0f * Rand.Value;

            Log.Message(string.Format("[SocialInteractions] RandomSelectionWeight for {0} and {1}: {2}", initiator.Name.ToStringShort, recipient.Name.ToStringShort, weight));
            return weight;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            // We now handle the job creation in the JobDriver_AskForDate
        }
    }
}