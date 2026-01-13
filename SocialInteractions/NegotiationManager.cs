using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;

namespace SocialInteractions
{
    /// <summary>
    /// Manages the negotiation flow between two pawns with LLM integration.
    /// Handles prompt generation, LLM requests, response parsing, and outcome determination.
    /// </summary>
    public class NegotiationManager
    {
        private Pawn initiator;
        private Pawn target;
        private Dialog_PawnNegotiation dialog;
        
        private StringBuilder conversationHistory = new StringBuilder();
        private List<string> currentChoices = new List<string>(); // Store current choices
        private string lastSelectedChoice = null;
        private int turnCount = 0;
        private const int MaxTurns = 10; // Safety limit
        
        private bool isActive = false;
        private NegotiationOutcome? pendingOutcome = null;
        
        // Store last dialogue lines for final display
        private List<DialogueLine> lastDialogueLines = new List<DialogueLine>();
        private int conversationId = -1;
        
        public NegotiationManager(Pawn initiator, Pawn target, Dialog_PawnNegotiation dialog)
        {
            this.initiator = initiator;
            this.target = target;
            this.dialog = dialog;
        }
        
        public void StartNegotiation()
        {
            isActive = true;
            pendingOutcome = null;
            turnCount = 0;
            conversationHistory.Clear();
            
            SLog.Message("[Negotiation] Starting negotiation between " + initiator.LabelShort + " and " + target.LabelShort);
            
            // Apply negotiating hediff
            ApplyNegotiatingHediff();
            
            // Send initial LLM request
            SendLLMRequest(null);
        }
        
        public void OnChoiceSelected(int choiceIndex)
        {
            if (!isActive) return;
            
            SLog.Message("[Negotiation] OnChoiceSelected called with index: " + choiceIndex + ", currentChoices.Count: " + currentChoices.Count);
            
            if (choiceIndex >= 0 && choiceIndex < currentChoices.Count)
            {
                lastSelectedChoice = currentChoices[choiceIndex];
                SLog.Message("[Negotiation] Choice selected: " + lastSelectedChoice);
                SendLLMRequest(lastSelectedChoice);
            }
            else
            {
                SLog.Warning("[Negotiation] Invalid choice index: " + choiceIndex);
            }
        }
        
        public void OnCustomInput(string customText)
        {
            if (!isActive) return;
            
            lastSelectedChoice = customText;
            SLog.Message("[Negotiation] Custom input: " + customText);
            SendLLMRequest(customText);
        }
        
        public void EndNegotiationEarly()
        {
            SLog.Message("[Negotiation] Ended early by user");
            EndNegotiation();
        }
        
