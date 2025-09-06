using HarmonyLib;
using RimWorld;
using Verse;
using System;
using UnityEngine;

namespace SocialInteractions
{
    public static class CombatTaunts
    {
        public static readonly string[] AttackingTaunts = new string[]
        {
            "Eat lead!",
            "You're finished!",
            "Die, you scum!",
			"Your aim's as useless as your life!",
			"Looks like you lost the fight before it started, maggot!",
			"Time to put you in the dirt where you belong, worm!",
			"I've seen better shots from a blind monkey.",
			"Your sorry ass isn't worth the bullet, scum.",
			"If you were any more pathetic, you'd be a goblin.",
			"That shot was so bad, even a pustule could do better.",
			"Get used to dying, because it's gonna be your new hobby.",
			"You're going down, bitch!",
			"Stick that in your ass, loser!",
			"Looks like your momma didn't raise no soldier!",
			"Gonna paint the floor red with your blood, fuckface!",
			"This is for my homies!",
			"Your mama must've dropped you on your head as a kid, dumbass.",
			"Time to send you back to whatever rock you crawled out from under!",
			"I'm gonna make you eat those bullets, maggot!",
			"You're gonna bleed out like a pig!",
			"Your mom didn't raise no killer!",
			"Fuck your sorry ass!",
			"Die, maggot!",
			"Crybaby needs a timeout!",
			"That shot won't save you from my lead!",
			"You're just a warm body to me now.",
			"My bullet's got your name on it.",
			"Looks like someone left their brains in their locker.",
			"You're about to meet your maker!",
			"I'm the one who'll be carving you up tonight.",
			"Wish you'd aimed better, asshole!",
			"You're just a target practice dummy!",
			"Your screams will be music to my ears!",
			"Time to send you back to whatever hell you crawled out of.",
			"You're about to get reamed... literally!",
			"This ain't no game, punk. You're dead meat!",
			"Your life just flashed before your eyes…and it was short.",
			"You shoulda stayed in bed, you useless sack of shit."
        };

        public static readonly string[] MeleeAttackingTaunts = new string[]
        {
            "Take this!",
            "Eat steel!",
            "Die by my blade!",
            "Feel my steel!",
            "You're mine!",
            "Taste my blade!",
            "Prepare to die!",
            "I'll gut you!",
            "Slice and dice!",
            "Cut to pieces!",
            "You're dead meat!",
            "Time to bleed!",
            "This is for my homies!",
            "Your blood will be my victory!",
            "I'll carve you up!",
            "You're finished!",
            "Die, scum!",
            "Feel the pain!",
            "I'll rip you apart!",
            "You're history!",
            "Time to meet your maker!",
            "This is gonna hurt!",
            "I'll end you!",
            "You're going down!",
            "This is for my family!",
            "Feel my wrath!",
            "You're nothing!",
            "I'll crush you!",
            "Die screaming!",
            "This is personal!",
            "You're dead!",
            "I'll make you pay!",
            "Time to die!",
            "You're finished, maggot!",
            "Eat steel, bitch!",
            "This is the end for you!",
            "I'll show you pain!",
            "Your time is up!",
            "I'll send you to hell!",
            "You're fucked!"
        };

        public static readonly string[] GettingHitComplaints = new string[]
        {
            "Argh!",
            "They got me!",
            "I'm hit!",
            "Gah!",
            "That stings!",
			"Ahh, shit!",
			"Ow, fuck!",
			"I'm hit!!",
			"Son of a bitch!",
			"Ngh, that hurt!",
			"It's just a flesh wound...",
			"Goddammit, not again!",
			"Take that, you bastard!",
			"Fucking ow, my side!",
			"Shit, I'm bleeding!",
			"Who the hell shot me?!",
			"Gah, that stings!",
			"I've been hit!",
			"Dammit, my leg!",
			"Ow! What the fuck?",
			"Shit, did he just-",
			"Ah! My side hurts!",
			"Gah, not again!",
			"Ugh, why me?!",
			"Oof! That hurts like a motherfucker!",
			"Fuck, is that blood?",
			"Oh no, not shot again...",
			"Ahh, my leg's on fire!",
			"What do you mean shoot back?! I'm a pacifist!",
			"Holy crap, that hurt like hell!",
			"I think I need a medic... or a mortician.",
			"Why'd he have to aim for my dick?!",
			"This isn't fun anymore, I hate this game!",
			"Oh man, I'm bleeding out fast!"
        };

