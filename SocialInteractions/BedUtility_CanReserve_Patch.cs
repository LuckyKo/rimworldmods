using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(BedUtility), "CanReserve")]
    public static class BedUtility_CanReserve_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, Building_Bed bed, ref bool __result)
        {
            if (bed.OwnersForReading.Contains(p))
            {
                __result = true;
            }
        }
    }
}
