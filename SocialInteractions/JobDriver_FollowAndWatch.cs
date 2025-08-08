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
                IntVec3 watchCell;
                if (this.job.targetB.Thing != null)
                {
                    watchCell = CellFinder.StandableCellNear(this.job.targetB.Thing.Position, this.job.targetB.Thing.Map, 5);
                }
                else
                {
                    // Fallback to initiator's position if joy spot is not a physical thing
                    watchCell = CellFinder.StandableCellNear(this.job.targetA.Thing.Position, this.job.targetA.Thing.Map, 5);
                }

                if (!watchCell.IsValid) watchCell = this.job.targetA.Thing.Position; // Fallback to initiator's exact position if no valid cell found
                this.pawn.pather.StartPath(watchCell, PathEndMode.OnCell);
            };
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            follow.atomicWithPrevious = true;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: Starting watch toil.");
                this.pawn.rotationTracker.FaceCell(this.job.targetA.Cell);
            };
            watch.tickAction = () =>
            {
                // Add null and type check for initiator
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null)
                {
                    this.ReadyForNextToil(); // End the job if initiator is invalid
                    return;
                }

                Thing joySpot = this.job.targetB.Thing;

                // End condition: initiator is no longer doing a job at the original joy spot.
                if (initiator.CurJob == null || initiator.CurJob.targetA.Thing != joySpot)
                {
                    Log.Message(string.Format("[SocialInteractions] FollowAndWatch: Initiator's joy job at {0} has ended. Advancing date stage.", joySpot.Label));
                    
                    Date date = DatingManager.GetDateWith(this.pawn);
                    if (date != null && date.Stage == DateStage.Joy)
                    {
                        DatingManager.AdvanceDateStage(this.pawn);
                    }
                    
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }
                
                // Periodically try to join the initiator's joy activity
                if (Find.TickManager.TicksGame % 60 == 0) // Check once per second
                {
                    if (initiator.CurJob != null && initiator.CurJob.def != null && initiator.CurJob.def.joyKind != null) // Check if initiator's job is a joy job
                    {
                        Building joyBuilding = initiator.CurJob.targetA.Thing as Building;
                        if (joyBuilding != null) // Only proceed if it's a building-based joy activity
                        {
                            JoyGiverDef joyGiverDef = DefDatabase<JoyGiverDef>.AllDefs.FirstOrDefault(x => x.joyKind == joyBuilding.def.building.joyKind);
                            if (joyGiverDef != null && joyGiverDef.jobDef != null && joyGiverDef.jobDef.joyMaxParticipants > 1)
                            {
                                // Use the new TryGiveJob overload in JoyGiver_FollowAndWatch to find a suitable job
                                JoyGiver_FollowAndWatch joyGiverFollowAndWatch = new JoyGiver_FollowAndWatch();
                                Job newRecipientJoyJob = joyGiverFollowAndWatch.TryGiveJob(this.pawn, joyBuilding);

                                if (newRecipientJoyJob != null)
                                {
                                    this.pawn.jobs.StartJob(newRecipientJoyJob, JobCondition.InterruptForced);
                                    this.ReadyForNextToil(); // End FollowAndWatch job
                                    return;
                                }
                            }
                        }
                    }
                }
                // Gain joy while watching
                if (initiator.CurJob != null && initiator.CurJob.def != null && initiator.CurJob.def.joyKind != null)
                {
                    JoyUtility.JoyTickCheckEnd(this.pawn, 1, JoyTickFullJoyAction.None);
                }
            };
            watch.AddFinishAction(() =>
            {
                Pawn initiatorPawn = this.job.targetA.Thing as Pawn;
                if (initiatorPawn != null)
                {
                    Hediff hediff = this.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                    if (hediff != null) this.pawn.health.RemoveHediff(hediff);

                    hediff = initiatorPawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                    if (hediff != null) initiatorPawn.health.RemoveHediff(hediff);
                }
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}
