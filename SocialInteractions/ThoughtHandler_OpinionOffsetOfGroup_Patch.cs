using HarmonyLib;
using RimWorld;
using Verse;
using System;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(ThoughtHandler), "OpinionOffsetOfGroup")]
    public static class ThoughtHandler_OpinionOffsetOfGroup_Patch
    {
        public static bool Prefix(ISocialThought group, ref int __result)
        {
            Thought thought = group as Thought;
            if (thought != null && thought.def != null && thought.def.defName == "OnDate")
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }
}