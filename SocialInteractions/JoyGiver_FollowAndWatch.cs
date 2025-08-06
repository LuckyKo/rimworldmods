using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JoyGiver_FollowAndWatch : JoyGiver
    {
        // Existing TryGiveJob (less used now)
        public override Job TryGiveJob(Pawn pawn)
        {
            Pawn initiator = DatingManager.GetPartnerOnDateWith(pawn);
            if (initiator != null && initiator.CurJob != null && initiator.CurJob.targetA.IsValid)
            {
                return JobMaker.MakeJob(SI_InteractionDefOf.FollowAndWatchInitiator, initiator, initiator.CurJob.targetA.Thing);
            }
            return null;
        }

        // New TryGiveJob overload for direct assignment
        public Job TryGiveJob(Pawn pawn, Pawn initiator, LocalTargetInfo target)
        {
            return JobMaker.MakeJob(SI_InteractionDefOf.FollowAndWatchInitiator, initiator, target);
        }
    }
}