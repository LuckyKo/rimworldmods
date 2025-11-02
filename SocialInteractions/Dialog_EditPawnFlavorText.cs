using RimWorld;
using UnityEngine;
using Verse;

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
                string.Format("Edit Bio for {0}", pawn.Name.ToStringShort));
            
            // Text area for editing the flavor text
            Rect textAreaRect = new Rect(MyMargin, MyMargin + 40f, inRect.width - 2 * MyMargin, 
                inRect.height - 2 * MyMargin - ButtonHeight - 55f);
            
            // Create a text area using Widgets.TextFieldMultiline to allow multi-line editing
            string newText = Widgets.TextArea(textAreaRect, flavorText);
            
            // Update the flavor text if it changed
            if (newText != flavorText)
            {
                flavorText = newText;
            }

            // Done button
            Rect doneButtonRect = new Rect(
                inRect.width - MyMargin - 150f, // Position from right
                inRect.height - ButtonHeight - MyMargin, 
                150f, 
                ButtonHeight
            );
            
            if (Widgets.ButtonText(doneButtonRect, "Save"))
            {
                // Save the flavor text
                SocialInteractions.SetPawnFlavorText(pawn, flavorText);
                Close();
            }
            
            // Cancel button
            Rect cancelButtonRect = new Rect(
                MyMargin, 
                inRect.height - ButtonHeight - MyMargin,
                150f, 
                ButtonHeight
            );
            
            if (Widgets.ButtonText(cancelButtonRect, "Cancel"))
            {
                Close();
            }

            // Clear button
            Rect clearButtonRect = new Rect(
                inRect.width / 2f - 75f, 
                inRect.height - ButtonHeight - MyMargin,
                150f, 
                ButtonHeight
            );
            
            if (Widgets.ButtonText(clearButtonRect, "Clear"))
            {
                flavorText = string.Empty;
                SocialInteractions.SetPawnFlavorText(pawn, string.Empty);
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