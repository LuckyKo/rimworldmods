using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SocialInteractions
{
    public class JobDriver_GoOnDate : JobDriver
    {
        private Pawn Partner
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            // Add null checks to prevent NullReferenceException
            string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
            string partnerName = (this.Partner != null && this.Partner.Name != null) ? this.Partner.Name.ToStringShort : "NULL";
            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Starting job for {0} to date {1}.", pawnName, partnerName));
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null checks to prevent NullReferenceException
            string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
            string partnerName = (this.Partner != null && this.Partner.Name != null) ? this.Partner.Name.ToStringShort : "NULL";
            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: TryMakePreToilReservations for {0} to date {1}.", pawnName, partnerName));
            
            // Make sure we can reserve the partner
            if (this.pawn == null || this.Partner == null)
            {
                return false;
            }
            
            // Reserve the partner for this job
            return this.pawn.Reserve(this.Partner, this.job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Partner

            Toil rangeCheck = new Toil();
            rangeCheck.initAction = () =>
            {
                Pawn recipient = this.Partner;
                // Add null checks
                if (recipient == null || this.pawn == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in rangeCheck, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                int maxDistance = 50; // 50x50 tiles
                if ((Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)) > maxDistance)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Aborting job. Recipient {0} is too far from initiator {1}. Distance: {2}, Max Distance: {3}", recipient.Name.ToStringShort, this.pawn.Name.ToStringShort, (Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)), maxDistance));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            rangeCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return rangeCheck;

            // Go to the partner
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Ask for the date
            Toil askToil = new Toil();
            askToil.initAction = () => {
                Pawn recipient = this.Partner;
                // Add comprehensive null checks
                if (this.pawn == null || recipient == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in askToil, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                float acceptanceChance = 0.5f;
                if (recipient.relations != null)
                {
                    int opinion = recipient.relations.OpinionOf(this.pawn);
                    acceptanceChance += opinion / 200f;
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Calculating acceptance chance for {0} asking {1}. Base: 0.5, Opinion: {2}, Final: {3}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, opinion, acceptanceChance));
                }
                bool accepted = Rand.Value < acceptanceChance;
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Acceptance roll for {0} asking {1}. Rolled: {2}, Chance: {3}, Accepted: {4}", this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, Rand.Value, acceptanceChance, accepted));

                if (accepted)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Date accepted between {0} and {1}.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort));
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateAccepted"), this.pawn, this.Partner, null));
                    DatingManager.StartDate(this.pawn, this.Partner);
                    Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort), new LookTargets(this.pawn, this.Partner), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Date rejected between {0} and {1}.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort));
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateRejected"), this.pawn, this.Partner, null));
                    DatingManager.RejectDate(this.pawn, this.Partner);
                    SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateRejected, "date");
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            askToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return askToil;

            // Find a joy job and assign jobs
            Toil findJoyJobAndAssign = new Toil();
            findJoyJobAndAssign.initAction = () =>
            {
                // Add null checks
                if (this.pawn == null || this.Partner == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or Partner is null in findJoyJobAndAssign, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: findJoyJobAndAssign initAction called for pawn {0}.", this.pawn != null ? this.pawn.Name.ToStringShort : "NULL"));
                
                // Find a suitable joy job for the initiator
                Job joyJob = FindJoyJobFor(this.pawn, this.Partner);
                if (joyJob == null)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Could not find a suitable joy job for pawn {0}.", this.pawn.Name.ToStringShort));
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // --- Trigger LLM Interaction with correct subject ---
                string dateSubject = SpeechBubbleManager.GetDateSubject(this.pawn, this.Partner, joyJob.targetA);
                SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateAccepted, dateSubject);
                // --- End LLM Interaction ---
                
                // Create the FollowAndWatch job for the partner
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Creating FollowAndWatch job for partner {0}", 
                    this.Partner != null ? this.Partner.Name.ToStringShort : "NULL"));
                Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, this.pawn);
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Starting FollowAndWatch job for partner {0}", 
                    this.Partner != null ? this.Partner.Name.ToStringShort : "NULL"));
                this.Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: FollowAndWatch job started for partner {0}", 
                    this.Partner != null ? this.Partner.Name.ToStringShort : "NULL"));
                
                // Replace this job with the actual joy job
                this.pawn.jobs.StartJob(joyJob, JobCondition.InterruptForced);
            };
            findJoyJobAndAssign.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findJoyJobAndAssign;
        }
        
        private Job FindJoyJobFor(Pawn initiator, Pawn partner)
        {
            if (initiator == null || partner == null || initiator.Map == null)
            {
                return null;
            }
            
            // Get all joy givers
            List<JoyGiverDef> joyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading;
            
            // Shuffle the list to randomize selection
            joyGivers.Shuffle();
            
            // Try to find a suitable joy job
            foreach (JoyGiverDef joyGiverDef in joyGivers)
            {
                if (joyGiverDef == null || joyGiverDef.Worker == null)
                {
                    continue;
                }
                
                try
                {
                    // Check if both pawns can do this joy activity
                    if (!joyGiverDef.Worker.CanBeGivenTo(initiator) || !joyGiverDef.Worker.CanBeGivenTo(partner))
                    {
                        continue;
                    }
                    
                    // Try to get a job for the initiator
                    Job joyJob = joyGiverDef.Worker.TryGiveJob(initiator);
                    if (joyJob != null && joyJob.def != null)
                    {
                        // Additional null checks for the job and its target
                        if (joyJob.targetA == null || joyJob.targetA.Thing == null)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: Joy giver {0} returned job with null target, skipping.", 
                                joyGiverDef.defName));
                            continue;
                        }
                        
                        // Try to reserve the target for both pawns
                        if (initiator.CanReserveAndReach(joyJob.targetA, PathEndMode.InteractionCell, Danger.None) &&
                            partner.CanReserveAndReach(joyJob.targetA, PathEndMode.InteractionCell, Danger.None))
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Found suitable joy job {0} for {1} at {2}.", 
                                joyJob.def.defName, initiator.Name.ToStringShort, joyJob.targetA.ToString()));
                            return joyJob;
                        }
                        // If we can't reserve it, we just continue to the next joy giver
                        // No need to explicitly clean up the job
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: Exception while trying joy giver {0}: {1}", 
                        joyGiverDef.defName, ex.Message));
                    // Continue to the next joy giver
                }
            }
            
            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: No suitable joy job found for {0}.", initiator.Name.ToStringShort));
            return null;
        }
    }
}