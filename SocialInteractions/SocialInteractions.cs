using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text;

namespace SocialInteractions
{
    [StaticConstructorOnStartup]
    public static class SocialInteractions
    {
        public static SocialInteractionsModSettings Settings;

        static SocialInteractions()
        {
            var harmony = new Harmony("com.gemini.socialinteractions");
            harmony.PatchAll();
            Settings = LoadedModManager.GetMod<SocialInteractionsMod>().GetSettings<SocialInteractionsModSettings>();
        }

        public static async void HandleDeepTalkInteraction(Pawn initiator, Pawn recipient)
        {
            Log.Message("HandleDeepTalkInteraction called.");

            if (!Settings.llmInteractionsEnabled)
            {
                Log.Message("LLM Interactions are disabled in settings.");
                return;
            }

            if (string.IsNullOrEmpty(Settings.llmApiUrl) || string.IsNullOrEmpty(Settings.llmPromptTemplate))
            {
                Log.Warning("SocialInteractions: LLM API URL or Prompt Template not configured.");
                return;
            }

            Log.Message("LLM API URL: " + Settings.llmApiUrl);
            Log.Message("LLM Prompt Template: " + Settings.llmPromptTemplate);

            KoboldApiClient client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);

            // Placeholder replacement (initial version, will expand later)
            string prompt = Settings.llmPromptTemplate;

            // Pawn 1 (Initiator) attributes
            string pawn1Age = initiator.ageTracker.AgeBiologicalYears.ToString();
            string pawn1Sex = initiator.gender.ToString();
            string pawn1Traits = "";
            if (initiator.story != null)
            {
                Log.Message("Initiator story is not null.");
                if (initiator.story.traits != null)
                {
                    Log.Message("Initiator traits is not null.");
                    List<string> traitsList = new List<string>();
                    foreach (Trait trait in initiator.story.traits.allTraits)
                    {
                        traitsList.Add(trait.Label);
                        Log.Message("Initiator trait found: " + trait.Label);
                    }
                    pawn1Traits = string.Join(", ", traitsList.ToArray());
                    Log.Message("Final pawn1Traits: " + pawn1Traits);
                }
                else
                {
                    Log.Message("Initiator traits is null.");
                }
            }
            else
            {
                Log.Message("Initiator story is null.");
            }
            string pawn1Mood = (initiator.needs != null && initiator.needs.mood != null) ? (initiator.needs.mood.CurLevelPercentage * 100).ToString("F0") + "%" : "N/A";

            // Pawn 2 (Recipient) attributes
            string pawn2Age = recipient.ageTracker.AgeBiologicalYears.ToString();
            string pawn2Sex = recipient.gender.ToString();
            string pawn2Traits = "";
            if (recipient.story != null)
            {
                Log.Message("Recipient story is not null.");
                if (recipient.story.traits != null)
                {
                    Log.Message("Recipient traits is not null.");
                    List<string> traitsList = new List<string>();
                    foreach (Trait trait in recipient.story.traits.allTraits)
                    {
                        traitsList.Add(trait.Label);
                        Log.Message("Recipient trait found: " + trait.Label);
                    }
                    pawn2Traits = string.Join(", ", traitsList.ToArray());
                    Log.Message("Final pawn2Traits: " + pawn2Traits);
                }
                else
                {
                    Log.Message("Recipient traits is null.");
                }
            }
            else
            {
                Log.Message("Recipient story is null.");
            }
            string pawn2Mood = (recipient.needs != null && recipient.needs.mood != null) ? (recipient.needs.mood.CurLevelPercentage * 100).ToString("F0") + "%" : "N/A";

            // World info attributes
            string currentDate = GenDate.DateReadoutStringAt(Find.TickManager.TicksGame, Find.WorldGrid.LongLatOf(initiator.Tile));
            string currentTime = GenDate.HourOfDay(Find.TickManager.TicksGame, Find.WorldGrid.LongLatOf(initiator.Tile).x).ToString("D2") + ":00";
            string currentWeather = Find.CurrentMap.weatherManager.curWeather.label;

            // Replace placeholders
            prompt = prompt.Replace("[pawn1]", initiator.Name.ToStringShort);
            prompt = prompt.Replace("[pawn2]", recipient.Name.ToStringShort);
            prompt = prompt.Replace("[pawn1_age]", pawn1Age);
            prompt = prompt.Replace("[pawn1_sex]", pawn1Sex);
            prompt = prompt.Replace("[pawn1_traits]", pawn1Traits);
            prompt = prompt.Replace("[pawn1_mood]", pawn1Mood);
            prompt = prompt.Replace("[pawn2_age]", pawn2Age);
            prompt = prompt.Replace("[pawn2_sex]", pawn2Sex);
            prompt = prompt.Replace("[pawn2_traits]", pawn2Traits);
            prompt = prompt.Replace("[pawn2_mood]", pawn2Mood);
            prompt = prompt.Replace("[date]", currentDate);
            prompt = prompt.Replace("[time]", currentTime);
            prompt = prompt.Replace("[weather]", currentWeather);

            Log.Message("Sending prompt to LLM: " + prompt);
            string llmResponse = await client.GenerateText(prompt, Settings.llmMaxTokens, Settings.llmTemperature);
            Log.Message("Received response from LLM (or null if failed).");

