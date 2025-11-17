using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildPlayWithItem : JobDriver
    {
        private const int BasePlayDuration = 1800; // 30 seconds in ticks
        private const float ItemDamageChance = 0.3f; // 30% chance of damaging the item during play

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Child should be able to reserve the item and the target location
            Pawn pawn = this.pawn;
            Job job = this.job;
            
            // Reserve the item to be played with
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed))
            {
                return false;
            }

            // Reserve the play location
            if (!pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed))
            {
                return false;
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the item disappears or becomes invalid
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => !job.GetTarget(TargetIndex.A).Thing.def.EverHaulable); // If item becomes unhauled

            // Go to the item and pick it up
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            // Manually pick up the item to avoid stack count issues with minified/special items
            Toil pickupToil = new Toil();
            pickupToil.initAction = delegate
            {
                Pawn actor = pickupToil.actor;
                Thing item = pickupToil.actor.CurJob.GetTarget(TargetIndex.A).Thing;

                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null during pickup, ending job");
                    pickupToil.actor.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                    return;
                }

                // Calculate how many to take (for stackable items)
                int takeNum = 1; // Always take just 1 for this behavior to avoid complexity
                if (item.def.stackLimit > 1 && item.stackCount > 1)
                {
                    takeNum = Mathf.Min(1, item.stackCount); // Take minimum of requested or available
                }

                // Actually pick up the item
                actor.carryTracker.TryStartCarry(item, takeNum);
            };
            pickupToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pickupToil;

            // Go to the play location
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // Play with the item - this is where the damage chance occurs
            Toil playToil = new Toil();
            playToil.initAction = delegate
            {
                Pawn child = pawn; // 'pawn' is a property of JobDriver
                Thing item = (Thing)job.GetTarget(TargetIndex.A).Thing;
                
                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Log the play session
                SLog.Message(string.Format("[SocialInteractions] Child {0} is playing with item {1}", 
                    child.LabelShort, item.Label));

                // Show message to player about the child playing
                // Messages.Message(string.Format("{0} (child) is playing with {1}!", child.LabelShort, item.Label),
                    // new LookTargets(child, item), MessageTypeDefOf.CautionInput);

                bool itemWasDamaged = false;

                // There's a chance the child will damage the item during play
                if (Rand.Value < ItemDamageChance)
                {
                    // Damage the item
                    ThingWithComps thingWithComps = item as ThingWithComps;
                    if (thingWithComps != null)
                    {
                        // Apply damage to the item (damage type could be "Scratch" or similar)
                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Deterioration, item.MaxHitPoints / 4, 1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown);
                        thingWithComps.TakeDamage(dinfo);

                        SLog.Message(string.Format("[SocialInteractions] Child {0} damaged item {1}",
                            child.LabelShort, item.Label));

                        // Show message to player about the damage
                        Messages.Message(string.Format("{0} (child) damaged {1} while playing!", child.LabelShort, item.Label),
                            new LookTargets(child, item), MessageTypeDefOf.NegativeEvent);

                        itemWasDamaged = true;
                    }
                    else if (item.def.useHitPoints)
                    {
                        // Direct hit point damage for simple items
                        item.HitPoints = Mathf.Max(1, item.HitPoints - (item.MaxHitPoints / 4));

                        SLog.Message(string.Format("[SocialInteractions] Child {0} damaged item {1}",
                            child.LabelShort, item.Label));

                        // Show message to player about the damage
                        Messages.Message(string.Format("{0} (child) damaged {1} while playing!", child.LabelShort, item.Label),
                            new LookTargets(child, item), MessageTypeDefOf.NegativeEvent);

                        itemWasDamaged = true;
                    }
                }

                // Create appropriate subject based on whether the item was damaged
                string subject;
                if (itemWasDamaged)
                {
                    subject = string.Format("playing with {1} and accidentally broke it!", child.LabelShort, item.LabelCap);
                }
                else
                {
                    subject = string.Format("playing with {1} and it's fun!", child.LabelShort, item.LabelCap);
                }

                // Trigger a monologue for the child about playing with the item
                SocialInteractions.HandleMonologue(child, subject);
            };
            
            playToil.defaultCompleteMode = ToilCompleteMode.Delay;
            playToil.defaultDuration = BasePlayDuration;
            playToil.socialMode = RandomSocialMode.Off; // Child is focused on playing with the item
            yield return playToil;
            
            // Drop the item where the child is
            Toil dropToil = new Toil();
            dropToil.initAction = delegate
            {
                Pawn child = pawn;
                Thing item = (Thing)job.GetTarget(TargetIndex.A).Thing;
                
                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null during drop, ending job");
                    return;
                }

                // Drop the item at the current location (where the child has been playing)
                IntVec3 dropLocation = child.Position;

                if (dropLocation.IsValid && dropLocation.InBounds(Map))
                {
                    // Actually drop the item
                    Thing droppedThing = child.carryTracker.CarriedThing;
                    if (droppedThing != null)
                    {
                        child.carryTracker.TryDropCarriedThing(dropLocation, ThingPlaceMode.Near, out droppedThing);

                        SLog.Message(string.Format("[SocialInteractions] Child {0} dropped item {1} at {2}",
                            child.LabelShort, droppedThing.Label, dropLocation));
                    }
                }
            };
            
            yield return dropToil;
        }
    }
}