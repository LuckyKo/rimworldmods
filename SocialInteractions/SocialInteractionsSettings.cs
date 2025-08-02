using Verse;
using UnityEngine;

namespace SocialInteractions
{
    public class SocialInteractionsModSettings : ModSettings
    {
        public bool pawnsStopOnInteraction = true;
        public string llmApiUrl = "http://localhost:5001";
        public string llmApiKey = "1234";
        public string llmPromptTemplate = @"The following is a [topic] interraction between two RimWorld characters, [pawn1] and [pawn2]. Keep each dialogue line short with around 3-4 dialogue lines in total. It's a brutal world out there so use swearing, explicit or rough language freely.

[pawn1] is a [pawn1_age] yo [pawn1_sex], has the following traits: [pawn1_traits], [pawn1_genes]; [pawn1] is proficient in: [pawn1_proficiencies]; [pawn1]'s mood is [pawn1_mood], positives: [pawn1_likes] / negatives: [pawn1_dislikes]; Medical conditions: [pawn1_afflictions]. [pawn1] is currently [pawn1_action]

[pawn2] is a [pawn2_age] yo [pawn2_sex], has the following traits: [pawn2_traits], [pawn2_genes]; [pawn2] is proficient in: [pawn2_proficiencies]; [pawn2]'s mood is [pawn2_mood], positives: [pawn2_likes] / negatives: [pawn2_dislikes]; Medical conditions: [pawn2_afflictions]. [pawn2] is currently [pawn2_action]

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
        public string llmStoppingStrings = @"</start>
<start>
<end>
</end>";
        public bool enableCombatTaunts = true;


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
            Scribe_Values.Look(ref llmMaxTokens, "llmMaxTokens", 200);

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
            listingStandard.CheckboxLabeled("Pawns stop on interaction", ref settings.pawnsStopOnInteraction, "If enabled, pawns will stop their current activities during social interactions.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable Combat Taunts", ref settings.enableCombatTaunts, "If enabled, pawns will taunt each other in combat.");

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Enable LLM Interactions", ref settings.llmInteractionsEnabled, "If enabled, Deep Talk interactions will use the configured LLM API.");
            listingStandard.CheckboxLabeled("Prevent Spam", ref settings.preventSpam, "If enabled, new LLM interactions will not start until the previous one has finished displaying its speech bubbles.");

            listingStandard.Gap();
            listingStandard.Label("LLM API Configuration");

            listingStandard.Label("API URL:");
            settings.llmApiUrl = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), settings.llmApiUrl);

            listingStandard.Label("API Key (stored in plain text):");
            settings.llmApiKey = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), settings.llmApiKey);

            listingStandard.Label("Prompt Template:");
            settings.llmPromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), settings.llmPromptTemplate);

            listingStandard.Gap();
            listingStandard.Label("LLM Stopping Strings (one per line):");
            settings.llmStoppingStrings = Widgets.TextArea(listingStandard.GetRect(100f), settings.llmStoppingStrings);

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
            listingStandard.Label("Enabled LLM Interaction Types:");
            listingStandard.CheckboxLabeled("Chitchat", ref settings.enableChitchat);
            listingStandard.CheckboxLabeled("DeepTalk", ref settings.enableDeepTalk);
            listingStandard.CheckboxLabeled("Insult", ref settings.enableInsult);
            listingStandard.CheckboxLabeled("RomanceAttempt", ref settings.enableRomanceAttempt);
            listingStandard.CheckboxLabeled("MarriageProposal", ref settings.enableMarriageProposal);
            listingStandard.CheckboxLabeled("Reassure", ref settings.enableReassure);
            listingStandard.CheckboxLabeled("DisturbingChat", ref settings.enableDisturbingChat);
            listingStandard.CheckboxLabeled("TendPatient", ref settings.enableTendPatient);
            listingStandard.CheckboxLabeled("Rescue", ref settings.enableRescue);
            listingStandard.CheckboxLabeled("VisitSickPawn", ref settings.enableVisitSickPawn);
            listingStandard.CheckboxLabeled("Lovin", ref settings.enableLovin);

            listingStandard.End();

            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }
    }
}