using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Pawn_Tick_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            Pawn pawn = __instance;
            if (pawn.relations == null)
            {
                return;
            }
            
            if (pawn.IsHashIntervalTick(SocialInteractions.Settings.jobCheckIntervalTicks)) // Check every second
            {
                Pawn partner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, (p) => !p.Dead);
                if (partner == null)
                {
                    partner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, (p) => !p.Dead);
                }
                if (partner == null)
                {
                    partner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, (p) => !p.Dead);
                }

                if (partner != null && DatingManager.IsOnDate(partner))
                {
                    // Check if the partner is in the "lovin" stage of the date
                    Date date = DatingManager.GetDateWith(partner);
                    if (date != null && date.Stage == DateStage.Lovin)
                    {
                        Pawn cheatingPartner = DatingManager.GetPartnerOfDateWith(partner);
                        if (cheatingPartner != null && cheatingPartner != pawn && pawn.Position.InHorDistOf(partner.Position, 10f))
                        {
                            // Caught them in the act of lovin'!
                            // Trigger LLM interaction
                            SocialInteractions.HandleNonStoppingInteraction(pawn, partner, SI_InteractionDefOf.CaughtCheating, "caught cheating");
                            
                            // After the interaction, there's a chance to start a social fight
                            // We'll handle the fight initiation in the InteractionWorker_CaughtCheating
                        }
                    }
                }
            }
        }
    }
}