            if (!string.IsNullOrEmpty(llmResponse))
            {
                Log.Message("LLM Response: " + llmResponse);
                // Split response into alternating messages (simple split for now)
                string[] messages = llmResponse.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string message in messages)
                {
                    Log.Message("Processing message: '" + message + "'");
                    Pawn speaker = null;
                    string cleanedMessage = message.Trim();
                    Log.Message("Cleaned message: '" + cleanedMessage + "'");

                    string initiatorPrefix = initiator.Name.ToStringShort + ":";
                    string recipientPrefix = recipient.Name.ToStringShort + ":";

                    Log.Message("Checking for initiator prefix: '" + initiatorPrefix + "' (Result: " + cleanedMessage.StartsWith(initiatorPrefix) + ")");
                    Log.Message("Checking for recipient prefix: '" + recipientPrefix + "' (Result: " + cleanedMessage.StartsWith(recipientPrefix) + ")");

                    if (cleanedMessage.StartsWith(initiatorPrefix))
                    {
                        speaker = initiator;
                        cleanedMessage = cleanedMessage.Substring(initiatorPrefix.Length).Trim();
                        Log.Message("Identified speaker as initiator. Cleaned message after prefix removal: '" + cleanedMessage + "'");
                    }
                    else if (cleanedMessage.StartsWith(recipientPrefix))
                    {
                        speaker = recipient;
                        cleanedMessage = cleanedMessage.Substring(recipientPrefix.Length).Trim();
                        Log.Message("Identified speaker as recipient. Cleaned message after prefix removal: '" + cleanedMessage + "'");
                    }
                    else
                    {
                        // If no specific speaker is identified, default to initiator or handle as a general narration
                        speaker = initiator;
                        Log.Message("No specific speaker identified. Defaulting to initiator.");
                    }

                    if (speaker != null && !string.IsNullOrEmpty(cleanedMessage))
                    {
                        string wrappedMessage = WrapText(cleanedMessage, Settings.wordsPerLineLimit);
                        MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, wrappedMessage, SocialInteractions.Settings.speechBubbleDurationSeconds);
                        await Task.Delay(EstimateReadingTime(cleanedMessage));
                    }
                    else
                    {
                        Log.Warning("Skipping empty or speaker-less message: '" + cleanedMessage + "'");
                    }
                }
            }
            else
            {
                Log.Warning("SocialInteractions: LLM returned empty response.");
            }
        }

        private static string WrapText(string text, int wordsPerLine)
        {
            if (wordsPerLine <= 0) return text; // No wrapping if limit is zero or negative

            string[] words = text.Split(' ');
            System.Text.StringBuilder wrappedText = new System.Text.StringBuilder();
            int wordCount = 0;

            for (int i = 0; i < words.Length; i++)
            {
                wrappedText.Append(words[i]);
                wordCount++;

                if (wordCount >= wordsPerLine && i < words.Length - 1)
                {
                    wrappedText.Append("\n");
                    wordCount = 0;
                }
                else if (i < words.Length - 1)
                {
                    wrappedText.Append(" ");
                }
            }
            return wrappedText.ToString();
        }

        private static int EstimateReadingTime(string text)
        {
            // Simple estimate: words per second from settings.
            int wordCount = text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (SocialInteractions.Settings.wordsPerSecond <= 0) return wordCount * 300; // Fallback if setting is zero or negative
            return (int)(wordCount / SocialInteractions.Settings.wordsPerSecond * 1000); // Milliseconds
        }
    }


    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class Pawn_InteractionsTracker_TryInteractWith_Patch
    {
        public static void Postfix(bool __result, Pawn_InteractionsTracker __instance, Pawn recipient)
        {
            Pawn initiator = (Pawn)AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn").GetValue(__instance);
            if (__result && initiator != null && recipient != null && SocialInteractions.Settings.pawnsStopOnInteractionEnabled)
            {
                // Pawn stopping logic
                int waitTicks = 120; // 2 seconds

                Job initiatorJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, waitTicks);
                initiator.jobs.StartJob(initiatorJob, JobCondition.InterruptForced);

                Job recipientJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, waitTicks);
                recipient.jobs.StartJob(recipientJob, JobCondition.InterruptForced);
            }
        }
    }

    [HarmonyPatch(typeof(PlayLog), "Add")]
    public static class PlayLog_Add_Patch
    {
        public static void Postfix(LogEntry entry)
        {
            if (entry.GetType().Name == "PlayLogEntry_Interaction")
            {
                var intDefField = entry.GetType().GetField("intDef", BindingFlags.NonPublic | BindingFlags.Instance);
                var interactionDef = intDefField.GetValue(entry) as InteractionDef;

                var initiatorField = entry.GetType().GetField("initiator", BindingFlags.NonPublic | BindingFlags.Instance);
                Pawn initiator = initiatorField.GetValue(entry) as Pawn;

                var recipientField = entry.GetType().GetField("recipient", BindingFlags.NonPublic | BindingFlags.Instance);
                Pawn recipient = recipientField.GetValue(entry) as Pawn;

                Log.Message("PlayLog_Add_Patch.Postfix called. Entry type: " + entry.GetType().Name);

                if (initiator != null && recipient != null)
                {
                    Log.Message("Initiator: " + initiator.Name.ToStringShort + ", Recipient: " + recipient.Name.ToStringShort);
                    if (interactionDef != null)
                    {
                        Log.Message("InteractionDef: " + interactionDef.defName);
                        Log.Message("Interaction detected. Calling HandleDeepTalkInteraction for any interaction type.");
                        SocialInteractions.HandleDeepTalkInteraction(initiator, recipient);
                        string text = entry.ToGameStringFromPOV(initiator);
                        if (!string.IsNullOrEmpty(text))
                        {
                            MoteMaker.ThrowText(initiator.DrawPos, initiator.Map, text, SocialInteractions.Settings.speechBubbleDurationSeconds);
                        }
                    }
                    else
                    {
                        Log.Warning("InteractionDef is null for PlayLogEntry_Interaction.");
                    }
                }
                else
                {
                    
                }
            }
        }
    }
}