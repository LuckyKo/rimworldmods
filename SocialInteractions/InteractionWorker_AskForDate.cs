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
            if (IsPawnBusyWithCriticalJob(initiator) || IsPawnBusyWithCriticalJob(recipient)) return 0f; // New check
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

            // If not a partner, use opinion as the weight
            float opinion = initiator.relations.OpinionOf(recipient);
            if (opinion > 0) // Only consider positive opinions
            {
                return opinion * Rand.Range(0.1f, 0.5f); // Scale opinion to a reasonable weight
            }
            return 0f;
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
                Job job = JobMaker.MakeJob(goOnDateJobDef, initiator, recipient);
                initiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }

        private bool IsPawnBusyWithCriticalJob(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null || pawn.jobs.curJob == null) return false;

            JobDef currentJobDef = pawn.jobs.curJob.def;

            // Check for drafted pawns
            if (pawn.Drafted) return true;

            // List of critical/uninterruptible job defs
            List<JobDef> criticalJobDefs = new List<JobDef>
            {
                JobDefOf.LayDown, // Sleeping
                JobDefOf.Ingest, // Eating
                JobDefOf.TendPatient,
                JobDefOf.Flee,
                JobDefOf.AttackMelee,
                JobDefOf.AttackStatic,
                JobDefOf.Wait_Combat,
                JobDefOf.Flick,
                JobDefOf.HaulToContainer
            };

            return criticalJobDefs.Contains(currentJobDef);
        }
    }
}