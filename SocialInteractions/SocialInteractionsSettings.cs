using Verse;
using UnityEngine;
using System.Collections.Generic; // New using directive
using System;

namespace SocialInteractions
{
    public enum LlmApiType
    {
        KoboldCpp,
        Ollama,
        LMStudio,
        OpenAI,
        Gemini,
        Qwen,
        Deepseek,
        Grok,
        Claude
    }

    public class SocialInteractionsModSettings : ModSettings
    {
        // Default templates
        public const string DEFAULT_DIALOGUE_TEMPLATE = @"The following is an interraction between two RimWorld characters, [pawn1] and [pawn2]. Keep each dialogue line short with around 3-4 dialogue lines in total. It's a brutal world out there so use swearing, explicit or rough language freely.

[pawn1] is a [pawn1_sex], age [pawn1_age], a [pawn1_title] following the [pawn1_ideology] ideology, has the following traits: [pawn1_traits]; Xenotype: [pawn1_genes]; [pawn1] is proficient in: [pawn1_proficiencies]; [pawn1]'s mood is [pawn1_mood], positives: [pawn1_likes] / negatives: [pawn1_dislikes]; Medical status: [pawn1_afflictions]. [pawn1]'s family: [pawn1_family]. [pawn1] is currently [pawn1_action].

[pawn2] is a [pawn2_sex], age [pawn2_age], a [pawn2_title] following the [pawn2_ideology] ideology, has the following traits: [pawn2_traits]; Xenotype: [pawn2_genes]; [pawn2] is proficient in: [pawn2_proficiencies]; [pawn2]'s mood is [pawn2_mood], positives: [pawn2_likes] / negatives: [pawn2_dislikes]; Medical status: [pawn2_afflictions]. [pawn2]'s family: [pawn2_family]. [pawn2] is currently [pawn2_action].

[pawn2] is [pawn1]'s [relation].
Last time they spoke: [pawn1_journal]

It's currently [time], on [date] and the weather is [weather].

Current event: [subject]

<start>
[pawn1]:";
        
        public const string DEFAULT_MONOLOGUE_TEMPLATE = @"The following is a [topic] by a RimWorld character, [pawn1]. It's a brutal world out there so use swearing, explicit or rough language freely.

[pawn1] is a [pawn1_sex], age [pawn1_age], a [pawn1_title] following the [pawn1_ideology] ideology, has the following traits: [pawn1_traits]; Xenotype: [pawn1_genes]; [pawn1] is proficient in: [pawn1_proficiencies]; [pawn1]'s mood is [pawn1_mood], positives: [pawn1_likes] / negatives: [pawn1_dislikes]; Medical status: [pawn1_afflictions]. [pawn1] is currently [pawn1_action].

It's currently [time], on [date] and the weather is [weather].

Current event: [pawn1] [subject]

<start>
[pawn1]:";

        public string llmApiKey = "1234";
        public string llmPromptTemplate = DEFAULT_DIALOGUE_TEMPLATE;
        public string llmMonologuePromptTemplate = DEFAULT_MONOLOGUE_TEMPLATE;
        public bool llmInteractionsEnabled = false;
        public int wordsPerLineLimit = 10; // Default to 10 words per line
        public float wordsPerSecond = 4.0f; // Default to 5 words per second
        public float llmTemperature = 0.7f; // Default temperature
        public int llmMaxTokens = 300; // Default max tokens
        public int llmTopK = 0; // Default Top K (0 = disabled)
        public float llmTopP = 1.0f; // Default Top P (1.0 = disabled)
        public float llmMinP = 0.0f; // Default Min P (0.0 = disabled)
        public string ollamaModelName = "llama3.2"; // Default Ollama model name
        public string lmStudioModelName = "gemma-2-2b-it"; // Default LM Studio model name
        public string openAiModelName = "gpt-3.5-turbo"; // Default OpenAI model name
        public string geminiModelName = "gemini-2.5-flash"; // Default Gemini model name
        public string qwenModelName = "qwen-max"; // Default Qwen model name
        public string deepseekModelName = "deepseek-chat"; // Default Deepseek model name
        public string grokModelName = "grok-3-mini"; // Default Grok model name
        public string claudeModelName = "claude-3-sonnet-20240229"; // Default Claude model name
        public bool preventSpam = false;

