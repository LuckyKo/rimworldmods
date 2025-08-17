
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace SocialInteractions
{
    public class ThinkNode_JoinDateJoyActivity : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // 1. Is the pawn the partner on a date?
            if (!DatingManager.IsOnDate(pawn))
            {
                return null;
            }

            Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
            if (initiator == null || initiator == pawn)
            {
                // This pawn is the initiator, not the partner, or something is wrong.
                return null;
            }

            // 2. Is the date in the "Joy" stage?
            Date date = DatingManager.GetDateWith(pawn);
            if (date == null || date.Stage != DateStage.Joy)
            {
                return null;
            }

            // 3. Is the initiator doing a joy job?
            if (initiator.CurJob == null)
            {
                return null;
            }

            JoyGiverDef initiatorJoyGiver = DefDatabase<JoyGiverDef>.AllDefs.FirstOrDefault(jg => jg.jobDef == initiator.CurJob.def);
            if (initiatorJoyGiver == null)
            {
                // Initiator's current job is not a joy job.
                return null;
            }

            // 4. Is the partner's joy low enough to want to join?
            if (pawn.needs.joy.CurLevelPercentage >= 0.95f)
            {
                return null;
            }

            // 5. Can the partner do the same joy activity?
            Job partnerJoyJob = initiatorJoyGiver.Worker.TryGiveJob(pawn);
            if (partnerJoyJob == null)
            {
                return null;
            }

            // 6. Does the new job target the same thing or a nearby spot?
            bool targetsMatch = false;
            if (partnerJoyJob.targetA.Thing != null && initiator.CurJob.targetA.Thing != null)
            {
                targetsMatch = partnerJoyJob.targetA.Thing == initiator.CurJob.targetA.Thing;
            }
            else if (partnerJoyJob.targetA.Cell.IsValid && initiator.CurJob.targetA.Cell.IsValid)
            {
                targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(initiator.CurJob.targetA.Cell) <= 7f;
            }

            if (targetsMatch)
            {
                SLog.Message(string.Format("[SocialInteractions] ThinkNode_JoinDateJoyActivity: Partner {0} is joining initiator {1} in joy activity {2}.", pawn.LabelShort, initiator.LabelShort, partnerJoyJob.def.defName));
                return partnerJoyJob;
            }

            return null;
        }
    }
}
