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
        private int conversationId = -1;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref llmTaskComplete, "llmTaskComplete", false);
            Scribe_Values.Look(ref llmResponse, "llmResponse");
            Scribe_Collections.Look(ref messages, "messages", LookMode.Value);
            Scribe_Values.Look(ref conversationId, "conversationId", -1);
        }

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
                try
                {
                    llmTaskComplete = false;

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

                    string subjectForTask = customJob.subject;
                    if (subjectForTask == null)
                    {
                        Log.Error("Subject is null. Ending job.");
                        pawn.jobs.EndCurrentJob(JobCondition.Errored);
                        return;
                    }

                    Task.Run(async () => {
                        try
                        {
                            if (recipientForTask == null)
                            {
                                Log.Error("Recipient became null before LLM task could run.");
                                llmTaskComplete = true;
                                return;
                            }

                            if (SocialInteractions.Settings == null)
                            {
                                Log.Error("SocialInteractions.Settings is null. Cannot generate LLM response.");
                                llmTaskComplete = true;
                                return;
                            }

                            if (string.IsNullOrEmpty(SocialInteractions.Settings.llmApiUrl))
                            {
                                Log.Error("LLM API URL is not set in mod settings. Cannot generate LLM response.");
                                llmTaskComplete = true;
                                return;
                            }

                            if (interactionDefForTask == null)
                            {
                                Log.Error("InteractionDef is null inside Task.Run. Cannot generate LLM response.");
                                llmTaskComplete = true;
                                return;
                            }

                            string prompt = SocialInteractions.GenerateDeepTalkPrompt(pawn, recipientForTask, interactionDefForTask, subjectForTask);
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                KoboldApiClient client = new KoboldApiClient(SocialInteractions.Settings.llmApiUrl, SocialInteractions.Settings.llmApiKey);
                                List<string> stoppingStrings = new List<string>();
                                if (SocialInteractions.Settings.llmStoppingStrings != null)
                                {
                                    stoppingStrings = new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                                }
                                llmResponse = await client.GenerateText(prompt, SocialInteractions.Settings.llmMaxTokens, SocialInteractions.Settings.llmTemperature, stoppingStrings, SocialInteractions.Settings.enableXtcSampling);
                                if (!string.IsNullOrEmpty(llmResponse))
                                {
                                    messages = llmResponse.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("Error in Task.Run: {0} {1}", ex.Message, ex.StackTrace));
                        }
                        finally
                        {
                            llmTaskComplete = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in getLlmResponseToil: {0} {1}", ex.Message, ex.StackTrace));
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                }
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
                if (messages.Any())
                {
                    conversationId = SpeechBubbleManager.StartConversation();
                }

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
                        SpeechBubbleManager.Enqueue(speaker, wrappedMessage, duration, i == 0, conversationId);
                    }
                }
            };
            displayMessagesToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return displayMessagesToil;

            // Wait for conversation to finish
            Toil waitForConversationToil = new Toil();
            waitForConversationToil.FailOn(() =>
            {
                Pawn recipientPawn = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                return recipientPawn == null || recipientPawn.Downed || recipientPawn.Dead;
            });
            waitForConversationToil.tickAction = () => {
                if (job.def.joyKind != null && pawn.needs != null && pawn.needs.joy != null)
                {
                    pawn.needs.joy.GainJoy(0.00015f, job.def.joyKind);
                }
                if (conversationId == -1 || !SpeechBubbleManager.IsConversationActive(conversationId))
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