using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Reflection;

namespace SocialInteractions
{
    // Patch the transition from gathering to actual ceremony to trigger LLM once at the beginning
    [HarmonyPatch(typeof(LordJob_Joinable_MarriageCeremony), "CreateGraph")]
    public static class LordJob_Joinable_MarriageCeremony_CreateGraph_Patch
    {
        static void Postfix(LordJob_Joinable_MarriageCeremony __instance, StateGraph __result)
        {
            // Find the transition that leads to the marriage ceremony (from gathering to actual vows)
            // Looking at the decompiled code structure, we're looking for the transition that has a
            // TransitionAction_Message with "MessageMarriageCeremonyStarts"
            foreach (var transition in __result.transitions)
            {
                // Check if this transition has a pre-action with the marriage ceremony message
                if (transition.preActions != null)
                {
                    foreach (var preAction in transition.preActions)
                    {
                        // We could try to identify the specific message action, but let's just target
                        // any transition that leads to the marriage ceremony toil
                        // In the original code, the marriage ceremony toil is the destination
                        // We'll use reflection to check the transition's destination toil
                        var destinationField = typeof(Transition).GetField("destToil", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (destinationField != null)
                        {
                            var destinationToil = destinationField.GetValue(transition) as LordToil;
                            if (destinationToil is LordToil_MarriageCeremony)
                            {
                                // This is the transition from party to marriage ceremony - add our LLM trigger
                                transition.AddPreAction(new TransitionAction_Custom(() =>
                                {
                                    TriggerMarriageCeremonyLLM(__instance);
                                }));
                                break; // Only add to the first matching transition
                            }
                        }
                    }
                }
            }
        }

        private static void TriggerMarriageCeremonyLLM(LordJob_Joinable_MarriageCeremony lordJob)
        {
            // Check if LLM is enabled for marriage ceremonies
            if (!SocialInteractions.IsLlmMarriageCeremonyEnabled())
            {
                return;
            }

            // Check if LLM is busy and if we should prevent spam
            if (SocialInteractions.Settings.preventSpam && SpeechBubbleManager.IsLlmCurrentlyBusy())
            {
                return;
            }

            // Get the pawns involved in the ceremony
            var firstPawn = lordJob.firstPawn;
            var secondPawn = lordJob.secondPawn;

            if (firstPawn == null || secondPawn == null)
            {
                return;
            }

            SLog.Message(string.Format("[SocialInteractions] Marriage ceremony beginning detected for {0} and {1}", 
                firstPawn.LabelShort, secondPawn.LabelShort));

            // Create a subject for the marriage ceremony based on the pawn names and context
            string subject = string.Format("{0} and {1} are exchanging vows in their marriage ceremony.", 
                firstPawn.LabelShort, secondPawn.LabelShort);

            // Generate LLM prompt and handle interaction
            string prompt = SocialInteractions.GenerateDeepTalkPrompt(firstPawn, secondPawn, null, subject);

            if (!string.IsNullOrEmpty(prompt))
            {
                SLog.Message("[SocialInteractions] Marriage ceremony has a valid prompt, creating LLM interaction.");

                // Handle the interaction using the same approach as other LLM interactions
                int conversationId = SocialInteractions.HandleNonStoppingInteraction(firstPawn, secondPawn, null, subject, true, true);

                // Show a default bubble to indicate the ceremony is happening if LLM doesn't respond quickly
                string formattedSubject = SpeechBubbleManager.FormatSpeakerName(firstPawn, "Exchanging vows...");
                SpeechBubbleManager.ShowDefaultBubble(firstPawn, formattedSubject);
            }
            else
            {
                SLog.Message("[SocialInteractions] Marriage ceremony does not have a valid prompt, proceeding with default behavior.");
            }
        }
    }
}