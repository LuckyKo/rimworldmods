using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;

namespace SocialInteractions
{
    public static class LovinBouncer
    {
        public static Dictionary<Pawn, float> bounces = new Dictionary<Pawn, float>();
    }

    public static class CustomToils_Bed
    {
        public static Toil GotoBed(TargetIndex bedIndex, TargetIndex partnerIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Building_Bed bed = (Building_Bed)actor.CurJob.GetTarget(bedIndex).Thing;
                Pawn partner = (Pawn)actor.CurJob.GetTarget(partnerIndex).Thing;

                Pawn initiator = DatingManager.GetInitiatorOfDateWith(actor);
                int slotIndex = (actor == initiator) ? 0 : 0;

                actor.pather.StartPath(bed.GetSleepingSlotPos(slotIndex), PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }
    }

    public class JobDriver_DateLovin : JobDriver
    {
        private TargetIndex PartnerInd = TargetIndex.A;
        private TargetIndex BedPosInd = TargetIndex.B;

        private Pawn Partner { get { return (Pawn)(Thing)job.GetTarget(PartnerInd); } }
        private IntVec3 BedPos { get { return job.GetTarget(BedPosInd).Cell; } }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null checks to prevent NullReferenceException
            if (pawn == null || Partner == null)
            {
                Log.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in TryMakePreToilReservations.");
                return false;
            }
            // Only reserve the partner, not the bed position
            return pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Add null checks
            if (pawn == null || Partner == null)
            {
                Log.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in MakeNewToils.");
                yield break;
            }
            
            this.FailOnDespawnedOrNull(PartnerInd);
            this.FailOn(() => !Partner.health.capacities.CanBeAwake);

            // Go to the bed position
            yield return Toils_Goto.GotoCell(BedPosInd, PathEndMode.OnCell);

            Toil lovinToil = ToilMaker.MakeToil("LovinToil");
            lovinToil.defaultCompleteMode = ToilCompleteMode.Delay;
            lovinToil.defaultDuration = 2500;
            lovinToil.initAction = delegate
            {
                // Add null checks
                if (pawn == null || Partner == null)
                {
                    Log.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in lovinToil initAction.");
                    return;
                }
                
                pawn.pather.StopDead();
                Partner.pather.StopDead();
                LovinBouncer.bounces.Add(pawn, 0f);
                LovinBouncer.bounces.Add(Partner, 0f);
            };
            lovinToil.tickAction = delegate
            {
                // Add null checks
                if (pawn == null || Partner == null)
                {
                    Log.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in lovinToil tickAction.");
                    return;
                }
                
                float bounceOffset = Mathf.Sin((float)Find.TickManager.TicksGame * 0.4f) * 1.0f;
                LovinBouncer.bounces[pawn] = bounceOffset;
                LovinBouncer.bounces[Partner] = bounceOffset;

                // Show heart emotes for both pawns and gain joy
                if (pawn.IsHashIntervalTick(100))
                {
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                    // Gain joy for the initiator
                    if (pawn.needs != null && pawn.needs.joy != null)
                    {
                        pawn.needs.joy.GainJoy(0.001f, JoyKindDefOf.Social);
                    }
                }
                if (Partner.IsHashIntervalTick(100))
                {
                    FleckMaker.ThrowMetaIcon(Partner.Position, Partner.Map, FleckDefOf.Heart);
                    // Gain joy for the partner
                    if (Partner.needs != null && Partner.needs.joy != null)
                    {
                        Partner.needs.joy.GainJoy(0.001f, JoyKindDefOf.Social);
                    }
                }
            };
            lovinToil.AddFinishAction(delegate
            {
                // Add null checks
                if (pawn == null || Partner == null)
                {
                    Log.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in lovinToil finishAction.");
                    return;
                }
                
                LovinBouncer.bounces.Remove(pawn);
                LovinBouncer.bounces.Remove(Partner);

                // Add thoughts BEFORE advancing the date stage
                if (pawn != null && Partner != null)
                {
                    var thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                    thought.otherPawn = Partner;
                    if (pawn.needs != null && pawn.needs.mood != null && pawn.needs.mood.thoughts != null && pawn.needs.mood.thoughts.memories != null) 
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                    
                    var thought2 = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                    thought2.otherPawn = pawn;
                    if (Partner.needs != null && Partner.needs.mood != null && Partner.needs.mood.thoughts != null && Partner.needs.mood.thoughts.memories != null) 
                        Partner.needs.mood.thoughts.memories.TryGainMemory(thought2, null);
                }

                Date date = DatingManager.GetDateWith(pawn);
                if (date != null && date.Stage == DateStage.Lovin)
                {
                    DatingManager.AdvanceDateStage(pawn);
                }
            });
            yield return lovinToil;
        }
    }
}