        private void SendLLMRequest(string selectedChoice)
        {
            dialog.SetWaiting(true);
            turnCount++;
            
            if (turnCount > MaxTurns)
            {
                SLog.Warning("[Negotiation] Max turns reached, forcing conclusion");
                EndNegotiation();
                return;
            }
            
            // Build prompt
            string prompt = BuildPrompt(selectedChoice);
            SLog.Message("[Negotiation] Sending prompt (turn " + turnCount + "):\n" + prompt.Substring(0, Math.Min(500, prompt.Length)) + "...");
            
            // Send async LLM request
            Task.Run(async () =>
            {
                try
                {
                    string response = await GetLLMResponse(prompt);
                    
                    // Process on main thread
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        if (isActive)
                        {
                            ProcessLLMResponse(response);
                        }
                    });
                }
                catch (Exception ex)
                {
                    SLog.Error("[Negotiation] LLM error: " + ex.Message);
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        if (isActive)
                        {
                            HandleLLMFailure();
                        }
                    });
                }
            });
        }
        
        private string BuildPrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            // System context
            sb.AppendLine("You are writing a negotiation dialogue between two colonists in a colony survival game.");
            sb.AppendLine();
            
            // Pawn 1 context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Initiator - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Pawn 2 context
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Target - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            sb.AppendLine();
            
            // Relationship
            sb.AppendLine("[Relationship]");
            sb.AppendLine(SocialInteractions.GetRelationship(initiator, target));
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " chooses: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            // Instructions
            sb.AppendLine("Continue the dialogue. Write what " + initiator.LabelShort + " says (based on the choice if given), then " + target.LabelShort + "'s response.");
            sb.AppendLine("If the conversation has reached a natural conclusion (agreement, disagreement, or impasse), provide only the outcome:");
            sb.AppendLine("Else, provide exactly 3 new action choices for " + initiator.LabelShort + ".");
            sb.AppendLine();
            sb.AppendLine("FORMAT:");
            sb.AppendLine(initiator.LabelShort + ": dialogue");
            sb.AppendLine(target.LabelShort + ": response");
            sb.AppendLine();
            sb.AppendLine("OUTCOME: POSITIVE | NEUTRAL | NEGATIVE | NONE");
            sb.AppendLine();
            sb.AppendLine("CHOICES:");
            sb.AppendLine("1. action/statement");
            sb.AppendLine("2. action/statement");
            sb.AppendLine("3. action/statement");
            sb.AppendLine("END_CHOICES");
            
            return sb.ToString();
        }
        
        private void AppendPawnContext(StringBuilder sb, Dictionary<string, string> data, string prefix, Pawn pawn)
        {
            string name = data.ContainsKey(prefix) ? data[prefix] : "Unknown";
            string sex = data.ContainsKey(prefix + "_sex") ? data[prefix + "_sex"] : "unknown";
            string age = data.ContainsKey(prefix + "_age") ? data[prefix + "_age"] : "unknown";
            string title = data.ContainsKey(prefix + "_title") ? data[prefix + "_title"] : "unknown";
            string faction = data.ContainsKey(prefix + "_faction") ? data[prefix + "_faction"] : "unknown";
            string ideology = data.ContainsKey(prefix + "_ideology") ? data[prefix + "_ideology"] : "unknown";
            string traits = data.ContainsKey(prefix + "_traits") ? data[prefix + "_traits"] : "unknown";
            string genes = data.ContainsKey(prefix + "_genes") ? data[prefix + "_genes"] : "unknown";
            string proficiencies = data.ContainsKey(prefix + "_proficiencies") ? data[prefix + "_proficiencies"] : "unknown";
            string noskills = data.ContainsKey(prefix + "_noskills") ? data[prefix + "_noskills"] : "unknown";
            string mood = data.ContainsKey(prefix + "_mood") ? data[prefix + "_mood"] : "unknown";
            string likes = data.ContainsKey(prefix + "_likes") ? data[prefix + "_likes"] : "unknown";
            string dislikes = data.ContainsKey(prefix + "_dislikes") ? data[prefix + "_dislikes"] : "unknown";
            string afflictions = data.ContainsKey(prefix + "_afflictions") ? data[prefix + "_afflictions"] : "unknown";
            string family = data.ContainsKey(prefix + "_family") ? data[prefix + "_family"] : "unknown";
            string bio = data.ContainsKey(prefix + "_bio") ? data[prefix + "_bio"] : "";
            string action = data.ContainsKey(prefix + "_action") ? data[prefix + "_action"] : "unknown";

            string description = string.Format(
                "{0} is a {1}, age {2}, a {3} of the {4} faction, following the {5} ideology, has the following traits: {6}; Xenotype: {7}; {0} is proficient in: {8}; {0} is incapable of: {9}; {0}'s mood is {10}, positives: {11} / negatives: {12}; Medical status: {13}. {0}'s family: {14}. {15}",
                name, sex, age, title, faction, ideology, traits, genes, proficiencies, noskills, mood, likes, dislikes, afflictions, family, bio);
            
            sb.AppendLine(description);

            // Add Social Skill context
            if (pawn.skills != null)
            {
                var socialSkill = pawn.skills.GetSkill(SkillDefOf.Social);
                if (socialSkill != null)
                {
                    sb.AppendLine(string.Format("IMPORTANT: {0}'s Social skill level is {1} (0-20 scale). Use this to determine their negotiation capability and eloquence.", name, socialSkill.Level));
                }
            }

            sb.AppendLine(string.Format("{0} is currently {1}", name, action));
        }
        

        
        private void AppendWorldContext(StringBuilder sb)
        {
            if (initiator.Map == null) return;
            
            sb.AppendLine("[World Context]");
            
            long absTicks = Find.TickManager.TicksAbs;
            float longitude = Find.WorldGrid.LongLatOf(initiator.Tile).x;
            int day = GenDate.DayOfQuadrum(absTicks, longitude);
            Quadrum quadrum = GenDate.Quadrum(absTicks, longitude);
            int year = GenDate.Year(absTicks, longitude);
            int hour = (int)(GenDate.DayPercent(absTicks, longitude) * 24f);
            
            sb.AppendLine("- Date: " + day + " of " + quadrum.Label() + ", " + year);
            sb.AppendLine("- Time: " + hour.ToString("D2") + ":00");
            sb.AppendLine("- Weather: " + initiator.Map.weatherManager.curWeather.label);
            sb.AppendLine("- Location: " + SocialInteractions.GetBiomeInfo(initiator.Map));
        }
        
        private async Task<string> GetLLMResponse(string prompt)
        {
            // Use existing API client infrastructure
            var settings = SocialInteractions.Settings;
            string apiKey = settings.llmApiKey;
            
            switch (settings.llmApiType)
            {
                case LlmApiType.KoboldCpp:
                    using (var client = new KoboldApiClient(settings.llmApiUrl, apiKey))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.Ollama:
                    using (var client = new OllamaApiClient(settings.llmApiUrl, settings.ollamaModelName))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.LMStudio:
                    using (var client = new LMStudioApiClient(settings.llmApiUrl, apiKey))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.OpenAI:
                    using (var client = new OpenAiApiClient(settings.llmApiUrl, apiKey, settings.openAiModelName))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.Gemini:
                    using (var client = new GeminiApiClient(settings.llmApiUrl, apiKey))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.Qwen:
                    using (var client = new QwenApiClient(settings.llmApiUrl, apiKey, settings.qwenModelName))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.Deepseek:
                    using (var client = new DeepseekApiClient(settings.llmApiUrl, apiKey, settings.deepseekModelName))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.Grok:
                    using (var client = new GrokApiClient(settings.llmApiUrl, apiKey, settings.grokModelName))
                    {
                        return await client.GenerateText(prompt);
                    }
                case LlmApiType.Claude:
                    using (var client = new ClaudeApiClient(settings.llmApiUrl, apiKey, settings.claudeModelName))
                    {
                        return await client.GenerateText(prompt);
                    }
                default:
                    throw new Exception("Unknown API type: " + settings.llmApiType);
            }
        }
        
        private void ProcessLLMResponse(string response)
        {
            SLog.Message("[Negotiation] Received response:\n" + response.Substring(0, Math.Min(500, response.Length)) + "...");
            
            // Check for outcome
            var outcomeMatch = Regex.Match(response, @"OUTCOME:\s*(POSITIVE|NEUTRAL|NEGATIVE)", RegexOptions.IgnoreCase);
            if (outcomeMatch.Success)
            {
                string outcomeStr = outcomeMatch.Groups[1].Value.ToUpper();
                NegotiationOutcome outcome;
                if (outcomeStr == "POSITIVE")
                {
                    outcome = NegotiationOutcome.Positive;
                }
                else if (outcomeStr == "NEGATIVE")
                {
                    outcome = NegotiationOutcome.Negative;
                }
                else
                {
                    outcome = NegotiationOutcome.Neutral;
                }
                
                // Extract any final dialogue before outcome processing
                ExtractAndDisplayDialogue(response);

                if (outcome == NegotiationOutcome.Negative)
                {
                    // Negative Outcome: Fail and Close
                    // Play negative sound
                    MessageTypeDefOf.NegativeEvent.sound.PlayOneShotOnCamera(null);
                    
                    dialog.AddConversationEntry("System", "<color=#FF4444>Negotiation Failed.</color>", false);
                    
                    // Logic: Negative always overrides pending.
                    EndNegotiation(outcome);
                    return;
                }
                else if (outcome == NegotiationOutcome.Positive)
                {
                    // Positive Outcome: Success and Keep Open
                    // Play positive sound
                    MessageTypeDefOf.PositiveEvent.sound.PlayOneShotOnCamera(null);
                    
                    string colorTag = "<color=#44FF44>";
                    string statusMsg = "Negotiation Successful!";
                    
                    dialog.AddConversationEntry("System", colorTag + statusMsg + "</color> You may continue chatting.", false);
                    
                    // Store this outcome as pending. If we continue and fail later, this will be overridden.
                    // If we continue and close, this will be applied.
                    pendingOutcome = outcome;
                    
                    // Do NOT close the window (EndNegotiation).
                    // Fall through to ExtractChoices to generate options for continuing
                }
            }
            
            // Extract dialogue (if not parsed above)
            // Since ExtractAndDisplayDialogue checks for duplicates or we already called it inside, 
            // we should technically avoid calling it twice. 
            // However, ExtractAndDisplayDialogue uses a regex that WON'T match if we already consumed the response?
            // Actually, Regex.Matches works on the string. It will match again.
            // We must skip this if we already did it in the outcome block.
            // But wait, the outcome block calls ExtractAndDisplayDialogue(response).
            // So we should put this call inside an 'else' or return early.
            // But we can't return early for Positive case because we need ExtractChoices.
            // Simplified: Removing the call below and putting it BEFORE the outcome check is safest.
            // But outcome check logic relies on "Extract any final dialogue before outcome".
            // Let's rely on the fact that if outcomeMatch.Success is false, we need to call it.
            // If it is true, we called it inside.
            // So:
            if (!outcomeMatch.Success)
            {
                ExtractAndDisplayDialogue(response);
            }
            
            // Extract choices and store them
            currentChoices = ExtractChoices(response);
            if (currentChoices.Count == 0)
            {
                SLog.Warning("[Negotiation] No choices parsed, providing defaults");
                currentChoices = GetDefaultChoices();
            }
            
            SLog.Message("[Negotiation] Setting " + currentChoices.Count + " choices");
            dialog.SetChoices(currentChoices);
        }
        
        private void ExtractAndDisplayDialogue(string response)
        {
            // Parse all dialogue lines in order of appearance
            // Match lines like "Name: text" for both pawns
            string initiatorName = initiator.LabelShort;
            string targetName = target.LabelShort;
            
            // Use a single regex to find all dialogue lines, then determine speaker
            string pattern = @"(?:^|\n)([\w]+):\s*(.+?)(?=\n|$)";
            var allMatches = Regex.Matches(response, pattern, RegexOptions.IgnoreCase);
            
            var ttsBatch = new List<DialogueLine>();
            
            foreach (Match match in allMatches)
            {
                string speaker = match.Groups[1].Value.Trim();
                string text = match.Groups[2].Value.Trim();
                
                if (string.IsNullOrEmpty(text)) continue;
                
                // Skip if this looks like a keyword/instruction rather than dialogue
                if (speaker.ToUpper() == "CHOICES" || speaker.ToUpper() == "OUTCOME" ||
                    speaker.ToUpper() == "END_CHOICES" || speaker.ToUpper() == "FORMAT")
                {
                    continue;
                }
                
                // Determine if this is initiator or target
                bool isInitiator = speaker.Equals(initiatorName, StringComparison.OrdinalIgnoreCase);
                bool isTarget = speaker.Equals(targetName, StringComparison.OrdinalIgnoreCase);
                
                if (isInitiator || isTarget)
                {
                    Pawn speakerPawn = isInitiator ? initiator : target;
                    Pawn recipientPawn = isInitiator ? target : initiator;
                    
                    dialog.AddConversationEntry(speaker, text, isInitiator);
                    conversationHistory.AppendLine(speaker + ": " + text);
                    
                    // Store for final display AND for TTS batching
                    var lineEntry = new DialogueLine(speakerPawn, recipientPawn, text);
                    lastDialogueLines.Add(lineEntry);
                    ttsBatch.Add(lineEntry);
                    
                    // Log to ChatLogManager
                    if (conversationId < 0)
                    {
                        conversationId = SpeechBubbleManager.StartConversation();
                    }
                    string fallbackText = string.Format("{0} negotiates with {1}.", speakerPawn.Name.ToStringShort, recipientPawn.Name.ToStringShort);
                    ChatLogManager.AddMessage(new ChatMessage(speakerPawn, recipientPawn, text, MessageType.LLMChat, conversationId, Color.white, fallbackText, text));
                    
                    SLog.Message("[Negotiation] Added dialogue: " + speaker + ": " + text.Substring(0, Math.Min(50, text.Length)));
                }
            }

            // Start staggered TTS dispatch
            if (ttsBatch.Count > 0 && SocialInteractions.Settings.enableTTS && Current.Root != null)
            {
                ((MonoBehaviour)Current.Root).StartCoroutine(ProcessTTSBatch(ttsBatch));
            }
        }

        private IEnumerator ProcessTTSBatch(List<DialogueLine> batch)
        {
            foreach (var line in batch)
            {
                TTSManager.Speak(line.Text, line.Speaker, SocialInteractions.Settings.ttsSpeed, (int)SocialInteractions.Settings.ttsVolume);
                // Stagger requests by 200ms (realtime) to force FIFO processing on server/network
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        
        private List<string> ExtractChoices(string response)
        {
            var choices = new List<string>();
            
            // Look for CHOICES: ... END_CHOICES block
            var choicesMatch = Regex.Match(response, @"CHOICES:\s*\n([\s\S]*?)(?:END_CHOICES|OUTCOME:|$)", RegexOptions.IgnoreCase);
            if (choicesMatch.Success)
            {
                string choicesBlock = choicesMatch.Groups[1].Value;
                
                // Extract numbered choices
                var choiceMatches = Regex.Matches(choicesBlock, @"^\s*\d+\.\s*(.+?)$", RegexOptions.Multiline);
                foreach (Match match in choiceMatches)
                {
                    string choice = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(choice) && choices.Count < 3)
                    {
                        choices.Add(choice);
                    }
                }
            }
            
            return choices;
        }
        
        private List<string> GetDefaultChoices()
        {
            return new List<string>
            {
                "Continue the discussion",
                "Change the subject",
                "End the conversation"
            };
        }
        
        private List<string> GetCurrentChoices()
        {
            return currentChoices;
        }
        
        private void HandleLLMFailure()
        {
            SLog.Warning("[Negotiation] LLM failed, using skill-based fallback");
            
            // Simple skill-based resolution
            int socialSkill = initiator.skills.GetSkill(SkillDefOf.Social).Level;
            float successChance = socialSkill / 20f; // 0% at 0 skill, 100% at 20 skill
            
            bool success = Rand.Value < successChance;
            
            dialog.AddConversationEntry("System", "The conversation concluded " + (success ? "positively" : "awkwardly") + ".", false);
            
            EndNegotiation(success ? NegotiationOutcome.Positive : NegotiationOutcome.Negative);
        }
        
        private void EndNegotiation(NegotiationOutcome? outcomeOverride = null)
        {
            // If explicit override (e.g. Negative), use it. Otherwise use pending, or default to Neutral.
            NegotiationOutcome finalOutcome = outcomeOverride ?? pendingOutcome ?? NegotiationOutcome.Neutral;
            
            SLog.Message("[Negotiation] EndNegotiation called. Override: " + outcomeOverride + ", Pending: " + pendingOutcome + " -> Final: " + finalOutcome);
            
            FinalizeNegotiation(finalOutcome);
            
            // Close dialog
            // Note: CloseDialog() triggers PreClose() -> Cleanup(). 
            // We set isActive=false inside FinalizeNegotiation so Cleanup won't run again.
            dialog.CloseDialog();
        }
        
        public void Cleanup()
        {
            // Called when window is closed manually (X button) or via CloseDialog()
            if (isActive)
            {
                SLog.Message("[Negotiation] Cleanup called while active (Manual Close). Pending: " + pendingOutcome);
                // If closing manually, use pending outcome or Neutral
                NegotiationOutcome finalOutcome = pendingOutcome ?? NegotiationOutcome.Neutral;
                FinalizeNegotiation(finalOutcome);
            }
        }
        
        private void FinalizeNegotiation(NegotiationOutcome outcome)
        {
            if (!isActive) return;
            isActive = false;
            
            SLog.Message("[Negotiation] Finalizing with outcome: " + outcome);
            
            // Remove hediff
            RemoveNegotiatingHediff();
            
            // Apply outcome (mood/xp)
            ApplyOutcome(outcome);
            
            // Set cooldown
            SetCooldown();
            
            // Show final dialogue lines as speech bubbles (last 2 lines)
            // Use standard Enqueue to ensure sequential playback (not reverse/overlapping)
            // Use the overload that DOES NOT log to ChatLog or trigger TTS (to avoid redundancy)
            int startIndex = Math.Max(0, lastDialogueLines.Count - 2);
            for (int i = startIndex; i < lastDialogueLines.Count; i++)
            {
                DialogueLine line = lastDialogueLines[i];
                
                // Format text with speaker name (e.g. "<color=...>Name</color>: Message")
                string formattedText = SpeechBubbleManager.FormatSpeakerName(line.Speaker, line.Text);
                
                // Wrap text for display
                string wrappedText = SocialInteractions.WrapText(formattedText, SocialInteractions.Settings.wordsPerLineLimit);
                
                // Estimate reading time
                float duration = SpeechBubbleManager.EstimateReadingTime(line.Text);
                
                // Enqueue using the non-logging overload:
                // (speaker, text, duration, isFirstMessage, conversationId, color, useCustomMote)
                SpeechBubbleManager.Enqueue(line.Speaker, wrappedText, duration, false, conversationId, Color.white, true);
            }
            
            // We do NOT manually call EndConversation here.
            // The SpeechBubbleManager will automatically end the conversation when the queue empties.
            
            // Close dialog
            dialog.CloseDialog();
        }
        
        private void ApplyNegotiatingHediff()
        {
            if (initiator.health != null && SI_HediffDefOf.SI_Negotiating != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(SI_HediffDefOf.SI_Negotiating, initiator);
                initiator.health.AddHediff(hediff);
                SLog.Message("[Negotiation] Applied SI_Negotiating hediff to " + initiator.LabelShort);
            }
        }
        
        private void RemoveNegotiatingHediff()
        {
            if (initiator.health != null)
            {
                Hediff hediff = initiator.health.hediffSet.GetFirstHediffOfDef(SI_HediffDefOf.SI_Negotiating);
                if (hediff != null)
                {
                    initiator.health.RemoveHediff(hediff);
                    SLog.Message("[Negotiation] Removed SI_Negotiating hediff from " + initiator.LabelShort);
                }
            }
        }
        
        private void ApplyOutcome(NegotiationOutcome outcome)
        {
            if (initiator.needs == null || initiator.needs.mood == null || 
                initiator.needs.mood.thoughts == null || initiator.needs.mood.thoughts.memories == null) return;
            
            switch (outcome)
            {
                case NegotiationOutcome.Positive:
                    if (SI_ThoughtDefOf.SI_NegotiationPositive != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationPositive);
                        SLog.Message("[Negotiation] Applied positive thought to " + initiator.LabelShort);
                    }
                    break;
                case NegotiationOutcome.Negative:
                    if (SI_ThoughtDefOf.SI_NegotiationNegative != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationNegative);
                        SLog.Message("[Negotiation] Applied negative thought to " + initiator.LabelShort);
                    }
                    break;
            }
        }
        
        private void SetCooldown()
        {
            // TODO: Set cooldown in settings
            SLog.Message("[Negotiation] Would set cooldown for " + initiator.LabelShort);
        }
        

    }
    
    public enum NegotiationOutcome
    {
        Positive,
        Neutral,
        Negative
    }

    /// <summary>
    /// Helper class to store dialogue lines for final display
    /// </summary>
    public class DialogueLine
    {
        public Pawn Speaker { get; set; }
        public Pawn Recipient { get; set; }
        public string Text { get; set; }
        
        public DialogueLine(Pawn speaker, Pawn recipient, string text)
        {
            Speaker = speaker;
            Recipient = recipient;
            Text = text;
        }
    }
}