        // API settings
        public LlmApiType llmApiType = LlmApiType.KoboldCpp; // Default to KoboldCpp
        public string llmApiUrl = "http://localhost:5001";
        
        // Feature enablement settings
        public bool pawnsStopOnInteraction = true;
        public bool enableCombatTaunts = true;
        public bool enableDatingFeature = true;
        public bool enableXtcSampling = false;
        
        public bool verboseLogging = false;
        public bool useBackgroundTextRendering = false; // False = drop shadow (current), True = background style
        
        // Interaction type settings
        public bool enableChitchat = true;
        public bool enableManualChat = true; // New setting for manual chat
        public bool enableDeepTalk = true;
        public bool enableInsult = true;
        public bool enableRomanceAttempt = true;
        public bool enableMarriageProposal = true;
        public bool enableReassure = true;
        public bool enableDisturbingChat = true;
        public bool enableTendPatient = true;
        public bool enableRescue = true;
        public bool enableVisitSickPawn = true;
        public bool enableLovin = true;
        public bool enableDating = true;
        
        // String settings
        public string llmStoppingStrings = @"<end>
</end>
</start>
<start>
—END—
**end**
(end)";
        
        // Magic number settings (not exposed in UI)
        public float meleeTauntProbability = 0.35f;
        public float shootTauntProbability = 0.15f;
        public float gettingHitComplaintProbability = 0.3f;
        public float downedCallForHelpProbability = 0.85f;
        public int dateCooldownTicks = 5000;
        public int maxDistanceForDate = 50;
        public float joyThresholdForDate = 0.5f;
        public int jobCheckIntervalTicks = 60;
        public int initialToleranceTicks = 60;
        public int goOnDateCooldownTicks = 600;
        public int cheatingConfrontationTicks = 300;
        
        // Dating lovin' settings
        public float baseLovinChance = 0.95f;
        public int dateLovinTicks = 2500;
        public int dateLovinTimeoutTicks = 600; // 10 seconds
        public float maxDistanceToLovinSpot = 50f; // Maximum distance to accept a bed for lovin'
        
        // Dating partner selection weights/penalties
        public float spouseDateWeight = 100f;
        public float fianceDateWeight = 90f;
        public float loverDateWeight = 80f;
        public float opinionAdjustmentFactor = 50f; // For relationship partners, opinion adjustment range: -2 to +2
        public float nonRelatedPartnerWeightFactor = 0.7f; // General weight factor for non-related partners
        public float cheatingPenalty = 30f;
        public float opinionDifferenceThreshold = 20f; // Opinion difference needed to eliminate cheating penalty


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnsStopOnInteraction, "pawnsStopOnInteraction", true);
            Scribe_Values.Look(ref enableCombatTaunts, "enableCombatTaunts", true);
            Scribe_Values.Look(ref llmInteractionsEnabled, "llmInteractionsEnabled", false);
            Scribe_Values.Look(ref llmApiType, "llmApiType", LlmApiType.KoboldCpp);
            Scribe_Values.Look(ref llmApiUrl, "llmApiUrl", "");
            Scribe_Values.Look(ref llmApiKey, "llmApiKey", "");
            Scribe_Values.Look(ref llmPromptTemplate, "llmPromptTemplate", "");
            Scribe_Values.Look(ref llmMonologuePromptTemplate, "llmMonologuePromptTemplate", "");
            Scribe_Values.Look(ref wordsPerLineLimit, "wordsPerLineLimit", 10);
            
