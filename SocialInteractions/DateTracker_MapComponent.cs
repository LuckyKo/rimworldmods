using RimWorld;
using Verse;
using Verse.AI;
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
                // Debug logging to see if we're entering this method
                //SLog.Message("[SocialInteractions] DateTracker: MapComponentTick called.");
                
                // Check for stuck dates
                DatingManager.CheckForStuckDates(this.map);
                
                // Create a snapshot of all pawns to avoid collection modification during iteration
                List<Pawn> allPawns = new List<Pawn>(this.map.mapPawns.AllPawns);
                
                // Debug logging to see how many pawns we're checking
                //SLog.Message(string.Format("[SocialInteractions] DateTracker: Checking {0} pawns for dates.", allPawns.Count));
                
                foreach (Pawn pawn in allPawns)
                {
                    if (DatingManager.IsOnDate(pawn))
                    {
                        // Debug logging to see which pawns are on a date
                        //SLog.Message(string.Format("[SocialInteractions] DateTracker: Pawn {0} is on a date.", pawn.LabelShort));
                        
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        Pawn partner = DatingManager.GetPartnerOfDateWith(pawn);
                        JobDef dateLovinJobDef = SI_JobDefOf.DateLovin;
                        
                        // Check if the initiator is doing a joy job
                        bool isDoingJoyJob = false;
                        JobDef initiatorJoyJobDef = null;
                        if (initiator != null && initiator.CurJob != null)
                        {
                            // Check if the job is a joy job
                            foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                            {
                                if (joyGiver.jobDef == initiator.CurJob.def)
                                {
                                    isDoingJoyJob = true;
                                    initiatorJoyJobDef = initiator.CurJob.def;
                                    break;
                                }
                            }
                        }
                        
                        // Check if the initiator's joy need is satisfied
                        if (initiator != null && initiator.needs != null && initiator.needs.joy != null && 
                            initiator.needs.joy.CurLevelPercentage >= 0.95f)
                        {
                            // Check if the date is already in the Lovin stage or beyond
                            Date dateStatus = DatingManager.GetDateWith(initiator);
                            if (dateStatus != null && dateStatus.Stage < DateStage.Lovin)
                            {
                                // Initiator's joy need is satisfied, advance to Lovin stage
                                SLog.Message(string.Format("[SocialInteractions] DateTracker: Initiator {0} joy need is satisfied ({1:P}), advancing to Lovin stage.", 
                                    initiator.LabelShort, initiator.needs.joy.CurLevelPercentage));
                                DatingManager.AdvanceDateStage(initiator);
                            }
                        }
                        else if (initiator != null && !isDoingJoyJob && initiator.CurJobDef != dateLovinJobDef)
                        {
                            // Initiator is no longer doing a joy job or DateLovin job, so advance the date stage
                            SLog.Message(string.Format("[SocialInteractions] DateTracker: Initiator {0} is no longer doing a joy job or DateLovin job, advancing date stage.", 
                                initiator.LabelShort));
                            DatingManager.AdvanceDateStage(pawn);
                        }
                        else if (initiator != null && isDoingJoyJob && initiatorJoyJobDef != null)
                        {
                            // Initiator is doing a joy job, check if the partner should join in or continue with their current activity
                            HandlePartnerJoyActivity(initiator, partner, initiatorJoyJobDef);
                        }
                        // Additional check: if either pawn is dead or downed, end the date
                        else if (pawn.Dead || pawn.Downed || (initiator != null && (initiator.Dead || initiator.Downed)))
                        {
                            DatingManager.EndDate(DatingManager.GetDateWith(pawn));
                        }
                        // Additional check: if the partner is no longer in a valid date job, end the date
                        else if (partner != null)
                        {
                            // Check if the partner is in a valid date job
                            bool isInValidDateJob = false;
                            
                            // Partner should be in FollowAndWatch job, a joy job, or DateLovin job
                            if (partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
                            {
                                isInValidDateJob = true;
                            }
                            else if (partner.CurJobDef == SI_JobDefOf.DateLovin)
                            {
                                isInValidDateJob = true;
                            }
                            else if (partner.CurJob != null)
                            {
                                // Check if the job is a joy job
                                foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                                {
                                    if (joyGiver.jobDef == partner.CurJob.def)
                                    {
                                        isInValidDateJob = true;
                                        break;
                                    }
                                }
                            }
                            
                            // If the partner is not in a valid date job, end the date
                            if (!isInValidDateJob)
                            {
                                SLog.Message(string.Format("[SocialInteractions] DateTracker: Partner {0} is no longer in a valid date job, ending date.", partner.LabelShort));
                                DatingManager.EndDate(DatingManager.GetDateWith(initiator));
                            }
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

        private void HandlePartnerJoyActivity(Pawn initiator, Pawn partner, JobDef joyJobDef)
        {
            // Check if we have valid pawns
            if (initiator == null || partner == null || joyJobDef == null)
            {
                return;
            }
            
            // Check if the partner is already doing the same joy job
            if (partner.CurJobDef == joyJobDef)
            {
                // Partner is already doing the joy job, let them continue
                // Check if they've gained enough joy and should go back to following
                if (partner.needs != null && partner.needs.joy != null && 
                    partner.needs.joy.CurLevelPercentage >= 0.99f)
                {
                    // Partner's joy need is satisfied, interrupt their joy job to go back to following
                    SLog.Message(string.Format("[SocialInteractions] DateTracker: Partner {0} joy need is satisfied ({1:P}), going back to following {2}.", 
                        partner.LabelShort, partner.needs.joy.CurLevelPercentage, initiator.LabelShort));
                    partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    
                    // Start the FollowAndWatch job for the partner
                    Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator);
                    partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                }
                return;
            }
            
            // Check if the partner is doing a DateLovin job
            if (partner.CurJobDef == SI_JobDefOf.DateLovin)
            {
                return;
            }
            
            // Check if the partner is doing the FollowAndWatch job
            if (partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
            {
                // Partner is following, check if they should join the joy activity
                // Check if the partner's joy need is high enough that they don't want to join
                if (partner.needs != null && partner.needs.joy != null && 
                    partner.needs.joy.CurLevelPercentage >= 0.95f)
                {
                    SLog.Message(string.Format("[SocialInteractions] DateTracker: Partner {0}'s joy level is too high to join activity", 
                        partner.LabelShort));
                    return;
                }
                
                // Try to have the partner join the joy activity
                TryHavePartnerJoinJoyActivity(initiator, partner, joyJobDef);
                return;
            }
            
            // If the partner is doing some other job, we'll assume they're not part of the date anymore
            // This could happen if they were interrupted by something else
            SLog.Message(string.Format("[SocialInteractions] DateTracker: Partner {0} is doing an unexpected job {1}, restarting FollowAndWatch job.", 
                partner.LabelShort, partner.CurJobDef != null ? partner.CurJobDef.defName : "NULL"));
            
            // Start the FollowAndWatch job for the partner
            Job followJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator);
            partner.jobs.StartJob(followJob, JobCondition.InterruptForced);
        }

        private void TryHavePartnerJoinJoyActivity(Pawn initiator, Pawn partner, JobDef joyJobDef)
        {
            // Check if we have valid pawns
            if (initiator == null || partner == null || joyJobDef == null)
            {
                return;
            }
            
            // Find the joy giver for this job def
            JoyGiverDef initiatorJoyGiver = null;
            foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
            {
                if (joyGiver.jobDef == joyJobDef)
                {
                    initiatorJoyGiver = joyGiver;
                    break;
                }
            }
            
            if (initiatorJoyGiver == null)
            {
                SLog.Message(string.Format("[SocialInteractions] DateTracker: Could not find joy giver for job def {0}", 
                    joyJobDef.defName));
                return;
            }
            
            // Try to give the partner the same joy job as the initiator
            Job partnerJoyJob = initiatorJoyGiver.Worker.TryGiveJob(partner);
            if (partnerJoyJob == null)
            {
                SLog.Message(string.Format("[SocialInteractions] DateTracker: Partner {0} cannot do the same joy activity as initiator {1}", 
                    partner.LabelShort, initiator.LabelShort));
                return;
            }
            
            // Check if the target locations match or are nearby
            bool targetsMatch = false;
            if (partnerJoyJob.targetA.Thing != null && initiator.CurJob.targetA.Thing != null)
            {
                targetsMatch = partnerJoyJob.targetA.Thing == initiator.CurJob.targetA.Thing;
            }
            else if (partnerJoyJob.targetA.Cell.IsValid && initiator.CurJob.targetA.Cell.IsValid)
            {
                targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(initiator.CurJob.targetA.Cell) <= 7f;
            }
            
            if (targetsMatch)
            {
                SLog.Message(string.Format("[SocialInteractions] DateTracker: Partner {0} is joining initiator {1} in joy activity {2}.", 
                    partner.LabelShort, initiator.LabelShort, partnerJoyJob.def.defName));
                
                // Enqueue the joy job and then interrupt the current job for a smooth transition
                partner.jobs.jobQueue.EnqueueFirst(partnerJoyJob);
                partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] DateTracker: Target locations don't match for partner {0} and initiator {1}", 
                    partner.LabelShort, initiator.LabelShort));
            }
        }
    }
}