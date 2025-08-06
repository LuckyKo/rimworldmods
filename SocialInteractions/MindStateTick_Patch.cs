using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn_MindState), "MindStateTick")]
    public static class MindStateTick_Patch
    {
        public static void Postfix(Pawn_MindState __instance)
        {
            Pawn pawn = __instance.pawn;
            if (pawn.IsHashIntervalTick(60)) // Check every second
            {
                Pawn partner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, (p) => !p.Dead);
                if (partner == null)
                {
                    partner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, (p) => !p.Dead);
                }
                if (partner == null)
                {
                    partner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, (p) => !p.Dead);
                }

                if (partner != null && DatingManager.IsOnDate(partner))
                {
                    Pawn cheatingPartner = DatingManager.GetPartnerOnDateWith(partner);
                    if (cheatingPartner != null && cheatingPartner != pawn && pawn.Position.InHorDistOf(partner.Position, 10f))
                    {
                        // Caught them!
                        InteractionDef intDef = DefDatabase<InteractionDef>.GetNamed("CaughtCheating");
                        intDef.Worker.Interacted(pawn, partner, null, out _, out _, out _, out _);
                    }
                }
            }
        }
    }
}