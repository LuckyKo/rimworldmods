using HarmonyLib;
using UnityEngine;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    public static class PawnRenderer_RenderPawnAt_Patch
    {
        public static void Prefix(PawnRenderer __instance, ref Vector3 drawLoc, Pawn ___pawn)
        {
            if (LovinBouncer.bounces.TryGetValue(___pawn, out float bounceOffset))
            {
                drawLoc.y += bounceOffset;
            }
        }
    }
}
