using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using System.Linq;

namespace SocialInteractions
{
    [StaticConstructorOnStartup]
    public static class SocialInteractions
    {
        public static SocialInteractionsModSettings Settings;
        public static bool isShowingBubble = false;

        static SocialInteractions()
        {
            var harmony = new Harmony("com.gemini.socialinteractions");
            harmony.PatchAll();
            Settings = LoadedModManager.GetMod<SocialInteractionsMod>().GetSettings<SocialInteractionsModSettings>();
        }

        public static async void HandleDeepTalkInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef)
        {
            if (Settings.preventSpam && isShowingBubble) return;

            if (!Settings.llmInteractionsEnabled)
            {
                return;
            }

            bool isEnabled = false;
            if (interactionDef == InteractionDefOf.Chitchat && Settings.enableChitchat) isEnabled = true;
            else if (interactionDef == InteractionDefOf.DeepTalk && Settings.enableDeepTalk) isEnabled = true;
            else if (interactionDef == InteractionDefOf.Insult && Settings.enableInsult) isEnabled = true;
            else if (interactionDef == InteractionDefOf.RomanceAttempt && Settings.enableRomanceAttempt) isEnabled = true;
            else if (interactionDef == InteractionDefOf.MarriageProposal && Settings.enableMarriageProposal) isEnabled = true;
            else if (interactionDef == InteractionDefOf.Reassure && Settings.enableReassure) isEnabled = true;
            else if (interactionDef == InteractionDefOf.DisturbingChat && Settings.enableDisturbingChat) isEnabled = true;

            if (!isEnabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(Settings.llmApiUrl) || string.IsNullOrEmpty(Settings.llmPromptTemplate))
            {
                return;
            }

            KoboldApiClient client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);

            // Placeholder replacement (initial version, will expand later)
            string prompt = Settings.llmPromptTemplate;
            prompt = prompt.Replace("[topic]", interactionDef.label);

            // Get relationship
            string relation = GetRelationship(initiator, recipient);
            prompt = prompt.Replace("[relation]", relation);

            // Pawn 1 (Initiator) attributes
            string pawn1Age = initiator.ageTracker.AgeBiologicalYears.ToString();
            string pawn1Sex = initiator.gender.ToString();
            string pawn1Traits = "";
            if (initiator.story != null)
            {
                if (initiator.story.traits != null)
                {
                    List<string> traitsList = new List<string>();
                    foreach (Trait trait in initiator.story.traits.allTraits)
                    {
                        traitsList.Add(trait.Label);
                    }
                    pawn1Traits = string.Join(", ", traitsList.ToArray());
                }
            }
            string pawn1Mood = (initiator.needs != null && initiator.needs.mood != null) ? (initiator.needs.mood.CurLevelPercentage * 100).ToString("F0") + "%" : "N/A";
            string pawn1Afflictions = GetAfflictions(initiator);
            string pawn1Likes = GetLikes(initiator);
            string pawn1Action = initiator.GetJobReport().CapitalizeFirst();
            string pawn1Genes = "";
            if (initiator.genes != null)
            {
                pawn1Genes = initiator.genes.XenotypeLabel;
                List<string> geneList = new List<string>();
                foreach (Gene gene in initiator.genes.GenesListForReading)
                {
                    if (!gene.def.skinColorBase.HasValue && !gene.Overridden)
                    {
                        geneList.Add(gene.def.label);
                    }
                }
                if (geneList.Count > 0)
                {
                    pawn1Genes += " (" + string.Join(", ", geneList.ToArray()) + ")";
                }
            }

            // Pawn 2 (Recipient) attributes
            string pawn2Age = recipient.ageTracker.AgeBiologicalYears.ToString();
            string pawn2Sex = recipient.gender.ToString();
            string pawn2Traits = "";
            if (recipient.story != null)
            {
                if (recipient.story.traits != null)
                {
                    List<string> traitsList = new List<string>();
                    foreach (Trait trait in recipient.story.traits.allTraits)
                    {
                        traitsList.Add(trait.Label);
                    }
                    pawn2Traits = string.Join(", ", traitsList.ToArray());
                }
            }
            string pawn2Mood = (recipient.needs != null && recipient.needs.mood != null) ? (recipient.needs.mood.CurLevelPercentage * 100).ToString("F0") + "%" : "N/A";
            string pawn2Afflictions = GetAfflictions(recipient);
            string pawn2Likes = GetLikes(recipient);
            string pawn2Action = recipient.GetJobReport().CapitalizeFirst();
            string pawn2Genes = "";
            if (recipient.genes != null)
            {
                pawn2Genes = recipient.genes.XenotypeLabel;
                List<string> geneList = new List<string>();
                foreach (Gene gene in recipient.genes.GenesListForReading)
                {
                    if (!gene.def.skinColorBase.HasValue && !gene.Overridden)
                    {
                        geneList.Add(gene.def.label);
                    }
                }
                if (geneList.Count > 0)
                {
                    pawn2Genes += " (" + string.Join(", ", geneList.ToArray()) + ")";
                }
            }

            // World info attributes
            long absTicks = Find.TickManager.TicksAbs;
            float longitude = Find.WorldGrid.LongLatOf(initiator.Tile).x;
            string currentDate = GenDate.DateFullStringAt(absTicks, Find.WorldGrid.LongLatOf(initiator.Tile));
            int hour = (int)(GenDate.DayPercent(absTicks, longitude) * 24f);
            string currentTime = hour.ToString("D2") + ":00";
            string currentWeather = Find.CurrentMap.weatherManager.curWeather.label;

