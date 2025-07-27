using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JoyGiver_HaveDeepTalk : JoyGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!SocialInteractionUtility.CanInitiateInteraction(pawn))
            {
                return null;
            }

            Pawn bestLover = LovePartnerRelationUtility.GetPartnerInMyBed(pawn);
            if (bestLover != null && pawn.relations.OpinionOf(bestLover) > 20)
            {
                return JobMaker.MakeJob(def.jobDef, bestLover);
            }

            return null;
        }
    }
}