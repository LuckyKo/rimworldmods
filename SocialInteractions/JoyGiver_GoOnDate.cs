using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JoyGiver_GoOnDate : JoyGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            return null; // This joy giver is only used to find spots, not to give jobs directly.
        }
    }
}