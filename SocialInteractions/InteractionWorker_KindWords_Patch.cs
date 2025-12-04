using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(InteractionWorker_KindWords), "Interacted")]
    public static class InteractionWorker_KindWords_Patch
    {
        public static void Postfix(InteractionWorker_KindWords __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
        {
            if (!SocialInteractions.Settings.enableKindWordsInteractions || !SocialInteractions.Settings.llmInteractionsEnabled)
            {
                return;
            }

            if (initiator == null || recipient == null)
            {
                return;
            }

            // Construct the subject for the LLM prompt
            string subject = string.Format("{0} offering kind words to {1}.", initiator.LabelShort, recipient.LabelShort);

            // Get the KindWords interaction definition from the game
            InteractionDef kindWordsDef = DefDatabase<InteractionDef>.GetNamed("KindWords", false);
            if (kindWordsDef == null)
            {
                // Fallback to Chitchat if KindWords is not available
                kindWordsDef = InteractionDefOf.Chitchat;
            }

            // Trigger the LLM interaction
            // Use HandleNonStoppingInteraction as this is a quick interaction
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, kindWordsDef, subject);
        }
    }
}
