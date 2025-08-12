using Verse;
using UnityEngine;
using System.Collections.Generic; // New using directive
using System;

namespace SocialInteractions
{
    public class SocialInteractionsModSettings : ModSettings
    {
        public bool pawnsStopOnInteraction = true;
        public string llmApiUrl = "http://localhost:5001";
        public string llmApiKey = "1234";
        public string llmPromptTemplate = @"The following is a [topic] interraction between two RimWorld characters, [pawn1] and [pawn2]. Keep each dialogue line short with around 3-4 dialogue lines in total. It's a brutal world out there so use swearing, explicit or rough language freely.

[pawn1] is a [pawn1_age] yo [pawn1_sex], has the following traits: [pawn1_traits], [pawn1_genes]; [pawn1] is proficient in: [pawn1_proficiencies]; [pawn1]'s mood is [pawn1_mood], positives: [pawn1_likes] / negatives: [pawn1_dislikes]; Medical status: [pawn1_afflictions]. [pawn1] is currently [pawn1_action]

[pawn2] is a [pawn2_age] yo [pawn2_sex], has the following traits: [pawn2_traits], [pawn2_genes]; [pawn2] is proficient in: [pawn2_proficiencies]; [pawn2]'s mood is [pawn2_mood], positives: [pawn2_likes] / negatives: [pawn2_dislikes]; Medical status: [pawn2_afflictions]. [pawn2] is currently [pawn2_action]

[pawn2] is a [relation] to [pawn1].

It's currently [time], on [date] and the weather is [weather].

[subject]

<start>
[pawn1]:";
        public bool llmInteractionsEnabled = false;
        public int wordsPerLineLimit = 10; // Default to 10 words per line
        public float wordsPerSecond = 4.0f; // Default to 5 words per second
        public float llmTemperature = 0.7f; // Default temperature
        public int llmMaxTokens = 300; // Default max tokens

        public bool enableChitchat = true;
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
        public bool preventSpam = false;
        public string llmStoppingStrings = @"<end>
</end>
</start>
<start>
—END—
**end**
(end)";
        public bool enableCombatTaunts = true;
        public bool enableXtcSampling = false;
        public bool enableDating = true;
        public float joyThresholdForDate = 0.6f; // New setting


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnsStopOnInteraction, "pawnsStopOnInteraction", true);
            Scribe_Values.Look(ref enableCombatTaunts, "enableCombatTaunts", true);
            Scribe_Values.Look(ref llmInteractionsEnabled, "llmInteractionsEnabled", false);
            Scribe_Values.Look(ref llmApiUrl, "llmApiUrl", "");
            Scribe_Values.Look(ref llmApiKey, "llmApiKey", "");
            Scribe_Values.Look(ref llmPromptTemplate, "llmPromptTemplate", "");
            Scribe_Values.Look(ref wordsPerLineLimit, "wordsPerLineLimit", 10);
            
            Scribe_Values.Look(ref llmTemperature, "llmTemperature", 0.7f);
            Scribe_Values.Look(ref llmMaxTokens, "llmMaxTokens", 300);

            Scribe_Values.Look(ref enableChitchat, "enableChitchat", true);
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
            Scribe_Values.Look(ref llmStoppingStrings, "llmStoppingStrings", "");
            Scribe_Values.Look(ref preventSpam, "preventSpam", false);
            Scribe_Values.Look(ref enableXtcSampling, "enableXtcSampling", false);
            Scribe_Values.Look(ref enableDating, "enableDating", true);
            Scribe_Values.Look(ref joyThresholdForDate, "joyThresholdForDate", 0.8f); // New setting
        }
    }

    public class SocialInteractionsMod : Mod
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string llmApiUrlBuffer;
        private string llmApiKeyBuffer;
        private string llmPromptTemplateBuffer;

        public SocialInteractionsMod(ModContentPack content)
            : base(content)
        {
            SocialInteractions.Settings = GetSettings<SocialInteractionsModSettings>();
            llmApiUrlBuffer = SocialInteractions.Settings.llmApiUrl;
            llmApiKeyBuffer = SocialInteractions.Settings.llmApiKey;
            llmPromptTemplateBuffer = SocialInteractions.Settings.llmPromptTemplate;
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
            listingStandard.CheckboxLabeled("Enable LLM Interactions", ref SocialInteractions.Settings.llmInteractionsEnabled, "If enabled, Deep Talk interactions will use the configured LLM API.");
            listingStandard.CheckboxLabeled("Prevent Spam", ref SocialInteractions.Settings.preventSpam, "If enabled, new LLM interactions will not start until the previous one has finished displaying its speech bubbles.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable XTC Sampling", ref SocialInteractions.Settings.enableXtcSampling, "If enabled, XTC (Exclude Top Choices) sampling will be used for LLM requests to encourage more creative responses.");

            listingStandard.Gap();
            listingStandard.Label("LLM API Configuration");

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

            listingStandard.Label("Prompt Template:");
            string newPromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), llmPromptTemplateBuffer);
            if (newPromptTemplate != llmPromptTemplateBuffer)
            {
                llmPromptTemplateBuffer = newPromptTemplate;
                SocialInteractions.Settings.llmPromptTemplate = newPromptTemplate;
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
            listingStandard.Label(string.Format("Joy threshold for date (0.0 - 1.0): {0}", SocialInteractions.Settings.joyThresholdForDate.ToString("F2")));
            SocialInteractions.Settings.joyThresholdForDate = listingStandard.Slider(SocialInteractions.Settings.joyThresholdForDate, 0f, 1f);

            listingStandard.Gap();
            listingStandard.Label("LLM Temperature (0.1 - 2.0):");
            string temperatureBuffer = SocialInteractions.Settings.llmTemperature.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmTemperature, ref temperatureBuffer, 0.1f, 2.0f);

            listingStandard.Gap();
            listingStandard.Label("LLM Max Tokens (1 - 2000):");
            string maxTokensBuffer = SocialInteractions.Settings.llmMaxTokens.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmMaxTokens, ref maxTokensBuffer, 1, 2000);

            listingStandard.Gap();
            listingStandard.Label("Enabled LLM Interaction Types:");
            listingStandard.CheckboxLabeled("Chitchat", ref SocialInteractions.Settings.enableChitchat);
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