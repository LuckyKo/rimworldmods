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
                // If they have a partner, and that partner is still doing the same joy activity, don't end the job.
                if (partner != null && partner.CurJob != null && pawn.CurJob != null && partner.CurJob.def == pawn.CurJob.def && partner.CurJob.targetA == pawn.CurJob.targetA)
                {
                    fullJoyAction = JoyTickFullJoyAction.None;
                }
            }
        }
    }
}
