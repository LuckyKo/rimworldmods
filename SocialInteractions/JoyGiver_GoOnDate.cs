using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JoyGiver_GoOnDate : JoyGiver
    {
        private static Dictionary<Pawn, int> lastAttemptTick = new Dictionary<Pawn, int>();
        private const int CooldownTicks = 600; // 10 seconds (60 ticks per second)

        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null) return null;

            if (DatingManager.IsOnDateCooldown(pawn))
            {
                return null;
            }

            int lastTick;
            if (lastAttemptTick.TryGetValue(pawn, out lastTick) && Find.TickManager.TicksGame - lastTick < CooldownTicks)
            {
                return null;
            }

            lastAttemptTick[pawn] = Find.TickManager.TicksGame;

            if (DatingManager.IsOnDate(pawn))
            {
                return null;
            }

            if (pawn.jobs != null && pawn.jobs.curJob != null &&
                (pawn.jobs.curJob.def == DefDatabase<JobDef>.GetNamed("AskForDate") || pawn.jobs.curJob.def == DefDatabase<JobDef>.GetNamed("GoOnDate")))
            {
                return null;
            }

            if (pawn.needs == null || pawn.needs.joy == null || pawn.needs.joy.CurLevelPercentage > 0.9f)
            {
                return null;
            }

            Pawn partner = FindPartnerFor(pawn);
            if (partner == null)
            {
                return null;
            }

            if (!SocialInteractionUtility.CanInitiateInteraction(pawn) || !SocialInteractionUtility.CanReceiveInteraction(partner) || !pawn.CanReserve(partner))
            {
                return null;
            }

            JobDef askForDateJobDef = DefDatabase<JobDef>.GetNamed("AskForDate");
            if (askForDateJobDef == null) return null;
            Job job = JobMaker.MakeJob(askForDateJobDef, partner);
            return job;
        }

        private Pawn FindPartnerFor(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null) return null;
            return (Pawn)pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && p.relations != null &&
                            (p.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn) ||
                             p.relations.DirectRelationExists(PawnRelationDefOf.Fiance, pawn) ||
                             p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn) ||
                             p.relations.OpinionOf(pawn) > 10))
                .FirstOrDefault();
        }
    }
}