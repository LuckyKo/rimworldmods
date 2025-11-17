using RimWorld;
using Verse;

namespace SocialInteractions
{
    [DefOf]
    public static class ChildThoughtDefOf
    {
        public static ThoughtDef ChildAnnoyance;
        public static ThoughtDef ChildMisbehaved;
        public static ThoughtDef ChildBoredom;
        public static ThoughtDef ChildCrying;

        static ChildThoughtDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ChildThoughtDefOf));
        }
    }
}