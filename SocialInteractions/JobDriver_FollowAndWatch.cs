using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_FollowAndWatch : JobDriver
    {
        private int ticksSinceNotInJoy = 0;
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
                if (this.pawn == null || this.job == null || this.job.targetA == null) return;
                if (this.pawn != null && this.pawn.pather != null) this.pawn.pather.StartPath(this.job.targetA, PathEndMode.Touch);
            };
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return follow;

            Toil watch = new Toil();
            watch.initAction = () =>
            {
                Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: Starting watch toil.");
            };
            watch.tickAction = () =>
            {
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: initiator is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                if (this.pawn == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                if (this.job == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: job is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }
                if (this.job.targetB == null)
                {
                    Log.Message("[SocialInteractions] JobDriver_FollowAndWatch: job.targetB is null, ending job.");
                    this.ReadyForNextToil();
                    return;
                }

                Thing joySpot = this.job.targetB.Thing;

                if (this.pawn.IsHashIntervalTick(60) && (this.pawn.pather == null || !this.pawn.pather.Moving || this.pawn.pather.Destination != initiator.Position))
                {
                    this.pawn.pather.StartPath(initiator, PathEndMode.InteractionCell);
                }

                if (initiator.needs.joy.CurLevelPercentage >= 1f)
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: initiator ({0}) joy is full, ending job.", initiator.Name.ToStringShort));
                    this.ReadyForNextToil();
                    return;
                }

                if (initiator.CurJob == null || initiator.CurJob.def.joyKind == null || initiator.CurJob.targetA.Thing != joySpot)
                {
                    ticksSinceNotInJoy++;
                    if (ticksSinceNotInJoy > 60)
                    {
                        Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: initiator ({0}) job changed ({1}) for too long, ending job.", initiator.Name.ToStringShort, initiator.CurJob != null ? initiator.CurJob.def.defName : "null"));
                        this.ReadyForNextToil(); // End the FollowAndWatch job
                        return;
                    }
                }
                else
                {
                    ticksSinceNotInJoy = 0;
                }

                if (!DatingManager.IsOnDate(initiator))
                {
                    Log.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: date ended for initiator ({0}), ending job.", initiator.Name.ToStringShort));
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }
            
                if (this.pawn.needs != null && this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
                }
            };
            watch.AddFinishAction(() =>
            {
                // OnDate hediffs are now handled by DatingManager
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}