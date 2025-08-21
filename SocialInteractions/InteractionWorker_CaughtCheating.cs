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
            
            // After the LLM interaction, there's a chance for different outcomes:
            // 50% chance to break off with nothing but a bad memory
            // 40% chance to fight the cheater (recipient)
            // 10% chance to fight the partner (the one being cheated on)
            float fightRoll = Rand.Value;
            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Fight roll: {0}.", fightRoll));
            
            if (fightRoll < 0.5f)
            {
                // 50% chance: Break off with nothing but a bad memory
                // No fight, just let the interaction end naturally
                SLog.Message("[SocialInteractions] TriggerFightLogic: 50% chance - breaking off with no fight.");
                return;
            }
            else if (fightRoll < 0.9f)
            {
                // 40% chance: Fight the cheater (recipient)
                SLog.Message("[SocialInteractions] TriggerFightLogic: 40% chance - evaluating fight with cheater.");
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
                    SLog.Message("[SocialInteractions] TriggerFightLogic: All conditions met, starting fight with cheater.");
                    bool fightStarted = initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, recipient);
                    SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: TryStartMentalState returned: {0}", fightStarted));
                    if (!fightStarted)
                    {
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Failed to start fight with cheater. Checking why...");
                        // Check why the fight failed to start
                        if (initiator.mindState.mentalStateHandler.CurState != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Initiator already in mental state: {0}", initiator.mindState.mentalStateHandler.CurState.def.defName));
                        }
                        if (!initiator.mindState.mentalStateHandler.InMentalState)
                        {
                            SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator mental state handler reports not in mental state.");
                        }
                    }
                }
                else
                {
                    SLog.Message("[SocialInteractions] TriggerFightLogic: Conditions not met for fighting cheater.");
                    // Log which conditions failed
                    if (initiator.Faction != recipient.Faction)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Faction mismatch between initiator and recipient.");
                    if (initiator.mindState == null)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator mindState is null.");
                    if (initiator.mindState != null && initiator.mindState.mentalStateHandler == null)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator mentalStateHandler is null.");
                    if (initiator.Downed)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is downed.");
                    if (initiator.Dead)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is dead.");
                    if (recipient.Downed)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Recipient is downed.");
                    if (recipient.Dead)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Recipient is dead.");
                    if (!initiator.Spawned)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is not spawned.");
                    if (!recipient.Spawned)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Recipient is not spawned.");
                    if (!initiator.Awake())
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is not awake.");
                    if (!recipient.Awake())
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Recipient is not awake.");
                    if (initiator.Spawned && recipient.Spawned && !SocialInteractionUtility.CanInitiateInteraction(initiator))
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator cannot initiate interaction.");
                    if (initiator.Spawned && recipient.Spawned && !SocialInteractionUtility.CanReceiveInteraction(recipient))
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Recipient cannot receive interaction.");
                }
            }
            else
            {
                // 10% chance: Fight the partner (the one being cheated on)
                SLog.Message("[SocialInteractions] TriggerFightLogic: 10% chance - evaluating fight with partner.");
                if (partner != null &&
                    initiator.Faction == partner.Faction && 
                    initiator.mindState != null && 
                    initiator.mindState.mentalStateHandler != null &&
                    !initiator.Downed && !initiator.Dead && 
                    !partner.Downed && !partner.Dead &&
                    initiator.Spawned && partner.Spawned &&
                    initiator.Awake() && partner.Awake() &&
                    SocialInteractionUtility.CanInitiateInteraction(initiator) &&
                    SocialInteractionUtility.CanReceiveInteraction(partner))
                {
                    SLog.Message("[SocialInteractions] TriggerFightLogic: All conditions met, starting fight with partner.");
                    bool fightStarted = initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, partner);
                    SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: TryStartMentalState returned: {0}", fightStarted));
                    if (!fightStarted)
                    {
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Failed to start fight with partner. Checking why...");
                        // Check why the fight failed to start
                        if (initiator.mindState.mentalStateHandler.CurState != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Initiator already in mental state: {0}", initiator.mindState.mentalStateHandler.CurState.def.defName));
                        }
                        if (!initiator.mindState.mentalStateHandler.InMentalState)
                        {
                            SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator mental state handler reports not in mental state.");
                        }
                    }
                }
                else
                {
                    SLog.Message("[SocialInteractions] TriggerFightLogic: Conditions not met for fighting partner.");
                    // Log which conditions failed
                    if (partner == null)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Partner is null.");
                    if (partner != null && initiator.Faction != partner.Faction)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Faction mismatch between initiator and partner.");
                    if (initiator.mindState == null)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator mindState is null.");
                    if (initiator.mindState != null && initiator.mindState.mentalStateHandler == null)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator mentalStateHandler is null.");
                    if (initiator.Downed)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is downed.");
                    if (initiator.Dead)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is dead.");
                    if (partner != null && partner.Downed)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Partner is downed.");
                    if (partner != null && partner.Dead)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Partner is dead.");
                    if (!initiator.Spawned)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is not spawned.");
                    if (partner != null && !partner.Spawned)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Partner is not spawned.");
                    if (!initiator.Awake())
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator is not awake.");
                    if (partner != null && !partner.Awake())
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Partner is not awake.");
                    if (initiator.Spawned && partner != null && partner.Spawned && !SocialInteractionUtility.CanInitiateInteraction(initiator))
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Initiator cannot initiate interaction.");
                    if (initiator.Spawned && partner != null && partner.Spawned && !SocialInteractionUtility.CanReceiveInteraction(partner))
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Partner cannot receive interaction.");
                }
            }
        }
    }
}
