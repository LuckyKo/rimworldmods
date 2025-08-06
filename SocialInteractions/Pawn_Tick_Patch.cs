using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Pawn_Tick_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            Pawn pawn = __instance;
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