            // Replace placeholders
            prompt = prompt.Replace("[pawn1]", initiator.Name.ToStringShort);
            prompt = prompt.Replace("[pawn2]", recipient.Name.ToStringShort);
            prompt = prompt.Replace("[pawn1_age]", pawn1Age);
            prompt = prompt.Replace("[pawn1_sex]", pawn1Sex);
            prompt = prompt.Replace("[pawn1_traits]", pawn1Traits);
            prompt = prompt.Replace("[pawn1_mood]", pawn1Mood);
            prompt = prompt.Replace("[pawn1_afflictions]", pawn1Afflictions);
            prompt = prompt.Replace("[pawn1_likes]", pawn1Likes);
            prompt = prompt.Replace("[pawn1_action]", pawn1Action);
            prompt = prompt.Replace("[pawn1_genes]", pawn1Genes);
            prompt = prompt.Replace("[pawn2_age]", pawn2Age);
            prompt = prompt.Replace("[pawn2_sex]", pawn2Sex);
            prompt = prompt.Replace("[pawn2_traits]", pawn2Traits);
            prompt = prompt.Replace("[pawn2_mood]", pawn2Mood);
            prompt = prompt.Replace("[pawn2_afflictions]", pawn2Afflictions);
            prompt = prompt.Replace("[pawn2_likes]", pawn2Likes);
            prompt = prompt.Replace("[pawn2_action]", pawn2Action);
            prompt = prompt.Replace("[pawn2_genes]", pawn2Genes);
            prompt = prompt.Replace("[date]", currentDate);
            prompt = prompt.Replace("[time]", currentTime);
            prompt = prompt.Replace("[weather]", currentWeather);

            isShowingBubble = true;
            List<string> stoppingStrings = new List<string>(Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));

            string llmResponse = await client.GenerateText(prompt, Settings.llmMaxTokens, Settings.llmTemperature, stoppingStrings);

            if (!string.IsNullOrEmpty(llmResponse))
            {
                // Split response into alternating messages (simple split for now)
                string[] messages = llmResponse.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string message in messages)
                {
                    Pawn speaker = null;
                    string cleanedMessage = message.Trim();

                    if (cleanedMessage.StartsWith(initiator.Name.ToStringShort + ":"))
                    {
                        speaker = initiator;
                    }
                    else if (cleanedMessage.StartsWith(recipient.Name.ToStringShort + ":"))
                    {
                        speaker = recipient;
                    }
                    else
                    {
                        speaker = initiator;
                    }

                    if (speaker != null && !string.IsNullOrEmpty(cleanedMessage))
                    {
                        string wrappedMessage = WrapText(cleanedMessage, Settings.wordsPerLineLimit);
                        MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, wrappedMessage, SocialInteractions.EstimateReadingTime(cleanedMessage) / 1000f);
                        await Task.Delay(EstimateReadingTime(cleanedMessage));
                    }
                }
            }
            isShowingBubble = false;
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

        public static int EstimateReadingTime(string text)
        {
            // Simple estimate: words per second from settings.
            int wordCount = text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (SocialInteractions.Settings.wordsPerSecond <= 0) return wordCount * 300; // Fallback if setting is zero or negative
            return (int)(wordCount / SocialInteractions.Settings.wordsPerSecond * 1000); // Milliseconds
        }

        private static string GetRelationship(Pawn initiator, Pawn recipient)
        {
            // Check for the most important direct relationships first
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient)) return "Spouse";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient)) return "Lover";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient)) return "Fiance";

            // Check for family relationships
            PawnRelationDef relationDef = initiator.GetRelations(recipient).FirstOrDefault();
            if (relationDef != null) return relationDef.label;

            // Check for bond
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Bond, recipient)) return "Bonded";

            // Fallback to opinion-based relationship
            int opinion = recipient.relations.OpinionOf(initiator);
            if (opinion >= 20) return "Friend";
            if (opinion <= -20) return "Rival";

            return "Acquaintance";
        }

        private static string GetAfflictions(Pawn pawn)
        {
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return "None";
            }

            List<Thought> thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetDistinctMoodThoughtGroups(thoughts);

            var negativeThoughts = thoughts.Where(t => t.MoodOffset() < 0).OrderBy(t => t.MoodOffset()).Take(3).Select(t => t.LabelCap);

            if (negativeThoughts.Any())
            {
                return string.Join(", ", negativeThoughts.ToArray());
            }

            return "None";
        }

        private static string GetLikes(Pawn pawn)
        {
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return "None";
            }

            List<Thought> thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetDistinctMoodThoughtGroups(thoughts);

            var positiveThoughts = thoughts.Where(t => t.MoodOffset() > 0).OrderByDescending(t => t.MoodOffset()).Take(3).Select(t => t.LabelCap);

            if (positiveThoughts.Any())
            {
                return string.Join(", ", positiveThoughts.ToArray());
            }

            return "None";
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

                if (initiator != null && recipient != null)
                {
                    if (interactionDef != null)
                    {
                        SocialInteractions.HandleDeepTalkInteraction(initiator, recipient, interactionDef);
                        string text = entry.ToGameStringFromPOV(initiator);
                        if (!string.IsNullOrEmpty(text))
                        {
                            MoteMaker.ThrowText(initiator.DrawPos, initiator.Map, text, SocialInteractions.EstimateReadingTime(text) / 1000f);
                        }
                    }
                }
            }
        }
    }
}