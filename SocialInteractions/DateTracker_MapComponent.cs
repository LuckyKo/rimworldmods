using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class DateTracker_MapComponent : MapComponent
    {
        private int lastCleanupTick = 0;
        private const int CleanupInterval = 1800; // 30 seconds

        public DateTracker_MapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            DatingManager.ExposeData();
            Scribe_Values.Look(ref lastCleanupTick, "lastCleanupTick", 0);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Check every second
            if (Find.TickManager.TicksGame % SocialInteractions.Settings.jobCheckIntervalTicks == 0)
            {
                // Check for stuck dates
                DatingManager.CheckForStuckDates(this.map);
                
                // Create a snapshot of all pawns to avoid collection modification during iteration
                List<Pawn> allPawns = new List<Pawn>(this.map.mapPawns.AllPawns);
                
                foreach (Pawn pawn in allPawns)
                {
                    if (DatingManager.IsOnDate(pawn))
                    {
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        JobDef dateLovinJobDef = SI_JobDefOf.DateLovin;
                        
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
                        
                        if (initiator != null && !isDoingJoyJob && initiator.CurJobDef != dateLovinJobDef)
                        {
                            // Initiator is no longer doing a joy job or DateLovin job, so advance the date stage
                            DatingManager.AdvanceDateStage(pawn);
                        }
                        // Additional check: if either pawn is dead or downed, end the date
                        else if (pawn.Dead || pawn.Downed || (initiator != null && (initiator.Dead || initiator.Downed)))
                        {
                            DatingManager.EndDate(DatingManager.GetDateWith(pawn));
                        }
                    }
                }
            }

            // Periodically clean up expired date cooldowns
            if (Find.TickManager.TicksGame - lastCleanupTick >= CleanupInterval)
            {
                DatingManager.CleanupExpiredDateCooldowns();
                lastCleanupTick = Find.TickManager.TicksGame;
            }
        }
    }
}