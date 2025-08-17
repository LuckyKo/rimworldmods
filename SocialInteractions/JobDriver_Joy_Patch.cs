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
                    
                    // If it was a joy job, advance the date stage
                    if (isJoyJob)
                    {
                        SLog.Message(string.Format("[SocialInteractions] Joy job completed for pawn {0}, advancing date stage.", __instance.pawn.Name.ToStringShort));
                        DatingManager.AdvanceDateStage(__instance.pawn);
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