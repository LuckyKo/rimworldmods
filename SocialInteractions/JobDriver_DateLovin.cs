using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace SocialInteractions
{
    public class JobDriver_DateLovin : JobDriver
    {
        private int ticksLeft;

        private TargetIndex PartnerInd = TargetIndex.A;

        private TargetIndex BedInd = TargetIndex.B;

        private const int TicksBetweenHeartMotes = 100;

        private Pawn Partner
        {
            get
            {
                return (Pawn)(Thing)job.GetTarget(PartnerInd);
            }
        }

        private Building_Bed Bed
        {
            get
            {
                return (Building_Bed)(Thing)job.GetTarget(BedInd);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn == null || Partner == null || Bed == null) return false;
            if (pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(Bed, job, Bed.SleepingSlotsCount, 0, null, errorOnFailed);
            }
            return false;
        }

        public override bool CanBeginNowWhileLyingDown()
        {
            return pawn != null && Bed != null && JobInBedUtility.InBedOrRestSpotNow(pawn, job.GetTarget(BedInd));
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(BedInd);
            this.FailOnDespawnedOrNull(PartnerInd);
            this.FailOn(() => Partner == null || Partner.health == null || Partner.health.capacities == null || !Partner.health.capacities.CanBeAwake);
            this.KeepLyingDown(BedInd);
            yield return Toils_Bed.ClaimBedIfNonMedical(BedInd);
            yield return Toils_Bed.GotoBed(BedInd);

            Toil wakeUp = new Toil();
            wakeUp.initAction = () => { if (pawn.Awake()) return; };
            yield return wakeUp;

            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate
            {
                if (pawn == null) return;
                Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                if (pawn == initiator)
                {
                    ticksLeft = (int)(2500f * Mathf.Clamp(Rand.Range(0.1f, 1.1f), 0.1f, 2f));
                    if (Find.HistoryEventsManager != null) Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InitiatedLovin, pawn.Named(HistoryEventArgsNames.Doer)));
                }
                else
                {
                    ticksLeft = 9999999;
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil;

            Toil toil2 = Toils_LayDown.LayDown(BedInd, hasBed: true, lookForOtherJobs: false, canSleep: false, gainRestAndHealth: false);
            toil2.FailOn(() => Partner.CurJob == null || Partner.CurJob.def != SI_JobDefOf.DateLovin);
            toil2.AddPreTickIntervalAction(delegate(int delta)
            {
                ticksLeft -= delta;
                if (ticksLeft <= 0)
                {
                    ReadyForNextToil();
                }
                else if (pawn.IsHashIntervalTick(100, delta))
                {
                    if (Partner != null)
                    {
                        FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                    }
                }
            });
            toil2.AddFinishAction(delegate
            {
                if (pawn == null) return;

                Date date = DatingManager.GetDateWith(pawn);
                if (date != null && date.Stage == DateStage.Lovin)
                {
                    DatingManager.AdvanceDateStage(pawn);
                }

                Thought_Memory thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    if (pawn.needs.mood.thoughts != null && pawn.needs.mood.thoughts.memories != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thought_Memory, Partner);
                    }
                }
                if (Find.HistoryEventsManager != null)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.GotLovin, pawn.Named(HistoryEventArgsNames.Doer)));
                    HistoryEventDef def = HistoryEventDefOf.GotLovin_NonSpouse;
                    if (pawn.relations != null && Partner != null && pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, Partner))
                    {
                        def = HistoryEventDefOf.GotLovin_Spouse;
                    }
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(def, pawn.Named(HistoryEventArgsNames.Doer)));
                }
            });
            toil2.socialMode = RandomSocialMode.Off;
            yield return toil2;
        }
    }
}
