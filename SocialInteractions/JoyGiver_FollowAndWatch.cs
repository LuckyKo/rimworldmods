using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JoyGiver_FollowAndWatch : JoyGiver
    {
        // Existing TryGiveJob (less used now)
        public override Job TryGiveJob(Pawn pawn)
        {
            Pawn initiator = DatingManager.GetPartnerOnDateWith(pawn);
            if (initiator != null && initiator.CurJob != null && initiator.CurJob.targetA.IsValid)
            {
                return JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator, initiator.CurJob.targetA.Thing);
            }
            return null;
        }

        // New TryGiveJob overload for direct assignment, used when joining an existing joy activity
        public Job TryGiveJob(Pawn pawn, Thing joySpot)
        {
            if (joySpot == null) return null;

            // If the joySpot is a building, try to find a suitable interaction cell or chair
            Building joyBuilding = joySpot as Building;
            if (joyBuilding != null)
            {
                // Check if the joy activity allows multiple participants
                JoyGiverDef joyGiverDef = DefDatabase<JoyGiverDef>.AllDefs.FirstOrDefault(x => x.joyKind == joyBuilding.def.building.joyKind);
                if (joyGiverDef != null && joyGiverDef.jobDef != null && joyGiverDef.jobDef.joyMaxParticipants > 1)
                {
                    // Try to find an available interaction cell or a sitable chair
                    // This logic is similar to what was in JobDriver_FollowAndWatch, but now within the JoyGiver
                    // This ensures the JoyGiver's rules for finding a spot are applied.

                    // For poker tables, find an available chair
                    if (joyBuilding.def.defName == "PokerTable")
                    {
                        foreach (Thing chair in joyBuilding.InteractionCell.GetThingList(pawn.Map).Where(t => t.def.building != null && t.def.building.isSittable))
                        {
                            if (pawn.CanReserve(chair))
                            {
                                return JobMaker.MakeJob(pawn.jobs.curJob.def, joyBuilding, chair);
                            }
                        }
                    }
                    else // For other multi-participant joy activities, find an available interaction cell
                    {
                        List<IntVec3> cells = new List<IntVec3>();
                        cells.AddRange(GenAdj.CellsAdjacent8Way(joyBuilding));
                        cells.AddRange(GenAdj.CellsAdjacentCardinal(joyBuilding));

                        foreach (IntVec3 cell in cells)
                        {
                            if (cell.Standable(pawn.Map) && !pawn.Map.pawnDestinationReservationManager.IsReserved(cell))
                            {
                                if (pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some))
                                {
                                    return JobMaker.MakeJob(pawn.jobs.curJob.def, joyBuilding, cell);
                                }
                            }
                        }
                    }
                }
            }

            // Fallback to FollowAndWatchInitiator if no specific joy job can be found
            return JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, joySpot);
        }
    }
}