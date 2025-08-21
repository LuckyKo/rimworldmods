using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using System;
using Verse.Utility;

namespace SocialInteractions
{
    public class JobDriver_GoOnDate : JobDriver
    {
        private Pawn Partner
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }
        
        private JobDef lastKnownInitiatorJobDef = null;
        private const int JoyJobJoinDelay = 180; // 3 seconds delay before trying to join joy job
        private int? initiatorJoyJobStartTick = null; // Track when the initiator started a joy job
        private bool partnerHasStartedJoyJob = false; // Track if partner has started a joy job for current joy activity
        private JobDef partnerJoyJobDef = null; // Track the joy job the partner is doing

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            SLog.Message("[SocialInteractions] JobDriver_GoOnDate: TryMakePreToilReservations called.");
            // Add null check to prevent NullReferenceException
            if (this.pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: pawn is null in TryMakePreToilReservations.");
                return false;
            }
            SLog.Message("[SocialInteractions] JobDriver_GoOnDate: TryMakePreToilReservations returning true.");
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void Notify_Starting()
        {
            SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Notify_Starting called.");
            base.Notify_Starting();
            // Add null checks to prevent NullReferenceException
            string pawnName = (this.pawn != null && this.pawn.Name != null) ? this.pawn.Name.ToStringShort : "NULL";
            string partnerName = (this.Partner != null && this.Partner.Name != null) ? this.Partner.Name.ToStringShort : "NULL";
            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Starting job for {0} to date {1}.", pawnName, partnerName));
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SLog.Message("[SocialInteractions] JobDriver_GoOnDate: MakeNewToils called.");
            
            // Add null check for pawn
            if (this.pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: pawn is null in MakeNewToils, ending job.");
                yield break;
            }

            // Fail if the partner is null or despawned
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // Check if recipient is within range
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
                
                int maxDistance = SocialInteractions.Settings.maxDistanceForDate; // 50x50 tiles
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
                if (recipient == null || this.pawn == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in askToil, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Calculate acceptance chance based on opinion and mood
                float baseChance = 0.95f; // 95% base chance
                
                // Factor in the recipient's opinion of the initiator
                int opinion = recipient.relations.OpinionOf(this.pawn);
                float opinionFactor = System.Math.Max(0f, System.Math.Min(1f, 0.5f + (opinion / 100f))); // Convert opinion to a 0-1 factor
                
                // Factor in the recipient's current mood
                float moodFactor = 1.0f;
                if (recipient.needs != null && recipient.needs.mood != null)
                {
                    // Convert mood level (0.0 to 1.0) to a factor between 0.5 and 1.5
                    // When mood is very low (0.0), factor is 0.5 (reduces chance)
                    // When mood is neutral (0.5), factor is 1.0 (no effect)
                    // When mood is very high (1.0), factor is 1.5 (increases chance)
                    moodFactor = 0.5f + recipient.needs.mood.CurLevelPercentage;
                }
                
                // Calculate final chance as base chance adjusted by both factors
                float finalChance = baseChance * opinionFactor * moodFactor;
                
                // Cap the chance at 100%
                finalChance = System.Math.Min(finalChance, 1.0f);
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Calculating acceptance chance for {0} asking {1}. Base: {2}, Opinion: {3}, Mood Factor: {4}, Final: {5}", 
                    this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, baseChance, opinion, moodFactor, finalChance));

