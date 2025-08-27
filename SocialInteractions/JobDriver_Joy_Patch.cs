using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
    public static class JobDriver_Joy_Patch
    {
        public static void Postfix(Pawn_JobTracker __instance, JobCondition condition)
        {
            // Access the pawn field through reflection with proper error handling
            Pawn pawn = null;
            try
            {
                var field = typeof(Pawn_JobTracker).GetField("pawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    pawn = (Pawn)field.GetValue(__instance);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] Error getting pawn from Pawn_JobTracker: {0}", ex.Message));
            }
            
            // Only proceed if we have a valid pawn
            if (pawn == null) return;
            
            // Check if the pawn is on a date
            if (DatingManager.IsOnDate(pawn))
            {
                // Log that the pawn is on a date
                SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Pawn {0} is on a date", 
                    pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                
                // Check if the job is the CaughtCheatingInteraction job, if so, skip the date logic
                if (__instance.curJob != null && __instance.curJob.def == SI_JobDefOf.CaughtCheatingInteraction)
                {
                    SLog.Message("[SocialInteractions] JobDriver_Joy_Patch: Skipping date logic for CaughtCheatingInteraction job.");
                    return;
                }
                
                // Check if the job was completed successfully
                if (__instance.curJob != null && condition == JobCondition.Succeeded)
                {
                    // Check if the job was a joy job
                    bool isJoyJob = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == __instance.curJob.def)
                        {
                            isJoyJob = true;
                            break;
                        }
                    }
                    
                    // Log the joy job check result
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Job {0} is joy job: {1}", 
                        __instance.curJob.def.defName, isJoyJob));
                    
                    // If it was a joy job, check if this pawn is the initiator of the date
                    if (isJoyJob)
                    {
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        if (initiator == pawn)
                        {
                            // This pawn is the initiator, so advance the date stage
                            SLog.Message(string.Format("[SocialInteractions] Joy job completed for initiator {0}, advancing date stage.", 
                                pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                            DatingManager.AdvanceDateStage(pawn);
                        }
                        else
                        {
                            // This pawn is the partner, so restart their follow job
                            SLog.Message(string.Format("[SocialInteractions] Joy job completed for partner {0}, restarting follow job.", 
                                pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                            
                            // Get the initiator of the date
                            Pawn dateInitiator = DatingManager.GetInitiatorOfDateWith(pawn);
                            if (dateInitiator != null)
                            {
                                // Create and start the FollowAndWatch job for the partner
                                Job followJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, dateInitiator);
                                // Add a small delay before starting the job to prevent race conditions
                                pawn.jobs.jobQueue.EnqueueFirst(followJob);
                                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                                SLog.Message(string.Format("[SocialInteractions] Restarted FollowAndWatch job for partner {0}", 
                                    pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                            }
                        }
                    }
                }
                // If the job was not completed successfully, check if it moved to a non-joy job
                // But only if the pawn is the initiator of the date and the date is in the joy stage
                else if (__instance.curJob != null && condition != JobCondition.Succeeded)
                {
                    // Check if the current job is NOT a joy job (meaning they moved to a non-joy job)
                    bool isCurrentJobJoy = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == __instance.curJob.def)
                        {
                            isCurrentJobJoy = true;
                            break;
                        }
                    }
                    
                    // Special case: If the current job is a DateLovin job, we should not treat it as a non-joy job
                    bool isCurrentJobDateLovin = (__instance.curJob.def == SI_JobDefOf.DateLovin);
                    
                    // If the current job is not a joy job and not a DateLovin job, advance the date
                    // But only if the pawn is the initiator of the date and the date is in the joy stage
                    if (!isCurrentJobJoy && !isCurrentJobDateLovin)
                    {
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        Date date = DatingManager.GetDateWith(pawn);
                        if (initiator == pawn && date != null && date.Stage == DateStage.Joy)
                        {
                            SLog.Message(string.Format("[SocialInteractions] Pawn {0} moved to non-joy job {1}, checking if should advance date.", 
                                pawn.Name != null ? pawn.Name.ToStringShort : "NULL", 
                                __instance.curJob.def.defName));
                            
                            // This pawn is the initiator, so advance the date stage
                            SLog.Message(string.Format("[SocialInteractions] Initiator {0} moved to non-joy job, advancing date stage.", 
                                pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                            DatingManager.AdvanceDateStage(pawn);
                        }
                        // For partners, we don't need to do anything special as they will be handled by the stuck date detection
                    }
                }
            }
            /*
            else
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Pawn {0} is NOT on a date", 
                    pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
            }
            */
        }
    }
}