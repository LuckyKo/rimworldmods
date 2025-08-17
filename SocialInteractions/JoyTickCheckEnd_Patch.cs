using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(JoyUtility), "JoyTickCheckEnd")]
    public static class JoyTickCheckEnd_Patch
    {
        static void Prefix(Pawn pawn, ref JoyTickFullJoyAction fullJoyAction)
        {
            // If the job was going to end due to full joy, check if the pawn is on a date.
            if (fullJoyAction == JoyTickFullJoyAction.EndJob)
            {
                Pawn partner = DatingManager.GetPartnerOnDateWith(pawn);
                // If they have a partner, and that partner is doing the FollowAndWatch job, don't end the job.
                // This allows the initiator to continue the joy activity while the partner is following and watching.
                if (partner != null && partner.CurJob != null && partner.CurJob.def == SI_JobDefOf.FollowAndWatchInitiator)
                {
                    fullJoyAction = JoyTickFullJoyAction.None;
                }
            }
        }
    }
}