            Scribe_Values.Look(ref llmTemperature, "llmTemperature", 0.7f);
            Scribe_Values.Look(ref llmMaxTokens, "llmMaxTokens", 300);
            Scribe_Values.Look(ref llmTopK, "llmTopK", 0);
            Scribe_Values.Look(ref llmTopP, "llmTopP", 1.0f);
            Scribe_Values.Look(ref llmMinP, "llmMinP", 0.0f);
            Scribe_Values.Look(ref ollamaModelName, "ollamaModelName", "llama3.2");
            Scribe_Values.Look(ref lmStudioModelName, "lmStudioModelName", "gemma-2-2b-it");
            Scribe_Values.Look(ref geminiModelName, "geminiModelName", "gemini-2.5-flash");
            Scribe_Values.Look(ref qwenModelName, "qwenModelName", "qwen-max");
            Scribe_Values.Look(ref deepseekModelName, "deepseekModelName", "deepseek-chat");
            Scribe_Values.Look(ref grokModelName, "grokModelName", "grok-3-mini");
            Scribe_Values.Look(ref claudeModelName, "claudeModelName", "claude-3-sonnet-20240229");

            Scribe_Values.Look(ref enableChitchat, "enableChitchat", true);
            Scribe_Values.Look(ref enableManualChat, "enableManualChat", true); // New setting for manual chat
            Scribe_Values.Look(ref enableDeepTalk, "enableDeepTalk", true);
            Scribe_Values.Look(ref enableInsult, "enableInsult", true);
            Scribe_Values.Look(ref enableRomanceAttempt, "enableRomanceAttempt", true);
            Scribe_Values.Look(ref enableMarriageProposal, "enableMarriageProposal", true);
            Scribe_Values.Look(ref enableReassure, "enableReassure", true);
            Scribe_Values.Look(ref enableDisturbingChat, "enableDisturbingChat", true);
            Scribe_Values.Look(ref enableTendPatient, "enableTendPatient", true);
            Scribe_Values.Look(ref enableRescue, "enableRescue", true);
            Scribe_Values.Look(ref enableVisitSickPawn, "enableVisitSickPawn", true);
            Scribe_Values.Look(ref enableLovin, "enableLovin", true);
            Scribe_Values.Look(ref enableDating, "enableDating", true);
            Scribe_Values.Look(ref enableDatingFeature, "enableDatingFeature", true);
            Scribe_Values.Look(ref llmStoppingStrings, "llmStoppingStrings", "");
            Scribe_Values.Look(ref preventSpam, "preventSpam", false);
            Scribe_Values.Look(ref enableXtcSampling, "enableXtcSampling", false);
            Scribe_Values.Look(ref joyThresholdForDate, "joyThresholdForDate", 0.8f);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
            
            Scribe_Values.Look(ref useBackgroundTextRendering, "useBackgroundTextRendering", false);
            
            // Magic number settings (not exposed in UI)
            Scribe_Values.Look(ref meleeTauntProbability, "meleeTauntProbability", 0.35f);
            Scribe_Values.Look(ref shootTauntProbability, "shootTauntProbability", 0.15f);
            Scribe_Values.Look(ref gettingHitComplaintProbability, "gettingHitComplaintProbability", 0.3f);
            Scribe_Values.Look(ref downedCallForHelpProbability, "downedCallForHelpProbability", 0.85f);
            Scribe_Values.Look(ref baseLovinChance, "baseLovinChance", 0.75f);
            Scribe_Values.Look(ref dateCooldownTicks, "dateCooldownTicks", 3000);
            Scribe_Values.Look(ref dateLovinTicks, "dateLovinTicks", 2000);
            Scribe_Values.Look(ref cheatingConfrontationTicks, "cheatingConfrontationTicks", 300);
            Scribe_Values.Look(ref maxDistanceForDate, "maxDistanceForDate", 50);
            Scribe_Values.Look(ref jobCheckIntervalTicks, "jobCheckIntervalTicks", 60);
            Scribe_Values.Look(ref initialToleranceTicks, "initialToleranceTicks", 60);
            Scribe_Values.Look(ref goOnDateCooldownTicks, "goOnDateCooldownTicks", 600);
            
            // Dating lovin' settings
            Scribe_Values.Look(ref dateLovinTimeoutTicks, "dateLovinTimeoutTicks", 300);
            Scribe_Values.Look(ref maxDistanceToLovinSpot, "maxDistanceToLovinSpot", 50f);
            
