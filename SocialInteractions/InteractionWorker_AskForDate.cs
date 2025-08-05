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
            if (initiator == recipient) return 0f;
            if (initiator.jobs != null && initiator.jobs.curJob != null && initiator.jobs.curJob.def.defName == "OnDate") return 0f;
            if (recipient.jobs != null && recipient.jobs.curJob != null && recipient.jobs.curJob.def.defName == "OnDate") return 0f;
            if (initiator.ageTracker.AgeBiologicalYearsFloat < 16f || recipient.ageTracker.AgeBiologicalYearsFloat < 16f) return 0f;
            if (initiator.relations.OpinionOf(recipient) < 10) return 0f;
            return 1.0f * Rand.Value;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;
        }
    }
}