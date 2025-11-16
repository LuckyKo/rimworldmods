using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public static class ChildrenMisbehaviorManager
    {
        // Misbehavior factor calculation constants
        private const float MaxMisbehaviorFactor = 1.0f;
        private const float MinMisbehaviorFactor = 0.0f;
        private const float BaseParentalOpinionThreshold = 20f; // Opinion below this increases misbehavior
        private const int ChildAgeLimit = 13; // Pawns under this age are considered children for misbehavior (12 and under)
        private const int TeenagerAgeLimit = 17; // Pawns under this age may have different behavior patterns
        private const int MaxTimeSinceParentInteraction = 180000; // 5 days in ticks, after which misbehavior increases
        
        // Misbehavior level thresholds
        private const float Level1Threshold = 0.02f; // Annoying adults
        private const float Level2Threshold = 0.05f; // Misplacing items
        private const float Level3Threshold = 0.7f; // Damaging property
        private const float Level4Threshold = 0.9f; // Dangerous behavior

        // Track ongoing misbehavior activities to prevent spam
        private static Dictionary<Pawn, int> lastMisbehaviorTick = new Dictionary<Pawn, int>();
        private static int misbehaviorCheckInterval = 3000; // Check every 3000 ticks (~5 min)

        /// <summary>
        /// Calculates the misbehavior factor for a child pawn based on parental relationship quality and other factors
        /// </summary>
        public static float CalculateMisbehaviorFactor(Pawn child)
        {
            if (child == null || !IsChild(child))
            {
                return 0f;
            }

            float misbehaviorFactor = 0.3f; // Base factor
            float parentImpactMultiplier = SocialInteractions.Settings.childrenMisbehaviorParentOpinionImpact;

            // Factor in child's current mood
            float moodFactor = 0f;
            if (child.needs != null && child.needs.mood != null)
            {
                float moodPercent = child.needs.mood.CurLevelPercentage;
                // Lower mood = higher misbehavior tendency (inverted relationship)
                moodFactor = (0.5f - moodPercent) * 0.4f; // Scale the mood impact
            }

            // Check for parents/guardians and their opinions
            List<Pawn> allPotentialParents = GetParentsAndGuardians(child);

            // Filter to only the most important relationships (parents, spouses, lovers, close family) to avoid dilution
            List<Pawn> significantParents = new List<Pawn>();
            foreach (Pawn potentialParent in allPotentialParents)
            {
                if (potentialParent != null && !potentialParent.Dead && potentialParent.Spawned)
                {
                    // Only include immediate family and significant relationships (not extended family that dilutes the effect)
                    if (child.relations != null)
                    {
                        var directRelationsEnum = child.GetRelations(potentialParent);
                        List<PawnRelationDef> directRelations = directRelationsEnum.ToList(); // Convert to list to allow indexing
                        bool isSignificant = false;

                        foreach (var relation in directRelations)
                        {
                            // Only consider immediate family relations
                            if (relation == PawnRelationDefOf.Parent ||
                                relation == PawnRelationDefOf.Bond)
                            {
                                isSignificant = true;
                                break;
                            }
                        }

                        if (isSignificant)
                        {
                            significantParents.Add(potentialParent);
                        }
                    }
                }
            }

            if (significantParents.Count == 0)
            {
                // No significant parents/guardians = maximum misbehavior tendency
                misbehaviorFactor = Mathf.Min(misbehaviorFactor + 0.5f, MaxMisbehaviorFactor);
            }
            else
            {
                // Calculate based on average significant parental opinion
                float totalOpinion = 0f;
                int validParents = 0;

                foreach (Pawn parent in significantParents)
                {
                    if (parent != null && !parent.Dead && parent.Spawned)
                    {
                        int opinion = 0;
                        if (child.relations != null)
                        {
                            opinion = child.relations.OpinionOf(parent); // FIXED: Child's opinion of the parent, not parent's opinion of child
                        }
                        totalOpinion += opinion;
                        validParents++;
                    }
                }

                if (validParents > 0)
                {
                    float avgParentOpinion = totalOpinion / validParents;

                    // Lower parent opinion increases misbehavior factor
                    if (avgParentOpinion < BaseParentalOpinionThreshold)
                    {
                        float opinionFactor = (BaseParentalOpinionThreshold - avgParentOpinion) / BaseParentalOpinionThreshold;
                        float increaseAmount = opinionFactor * 0.6f * parentImpactMultiplier; // Increased from 0.5f to 0.6f
                        misbehaviorFactor = Mathf.Min(misbehaviorFactor + increaseAmount, MaxMisbehaviorFactor);
                    }
                    else
                    {
                        // Higher parent opinion decreases misbehavior factor
                        float opinionFactor = Mathf.Clamp((avgParentOpinion - BaseParentalOpinionThreshold) / 100f, 0f, 1f);
                        float decreaseAmount = opinionFactor * 0.4f * parentImpactMultiplier; // Increased from 0.3f to 0.4f
                        misbehaviorFactor = Mathf.Max(misbehaviorFactor - decreaseAmount, 0.1f); // Keep minimum at 0.1f
                    }
                }
            }

            // Apply mood factor as a multiplicative factor instead of additive
            float moodMultiplier = 1.0f + (moodFactor * 1.0f); // Adjust multiplier factor as needed
            misbehaviorFactor *= moodMultiplier;

            // Add random factor based on child traits
            float traitInfluence = GetTraitInfluenceOnMisbehavior(child);
            misbehaviorFactor += traitInfluence;

            // Clamp to reasonable range to prevent negative factors from eliminating misbehavior entirely
            misbehaviorFactor = Mathf.Clamp(misbehaviorFactor, 0.1f, MaxMisbehaviorFactor);

            return misbehaviorFactor;
        }

        /// <summary>
        /// Determines if a pawn should engage in misbehavior based on their misbehavior factor and other conditions
        /// </summary>
        public static bool ShouldChildMisbehave(Pawn child, out float misbehaviorLevel)
        {
            misbehaviorLevel = 0f;

            // SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave called for: {0}", child != null ? child.LabelShort : "null"));

            if (child == null || !IsChild(child))
            {
                SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: child {0} is null or not a child", child.LabelShort));
                return false;
            }

            // Check if children misbehavior is enabled in settings
            if (!SocialInteractions.Settings.enableChildrenMisbehavior)
            {
                // SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: children misbehavior is disabled in settings"));
                return false;
            }

            // Check if enough time has passed since last misbehavior
            if (lastMisbehaviorTick.ContainsKey(child))
            {
                int lastTick = lastMisbehaviorTick[child];
                if (Find.TickManager.TicksGame - lastTick < misbehaviorCheckInterval)
                {
                    // SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: child {0} is still in cooldown", child.LabelShort));
                    return false;
                }
            }

            // Check if child is currently in a job that would prevent misbehavior
            if (child.jobs != null && child.CurJob != null)
            {
                if (child.CurJob.def != JobDefOf.GotoWander && child.CurJob.def != JobDefOf.Wait_Wander)
                {
                    // SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: child {0} is in job {1}, not allowed to misbehave",
                        // child.LabelShort, child.CurJob.def.defName));
                    return false;
                }
            }

            // Calculate misbehavior factor
            float misbehaviorFactor = CalculateMisbehaviorFactor(child);
            // SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: Child {0} misbehavior factor: {1:F3}", child.LabelShort, misbehaviorFactor));

            // Apply base chance from settings
            float baseChance = SocialInteractions.Settings.baseChildrenMisbehaviorChance;
            // SLog.Message(string.Format("[SocialInteractions] Base chance from settings: {0:F3}", baseChance));

            // Calculate the total probability
            float totalChance = baseChance * misbehaviorFactor;
            float randomValue = Rand.Value;

            // Use a random chance based on the base chance and misbehavior factor
            if (randomValue < totalChance)
            {
                SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: Child {0} will misbehave! (chance {1:F3} > random {2:F3})",
                    child.LabelShort, totalChance, randomValue));
                misbehaviorLevel = misbehaviorFactor;
                lastMisbehaviorTick[child] = Find.TickManager.TicksGame;
                return true;
            }
            else
            {
                // SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: Child {0} will NOT misbehave (chance {1:F3} <= random {2:F3})",
                    // child.LabelShort, totalChance, randomValue));
            }

            return false;
        }

        /// <summary>
        /// Executes a misbehavior action for a child pawn
        /// Higher misbehavior levels unlock more options, but only one is randomly selected
        /// </summary>
        public static void ExecuteMisbehavior(Pawn child, float misbehaviorLevel)
        {
            if (child == null || !IsChild(child))
            {
                return;
            }

            // Check if misbehavior level is too low first
            if (misbehaviorLevel < Level1Threshold)
            {
                return; // No behaviors executed
            }

            // Build a list of eligible misbehavior activities based on the calculated level
            List<Action> eligibleBehaviors = new List<Action>();

            if (misbehaviorLevel >= Level1Threshold)
            {
                eligibleBehaviors.Add(() => AnnoyAdults(child));
            }

            if (misbehaviorLevel >= Level2Threshold)
            {
                eligibleBehaviors.Add(() => MisplaceItems(child));
            }

            if (misbehaviorLevel >= Level3Threshold)
            {
                eligibleBehaviors.Add(() => DamageProperty(child));
            }

            if (misbehaviorLevel >= Level4Threshold)
            {
                eligibleBehaviors.Add(() => DangerousBehavior(child));
            }

            // Select one behavior randomly from the eligible options
            if (eligibleBehaviors.Count > 0)
            {
                int selectedIndex = Rand.Range(0, eligibleBehaviors.Count);

                SLog.Message(string.Format("[SocialInteractions] ExecuteMisbehavior: Child misbehavior behavior selected. Index: {0}, Total eligible: {1}",
                    selectedIndex, eligibleBehaviors.Count));

                // Execute the selected behavior
                eligibleBehaviors[selectedIndex]();

                // Only trigger monologue for specific behaviors that need it
                // Annoying adults has its own interaction, misplacing items has its own monologue in the method
                // So we don't need a general monologue here for any behavior
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] ExecuteMisbehavior: No eligible behaviors found for child misbehavior"));
            }
            // No fallback monologue - each behavior that needs dialogue handles it internally
        }

        private static void AnnoyAdults(Pawn child)
        {
            SLog.Message(string.Format("[SocialInteractions] AnnoyAdults method called for child: {0}", child != null ? child.LabelShort : "null"));

            if (child == null || child.Map == null)
            {
                SLog.Warning("[SocialInteractions] AnnoyAdults: child is null or map is null");
                return;
            }

            // Find nearby adults to annoy
            Pawn targetAdult = FindNearbyAnnoyableAdult(child);

            SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: FindNearbyAnnoyableAdult returned: {0}", targetAdult != null ? targetAdult.LabelShort : "null"));

            if (targetAdult != null)
            {
                // Log the action
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Child {0} is annoying adult {1}", child.LabelShort, targetAdult.LabelShort));

                // Show message to player
                Messages.Message(string.Format("{0} (child) is pestering {1} (adult) with annoying questions!", child.LabelShort, targetAdult.LabelShort),
                    new LookTargets(child, targetAdult), MessageTypeDefOf.CautionInput);

                // Create the ChildAnnoyAdult job for the child to follow and pester the adult
                Job annoyJob = JobMaker.MakeJob(SI_JobDefOf.ChildAnnoyAdult, targetAdult);
                bool jobTaken = child.jobs.TryTakeOrderedJob(annoyJob);
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Child {0} tried to take ChildAnnoyAdult job with {1}, success: {2}",
                    child.LabelShort, targetAdult.LabelShort, jobTaken));

                if (!jobTaken)
                {
                    SLog.Warning(string.Format("[SocialInteractions] AnnoyAdults: Child {0} failed to take ChildAnnoyAdult job with {1}",
                        child.LabelShort, targetAdult.LabelShort));
                }
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Child {0} found no adults to annoy, becoming bored", child.LabelShort));

                // If no adult found, child becomes bored and expresses it
                // Apply boredom thought to the child
                if (child.needs != null && child.needs.mood != null)
                {
                    child.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildBoredom, null);
                    SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Applied ChildBoredom thought to {0}", child.LabelShort));
                }

                // Trigger LLM monologue about being bored with proper subject formatting
                string subject = string.Format("I'm bored because there's nobody to play with", child.LabelShort);
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Triggering monologue for bored child: {0}", subject));
                SocialInteractions.HandleMonologue(child, subject);
            }
        }

        private static void ApplyNegativeMoodToAdult(Pawn adult, Pawn child)
        {
            if (adult == null || adult.needs == null || adult.needs.mood == null)
            {
                return;
            }

            // Add a thought to the adult about being annoyed by the child
            adult.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildAnnoyance, child);
        }

        private static void MisplaceItems(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return;
            }

            // Find valuable items in storage zones near the child that they can take
            Thing itemToTake = FindValuableItemInStorage(child, child.Map, 100); // Look in 10-cell radius

            if (itemToTake != null)
            {
                // Find a random location for the child to go play with the item
                IntVec3 playLocation = FindRandomPlayLocation(child, child.Map);

                if (playLocation != IntVec3.Invalid)
                {
                    // Start the job for the child to take the item to the play location and play with it
                    Job playWithItemJob = JobMaker.MakeJob(SI_JobDefOf.ChildPlayWithItem, itemToTake, playLocation);
                    child.jobs.TryTakeOrderedJob(playWithItemJob);

                    SLog.Message(string.Format("[SocialInteractions] MisplaceItems: Child {0} is taking item {1} to play with at location {2}",
                        child.LabelShort, itemToTake.Label, playLocation));

                    // Show message to player
                    Messages.Message(string.Format("{0} (child) is taking {1} to play with!", child.LabelShort, itemToTake.Label),
                        new LookTargets(child, itemToTake), MessageTypeDefOf.CautionInput);
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] MisplaceItems: Child {0} found item {1} but no suitable play location",
                        child.LabelShort, itemToTake.Label));
                }
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] MisplaceItems: Child {0} found no valuable items to take", child.LabelShort));
            }
        }

        /// <summary>
        /// Finds a valuable item in storage zones near the child
        /// </summary>
        private static Thing FindValuableItemInStorage(Pawn child, Map map, int radius)
        {
            List<Thing> potentialItems = new List<Thing>();

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, radius, true))
            {
                if (!c.InBounds(map)) continue;

                // Only check cells that are in storage zones
                Zone zone = map.zoneManager.ZoneAt(c);
                if (zone is Zone_Stockpile)
                {
                    foreach (Thing thing in c.GetThingList(map))
                    {
                        // Only consider items that can be hauled and have some value
                        if (thing.def.EverHaulable &&
                            !thing.Position.Fogged(map) &&
                            thing.Spawned &&
                            thing.MarketValue > 10f) // Only items worth more than 10 silver
                        {
                            // Consider items that are in stockpile zones as "valuable"
                            potentialItems.Add(thing);
                        }
                    }
                }
            }

            // If we found valuable items, pick one randomly
            if (potentialItems.Count > 0)
            {
                // Sort by value to make more valuable items more likely to be chosen
                potentialItems.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));

                // Weighted selection - more valuable items have higher chance
                float totalValue = 0f;
                foreach (Thing item in potentialItems)
                {
                    totalValue += item.MarketValue;
                }

                if (totalValue > 0)
                {
                    float randomValue = Rand.Value * totalValue;
                    float currentValue = 0f;

                    foreach (Thing item in potentialItems)
                    {
                        currentValue += item.MarketValue;
                        if (randomValue <= currentValue)
                        {
                            return item;
                        }
                    }
                }

                // Fallback to random selection
                return potentialItems[Rand.Range(0, potentialItems.Count)];
            }

            return null;
        }

        /// <summary>
        /// Finds a random suitable location for the child to play
        /// </summary>
        private static IntVec3 FindRandomPlayLocation(Pawn child, Map map)
        {
            List<IntVec3> possibleCells = new List<IntVec3>();

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, 20, true)) // Look in 20 cell radius
            {
                if (c.InBounds(map) && c.Walkable(map) && !c.Fogged(map))
                {
                    // Avoid building areas and prefer open spaces
                    if (c.GetEdifice(map) == null && c.GetFirstItem(map) == null)
                    {
                        possibleCells.Add(c);
                    }
                }
            }

            if (possibleCells.Count > 0)
            {
                return possibleCells[Rand.Range(0, possibleCells.Count)];
            }

            return IntVec3.Invalid; // No suitable location found
        }

        private static IntVec3 FindInappropriateStorageLocation(Pawn child, Map map, Thing item)
        {
            // Look for inappropriate storage zones like dumping areas or random locations
            // Try to find a trash zone or unassigned area to dump items

            // Look in a larger radius for potential inappropriate locations
            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, 20, true))
            {
                if (!c.InBounds(map)) continue;

                // Check if this is an unassigned area that's not meant for storage
                if (c.GetEdifice(map) == null) // No building blocking placement
                {
                    // Check if it's a dumping cell (like a waste area)
                    Zone zone = map.zoneManager.ZoneAt(c);
                    if (zone != null && zone is Zone_Stockpile)
                    {
                        // If it's a stockpile with inappropriate categories for this item, use it
                        Zone_Stockpile stockpile = (Zone_Stockpile)zone;
                        if (!stockpile.Accepts(item))
                        {
                            return c;
                        }
                    }
                    else if (zone != null && zone.label.Contains("Dumping"))
                    {
                        // Found a dumping zone
                        return c;
                    }
                }
            }

            // If no specific bad zones found, return invalid to indicate random placement
            return IntVec3.Invalid;
        }

        private static void DamageProperty(Pawn child)
        {
            // Placeholder for property damage logic
            SLog.Message(string.Format("[SocialInteractions] DamageProperty: Child {0} is damaging property", child.LabelShort));
        }

        private static void DangerousBehavior(Pawn child)
        {
            // Placeholder for dangerous behavior logic
            SLog.Message(string.Format("[SocialInteractions] DamageProperty: Child {0} is engaging in dangerous behavior", child.LabelShort));
        }

        private static string GetMisbehaviorLevelDescription(float misbehaviorLevel)
        {
            if (misbehaviorLevel < Level1Threshold) return "no";
            else if (misbehaviorLevel < Level2Threshold) return "level 1";
            else if (misbehaviorLevel < Level3Threshold) return "level 2";
            else if (misbehaviorLevel < Level4Threshold) return "level 3";
            else return "level 4";
        }

        private static void TriggerChildMonologue(Pawn child, float misbehaviorLevel)
        {
            string subject = GetMisbehaviorSubject(child, misbehaviorLevel);
            SocialInteractions.HandleMonologue(child, subject);
        }

        private static string GetMisbehaviorSubject(Pawn child, float misbehaviorLevel)
        {
            if (misbehaviorLevel < Level2Threshold)
            {
                return "acted up for attention";
            }
            else if (misbehaviorLevel < Level3Threshold)
            {
                return "misplaced something";
            }
            else if (misbehaviorLevel < Level4Threshold)
            {
                return "damaged something";
            }
            else
            {
                return "did something dangerous";
            }
        }

        private static string GetRandomAnnoyanceText()
        {
            string[] annoyanceTexts = {
                "Are we there yet?",
                "I'm boooored!",
                "Why do I have to?",
                "Are you sure?",
                "I know something you don't know!",
                "Make me!",
                "It's not fair!",
                "I didn't do anything!",
                "Why not?",
                "Because I said so!",
                "You're not the boss of me!",
                "I hate you!",
                "I'm telling mom/dad!",
                "You started it!",
                "It's not my fault!"
            };

            return annoyanceTexts[Rand.Range(0, annoyanceTexts.Length)];
        }

        private static Pawn FindNearbyAnnoyableAdult(Pawn child)
        {
            if (child.Map == null)
            {
                return null;
            }

            // Find colonists that are not currently busy
            List<Pawn> candidates = new List<Pawn>();
            
            foreach (Pawn pawn in child.Map.mapPawns.FreeColonists)
            {
                if (pawn != null && pawn != child && IsAdult(pawn) && !pawn.Dead && pawn.Spawned)
                {
                    // Only consider adults who are idle or doing non-critical work
                    if (pawn.CurJob == null || 
                        pawn.CurJob.def == JobDefOf.Wait || 
                        pawn.CurJob.def == JobDefOf.Wait_Wander || 
                        pawn.CurJob.def == JobDefOf.GotoWander)
                    {
                        float dist = (pawn.Position - child.Position).LengthHorizontal;
                        if (dist <= 50f) // Within reasonable distance
                        {
                            candidates.Add(pawn);
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                return candidates[Rand.Range(0, candidates.Count)];
            }

            return null;
        }

        private static List<Pawn> GetParentsAndGuardians(Pawn child)
        {
            List<Pawn> parents = new List<Pawn>();
            
            if (child.relations != null)
            {
                // Get direct parents (biological, adoptive, etc.)
                foreach (Pawn otherPawn in child.Map.mapPawns.AllPawns)
                {
                    if (otherPawn != null && !otherPawn.Dead && otherPawn.Spawned && otherPawn.RaceProps.Humanlike)
                    {
                        if (child.relations.DirectRelationExists(PawnRelationDefOf.Parent, otherPawn))
                        {
                            parents.Add(otherPawn);
                        }
                        else
                        {
                            // Check if they are family by blood
                            var allRelations = child.GetRelations(otherPawn);
                            foreach (var relation in allRelations)
                            {
                                if (relation == PawnRelationDefOf.Parent || relation == PawnRelationDefOf.Child ||
                                    relation == PawnRelationDefOf.Sibling || relation == PawnRelationDefOf.Grandchild ||
                                    relation == PawnRelationDefOf.Grandparent)
                                {
                                    parents.Add(otherPawn);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return parents;
        }

        private static bool HasParentBeenAbsentForLongTime(Pawn parent, Pawn child)
        {
            // This is a simplified check - in a real implementation, we'd need to track 
            // actual interaction times between parent and child
            return false; // Placeholder implementation
        }

        private static float GetTraitInfluenceOnMisbehavior(Pawn child)
        {
            float traitInfluence = 0f;
            
            if (child.story != null && child.story.traits != null)
            {
                foreach (Trait trait in child.story.traits.allTraits)
                {
                    if (trait != null)
                    {
                        switch (trait.def.defName)
                        {
                            case "Rebellious":
                                traitInfluence += 0.2f;
                                break;
                            case "Nervous":
                                traitInfluence += 0.1f;
                                break;
                            case "Wimp":
                                traitInfluence += 0.1f;
                                break;
                            case "Bloodlust":
                                traitInfluence += 0.15f;
                                break;
                            case "Psychopath":
                                traitInfluence += 0.3f;
                                break;
                            case "Kind":
                                traitInfluence -= 0.2f;
                                break;
                            default:
                                // Other traits can be added as needed
                                break;
                        }
                    }
                }
            }
            
            return Mathf.Clamp(traitInfluence, -0.5f, 0.5f);
        }

        private static bool IsChild(Pawn pawn)
        {
            if (pawn == null || pawn.ageTracker == null)
            {
                return false;
            }

            // Using the standard RimWorld age classification
            // Only process pawns that are between 3-12 years old (exclude toddlers under 3)
            int age = pawn.ageTracker.AgeBiologicalYears;
            return age >= 3 && age < ChildAgeLimit;
        }

        private static bool IsAdult(Pawn pawn)
        {
            if (pawn == null || pawn.ageTracker == null)
            {
                return false;
            }
            
            return pawn.ageTracker.AgeBiologicalYears >= ChildAgeLimit;
        }

        /// <summary>
        /// Cleanup method to be called periodically (e.g. with a MapComponent)
        /// </summary>
        public static void Cleanup()
        {
            // Remove references to pawns that are no longer valid
            List<Pawn> toRemove = new List<Pawn>();
            
            foreach (var kvp in lastMisbehaviorTick)
            {
                if (kvp.Key == null || kvp.Key.Dead || !kvp.Key.Spawned)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (Pawn pawn in toRemove)
            {
                lastMisbehaviorTick.Remove(pawn);
            }
        }
    }
}