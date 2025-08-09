using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace SocialInteractions
{
    public class JoyGiver_GoOnDate : JoyGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null) return null;

            // Add this check to prevent new date jobs if pawn is already on a date
            if (DatingManager.IsOnDate(pawn))
            {
                return null;
            }

            // 1. Check if pawn needs joy
            if (pawn.needs == null || pawn.needs.joy == null || pawn.needs.joy.CurLevelPercentage > 0.9f)
            {
                return null;
            }

            // 2. Find a suitable partner
            Pawn partner = FindPartnerFor(pawn);
            if (partner == null)
            {
                return null;
            }

            // 3. Check if both pawns can interact
            if (!SocialInteractionUtility.CanInitiateInteraction(pawn) || !SocialInteractionUtility.CanReceiveInteraction(partner))
            {
                return null;
            }

            // 4. Create and return the "AskForDate" job
            JobDef askForDateJobDef = DefDatabase<JobDef>.GetNamed("AskForDate");
            if (askForDateJobDef == null) return null;
            Job job = JobMaker.MakeJob(askForDateJobDef, partner);
            return job;
        }

        private Pawn FindPartnerFor(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null) return null;
            // Find a pawn who is a lover, fiance, or spouse
            return (Pawn)pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && p.relations != null &&
                            (p.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn) ||
                             p.relations.DirectRelationExists(PawnRelationDefOf.Fiance, pawn) ||
                             p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn)))
                .FirstOrDefault();
        }
    }
}