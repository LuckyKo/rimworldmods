using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
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
                // Get the interaction definition through reflection with proper error handling
                InteractionDef interactionDef = null;
                try
                {
                    var field = typeof(InteractionWorker).GetField("intDef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        interactionDef = (InteractionDef)field.GetValue(__instance);
                    }
                }
                catch (System.Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Error getting interaction def from InteractionWorker: {0}", ex.Message));
                }
                
                if (interactionDef != null)
                {
                    if ((interactionDef == InteractionDefOf.Chitchat && SocialInteractions.Settings.enableChitchat) ||
                        (interactionDef == InteractionDefOf.RomanceAttempt && SocialInteractions.Settings.enableRomanceAttempt) ||
                        (interactionDef == InteractionDefOf.DeepTalk && SocialInteractions.Settings.enableDeepTalk) ||
                        (interactionDef == InteractionDefOf.Insult && SocialInteractions.Settings.enableInsult) ||
                        (interactionDef == InteractionDefOf.MarriageProposal && SocialInteractions.Settings.enableMarriageProposal) ||
                        (interactionDef == InteractionDefOf.Reassure && SocialInteractions.Settings.enableReassure) ||
                        (interactionDef == InteractionDefOf.DisturbingChat && SocialInteractions.Settings.enableDisturbingChat))
                    {
                        // Create a PlayLogEntry_Interaction to get the social log message
                        PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(interactionDef, initiator, recipient, extraSentencePacks);
                        string subject = SocialInteractions.RemoveRichTextTags(entry.ToGameStringFromPOV(initiator));
                        SpeechBubbleManager.ShowDefaultBubble(initiator, subject);

                        Job_HaveDeepTalk initiatorJob = new Job_HaveDeepTalk(DefDatabase<JobDef>.GetNamed("HaveDeepTalk"), recipient);
                        initiatorJob.interactionDef = interactionDef;
                        initiatorJob.subject = subject;
                        initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                        Job recipientJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), initiator);
                        recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                    }
                }
            }
        }
    }
}