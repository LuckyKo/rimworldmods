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
            
            // Only log if the pawn is on a date to reduce log spam
            /*
            if (pawn != null)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: EndCurrentJob called for pawn {0}, job {1}, condition {2}", 
                    pawn.Name != null ? pawn.Name.ToStringShort : "NULL", 
                    __instance.curJob != null ? __instance.curJob.def.defName : "NULL", 
                    condition));
            }
            */
            
            // Check if the job was completed successfully
            if (pawn != null && __instance.curJob != null && condition == JobCondition.Succeeded)
            {
                // Check if the job is the CaughtCheatingInteraction job, if so, skip the date logic
                if (__instance.curJob.def == SI_JobDefOf.CaughtCheatingInteraction)
                {
                    SLog.Message("[SocialInteractions] JobDriver_Joy_Patch: Skipping date logic for CaughtCheatingInteraction job.");
                    return;
                }
                
                // Check if the pawn is on a date
                if (DatingManager.IsOnDate(pawn))
                {
                    // Log that the pawn is on a date
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Pawn {0} is on a date", 
                        pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                    
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
                                pawn.jobs.StartJob(followJob, JobCondition.InterruptForced);
                                SLog.Message(string.Format("[SocialInteractions] Restarted FollowAndWatch job for partner {0}", 
                                    pawn.Name != null ? pawn.Name.ToStringShort : "NULL"));
                            }
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
}