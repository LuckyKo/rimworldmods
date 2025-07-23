using Verse;
using UnityEngine;

namespace SocialInteractions
{
    public class SocialInteractionsModSettings : ModSettings
    {
        public bool pawnsStopOnInteractionEnabled = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnsStopOnInteractionEnabled, "pawnsStopOnInteractionEnabled", true);
        }
    }

    public class SocialInteractionsMod : Mod
    {
        SocialInteractionsModSettings settings;

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
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("Pawns stop on interaction", ref settings.pawnsStopOnInteractionEnabled, "If enabled, pawns will stop their current activities during social interactions.");
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}