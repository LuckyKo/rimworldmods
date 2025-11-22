using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildBreakBuilding : JobDriver
    {
        private const int BonkingDuration = 300; // ~5 seconds

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            
            // Fail if target is not a building with CompBreakdownable
            this.FailOn(() =>
            {
                Thing building = job.GetTarget(TargetIndex.A).Thing;
                if (building == null) return true;
                
                CompBreakdownable breakdownComp = building.TryGetComp<CompBreakdownable>();
                return breakdownComp == null || breakdownComp.BrokenDown;
            });

            // Move to the building
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Bonking toil - attack animation
            Toil bonkToil = new Toil();
            bonkToil.initAction = () =>
            {
                SLog.Message(string.Format("[SocialInteractions] ChildBreakBuilding: Child {0} started bonking {1}",
                    pawn.LabelShort, TargetA.Thing.Label));
            };
            bonkToil.tickAction = () =>
            {
                // Face the building
                pawn.rotationTracker.FaceTarget(TargetA);
                
                // Periodically show attack animation
                if (pawn.IsHashIntervalTick(60)) // Every second
                {
                    pawn.meleeVerbs.TryMeleeAttack(TargetA.Thing);
                }
            };
            bonkToil.defaultCompleteMode = ToilCompleteMode.Delay;
            bonkToil.defaultDuration = BonkingDuration;
            bonkToil.WithProgressBarToilDelay(TargetIndex.A);
            yield return bonkToil;

            // Break the building
            Toil breakToil = new Toil();
            breakToil.initAction = () =>
            {
                Thing building = TargetA.Thing;
                CompBreakdownable breakdownComp = building.TryGetComp<CompBreakdownable>();

                if (breakdownComp != null && !breakdownComp.BrokenDown)
                {
                    // Trigger breakdown
                    breakdownComp.DoBreakdown();

                    SLog.Message(string.Format("[SocialInteractions] ChildBreakBuilding: Child {0} broke {1} at {2}",
                        pawn.LabelShort, building.Label, building.Position));

                    // Show message to player
                    Messages.Message(string.Format("{0} (child) broke {1}!", pawn.LabelShort, building.Label),
                        new LookTargets(pawn, building), MessageTypeDefOf.NegativeEvent);

                    // Add a thought to the child about being destructive
                    if (pawn.needs != null && pawn.needs.mood != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildDestructive, null);
                    }

                    // Trigger LLM interaction about breaking property
                    string subject = string.Format("broke {0}, sorry about that!", building.Label);
                    SocialInteractions.HandleMonologue(pawn, subject);
                }
            };
            breakToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return breakToil;
        }
    }
}
