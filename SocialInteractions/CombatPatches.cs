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
            "Take this!",
            "You're finished!",
            "Eat lead!",
            "Die, you scum!",
			"Your aim's as useless as your life!",
			"Looks like you lost the fight before it started, maggot!",
			"Time to put you in the dirt where you belong, worm!",
			"I've seen better shots from a blind monkey.",
			"Your sorry ass isn't worth the bullet, scum.",
			"If you were any more pathetic, you'd be a goblin.",
			"You're dead meat!",
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
            if (__result && SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && __instance.CasterPawn.RaceProps.Humanlike && Rand.Value < 0.75f)
            {
                string taunt = CombatTaunts.AttackingTaunts.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(taunt);
                SpeechBubbleManager.EnqueueInstant(__instance.CasterPawn, taunt, duration);
            }
        }
    }

    

    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
    public static class Verb_Shoot_TryCastShot_Patch
    {
        public static void Postfix(Verb_Shoot __instance, bool __result)
        {
            if (__result && SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && __instance.CasterPawn.RaceProps.Humanlike && Rand.Value < 0.25f)
            {
                Pawn casterPawn = __instance.CasterPawn;
                string taunt = CombatTaunts.AttackingTaunts.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(taunt);
                SpeechBubbleManager.EnqueueInstant(casterPawn, taunt, duration);
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

            if (pawn == null || !pawn.Spawned || pawn.Downed || !pawn.Awake() || !pawn.RaceProps.Humanlike) return;

            if (dinfo.Instigator == null || dinfo.Instigator == pawn || !dinfo.Def.ExternalViolenceFor(pawn)) return;

            if (dinfo.Instigator.HostileTo(pawn))
            {
                if (Rand.Value < 0.4f)
                {
                    string complaint = CombatTaunts.GettingHitComplaints.RandomElement();
                    float duration = SocialInteractions.EstimateReadingTime(complaint);
                    SpeechBubbleManager.EnqueueInstant(pawn, complaint, duration, Color.yellow);
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
            if (pawn.Spawned && pawn.RaceProps.Humanlike && Rand.Value < 0.85f)
            {
                string callForHelp = CombatTaunts.DownedCallsForHelp.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(callForHelp);
                SpeechBubbleManager.EnqueueInstant(pawn, callForHelp, duration, Color.red);
            }
        }
    }
    
}