using System;
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
        private const int CooldownTicks = 600; // 10 seconds (60 ticks per second)

        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null) return null;

            // Cooldown to prevent spamming date attempts
            int lastTick;
            if (lastAttemptTick.TryGetValue(pawn, out lastTick) && Find.TickManager.TicksGame - lastTick < CooldownTicks)
            {
                return null;
            }

            // Update last attempt tick
            lastAttemptTick[pawn] = Find.TickManager.TicksGame;

            // Add this check to prevent new date jobs if pawn is already on a date
            if (DatingManager.IsOnDate(pawn))
            {
                return null;
            }

            // Prevent spamming jobs if pawn already has an AskForDate or GoOnDate job
            if (pawn.jobs != null && pawn.jobs.curJob != null &&
                (pawn.jobs.curJob.def == DefDatabase<JobDef>.GetNamed("AskForDate") || pawn.jobs.curJob.def == DefDatabase<JobDef>.GetNamed("GoOnDate")))
            {
                return null;
            }

            // 1. Check if pawn needs joy
            if (pawn.needs == null || pawn.needs.joy == null || pawn.needs.joy.CurLevelPercentage > 0.9f)
            {
                return null;
            }

            // 2. Find a suitable partner
            Pawn partner = FindPartnerFor(pawn);
            if (partner == null)
            {
                return null;
            }

            // Add distance check
            int maxDistance = SocialInteractionsMod.Settings.maxDateDistance;
            if ((Math.Abs(pawn.Position.x - partner.Position.x) + Math.Abs(pawn.Position.z - partner.Position.z)) > maxDistance)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Not giving job. Recipient {0} is too far from initiator {1}. Distance: {2}, Max Distance: {3}", partner.Name.ToStringShort, pawn.Name.ToStringShort, (Math.Abs(pawn.Position.x - partner.Position.x) + Math.Abs(pawn.Position.z - partner.Position.z)), maxDistance));
                return null;
            }

            // 3. Check if both pawns can interact and if the partner can be reserved
            if (!SocialInteractionUtility.CanInitiateInteraction(pawn) || !SocialInteractionUtility.CanReceiveInteraction(partner) || !pawn.CanReserve(partner))
            {
                return null;
            }

            // 4. Create and return the "AskForDate" job
            JobDef askForDateJobDef = DefDatabase<JobDef>.GetNamed("AskForDate");
            if (askForDateJobDef == null) return null;
            Job job = JobMaker.MakeJob(askForDateJobDef, partner);
            return job;
        }

        private Pawn FindPartnerFor(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null) return null;
            // Find a pawn who is a lover, fiance, or spouse
            return (Pawn)pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && p.relations != null &&
                            (p.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn) ||
                             p.relations.DirectRelationExists(PawnRelationDefOf.Fiance, pawn) ||
                             p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn) ||
                             p.relations.OpinionOf(pawn) > 10)) // Good opinion threshold
                .FirstOrDefault();
        }
    }
}