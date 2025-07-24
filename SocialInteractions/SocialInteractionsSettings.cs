using Verse;
using UnityEngine;

namespace SocialInteractions
{
    public class SocialInteractionsModSettings : ModSettings
    {
        public bool pawnsStopOnInteractionEnabled = true;
        public string llmApiUrl = "";
        public string llmApiKey = "";
        public string llmPromptTemplate = "";
        public bool llmInteractionsEnabled = false;
        public int wordsPerLineLimit = 10; // Default to 10 words per line
        public float wordsPerSecond = 5.0f; // Default to 5 words per second
        public float llmTemperature = 0.7f; // Default temperature
        public int llmMaxTokens = 200; // Default max tokens

        public bool enableChitchat = true;
        public bool enableDeepTalk = true;
        public bool enableInsult = true;
        public bool enableRomanceAttempt = true;
        public bool enableMarriageProposal = true;
        public bool enableReassure = true;
        public bool enableDisturbingChat = true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnsStopOnInteractionEnabled, "pawnsStopOnInteractionEnabled", true);
            Scribe_Values.Look(ref llmApiUrl, "llmApiUrl", "");
            Scribe_Values.Look(ref llmApiKey, "llmApiKey", "");
            Scribe_Values.Look(ref llmPromptTemplate, "llmPromptTemplate", "");
            Scribe_Values.Look(ref llmInteractionsEnabled, "llmInteractionsEnabled", false);
            Scribe_Values.Look(ref wordsPerLineLimit, "wordsPerLineLimit", 10);
            
            Scribe_Values.Look(ref llmTemperature, "llmTemperature", 0.7f);
            Scribe_Values.Look(ref llmMaxTokens, "llmMaxTokens", 200);

            Scribe_Values.Look(ref enableChitchat, "enableChitchat", true);
            Scribe_Values.Look(ref enableDeepTalk, "enableDeepTalk", true);
            Scribe_Values.Look(ref enableInsult, "enableInsult", true);
            Scribe_Values.Look(ref enableRomanceAttempt, "enableRomanceAttempt", true);
            Scribe_Values.Look(ref enableMarriageProposal, "enableMarriageProposal", true);
            Scribe_Values.Look(ref enableReassure, "enableReassure", true);
            Scribe_Values.Look(ref enableDisturbingChat, "enableDisturbingChat", true);
        }
    }

    public class SocialInteractionsMod : Mod
    {
        SocialInteractionsModSettings settings;
        private Vector2 scrollPosition = Vector2.zero;

        public SocialInteractionsMod(ModContentPack content)
            : base(content)
        {
            this.settings = GetSettings<SocialInteractionsModSettings>();
        }

        public override string SettingsCategory()
        {
            return "Social Interactions";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(inRect.x, inRect.y, inRect.width - 16f, inRect.height * 2); // Adjust height as needed
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(viewRect);
            listingStandard.CheckboxLabeled("Pawns stop on interaction", ref settings.pawnsStopOnInteractionEnabled, "If enabled, pawns will stop their current activities during social interactions.");

            listingStandard.Gap();
            listingStandard.Label("LLM API Configuration");

            listingStandard.Label("API URL:");
            settings.llmApiUrl = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), settings.llmApiUrl);

            listingStandard.Label("API Key (stored in plain text):");
            settings.llmApiKey = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), settings.llmApiKey);

            listingStandard.Label("Prompt Template:");
            settings.llmPromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), settings.llmPromptTemplate);

            listingStandard.Gap();
            listingStandard.Label("Words per line limit (for speech bubbles):");
            string wordsPerLineBuffer = settings.wordsPerLineLimit.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref settings.wordsPerLineLimit, ref wordsPerLineBuffer, 1, 50);

            listingStandard.Gap();
            listingStandard.Label("Words per second (for speech bubble duration):");
            string wordsPerSecondBuffer = settings.wordsPerSecond.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref settings.wordsPerSecond, ref wordsPerSecondBuffer, 1.0f, 20.0f);

            listingStandard.Gap();
            listingStandard.Label("LLM Temperature (0.1 - 2.0):");
            string temperatureBuffer = settings.llmTemperature.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref settings.llmTemperature, ref temperatureBuffer, 0.1f, 2.0f);

            listingStandard.Gap();
            listingStandard.Label("LLM Max Tokens (1 - 2000):");
            string maxTokensBuffer = settings.llmMaxTokens.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref settings.llmMaxTokens, ref maxTokensBuffer, 1, 2000);

            

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable LLM Interactions", ref settings.llmInteractionsEnabled, "If enabled, Deep Talk interactions will use the configured LLM API.");

            listingStandard.Gap();
            listingStandard.Label("Enabled LLM Interaction Types:");
            listingStandard.CheckboxLabeled("Chitchat", ref settings.enableChitchat);
            listingStandard.CheckboxLabeled("DeepTalk", ref settings.enableDeepTalk);
            listingStandard.CheckboxLabeled("Insult", ref settings.enableInsult);
            listingStandard.CheckboxLabeled("RomanceAttempt", ref settings.enableRomanceAttempt);
            listingStandard.CheckboxLabeled("MarriageProposal", ref settings.enableMarriageProposal);
            listingStandard.CheckboxLabeled("Reassure", ref settings.enableReassure);
            listingStandard.CheckboxLabeled("DisturbingChat", ref settings.enableDisturbingChat);

            listingStandard.End();

            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }
    }
}