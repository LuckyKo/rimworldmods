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
            if (pawn == null || Cheater == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_CaughtCheating: Pawn or Cheater is null, ending job.");
                yield break;
            }

            // Make sure we're still near the cheater
            if (!pawn.Position.InHorDistOf(Cheater.Position, 5f))
            {
                SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Pawn is too far from cheater, ending job.");
                yield break;
            }

            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Pawn {0} is near cheater {1}, proceeding with job.", 
                pawn.LabelShort, Cheater.LabelShort));

            // Retrieve the date partner for the cheater
            Pawn partner = null;
            if (SocialInteractions.CheaterPartners.ContainsKey(Cheater.ThingID))
            {
                partner = SocialInteractions.CheaterPartners[Cheater.ThingID];
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Retrieved partner: {0}", partner.LabelShort));
            }
            else
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: No partner found for cheater {0}", Cheater.LabelShort));
            }

            // Trigger the LLM interaction with the partner and store the conversation ID
            conversationId = SocialInteractions.HandleCaughtCheatingInteraction(pawn, Cheater, partner);
            
            // Make the partner flee immediately upon the spouse's arrival
            if (partner != null)
            {
                InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
                interactionWorker.MakePartnerFleeImmediately(partner, pawn); // pawn is the angry spouse
            }
            
            // Remove the partner from the dictionary
            if (SocialInteractions.CheaterPartners.ContainsKey(Cheater.ThingID))
            {
                SocialInteractions.CheaterPartners.Remove(Cheater.ThingID);
            }
            
            // Custom wait toil
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
                    
                    // End the date when the angry spouse arrives and the waiting period is over
                    Date date = DatingManager.GetDateWith(Cheater);
                    if (date != null)
                    {
                        SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Ending date as angry spouse has arrived.");
                        DatingManager.EndDate(date);
                    }
                    
                    // Get the partner (the one being cheated on)
                    // Pawn currentPartner = DatingManager.GetPartnerOfDateWith(Cheater); // This will be null since the date was ended
                    
                    // Trigger fight logic
                    InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
                    interactionWorker.TriggerFightLogic(pawn, Cheater, partner); // Pass the partner we retrieved earlier
                    
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Fight logic triggered for pawn {0}.", pawn.LabelShort));
                    
                    // Check if a social fight was successfully started
                    if (pawn.mindState != null && 
                        pawn.mindState.mentalStateHandler != null &&
                        pawn.mindState.mentalStateHandler.InMentalState && 
                        pawn.mindState.mentalStateHandler.CurState.def == MentalStateDefOf.SocialFighting)
                    {
                        // A social fight was successfully started
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Pawn {0} is in social fight, ending job to let mental state take over.", pawn.LabelShort));
                        // End the job and let the mental state handle the fighting
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    else
                    {
                        // If no fight was started, end the job
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: No fight started for pawn {0}, ending job.", pawn.LabelShort));
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