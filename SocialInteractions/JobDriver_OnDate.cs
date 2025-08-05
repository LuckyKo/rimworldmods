using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_OnDate : JobDriver
        {

            public override bool TryMakePreToilReservations(bool errorOnFailed)
            {
                return true;
            }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            Log.Message("[SocialInteractions] JobDriver_OnDate: MakeNewToils entered.");
            Pawn partner = (Pawn)this.job.targetA.Thing;
            Thing joySpot = (Thing)this.job.targetB.Thing;
            Log.Message(string.Format("[SocialInteractions] JobDriver_OnDate: Partner: {0}, JoySpot: {1}, JoySpotDef: {2}", partner, joySpot, joySpot.def.defName));
            JoyGiverDef joyGiverDef = DefDatabase<JoyGiverDef>.AllDefsListForReading.FirstOrDefault(j => j.thingDefs != null && j.thingDefs.Contains(joySpot.def));
            Log.Message(string.Format("[SocialInteractions] JobDriver_OnDate: JoyGiverDef found: {0}", joyGiverDef != null ? joyGiverDef.defName : "NULL"));

            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);

            // Go to the joy spot
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);

            // Toil to start the joy activity
            Toil doJoyActivity = new Toil();
            doJoyActivity.initAction = () =>
            {
                if (this.pawn.thingIDNumber < partner.thingIDNumber)
                {
                    string subject = string.Format("{0} is going on a date with {1} doing {2}", this.pawn.Name.ToStringShort, partner.Name.ToStringShort, joySpot.def.label);
                    SocialInteractions.HandleJobGiverInteraction(this.pawn, partner, SI_InteractionDefOf.DateAccepted, subject);
                }

                if (joyGiverDef != null)
                {
                    JoyGiver joyGiver = joyGiverDef.Worker;
                    if (joyGiver != null)
                    {
                        Job joyJob = joyGiver.TryGiveJob(this.pawn);
                        if (joyJob != null)
                        {
                            this.pawn.jobs.StartJob(joyJob, JobCondition.InterruptForced, null, false, true, null, null, false);
                        }
                        else
                        {
                            Log.Warning(string.Format("[SocialInteractions] Could not give joy job for {0} at {1}. TryGiveJob returned null.", this.pawn.LabelShort, joySpot.LabelShort));
                            this.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        }
                    }
                    else
                    {
                        Log.Warning(string.Format("[SocialInteractions] JoyGiver worker is null for {0}", joyGiverDef.LabelCap));
                        this.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    }
                }
                else
                {
                    Log.Warning(string.Format("[SocialInteractions] No JoyGiverDef found for {0}", joySpot.LabelShort));
                    this.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };
            doJoyActivity.defaultCompleteMode = ToilCompleteMode.Never;
            doJoyActivity.tickAction = () =>
            {
                // End the date if the initiator's joy is full
                if (this.pawn.needs.joy.CurLevelPercentage >= 0.99f)
                {
                    this.ReadyForNextToil();
                }
            };
            yield return doJoyActivity;

            // Toil for the partner to watch or join
            Toil partnerActivity = new Toil();
            partnerActivity.initAction = () =>
            {
                if (partner.CurJob == null || partner.CurJob.def != joyGiverDef.jobDef)
                {
                    // If partner couldn't join the joy activity, make them watch or just stand there
                    Job watchJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WatchBuilding"), joySpot);
                    if (partner.jobs.TryTakeOrderedJob(watchJob, JobTag.Misc))
                    {
                        Log.Message(string.Format("[SocialInteractions] {0} is watching {1} at {2}", partner.LabelShort, this.pawn.LabelShort, joySpot.LabelShort));
                    }
                    else
                    {
                        Log.Message(string.Format("[SocialInteractions] {0} is just standing and watching {1} at {2}", partner.LabelShort, this.pawn.LabelShort, joySpot.LabelShort));
                        partner.rotationTracker.FaceCell(this.pawn.Position);
                    }
                }
            };
            partnerActivity.defaultCompleteMode = ToilCompleteMode.Never;
            partnerActivity.tickAction = () =>
            {
                // Ensure partner continues to gain social joy
                partner.needs.joy.GainJoy(0.0005f, JoyKindDefOf.Social);

                // End this toil when the initiator's joy job ends
                if (this.pawn.needs.joy.CurLevelPercentage >= 0.99f)
                {
                    partnerActivity.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            yield return partnerActivity;

            // Final toil to end the job for both pawns
            Toil endJob = new Toil();
            endJob.initAction = () =>
            {
                this.pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                if (partner != null && partner.jobs != null && partner.jobs.curJob != null && partner.jobs.curJob.def == DefDatabase<JobDef>.GetNamed("OnDate"))
                {
                    partner.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };
            endJob.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return endJob;
        }
    }
}