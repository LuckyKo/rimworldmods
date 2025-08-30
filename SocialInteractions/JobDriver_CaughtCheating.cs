using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine; // Added for Texture2D
using System; // Added for Exception

namespace SocialInteractions
{
    public class JobDriver_CaughtCheating : JobDriver
    {
        // Static field for the speech bubble icon
        private static readonly Texture2D moteIcon = ContentFinder<Texture2D>.Get("Things/Mote/SpeechSymbols/Speech");

        private Pawn Cheater
        {
            get
            {
                return (Pawn)job.targetA.Thing;
            }
        }

        private new int startTick = -1;
        private int conversationId = -1; // Store the conversation ID for this interaction
        
        // Use the settings value as minimum duration
        private int MinWaitDuration { get { return SocialInteractions.Settings.cheatingConfrontationTicks; } }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: TryMakePreToilReservations called.");
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            startTick = Find.TickManager.TicksGame;
            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Notify_Starting called for pawn {0} to confront cheater {1}. Start tick: {2}", 
                pawn != null ? pawn.LabelShort : "NULL", 
                Cheater != null ? Cheater.LabelShort : "NULL",
                startTick));
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: MakeNewToils called.");
            
            // Add null checks
            if (pawn == null || job == null || job.targetA.Thing == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_CaughtCheating: Pawn, job, or job.targetA.Thing is null, ending job.");
                yield break;
            }

            Pawn cheater = (Pawn)job.targetA.Thing;

            // Make sure we're still near the cheater
            if (!pawn.Position.InHorDistOf(cheater.Position, 5f))
            {
                SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Pawn is too far from cheater, ending job.");
                yield break;
            }

            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Pawn {0} is near cheater {1}, proceeding with job.", 
                pawn.LabelShort, cheater.LabelShort));

            // Hold the cheater in place during the dialogue
            Pawn_Tick_Patch.HoldPawnInPlace(cheater, cheater.Position);

            // Retrieve the date partner for the cheater
            Pawn partner = null;
            if (SocialInteractions.CheaterPartners.ContainsKey(cheater.ThingID))
            {
                partner = SocialInteractions.CheaterPartners[cheater.ThingID];
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Retrieved partner: {0}", partner.LabelShort));
            }
            else
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: No partner found for cheater {0}", cheater.LabelShort));
            }

            // Create a BeTalkedTo job for the cheater to hold them in place during the conversation
            Job beTalkedToJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), pawn);
            cheater.jobs.TryTakeOrderedJob(beTalkedToJob, JobTag.Misc);
            
            // Trigger the LLM interaction with the partner and store the conversation ID
            conversationId = SocialInteractions.HandleCaughtCheatingInteraction(pawn, cheater, partner);
            
            // Make the partner flee immediately upon the spouse's arrival
            if (partner != null)
            {
                InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
                interactionWorker.MakePartnerFleeImmediately(partner, pawn); // pawn is the angry spouse
            }
            
            // Custom wait toil to wait for the conversation to finish
            Toil waitToil = new Toil();
            waitToil.initAction = () => 
            {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Wait toil initAction called for pawn {0}. Start tick: {1}", 
                    pawn.LabelShort, startTick));

                // Create the speech bubble mote when the pawn starts waiting (i.e., confronting)
                try
                {
                    if (moteIcon != null)
                    {
                        MoteMaker.MakeSpeechBubble(pawn, moteIcon);
                    }
                }
                catch (System.Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while creating speech bubble for pawn {0}: {1}", pawn.LabelShort, ex.Message));
                }
            };
            
            // Play the appropriate speech sound based on gender using Toil's method
            waitToil.PlaySustainerOrSound(() => (pawn.gender != Gender.Female) ? SoundDefOf.Speech_Leader_Male : SoundDefOf.Speech_Leader_Female, pawn.story.VoicePitchFactor);
            
            waitToil.tickAction = () => 
            {
                // Check if the minimum wait duration has elapsed
                int elapsedTicks = Find.TickManager.TicksGame - startTick;
                bool minDurationElapsed = elapsedTicks >= MinWaitDuration;
                
                // Check if the specific conversation for this cheating interaction is still active or has pending speech bubbles
                bool isConversationFinished = true;
                try
                {
                    // Check if this conversation is still active or has pending speech bubbles
                    if (conversationId != -1)
                    {
                        isConversationFinished = !SpeechBubbleManager.IsConversationActive(conversationId) && !SpeechBubbleManager.HasPendingSpeechBubbles(conversationId);
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while checking conversation status: {0}", ex.Message));
                }
                
                // If minimum duration has elapsed and the conversation is finished, end the confrontation
                if (minDurationElapsed && isConversationFinished)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Wait duration elapsed for pawn {0}.", pawn.LabelShort));
                    
                    // Remove the partner from the CheaterPartners dictionary
                    Pawn cheaterPawn = (Pawn)job.targetA.Thing;
                    if (cheaterPawn != null && SocialInteractions.CheaterPartners.ContainsKey(cheaterPawn.ThingID))
                    {
                        SocialInteractions.CheaterPartners.Remove(cheaterPawn.ThingID);
                    }
                    
                    // End the date when the angry spouse arrives and the waiting period is over
                    Date date = DatingManager.GetDateWith(cheaterPawn);
                    if (date != null)
                    {
                        SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Ending date as angry spouse has arrived.");
                        DatingManager.EndDate(date);
                    }
                    else
                    {
                        // If we can't get the date from the cheater, try to get it from the partner
                        if (partner != null)
                        {
                            date = DatingManager.GetDateWith(partner);
                            if (date != null)
                            {
                                SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Ending date as angry spouse has arrived (from partner).");
                                DatingManager.EndDate(date);
                            }
                        }
                    }
                    
                    // Remove the OnDate hediff from the partner if they still have it
                    if (partner != null)
                    {
                        HediffDef onDateDef = HediffDef.Named("OnDate");
                        if (onDateDef != null)
                        {
                            try
                            {
                                Hediff onDateHediff = partner.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                                if (onDateHediff != null)
                                {
                                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Removing OnDate hediff from partner {0}.", partner.LabelShort));
                                    partner.health.RemoveHediff(onDateHediff);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception removing OnDate hediff from partner {0}: {1}", partner.LabelShort, ex.Message));
                            }
                        }
                    }
                    
                    // Trigger fight logic
                    InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
                    interactionWorker.TriggerFightLogic(pawn, cheaterPawn, partner); // Pass the partner we retrieved earlier
                    
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Fight logic triggered for pawn {0}.", pawn.LabelShort));
                    
                    // Check if a social fight was successfully started
                    if (pawn.mindState != null && 
                        pawn.mindState.mentalStateHandler != null &&
                        pawn.mindState.mentalStateHandler.InMentalState && 
                        pawn.mindState.mentalStateHandler.CurState.def == MentalStateDefOf.SocialFighting)
                    {
                        // A social fight was successfully started
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Pawn {0} is in social fight, ending job to let mental state take over.", pawn.LabelShort));
                        // End the conversation before ending the job
                        if (conversationId != -1)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Ending conversation ID: {0}", conversationId));
                            SpeechBubbleManager.EndConversation(conversationId);
                        }
                        // End the job and let the mental state handle the fighting
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    else
                    {
                        // If no fight was started, end the job
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: No fight started for pawn {0}, ending job.", pawn.LabelShort));
                        // End the conversation before ending the job
                        if (conversationId != -1)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Ending conversation ID: {0}", conversationId));
                            SpeechBubbleManager.EndConversation(conversationId);
                        }
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                }
            };
            waitToil.defaultCompleteMode = ToilCompleteMode.Never; // We'll complete it manually or let mental state take over
            yield return waitToil;
            
            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: MakeNewToils finished for pawn {0}.", pawn.LabelShort));
        }
    }
}