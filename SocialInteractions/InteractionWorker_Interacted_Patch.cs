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
            if (__instance.InteractionDef == InteractionDefOf.Chitchat)
            {
                if (SocialInteractions.Settings.enableChitchat)
                {
                    string subject = __instance.InteractionDef.label;
                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);

                    if (SocialInteractions.Settings.pawnsStopOnInteraction)
                    {
                        Job_HaveDeepTalk initiatorJob = new Job_HaveDeepTalk(DefDatabase<JobDef>.GetNamed("HaveDeepTalk"), recipient);
                        initiatorJob.interactionDef = __instance.InteractionDef;
                        initiatorJob.subject = subject;
                        initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                        Job recipientJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), initiator);
                        recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                    }
                    else
                    {
                        SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, __instance.InteractionDef, subject);
                    }
                }
            }
        }
    }
}