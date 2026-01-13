using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    /// <summary>
    /// Dialog for pawn-to-pawn negotiation with LLM-generated choices.
    /// Styled after RimWorld's faction comms Dialog_Negotiation.
    /// </summary>
    public class Dialog_PawnNegotiation : Window
    {
        private Pawn initiator;
        private Pawn target;
        private NegotiationManager manager;
        
        // Conversation state
        private List<ConversationEntry> conversationHistory = new List<ConversationEntry>();
        private List<string> currentChoices = new List<string>();
        private string customInputText = "";
        private bool waitingForLLM = false;
        private string waitingMessage = "Waiting for response...";
        
        // Scroll positions
        private Vector2 conversationScrollPos = Vector2.zero;
        private Vector2 choicesScrollPos = Vector2.zero;
        private bool shouldAutoScroll = true; // Auto-scroll when new content is added
        
        // Layout constants
        // Layout constants
        private const float TitleHeight = 40f;
        private const float InfoHeight = 30f;
        private const float HeaderHeight = TitleHeight + InfoHeight;
        private const float ChoiceButtonHeight = 32f;
        private const float ChoiceSpacing = 5f;
        private const float CustomInputHeight = 30f;
        private const float BottomButtonsHeight = 30f;
        private const float DialogMargin = 10f;
        
        public override Vector2 InitialSize
        {
            get { return new Vector2(720f, 650f); }
        }
        
        public Dialog_PawnNegotiation(Pawn initiator, Pawn target)
        {
            this.initiator = initiator;
            this.target = target;
            
            // Window settings
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = false; // We handle closing ourselves
            
            soundAppear = SoundDefOf.CommsWindow_Open;
            soundClose = SoundDefOf.CommsWindow_Close;
            
            // Create the manager
            manager = new NegotiationManager(initiator, target, this);
        }
        
        public override void PostOpen()
        {
            base.PostOpen();
            // Start the negotiation
            manager.StartNegotiation();
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            // Draw header with both pawns
            DrawHeader(inRect);
            
            // Calculate content area (below header)
            float contentY = HeaderHeight + Margin;
            float contentHeight = inRect.height - contentY;
            
            // Split remaining space: conversation history (top) and choices (bottom)
            float choicesAreaHeight = CalculateChoicesAreaHeight();
            float conversationHeight = contentHeight - choicesAreaHeight - Margin;
            
            Rect conversationRect = new Rect(0, contentY, inRect.width, conversationHeight);
            Rect choicesRect = new Rect(0, contentY + conversationHeight + Margin, inRect.width, choicesAreaHeight);
            
            DrawConversationHistory(conversationRect);
            DrawChoicesArea(choicesRect);
        }
        
        private void DrawHeader(Rect inRect)
        {
            Widgets.BeginGroup(inRect);
            
            float halfWidth = inRect.width / 2f;
            
            // Left side: Initiator
            Rect initiatorNameRect = new Rect(0, 0, halfWidth, TitleHeight);
            Rect initiatorInfoRect = new Rect(0, TitleHeight, halfWidth, InfoHeight);
            
            // Right side: Target
            Rect targetNameRect = new Rect(halfWidth, 0, halfWidth, TitleHeight);
            Rect targetInfoRect = new Rect(halfWidth, TitleHeight, halfWidth, InfoHeight);
            
            // Draw initiator
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(initiatorNameRect, initiator.LabelCap);
            
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            string initiatorInfo = "SocialSkillIs".Translate(initiator.skills.GetSkill(SkillDefOf.Social).Level);
            Widgets.Label(initiatorInfoRect, initiatorInfo);
            
            // Draw target
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = Color.white;
            Widgets.Label(targetNameRect, target.LabelCap);
            
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            Text.Anchor = TextAnchor.UpperRight;
            string targetInfo = "SocialSkillIs".Translate(target.skills.GetSkill(SkillDefOf.Social).Level);
            Widgets.Label(targetInfoRect, targetInfo);
            
            // Reset
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            
            Widgets.EndGroup();
        }
        
        private void DrawConversationHistory(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            Rect innerRect = rect.ContractedBy(Margin);
            
            // Create a snapshot copy to avoid collection modification during enumeration
            // Use try-catch because modifications may happen from another thread
            ConversationEntry[] historySnapshot;
            try
            {
                historySnapshot = conversationHistory.ToArray();
            }
            catch (System.ArgumentException)
            {
                // Collection was modified during copy, skip this frame
                return;
            }
            catch (System.InvalidOperationException)
            {
                // Collection was modified during copy, skip this frame
                return;
            }
            
            // Calculate content height
            float contentHeight = 0f;
            Text.Font = GameFont.Small;
            foreach (var entry in historySnapshot)
            {
                string line = entry.SpeakerName + ": " + entry.Text;
                contentHeight += Text.CalcHeight(line, innerRect.width - 16f) + 5f;
            }
            
            if (waitingForLLM)
            {
                contentHeight += Text.CalcHeight(waitingMessage, innerRect.width - 16f) + 5f;
            }
            
            Rect viewRect = new Rect(0, 0, innerRect.width - 16f, Mathf.Max(contentHeight, innerRect.height));
            
            Widgets.BeginScrollView(innerRect, ref conversationScrollPos, viewRect);
            
            float y = 0f;
            foreach (var entry in historySnapshot)
            {
                // Color code by speaker
                GUI.color = entry.IsInitiator ? new Color(0.8f, 1f, 0.8f) : new Color(0.8f, 0.8f, 1f);
                
                string line = entry.SpeakerName + ": " + entry.Text;
                float lineHeight = Text.CalcHeight(line, viewRect.width);
                Widgets.Label(new Rect(0, y, viewRect.width, lineHeight), line);
                y += lineHeight + 5f;
            }
            
            if (waitingForLLM)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0, y, viewRect.width, 30f), waitingMessage);
            }
            
            GUI.color = Color.white;
            Widgets.EndScrollView();
            
            // Auto-scroll to bottom only when new content was added
            if (shouldAutoScroll && contentHeight > innerRect.height)
            {
                conversationScrollPos.y = contentHeight - innerRect.height;
                shouldAutoScroll = false; // Reset flag after scrolling
            }
        }
        
        private float CalculateChoicesAreaHeight()
        {
            int choiceCount = currentChoices.Count;
            if (choiceCount == 0) choiceCount = 1; // At least show "waiting" or end button
            
            // Choices + custom input + end button + margins
            return (choiceCount * (ChoiceButtonHeight + ChoiceSpacing)) 
                   + CustomInputHeight + ChoiceSpacing 
                   + BottomButtonsHeight + Margin * 2;
        }
        
        private void DrawChoicesArea(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(Margin);
            
            float y = innerRect.y;
            float buttonWidth = innerRect.width;
            
            // Draw choices
            if (!waitingForLLM && currentChoices.Count > 0)
            {
                for (int i = 0; i < currentChoices.Count; i++)
                {
                    string fullChoice = currentChoices[i];
                    string choiceLabel = (i + 1) + ". " + TruncateWithEllipsis(fullChoice, 95);
                    Rect buttonRect = new Rect(innerRect.x, y, buttonWidth, ChoiceButtonHeight);
                    
                    if (Widgets.ButtonText(buttonRect, choiceLabel, true, true, true))
                    {
                        manager.OnChoiceSelected(i);
                    }
                    
                    // Add tooltip for full text in case it's truncated
                    TooltipHandler.TipRegion(buttonRect, fullChoice);
                    
                    y += ChoiceButtonHeight + ChoiceSpacing;
                }
            }
            else if (waitingForLLM)
            {
                // Show waiting indicator
                Rect waitRect = new Rect(innerRect.x, y, buttonWidth, ChoiceButtonHeight);
                GUI.color = Color.gray;
                Widgets.Label(waitRect, "Generating response...");
                GUI.color = Color.white;
                y += ChoiceButtonHeight + ChoiceSpacing;
            }
            
            // Custom input field
            Rect inputLabelRect = new Rect(innerRect.x, y, 100f, CustomInputHeight);
            Rect inputFieldRect = new Rect(innerRect.x + 65f, y, buttonWidth - 65f - 80f, CustomInputHeight);
            Rect sendButtonRect = new Rect(innerRect.x + buttonWidth - 70f, y, 70f, CustomInputHeight);
            
            Widgets.Label(inputLabelRect, "Or say:");
            customInputText = Widgets.TextField(inputFieldRect, customInputText);
            
            bool canSend = !waitingForLLM && !string.IsNullOrEmpty(customInputText.Trim());
            if (Widgets.ButtonText(sendButtonRect, "Send", true, true, canSend) && canSend)
            {
                manager.OnCustomInput(customInputText.Trim());
                customInputText = "";
            }
            
            y += CustomInputHeight + ChoiceSpacing + Margin;
            
            // End conversation button
            Rect endButtonRect = new Rect(innerRect.x, y, buttonWidth, BottomButtonsHeight);
            if (Widgets.ButtonText(endButtonRect, "End Conversation"))
            {
                manager.EndNegotiationEarly();
            }
        }
        
        // Called by NegotiationManager
        public void AddConversationEntry(string speakerName, string text, bool isInitiator)
        {
            conversationHistory.Add(new ConversationEntry
            {
                SpeakerName = speakerName,
                Text = text,
                IsInitiator = isInitiator
            });
            shouldAutoScroll = true; // Scroll to new content
        }
        
        public void SetChoices(List<string> choices)
        {
            currentChoices = choices ?? new List<string>();
            waitingForLLM = false;
        }
        
        public void SetWaiting(bool waiting)
        {
            waitingForLLM = waiting;
            if (waiting)
            {
                currentChoices.Clear();
            }
        }
        
        public void CloseDialog()
        {
            Close();
        }
        
        public override void PreClose()
        {
            base.PreClose();
            manager.Cleanup();
        }

        private string TruncateWithEllipsis(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3).Trim() + "...";
        }
    }
    
    /// <summary>
    /// Single entry in the conversation history.
    /// </summary>
    public class ConversationEntry
    {
        public string SpeakerName;
        public string Text;
        public bool IsInitiator;
    }
}
