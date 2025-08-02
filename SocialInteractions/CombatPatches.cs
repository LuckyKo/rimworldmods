
using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    public static class CombatTaunts
    {
        public static readonly string[] AttackingTaunts = new string[]
        {
            "Take this!",
            "You're finished!",
            "For the colony!",
            "Eat steel!",
            "Die, you scum!"
        };

        public static readonly string[] GettingHitComplaints = new string[]
        {
            "Argh!",
            "They got me!",
            "I'm hit!",
            "Gah!",
            "That stings!"
        };

        public static readonly string[] DownedCallsForHelp = new string[]
        {
            "I'm down! Need help!",
            "Medic!",
            "Can't go on...",
            "They got me good...",
            "Ugh... darkness..."
        };
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Verb_MeleeAttack_TryCastShot_Patch
    {
        public static void Postfix(Verb_MeleeAttack __instance)
        {
            if (SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && Rand.Value < 0.25f)
            {
                string taunt = CombatTaunts.AttackingTaunts.RandomElement();
                SpeechBubbleManager.Enqueue(__instance.CasterPawn, taunt, 2f, true, 0);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
    public static class Pawn_HealthTracker_PreApplyDamage_Patch
    {
        public static void Postfix(Pawn_HealthTracker __instance, DamageInfo dinfo)
        {
            if (!SocialInteractions.Settings.enableCombatTaunts) return;
            Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_HealthTracker), "pawn").GetValue(__instance);
            if (dinfo.Def.harmsHealth && pawn.Spawned && Rand.Value < 0.25f)
            {
                string complaint = "<color=yellow>" + CombatTaunts.GettingHitComplaints.RandomElement() + "</color>";
                SpeechBubbleManager.Enqueue(pawn, complaint, 2f, true, 0);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Pawn_HealthTracker_MakeDowned_Patch
    {
        public static void Postfix(Pawn_HealthTracker __instance)
        {
            if (!SocialInteractions.Settings.enableCombatTaunts) return;
            Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_HealthTracker), "pawn").GetValue(__instance);
            if (pawn.Spawned && Rand.Value < 0.75f) // Higher chance for downed calls
            {
                string callForHelp = "<color=red>" + CombatTaunts.DownedCallsForHelp.RandomElement() + "</color>";
                SpeechBubbleManager.Enqueue(pawn, callForHelp, 3f, true, 0);
            }
        }
    }
}
