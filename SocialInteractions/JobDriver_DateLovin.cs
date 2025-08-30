using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace SocialInteractions
{
    public class JobDriver_DateLovin : JobDriver
    {
        /// <summary>
        /// Checks if a pawn is still valid for dating activities
        /// </summary>
        /// <param name="pawn">The pawn to check</param>
        /// <returns>True if the pawn is valid for dating, false otherwise</returns>
        private bool IsPawnValidForDating(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed)
            {
                return false;
            }
            
            if (pawn.InMentalState || pawn.health == null || pawn.health.capacities == null)
            {
                return false;
            }
            
            // Check if the pawn is capable of being awake (basic health check)
            if (!pawn.health.capacities.CanBeAwake)
            {
                return false;
            }
            
            // Check if the pawn is drafted
            if (pawn.Drafted)
            {
                return false;
            }
            
            // Check if the pawn is on a date in the Lovin stage
            // If so, they should not be doing other jobs
            if (DatingManager.IsOnDate(pawn))
            {
                Date date = DatingManager.GetDateWith(pawn);
                if (date != null && date.Stage == DateStage.Lovin)
                {
                    // If the pawn is not in the DateLovin job, they should not be doing other jobs
                    // But allow the job to start if there's no current job
                    if (pawn.jobs != null && pawn.jobs.curJob != null && pawn.jobs.curJob.def != SI_JobDefOf.DateLovin)
                    {
                        SLog.Message(string.Format("[SocialInteractions] IsPawnValidForDating: Pawn {0} is on a date in Lovin stage but not in DateLovin job.", pawn.LabelShort));
                        return false;
                    }
                }
            }
            
            return true;
        }
        private int ticksLeft;

        private TargetIndex PartnerInd = TargetIndex.A;
        private TargetIndex BedPosInd = TargetIndex.B;

        private Pawn Partner { get { return (Pawn)(Thing)job.GetTarget(PartnerInd); } }
        private IntVec3 BedPos { get { return job.GetTarget(BedPosInd).Cell; } }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
        }

        public override void Notify_Starting()
        {
            SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Notify_Starting called for pawn {0}. Job target: {1}", 
                pawn != null ? pawn.LabelShort : "NULL",
                Partner != null ? Partner.LabelShort : "NULL"));
            base.Notify_Starting();
            
            // When starting a DateLovin job, we want to make sure the pawn doesn't get interrupted by non-critical jobs
            // We'll clear any queued jobs
            if (pawn != null && pawn.jobs != null)
            {
                pawn.jobs.ClearQueuedJobs();
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: TryMakePreToilReservations called for pawn {0}. Job target: {1}", 
                pawn != null ? pawn.LabelShort : "NULL",
                Partner != null ? Partner.LabelShort : "NULL"));
                
            if (pawn == null || Partner == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in TryMakePreToilReservations.");
                return false;
            }

            // Use the helper method to check if both pawns are valid for dating
            if (!IsPawnValidForDating(pawn) || !IsPawnValidForDating(Partner))
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_DateLovin: pawn {0} or Partner {1} is not valid for dating in TryMakePreToilReservations.", 
                    pawn.LabelShort, Partner.LabelShort));
                return false;
            }

            // Reserve the partner to prevent interruptions
            if (!pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed))
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_DateLovin: Failed to reserve partner {0} for pawn {1}.", 
                    Partner.LabelShort, pawn.LabelShort));
                return false;
            }

            // When starting a DateLovin job, we want to make sure the pawn doesn't get interrupted by non-critical jobs
            // Clear any queued jobs
            if (pawn.jobs != null)
            {
                pawn.jobs.ClearQueuedJobs();
            }

            SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: TryMakePreToilReservations returning true for pawn {0}.", 
                pawn.LabelShort));
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PartnerInd);
            this.FailOn(() => !Partner.health.capacities.CanBeAwake);

            yield return Toils_Goto.GotoCell(BedPosInd, PathEndMode.OnCell);

            // Add a toil to wait for the partner to get into position
            Toil waitForPartnerToil = ToilMaker.MakeToil("WaitForPartner");
            waitForPartnerToil.initAction = delegate
            {
                // Set a reasonable timeout (300 ticks = 5 seconds)
                waitForPartnerToil.defaultDuration = 300;
            };
            waitForPartnerToil.tickAction = delegate
            {
                // Check if both pawns are within 1.5 cells of each other
                if (pawn.Position.DistanceTo(Partner.Position) <= 1.5f)
                {
                    // If they're close enough, proceed to the next toil
                    waitForPartnerToil.actor.jobs.curDriver.ReadyForNextToil();
                }
                // If not close enough, continue waiting until timeout
            };
            waitForPartnerToil.AddFinishAction(() => {
                // Check if we're moving to the next toil because we're close enough
                // or because of a timeout
                if (pawn.Position.DistanceTo(Partner.Position) <= 1.5f)
                {
                    // Both pawns are in position, start the LLM interaction
                    Date date = DatingManager.GetDateWith(pawn);
                    if (date != null)
                    {
                        SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, SpeechBubbleManager.GetDateLovinSubject(date.Initiator, date.Partner));
                    }
                }
                else
                {
                    // Timeout - end the date
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Timeout waiting for partner {0} to get in position for pawn {1}. Ending date.", 
                        Partner.LabelShort, pawn.LabelShort));
                    Date date = DatingManager.GetDateWith(pawn);
                    if (date != null)
                    {
                        DatingManager.EndDate(date);
                    }
                }
            });
            waitForPartnerToil.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return waitForPartnerToil;

            // Store references to both pawns to ensure we can access them later
            Pawn initiator = pawn;
            Pawn partner = Partner;

            Toil lovinToil = ToilMaker.MakeToil("LovinToil");
            lovinToil.initAction = delegate
            {
                // Check if the pawn is still on a date in the Lovin stage
                if (!DatingManager.IsOnDate(pawn))
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Pawn {0} is no longer on a date, ending job.", 
                        pawn != null ? pawn.LabelShort : "NULL"));
                    ReadyForNextToil();
                    return;
                }
                
                Date date = DatingManager.GetDateWith(pawn);
                if (date == null || date.Stage != DateStage.Lovin)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Date stage is not Lovin for pawn {0}, ending job.", 
                        pawn != null ? pawn.LabelShort : "NULL"));
                    ReadyForNextToil();
                    return;
                }
                
                ticksLeft = SocialInteractions.Settings.dateLovinTicks;
                // Don't add the SI_Naked hediff here - wait until the pawns actually start the lovin activity
            };
            lovinToil.tickAction = delegate
            {
                // Add null checks to prevent NullReferenceException
                if (initiator == null || initiator.jobs == null)
                {
                    return;
                }

                // Check if the job is still running
                if (initiator.jobs.curDriver != this)
                {
                    // This can happen if the job was interrupted or replaced
                    // Let's check if the pawn is still on a date in the Lovin stage
                    Date date = DatingManager.GetDateWith(initiator);
                    if (date != null && date.Stage == DateStage.Lovin)
                    {
                        // The pawn should still be in the DateLovin job
                        // Let's log this situation but continue processing
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Initiator {0} is on a date in Lovin stage but curDriver is not this job. Continuing processing.", 
                            initiator.LabelShort != null ? initiator.LabelShort : "NULL"));
                    }
                    else
                    {
                        // The pawn is no longer on a date in the Lovin stage
                        // End the toil
                        return;
                    }
                }
                
                // Re-validate partner reference
                Pawn currentPartner = Partner;
                if (currentPartner == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_DateLovin: Partner became null during tick, ending job.");
                    ReadyForNextToil();
                    return;
                }

                // Add the SI_Naked hediff when the lovin activity actually starts (first tick)
                // Only add it once
                if (ticksLeft == SocialInteractions.Settings.dateLovinTicks)
                {
                    SLog.Message(string.Format("[SocialInteractions] Adding SI_Naked hediff to {0} and {1}",
                        initiator != null ? initiator.LabelShort : "NULL",
                        currentPartner != null ? currentPartner.LabelShort : "NULL"));

                    // Add null checks before adding hediff
                    if (initiator != null && initiator.health != null)
                    {
                        initiator.health.AddHediff(HediffDef.Named("SI_Naked"));
                    }

                    if (currentPartner != null && currentPartner.health != null)
                    {
                        currentPartner.health.AddHediff(HediffDef.Named("SI_Naked"));
                    }
                }

                ticksLeft--;
                if (ticksLeft <= 0)
                {
                    // Handle thought-giving and date advancement here instead of in finishAction
                    try
                    {
                        // Get the partner from the date
                        Pawn partnerFromDate = null;
                        Date date = DatingManager.GetDateWith(initiator);

                        if (date != null)
                        {
                            if (date.Initiator == initiator)
                            {
                                partnerFromDate = date.Partner;
                            }
                            else if (date.Partner == initiator)
                            {
                                partnerFromDate = date.Initiator;
                            }

                            // Give thoughts to both pawns
                            if (initiator != null && partnerFromDate != null && currentPartner == partnerFromDate)
                            {
                                // Give thought to initiator
                                if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                                {
                                    var thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                                    thought.otherPawn = partnerFromDate;
                                    initiator.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                                }

                                // Give thought to partner
                                if (partnerFromDate.needs != null && partnerFromDate.needs.mood != null && partnerFromDate.needs.mood.thoughts != null && partnerFromDate.needs.mood.thoughts.memories != null)
                                {
                                    var thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                                    thought.otherPawn = initiator;
                                    partnerFromDate.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                                }
                                
                                // Handle pregnancy
                                if (ModsConfig.BiotechActive)
                                {
                                    Pawn malePawn = ((initiator.gender == Gender.Male) ? initiator : ((currentPartner.gender == Gender.Male) ? currentPartner : null));
                                    Pawn femalePawn = ((initiator.gender == Gender.Female) ? initiator : ((currentPartner.gender == Gender.Female) ? currentPartner : null));
                                    
                                    if (malePawn != null && femalePawn != null)
                                    {
                                        // Use the same pregnancy chance as vanilla lovin
                                        float pregnancyChance = 0.05f;
                                        
                                        if (Rand.Chance(pregnancyChance * PregnancyUtility.PregnancyChanceForPartners(femalePawn, malePawn)))
                                        {
                                            bool success;
                                            GeneSet inheritedGeneSet = PregnancyUtility.GetInheritedGeneSet(malePawn, femalePawn, out success);
                                            if (success)
                                            {
                                                Hediff_Pregnant hediff_Pregnant = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, femalePawn);
                                                hediff_Pregnant.SetParents(null, malePawn, inheritedGeneSet);
                                                femalePawn.health.AddHediff(hediff_Pregnant);
                                            }
                                            else if (PawnUtility.ShouldSendNotificationAbout(malePawn) || PawnUtility.ShouldSendNotificationAbout(femalePawn))
                                            {
                                                Messages.Message("MessagePregnancyFailed".Translate(malePawn.Named("FATHER"), femalePawn.Named("MOTHER")) + ": " + "CombinedGenesExceedMetabolismLimits".Translate(), new LookTargets(malePawn, femalePawn), MessageTypeDefOf.NegativeEvent);
                                            }
                                        }
                                    }
                                }
                            }

                            // Advance the date stage
                            if (date.Stage == DateStage.Lovin)
                            {
                                DatingManager.AdvanceDateStage(initiator);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception in DateLovin tickAction when handling thoughts/advancement: {0}", ex.Message));
                    }

                    ReadyForNextToil();
                }
                else if (initiator.IsHashIntervalTick(100))
                {
                    try
                    {
                        FleckMaker.ThrowMetaIcon(initiator.Position, initiator.Map, FleckDefOf.Heart);
                        if (initiator.needs != null && initiator.needs.joy != null)
                        {
                            initiator.needs.joy.GainJoy(0.05f, JoyKindDefOf.Social);
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception in DateLovin tickAction: {0}", ex.Message));
                    }
                }
            };
            lovinToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return lovinToil;

            Toil cleanupToil = ToilMaker.MakeToil("CleanupToil");
            cleanupToil.initAction = delegate
            {
                try
                {
                    // Re-validate partner reference
                    Pawn currentPartner = Partner;
                    
                    SLog.Message(string.Format("[SocialInteractions] Removing SI_Naked hediff from {0} and {1}",
                        initiator != null ? initiator.LabelShort : "NULL",
                        currentPartner != null ? currentPartner.LabelShort : "NULL"));

                    if (initiator != null && initiator.health != null && initiator.health.hediffSet != null)
                    {
                        Hediff hediff = initiator.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                        if (hediff != null)
                        {
                            initiator.health.RemoveHediff(hediff);
                            SLog.Message(string.Format("[SocialInteractions] SI_Naked hediff removed from {0}", initiator.LabelShort));
                        }
                    }

                    if (currentPartner != null && currentPartner.health != null && currentPartner.health.hediffSet != null)
                    {
                        Hediff hediff = currentPartner.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                        if (hediff != null)
                        {
                            currentPartner.health.RemoveHediff(hediff);
                            SLog.Message(string.Format("[SocialInteractions] SI_Naked hediff removed from {0}", currentPartner.LabelShort));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Exception removing SI_Naked hediff: {0}", ex.Message));
                }
            };
            yield return cleanupToil;
        }

        public override Vector3 ForcedBodyOffset
        {
            get
            {
                // Add safety checks to prevent NullReferenceException
                if (pawn == null)
                {
                    return Vector3.zero;
                }

                float num = Mathf.Sin((float)ticksLeft / 60f * 8f);
                Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);

                // If we can't get the initiator, just return zero offset
                if (initiator == null)
                {
                    return Vector3.zero;
                }

                // Re-validate partner reference
                Pawn currentPartner = Partner;
                if (currentPartner == null)
                {
                    return Vector3.zero;
                }

                // Male pawns bounce on X axis, female pawns bounce on Z axis
                if (pawn == initiator ^ initiator.gender == Gender.Female)
                {
                    // Initiator bounces on X
                    float num2 = Mathf.Sign(num);
                    return new Vector3(EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2, 0f, 0f);
                }
                else
                {
                    // Partner bounces on Z
                    float z = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
                    return new Vector3(0f, 0f, z);
                }
            }
        }

        private float EaseInOutQuad(float v)
        {
            if (!((double)v < 0.5))
            {
                return 1f - Mathf.Pow(-2f * v + 2f, 4f) / 2f;
            }
            return 8f * v * v * v * v;
        }
    }
}
