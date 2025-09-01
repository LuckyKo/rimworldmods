using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Faction), "TryGenerateNewLeader")]
    public static class Faction_Patch
    {
        public static void Postfix(Faction __instance, bool __result)
        {
            // Only trigger if a new leader was successfully generated and it's the player's faction
            if (!__result || __instance != Faction.OfPlayer)
            {
                return;
            }

            Pawn newLeader = __instance.leader;
            if (newLeader == null || !newLeader.IsColonistPlayerControlled)
            {
                return;
            }

            string subject = " has become the new leader";

            // Call the monologue handler
            SocialInteractions.HandleMonologue(newLeader, subject, true);
        }
    }
}
