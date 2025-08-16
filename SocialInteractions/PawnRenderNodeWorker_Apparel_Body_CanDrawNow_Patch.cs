using HarmonyLib;
using RimWorld;
using Verse;
using System;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Body), "CanDrawNow")]
    public static class PawnRenderNodeWorker_Apparel_Body_CanDrawNow_Patch
    {
        public static void Postfix(PawnRenderNode node, ref bool __result)
        {
            try
            {
                if (node?.tree?.pawn?.health?.hediffSet?.HasHediff(SI_HediffDefOf.SI_Naked) ?? false)
                {
                    __result = false;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[SocialInteractions] Exception in PawnRenderNodeWorker_Apparel_Body_CanDrawNow_Patch: {e}");
            }
        }
    }
}