            // Dating partner selection weights/penalties
            Scribe_Values.Look(ref spouseDateWeight, "spouseDateWeight", 100f);
            Scribe_Values.Look(ref fianceDateWeight, "fianceDateWeight", 90f);
            Scribe_Values.Look(ref loverDateWeight, "loverDateWeight", 80f);
            Scribe_Values.Look(ref opinionAdjustmentFactor, "opinionAdjustmentFactor", 50f);
            Scribe_Values.Look(ref nonRelatedPartnerWeightFactor, "nonRelatedPartnerWeightFactor", 1.0f);
            Scribe_Values.Look(ref cheatingPenalty, "cheatingPenalty", 30f);
            Scribe_Values.Look(ref opinionDifferenceThreshold, "opinionDifferenceThreshold", 20f);
        }
    }

    public class SocialInteractionsMod : Mod
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string llmApiUrlBuffer;
        private string llmApiKeyBuffer;
        private string llmPromptTemplateBuffer;
        private string llmMonologuePromptTemplateBuffer;
        private string openAiModelNameBuffer;

        public SocialInteractionsMod(ModContentPack content)
            : base(content)
        {
            SocialInteractions.Settings = GetSettings<SocialInteractionsModSettings>();
            llmApiUrlBuffer = SocialInteractions.Settings.llmApiUrl;
            llmApiKeyBuffer = SocialInteractions.Settings.llmApiKey;
            llmPromptTemplateBuffer = SocialInteractions.Settings.llmPromptTemplate;
            llmMonologuePromptTemplateBuffer = SocialInteractions.Settings.llmMonologuePromptTemplate;
            openAiModelNameBuffer = SocialInteractions.Settings.openAiModelName;
        }

        public override string SettingsCategory()
        {
            return "Social Interactions";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(inRect.x, inRect.y, inRect.width - 16f, inRect.height * 5); // Adjust height as needed
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(viewRect);
            listingStandard.CheckboxLabeled("Pawns stop on interaction", ref SocialInteractions.Settings.pawnsStopOnInteraction, "If enabled, pawns will stop their current activities during social interactions.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable Combat Taunts", ref SocialInteractions.Settings.enableCombatTaunts, "If enabled, pawns will taunt each other in combat.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable Dating Feature", ref SocialInteractions.Settings.enableDatingFeature, "If enabled, pawns will be able to go on dates.");
            listingStandard.Label(string.Format("Joy threshold for date (0.0 - 1.0): {0}", SocialInteractions.Settings.joyThresholdForDate.ToString("F2")));
            SocialInteractions.Settings.joyThresholdForDate = listingStandard.Slider(SocialInteractions.Settings.joyThresholdForDate, 0f, 1f);
            listingStandard.Label(string.Format("Base lovin' chance after a date (0.0 - 1.0): {0}", SocialInteractions.Settings.baseLovinChance.ToString("F2")));
            SocialInteractions.Settings.baseLovinChance = listingStandard.Slider(SocialInteractions.Settings.baseLovinChance, 0f, 1f);
            
            // Add a button to open the chat log window
            if (listingStandard.ButtonText("Open Chat Log Window"))
            {
                // Open the chat log tab
                Find.MainTabsRoot.SetCurrentTab(DefDatabase<RimWorld.MainButtonDef>.GetNamed("SocialInteractions_ChatLog"));
            }


            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable LLM Interactions", ref SocialInteractions.Settings.llmInteractionsEnabled, "If enabled, Deep Talk interactions will use the configured LLM API.");
            listingStandard.CheckboxLabeled("Prevent Spam", ref SocialInteractions.Settings.preventSpam, "If enabled, new LLM interactions will not start until the previous one has finished displaying its speech bubbles.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Use Text Background", ref SocialInteractions.Settings.useBackgroundTextRendering, "If enabled, LLM-generated text will be displayed with a background instead of a drop shadow.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable Verbose Logging", ref SocialInteractions.Settings.verboseLogging, "If enabled, detailed logs will be written to the Player.log file for debugging purposes.");

            listingStandard.Gap();
            listingStandard.Label("LLM API Configuration");

            // API Type Selection
            listingStandard.Gap();
            listingStandard.Label("LLM API Type:");
            string[] apiTypeNames = System.Enum.GetNames(typeof(LlmApiType));
            LlmApiType[] apiTypeValues = (LlmApiType[])System.Enum.GetValues(typeof(LlmApiType));
            int currentApiTypeIndex = System.Array.IndexOf(apiTypeValues, SocialInteractions.Settings.llmApiType);
            
            // Use a horizontal row of buttons instead of SelectionGrid
            Rect rowRect = listingStandard.GetRect(30f);
            float buttonWidth = rowRect.width / apiTypeNames.Length;
            for (int i = 0; i < apiTypeNames.Length; i++)
            {
                Rect buttonRect = new Rect(rowRect.x + i * buttonWidth, rowRect.y, buttonWidth, rowRect.height);
                bool isSelected = (i == currentApiTypeIndex);
                if (Widgets.ButtonText(buttonRect, apiTypeNames[i]))
                {
                    SocialInteractions.Settings.llmApiType = apiTypeValues[i];
                    // Set default URL based on API type
                    switch (apiTypeValues[i])
                    {
                        case LlmApiType.KoboldCpp:
                            SocialInteractions.Settings.llmApiUrl = "http://localhost:5001";
                            llmApiUrlBuffer = "http://localhost:5001";
                            break;
                        case LlmApiType.Ollama:
                            SocialInteractions.Settings.llmApiUrl = "http://localhost:11434";
                            llmApiUrlBuffer = "http://localhost:11434";
                            break;
                        case LlmApiType.LMStudio:
                            SocialInteractions.Settings.llmApiUrl = "http://localhost:1234";
                            llmApiUrlBuffer = "http://localhost:1234";
                            break;
                        case LlmApiType.OpenAI:
                            SocialInteractions.Settings.llmApiUrl = "https://api.openai.com";
                            llmApiUrlBuffer = "https://api.openai.com";
                            break;
                        case LlmApiType.Gemini:
                            SocialInteractions.Settings.llmApiUrl = "https://generativelanguage.googleapis.com";
                            llmApiUrlBuffer = "https://generativelanguage.googleapis.com";
                            break;
                        case LlmApiType.Qwen:
                            SocialInteractions.Settings.llmApiUrl = "https://dashscope.aliyuncs.com";
                            llmApiUrlBuffer = "https://dashscope.aliyuncs.com";
                            break;
                        case LlmApiType.Deepseek:
                            SocialInteractions.Settings.llmApiUrl = "https://api.deepseek.com";
                            llmApiUrlBuffer = "https://api.deepseek.com";
                            break;
                        case LlmApiType.Grok:
                            SocialInteractions.Settings.llmApiUrl = "https://api.x.ai";
                            llmApiUrlBuffer = "https://api.x.ai";
                            break;
                        case LlmApiType.Claude:
                            SocialInteractions.Settings.llmApiUrl = "https://api.anthropic.com";
                            llmApiUrlBuffer = "https://api.anthropic.com";
                            break;
                    }
                }
            }

            listingStandard.Label("API URL:");
            string newApiUrl = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), llmApiUrlBuffer);
            if (newApiUrl != llmApiUrlBuffer)
            {
                llmApiUrlBuffer = newApiUrl;
                SocialInteractions.Settings.llmApiUrl = newApiUrl;
            }

            listingStandard.Label("API Key (stored in plain text):");
            string newApiKey = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), llmApiKeyBuffer);
            if (newApiKey != llmApiKeyBuffer)
            {
                llmApiKeyBuffer = newApiKey;
                SocialInteractions.Settings.llmApiKey = newApiKey;
            }

            // Ollama-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Ollama)
            {
                listingStandard.Gap();
                listingStandard.Label("Ollama Model Name:");
                string newOllamaModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.ollamaModelName);
                if (!string.IsNullOrEmpty(newOllamaModel))
                {
                    SocialInteractions.Settings.ollamaModelName = newOllamaModel;
                }
            }
            
            // LM Studio-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.LMStudio)
            {
                listingStandard.Gap();
                listingStandard.Label("LM Studio Model Name:");
                string newLMStudioModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.lmStudioModelName);
                if (!string.IsNullOrEmpty(newLMStudioModel))
                {
                    SocialInteractions.Settings.lmStudioModelName = newLMStudioModel;
                }
            }
            
            // OpenAI-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.OpenAI)
            {
                listingStandard.Gap();
                listingStandard.Label("OpenAI Model Name:");
                string newOpenAiModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), openAiModelNameBuffer);
                if (!string.IsNullOrEmpty(newOpenAiModel))
                {
                    openAiModelNameBuffer = newOpenAiModel;
                    SocialInteractions.Settings.openAiModelName = newOpenAiModel;
                }
            }
            
            // Gemini-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Gemini)
            {
                listingStandard.Gap();
                listingStandard.Label("Gemini Model Name:");
                string newGeminiModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.geminiModelName);
                if (!string.IsNullOrEmpty(newGeminiModel))
                {
                    SocialInteractions.Settings.geminiModelName = newGeminiModel;
                }
            }
            
            // Qwen-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Qwen)
            {
                listingStandard.Gap();
                listingStandard.Label("Qwen Model Name:");
                string newQwenModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.qwenModelName);
                if (!string.IsNullOrEmpty(newQwenModel))
                {
                    SocialInteractions.Settings.qwenModelName = newQwenModel;
                }
            }
            
            // Deepseek-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Deepseek)
            {
                listingStandard.Gap();
                listingStandard.Label("Deepseek Model Name:");
                string newDeepseekModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.deepseekModelName);
                if (!string.IsNullOrEmpty(newDeepseekModel))
                {
                    SocialInteractions.Settings.deepseekModelName = newDeepseekModel;
                }
            }
            
            // Grok-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Grok)
            {
                listingStandard.Gap();
                listingStandard.Label("Grok Model Name:");
                string newGrokModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.grokModelName);
                if (!string.IsNullOrEmpty(newGrokModel))
                {
                    SocialInteractions.Settings.grokModelName = newGrokModel;
                }
            }
            
            // Claude-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Claude)
            {
                listingStandard.Gap();
                listingStandard.Label("Claude Model Name:");
                string newClaudeModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.claudeModelName);
                if (!string.IsNullOrEmpty(newClaudeModel))
                {
                    SocialInteractions.Settings.claudeModelName = newClaudeModel;
                }
            }

            listingStandard.Label("Prompt Template:");
            string newPromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), llmPromptTemplateBuffer);
            if (newPromptTemplate != llmPromptTemplateBuffer)
            {
                llmPromptTemplateBuffer = newPromptTemplate;
                SocialInteractions.Settings.llmPromptTemplate = newPromptTemplate;
            }

            listingStandard.Gap();
            listingStandard.Label("Monologue Prompt Template:");
            string newMonologuePromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), llmMonologuePromptTemplateBuffer);
            if (newMonologuePromptTemplate != llmMonologuePromptTemplateBuffer)
            {
                llmMonologuePromptTemplateBuffer = newMonologuePromptTemplate;
                SocialInteractions.Settings.llmMonologuePromptTemplate = newMonologuePromptTemplate;
            }

            // Add Reset Templates button
            listingStandard.Gap();
            listingStandard.Label("Reset Templates:");
            if (listingStandard.ButtonText("Reset Templates to Default"))
            {
                SocialInteractions.Settings.llmPromptTemplate = SocialInteractionsModSettings.DEFAULT_DIALOGUE_TEMPLATE;
                SocialInteractions.Settings.llmMonologuePromptTemplate = SocialInteractionsModSettings.DEFAULT_MONOLOGUE_TEMPLATE;
                llmPromptTemplateBuffer = SocialInteractions.Settings.llmPromptTemplate;
                llmMonologuePromptTemplateBuffer = SocialInteractions.Settings.llmMonologuePromptTemplate;
            }

            listingStandard.Gap();
            listingStandard.Label("LLM Stopping Strings (one per line):");
            SocialInteractions.Settings.llmStoppingStrings = Widgets.TextArea(listingStandard.GetRect(100f), SocialInteractions.Settings.llmStoppingStrings);

            listingStandard.Gap();
            listingStandard.Label("Words per line limit (for speech bubbles):");
            string wordsPerLineBuffer = SocialInteractions.Settings.wordsPerLineLimit.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.wordsPerLineLimit, ref wordsPerLineBuffer, 1, 50);

            listingStandard.Gap();
            listingStandard.Label("Words per second (for speech bubble duration):");
            string wordsPerSecondBuffer = SocialInteractions.Settings.wordsPerSecond.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.wordsPerSecond, ref wordsPerSecondBuffer, 1.0f, 20.0f);

            listingStandard.Gap();
            listingStandard.Label("Max Output Tokens (1 - 2000):");
            string maxTokensBuffer = SocialInteractions.Settings.llmMaxTokens.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmMaxTokens, ref maxTokensBuffer, 1, 2000);

            listingStandard.Gap();
            listingStandard.Label("Temperature (0.1 - 2.0):");
            string temperatureBuffer = SocialInteractions.Settings.llmTemperature.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmTemperature, ref temperatureBuffer, 0.1f, 2.0f);

            listingStandard.Gap();
            listingStandard.Label("Top K (0 = disabled, 1-100 = enabled):");
            string topKBuffer = SocialInteractions.Settings.llmTopK.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmTopK, ref topKBuffer, 0, 100);

            listingStandard.Gap();
            listingStandard.Label("Top P (0.0 - 1.0, 1.0 = disabled):");
            string topPBuffer = SocialInteractions.Settings.llmTopP.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmTopP, ref topPBuffer, 0.0f, 1.0f);

            listingStandard.Gap();
            listingStandard.Label("Min P (0.0 - 1.0, 0.0 = disabled):");
            string minPBuffer = SocialInteractions.Settings.llmMinP.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmMinP, ref minPBuffer, 0.0f, 1.0f);

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("XTC Sampling", ref SocialInteractions.Settings.enableXtcSampling, "If enabled, XTC (Exclude Top Choices) sampling will be used for LLM requests to encourage more creative responses.");

            listingStandard.Gap();
            listingStandard.Label("Enabled LLM Interaction Types:");
            listingStandard.CheckboxLabeled("Chitchat", ref SocialInteractions.Settings.enableChitchat);
            listingStandard.CheckboxLabeled("Manual Chat", ref SocialInteractions.Settings.enableManualChat); // New setting for manual chat
            listingStandard.CheckboxLabeled("DeepTalk", ref SocialInteractions.Settings.enableDeepTalk);
            listingStandard.CheckboxLabeled("Insult", ref SocialInteractions.Settings.enableInsult);
            listingStandard.CheckboxLabeled("RomanceAttempt", ref SocialInteractions.Settings.enableRomanceAttempt);
            listingStandard.CheckboxLabeled("MarriageProposal", ref SocialInteractions.Settings.enableMarriageProposal);
            listingStandard.CheckboxLabeled("Reassure", ref SocialInteractions.Settings.enableReassure);
            listingStandard.CheckboxLabeled("DisturbingChat", ref SocialInteractions.Settings.enableDisturbingChat);
            listingStandard.CheckboxLabeled("TendPatient", ref SocialInteractions.Settings.enableTendPatient);
            listingStandard.CheckboxLabeled("Rescue", ref SocialInteractions.Settings.enableRescue);
            listingStandard.CheckboxLabeled("VisitSickPawn", ref SocialInteractions.Settings.enableVisitSickPawn);
            listingStandard.CheckboxLabeled("Lovin", ref SocialInteractions.Settings.enableLovin);
            listingStandard.CheckboxLabeled("Dating", ref SocialInteractions.Settings.enableDating);

            listingStandard.End();

            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }
    }
}