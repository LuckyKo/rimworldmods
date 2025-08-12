using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;

namespace SocialInteractions
{
    public enum DateStage
    {
        Joy,
        Lovin,
        Finished
    }

    public class Date
    {
        public Pawn Initiator;
        public Pawn Partner;
        public DateStage Stage;

        public Date(Pawn initiator, Pawn partner)
        {
            this.Initiator = initiator;
            this.Partner = partner;
            this.Stage = DateStage.Joy;
        }
    }

    public static class DatingManager
    {
        private static List<Date> dates = new List<Date>();
        private static readonly object datesLock = new object();
        private static Dictionary<Pawn, int> dateCooldowns = new Dictionary<Pawn, int>();
        private const int DateCooldownTicks = 300; // 5 min

        public static void StartDate(Pawn initiator, Pawn partner)
        {
            lock (datesLock)
            {
                if (initiator == null || partner == null) return;
                Log.Message(string.Format("[SocialInteractions] DatingManager.StartDate called for Initiator: {0}, Partner: {1}", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                if (!IsOnDate(initiator) && !IsOnDate(partner))
                {
                    Log.Message(string.Format("[SocialInteractions] Starting date between {0} and {1}.", initiator.Name.ToStringShort, partner.Name.ToStringShort));
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
                    Log.Message(string.Format("[SocialInteractions] DatingManager.StartDate: Not starting date because one or both pawns are already on a date. Initiator: {0} (OnDate: {1}), Partner: {2} (OnDate: {3})", initiator.Name.ToStringShort, IsOnDate(initiator), partner.Name.ToStringShort, IsOnDate(partner)));
                }
            }
        }

        public static void EndDate(Pawn pawn)
        {
            lock (datesLock)
            {
                if (pawn == null) return;
                Log.Message(string.Format("[SocialInteractions] DatingManager.EndDate called for Pawn: {0}", pawn.Name.ToStringShort));
                Date date = GetDateWith(pawn);
                if (date != null)
                {
                    Log.Message(string.Format("[SocialInteractions] Ending date for {0} and {1}. Removing OnDate hediffs.", date.Initiator.Name.ToStringShort, date.Partner.Name.ToStringShort));

                    int expiryTick = Find.TickManager.TicksGame + DateCooldownTicks;
                    Log.Message(string.Format("[SocialInteractions] Adding date cooldown for {0} and {1} until tick {2}.", date.Initiator.LabelShort, date.Partner.LabelShort, expiryTick));
                    dateCooldowns[date.Initiator] = expiryTick;
                    dateCooldowns[date.Partner] = expiryTick;

                    // Remove OnDate hediff from initiator
                    Hediff hediffInitiator = null;
                    if (date.Initiator.health != null && date.Initiator.health.hediffSet != null)
                    {
                        hediffInitiator = date.Initiator.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                    }
                    if (hediffInitiator != null)
                    {
                        date.Initiator.health.RemoveHediff(hediffInitiator);
                    }

                    // Remove OnDate hediff from partner
                    Hediff hediffPartner = null;
                    if (date.Partner.health != null && date.Partner.health.hediffSet != null)
                    {
                        hediffPartner = date.Partner.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                    }
                    if (hediffPartner != null)
                    {
                        date.Partner.health.RemoveHediff(hediffPartner);
                    }

                    dates.RemoveAll(d => d.Initiator == pawn || d.Partner == pawn);
                }
            }
        }

        private static Date GetDateWith_Unlocked(Pawn pawn)
        {
            return dates.FirstOrDefault(d => d.Initiator == pawn || d.Partner == pawn);
        }

        public static bool IsOnDate(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null) return false;
            HediffDef onDateDef = HediffDef.Named("OnDate");
            if (onDateDef == null) return false;
            return pawn.health.hediffSet.HasHediff(onDateDef);
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
            int expiryTick;
            if (dateCooldowns.TryGetValue(pawn, out expiryTick))
            {
                bool onCooldown = Find.TickManager.TicksGame < expiryTick;
                Log.Message(string.Format("[SocialInteractions] IsOnDateCooldown check for {0}: Found expiry tick {1}. Current tick: {2}. On cooldown: {3}", pawn.LabelShort, expiryTick, Find.TickManager.TicksGame, onCooldown));
                if (onCooldown)
                {
                    return true;
                }
                else
                {
                    dateCooldowns.Remove(pawn);
                    return false;
                }
            }
            return false;
        }

        public static void AdvanceDateStage(Pawn pawn)
        {
            lock (datesLock)
            {
                if (pawn == null) return;
                Log.Message(string.Format("[SocialInteractions] DatingManager.AdvanceDateStage called for Pawn: {0}", pawn.Name.ToStringShort));
                Date date = GetDateWith(pawn);
                if (date != null && date.Initiator != null && date.Partner != null)
                {
                    Log.Message(string.Format("[SocialInteractions] Advancing date stage for {0} and {1}. Current stage: {2}", date.Initiator.Name.ToStringShort, date.Partner.Name.ToStringShort, date.Stage));
                    if (date.Stage == DateStage.Joy)
                    {
                        Log.Message("[SocialInteractions] Transitioning from Joy to Lovin stage.");
                        // End the partner's FollowAndWatch job.
                        if (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
                        {
                            date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
                        }

                        // Attempt to transition to the lovin' stage.
                        bool canLovin = CanHaveLovin(date.Initiator, date.Partner);
                        Log.Message(string.Format("[SocialInteractions] DatingManager.CanHaveLovin returned: {0}", canLovin));
                        if (canLovin)
                        {
                            Log.Message("[SocialInteractions] Conditions for lovin' met. Assigning lovin' job.");
                            date.Stage = DateStage.Lovin;
                            Building_Bed bed = (date.Initiator.ownership != null) ? date.Initiator.ownership.OwnedBed : null;
                            if (bed == null || bed.SleepingSlotsCount < 2)
                            {
                                bed = (date.Partner.ownership != null) ? date.Partner.ownership.OwnedBed : null;
                            }

                            if (bed == null || bed.SleepingSlotsCount < 2)
                            {
                                bed = RestUtility.FindBedFor(date.Initiator, date.Partner, checkSocialProperness: false, ignoreOtherReservations: false);
                            }

                            // End any existing lovin' jobs for initiator and partner
                            if (date.Initiator != null && date.Initiator.jobs != null && date.Initiator.CurJobDef == JobDefOf.Lovin) date.Initiator.jobs.EndCurrentJob(JobCondition.Succeeded);
                            if (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJobDef == JobDefOf.Lovin) date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);

                            if (bed != null)
                            {
                                Job lovinJob = JobMaker.MakeJob(SI_JobDefOf.DateLovin, date.Partner, bed);
                                Log.Message(string.Format("[SocialInteractions] Initiator Lovin Job created: {0}", lovinJob));
                                date.Initiator.jobs.StartJob(lovinJob, JobCondition.InterruptForced);
                                Job lovinJobPartner = JobMaker.MakeJob(SI_JobDefOf.DateLovin, date.Initiator, bed);
                                Log.Message(string.Format("[SocialInteractions] Partner Lovin Job created: {0}", lovinJobPartner));
                                date.Partner.jobs.StartJob(lovinJobPartner, JobCondition.InterruptForced);
                            }
                            else
                            {
                                Log.Message("[SocialInteractions] No suitable bed found for lovin'. Ending date.");
                                date.Stage = DateStage.Finished;
                                EndDate(pawn);
                                return;
                            }

                            string subject = SpeechBubbleManager.GetDateEndSubject(date.Initiator, date.Partner);
                            SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, subject);
                        }
                        else
                        {
                            Log.Message("[SocialInteractions] Conditions for lovin' not met. Ending date.");
                            date.Stage = DateStage.Finished;
                            EndDate(pawn);
                        }
                    }
                    else if (date.Stage == DateStage.Lovin)
                    {
                        Log.Message("[SocialInteractions] Lovin' stage finished. Ending date.");
                        // After lovin', the date is finished.
                        date.Stage = DateStage.Finished;
                        EndDate(pawn);
                    }
                }
            }
        }

