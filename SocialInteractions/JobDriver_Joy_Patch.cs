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
            // Log that the patch is being called
            if (__instance != null && __instance.pawn != null)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: EndCurrentJob called for pawn {0}, job {1}, condition {2}", 
                    __instance.pawn.Name.ToStringShort, 
                    __instance.curJob != null ? __instance.curJob.def.defName : "NULL", 
                    condition));
            }
            
            // Check if the job was completed successfully
            if (__instance != null && __instance.pawn != null && __instance.curJob != null && condition == JobCondition.Succeeded)
            {
                // Check if the pawn is on a date
                if (DatingManager.IsOnDate(__instance.pawn))
                {
                    // Log that the pawn is on a date
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Pawn {0} is on a date", __instance.pawn.Name.ToStringShort));
                    
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
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(__instance.pawn);
                        if (initiator == __instance.pawn)
                        {
                            // This pawn is the initiator, so advance the date stage
                            SLog.Message(string.Format("[SocialInteractions] Joy job completed for initiator {0}, advancing date stage.", __instance.pawn.Name.ToStringShort));
                            DatingManager.AdvanceDateStage(__instance.pawn);
                        }
                        else
                        {
                            // This pawn is the partner, so restart their follow job
                            SLog.Message(string.Format("[SocialInteractions] Joy job completed for partner {0}, restarting follow job.", __instance.pawn.Name.ToStringShort));
                            
                            // Get the initiator of the date
                            Pawn dateInitiator = DatingManager.GetInitiatorOfDateWith(__instance.pawn);
                            if (dateInitiator != null)
                            {
                                // Create and start the FollowAndWatch job for the partner
                                Job followJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, dateInitiator);
                                __instance.pawn.jobs.StartJob(followJob, JobCondition.InterruptForced);
                                SLog.Message(string.Format("[SocialInteractions] Restarted FollowAndWatch job for partner {0}", __instance.pawn.Name.ToStringShort));
                            }
                        }
                    }
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Pawn {0} is NOT on a date", __instance.pawn.Name.ToStringShort));
                }
            }
        }
    }
}