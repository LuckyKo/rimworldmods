using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JoyGiver_GoOnDate : JoyGiver
    {
        private static Dictionary<Pawn, int> lastAttemptTick = new Dictionary<Pawn, int>();
        // private const int CooldownTicks = 600; // 10 seconds (60 ticks per second) (now configurable in settings)

        public override Job TryGiveJob(Pawn pawn)
        {
            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: TryGiveJob called for pawn {0}.", pawn != null ? pawn.Name.ToStringShort : "NULL"));

            // Basic null check
            if (pawn == null) 
            {
                SLog.Message("[SocialInteractions] JoyGiver_GoOnDate: Pawn is null, returning null.");
                return null;
            }

            // Check if dating feature is enabled
            if (!SocialInteractions.Settings.enableDatingFeature)
            {
                SLog.Message("[SocialInteractions] JoyGiver_GoOnDate: Dating feature is disabled in settings, returning null.");
                return null;
            }

            // Check if pawn is on date cooldown
            if (DatingManager.IsOnDateCooldown(pawn))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is on date cooldown, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check cooldown to prevent spamming attempts
            int lastTick;
            if (lastAttemptTick.TryGetValue(pawn, out lastTick) && Find.TickManager.TicksGame - lastTick < SocialInteractions.Settings.goOnDateCooldownTicks)
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is on attempt cooldown, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Update last attempt tick
            lastAttemptTick[pawn] = Find.TickManager.TicksGame;
            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Updated last attempt tick for pawn {0}.", pawn.Name.ToStringShort));

            // Check if pawn is already on a date
            if (DatingManager.IsOnDate(pawn))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is already on a date, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check if pawn already has a dating-related job
            if (pawn.jobs != null && pawn.jobs.curJob != null &&
                (pawn.jobs.curJob.def == SI_JobDefOf.GoOnDate))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} already has a dating-related job, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check pawn's joy need - only initiate date if joy is low enough
            if (pawn.needs == null || pawn.needs.joy == null)
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} has no joy needs, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            float joyLevel = pawn.needs.joy.CurLevelPercentage;
            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} joy level: {1}, threshold: {2}", pawn.Name.ToStringShort, joyLevel, SocialInteractions.Settings.joyThresholdForDate));
            
            if (joyLevel > SocialInteractions.Settings.joyThresholdForDate)
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} joy level {1} is above threshold {2}, returning null.", pawn.Name.ToStringShort, joyLevel, SocialInteractions.Settings.joyThresholdForDate));
                return null;
            }

            // Check if pawn is awake and able to interact
            if (!pawn.Awake())
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is not awake, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            if (pawn.InBed())
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is in bed, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.LayDown)
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is lying down, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            // Don't allow drafted pawns to start dating (would interrupt combat)
            if (pawn.Drafted)
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is drafted, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Find a suitable partner
            Pawn partner = FindPartnerFor(pawn);
            if (partner == null)
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Could not find a suitable partner for pawn {0}, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check if both pawns can initiate/receive interaction and reserve each other
            if (!SocialInteractionUtility.CanInitiateInteraction(pawn))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} cannot initiate interaction, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            if (!SocialInteractionUtility.CanReceiveInteraction(partner))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Partner {0} cannot receive interaction, returning null.", partner.Name.ToStringShort));
                return null;
            }
            
            if (!pawn.CanReserve(partner))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} cannot reserve partner {1}, returning null.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
                return null;
            }
            
            if (!partner.CanReserve(pawn))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Partner {0} cannot reserve pawn {1}, returning null.", partner.Name.ToStringShort, pawn.Name.ToStringShort));
                return null;
            }

            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: All checks passed for pawn {0} and partner {1}. Creating GoOnDate job.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
            
            // Check if a date can actually be started before creating the job
            if (DatingManager.IsOnDate(pawn) || DatingManager.IsOnDate(partner))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Cannot start date between {0} and {1} because one or both are already on a date.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
                return null;
            }
            
            // Check if the partner is already being targeted for a date by another pawn
            if (IsPartnerBeingTargeted(partner))
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Partner {0} is already being targeted for a date by another pawn.", partner.Name.ToStringShort));
                return null;
            }
            
            // Create the GoOnDate job
            Job job = JobMaker.MakeJob(SI_JobDefOf.GoOnDate, partner);
            return job;
        }

        private Pawn FindPartnerFor(Pawn pawn)
        {
            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Searching for partner for pawn {0}.", pawn != null ? pawn.Name.ToStringShort : "NULL"));
            
            // Basic null and map checks
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null) 
            {
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn, Map, or MapPawns is null."));
                return null;
            }
            
            // Get all pawns on the map
            List<Pawn> allPawns = pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p != null && p.Faction != null && p.Faction.IsPlayer).ToList();
            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Found {0} pawns on map.", allPawns.Count));
            
            // Create a list to hold potential partners and their scores
            List<KeyValuePair<Pawn, float>> potentialPartners = new List<KeyValuePair<Pawn, float>>();
            
            // Evaluate each pawn as a potential partner
            foreach (Pawn p in allPawns)
            {
                // Basic checks (same as before)
                if (p == null) 
                {
                    // This should not happen with our Where filter, but let's be extra safe
                    SLog.Message("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Found null pawn in list (this should not happen).");
                    continue;
                }
                
                if (p == pawn) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Skipping self pawn {0}.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (p.relations == null) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} has no relations.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (!p.IsColonist) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is not a colonist.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (p.IsPrisoner) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is a prisoner.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (p.Downed) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is downed.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (!p.Awake()) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is not awake.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (p.InBed()) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is in bed.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                // Don't select drafted pawns for dating (would interrupt combat)
                if (p.Drafted) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is drafted.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (DatingManager.IsOnDate(p)) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is already on a date.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (DatingManager.IsOnDateCooldown(p)) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is on date cooldown.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                if (!pawn.CanReserveAndReach(p, PathEndMode.InteractionCell, Danger.None)) 
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} cannot reserve and reach {1}.", pawn.Name != null ? pawn.Name.ToStringShort : "NULL", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    continue;
                }
                
                // Calculate date attractiveness score
                float score = CalculateDateAttractiveness(pawn, p);
                SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} has date attractiveness score: {1}", p.Name != null ? p.Name.ToStringShort : "NULL", score));
                
                // Only consider pawns with a positive score
                if (score > 0)
                {
                    potentialPartners.Add(new KeyValuePair<Pawn, float>(p, score));
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is a potential partner for {1} with score {2}.", p.Name != null ? p.Name.ToStringShort : "NULL", pawn.Name != null ? pawn.Name.ToStringShort : "NULL", score));
                }
            }

            // If we have potential partners, select one based on their scores
            if (potentialPartners.Count > 0)
            {
                // Sort by score descending for easier debugging (not necessary for selection)
                potentialPartners.Sort((x, y) => y.Value.CompareTo(x.Value));
                
                // Select a partner based on weighted random selection
                Pawn selectedPartner = SelectPartnerWeighted(pawn, potentialPartners);
                
                if (selectedPartner != null)
                {
                    SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Selected partner {0} for pawn {1} based on weighted scores.", selectedPartner.Name.ToStringShort, pawn.Name.ToStringShort));
                    return selectedPartner;
                }
            }
            
            SLog.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: No suitable partner found for pawn {0}.", pawn.Name.ToStringShort));
            return null;
        }
        
        /// <summary>
        /// Calculates a "date attractiveness" score for a potential partner.
        /// Higher scores mean the pawn is more likely to be chosen for a date.
        /// </summary>
        /// <param name="initiator">The pawn initiating the date</param>
        /// <param name="partner">The potential partner</param>
        /// <returns>A float representing the attractiveness score (higher is better)</returns>
        private float CalculateDateAttractiveness(Pawn initiator, Pawn partner)
        {
            // Start with a base score
            float score = 0f;
            
            // Check for direct romantic relationships (highest priority)
            bool isLover = initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, partner);
            bool isFiance = initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, partner);
            bool isSpouse = initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner);
            
            // If they are already in a direct romantic relationship, give a very high score
            if (isLover || isFiance || isSpouse)
            {
                // Different weights for different relationship types
                if (isSpouse) 
                {
                    score = SocialInteractions.Settings.spouseDateWeight; // Highest priority for spouse
                }
                else if (isFiance) 
                {
                    score = SocialInteractions.Settings.fianceDateWeight;  // High priority for fiance
                }
                else if (isLover) 
                {
                    score = SocialInteractions.Settings.loverDateWeight;  // High priority for lover
                }
                
                // Adjust slightly based on opinion (range: -2 to +2)
                int opinion = initiator.relations.OpinionOf(partner);
                float opinionAdjustment = System.Math.Max(-2f, System.Math.Min(2f, opinion / SocialInteractions.Settings.opinionAdjustmentFactor));
                score += opinionAdjustment;
                
                return score;
            }
            
            // For non-related pawns, base the score primarily on opinion
            int baseOpinion = initiator.relations.OpinionOf(partner);
            
            // If opinion is very low, they're not a suitable partner
            if (baseOpinion <= 10)
            {
                return 0f;
            }
            
            // Base score is the opinion multiplied by the general weight factor for non-related partners
            score = baseOpinion * SocialInteractions.Settings.nonRelatedPartnerWeightFactor;
            
            // Check if the initiator has an official romantic partner
            Pawn officialPartner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
            if (officialPartner == null)
            {
                officialPartner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance);
            }
            if (officialPartner == null)
            {
                officialPartner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
            }
            
            // Only apply cheating penalty if the initiator has an official partner
            if (officialPartner != null)
            {
                // Calculate the opinion difference between the official partner and the potential partner
                int opinionOfOfficialPartner = initiator.relations.OpinionOf(officialPartner);
                int opinionDifference = baseOpinion - opinionOfOfficialPartner;
                
                // Apply a penalty based on the opinion difference
                // If the potential partner is much better (higher opinion), reduce or eliminate the penalty
                // If the potential partner is worse (lower opinion), apply a full penalty
                
                // Base penalty
                float cheatingPenalty = SocialInteractions.Settings.cheatingPenalty;
                
                // Adjust penalty based on opinion difference
                // If opinion difference is positive (potential partner is preferred), reduce penalty
                // If opinion difference is negative (official partner is preferred), keep full penalty
                if (opinionDifference > 0)
                {
                    // Reduce penalty linearly as opinion difference increases
                    // When opinion difference is threshold or more, penalty is eliminated
                    float reductionFactor = System.Math.Max(0f, System.Math.Min(1f, opinionDifference / SocialInteractions.Settings.opinionDifferenceThreshold));
                    cheatingPenalty = cheatingPenalty * (1f - reductionFactor);
                }
                
                score -= cheatingPenalty;
            }
            
            // Ensure the score doesn't go negative
            score = System.Math.Max(0f, score);
            
            return score;
        }
        
        /// <summary>
        /// Selects a partner from a list of potential partners using weighted random selection.
        /// </summary>
        /// <param name="initiator">The pawn initiating the date</param>
        /// <param name="potentialPartners">List of potential partners and their scores</param>
        /// <returns>The selected partner pawn, or null if none selected</returns>
        private Pawn SelectPartnerWeighted(Pawn initiator, List<KeyValuePair<Pawn, float>> potentialPartners)
        {
            if (potentialPartners == null || potentialPartners.Count == 0)
            {
                return null;
            }
            
            // Calculate the total score
            float totalScore = potentialPartners.Sum(pair => pair.Value);
            
            // If all scores are zero, just pick the first one
            if (totalScore <= 0f)
            {
                return potentialPartners[0].Key;
            }
            
            // Generate a random value between 0 and totalScore
            float randomValue = Rand.Value * totalScore;
            
            // Find the partner that corresponds to this random value
            float currentScore = 0f;
            foreach (var pair in potentialPartners)
            {
                currentScore += pair.Value;
                if (randomValue <= currentScore)
                {
                    return pair.Key;
                }
            }
            
            // Fallback (shouldn't happen, but just in case)
            return potentialPartners.Last().Key;
        }
        
        /// <summary>
        /// Checks if a partner is already being targeted for a date by another pawn
        /// </summary>
        /// <param name="partner">The potential partner to check</param>
        /// <returns>True if the partner is already being targeted for a date, false otherwise</returns>
        private bool IsPartnerBeingTargeted(Pawn partner)
        {
            if (partner == null || partner.Map == null || partner.Map.mapPawns == null)
            {
                return false;
            }
            
            // Get all pawns on the map
            IEnumerable<Pawn> allPawns = partner.Map.mapPawns.AllPawnsSpawned;
            
            // Check if any pawn has a GoOnDate job targeting this partner
            foreach (Pawn pawn in allPawns)
            {
                if (pawn == null || pawn == partner || pawn.jobs == null)
                {
                    continue;
                }
                
                // Check if this pawn has a GoOnDate job
                if (pawn.jobs.curJob != null && pawn.jobs.curJob.def == SI_JobDefOf.GoOnDate)
                {
                    // Check if this job is targeting our partner
                    Pawn jobTarget = pawn.jobs.curJob.targetA.Thing as Pawn;
                    if (jobTarget == partner)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}