using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class InteractionWorker_CaughtCheating : InteractionWorker
    {
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // Add null checks to prevent exceptions
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_CaughtCheating: Initiator or recipient is null, skipping interaction.");
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Add a memory to the initiator (the one who caught the cheater)
            ThoughtDef caughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("CaughtCheating");
            if (caughtCheatingThought != null)
            {
                initiator.needs.mood.thoughts.memories.TryGainMemory(caughtCheatingThought, recipient);
            }

            // Add a memory to the recipient (the cheater who got caught)
            ThoughtDef gotCaughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("GotCaughtCheating");
            if (gotCaughtCheatingThought != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(gotCaughtCheatingThought, initiator);
            }

            // Add a memory to the partner (the one being cheated on)
            Pawn partner = DatingManager.GetPartnerOfDateWith(recipient);
            if (partner != null)
            {
                ThoughtDef wasCheatedOnThought = DefDatabase<ThoughtDef>.GetNamed("WasCheatedOn");
                if (wasCheatedOnThought != null)
                {
                    partner.needs.mood.thoughts.memories.TryGainMemory(wasCheatedOnThought, recipient);
                }
            }

            // Create a job to handle the interaction once the initiator arrives
            // The Goto job is already created by Pawn_Tick_Patch
            Job followUpJob = JobMaker.MakeJob(SI_JobDefOf.CaughtCheatingInteraction, recipient);
            initiator.jobs.jobQueue.EnqueueFirst(followUpJob);

            // Call base method for any additional logic
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }

        public void TriggerFightLogic(Pawn initiator, Pawn recipient, Pawn partner)
        {
            // Add null checks to prevent exceptions
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] TriggerFightLogic: Initiator or recipient is null, skipping fight logic.");
                return;
            }

            SLog.Message("[SocialInteractions] TriggerFightLogic: Starting fight logic evaluation.");
            
            // The partner always flees
            if (partner != null)
            {
                // Add a memory to the partner that they were cheated on (using existing WasCheatedOn thought)
                ThoughtDef wasCheatedOnThought = DefDatabase<ThoughtDef>.GetNamed("WasCheatedOn");
                if (wasCheatedOnThought != null)
                {
                    partner.needs.mood.thoughts.memories.TryGainMemory(wasCheatedOnThought, recipient);
                }
                
                // Make the partner flee from the initiator
                TryMakePartnerFlee(partner, initiator);
            }
            
            // Determine if we fight the cheater
            float fightRoll = Rand.Value;
			SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Fight roll: {0}.", fightRoll));
            
            if (fightRoll < 0.5f)
            {
                // 50% chance: Break off with nothing but a bad memory
                // No fight, just let the interaction end naturally
                SLog.Message("[SocialInteractions] TriggerFightLogic: 50% chance - breaking off with no fight.");
                return;
            }
            else
            {
                // 50% chance: Fight the cheater (recipient)
                if (initiator.Faction == recipient.Faction && 
                    initiator.mindState != null && 
                    initiator.mindState.mentalStateHandler != null &&
                    !initiator.Downed && !initiator.Dead && 
                    !recipient.Downed && !recipient.Dead &&
                    initiator.Spawned && recipient.Spawned &&
                    initiator.Awake() && recipient.Awake() &&
                    SocialInteractionUtility.CanInitiateInteraction(initiator) &&
                    SocialInteractionUtility.CanReceiveInteraction(recipient))
                {
                    bool fightStarted = initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, recipient);
                    if (!fightStarted)
                    {
                        // Log why the fight failed to start if needed for debugging
                        if (initiator.mindState.mentalStateHandler.CurState != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Initiator already in mental state: {0}", initiator.mindState.mentalStateHandler.CurState.def.defName));
                        }
                    }
                }
                else
                {
                    // Log which conditions failed if needed for debugging
                    if (initiator.Faction != recipient.Faction)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Faction mismatch between initiator and recipient.");
                }
            }
        }
        
        public void MakePartnerFleeImmediately(Pawn partner, Pawn initiator)
        {
            if (partner == null || initiator == null)
            {
                SLog.Warning("[SocialInteractions] MakePartnerFleeImmediately: Partner or initiator is null, skipping.");
                return;
            }
            
            SLog.Message(string.Format("[SocialInteractions] MakePartnerFleeImmediately: Making partner {0} flee from initiator {1}.", partner.LabelShort, initiator.LabelShort));
            TryMakePartnerFlee(partner, initiator);
        }
        
        private void TryMakePartnerFlee(Pawn partner, Pawn initiator)
        {
            if (partner == null || initiator == null || partner.Map == null)
            {
                return;
            }
            
            // Remove the SI_OnDate hediff as the partner is no longer on the date
            HediffDef onDateDef = HediffDef.Named("OnDate");
            if (onDateDef != null)
            {
                try
                {
                    Hediff onDateHediff = partner.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                    if (onDateHediff != null)
                    {
                        SLog.Message(string.Format("[SocialInteractions] TryMakePartnerFlee: Removing OnDate hediff from partner {0}.", partner.LabelShort));
                        partner.health.RemoveHediff(onDateHediff);
                    }
                }
                catch (System.Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] TryMakePartnerFlee: Exception removing OnDate hediff from partner {0}: {1}", partner.LabelShort, ex.Message));
                }
            }
            
            // Create a list of threats (in this case, just the initiator)
            List<Thing> threats = new List<Thing> { initiator };
            
            // Try to find a cell to flee to
            IntVec3 fleeCell = CellFinderLoose.GetFleeDest(partner, threats, 10f); // Flee 10 cells away
            
            if (fleeCell.IsValid && fleeCell != partner.Position)
            {
                // Create a job for the partner to go to the flee cell
                Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, fleeCell);
                fleeJob.locomotionUrgency = LocomotionUrgency.Sprint; // Make them sprint away
                fleeJob.expiryInterval = 900; // Expire the job after 10 seconds if not completed
                
                // Start the job
                partner.jobs.TryTakeOrderedJob(fleeJob);
            }
        }
    }
}
