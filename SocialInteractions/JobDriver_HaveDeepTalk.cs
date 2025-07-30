using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SocialInteractions
{
    public class JobDriver_HaveDeepTalk : JobDriver
    {
        private Pawn Recipient { get { return (Pawn)job.GetTarget(TargetIndex.A).Thing; } }
        private bool llmTaskComplete = false;
        private string llmResponse;
        private List<string> messages = new List<string>();
        private bool conversationComplete = false;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Recipient, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Pawn recipient = (Pawn)job.GetTarget(TargetIndex.A).Thing;
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => recipient == null || !recipient.Spawned || !recipient.Awake());

            // Go to the recipient
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Face each other
            Toil faceToil = new Toil();
            faceToil.initAction = () => {
                pawn.rotationTracker.FaceCell(recipient.Position);
                recipient.rotationTracker.FaceCell(pawn.Position);
            };
            faceToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return faceToil;

            // Get LLM response
            Toil getLlmResponseToil = new Toil();
            getLlmResponseToil.initAction = () => {
                llmTaskComplete = false;
                SpeechBubbleManager.isConversationActive = true;
                SpeechBubbleManager.onConversationFinished += () => conversationComplete = true;

                Pawn recipientForTask = recipient;
                Job_HaveDeepTalk customJob = this.job as Job_HaveDeepTalk;
                if (customJob == null)
                {
                    Log.Error("Job is not a Job_HaveDeepTalk. Ending job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                    return;
                }
                InteractionDef interactionDefForTask = customJob.interactionDef;
                if (interactionDefForTask == null)
                {
                    Log.Error("InteractionDef is null. Ending job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                    return;
                }
                if (interactionDefForTask == null)
                {
                    Log.Error("InteractionDef is null. Ending job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                    return;
                }
                if (interactionDefForTask == null)
                {
                    Log.Error("InteractionDef is null. Ending job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                    return;
                }

                Task.Run(async () => {
                    if (recipientForTask == null)
                    {
                        Log.Error("Recipient became null before LLM task could run.");
                        llmTaskComplete = true;
                        SpeechBubbleManager.isConversationActive = false;
                        return;
                    }

                    if (SocialInteractions.Settings == null)
                    {
                        Log.Error("SocialInteractions.Settings is null. Cannot generate LLM response.");
                        llmTaskComplete = true;
                        SpeechBubbleManager.isConversationActive = false;
                        return;
                    }
                    if (string.IsNullOrEmpty(SocialInteractions.Settings.llmApiUrl))
                    {
                        Log.Error("LLM API URL is not set in mod settings. Cannot generate LLM response.");
                        llmTaskComplete = true;
                        SpeechBubbleManager.isConversationActive = false;
                        return;
                    }
                    try
                    {
                        string jobDefName = "UnknownJob";
                        if (job != null && job.def != null)
                        {
                            jobDefName = job.def.defName;
                        }
                        string prompt = SocialInteractions.GenerateDeepTalkPrompt(pawn, recipientForTask, interactionDefForTask, jobDefName);
                        if (!string.IsNullOrEmpty(prompt))
                        {
                            KoboldApiClient client = new KoboldApiClient(SocialInteractions.Settings.llmApiUrl, SocialInteractions.Settings.llmApiKey);
                            List<string> stoppingStrings = new List<string>();
                            if (SocialInteractions.Settings.llmStoppingStrings != null)
                            {
                                stoppingStrings = new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                            }
                            llmResponse = await client.GenerateText(prompt, SocialInteractions.Settings.llmMaxTokens, SocialInteractions.Settings.llmTemperature, stoppingStrings);
                            if (!string.IsNullOrEmpty(llmResponse))
                            {
                                messages = llmResponse.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("Error generating LLM response: {0} {1}", ex.Message, ex.StackTrace));
                        SpeechBubbleManager.isConversationActive = false;
                    }
                    finally
                    {
                        llmTaskComplete = true;
                    }
                });
            };
            getLlmResponseToil.tickAction = () => {
                if (llmTaskComplete)
                {
                    getLlmResponseToil.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            getLlmResponseToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return getLlmResponseToil;

            // Display messages
            Toil displayMessagesToil = new Toil();
            displayMessagesToil.initAction = () => {
                Pawn recipientForDisplay = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (recipientForDisplay == null) return;

                for (int i = 0; i < messages.Count; i++)
                {
                    string rawMessage = messages[i].Trim();
                    Pawn speaker = null;

                    if (rawMessage.StartsWith(pawn.Name.ToStringShort + ":"))
                    {
                        speaker = pawn;
                    }
                    else if (rawMessage.StartsWith(recipientForDisplay.Name.ToStringShort + ":"))
                    {
                        speaker = recipientForDisplay;
                    }
                    else
                    {
                        speaker = pawn;
                    }

                    if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                    {
                        string wrappedMessage = SocialInteractions.WrapText(rawMessage, SocialInteractions.Settings.wordsPerLineLimit);
                        float duration = SocialInteractions.EstimateReadingTime(rawMessage) / 1000f;
                        SpeechBubbleManager.Enqueue(speaker, wrappedMessage, duration, i == 0);
                    }
                }
            };
            displayMessagesToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return displayMessagesToil;

            // Wait for conversation to finish
            Toil waitForConversationToil = new Toil();
            waitForConversationToil.tickAction = () => {
                if (job.def.joyKind != null)
                {
                    pawn.needs.joy.GainJoy(0.00015f, job.def.joyKind);
                }
                if (conversationComplete)
                {
                    waitForConversationToil.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            waitForConversationToil.defaultCompleteMode = ToilCompleteMode.Never;
            waitForConversationToil.AddFinishAction(() => {
                SpeechBubbleManager.isLlmBusy = false;
                Pawn finalRecipient = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (finalRecipient != null && finalRecipient.jobs != null)
                {
                    JobDriver_BeTalkedTo recipientDriver = finalRecipient.jobs.curDriver as JobDriver_BeTalkedTo;
                    if (recipientDriver != null)
                    {
                        recipientDriver.EndJob(JobCondition.Succeeded);
                    }
                }
            });
            yield return waitForConversationToil;
        }
    }
}