        private static bool CanHaveLovin(Pawn initiator, Pawn partner)
        {
            if (initiator == null || partner == null) return false;

            if (initiator.ownership == null || partner.ownership == null) return false;

            // Bed check
            Building_Bed bed = initiator.ownership.OwnedBed;
            if (bed == null || bed.SleepingSlotsCount < 2)
            {
                bed = partner.ownership.OwnedBed;
            }
            if (bed == null || bed.SleepingSlotsCount < 2)
            {
                bed = RestUtility.FindBedFor(initiator, partner, checkSocialProperness: false, ignoreOtherReservations: false);
            }
            if (bed == null)
            {
                Log.Message(string.Format("[SocialInteractions] CanHaveLovin: No suitable bed found for {0} and {1}.", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                return false;
            }
            Log.Message(string.Format("[SocialInteractions] CanHaveLovin: Suitable bed found for {0} and {1}. Bed: {2}", initiator.Name.ToStringShort, partner.Name.ToStringShort, bed.LabelShort));

            // Probability check
            float baseChance = 0.75f;

            // Opinion factor
            if (initiator.relations == null || partner.relations == null) return false;
            float opinionFactor = UnityEngine.Mathf.InverseLerp(-100f, 100f, initiator.relations.OpinionOf(partner));
            opinionFactor *= UnityEngine.Mathf.InverseLerp(-100f, 100f, partner.relations.OpinionOf(initiator));
            Log.Message(string.Format("[SocialInteractions] CanHaveLovin: Opinion Factor for {0} and {1}: {2}", initiator.Name.ToStringShort, partner.Name.ToStringShort, opinionFactor));

            // Mood factor
            if (initiator.needs == null || initiator.needs.mood == null || partner.needs == null || partner.needs.mood == null) return false;
            float moodFactor = (initiator.needs.mood.CurLevel + partner.needs.mood.CurLevel) / 2f;
            Log.Message(string.Format("[SocialInteractions] CanHaveLovin: Mood Factor for {0} and {1}: {2}", initiator.Name.ToStringShort, partner.Name.ToStringShort, moodFactor));

            // Secondary Lovin Chance Factor
            float slcFactor = initiator.relations.SecondaryLovinChanceFactor(partner);
            slcFactor *= partner.relations.SecondaryLovinChanceFactor(initiator);
            Log.Message(string.Format("[SocialInteractions] CanHaveLovin: Secondary Lovin Chance Factor for {0} and {1}: {2}", initiator.Name.ToStringShort, partner.Name.ToStringShort, slcFactor));


            float finalChance = baseChance * opinionFactor * moodFactor * slcFactor;
            Log.Message(string.Format("[SocialInteractions] CanHaveLovin: Final Chance for {0} and {1}: {2}", initiator.Name.ToStringShort, partner.Name.ToStringShort, finalChance));

            return Rand.Value < finalChance;
        }

        public static List<Tuple<Thing, JoyGiverDef, IntVec3>> FindJoySpotFor(Pawn pawn, Pawn partner)
        {
            if (pawn == null || partner == null) return new List<Tuple<Thing, JoyGiverDef, IntVec3>>();
            List<Tuple<Thing, JoyGiverDef, IntVec3>> foundSpots = new List<Tuple<Thing, JoyGiverDef, IntVec3>>();

            // 1. Filter for suitable social JoyGiverDefs
            List<JoyGiverDef> suitableJoyGivers = new List<JoyGiverDef>();
            try
            {
                suitableJoyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                    .Where(jg =>
                    {
                        if (jg.jobDef == null)
                        {
                            return false;
                        }
                        if (jg.jobDef == JobDefOf.Lovin) { return false; }
                        if (jg.jobDef.defName == "VisitSickPawn") { return false; }
                        if (jg.jobDef.defName == "StandAndChat") { return false; }
                        if (jg.thingDefs == null || !jg.thingDefs.Any()) { return false; }
                        
                        if (jg.Worker == null)
                        {
                            return false;
                        }

                        try
                        {
                            if (!jg.Worker.CanBeGivenTo(pawn)) { return false; }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception checking CanBeGivenTo for initiator on {0}: {1}", jg.defName, ex.Message));
                            return false;
                        }

                        try
                        {
                            if (!jg.Worker.CanBeGivenTo(partner)) { return false; }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception checking CanBeGivenTo for partner on {0}: {1}", jg.defName, ex.Message));
                            return false;
                        }
                        return true;
                    }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception during JoyGiverDef filtering query definition: {0}", ex.Message));
            }

            try
            {
                foreach (var giver in suitableJoyGivers)
                {
                    // 2. For each JoyGiverDef, find compatible buildings on the map
                    if (giver.thingDefs != null)
                    {
                        foreach (var thingDef in giver.thingDefs)
                        {
                            IEnumerable<Building> potentialBuildings = null;
                            if (pawn.Map != null && pawn.Map.listerBuildings != null)
                            {
                                potentialBuildings = pawn.Map.listerBuildings.allBuildingsColonist
                                    .Where(b =>
                                        b != null && // Ensure building is not null
                                        b.def == thingDef && // Must be the specific ThingDef for this giver
                                        b.def.GetStatValueAbstract(StatDefOf.JoyGainFactor) > 0 && // Must provide joy
                                        pawn.CanReserveAndReach(b, PathEndMode.InteractionCell, Danger.None) && // Initiator can reserve and reach
                                        partner.CanReserveAndReach(b, PathEndMode.InteractionCell, Danger.None) // Partner can reserve and reach
                                    );
                            }

                            if (potentialBuildings != null)
                            {
                                foreach (var building in potentialBuildings)
                                {
                                    // Add a robust check for building's position in EdificeGrid
                                    try
                                    {
                                        // Attempt to access the edifice grid. If this throws, the building is problematic.
                                        Thing edifice = building.Map.edificeGrid[building.Position];
                                        // If we reach here, it means the access didn't throw.
                                        // We can optionally check if edifice is null or not the expected building, but the primary goal is to catch the IndexOutOfRangeException.
                                        // Only add to foundSpots if the edifice is the expected building and it's spawned.
                                        if (edifice == building && building.Spawned)
                                        {
                                            // Add explicit reservation check for the building itself
                                            if (!pawn.CanReserve(building) || !partner.CanReserve(building))
                                            {
                                                Log.Message(string.Format("[SocialInteractions] FindJoySpotFor: Building {0} at {1} cannot be reserved by both pawns. Skipping.", building.LabelShort, building.Position));
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
                                            }
                                            else
                                            {
                                                Log.Message(string.Format("[SocialInteractions] FindJoySpotFor: No suitable interaction cell found for building {0} at {1}.", building.LabelShort, building.Position));
                                            }
                                        }
                                        else
                                        {
                                            Log.Message(string.Format("[SocialInteractions] FindJoySpotFor: Excluding problematic building {0} at {1}. Edifice mismatch or not spawned. Edifice: {2}, Spawned: {3}", building.LabelShort, building.Position, edifice != null ? edifice.LabelShort : "NULL", building.Spawned));
                                        }
                                    }
                                    catch (IndexOutOfRangeException ex)
                                    {
                                        Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Excluding problematic building {0} at {1} due to IndexOutOfRangeException in EdificeGrid: {2}", building.LabelShort, building.Position, ex.Message));
                                    }
                                    catch (Exception ex) // Catch other potential exceptions during access
                                    {
                                        Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Excluding problematic building {0} at {1} due to unexpected exception during EdificeGrid access: {2}", building.LabelShort, building.Position, ex.Message));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[SocialInteractions] FindJoySpotFor: Exception during iteration through suitable JoyGiverDefs: {0}", ex.Message));
            }
            
            return foundSpots;
        }

        
    }
}
