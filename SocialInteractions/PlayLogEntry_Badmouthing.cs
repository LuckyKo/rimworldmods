using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SocialInteractions
{
    /// <summary>
    /// Custom PlayLogEntry for badmouthing interactions that includes information about the target pawn
    /// </summary>
    public class PlayLogEntry_Badmouthing : PlayLogEntry_Interaction
    {
        // Store the target pawn that was badmouthed
        private Pawn targetPawn;
        
        // Need parameterless constructor for XML serialization
        public PlayLogEntry_Badmouthing()
        {
        }
        
        public PlayLogEntry_Badmouthing(InteractionDef intDef, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, Pawn targetPawn = null)
            : base(intDef, initiator, recipient, extraSentencePacks)
        {
            this.targetPawn = targetPawn;
        }
        
        // Override the ToGameStringFromPOV method to include target pawn information
        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
        {
            // Call the base implementation first to get the standard log text
            string baseText = base.ToGameStringFromPOV(pov, forceLog);
            
            // If we have a target pawn, append information about who was badmouthed
            if (targetPawn != null)
            {
                // Depending on perspective, format the message differently
                if (pov == initiator)
                {
                    // From initiator's perspective: "You spoke negatively about Target to Recipient"
                    return string.Format("{0} (spoke negatively about {1})", baseText, targetPawn.LabelShort);
                }
                else if (pov == recipient)
                {
                    // From recipient's perspective: "Initiator spoke negatively about Target to you"
                    return string.Format("{0} (about {1})", baseText, targetPawn.LabelShort);
                }
                else if (pov == targetPawn)
                {
                    // From target's perspective: "Initiator spoke negatively about you to Recipient"
                    return string.Format("{0} (they spoke negatively about you)", baseText);
                }
                else
                {
                    // Third person perspective: "Initiator spoke negatively about Target to Recipient"
                    return string.Format("{0} (about {1})", baseText, targetPawn.LabelShort);
                }
            }
            
            return baseText;
        }
    }
}