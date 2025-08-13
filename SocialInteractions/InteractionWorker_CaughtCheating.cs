using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    /// <summary>
    /// Triggers when a pawn discovers their romantic partner cheating while on a date with someone else.
    /// Includes distance/LOS/trait-based weighting, cooldown to avoid spam, and letters + thoughts.
    /// </summary>
    public class InteractionWorker_CaughtCheating : InteractionWorker
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Tunables (could be moved to ModSettings if desired)
        // ─────────────────────────────────────────────────────────────────────────
        private const float BaseChance = 0.50f;     // 50% base chance (vanilla behavior)
        private const int NearbyDist8 = 8;          // distance tiers for weighting
        private const int NearbyDist16 = 16;
        private const bool RequireLineOfSight = false;  // set true to require LOS for the event
        private const int CooldownTicks = 60_000;   // ~1 in-game day

        // ─────────────────────────────────────────────────────────────────────────
        // Lightweight cooldown tracking (not saved; resets on reload)
        // ─────────────────────────────────────────────────────────────────────────
        private static readonly Dictionary<(int a, int b), int> _cooldown = new();

        private static (int a, int b) PairKey(Pawn p1, Pawn p2)
        {
            // Order by thingIDNumber to avoid directional duplicates
            var a = Math.Min(p1.thingIDNumber, p2.thingIDNumber);
            var b = Math.Max(p1.thingIDNumber, p2.thingIDNumber);
            return (a, b);
        }

        private static bool RecentlyTriggered(Pawn p1, Pawn p2)
        {
            if (Current.Game == null) return false;
            var key = PairKey(p1, p2);
            if (_cooldown.TryGetValue(key, out var lastTick))
            {
                return Find.TickManager.TicksGame - lastTick < CooldownTicks;
            }
            return false;
        }

        private static void MarkTriggered(Pawn p1, Pawn p2)
        {
            if (Current.Game == null) return;
            _cooldown[PairKey(p1, p2)] = Find.TickManager.TicksGame;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static Pawn GetPrimaryPartner(Pawn pawn)
        {
            if (pawn?.relations == null) return null;

            // Prioritize spouse > fiance > lover (alive only)
            Pawn p = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, x => !x.Dead);
            if (p == null)
                p = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, x => !x.Dead);
            if (p == null)
                p = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, x => !x.Dead);

            return p;
        }

        private static bool HasTrait(Pawn pawn, TraitDef trait)
        {
            try
            {
                return pawn?.story?.traits?.HasTrait(trait) ?? false;
            }
            catch
            {
                return false;
            }
        }

        private static bool InSameMapAndSpawned(params Pawn[] pawns)
        {
            if (pawns == null || pawns.Length == 0) return false;
            var map = pawns[0]?.Map;
            if (map == null) return false;
            foreach (var p in pawns)
            {
                if (p == null || p.Map != map || !p.Spawned) return false;
            }
            return true;
        }

        private static float DistanceFactor(Pawn a, Pawn b)
        {
            var d = a.Position.DistanceTo(b.Position);
            if (d <= NearbyDist8) return 1.50f;
            if (d <= NearbyDist16) return 1.20f;
            return 1.00f;
        }

        private static bool MeetsLOSCondition(Pawn observer, Pawn target)
        {
            if (!RequireLineOfSight) return true;
            var map = observer.Map;
            if (map == null) return false;
            try
            {
                return GenSight.LineOfSight(observer.Position, target.Position, map, true);
            }
            catch
            {
                // Be permissive if LOS check fails for any reason
                return true;
            }
        }

        private static void TryGainMemorySafe(Pawn pawn, string thoughtDefName, Pawn other = null)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtDefName);
            if (def != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(def, other);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Interaction selection weight
        // ─────────────────────────────────────────────────────────────────────────
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null) return 0f;
            if (!InSameMapAndSpawned(initiator, recipient)) return 0f;

            // Initiator must have a romantic partner and that partner must be the recipient
            var partner = GetPrimaryPartner(initiator);
            if (partner == null || partner != recipient) return 0f;

            // If the partner is currently on a date with someone (and that someone isn't the initiator), we may trigger
            var cheatingPartner = DatingManager.GetPartnerOnDateWith(partner);
            if (cheatingPartner == null || cheatingPartner == initiator) return 0f;
            if (!InSameMapAndSpawned(initiator, recipient, cheatingPartner)) return 0f;

            // Cooldown: don't spam
            if (RecentlyTriggered(initiator, recipient)) return 0f;

            // Optional visibility gating
            if (!MeetsLOSCondition(initiator, recipient) && !MeetsLOSCondition(initiator, cheatingPartner))
                return 0f;

            // Start with base chance and adjust
            float weight = BaseChance;

            // Proximity matters
            weight *= DistanceFactor(initiator, recipient);
            weight *= DistanceFactor(initiator, cheatingPartner);

            // Trait influence (jealous partners more likely to notice)
            if (HasTrait(initiator, TraitDefOf.Jealous)) weight *= 1.25f;

            // Clamp between 0 and 1 for safety
            weight = Math.Max(0f, Math.Min(1f, weight));
            return weight;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // When the interaction actually happens
        // ─────────────────────────────────────────────────────────────────────────
        public override void Interacted(
            Pawn initiator,
            Pawn recipient,
            List<RulePackDef> extraSentencePacks,
            out string letterText,
            out string letterLabel,
            out LetterDef letterDef,
            out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            if (initiator == null || recipient == null) return;

            // Confirm the recipient is on a date with someone else (the cheater)
            Pawn cheatingPartner = DatingManager.GetPartnerOnDateWith(recipient);
            if (cheatingPartner == null || cheatingPartner == initiator) return;

            // Thoughts: give the discoverer a memory; optionally tag the others if you have defs
            TryGainMemorySafe(initiator, "CaughtCheating", recipient);      // primary memory for the observer
            TryGainMemorySafe(recipient, "WasCaughtCheating", initiator);   // optional; define in your mod
            TryGainMemorySafe(cheatingPartner, "AffairExposed", initiator); // optional; define in your mod

            // Compose a letter for the player
            letterLabel = "Infidelity Discovered";
            letterText = $"{initiator.LabelShortCap} caught {recipient.LabelShort} cheating with {cheatingPartner.LabelShort}.";

            // Use a negative letter (can be changed to NeutralEvent if preferred)
            letterDef = LetterDefOf.NegativeEvent;

            // Provide look targets (filter nulls just in case)
            var targets = new List<Thing> { initiator, recipient, cheatingPartner }.Where(t => t != null);
            lookTargets = new LookTargets(targets);

            // Fire a non-blocking social interaction / notification hook (if your mod uses it)
            var caughtCheatingInteractionDef = DefDatabase<InteractionDef>.GetNamedSilentFail("CaughtCheating");
            if (caughtCheatingInteractionDef != null)
            {
                string subject = $"{initiator.NameShortColored} caught {recipient.NameShortColored} cheating with {cheatingPartner.NameShortColored}";
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, caughtCheatingInteractionDef, subject);
            }

            // Mark cooldown to prevent immediate repeats
            MarkTriggered(initiator, recipient);
        }
    }
}
