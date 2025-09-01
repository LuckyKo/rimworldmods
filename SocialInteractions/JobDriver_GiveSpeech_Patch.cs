using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using System.Linq;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(RimWorld.JobDriver_GiveSpeech), "TryMakePreToilReservations")]
    public static class JobDriver_GiveSpeech_Patch
    {
        public static void Postfix(RimWorld.JobDriver_GiveSpeech __instance, bool __result)
        {
            if (!__result || __instance.pawn == null || !__instance.pawn.IsColonistPlayerControlled)
            {
                return;
            }

            string subject = "is about to give a speech";

            Lord lord = __instance.pawn.GetLord();
            if (lord != null)
            {
                LordJob_Ritual lordJob_Ritual = lord.LordJob as LordJob_Ritual;
                if (lordJob_Ritual != null)
                {
                    Precept_Ritual ritual = lordJob_Ritual.Ritual;
                    if (ritual != null)
                    {
                        // Log all roles and assigned pawns for debugging
                        foreach (var group in lordJob_Ritual.assignments.RoleGroups())
                        {
                            SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Ritual role ID: " + group.Key);
                            foreach (var role in group)
                            {
                                foreach (Pawn p in lordJob_Ritual.assignments.AssignedPawns(role))
                                {
                                    SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Pawn in role " + group.Key + ": " + p.Name.ToStringShort);
                                }
                            }
                        }

                        Pawn executioner = lordJob_Ritual.assignments.FirstAssignedPawn("executioner");
                        Pawn prisoner = lordJob_Ritual.assignments.FirstAssignedPawn("prisoner");

                        if (executioner != null && prisoner != null)
                        {
                            subject = "is giving a speech for the execution of " + prisoner.Name.ToStringShort;
                            SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Found executioner and prisoner, subject: " + subject);
                        }
                        else
                        {
                            // Log which pawns are null for debugging
                            if (executioner == null)
                                SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: executioner is null");
                            if (prisoner == null)
                                SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: prisoner is null");
                            
                            subject = "is giving a speech for the " + ritual.LabelCap + " ritual";
                            SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Using fallback subject: " + subject);
                        }
                    }
                }
            }

            SocialInteractions.HandleMonologue(__instance.pawn, subject, true);
        }
    }
}
