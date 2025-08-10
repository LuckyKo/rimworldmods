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
using System.Text.RegularExpressions;

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

        public static bool IsLlmInteractionEnabled(InteractionDef interactionDef)
        {
            Log.Message(string.Format("[SocialInteractions] IsLlmInteractionEnabled called for: {0}", interactionDef.defName));
            if (!Settings.llmInteractionsEnabled) return false;

            
            if (interactionDef == InteractionDefOf.DeepTalk && Settings.enableDeepTalk) return true;
            if (interactionDef == InteractionDefOf.Insult && Settings.enableInsult) return true;
            if (interactionDef == InteractionDefOf.RomanceAttempt && Settings.enableRomanceAttempt) return true;
            if (interactionDef == InteractionDefOf.MarriageProposal && Settings.enableMarriageProposal) return true;
            if (interactionDef == InteractionDefOf.Reassure && Settings.enableReassure) return true;
            if (interactionDef == InteractionDefOf.DisturbingChat && Settings.enableDisturbingChat) return true;
            if (interactionDef.defName == "GoOnDate" && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateRejected && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateAccepted && Settings.enableDating) return true;
            return false;
        }

        public static bool IsLlmJobEnabled(JobDriver jobDriver)
        {
            if (!Settings.llmInteractionsEnabled) return false;

            if (jobDriver is JobDriver_TendPatient && Settings.enableTendPatient) return true;
            if (jobDriver is JobDriver_VisitSickPawn && Settings.enableVisitSickPawn) return true;
            if (jobDriver is JobDriver_Lovin && Settings.enableLovin) return true;

            return false;
        }

        public static string GenerateDeepTalkPrompt(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            if (initiator == null || recipient == null || interactionDef == null || subject == null)
            {
                return null;
            }

            if (!Settings.llmInteractionsEnabled)
            {
                return null;
            }

            bool isEnabled = false;
            
            if (interactionDef == InteractionDefOf.DeepTalk && Settings.enableDeepTalk) isEnabled = true;
            else if (interactionDef == InteractionDefOf.Insult && Settings.enableInsult) isEnabled = true;
            else if (interactionDef == InteractionDefOf.RomanceAttempt && Settings.enableRomanceAttempt) isEnabled = true;
            else if (interactionDef == InteractionDefOf.MarriageProposal && Settings.enableMarriageProposal) isEnabled = true;
            else if (interactionDef == InteractionDefOf.Reassure && Settings.enableReassure) isEnabled = true;
            else if (interactionDef == InteractionDefOf.DisturbingChat && Settings.enableDisturbingChat) isEnabled = true;
            else if (interactionDef == SI_InteractionDefOf.TendPatient && Settings.enableTendPatient) isEnabled = true;
            else if (interactionDef == SI_InteractionDefOf.Lovin && Settings.enableLovin) isEnabled = true;
            else if (interactionDef.defName == "GoOnDate" && Settings.enableDating) isEnabled = true;
            else if (interactionDef == SI_InteractionDefOf.DateRejected && Settings.enableDating) isEnabled = true;
            else if (interactionDef == SI_InteractionDefOf.DateAccepted && Settings.enableDating) isEnabled = true;

            Log.Message(string.Format("[SocialInteractions] GenerateDeepTalkPrompt: isEnabled for {0}: {1}", interactionDef.defName, isEnabled));
            if (!isEnabled)
            {
                return null;
            }

            if (string.IsNullOrEmpty(Settings.llmApiUrl) || string.IsNullOrEmpty(Settings.llmPromptTemplate))
            {
                return null;
            }

            // Placeholder replacement (initial version, will expand later)
            string prompt = Settings.llmPromptTemplate;
            prompt = prompt.Replace("[topic]", interactionDef.label);
            prompt = prompt.Replace("[subject]", subject ?? "");

            // Get relationship
            string relation = GetRelationship(initiator, recipient);
            prompt = prompt.Replace("[relation]", relation);

            // Pawn 1 (Initiator) attributes
            string pawn1Age = initiator.ageTracker.AgeBiologicalYears.ToString();
            string pawn1Sex = initiator.gender.ToString();
            string pawn1Traits = "";
            if (initiator.story != null && initiator.story.traits != null)
            {
                List<string> traitsList = new List<string>();
                foreach (Trait trait in initiator.story.traits.allTraits)
                {
                    traitsList.Add(trait.Label);
                }
                pawn1Traits = string.Join(", ", traitsList.ToArray());
            }

            string pawn1Mood = "N/A";
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                pawn1Mood = (initiator.needs.mood.CurLevelPercentage * 100).ToString("F0") + "%";
            }

            string pawn1Dislikes = GetDislikes(initiator);
            string pawn1Afflictions = GetAfflictions(initiator);
            string pawn1Likes = GetLikes(initiator);
            string pawn1Tech = GetTech(initiator);

            string pawn1Action = "None";
            if (initiator.jobs != null && initiator.jobs.curJob != null)
            {
                try
                {
                    pawn1Action = initiator.GetJobReport().CapitalizeFirst();
                }
                catch (Exception)
                {
                    pawn1Action = "None";
                }
            }

            string pawn1Proficiencies = GetProficiencies(initiator);

            string pawn1Genes = "";
            if (initiator.genes != null)
            {
                pawn1Genes = initiator.genes.XenotypeLabel;
                List<string> geneList = new List<string>();
                foreach (Gene gene in initiator.genes.GenesListForReading)
                {
                    if (gene.def != null && !gene.def.skinColorBase.HasValue && !gene.Overridden)
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
            if (recipient.story != null && recipient.story.traits != null)
            {
                List<string> traitsList = new List<string>();
                foreach (Trait trait in recipient.story.traits.allTraits)
                {
                    traitsList.Add(trait.Label);
                }
                pawn2Traits = string.Join(", ", traitsList.ToArray());
            }

            string pawn2Mood = "N/A";
            if (recipient.needs != null && recipient.needs.mood != null)
            {
                pawn2Mood = (recipient.needs.mood.CurLevelPercentage * 100).ToString("F0") + "%";
            }

            string pawn2Dislikes = GetDislikes(recipient);
            string pawn2Afflictions = GetAfflictions(recipient);
            string pawn2Likes = GetLikes(recipient);
            string pawn2Tech = GetTech(recipient);

            string pawn2Action = "None";
            if (recipient.jobs != null && recipient.jobs.curJob != null)
            {
                try
                {
                    pawn2Action = recipient.GetJobReport().CapitalizeFirst();
                }
                catch (Exception)
                {
                    pawn2Action = "None";
                }
            }

            string pawn2Proficiencies = GetProficiencies(recipient);

            string pawn2Genes = "";
            if (recipient.genes != null)
            {
                pawn2Genes = recipient.genes.XenotypeLabel;
                List<string> geneList = new List<string>();
                foreach (Gene gene in recipient.genes.GenesListForReading)
                {
                    if (gene.def != null && !gene.def.skinColorBase.HasValue && !gene.Overridden)
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
            string currentDate = "Unknown";
            string currentTime = "Unknown";
            string currentWeather = "Unknown";

            if (initiator.Map != null)
            {
                float longitude = Find.WorldGrid.LongLatOf(initiator.Tile).x;
                int day = GenDate.DayOfQuadrum(absTicks, longitude);
                Quadrum quadrum = GenDate.Quadrum(absTicks, longitude);
                int year = GenDate.Year(absTicks, longitude);
                currentDate = string.Format("{0} of {1}, {2}", day, quadrum.Label(), year);
                int hour = (int)(GenDate.DayPercent(absTicks, longitude) * 24f);
                currentTime = hour.ToString("D2") + ":00";
                currentWeather = initiator.Map.weatherManager.curWeather.label;
            }

            // Replace placeholders
            prompt = prompt.Replace("[pawn1]", initiator.Name.ToStringShort);
            prompt = prompt.Replace("[pawn2]", recipient.Name.ToStringShort);
            prompt = prompt.Replace("[pawn1_age]", pawn1Age);
            prompt = prompt.Replace("[pawn1_sex]", pawn1Sex);
            prompt = prompt.Replace("[pawn1_traits]", pawn1Traits);
            prompt = prompt.Replace("[pawn1_mood]", pawn1Mood);
            prompt = prompt.Replace("[pawn1_dislikes]", pawn1Dislikes);
            prompt = prompt.Replace("[pawn1_afflictions]", pawn1Afflictions);
            prompt = prompt.Replace("[pawn1_likes]", pawn1Likes);
            prompt = prompt.Replace("[pawn1_tech]", pawn1Tech);
            prompt = prompt.Replace("[pawn1_action]", pawn1Action);
            prompt = prompt.Replace("[pawn1_proficiencies]", pawn1Proficiencies);
            prompt = prompt.Replace("[pawn1_genes]", pawn1Genes);
            prompt = prompt.Replace("[pawn2_age]", pawn2Age);
            prompt = prompt.Replace("[pawn2_sex]", pawn2Sex);
            prompt = prompt.Replace("[pawn2_traits]", pawn2Traits);
            prompt = prompt.Replace("[pawn2_mood]", pawn2Mood);
            prompt = prompt.Replace("[pawn2_dislikes]", pawn2Dislikes);
            prompt = prompt.Replace("[pawn2_afflictions]", pawn2Afflictions);
            prompt = prompt.Replace("[pawn2_likes]", pawn2Likes);
            prompt = prompt.Replace("[pawn2_tech]", pawn2Tech);
            prompt = prompt.Replace("[pawn2_action]", pawn2Action);
            prompt = prompt.Replace("[pawn2_proficiencies]", pawn2Proficiencies);
            prompt = prompt.Replace("[pawn2_genes]", pawn2Genes);
            prompt = prompt.Replace("[date]", currentDate);
            prompt = prompt.Replace("[time]", currentTime);
            prompt = prompt.Replace("[weather]", currentWeather);

            return prompt;
        }

        public static string WrapText(string text, int wordsPerLine)
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

        public static float EstimateReadingTime(string text)
        {
            // Simple estimate: words per second from settings.
            int wordCount = text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Length;
            float estimatedTime = 0f;
            if (SocialInteractions.Settings.wordsPerSecond <= 0)
            {
                estimatedTime = wordCount * 0.3f; // Fallback if setting is zero or negative
            }
            else
            {
                estimatedTime = wordCount / SocialInteractions.Settings.wordsPerSecond; // Seconds
            }
            return estimatedTime;
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

        private static string GetDislikes(Pawn pawn)
        {
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return "None";
            }

            List<Thought> thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetDistinctMoodThoughtGroups(thoughts);

            var negativeThoughts = new List<Thought>(thoughts).Select(t =>
            {
                try
                {
                    if (t != null && t.MoodOffset() < 0) return t.LabelCap;
                }
                catch (Exception) { }
                return null;
            }).Where(l => l != null).Take(3);

            if (negativeThoughts.Any())
            {
                return string.Join(", ", negativeThoughts.ToArray());
            }

            return "None";
        }

        private static string GetAfflictions(Pawn pawn)
        {
            if (pawn.health == null || pawn.health.hediffSet == null)
            {
                return "None";
            }

            var significantHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && h.def.defName != "OnDate" && h.def.defName != "ImplantedIUD" && !(h is Hediff_MissingPart) && !(h is Hediff_Implant) && (h.def.isBad || h.def.makesSickThought))
                .OrderByDescending(h => h.Severity)
                .Take(3)
                .Select(h => h.LabelCap);

            if (significantHediffs.Any())
            {
                return string.Join(", ", significantHediffs.ToArray());
            }

            return "None";
        }

        private static string GetTech(Pawn pawn)
        {
            if (pawn.health == null || pawn.health.hediffSet == null)
            {
                return "None";
            }

            var techHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && h is Hediff_Implant)
                .OrderByDescending(h => h.Severity)
                .Take(3)
                .Select(h => h.LabelCap);

            if (techHediffs.Any())
            {
                return string.Join(", ", techHediffs.ToArray());
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

            var positiveThoughts = new List<Thought>(thoughts).Select(t =>
            {
                try
                {
                    if (t != null && t.MoodOffset() > 0) return t.LabelCap;
                }
                catch (Exception) { }
                return null;
            }).Where(l => l != null).Take(3);

            if (positiveThoughts.Any())
            {
                return string.Join(", ", positiveThoughts.ToArray());
            }

            return "None";
        }

        private static string GetProficiencies(Pawn pawn)
        {
            if (pawn.skills == null)
            {
                return "None";
            }

            var topSkills = pawn.skills.skills.OrderByDescending(s => s.Level).Take(3);
            List<string> skillLabels = new List<string>();
            foreach (var skill in topSkills)
            {
                skillLabels.Add(skill.def.LabelCap);
            }
            return string.Join(", ", skillLabels);
        }

        public static void HandleInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string defaultText)
        {
            if (IsLlmInteractionEnabled(interactionDef))
            {
                HandleNonStoppingInteraction(initiator, recipient, interactionDef, null);
            }
            else
            {
                if (!string.IsNullOrEmpty(defaultText))
                {
                    SpeechBubbleManager.ShowDefaultBubble(initiator, defaultText);
                }
            }
        }

        public static void HandleNonStoppingInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            Log.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction called for: {0}. preventSpam: {1}, isLlmBusy: {2}", interactionDef.defName, Settings.preventSpam, SpeechBubbleManager.isLlmBusy));
            if (Settings.preventSpam && SpeechBubbleManager.isLlmBusy) return;

            Task.Run(async () => {
                try
                {
                    string prompt = GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        KoboldApiClient client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);
                        List<string> stoppingStrings = new List<string>(Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                        string llmResponse = await client.GenerateText(prompt, Settings.llmMaxTokens, Settings.llmTemperature, stoppingStrings, Settings.enableXtcSampling);
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            string[] messages = llmResponse.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                            if (messages.Any())
                            {
                                for (int i = 0; i < messages.Length; i++)
                                {
                                    string rawMessage = messages[i].Trim();
                                    Pawn speaker = null;

                                    // Determine speaker and extract dialogue
                                    if (rawMessage.StartsWith(initiator.Name.ToStringShort + ":"))
                                    {
                                        speaker = initiator;
                                    }
                                    else if (rawMessage.StartsWith(recipient.Name.ToStringShort + ":"))
                                    {
                                        speaker = recipient;
                                    }
                                    else
                                    {
                                        speaker = initiator; // Default to initiator if speaker not specified
                                    }

                                    if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                                    {
                                        string formattedMessage = FormatLlmText(rawMessage);
                                        string wrappedMessage = WrapText(formattedMessage, Settings.wordsPerLineLimit);
                                        float duration = EstimateReadingTime(rawMessage);
                                        SpeechBubbleManager.Enqueue(speaker, wrappedMessage, duration, i == 0, 0, null);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleNonStoppingInteraction: {0} {1}", ex.Message, ex.StackTrace));
                }
            });
        }

		public static void HandleJobGiverInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            // Always show a default bubble immediately
            SpeechBubbleManager.ShowDefaultBubble(initiator, interactionDef.label);

            if (Settings.preventSpam && SpeechBubbleManager.isLlmBusy) return;

            Task.Run(async () => {
                try
                {
                    string prompt = GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);
                    Log.Message(string.Format("[SocialInteractions] Generated prompt: {0}", prompt != null ? prompt.Substring(0, Math.Min(prompt.Length, 200)) : "NULL"));
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        KoboldApiClient client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);
                        List<string> stoppingStrings = new List<string>(Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                        string llmResponse = await client.GenerateText(prompt, Settings.llmMaxTokens, Settings.llmTemperature, stoppingStrings, Settings.enableXtcSampling);
                        Log.Message(string.Format("[SocialInteractions] LLM Response: {0}", llmResponse != null ? llmResponse.Substring(0, Math.Min(llmResponse.Length, 200)) : "NULL"));
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            string[] messages = llmResponse.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                            if (messages.Any())
                            {
                                string rawMessage = messages[0].Trim();
                                Pawn speaker = null;

                                // Determine speaker and extract dialogue
                                if (rawMessage.StartsWith(initiator.Name.ToStringShort + ":"))
                                {
                                    speaker = initiator;
                                }
                                else if (rawMessage.StartsWith(recipient.Name.ToStringShort + ":"))
                                {
                                    speaker = recipient;
                                }
                                else
                                {
                                    speaker = initiator; // Default to initiator if speaker not specified
                                }

                                if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                                {
                                    string formattedMessage = FormatLlmText(rawMessage);
                                    string wrappedMessage = WrapText(formattedMessage, Settings.wordsPerLineLimit);
                                    float duration = EstimateReadingTime(rawMessage);
                                    SpeechBubbleManager.EnqueueInstant(speaker, wrappedMessage, duration);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleJobGiverInteraction: {0} {1}", ex.Message, ex.StackTrace));
                }
            });
        }

        public static string RemoveRichTextTags(string text)
        {
            return Regex.Replace(text, "<color=#.{8}>|</color>", "");
        }

        public static string FormatLlmText(string text)
        {
            // Use a regular expression to find text enclosed in asterisks, parentheses, or square brackets.
            text = Regex.Replace(text, @"\*(.*?)\*", "<color=#A9F0F0>$1</color>"); // light cyan for emphasis
            text = Regex.Replace(text, @"\((.*?)\)", "<color=#F0E68C>$1</color>"); // khaki for actions/emotes
            text = Regex.Replace(text, @"\\[(.*?)\\]", "<color=#DDA0DD>$1</color>"); // plum for thoughts/internal
            return text;
        }
    }
}