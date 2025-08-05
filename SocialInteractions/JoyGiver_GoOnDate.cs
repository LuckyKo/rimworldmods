using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JoyGiver_GoOnDate : JoyGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            // Only initiate a date if joy is below 80%
            if (pawn.needs.joy.CurLevelPercentage >= 0.8f)
            {
                return null;
            }

            // Find an available partner
            Pawn partner = FindPartnerFor(pawn);

            if (partner != null)
            {
                return new Job(DefDatabase<JobDef>.GetNamed("AskForDate"), partner);
            }

            return null;
        }

        private Pawn FindPartnerFor(Pawn pawn)
        {
            // Check for spouse, lover, or fiance
            foreach (PawnRelationDef relation in new[] { PawnRelationDefOf.Spouse, PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance })
            {
                Pawn partner = pawn.relations.GetFirstDirectRelationPawn(relation, x => 
                    !x.Dead && 
                    x.Map == pawn.Map && 
                    x.Awake() && 
                    !x.Downed && 
                    pawn.CanReserve(x) && 
                    (x.jobs.curDriver is JobDriver_HaveDeepTalk == false && x.jobs.curDriver is JobDriver_OnDate == false)
                );
                if (partner != null)
                {
                    return partner;
                }
            }
            return null;
        }
    }
}
