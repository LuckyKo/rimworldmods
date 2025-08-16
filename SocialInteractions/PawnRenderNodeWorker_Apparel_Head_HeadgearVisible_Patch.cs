using HarmonyLib;
using RimWorld;
using Verse;
using System;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Head), "HeadgearVisible")]
    public static class PawnRenderNodeWorker_Apparel_Head_HeadgearVisible_Patch
    {
        public static void Postfix(PawnDrawParms parms, ref bool __result)
        {
            try
            {
                if (parms?.pawn?.health?.hediffSet?.HasHediff(SI_HediffDefOf.SI_Naked) ?? false)
                {
                    __result = false;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[SocialInteractions] Exception in PawnRenderNodeWorker_Apparel_Head_HeadgearVisible_Patch: {e}");
            }
        }
    }
}