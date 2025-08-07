using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_FollowAndWatch : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator
            this.FailOnDespawnedOrNull(TargetIndex.B); // Joy Spot

            Toil follow = new Toil();
            follow.initAction = () =>
            {
                IntVec3 watchCell = CellFinder.StandableCellNear(this.job.targetB.Thing.Position, this.job.targetB.Thing.Map, 5);
                if (!watchCell.IsValid) watchCell = this.job.targetB.Thing.Position;
                this.pawn.pather.StartPath(watchCell, PathEndMode.OnCell);
            };
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            follow.atomicWithPrevious = true;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                this.pawn.rotationTracker.FaceCell(this.job.targetA.Cell);
            };
            watch.tickAction = () =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Thing joySpot = this.job.targetB.Thing;

                // End if initiator is not doing a job, or the job is not at the joy spot
                if (initiator.CurJob == null || initiator.CurJob.targetA.Thing != joySpot)
                {
                    Log.Message(string.Format("[SocialInteractions] FollowAndWatch: Ending because initiator's job ended or changed. Initiator: {0}, Job: {1}", initiator.Name.ToStringShort, initiator.CurJob != null ? initiator.CurJob.def.defName : "null"));
                    this.ReadyForNextToil();
                }
                else
                {
                    // Periodically try to join the initiator's joy activity
                    if (Find.TickManager.TicksGame % 60 == 0) // Check once per second
                    {
                        if (initiator.CurJob != null && initiator.CurJob.def.joyKind != null) // Check if initiator's job is a joy job
                        {
                            Building joyBuilding = initiator.CurJob.targetA.Thing as Building;
                            if (joyBuilding != null)
                            {
                                JoyGiverDef joyGiverDef = DefDatabase<JoyGiverDef>.AllDefs.FirstOrDefault(x => x.joyKind == joyBuilding.def.building.joyKind);
                                if (joyGiverDef != null && joyGiverDef.jobDef.joyMaxParticipants > 1)
                                {
                                    // Find an available interaction cell
                                    List<IntVec3> cells = new List<IntVec3>();
                                    cells.AddRange(GenAdj.CellsAdjacent8Way(joyBuilding));
                                    cells.AddRange(GenAdj.CellsAdjacentCardinal(joyBuilding));

                                    foreach (IntVec3 cell in cells)
                                    {
                                        if (cell.Standable(this.pawn.Map) && !this.pawn.Map.pawnDestinationReservationManager.IsReserved(cell))
                                        {
                                            Job newRecipientJoyJob = JobMaker.MakeJob(initiator.CurJob.def, joyBuilding, cell);
                                            if (this.pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some))
                                            {
                                                this.pawn.jobs.StartJob(newRecipientJoyJob, JobCondition.InterruptForced);
                                                this.ReadyForNextToil(); // End FollowAndWatch job
                                                return; // Exit the loop
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Gain joy while watching
                    JoyUtility.JoyTickCheckEnd(this.pawn, 1, JoyTickFullJoyAction.None);
                }
            };
            watch.AddFinishAction(() =>
            {
                Pawn initiator = (Pawn)this.job.targetA.Thing;
                Hediff hediff = this.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                if (hediff != null) this.pawn.health.RemoveHediff(hediff);

                hediff = initiator.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                if (hediff != null) initiator.health.RemoveHediff(hediff);
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}
