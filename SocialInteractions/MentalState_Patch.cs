using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Verse.AI.MentalStateHandler), "TryStartMentalState")]
    public static class MentalState_Patch
    {
        public static void Postfix(Verse.AI.MentalStateHandler __instance, bool __result, MentalStateDef stateDef)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

            // Only trigger if the mental state was successfully started and the pawn is a player colonist
            if (!__result || pawn == null || !pawn.IsColonistPlayerControlled)
            {
                return;
            }

            // The subject of the monologue will be the label of the mental state (e.g., "Berserk", "Sad wander")
            string subject = " is experiencing " + stateDef.LabelCap;

            // Call the monologue handler
            SocialInteractions.HandleMonologue(pawn, subject);
        }
    }
}
