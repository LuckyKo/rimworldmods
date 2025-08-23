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
        public static SocialInteractionsModSettings Settings { get; set; }
        public static bool isShowingBubble = false;
        
        // Static dictionary to store date partners for cheaters
        public static Dictionary<string, Pawn> CheaterPartners = new Dictionary<string, Pawn>();
        
        // Static field to store the conversation ID for the last cheating interaction
        public static int lastCheatingInteractionConversationId = -1;
        
        // --- For LLM Efficiency ---
        private static float lastResponseTimeSeconds = 1.0f; // Initial estimate
        // --- End For LLM Efficiency ---

        static SocialInteractions()
        {
            var harmony = new Harmony("com.gemini.socialinteractions");
            harmony.PatchAll();
            
            // Log that patches were applied
            SLog.Message("[SocialInteractions] Harmony patches applied");
        }

        public static bool IsLlmInteractionEnabled(InteractionDef interactionDef)
        {
            SLog.Message(string.Format("[SocialInteractions] IsLlmInteractionEnabled called for: {0}", interactionDef.defName));
            if (!Settings.llmInteractionsEnabled) return false;

            
            if (interactionDef == InteractionDefOf.Chitchat && Settings.enableChitchat) return true;
            if (interactionDef == InteractionDefOf.DeepTalk && Settings.enableDeepTalk) return true;
            if (interactionDef == InteractionDefOf.Insult && Settings.enableInsult) return true;
            if (interactionDef == InteractionDefOf.RomanceAttempt && Settings.enableRomanceAttempt) return true;
            if (interactionDef == InteractionDefOf.MarriageProposal && Settings.enableMarriageProposal) return true;
            if (interactionDef == InteractionDefOf.Reassure && Settings.enableReassure) return true;
            if (interactionDef == InteractionDefOf.DisturbingChat && Settings.enableDisturbingChat) return true;
            if (interactionDef.defName == "GoOnDate" && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateRejected && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateAccepted && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateLovin && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.CaughtCheating && Settings.enableDating) return true;
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
            if (initiator == null || recipient == null || interactionDef == null)
            {
                return null;
            }
            if (subject == null)
            {
                subject = interactionDef.label;
            }

            if (!Settings.llmInteractionsEnabled)
            {
                return null;
            }

            bool isEnabled = false;
            
            if (interactionDef == InteractionDefOf.Chitchat && Settings.enableChitchat) isEnabled = true;
            else if (interactionDef == InteractionDefOf.DeepTalk && Settings.enableDeepTalk) isEnabled = true;
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
            else if (interactionDef == SI_InteractionDefOf.DateLovin && Settings.enableDating) isEnabled = true;
            else if (interactionDef == SI_InteractionDefOf.CaughtCheating && Settings.enableDating) isEnabled = true;

            SLog.Message(string.Format("[SocialInteractions] GenerateDeepTalkPrompt: isEnabled for {0}: {1}", interactionDef.defName, isEnabled));
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
                float temperature = initiator.Map.mapTemperature.OutdoorTemp;
                currentWeather = string.Format("{0} ({1}°C)", initiator.Map.weatherManager.curWeather.label, temperature.ToString("F0"));
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
            if (initiator.relations == null || recipient == null) return "Acquaintance";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient)) return "Spouse";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient)) return "Lover";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient)) return "Fiance";

            // Check for family relationships - Convert to List to avoid enumeration issues
            // The IEnumerable from GetRelations can throw if the underlying collection is modified during enumeration.
            try
            {
                var relationsList = initiator.GetRelations(recipient).ToList(); // Force enumeration here
                PawnRelationDef relationDef = relationsList.FirstOrDefault();
                if (relationDef != null) 
                {
                    // Use gender-specific label if available
                    string genderedLabel = relationDef.GetGenderSpecificLabel(recipient);
                    if (!string.IsNullOrEmpty(genderedLabel))
                    {
                        return genderedLabel;
                    }
                    return relationDef.label;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(string.Format("[SocialInteractions] GetRelationship: Exception while getting relations list for {0} and {1}: {2}", initiator.LabelShort, recipient.LabelShort, ex.Message));
                // Fall through to other checks
            }

            // Check for bond
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Bond, recipient)) return "Bonded";

            // Fallback to opinion-based relationship
            if (recipient.relations != null)
            {
                int opinion = recipient.relations.OpinionOf(initiator);
                if (opinion >= 20) return "Friend";
                if (opinion <= -20) return "Rival";
            }

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
                catch (Exception) {{ }}
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

            // Check for pregnancy separately since it's not considered a "bad" hediff
            Hediff pregnancyHediff = null;
            if (ModsConfig.BiotechActive)
            {
                pregnancyHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman);
            }
            else
            {
                pregnancyHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            }

            List<string> afflictionsList = new List<string>();
            if (significantHediffs.Any())
            {
                afflictionsList.AddRange(significantHediffs);
            }

            if (pregnancyHediff != null)
            {
                // Add pregnancy information with trimester if available
                string pregnancyInfo = "pregnant";
                Hediff_Pregnant pregnant = pregnancyHediff as Hediff_Pregnant;
                if (pregnant != null)
                {
                    switch (pregnant.CurStageIndex)
                    {
                        case 0:
                            pregnancyInfo = "pregnant (1st trimester)";
                            break;
                        case 1:
                            pregnancyInfo = "pregnant (2nd trimester)";
                            break;
                        case 2:
                            pregnancyInfo = "pregnant (3rd trimester)";
                            break;
                    }
                }
                afflictionsList.Add(pregnancyInfo);
            }

            // Limit to 3 most significant afflictions
            if (afflictionsList.Count > 3)
            {
                afflictionsList = afflictionsList.Take(3).ToList();
            }

            if (afflictionsList.Any())
            {
                return string.Join(", ", afflictionsList.ToArray());
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
                catch (Exception) {{ }}
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
            // If pawns stop on interaction, let the job-based system handle it
            if (Settings.pawnsStopOnInteraction && 
                (interactionDef == InteractionDefOf.Chitchat || 
                 interactionDef == InteractionDefOf.DeepTalk || 
                 interactionDef == InteractionDefOf.Insult || 
                 interactionDef == InteractionDefOf.RomanceAttempt || 
                 interactionDef == InteractionDefOf.MarriageProposal || 
                 interactionDef == InteractionDefOf.Reassure || 
                 interactionDef == InteractionDefOf.DisturbingChat))
            {
                // For these interactions, when pawnsStopOnInteraction is true, 
                // the InteractionWorker_Interacted_Patch will create jobs to keep pawns in place.
                // We don't need to do anything here.
                return;
            }
            
            if (IsLlmInteractionEnabled(interactionDef))
            {
                HandleNonStoppingInteraction(initiator, recipient, interactionDef, defaultText);
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
            HandleNonStoppingInteraction(initiator, recipient, interactionDef, subject, false);
        }

        public static int HandleCaughtCheatingInteraction(Pawn initiator, Pawn recipient, Pawn partner = null)
        {
            // Generate a descriptive subject line for the LLM
            // If partner is provided, use it. Otherwise, look it up.
            string subject;
            if (partner != null)
            {
                subject = string.Format("{0} caught {1} cheating with {2}", 
                    initiator.LabelShort, recipient.LabelShort, partner.LabelShort);
            }
            else
            {
                Pawn foundPartner = DatingManager.GetPartnerOfDateWith(recipient);
                if (foundPartner != null)
                {
                    subject = string.Format("{0} caught {1} cheating with {2}", 
                        initiator.LabelShort, recipient.LabelShort, foundPartner.LabelShort);
                }
                else
                {
                    subject = string.Format("{0} caught {1} cheating", 
                        initiator.LabelShort, recipient.LabelShort);
                }
            }
                
            // Trigger the LLM interaction and return the conversation ID
            int conversationId = HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.CaughtCheating, subject, true, true);
            
            // Store the conversation ID for this cheating interaction
            lastCheatingInteractionConversationId = conversationId;
            
            return conversationId;
        }

        public static int HandleNonStoppingInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject, bool skipSpamProtection = false, bool clearQueueOnResponse = false)
        {
            SLog.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction called for: {0}. preventSpam: {1}, isLlmBusy: {2}, skipSpamProtection: {3}, clearQueueOnResponse: {4}", interactionDef.defName, Settings.preventSpam, SpeechBubbleManager.isLlmBusy, skipSpamProtection, clearQueueOnResponse));
            if (!skipSpamProtection && Settings.preventSpam && SpeechBubbleManager.isLlmBusy) 
            {
                // Show default bubble when LLM is busy and we're preventing spam
                if (!string.IsNullOrEmpty(subject))
                {
                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                }
                return -1; // Return -1 to indicate no conversation was started
            }

            // --- Explicitly set LLM busy flag when starting a new interaction ---
            // This must happen after the spam check and before the async task starts.
            SpeechBubbleManager.isLlmBusy = true;
            SLog.Message(string.Format("[SocialInteractions] Set isLlmBusy = true for interaction {0}", interactionDef.defName));
            // --- End Explicitly set LLM busy flag ---

            string prompt = GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);
            
            // If we can't generate a prompt, show a default bubble and return
            if (string.IsNullOrEmpty(prompt))
            {
                SLog.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction: No prompt generated for interaction {0}, showing default bubble", interactionDef.defName));
                if (!string.IsNullOrEmpty(subject))
                {
                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                }
                return -1; // Return -1 to indicate no conversation was started
            }
            
            SLog.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction: Prompt generated for interaction {0}", interactionDef.defName));

            // Start the conversation immediately to get the ID
            int conversationId = SpeechBubbleManager.StartConversation();
            SLog.Message(string.Format("[SocialInteractions] Started conversation ID: {0} for interaction {1}", conversationId, interactionDef.defName));

            Task.Run(async () => {
                KoboldApiClient client = null;
                // --- For LLM Efficiency Timing ---
                DateTime startTime = DateTime.UtcNow;
                // --- End For LLM Efficiency Timing ---
                try
                {
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);
                        string llmResponse = await client.GenerateText(prompt);
                        
                        // --- For LLM Efficiency Timing ---
                        DateTime endTime = DateTime.UtcNow;
                        TimeSpan responseTime = endTime - startTime;
                        float responseSeconds = (float)responseTime.TotalSeconds;
                        lastResponseTimeSeconds = responseSeconds;
                        // Log on main thread
                        SpeechBubbleManager.EnqueueJob(() => {
                            SLog.Message(string.Format("[SocialInteractions] LLM Response time for interaction {0}: {1:F2}s", interactionDef.defName, responseSeconds));
                        });
                        // --- End For LLM Efficiency Timing ---
                        
                        if (llmResponse == null)
                        {
                            Log.Warning(string.Format("[SocialInteractions] HandleNonStoppingInteraction: LLM API returned null response for interaction {0}", interactionDef.defName));
                            // Fallback to default interaction text
                            string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                            SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId));
                            return;
                        }
                        
                        // --- Clear queue for high-priority response ---
                        // If this interaction requested to clear the queue upon receiving a response,
                        // and the response is valid, do so before processing the messages.
                        // This ensures the interruption happens precisely when the high-impact content is ready.
                        if (clearQueueOnResponse)
                        {
                            SpeechBubbleManager.EnqueueJob(() => {
                                SLog.Message(string.Format("[SocialInteractions] Clearing speech queue for high-priority response from interaction: {0}", (interactionDef != null) ? interactionDef.defName : "Unknown"));
                                SpeechBubbleManager.ClearQueues();
                            });
                        }
                        // --- End Clear queue for high-priority response ---
                        
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Split the response using multiple possible line break characters
                            string[] messages = llmResponse.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                            if (messages.Any())
                            {
                                // --- For LLM Efficiency Timing ---
                                float totalDisplaySeconds = 0f;
                                // --- End For LLM Efficiency Timing ---
                                
                                for (int i = 0; i < messages.Length; i++)
                                {
                                    string rawMessage = messages[i].Trim();
                                    Pawn speaker = null;

                                    // Determine speaker and extract dialogue
                                    // More robust speaker detection
                                    if (rawMessage.StartsWith(initiator.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                    {
                                        speaker = initiator;
                                        rawMessage = rawMessage.Substring(initiator.Name.ToStringShort.Length + 1).Trim();
                                    }
                                    else if (rawMessage.StartsWith(recipient.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                    {
                                        speaker = recipient;
                                        rawMessage = rawMessage.Substring(recipient.Name.ToStringShort.Length + 1).Trim();
                                    }
                                    else
                                    {
                                        speaker = initiator; // Default to initiator if speaker not specified
                                    }

                                    if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                                    {
                                        float duration = EstimateReadingTime(rawMessage);
                                        // --- For LLM Efficiency Timing ---
                                        totalDisplaySeconds += duration;
                                        // --- End For LLM Efficiency Timing ---
                                        // --- Pass conversationId ---
                                        // Capture the loop variable to avoid closure issues
                                        int currentIndex = i;
                                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(speaker, rawMessage, recipient, currentIndex == 0, conversationId, true)); // Orange for high priority
                                        // --- End Pass conversationId ---
                                    }
                                }
                                
                                // --- For LLM Efficiency Unlock ---
                                // Calculate unlock delay based on last response time estimate and current display time
                                float unlockDelaySeconds = totalDisplaySeconds - lastResponseTimeSeconds;
                                // Log on main thread
                                SpeechBubbleManager.EnqueueJob(() => {
                                    SLog.Message(string.Format("[SocialInteractions] Efficiency - Total Display Time: {0:F2}s, Estimated Next Response Time: {1:F2}s, Unlock Delay: {2:F2}s", totalDisplaySeconds, lastResponseTimeSeconds, unlockDelaySeconds));
                                });
                                
                                if (unlockDelaySeconds > 0)
                                {
                                    // Schedule unlock after the delay
                                    SpeechBubbleManager.ScheduleUnlock(unlockDelaySeconds);
                                }
                                else
                                {
                                    // Unlock immediately (next tick) if LLM was slower than display or no delay needed
                                    SpeechBubbleManager.ScheduleUnlock(0.01f); // A tiny delay to ensure it happens on the next possible tick
                                }
                                // --- End For LLM Efficiency Unlock ---
                            }
                            else
                            {
                                Log.Warning(string.Format("[SocialInteractions] HandleNonStoppingInteraction: LLM API returned empty messages for interaction {0}", interactionDef.defName));
                                // Fallback to default interaction text
                                string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                                SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId));
                                
                                // --- For LLM Efficiency Unlock (Fallback) ---
                                // Even on fallback, schedule an unlock based on a default display time
                                float unlockDelaySeconds = 2.0f - lastResponseTimeSeconds; // Assume 2s default display for fallback
                                if (unlockDelaySeconds > 0)
                                    SpeechBubbleManager.ScheduleUnlock(unlockDelaySeconds);
                                else
                                    SpeechBubbleManager.ScheduleUnlock(0.01f);
                                // --- End For LLM Efficiency Unlock (Fallback) ---
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(string.Format("[SocialInteractions] HandleNonStoppingInteraction: Failed to generate prompt for interaction {0}", interactionDef.defName));
                        // Fallback to default interaction text
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId));
                        
                        // --- For LLM Efficiency Unlock (Prompt Fail) ---
                        float unlockDelaySeconds = 2.0f - lastResponseTimeSeconds; // Assume 2s default display for fallback
                        if (unlockDelaySeconds > 0)
                            SpeechBubbleManager.ScheduleUnlock(unlockDelaySeconds);
                        else
                            SpeechBubbleManager.ScheduleUnlock(0.01f);
                        // --- End For LLM Efficiency Unlock (Prompt Fail) ---
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleNonStoppingInteraction: {0} {1}", ex.Message, ex.StackTrace));
                    // Fallback to default interaction text
                    try
                    {
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId));
                        
                        // --- For LLM Efficiency Unlock (Exception) ---
                        float unlockDelaySeconds = 2.0f - lastResponseTimeSeconds; // Assume 2s default display for fallback
                        if (unlockDelaySeconds > 0)
                            SpeechBubbleManager.ScheduleUnlock(unlockDelaySeconds);
                        else
                            SpeechBubbleManager.ScheduleUnlock(0.01f);
                        // --- End For LLM Efficiency Unlock (Exception) ---
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(string.Format("Error in HandleNonStoppingInteraction fallback: {0} {1}", fallbackEx.Message, fallbackEx.StackTrace));
                    }
                }
                finally
                {
                    // --- End Conversation ---
                    if (conversationId != -1)
                    {
                        string interactionDefName = (interactionDef != null) ? interactionDef.defName : "Unknown";
                        SLog.Message(string.Format("[SocialInteractions] Ending conversation ID: {0} for interaction {1}", conversationId, interactionDefName));
                        SpeechBubbleManager.EndConversation(conversationId);
                        
                        // Note: isLlmBusy will be handled by the scheduled unlock or immediately if delay <= 0
                    }
                    // --- End End Conversation ---
                    if (client != null)
                    {
                        client.Dispose();
                    }
                }
            });
            
            // Return the conversation ID
            return conversationId;
        }

        public static void HandleJobGiverInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            // Always show a default bubble immediately
            SpeechBubbleManager.ShowDefaultBubble(initiator, interactionDef.label);

            if (Settings.preventSpam && SpeechBubbleManager.isLlmBusy) return;

            Task.Run(async () => {
                KoboldApiClient client = null;
                try
                {
                    string prompt = GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);
                    SLog.Message(string.Format("[SocialInteractions] Generated prompt: {0}", prompt != null ? prompt.Substring(0, Math.Min(prompt.Length, 200)) : "NULL"));
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);
                        string llmResponse = await client.GenerateText(prompt);
                        SLog.Message(string.Format("[SocialInteractions] LLM Response: {0}", llmResponse != null ? llmResponse.Substring(0, Math.Min(llmResponse.Length, 200)) : "NULL"));
                        
                        if (llmResponse == null)
                        {
                            Log.Warning(string.Format("[SocialInteractions] HandleJobGiverInteraction: LLM API returned null response for interaction {0}", interactionDef.defName));
                            // Fallback to default interaction text
                            string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                            SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f);
                            return;
                        }
                        
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Split the response using multiple possible line break characters
                            string[] messages = llmResponse.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                            if (messages.Any())
                            {
                                string rawMessage = messages[0].Trim();
                                Pawn speaker = null;

                                // Determine speaker and extract dialogue
                                // More robust speaker detection
                                if (rawMessage.StartsWith(initiator.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                {
                                    speaker = initiator;
                                    rawMessage = rawMessage.Substring(initiator.Name.ToStringShort.Length + 1).Trim();
                                }
                                else if (rawMessage.StartsWith(recipient.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                {
                                    speaker = recipient;
                                    rawMessage = rawMessage.Substring(recipient.Name.ToStringShort.Length + 1).Trim();
                                }
                                else
                                {
                                    speaker = initiator; // Default to initiator if speaker not specified
                                }

                                if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                                {
                                    SpeechBubbleManager.EnqueueInstant(speaker, messages[0].Trim(), recipient, true); // Light sky blue for job giver interactions
                                }
                            }
                            else
                            {
                                Log.Warning(string.Format("[SocialInteractions] HandleJobGiverInteraction: LLM API returned empty messages for interaction {0}", interactionDef.defName));
                                // Fallback to default interaction text
                                string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                                SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(string.Format("[SocialInteractions] HandleJobGiverInteraction: Failed to generate prompt for interaction {0}", interactionDef.defName));
                        // Fallback to default interaction text
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleJobGiverInteraction: {0} {1}", ex.Message, ex.StackTrace));
                    // Fallback to default interaction text
                    try
                    {
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f);
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(string.Format("Error in HandleJobGiverInteraction fallback: {0} {1}", fallbackEx.Message, fallbackEx.StackTrace));
                    }
                }
                finally
                {
                    if (client != null)
                    {
                        client.Dispose();
                    }
                }
            });
        }

        public static string RemoveRichTextTags(string text)
        {
            return Regex.Replace(text, "<color=#.{8}>|</color>", "");
        }
    }

    
}
