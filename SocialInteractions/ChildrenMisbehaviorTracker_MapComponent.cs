using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SocialInteractions
{
    public class ChildrenMisbehaviorTracker_MapComponent : MapComponent
    {
        private int lastChildMisbehaviorCheckTick = 0;
        private const int ChildMisbehaviorCheckInterval = 600; // Check every 600 ticks (~1 minute)

        public ChildrenMisbehaviorTracker_MapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            // Only check for child misbehavior periodically to avoid performance impact
            if (Find.TickManager.TicksGame - lastChildMisbehaviorCheckTick >= ChildMisbehaviorCheckInterval)
            {
                ProcessChildrenMisbehavior();
                lastChildMisbehaviorCheckTick = Find.TickManager.TicksGame;
            }

            // Cleanup periodically
            if (Find.TickManager.TicksGame % 3000 == 0) // Every 3000 ticks
            {
                ChildrenMisbehaviorManager.Cleanup();
            }
        }

        private void ProcessChildrenMisbehavior()
        {
            // Iterate through all colonist children on the map
            List<Pawn> children = new List<Pawn>();

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (pawn != null && pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears >= 3 && pawn.ageTracker.AgeBiologicalYears < 13 &&
                    pawn.RaceProps.Humanlike && !pawn.Dead && pawn.Spawned && pawn.Awake())
                {
                    // Additional check to make sure pawn is still valid after all the checks
                    if (pawn.LabelShort != null)  // This will catch potential issues with the pawn reference
                    {
                        children.Add(pawn);
                    }
                }
            }

            // Check each child for potential misbehavior
            foreach (Pawn child in children)
            {
                // Do a more comprehensive check for the child's validity
                if (child != null && child.ageTracker != null && child.ageTracker.AgeBiologicalYears >= 3 && child.ageTracker.AgeBiologicalYears < 13 &&
                    child.RaceProps.Humanlike && !child.Dead && child.Spawned && child.Awake())
                {
                    float misbehaviorLevel;
                    if (ChildrenMisbehaviorManager.ShouldChildMisbehave(child, out misbehaviorLevel))
                    {
                        ChildrenMisbehaviorManager.ExecuteMisbehavior(child, misbehaviorLevel);
                    }
                }
            }
        }
    }
}