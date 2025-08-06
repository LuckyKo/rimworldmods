using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(PlayLog), "Add")]
    public static class PlayLog_Add_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayLog __instance, LogEntry entry)
        {
            if (entry.GetType().Name == "PlayLogEntry_Interaction")
            {
                var intDefField = entry.GetType().GetField("intDef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var interactionDef = intDefField.GetValue(entry) as InteractionDef;

                var initiatorField = entry.GetType().GetField("initiator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Pawn initiator = initiatorField.GetValue(entry) as Pawn;

                var recipientField = entry.GetType().GetField("recipient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Pawn recipient = recipientField.GetValue(entry) as Pawn;

                if (initiator != null && recipient != null)
                {
                    string defaultText = SocialInteractions.RemoveRichTextTags(entry.ToGameStringFromPOV(initiator));

                    if (interactionDef == SI_InteractionDefOf.DateAccepted)
                    {
                        string subject = SpeechBubbleManager.GetDateSubject(initiator, recipient);
                        SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, interactionDef, subject);
                    }
                    else
                    {
                        SocialInteractions.HandleInteraction(initiator, recipient, interactionDef, defaultText);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_TendPatient), "MakeNewToils")]
    public static class JobDriver_TendPatient_Patch
    {
        public static void Postfix(JobDriver_TendPatient __instance, ref IEnumerable<Toil> __result)
        {
            if (!SocialInteractions.IsLlmJobEnabled(__instance))
            {
                return;
            }

            var newToils = new List<Toil>(__result);
            // Find the toil where the tending sound is played.
            var tendToil = newToils.FirstOrDefault(t => t.PlaySustainerOrSound(() => SoundDefOf.Interact_Tend) != null);

            if (tendToil != null)
            {
                tendToil.AddFinishAction(() =>
                {
                    Pawn doctor = __instance.pawn;
                    Pawn patient = (Pawn)__instance.job.targetA.Thing;
                    SocialInteractions.HandleNonStoppingInteraction(doctor, patient, SI_InteractionDefOf.TendPatient, "Tending to patient");
                });
            }
            else
            {
                // Log.Warning("SocialInteractions Mod: Could not find the correct toil to patch for 'TendPatient' interaction. Dialogue will not be triggered.");
            }

            __result = newToils;
        }
    }

    [HarmonyPatch(typeof(JobGiver_RescueNearby), "TryGiveJob")]
    public static class JobGiver_RescueNearby_Patch
    {
        public static void Postfix(JobGiver_RescueNearby __instance, ref Job __result, Pawn pawn)
        {
            if (__result == null || !SocialInteractions.Settings.enableRescue)
            {
                return;
            }

            Pawn rescuer = pawn;
            Pawn patient = (Pawn)__result.targetA.Thing;
            SocialInteractions.HandleJobGiverInteraction(rescuer, patient, SI_InteractionDefOf.Rescue, "Rescuing patient");
        }
    }

    [HarmonyPatch(typeof(JoyGiver_VisitSickPawn), "TryGiveJob")]
    public static class JoyGiver_VisitSickPawn_Patch
    {
        public static void Postfix(JoyGiver_VisitSickPawn __instance, ref Job __result, Pawn pawn)
        {
            if (__result == null || !SocialInteractions.Settings.enableVisitSickPawn)
            {
                return;
            }

            Pawn visitor = pawn;
            Pawn patient = (Pawn)__result.targetA.Thing;
            SocialInteractions.HandleJobGiverInteraction(visitor, patient, SI_InteractionDefOf.VisitSickPawn, "Visiting sick pawn");
        }
    }

    [HarmonyPatch(typeof(JobDriver_Lovin), "MakeNewToils")]
    public static class JobDriver_Lovin_Patch
    {
        public static void Postfix(JobDriver_Lovin __instance, ref IEnumerable<Toil> __result)
        {
            if (!SocialInteractions.IsLlmJobEnabled(__instance)) return;

            Pawn initiator = __instance.pawn;
            Pawn partner = (Pawn)__instance.job.targetA.Thing;

            // Symmetry breaking: only the pawn whose name comes first alphabetically will trigger the dialogue.
            if (initiator.Name.ToStringShort.CompareTo(partner.Name.ToStringShort) < 0)
            {
                var newToils = new List<Toil>(__result);
                int lovinToilIndex = newToils.FindIndex(t => t.socialMode == RandomSocialMode.Off);

                if (lovinToilIndex != -1)
                {
                    Toil dialogueToil = new Toil();
                    dialogueToil.initAction = () =>
                    {
                        SocialInteractions.HandleNonStoppingInteraction(initiator, partner, SI_InteractionDefOf.Lovin, "Lying in bed together, about to make love");
                    };
                    dialogueToil.defaultCompleteMode = ToilCompleteMode.Instant;

                    newToils.Insert(lovinToilIndex, dialogueToil);
                }
                else
                {
                    // Log.Warning("SocialInteractions Mod: Could not find the correct toil to patch for 'Lovin' interaction.");
                }
                
                __result = newToils;

                // Add a finish action to the last toil to reset isLlmBusy
                Toil lastToil = newToils[newToils.Count - 1];
                if (lastToil != null)
                {
                    lastToil.AddFinishAction(() =>
                    {
                        SpeechBubbleManager.isLlmBusy = false;
                    });
                }
            }
        }
    }
}