using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;

namespace SocialInteractions
{
    [StaticConstructorOnStartup]
    public static class SocialInteractions
    {
        static SocialInteractions()
        {
            var harmony = new Harmony("com.gemini.socialinteractions");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class Pawn_InteractionsTracker_TryInteractWith_Patch
    {
        public static void Postfix(bool __result, Pawn_InteractionsTracker __instance, Pawn recipient)
        {
            Pawn initiator = (Pawn)AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn").GetValue(__instance);
            if (__result && initiator != null && recipient != null)
            {
                // Pawn stopping logic
                int waitTicks = 120; // 2 seconds

                Job initiatorJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, waitTicks);
                initiator.jobs.StartJob(initiatorJob, JobCondition.InterruptForced);

                Job recipientJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, waitTicks);
                recipient.jobs.StartJob(recipientJob, JobCondition.InterruptForced);
            }
        }
    }

    [HarmonyPatch(typeof(PlayLog), "Add")]
    public static class PlayLog_Add_Patch
    {
        public static void Postfix(LogEntry entry)
        {
            if (entry.GetType().Name == "PlayLogEntry_Interaction")
            {
                var intDefField = entry.GetType().GetField("intDef", BindingFlags.NonPublic | BindingFlags.Instance);
                var interactionDef = intDefField.GetValue(entry) as InteractionDef;

                if (interactionDef == InteractionDefOf.Chitchat)
                {
                    var initiatorField = entry.GetType().GetField("initiator", BindingFlags.NonPublic | BindingFlags.Instance);
                    Pawn initiator = initiatorField.GetValue(entry) as Pawn;

                    if (initiator != null)
                    {
                        string text = entry.ToGameStringFromPOV(initiator);
                        if (!string.IsNullOrEmpty(text))
                        {
                            MoteMaker.ThrowText(initiator.DrawPos, initiator.Map, text);
                        }
                    }
                }
            }
        }
    }
}