        public static readonly string[] DownedCallsForHelp = new string[]
        {
            "I'm down! Need help!",
            "Medic!",
            "HELP!!",
            "Can't go on...",
            "They got me good...",
            "Ugh... darkness...",
			"Please don't leave me...",
			"Someone help me, I'm dying here...",
			"Medic! Hurry up before I bleed out!",
			"I can't see straight, call a doc..",
			"This is it, I'm finished... so fucking unfair.",
			"Don't let me die, please... I've got family!",
			"My vision's going black, I'm slipping away...",
			"Get a medic over here, stat! I'm critical!",
			"I don't wanna die here, not like this...",
			"Someone help me up, I'm not done fighting yet!",
			"I can feel myself fading... this is so scary.",
			"Call an evac, get me the fuck outta here!",
			"I'm too young to die, there's still so much I wanna do...",
			"I can barely breathe, send a medic post-haste!",
			"I don't wanna be a corpse, not now... not ever."
        };
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Verb_MeleeAttack_TryCastShot_Patch
    {
        public static void Postfix(Verb_MeleeAttack __instance, bool __result)
        {
            if (__result && SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && __instance.CasterPawn.RaceProps.Humanlike && !ShamblerHelper.IsShambler(__instance.CasterPawn) && Rand.Value < SocialInteractions.Settings.meleeTauntProbability)
            {
                string taunt = CombatTaunts.MeleeAttackingTaunts.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(taunt);
                SpeechBubbleManager.EnqueueInstant(__instance.CasterPawn, taunt, duration, null, false); // Use standard mote for combat taunts
            }
        }
    }

    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
    public static class Verb_Shoot_TryCastShot_Patch
    {
        public static void Postfix(Verb_Shoot __instance, bool __result)
        {
            if (__result && SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && __instance.CasterPawn.RaceProps.Humanlike && !ShamblerHelper.IsShambler(__instance.CasterPawn) && Rand.Value < SocialInteractions.Settings.shootTauntProbability)
            {
                Pawn casterPawn = __instance.CasterPawn;
                string taunt = CombatTaunts.AttackingTaunts.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(taunt);
                SpeechBubbleManager.EnqueueInstant(casterPawn, taunt, duration, null, false); // Use standard mote for combat taunts
            }
        }
    }
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PostApplyDamage")]
    public static class Pawn_HealthTracker_PreApplyDamage_Patch
    {
        public static void Postfix(Pawn_HealthTracker __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (!SocialInteractions.Settings.enableCombatTaunts) return;

            Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_HealthTracker), "pawn").GetValue(__instance);

            if (pawn == null || !pawn.Spawned || pawn.Downed || !pawn.Awake() || !pawn.RaceProps.Humanlike || ShamblerHelper.IsShambler(pawn)) return;

            if (dinfo.Instigator == null || dinfo.Instigator == pawn || !dinfo.Def.ExternalViolenceFor(pawn)) return;

            if (dinfo.Instigator.HostileTo(pawn))
            {
                if (Rand.Value < SocialInteractions.Settings.gettingHitComplaintProbability)
                {
                    string complaint = CombatTaunts.GettingHitComplaints.RandomElement();
                    float duration = SocialInteractions.EstimateReadingTime(complaint);
                    SpeechBubbleManager.EnqueueInstant(pawn, complaint, duration, Color.yellow, false); // Use standard mote for combat taunts
                }
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
            if (pawn.Spawned && pawn.RaceProps.Humanlike && !ShamblerHelper.IsShambler(pawn) && Rand.Value < SocialInteractions.Settings.downedCallForHelpProbability)
            {
                string callForHelp = CombatTaunts.DownedCallsForHelp.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(callForHelp);
                SpeechBubbleManager.EnqueueInstant(pawn, callForHelp, duration, Color.red, false); // Use standard mote for combat taunts
            }
        }
    }
}

// Helper method to check if a pawn is a shambler
public static class ShamblerHelper
{
    public static bool IsShambler(Pawn pawn)
    {
        // Check if the ModsConfig.AnomalyActive is true first (shambler functionality requires Anomaly DLC)
        if (!ModsConfig.AnomalyActive) return false;
        
        // Check if the pawn has the IsShambler property
        try
        {
            // Use reflection to access the IsShambler property since it might not be available in all versions
            var isShamblerProperty = typeof(Pawn).GetProperty("IsShambler");
            if (isShamblerProperty != null)
            {
                return (bool)isShamblerProperty.GetValue(pawn, null);
            }
        }
        catch (Exception ex)
        {
            // If there's any exception, just return false to be safe
            SocialInteractions.SLog.Warning(string.Format("[SocialInteractions] Exception checking if pawn is shambler: {0}", ex.Message));
        }
        
        return false;
    }
}