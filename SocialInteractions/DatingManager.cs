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
        private const int DateCooldownTicks = 300; // 5 min

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
                int expiryTick = Find.TickManager.TicksGame + DateCooldownTicks;
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
                int expiryTick = Find.TickManager.TicksGame + DateCooldownTicks;
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

        public static Pawn GetPartnerOnDateWith(Pawn pawn)
        {
            lock (datesLock)
            {
                if (pawn == null) return null;
                Date date = GetDateWith(pawn);
                if (date != null)
                {
                    return date.Initiator == pawn ? date.Partner : date.Initiator;
                }
                return null;
            }
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
            
            JobDef goOnDateJobDef = DefDatabase<JobDef>.GetNamed("GoOnDate", false);
            // If we can't find the job definition, skip this check
            if (goOnDateJobDef == null) return;
            
            // Get a snapshot of all pawns to avoid modification during iteration
            List<Pawn> allPawns = new List<Pawn>(map.mapPawns.AllPawns);
            
            foreach (Pawn pawn in allPawns)
            {
                if (IsOnDate(pawn))
                {
                    Pawn initiator = GetInitiatorOfDateWith(pawn);
                    // If we can't find a valid initiator or the initiator is not doing the GoOnDate job, advance the date
                    if (initiator == null || initiator.jobs == null || initiator.CurJob == null || (initiator.CurJobDef != goOnDateJobDef && initiator.CurJobDef != SI_JobDefOf.DateLovin))
                    {
                        SLog.Message(string.Format("[SocialInteractions] Found stuck date for pawn {0}, advancing stage.", pawn.Name.ToStringShort));
                        AdvanceDateStage(pawn);
                    }
                }
            }
        }

        public static void AdvanceDateStage(Pawn pawn)
        {
            lock (datesLock)
            {
                if (pawn == null) return;
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

                SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, "");
            }
            else
            {
                SLog.Warning(string.Format("[SocialInteractions] TransitionToLovin: No suitable bed found. Ending date for {0} and {1}.", date.Initiator.LabelShort, date.Partner.LabelShort));
                date.Stage = DateStage.Finished;
                HandleDateStage(date);
            }
        }

        private static Building_Bed FindSuitableBedForLovin(Pawn initiator, Pawn partner)
        {
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Searching for bed for {0} and {1}.", initiator.LabelShort, partner.LabelShort));
            if (initiator == null || initiator.Map == null || partner == null) 
            {
                SLog.Error("[SocialInteractions] FindSuitableBedForLovin: Initiator, Partner, or Map is null.");
                return null;
            }

            // Social compatibility and probability check
            float baseChance = 0.75f;
            float opinionFactor = Mathf.InverseLerp(-100f, 100f, initiator.relations.OpinionOf(partner)) * Mathf.InverseLerp(-100f, 100f, partner.relations.OpinionOf(initiator));
            float moodFactor = (initiator.needs.mood.CurLevel + partner.needs.mood.CurLevel) / 2f;
            float slcFactor = initiator.relations.SecondaryLovinChanceFactor(partner) * partner.relations.SecondaryLovinChanceFactor(initiator);
            float finalChance = baseChance * ((opinionFactor + moodFactor) / 2f) * slcFactor;
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Lovin chance calculation: base({0}) * (opinion({1}) + mood({2}))/2 * slc({3}) = {4}", baseChance, opinionFactor, moodFactor, slcFactor, finalChance));

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
            SLog.Message(string.Format("[SocialInteractions] FindSuitableBedForLovin: Found {0} beds that are reachable and have enough slots.", potentialBeds.Count()));

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

        public static List<Tuple<Thing, JoyGiverDef, IntVec3>> FindJoySpotFor(Pawn pawn, Pawn partner)
        {
            if (pawn == null || partner == null) return new List<Tuple<Thing, JoyGiverDef, IntVec3>>();
            SLog.Message(string.Format("[SocialInteractions] DatingManager.FindJoySpotFor called for {0} and {1}.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
            List<Tuple<Thing, JoyGiverDef, IntVec3>> foundSpots = new List<Tuple<Thing, JoyGiverDef, IntVec3>>();

            // 1. Filter for suitable social JoyGiverDefs
            List<JoyGiverDef> suitableJoyGivers = new List<JoyGiverDef>();
            try
            {
                // Convert to list first to avoid multiple enumerations
                List<JoyGiverDef> allJoyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading;
                
                foreach (JoyGiverDef jg in allJoyGivers)
                {
                    if (jg.jobDef == null)
                    {
                        continue;
                    }
                    if (jg.jobDef == JobDefOf.Lovin) { continue; }
                    if (jg.jobDef.defName == "VisitSickPawn") { continue; }
                    if (jg.jobDef.defName == "StandAndChat") { continue; }
                    if (jg.thingDefs == null || !jg.thingDefs.Any()) { continue; }
                    
                    if (jg.Worker == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (!jg.Worker.CanBeGivenTo(pawn)) { continue; }
                    }
                    catch (Exception ex)
                    {
                        SLog.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception checking CanBeGivenTo for initiator on {0}: {1}", jg.defName, ex.Message));
                        continue;
                    }

                    try
                    {
                        if (!jg.Worker.CanBeGivenTo(partner)) { continue; }
                    }
                    catch (Exception ex)
                    {
                        SLog.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception checking CanBeGivenTo for partner on {0}: {1}", jg.defName, ex.Message));
                        continue;
                    }
                    
                    suitableJoyGivers.Add(jg);
                }
            }
            catch (Exception ex)
            {
                SLog.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception during JoyGiverDef filtering: {0}", ex.Message));
            }
            SLog.Message(string.Format("[SocialInteractions] DatingManager.FindJoySpotFor: Found {0} suitable joy givers.", suitableJoyGivers.Count));

            try
            {
                foreach (var giver in suitableJoyGivers)
                {
                    // 2. For each JoyGiverDef, find compatible buildings on the map
                    if (giver.thingDefs != null)
                    {
                        foreach (var thingDef in giver.thingDefs)
                        {
                            if (pawn.Map == null || pawn.Map.listerBuildings == null)
                            {
                                continue;
                            }
                            
                            // Get buildings once and convert to list to avoid multiple enumerations
                            List<Building> allBuildings = pawn.Map.listerBuildings.allBuildingsColonist;
                            
                            foreach (Building building in allBuildings)
                            {
                                // Basic validity checks
                                if (building == null || building.Destroyed || !building.Spawned)
                                {
                                    continue;
                                }
                                
                                // Check if building matches the required thingDef
                                if (building.def != thingDef)
                                {
                                    continue;
                                }
                                
                                // Check if building has positive joy gain factor
                                if (building.def.GetStatValueAbstract(StatDefOf.JoyGainFactor) <= 0)
                                {
                                    continue;
                                }
                                
                                // Check if both pawns can reach and reserve the building
                                if (!pawn.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.None) || 
                                    !partner.CanReserveAndReach(building, PathEndMode.InteractionCell, Danger.None))
                                {
                                    continue;
                                }

                                // Add a robust check for building's position in EdificeGrid
                                try
                                {
                                    // Attempt to access the edifice grid. If this throws, the building is problematic.
                                    Thing edifice = building.Map.edificeGrid[building.Position];
                                    // Only add to foundSpots if the edifice is the expected building and it's spawned.
                                    if (edifice != building || !building.Spawned)
                                    {
                                        SLog.Message(string.Format("[SocialInteractions] FindJoySpotFor: Excluding problematic building {0} at {1}. Edifice mismatch or not spawned. Edifice: {2}, Spawned: {3}", building.LabelShort, building.Position, edifice != null ? edifice.LabelShort : "NULL", building.Spawned));
                                        continue;
                                    }
                                    
                                    // Add explicit reservation check for the building itself
                                    if (!pawn.CanReserve(building) || !partner.CanReserve(building))
                                    {
                                        SLog.Message(string.Format("[SocialInteractions] FindJoySpotFor: Building {0} at {1} cannot be reserved by both pawns. Skipping.", building.LabelShort, building.Position));
                                        continue; // Skip this building if it cannot be reserved
                                    }

                                    // NEW LOGIC: Find an accessible interaction cell
                                    IntVec3 interactionCell = IntVec3.Invalid;

                                    // Prioritize interaction cells defined by the building
                                    IntVec3 potentialCell = building.InteractionCell;
                                    if (potentialCell.IsValid && potentialCell.InBounds(building.Map) && !potentialCell.Impassable(building.Map) &&
                                        pawn.CanReach(potentialCell, PathEndMode.OnCell, Danger.None) &&
                                        partner.CanReach(potentialCell, PathEndMode.OnCell, Danger.None) &&
                                        pawn.CanReserve(potentialCell) && partner.CanReserve(potentialCell))
                                    {
                                        interactionCell = potentialCell;
                                    }

                                    // If no specific interaction cell, try adjacent cells
                                    if (interactionCell == IntVec3.Invalid)
                                    {
                                        foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(building))
                                        {
                                            if (c.IsValid && c.InBounds(building.Map) && !c.Impassable(building.Map) &&
                                                pawn.CanReach(c, PathEndMode.OnCell, Danger.None) &&
                                                partner.CanReach(c, PathEndMode.OnCell, Danger.None) &&
                                                pawn.CanReserve(c) && partner.CanReserve(c))
                                            {
                                                interactionCell = c;
                                                break; // Found a suitable cell, break
                                            }
                                        }
                                    }

                                    if (interactionCell != IntVec3.Invalid)
                                    {
                                        foundSpots.Add(new Tuple<Thing, JoyGiverDef, IntVec3>(building, giver, interactionCell));
                                        SLog.Message(string.Format("[SocialInteractions] FindJoySpotFor: Found joy spot {0} at {1} with interaction cell {2}.", building.LabelShort, building.Position, interactionCell));
                                    }
                                    else
                                    {
                                        SLog.Message(string.Format("[SocialInteractions] FindJoySpotFor: No suitable interaction cell found for building {0} at {1}.", building.LabelShort, building.Position));
                                    }
                                }
                                catch (IndexOutOfRangeException ex)
                                {
                                    SLog.Error(string.Format("[SocialInteractions] FindJoySpotFor: Excluding problematic building {0} at {1} due to IndexOutOfRangeException in EdificeGrid: {2}", building.LabelShort, building.Position, ex.Message));
                                }
                                catch (Exception ex) // Catch other potential exceptions during access
                                {
                                    SLog.Error(string.Format("[SocialInteractions] FindJoySpotFor: Excluding problematic building {0} at {1} due to unexpected exception during EdificeGrid access: {2}", building.LabelShort, building.Position, ex.Message));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SLog.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception during iteration through suitable JoyGiverDefs: {0}", ex.Message));
            }
            
            SLog.Message(string.Format("[SocialInteractions] DatingManager.FindJoySpotFor: Returning {0} joy spots.", foundSpots.Count));
            return foundSpots;
        }

        
    }
}