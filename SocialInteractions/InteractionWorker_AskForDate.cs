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
            if (initiator == null || recipient == null) return 0f;
            if (DatingManager.IsOnDate(initiator) || DatingManager.IsOnDate(recipient)) return 0f;
            if (initiator == recipient) return 0f;
            if (initiator.jobs != null && initiator.jobs.curJob != null && (initiator.jobs.curJob.def.defName == "AskForDate" || initiator.jobs.curJob.def.defName == "FollowAndWatchInitiator")) return 0f;
            if (recipient.jobs != null && recipient.jobs.curJob != null && (recipient.jobs.curJob.def.defName == "AskForDate" || recipient.jobs.curJob.def.defName == "FollowAndWatchInitiator")) return 0f;
            if (initiator.needs == null || initiator.needs.joy == null) return 0f;
            if (initiator.needs.joy.CurLevelPercentage > 0.8f) return 0f;
            if (initiator.ageTracker == null || initiator.ageTracker.AgeBiologicalYearsFloat < 16f || recipient.ageTracker == null || recipient.ageTracker.AgeBiologicalYearsFloat < 16f) return 0f;
            // Check if the recipient is a partner
            bool isPartner = false;
            if (initiator.relations != null)
            {
                isPartner = initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient) ||
                            initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient) ||
                            initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient);
            }

            // If they are a partner, give a very high chance
            if (isPartner)
            {
                return 100f; // A high weight to strongly prioritize partners
            }

            // If not a partner, calculate the romance chance for a "cheating" attempt
            float romanceChance = InteractionWorker_RomanceAttempt.SuccessChance(initiator, recipient, 1f);

            // Use the romance chance as the weight, with a small random factor
            return romanceChance * Rand.Range(0.8f, 1.0f);
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            // Add this check as a final safeguard
            if (DatingManager.IsOnDate(initiator) || DatingManager.IsOnDate(recipient))
            {
                Log.Message(string.Format("[SocialInteractions] InteractionWorker_AskForDate: Preventing duplicate date initiation. Initiator: {0}, Recipient: {1}", initiator.Name.ToStringShort, recipient.Name.ToStringShort));
                return; // Do nothing if either pawn is already on a date
            }

            JobDef goOnDateJobDef = DefDatabase<JobDef>.GetNamed("GoOnDate");
            if (goOnDateJobDef != null && initiator.jobs != null)
            {
                Job job = JobMaker.MakeJob(goOnDateJobDef, recipient);
                initiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }
    }
}