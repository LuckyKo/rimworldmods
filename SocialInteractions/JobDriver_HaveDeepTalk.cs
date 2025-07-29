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
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Recipient.Spawned || !Recipient.Awake());

            // Go to the recipient
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Face each other
            Toil faceToil = new Toil();
            faceToil.initAction = () => {
                pawn.rotationTracker.FaceCell(Recipient.Position);
                Recipient.rotationTracker.FaceCell(pawn.Position);
            };
            faceToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return faceToil;

            // Get LLM response
            Toil getLlmResponseToil = new Toil();
            getLlmResponseToil.initAction = () => {
                llmTaskComplete = false;
                SpeechBubbleManager.isConversationActive = true;
                SpeechBubbleManager.onConversationFinished += () => conversationComplete = true;
                Task.Run(async () => {
                    if (SocialInteractions.Settings == null)
                    {
                        Log.Error("SocialInteractions.Settings is null. Cannot generate LLM response.");
                        llmTaskComplete = true;
                        SpeechBubbleManager.isConversationActive = false;
                        return; // Exit the async task early
                    }
                    try
                    {
                        InteractionDef interactionDef = SocialInteractions.currentInteractionDefForJob ?? InteractionDefOf.Chitchat; // Default to Chitchat if null
                        string jobDefName;
                        if (job.def != null && job.def.defName != null)
                        {
                            jobDefName = job.def.defName;
                        }
                        else
                        {
                            jobDefName = "UnknownJob";
                        }
                        string prompt = SocialInteractions.GenerateDeepTalkPrompt(pawn, Recipient, interactionDef, jobDefName);
                        if (!string.IsNullOrEmpty(prompt))
                        {
                            KoboldApiClient client = new KoboldApiClient(SocialInteractions.Settings.llmApiUrl, SocialInteractions.Settings.llmApiKey);
                            List<string> stoppingStrings = new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                            llmResponse = await client.GenerateText(prompt, SocialInteractions.Settings.llmMaxTokens, SocialInteractions.Settings.llmTemperature, stoppingStrings);
                            if (!string.IsNullOrEmpty(llmResponse))
                            {
                                messages = llmResponse.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("Error generating LLM response: {0}", ex.Message));
                        SpeechBubbleManager.isConversationActive = false; // Ensure conversation is not active if LLM task fails
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
                for (int i = 0; i < messages.Count; i++)
                {
                    string rawMessage = messages[i].Trim();
                    Pawn speaker = null;

                    if (rawMessage.StartsWith(pawn.Name.ToStringShort + ":"))
                    {
                        speaker = pawn;
                    }
                    else if (rawMessage.StartsWith(Recipient.Name.ToStringShort + ":"))
                    {
                        speaker = Recipient;
                    }
                    else
                    {
                        speaker = pawn; // Default to initiator if speaker not specified
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
                if (Recipient != null && Recipient.jobs != null)
                {
                    JobDriver_BeTalkedTo recipientDriver = Recipient.jobs.curDriver as JobDriver_BeTalkedTo;
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