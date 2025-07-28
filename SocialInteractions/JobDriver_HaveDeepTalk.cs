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
        private int messageIndex = 0;
        private int currentMessageDurationTicks = 0;
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
                    string prompt = SocialInteractions.GenerateDeepTalkPrompt(pawn, Recipient, SocialInteractions.currentInteractionDefForJob, job.def.defName);
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
                    llmTaskComplete = true;
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
                messageIndex = 0;
                if (messages.Any())
                {
                    DisplayCurrentMessage();
                } else {
                    this.ReadyForNextToil();
                }
            };
            displayMessagesToil.tickAction = () => {
                currentMessageDurationTicks--;
                if (currentMessageDurationTicks <= 0)
                {
                    messageIndex++;
                    if (messageIndex >= messages.Count)
                    {
                        this.ReadyForNextToil();
                    }
                    else
                    {
                        DisplayCurrentMessage();
                    }
                }
            };
            displayMessagesToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return displayMessagesToil;

            // Wait for conversation to finish
            Toil waitForConversationToil = new Toil();
            waitForConversationToil.tickAction = () => {
                pawn.needs.joy.GainJoy(0.00015f, job.def.joyKind);
                if (conversationComplete)
                {
                    waitForConversationToil.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            waitForConversationToil.defaultCompleteMode = ToilCompleteMode.Never;
            waitForConversationToil.AddFinishAction(() => {
                JobDriver_BeTalkedTo recipientDriver = Recipient.jobs.curDriver as JobDriver_BeTalkedTo;
                if (recipientDriver != null)
                {
                    recipientDriver.EndJob(JobCondition.Succeeded);
                }
            });
            yield return waitForConversationToil;
        }

        

        private void DisplayCurrentMessage()
        {
            if (messageIndex >= messages.Count) {
                this.ReadyForNextToil();
                return;
            }

            string rawMessage = messages[messageIndex].Trim();
            string cleanedMessage = rawMessage;
            Pawn speaker = null;

            // Determine speaker and extract dialogue
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

            if (string.IsNullOrWhiteSpace(cleanedMessage)) {
                currentMessageDurationTicks = 1; // Advance quickly if message is empty
                return;
            }

            if (speaker != null)
            {
                string wrappedMessage = SocialInteractions.WrapText(cleanedMessage, SocialInteractions.Settings.wordsPerLineLimit);
                float duration = SocialInteractions.EstimateReadingTime(cleanedMessage) / 1000f;
                SpeechBubbleManager.Enqueue(speaker, wrappedMessage, duration);
                currentMessageDurationTicks = (int)(duration / 16.66f); // Convert milliseconds to ticks (60 ticks/sec)
                if (currentMessageDurationTicks <= 0) currentMessageDurationTicks = 1; // Ensure at least 1 tick duration
            }
        }
    }
}