                // Roll for acceptance
                float roll = Rand.Value;
                bool accepted = roll < finalChance;
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Acceptance roll for {0} asking {1}. Rolled: {2}, Chance: {3}, Accepted: {4}", 
                    this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, roll, finalChance, accepted));

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
                    SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateRejected, SpeechBubbleManager.GetDateRejectionSubject(this.pawn, this.Partner));
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
                
                // Store the job def for monitoring
                lastKnownInitiatorJobDef = joyJob.def;
                
                // Replace this job with the actual joy job
                this.pawn.jobs.StartJob(joyJob, JobCondition.InterruptForced);
                
                // End this job successfully since we've set up the date
                this.EndJobWith(JobCondition.Succeeded);
            };
            findJoyJobAndAssign.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findJoyJobAndAssign;
        }

        private void TryHavePartnerJoinJoyActivity(JobDef joyJobDef)
        {
            // Check if the partner's joy need is high enough that they don't want to join
            if (this.Partner.needs != null && this.Partner.needs.joy != null && 
                this.Partner.needs.joy.CurLevelPercentage >= 0.95f)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner {0}'s joy level is too high to join activity", 
                    this.Partner.Name.ToStringShort));
                return;
            }
            
            // Find the joy giver for this job def
            JoyGiverDef initiatorJoyGiver = null;
            foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
            {
                if (joyGiver.jobDef == joyJobDef)
                {
                    initiatorJoyGiver = joyGiver;
                    break;
                }
            }
            
            if (initiatorJoyGiver == null)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Could not find joy giver for job def {0}", 
                    joyJobDef.defName));
                return;
            }
            
            // Try to give the partner the same joy job as the initiator
            Job partnerJoyJob = initiatorJoyGiver.Worker.TryGiveJob(this.Partner);
            if (partnerJoyJob == null)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner {0} cannot do the same joy activity as initiator {1}, will continue following.", 
                    this.Partner.Name.ToStringShort, this.pawn.Name.ToStringShort));
                return;
            }
            
            // Check if the target locations match or are nearby
            bool targetsMatch = false;
            if (partnerJoyJob.targetA.Thing != null && this.pawn.CurJob.targetA.Thing != null)
            {
                targetsMatch = partnerJoyJob.targetA.Thing == this.pawn.CurJob.targetA.Thing;
            }
            else if (partnerJoyJob.targetA.Cell.IsValid && this.pawn.CurJob.targetA.Cell.IsValid)
            {
                targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(this.pawn.CurJob.targetA.Cell) <= 7f;
            }
            
            if (targetsMatch)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Partner {0} is joining initiator {1} in joy activity {2}.", 
                    this.Partner.Name.ToStringShort, this.pawn.Name.ToStringShort, partnerJoyJob.def.defName));
                
                // Track the joy job we're starting for the partner
                partnerJoyJobDef = partnerJoyJob.def;
                
                // Enqueue the joy job and then interrupt the current job for a smooth transition
                this.Partner.jobs.jobQueue.EnqueueFirst(partnerJoyJob);
                this.Partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Target locations don't match for partner {0} and initiator {1}", 
                    this.Partner.Name.ToStringShort, this.pawn.Name.ToStringShort));
            }
        }

        private Job FindJoyJobFor(Pawn initiator, Pawn partner)
        {
            if (initiator == null || partner == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: initiator or partner is null in FindJoyJobFor, returning null.");
                return null;
            }

            // Get all joy givers
            IEnumerable<JoyGiverDef> joyGivers = DefDatabase<JoyGiverDef>.AllDefs.OrderByDescending(jg => jg.Worker.GetChance(initiator));

            // Create a list of joy givers with their weights (using base chance as weight)
            List<Pair<JoyGiverDef, float>> weightedJoyGivers = new List<Pair<JoyGiverDef, float>>();
            float totalWeight = 0f;

            foreach (JoyGiverDef joyGiverDef in joyGivers)
            {
                float weight = joyGiverDef.Worker.GetChance(initiator);
                
                if (weight > 0)
                {
                    weightedJoyGivers.Add(new Pair<JoyGiverDef, float>(joyGiverDef, weight));
                    totalWeight += weight;
                }
            }

            // If we have no valid joy givers, return null
            if (weightedJoyGivers.Count == 0 || totalWeight <= 0)
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: No suitable joy job found for {0}.", initiator.Name.ToStringShort));
                return null;
            }

            // Try multiple times to find a suitable job using weighted random selection
            const int maxAttempts = 10;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Select a joy giver using weighted random selection
                float randomValue = Rand.Value * totalWeight;
                float currentWeight = 0f;
                JoyGiverDef selectedJoyGiverDef = null;

                foreach (var pair in weightedJoyGivers)
                {
                    currentWeight += pair.Second;
                    if (randomValue <= currentWeight)
                    {
                        selectedJoyGiverDef = pair.First;
                        break;
                    }
                }

                // If we didn't select a joy giver (shouldn't happen), default to the first one
                if (selectedJoyGiverDef == null)
                {
                    selectedJoyGiverDef = weightedJoyGivers[0].First;
                }

                try
                {
                    // Try to get a job from this joy giver
                    Job joyJob = selectedJoyGiverDef.Worker.TryGiveJob(initiator);
                    if (joyJob != null)
                    {
                        // Skip jobs that don't have a valid target
                        if (joyJob.targetA.Thing == null && !joyJob.targetA.Cell.IsValid)
                        {
                            // Clean up the job since we're not using it
                            if (joyJob.def != null)
                            {
                                // No need to explicitly clean up, the job will be garbage collected
                            }
                            continue;
                        }
                        
                        // Determine the correct PathEndMode based on whether the target is a Thing or a Cell
                        PathEndMode pathEndMode = joyJob.targetA.HasThing ? PathEndMode.InteractionCell : PathEndMode.OnCell;

                        // Try to reserve the target for both pawns
                        if (initiator.CanReserveAndReach(joyJob.targetA, pathEndMode, Danger.None) &&
                            partner.CanReserveAndReach(joyJob.targetA, pathEndMode, Danger.None))
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Found suitable joy job {0} for {1} at {2} with PathEndMode {3}.", 
                                joyJob.def.defName, initiator.Name.ToStringShort, joyJob.targetA.ToString(), pathEndMode));
                            return joyJob;
                        }
                        // If we can't reserve it, we try again with a different joy giver
                        // No need to explicitly clean up the job
                    }
                }
                catch (NullReferenceException nre)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: NullReferenceException while trying joy giver {0}: {1}", 
                        selectedJoyGiverDef.defName, nre.Message));
                    // Continue to the next attempt
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: Exception while trying joy giver {0}: {1}", 
                        selectedJoyGiverDef.defName, ex.Message));
                    // Continue to the next attempt
                }
            }
            
            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: No suitable joy job found for {0} after {1} attempts.", initiator.Name.ToStringShort, maxAttempts));
            return null;
        }
    }
}