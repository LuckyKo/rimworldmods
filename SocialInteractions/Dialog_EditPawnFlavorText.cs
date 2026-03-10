using RimWorld;
using UnityEngine;
using Verse;
using System.Threading.Tasks;

namespace SocialInteractions
{
    /// <summary>
    /// Dialog window for editing a pawn's custom flavor text
    /// </summary>
    public class Dialog_EditPawnFlavorText : Window
    {
        private Pawn pawn;
        private string flavorText;
        private string initialFlavorText;
        private bool isGenerating = false;

        private const float WindowWidth = 600f;
        private const float WindowHeight = 400f;
        private const float MyMargin = 17f;
        private const float ButtonHeight = 35f;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WindowWidth, WindowHeight); }
        }

        public Dialog_EditPawnFlavorText(Pawn pawn)
        {
            this.pawn = pawn;
            this.flavorText = SocialInteractions.GetPawnFlavorText(pawn);
            this.initialFlavorText = this.flavorText;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(MyMargin, MyMargin, inRect.width - 2 * MyMargin, 30f), 
                string.Format("SocialInteractions_EditBioFor".Translate(), pawn.Name.ToStringShort));
            
            Text.Font = GameFont.Small;
            
            // Text area for editing the flavor text
            Rect textAreaRect = new Rect(MyMargin, MyMargin + 40f, inRect.width - 2 * MyMargin, 
                inRect.height - 2 * MyMargin - ButtonHeight - 55f);
            
            Rect outRect = textAreaRect;
            // Calculate height ensuring we have a bit of extra space for new lines, but at least the outRect height
            float calcHeight = Text.CalcHeight(flavorText, outRect.width - 16f) + 100f;
            float viewHeight = Mathf.Max(calcHeight, outRect.height);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            string newText = Widgets.TextArea(new Rect(0f, 0f, viewRect.width, viewHeight), flavorText);
            Widgets.EndScrollView();
            
            // Update the flavor text if it changed
            if (newText != flavorText)
            {
                flavorText = newText;
            }

            // Calculate button layout for 4 buttons to fit within the bottom margin
            float btnWidth = 130f;
            float btnSpacing = (inRect.width - (MyMargin * 2) - (btnWidth * 4)) / 3f;
            if (btnSpacing < 5f) btnSpacing = 5f; // Minimum spacing
            
            float currentX = MyMargin;
            float btnY = inRect.height - ButtonHeight - MyMargin;

            // Cancel button
            Rect cancelButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(cancelButtonRect, "SocialInteractions_Cancel".Translate()))
            {
                Close();
            }
            currentX += btnWidth + btnSpacing;

            // Auto-Generate button
            Rect autoGenButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            
            // Disable button if LLM is not enabled or if already generating
            bool canGenerate = SocialInteractions.Settings.llmInteractionsEnabled && 
                               !string.IsNullOrEmpty(SocialInteractions.Settings.llmApiUrl);
            
            string autoGenLabel = isGenerating ? "SocialInteractions_AutoGenerateBioGenerating".Translate() : "SocialInteractions_AutoGenerateBio".Translate();
            
            GUI.color = canGenerate && !isGenerating ? Color.white : Color.grey;
            
            if (Widgets.ButtonText(autoGenButtonRect, autoGenLabel, true, false, canGenerate && !isGenerating))
            {
                if (canGenerate && !isGenerating)
                {
                    isGenerating = true;
                    Task.Run(async () => 
                    {
                        string result = await SocialInteractions.GenerateBioAsync(pawn);
                        LongEventHandler.ExecuteWhenFinished(() => 
                        {
                            if (!string.IsNullOrEmpty(result))
                            {
                                flavorText = result;
                            }
                            isGenerating = false;
                        });
                    });
                }
            }
            if (!canGenerate)
            {
                TooltipHandler.TipRegion(autoGenButtonRect, "SocialInteractions_AutoGenerateBioDisabledTooltip".Translate());
            }
            else
            {
                TooltipHandler.TipRegion(autoGenButtonRect, "SocialInteractions_AutoGenerateBioTooltip".Translate());
            }
            GUI.color = Color.white;
            currentX += btnWidth + btnSpacing;

            // Clear button
            Rect clearButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(clearButtonRect, "SocialInteractions_Clear".Translate()))
            {
                flavorText = string.Empty;
                SocialInteractions.SetPawnFlavorText(pawn, string.Empty);
            }
            
            // Save button
            Rect doneButtonRect = new Rect(inRect.width - MyMargin - btnWidth, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(doneButtonRect, "SocialInteractions_Save".Translate()))
            {
                // Save the flavor text
                SocialInteractions.SetPawnFlavorText(pawn, flavorText);
                Close();
            }
        }

        private Vector2 scrollPosition = Vector2.zero;

        public override void PostClose()
        {
            base.PostClose();
            
            // If the flavor text was changed but the window was closed without saving,
            // restore the original value
            if (flavorText != initialFlavorText && !string.IsNullOrEmpty(initialFlavorText))
            {
                // We need to restore to the original value
                // But actually, since this is just a dialog close, we don't want to revert
                // The actual save happens in the OK button, so we don't need to do anything special here
            }
        }
    }
}