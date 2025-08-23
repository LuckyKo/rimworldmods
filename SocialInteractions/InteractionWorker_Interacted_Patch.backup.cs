using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(InteractionWorker), "Interacted")]
    public static class InteractionWorker_Interacted_Patch
    {
        public static void Postfix(InteractionWorker __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, string letterText, string letterLabel, LetterDef letterDef, LookTargets lookTargets)
        {
            // Only handle interactions when pawns stop on interaction (stopping interactions)
            if (SocialInteractions.Settings.pawnsStopOnInteraction)
            {
                // Handle all LLM-enabled interactions for stopping interactions
                if ((__instance.InteractionDef == InteractionDefOf.Chitchat && SocialInteractions.Settings.enableChitchat) ||
                    (__instance.InteractionDef == InteractionDefOf.RomanceAttempt && SocialInteractions.Settings.enableRomanceAttempt) ||
                    (__instance.InteractionDef == InteractionDefOf.DeepTalk && SocialInteractions.Settings.enableDeepTalk) ||
                    (__instance.InteractionDef == InteractionDefOf.Insult && SocialInteractions.Settings.enableInsult) ||
                    (__instance.InteractionDef == InteractionDefOf.MarriageProposal && SocialInteractions.Settings.enableMarriageProposal) ||
                    (__instance.InteractionDef == InteractionDefOf.Reassure && SocialInteractions.Settings.enableReassure) ||
                    (__instance.InteractionDef == InteractionDefOf.DisturbingChat && SocialInteractions.Settings.enableDisturbingChat))
                {
                    // Create a PlayLogEntry_Interaction to get the social log message
                    PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(__instance.InteractionDef, initiator, recipient, extraSentencePacks);
                    string subject = SocialInteractions.RemoveRichTextTags(entry.ToGameStringFromPOV(initiator));
                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);

                    Job_HaveDeepTalk initiatorJob = new Job_HaveDeepTalk(DefDatabase<JobDef>.GetNamed("HaveDeepTalk"), recipient);
                    initiatorJob.interactionDef = __instance.InteractionDef;
                    initiatorJob.subject = subject;
                    initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                    Job recipientJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), initiator);
                    recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                }
            }
        }
    }
}