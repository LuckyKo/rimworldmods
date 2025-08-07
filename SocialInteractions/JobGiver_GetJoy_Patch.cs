using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(JobGiver_GetJoy), "TryGiveJob")]
    public static class JobGiver_GetJoy_Patch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            // Don't interfere if pawn's joy is high
            if (pawn.needs.joy.CurLevel > 0.8f)
            {
                return true; // Let original method run
            }

            // Find a romantic partner
            Pawn partner = FindRomanticPartnerFor(pawn);
            if (partner != null)
            {
                float chance = 0.5f;
                if (pawn.jobs.curJob != null && pawn.jobs.curJob.def == JobDefOf.Wait_Wander)
                {
                    chance = 0.8f;
                }

                if (Rand.Value < chance)
                {
                    // Instead of creating a job, we'll create an interaction
                    InteractionDef intDef = DefDatabase<InteractionDef>.GetNamed("AskForDate");
                    pawn.interactions.TryInteractWith(partner, intDef);
                    return true; // Allow original method to run and assign a joy job
                }
            }

            return true; // Let original method run
        }

        private static Pawn FindRomanticPartnerFor(Pawn pawn)
        {
            var potentialPartners = pawn.Map.mapPawns.AllPawns.Where(p => p != pawn && p.IsColonist && !p.IsPrisoner && !p.Downed && p.Awake()).ToList();
            foreach(var p in potentialPartners)
            {
                if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, p) || pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, p) || pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, p))
                {
                    return p;
                }
            }
            return null;
        }
    }
}