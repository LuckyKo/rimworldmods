using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_AskForDate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            Log.Message("[SocialInteractions] JobDriver_AskForDate: MakeNewToils entered.");
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil askOut = new Toil();
            askOut.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                Log.Message(string.Format("[SocialInteractions] Initiator ({0}) current job: {1}", this.pawn.Name.ToStringShort, this.pawn.jobs.curJob != null ? this.pawn.jobs.curJob.ToString() : "None"));
                Log.Message(string.Format("[SocialInteractions] Recipient ({0}) current job: {1}", recipient.Name.ToStringShort, recipient.jobs.curJob != null ? recipient.jobs.curJob.ToString() : "None"));
                
                bool interactionLogged = this.pawn.interactions.TryInteractWith(recipient, DefDatabase<InteractionDef>.GetNamed("AskForDate"));
                Log.Message(string.Format("[SocialInteractions] TryInteractWith (AskForDate) logged: {0}", interactionLogged));
                if (!interactionLogged)
                {
                    Log.Message(string.Format("[SocialInteractions] AskForDate interaction not logged for {0} with {1}. Ending job.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort));
                    this.EndJobWith(JobCondition.Incompletable);
                    return; // Exit initAction early
                };
            };
            askOut.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return askOut;

            yield return Toils_General.Wait(10); // Wait a few ticks for the interaction to process

            Toil handleResponse = new Toil();
            handleResponse.initAction = () =>
            {
                Pawn recipient = (Pawn)this.job.targetA.Thing;
                bool accepted = recipient.relations.OpinionOf(this.pawn) > 10 && Rand.Value > 0.5f;
                Log.Message(string.Format("[SocialInteractions] Date acceptance for {0} and {1}: {2}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, accepted));

                if (accepted)
                {
                    // Find a suitable spot for the date
                    Thing joySpot = FindJoySpotFor(this.pawn, recipient);
                    Log.Message(string.Format("[SocialInteractions] Joy spot found: {0}", joySpot != null));
                    if (joySpot != null)
                    {
                        Job initiatorJob = new Job(DefDatabase<JobDef>.GetNamed("OnDate"), recipient, joySpot);

                        bool initiatorJobStarted = this.pawn.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);
                        Log.Message(string.Format("[SocialInteractions] Initiator job started: {0}. Current job: {1}", initiatorJobStarted, this.pawn.jobs.curJob));

                        Job recipientJob = new Job(DefDatabase<JobDef>.GetNamed("OnDate"), this.pawn, joySpot);
                        recipient.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        bool recipientJobStarted = recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);
                        Log.Message(string.Format("[SocialInteractions] Recipient job started: {0}. Current job: {1}", recipientJobStarted, recipient.jobs.curJob));

                        if (initiatorJobStarted && recipientJobStarted)
                        {
                            // Notify the player
                            Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort), new LookTargets(this.pawn, recipient), MessageTypeDefOf.PositiveEvent);
                        }
                        else
                        {
                            Log.Message(string.Format("[SocialInteractions] Date job failed for {0} and {1}. Initiator job started: {2}, Recipient job started: {3}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, initiatorJobStarted, recipientJobStarted));
                            this.pawn.interactions.TryInteractWith(recipient, SI_InteractionDefOf.DateRejected);
                        }
                    }
                    else
                    {
                        Log.Message(string.Format("[SocialInteractions] No joy giver def found for {0}. Defaulting to chitchat.", joySpot.def.defName));
                        this.pawn.interactions.TryInteractWith(recipient, SI_InteractionDefOf.DateRejected);
                    }
                }
                else
                {
                    // The date was rejected
                    Log.Message(string.Format("[SocialInteractions] Date rejected by {0}. Defaulting to chitchat.", recipient.Name.ToStringShort));
                    this.pawn.interactions.TryInteractWith(recipient, SI_InteractionDefOf.DateRejected);
                }
            };
            handleResponse.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return handleResponse;
        }

        private Thing FindJoySpotFor(Pawn pawn, Pawn partner)
        {
            return pawn.Map.listerThings.AllThings
                .Where(t => t.def.building != null &&
                             DefDatabase<JoyGiverDef>.AllDefsListForReading.Any(j => j.thingDefs != null && j.thingDefs.Contains(t.def)) &&
                             t.def.GetStatValueAbstract(StatDefOf.JoyGainFactor) > 0 &&
                             JoyUtility.EnjoyableOutsideNow(pawn.Map) &&
                             pawn.CanReserveAndReach(t, PathEndMode.InteractionCell, Danger.None) &&
                             partner.CanReserveAndReach(t, PathEndMode.InteractionCell, Danger.None))
                .OrderBy(t => t.Position.DistanceTo(pawn.Position))
                .FirstOrDefault();
        }
    }
}