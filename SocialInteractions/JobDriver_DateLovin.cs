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

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn == null || Partner == null)
            {
                return false;
            }
            return pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PartnerInd);
            this.FailOn(() => !Partner.health.capacities.CanBeAwake);

            yield return Toils_Goto.GotoCell(BedPosInd, PathEndMode.OnCell);

            // Store references to both pawns to ensure we can access them later
            Pawn initiator = pawn;
            Pawn partner = Partner;

            Toil lovinToil = ToilMaker.MakeToil("LovinToil");
            lovinToil.initAction = delegate
            {
                ticksLeft = SocialInteractions.Settings.dateLovinTicks;
                SLog.Message(string.Format("[SocialInteractions] Adding SI_Naked hediff to {0} and {1}",
                    initiator != null ? initiator.LabelShort : "NULL",
                    partner != null ? partner.LabelShort : "NULL"));

                // Add null checks before adding hediff
                if (initiator != null && initiator.health != null)
                {
                    initiator.health.AddHediff(HediffDef.Named("SI_Naked"));
                }

                if (partner != null && partner.health != null)
                {
                    partner.health.AddHediff(HediffDef.Named("SI_Naked"));
                }
            };
            lovinToil.tickAction = delegate
            {
                // Add null checks to prevent NullReferenceException
                if (initiator == null || initiator.jobs == null)
                {
                    return;
                }

                if (initiator.jobs.curDriver != this)
                {
                    return;
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
                            if (initiator != null && partnerFromDate != null)
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
                                    Pawn malePawn = ((initiator.gender == Gender.Male) ? initiator : ((partnerFromDate.gender == Gender.Male) ? partnerFromDate : null));
                                    Pawn femalePawn = ((initiator.gender == Gender.Female) ? initiator : ((partnerFromDate.gender == Gender.Female) ? partnerFromDate : null));
                                    
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
                    SLog.Message(string.Format("[SocialInteractions] Removing SI_Naked hediff from {0} and {1}",
                        initiator != null ? initiator.LabelShort : "NULL",
                        partner != null ? partner.LabelShort : "NULL"));

                    if (initiator != null && initiator.health != null && initiator.health.hediffSet != null)
                    {
                        Hediff hediff = initiator.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                        if (hediff != null)
                        {
                            initiator.health.RemoveHediff(hediff);
                            SLog.Message(string.Format("[SocialInteractions] SI_Naked hediff removed from {0}", initiator.LabelShort));
                        }
                    }

                    if (partner != null && partner.health != null && partner.health.hediffSet != null)
                    {
                        Hediff hediff = partner.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                        if (hediff != null)
                        {
                            partner.health.RemoveHediff(hediff);
                            SLog.Message(string.Format("[SocialInteractions] SI_Naked hediff removed from {0}", partner.LabelShort));
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

                // Male pawns bounce on X axis, female pawns bounce on Z axis
                if (pawn == initiator ^ initiator.gender == Gender.Female)
                {
                    // Initiator bounces on X
                    float num2 = Mathf.Sign(num);
                    return new Vector3(EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2, 0f, 0f);
                }
                else
                {
                    // Parner bounces on Z
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
