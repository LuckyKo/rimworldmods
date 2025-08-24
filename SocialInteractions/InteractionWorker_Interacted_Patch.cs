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
            // SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch.Postfix called. pawnsStopOnInteraction: {0}", SocialInteractions.Settings.pawnsStopOnInteraction));
            
            // Only handle interactions when pawns stop on interaction (stopping interactions)
            if (SocialInteractions.Settings.pawnsStopOnInteraction)
            {
                // SLog.Message("[SocialInteractions] pawnsStopOnInteraction is true, proceeding with job creation.");
                
                // Get the interaction definition from the __instance parameter
                InteractionDef interactionDef = __instance.interaction;
                // SLog.Message(string.Format("[SocialInteractions] InteractionDef retrieved: {0}", interactionDef != null ? interactionDef.defName : "NULL"));
                
                if (interactionDef != null)
                {
                    // SLog.Message(string.Format("[SocialInteractions] Checking if interaction should be handled: {0}", interactionDef.defName));
                    
                    if ((interactionDef == InteractionDefOf.Chitchat && SocialInteractions.Settings.enableChitchat) ||
                        (interactionDef == InteractionDefOf.RomanceAttempt && SocialInteractions.Settings.enableRomanceAttempt) ||
                        (interactionDef == InteractionDefOf.DeepTalk && SocialInteractions.Settings.enableDeepTalk) ||
                        (interactionDef == InteractionDefOf.Insult && SocialInteractions.Settings.enableInsult) ||
                        (interactionDef == InteractionDefOf.MarriageProposal && SocialInteractions.Settings.enableMarriageProposal) ||
                        (interactionDef == InteractionDefOf.Reassure && SocialInteractions.Settings.enableReassure) ||
                        (interactionDef == InteractionDefOf.DisturbingChat && SocialInteractions.Settings.enableDisturbingChat))
                    {
                        // SLog.Message(string.Format("[SocialInteractions] Interaction {0} matches criteria, checking if LLM interaction is enabled.", interactionDef.defName));
                        
                        // Create a PlayLogEntry_Interaction to get the social log message
                        PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(interactionDef, initiator, recipient, extraSentencePacks);
                        string subject = SocialInteractions.RemoveRichTextTags(entry.ToGameStringFromPOV(initiator));
                        
                        // Check if LLM interaction is enabled for this interaction type
                        bool isLlmEnabled = SocialInteractions.IsLlmInteractionEnabled(interactionDef);
                        
                        if (isLlmEnabled)
                        {
                            // For LLM-enabled interactions, check if we can generate a prompt
                            string prompt = SocialInteractions.GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);
                            
                            // Check if LLM is busy and if we should prevent spam
                            if (SocialInteractions.Settings.preventSpam && SpeechBubbleManager.isLlmBusy)
                            {
                                // SLog.Message(string.Format("[SocialInteractions] Interaction {0} - LLM is busy and preventSpam is true, showing default bubble without creating jobs.", interactionDef.defName));
                                // Show default bubble when LLM is busy and we're preventing spam
                                if (!string.IsNullOrEmpty(subject))
                                {
                                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                                }
                                return;
                            }
                            
                            // Only create jobs if we can generate a prompt (i.e., an actual LLM request will be sent)
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} has a valid prompt, creating jobs.", interactionDef.defName));
                                
                                // For LLM-enabled interactions, format the text with rich text formatting
                                string formattedSubject = SpeechBubbleManager.FormatSpeakerName(initiator, subject);
                                SpeechBubbleManager.ShowDefaultBubble(initiator, formattedSubject);

                                Job_HaveDeepTalk initiatorJob = new Job_HaveDeepTalk(DefDatabase<JobDef>.GetNamed("HaveDeepTalk"), recipient);
                                initiatorJob.interactionDef = interactionDef;
                                initiatorJob.subject = subject;
                                initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                                Job recipientJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), initiator);
                                recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                                
                                // SLog.Message("[SocialInteractions] InteractionWorker_Interacted_Patch. Jobs created successfully.");
                            }
                            else
                            {
                                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} does not have a valid prompt, showing default bubble without creating jobs.", interactionDef.defName));
                                // For non-LLM interactions, show the default text
                                SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                            }
                        }
                        else
                        {
                            // SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} is not LLM-enabled, showing default bubble without creating jobs.", interactionDef.defName));
                            // For non-LLM interactions, show the default text
                            SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                        }
                    }
                    else
                    {
                        // SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} does not match criteria.", interactionDef.defName));
                    }
                }
                else
                {
                    SLog.Warning("[SocialInteractions] InteractionWorker_Interacted_Patch. InteractionDef is null, skipping job creation.");
                }
            }
            else
            {
                // SLog.Message("[SocialInteractions] InteractionWorker_Interacted_Patch. pawnsStopOnInteraction is false, using default behavior.");
            }
        }
    }
}