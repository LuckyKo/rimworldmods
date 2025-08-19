using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;
using UnityEngine;

namespace SocialInteractions
{
    public enum DateStage
    {
        Joy,
        Lovin,
        Finished
    }

    public class Date : IExposable
    {
        public Pawn Initiator;
        public Pawn Partner;
        public DateStage Stage;

        public Date()
        {
            // Default constructor for deserialization
        }

        public Date(Pawn initiator, Pawn partner)
        {
            this.Initiator = initiator;
            this.Partner = partner;
            this.Stage = DateStage.Joy;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Initiator, "initiator");
            Scribe_References.Look(ref Partner, "partner");
            Scribe_Values.Look(ref Stage, "stage", DateStage.Joy);
        }
    }

    public static class DatingManager
    {
        private static List<Date> dates = new List<Date>();
        private static readonly object datesLock = new object();
        private static Dictionary<int, int> dateCooldowns = new Dictionary<int, int>();
        // private const int DateCooldownTicks = 300; // 5 min (now configurable in settings)

        // Add methods for serialization
        public static void ExposeData()
        {
            lock (datesLock)
            {
                // Serialize the dates list
                Scribe_Collections.Look(ref dates, "dates", LookMode.Deep);
                
                // Serialize the date cooldowns dictionary
                Scribe_Collections.Look(ref dateCooldowns, "dateCooldowns", LookMode.Value, LookMode.Value);
            }
        }

        // Flag to track if AdvanceDateStage was called from within JobDriver_GoOnDate
        // This helps distinguish between successful advancement and interruption.
        [ThreadStatic] // Use ThreadStatic to avoid conflicts in multi-threaded scenarios, though RimWorld jobs are mostly single-threaded per pawn.
        private static bool _wasDateStageAdvancedByJob = false;
        public static bool WasDateStageAdvancedByJob { get { return _wasDateStageAdvancedByJob; } set { _wasDateStageAdvancedByJob = value; } }

        public static void StartDate(Pawn initiator, Pawn partner)
        {
            lock (datesLock)
            {
                if (initiator == null || partner == null) return;
                SLog.Message(string.Format("[SocialInteractions] DatingManager.StartDate called for Initiator: {0}, Partner: {1}", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                if (!IsOnDate(initiator) && !IsOnDate(partner))
                {
                    SLog.Message(string.Format("[SocialInteractions] Starting date between {0} and {1}.", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                    HediffDef onDateHediffDef = HediffDef.Named("OnDate");
                    if (onDateHediffDef != null)
                    {
                        if (initiator.health != null) initiator.health.AddHediff(onDateHediffDef);
                        if (partner.health != null) partner.health.AddHediff(onDateHediffDef);
                    }
                    dates.Add(new Date(initiator, partner));
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] DatingManager.StartDate: Not starting date because one or both pawns are already on a date. Initiator: {0} (OnDate: {1}), Partner: {2} (OnDate: {3})", initiator.Name.ToStringShort, IsOnDate(initiator), partner.Name.ToStringShort, IsOnDate(partner)));
                }
            }
        }

        public static void RejectDate(Pawn initiator, Pawn partner)
        {
            // Remove the OnDate hediff if it was added
            HediffDef onDateHediffDef = HediffDef.Named("OnDate");
            if (onDateHediffDef != null)
            {
                if (initiator != null && initiator.health != null)
                {
                    Hediff initiatorHediff = initiator.health.hediffSet.GetFirstHediffOfDef(onDateHediffDef);
                    if (initiatorHediff != null)
                    {
                        initiator.health.RemoveHediff(initiatorHediff);
                    }
                }
                
                if (partner != null && partner.health != null)
                {
                    Hediff partnerHediff = partner.health.hediffSet.GetFirstHediffOfDef(onDateHediffDef);
                    if (partnerHediff != null)
                    {
                        partner.health.RemoveHediff(partnerHediff);
                    }
                }
            }
            
            // Add cooldown to prevent immediate re-invitation
            if (initiator != null && partner != null)
            {
                int expiryTick = Find.TickManager.TicksGame + SocialInteractions.Settings.dateCooldownTicks;
                dateCooldowns[initiator.thingIDNumber] = expiryTick;
                dateCooldowns[partner.thingIDNumber] = expiryTick;
            }
        }

        public static void EndDate(Date date)
        {
            lock (datesLock)
            {
                if (date == null) 
                {
                    SLog.Warning("[SocialInteractions] DatingManager.EndDate called with null date.");
                    return; 
                }

                // Add null checks for initiator and partner
                if (date.Initiator == null && date.Partner == null)
                {
                    SLog.Warning("[SocialInteractions] DatingManager.EndDate called with date that has null initiator and partner.");
                    return;
                }

                string initiatorLabel = (date.Initiator != null) ? date.Initiator.LabelShort : "NULL";
                string partnerLabel = (date.Partner != null) ? date.Partner.LabelShort : "NULL";
                
                SLog.Message(string.Format("[SocialInteractions] Ending date for {0} and {1}.", initiatorLabel, partnerLabel));

                // Remove the date from the list first to prevent race conditions
                if (!dates.Remove(date))
                {
                    // If the date was already removed, do nothing further.
                    SLog.Message(string.Format("[SocialInteractions] Date for {0} and {1} was already removed.", initiatorLabel, partnerLabel));
                    return;
                }

                // Add cooldown for non-null pawns
                int expiryTick = Find.TickManager.TicksGame + SocialInteractions.Settings.dateCooldownTicks;
                if (date.Initiator != null)
                    dateCooldowns[date.Initiator.thingIDNumber] = expiryTick;
                if (date.Partner != null)
                    dateCooldowns[date.Partner.thingIDNumber] = expiryTick;

                // Explicitly end both pawns' jobs if they're still on DateLovin jobs
                JobDef dateLovinJobDef = SI_JobDefOf.DateLovin;
                if (date.Initiator != null && date.Initiator.jobs != null && date.Initiator.CurJobDef == dateLovinJobDef)
                {
                    SLog.Message(string.Format("[SocialInteractions] Ending DateLovin job for initiator {0}.", initiatorLabel));
                    try
                    {
                        date.Initiator.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception ending DateLovin job for initiator {0}: {1}", initiatorLabel, ex.Message));
                    }
                }
                if (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJobDef == dateLovinJobDef)
                {
                    SLog.Message(string.Format("[SocialInteractions] Ending DateLovin job for partner {0}.", partnerLabel));
                    try
                    {
                        date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception ending DateLovin job for partner {0}: {1}", partnerLabel, ex.Message));
                    }
                }

                // Remove hediffs
                HediffDef onDateDef = HediffDef.Named("OnDate");
                if (onDateDef != null)
                {
                    if (date.Initiator != null && date.Initiator.health != null && date.Initiator.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff hediffInitiator = date.Initiator.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                            if (hediffInitiator != null) date.Initiator.health.RemoveHediff(hediffInitiator);
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing OnDate hediff from initiator {0}: {1}", initiatorLabel, ex.Message));
                        }
                    }

                    if (date.Partner != null && date.Partner.health != null && date.Partner.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff hediffPartner = date.Partner.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                            if (hediffPartner != null) date.Partner.health.RemoveHediff(hediffPartner);
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing OnDate hediff from partner {0}: {1}", partnerLabel, ex.Message));
                        }
                    }
                }
            }
        }

        private static Date GetDateWith_Unlocked(Pawn pawn)
        {
            return dates.FirstOrDefault(d => d.Initiator == pawn || d.Partner == pawn);
        }

        public static bool IsOnDate(Pawn pawn)
        {
            if (pawn == null) 
            {
                // Only log in debug mode or remove entirely
                // SLog.Message("[SocialInteractions] DatingManager.IsOnDate: Pawn is null, returning false.");
                return false;
            }
            
            if (pawn.health == null || pawn.health.hediffSet == null) 
            {
                // Only log in debug mode or remove entirely
                // SLog.Message(string.Format("[SocialInteractions] DatingManager.IsOnDate: Pawn {0} has no health or hediffSet, returning false.", pawn.Name.ToStringShort));
                return false;
            }
            
            HediffDef onDateDef = HediffDef.Named("OnDate");
            if (onDateDef == null) 
            {
                // Only log in debug mode or remove entirely
                // SLog.Message("[SocialInteractions] DatingManager.IsOnDate: OnDate hediff def is null, returning false.");
                return false;
            }
            
            bool hasHediff = pawn.health.hediffSet.HasHediff(onDateDef);
            // Only log in debug mode or remove entirely
            // SLog.Message(string.Format("[SocialInteractions] DatingManager.IsOnDate: Pawn {0} has OnDate hediff: {1}", pawn.Name.ToStringShort, hasHediff));
            return hasHediff;
        }

        public static Date GetDateWith(Pawn pawn)
        {
            lock (datesLock)
            {
                return GetDateWith_Unlocked(pawn);
            }
        }

                public static Pawn GetPartnerOfDateWith(Pawn pawn)
        {
            if (pawn == null) return null;

            lock (datesLock)
            {
                foreach (Date date in dates)
                {
                    if (date.Initiator == pawn)
                    {
                        return date.Partner;
                    }
                    else if (date.Partner == pawn)
                    {
                        return date.Initiator;
                    }
                }
            }
            return null;
        }

        public static Pawn GetInitiatorOfDateWith(Pawn pawn)
        {
            lock (datesLock)
            {
                if (pawn == null) return null;
                Date date = GetDateWith_Unlocked(pawn);
                if (date != null)
                {
                    return date.Initiator;
                }
                return null;
            }
        }

        public static bool IsOnDateCooldown(Pawn pawn)
        {
            if (pawn == null) return true;
            lock (datesLock)
            {
                int expiryTick;
                if (dateCooldowns.TryGetValue(pawn.thingIDNumber, out expiryTick))
                {
                    bool onCooldown = Find.TickManager.TicksGame < expiryTick;
                    SLog.Message(string.Format("[SocialInteractions] IsOnDateCooldown check for {0}: Found expiry tick {1}. Current tick: {2}. On cooldown: {3}", pawn.LabelShort, expiryTick, Find.TickManager.TicksGame, onCooldown));
                    if (onCooldown)
                    {
                        return true;
                    }
                    else
                    {
                        dateCooldowns.Remove(pawn.thingIDNumber);
                        return false;
                    }
                }
                return false;
            }
        }

        public static void CleanupExpiredDateCooldowns()
        {
            lock (datesLock)
            {
                List<int> expiredKeys = new List<int>();
                int currentTick = Find.TickManager.TicksGame;
                
                foreach (var kvp in dateCooldowns)
                {
                    if (currentTick >= kvp.Value)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                foreach (int key in expiredKeys)
                {
                    dateCooldowns.Remove(key);
                }
                
                if (expiredKeys.Count > 0)
                {
                    SLog.Message(string.Format("[SocialInteractions] Cleaned up {0} expired date cooldowns", expiredKeys.Count));
                }
            }
        }

        public static void CheckForStuckDates(Map map)
        {
            if (map == null || map.mapPawns == null) return;
            
            JobDef dateLovinJobDef = SI_JobDefOf.DateLovin;
            
            // Get a snapshot of all pawns to avoid modification during iteration
            List<Pawn> allPawns = new List<Pawn>(map.mapPawns.AllPawns);
            
            foreach (Pawn pawn in allPawns)
            {
                if (IsOnDate(pawn))
                {
                    Pawn initiator = GetInitiatorOfDateWith(pawn);
                    
                    // Check if the initiator is doing a joy job
                    bool isDoingJoyJob = false;
                    if (initiator != null && initiator.CurJob != null)
                    {
                        // Check if the job is a joy job
                        foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                        {
                            if (joyGiver.jobDef == initiator.CurJob.def)
                            {
                                isDoingJoyJob = true;
                                break;
                            }
                        }
                    }
                    
                    // If we can't find a valid initiator or the initiator is not doing a joy job or DateLovin job, advance the date
                    if (initiator == null || initiator.jobs == null || initiator.CurJob == null || (!isDoingJoyJob && initiator.CurJobDef != dateLovinJobDef))
                    {
                        SLog.Message(string.Format("[SocialInteractions] Found stuck date for pawn {0}, advancing stage.", pawn.Name.ToStringShort));
                        AdvanceDateStage(pawn);
                    }
                }
            }
        }

        public static void AdvanceDateStage(Pawn pawn)
        {
            SLog.Message(string.Format("[SocialInteractions] DatingManager.AdvanceDateStage called for pawn {0}", 
                pawn != null ? pawn.LabelShort : "NULL"));
            
            lock (datesLock)
            {
                if (pawn == null) 
                {
                    SLog.Message("[SocialInteractions] DatingManager.AdvanceDateStage: pawn is null, returning");
                    return;
                }
                
                Date date = GetDateWith_Unlocked(pawn);
                if (date != null)
                {
                    if (date.Stage == DateStage.Finished) 
                    {
                        SLog.Message(string.Format("[SocialInteractions] AdvanceDateStage: Date for {0} and {1} is already finished. No action taken.", 
                            date.Initiator != null ? date.Initiator.LabelShort : "NULL", 
                            date.Partner != null ? date.Partner.LabelShort : "NULL"));
                        return;
                    }

                    date.Stage++;
                    SLog.Message(string.Format("[SocialInteractions] AdvanceDateStage: Advancing date stage for {0} and {1}. New stage: {2}", 
                        date.Initiator != null ? date.Initiator.LabelShort : "NULL", 
                        date.Partner != null ? date.Partner.LabelShort : "NULL", 
                        date.Stage));
                    HandleDateStage(date);
                }
                else
                {
                    SLog.Warning(string.Format("[SocialInteractions] AdvanceDateStage: Called for pawn {0} who is not on a date.", pawn.LabelShort));
                }
            }
        }

        private static void HandleDateStage(Date date)
        {
            SLog.Message(string.Format("[SocialInteractions] HandleDateStage: Handling date stage {0} for {1} and {2}", date.Stage, date.Initiator.LabelShort, date.Partner.LabelShort));
            switch (date.Stage)
            {
                case DateStage.Lovin:
                    TransitionToLovin(date);
                    break;
                case DateStage.Finished:
                    EndDate(date);
                    break;
            }
        }

        private static void TransitionToLovin(Date date)
        {
            SLog.Message(string.Format("[SocialInteractions] TransitionToLovin: Attempting to transition date for {0} and {1} to Lovin stage.", date.Initiator.LabelShort, date.Partner.LabelShort));

            if (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
            {
                SLog.Message(string.Format("[SocialInteractions] TransitionToLovin: Ending partner's ({0}) FollowAndWatch job.", date.Partner.LabelShort));
                date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
            }

            Building_Bed bed = FindSuitableBedForLovin(date.Initiator, date.Partner);
            if (bed != null)
            {
                SLog.Message(string.Format("[SocialInteractions] TransitionToLovin: Found suitable bed {0} at {1}. Assigning lovin' jobs.", bed.LabelShort, bed.Position));

                // End any existing jobs that might interfere
                if (date.Initiator != null && date.Initiator.jobs != null) date.Initiator.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                if (date.Partner != null && date.Partner.jobs != null) date.Partner.jobs.EndCurrentJob(JobCondition.InterruptForced, false);

                // Create jobs without reserving the bed - just use its position
                // This allows spouses to potentially catch them in the act
                Job lovinJobInitiator = JobMaker.MakeJob(SI_JobDefOf.DateLovin, date.Partner, bed.Position);
                date.Initiator.jobs.StartJob(lovinJobInitiator, JobCondition.InterruptForced);
                SLog.Message(string.Format("[SocialInteractions] TransitionToLovin: Started DateLovin job for initiator {0}.", date.Initiator.LabelShort));

                Job lovinJobPartner = JobMaker.MakeJob(SI_JobDefOf.DateLovin, date.Initiator, bed.Position);
                date.Partner.jobs.StartJob(lovinJobPartner, JobCondition.InterruptForced);
                SLog.Message(string.Format("[SocialInteractions] TransitionToLovin: Started DateLovin job for partner {0}.", date.Partner.LabelShort));

                SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, SpeechBubbleManager.GetDateLovinSubject(date.Initiator, date.Partner));
            }
            else
            {
                SLog.Warning(string.Format("[SocialInteractions] TransitionToLovin: No suitable bed found. Ending date for {0} and {1}.", date.Initiator.LabelShort, date.Partner.LabelShort));
                date.Stage = DateStage.Finished;
                HandleDateStage(date);
            }
        }

        public static float CalculateDateCompatibility(Pawn pawn1, Pawn pawn2)
        {
            // Start with a base compatibility factor
            float compatibility = 1.0f;

            // Check sexual compatibility first - this is a make-or-break factor
            float sexualCompatibility = CalculateSexualCompatibility(pawn1, pawn2);
            if (sexualCompatibility <= 0f)
            {
                // If they're not sexually compatible, they won't progress to lovin'
                return 0f;
            }
            
            // Apply sexual compatibility as a multiplier
            compatibility *= sexualCompatibility;

            // Add opinion-based compatibility (mutual respect is important for dates)
            float opinion1 = pawn1.relations.OpinionOf(pawn2);
            float opinion2 = pawn2.relations.OpinionOf(pawn1);
            float opinionCompatibility = Mathf.InverseLerp(-100f, 100f, (opinion1 + opinion2) / 2f);
            compatibility *= Mathf.Lerp(0.5f, 1.5f, opinionCompatibility);

            // Consider personality trait compatibility
            if (pawn1.story != null && pawn1.story.traits != null && pawn2.story != null && pawn2.story.traits != null)
            {
                // Positive traits that might increase compatibility
                int positiveTraitMatches = 0;
                int negativeTraitConflicts = 0;

                // Check for matching positive traits
                if (pawn1.story.traits.HasTrait(TraitDefOf.Kind) && pawn2.story.traits.HasTrait(TraitDefOf.Kind))
                    positiveTraitMatches++;
                if (pawn1.story.traits.HasTrait(TraitDefOf.Joyous) && pawn2.story.traits.HasTrait(TraitDefOf.Joyous))
                    positiveTraitMatches++;

                // Check for conflicting traits
                if (pawn1.story.traits.HasTrait(TraitDefOf.Kind) && pawn2.story.traits.HasTrait(TraitDefOf.Psychopath))
                    negativeTraitConflicts++;
                if (pawn1.story.traits.HasTrait(TraitDefOf.Psychopath) && pawn2.story.traits.HasTrait(TraitDefOf.Kind))
                    negativeTraitConflicts++;
                if (pawn1.story.traits.HasTrait(TraitDefOf.Brawler) && pawn2.story.traits.HasTrait(TraitDefOf.Wimp))
                    negativeTraitConflicts++;

                // Adjust compatibility based on trait matches/conflicts
                compatibility *= (1.0f + positiveTraitMatches * 0.1f); // Up to 1.2x for positive matches
                compatibility *= (1.0f - negativeTraitConflicts * 0.15f); // Down to 0.7x for conflicts
            }

            // Consider age compatibility (similar ages might be more compatible for dates)
            float ageDifference = Math.Abs(pawn1.ageTracker.AgeBiologicalYearsFloat - pawn2.ageTracker.AgeBiologicalYearsFloat);
            float ageCompatibility = Mathf.InverseLerp(20f, 0f, ageDifference); // More compatible with less age difference
            compatibility *= Mathf.Lerp(0.8f, 1.2f, ageCompatibility);

            // Ensure compatibility stays within reasonable bounds
            compatibility = Mathf.Clamp(compatibility, 0.1f, 3.0f);

            return compatibility;
        }

        // Helper method to calculate sexual compatibility based on vanilla RimWorld logic
        public static float CalculateSexualCompatibility(Pawn pawn1, Pawn pawn2)
        {
            // Check if they're the same pawn or different species
            if (pawn1.def != pawn2.def || pawn1 == pawn2)
            {
                return 0f;
            }

            // Check traits for sexual compatibility
            if (pawn1.story != null && pawn1.story.traits != null && pawn2.story != null && pawn2.story.traits != null)
            {
                // Asexual pawns won't engage in lovin'
                if (pawn1.story.traits.HasTrait(TraitDefOf.Asexual) || pawn2.story.traits.HasTrait(TraitDefOf.Asexual))
                {
                    return 0f;
                }

                // Gender compatibility based on traits
                if (!pawn1.story.traits.HasTrait(TraitDefOf.Bisexual) && !pawn2.story.traits.HasTrait(TraitDefOf.Bisexual))
                {
                    // Neither is bisexual, so check if they're compatible
                    if (pawn1.story.traits.HasTrait(TraitDefOf.Gay) && pawn2.story.traits.HasTrait(TraitDefOf.Gay))
                    {
                        // Both are gay, they need to be the same gender
                        if (pawn1.gender != pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                    else if (pawn1.story.traits.HasTrait(TraitDefOf.Gay))
                    {
                        // pawn1 is gay, pawn2 needs to be the same gender
                        if (pawn2.gender != pawn1.gender)
                        {
                            return 0f;
                        }
                    }
                    else if (pawn2.story.traits.HasTrait(TraitDefOf.Gay))
                    {
                        // pawn2 is gay, pawn1 needs to be the same gender
                        if (pawn1.gender != pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                    else
                    {
                        // Both are straight, they need to be different genders
                        if (pawn1.gender == pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                }
            }

            // Age check (both must be at least 16)
			// temprorary disabled for debug
            // if (pawn1.ageTracker.AgeBiologicalYearsFloat < 16f || pawn2.ageTracker.AgeBiologicalYearsFloat < 16f)
            // {
            //     return 0f;
            // }

             // If all checks pass, return a positive compatibility factor based on attractiveness
            float pawn1Attractiveness = CalculateAttractiveness(pawn1, pawn2);
            float pawn2Attractiveness = CalculateAttractiveness(pawn2, pawn1);
            
            // Average the attractiveness factors
            return (pawn1Attractiveness + pawn2Attractiveness) / 2f;
        }

        // Helper method to calculate attractiveness factor (similar to vanilla PrettinessFactor)
        public static float CalculateAttractiveness(Pawn observer, Pawn target)
        {
            float beauty = 0f;
            if (target.RaceProps.Humanlike)
            {
                beauty = target.GetStatValue(StatDefOf.PawnBeauty);
            }

            if (beauty < 0f)
            {
                return 0.3f; // Unattractive
            }
            else if (beauty > 0f)
            {
                return 1f + beauty; // Attractive
            }
            else
            {
                return 1.0f; // Average
            }
        }

        // Helper method to find a suitable bed for lovin'
        public static Building_Bed FindSuitableBedForLovin(Pawn initiator, Pawn partner)
        {
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Searching for bed for {0} and {1}.", initiator.LabelShort, partner.LabelShort));
            if (initiator == null || initiator.Map == null || partner == null) 
            {
                SLog.Error("[SocialInteractions] FindSuitableBedForLovin: Initiator, Partner, or Map is null.");
                return null;
            }

            // Social compatibility and probability check
            float baseChance = SocialInteractions.Settings.baseLovinChance;
            float moodFactor = (initiator.needs.mood.CurLevel + partner.needs.mood.CurLevel) / 2f;
            float dateCompatibility = CalculateDateCompatibility(initiator, partner);
            float finalChance = baseChance * moodFactor * dateCompatibility;
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Lovin chance calculation: base({0}) * mood({1}) * compatibility({2}) = {3}", baseChance, moodFactor, dateCompatibility, finalChance));

            if (!Rand.Chance(finalChance))
            {
                SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Lovin chance failed. Rolled > {0}.", finalChance));
                return null;
            }
            SLog.Message("[SocialInteractions] FindSuitableBedForLovin: Lovin chance succeeded.");

            var allBeds = initiator.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().ToList();
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Found {0} colonist beds on map.", allBeds.Count));

            var potentialBeds = allBeds.Where(bed =>
                {
                    if (bed == null || bed.Destroyed || !bed.Spawned) return false;
                    // Remove the restriction on bed size since we're not actually laying in bed
                    // if (bed.SleepingSlotsCount < 2) { /*SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Bed {0} has < 2 slots.", bed.LabelShort));*/ return false; }
                    if (!initiator.CanReserveAndReach(bed, PathEndMode.InteractionCell, Danger.None)) { /*SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Initiator cannot reserve/reach {0}.", bed.LabelShort));*/ return false; }
                    if (!partner.CanReserveAndReach(bed, PathEndMode.InteractionCell, Danger.None)) { /*SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Partner cannot reserve/reach {0}.", bed.LabelShort));*/ return false; }
                    return true;
                });
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Found {0} beds that are reachable.", potentialBeds.Count()));

            // Prioritize owned beds
            Building_Bed ownedBed = potentialBeds.FirstOrDefault(b => b.OwnersForReading.Contains(initiator));
            if (ownedBed != null) 
            {
                SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Found bed owned by initiator: {0}.", ownedBed.LabelShort));
                return ownedBed;
            }

            // Fallback to any bed
            Building_Bed anyBed = potentialBeds.FirstOrDefault();
            if (anyBed != null) 
            {
                SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Found available bed: {0}.", anyBed.LabelShort));
                return anyBed;
            }

            SLog.Warning("[SocialInteractions] FindSuitableBedForLovin: No suitable bed found after all checks.");
            return null;
        }
    }
}