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
            Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: TryGiveJob called for pawn {0}.", pawn != null ? pawn.Name.ToStringShort : "NULL"));

            // Basic null check
            if (pawn == null) 
            {
                Log.Message("[SocialInteractions] JoyGiver_GoOnDate: Pawn is null, returning null.");
                return null;
            }

            // Check if dating feature is enabled
            if (!SocialInteractions.Settings.enableDatingFeature)
            {
                Log.Message("[SocialInteractions] JoyGiver_GoOnDate: Dating feature is disabled in settings, returning null.");
                return null;
            }

            // Check if pawn is on date cooldown
            if (DatingManager.IsOnDateCooldown(pawn))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is on date cooldown, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check cooldown to prevent spamming attempts
            int lastTick;
            if (lastAttemptTick.TryGetValue(pawn, out lastTick) && Find.TickManager.TicksGame - lastTick < CooldownTicks)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is on attempt cooldown, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Update last attempt tick
            lastAttemptTick[pawn] = Find.TickManager.TicksGame;
            Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Updated last attempt tick for pawn {0}.", pawn.Name.ToStringShort));

            // Check if pawn is already on a date
            if (DatingManager.IsOnDate(pawn))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is already on a date, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check if pawn already has a dating-related job
            if (pawn.jobs != null && pawn.jobs.curJob != null &&
                (pawn.jobs.curJob.def == SI_JobDefOf.GoOnDate))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} already has a dating-related job, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check pawn's joy need - only initiate date if joy is low enough
            /* Temporarily removed for testing
            if (pawn.needs == null || pawn.needs.joy == null)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} has no joy needs, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            float joyLevel = pawn.needs.joy.CurLevelPercentage;
            Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} joy level: {1}, threshold: {2}", pawn.Name.ToStringShort, joyLevel, SocialInteractions.Settings.joyThresholdForDate));
            
            if (joyLevel > SocialInteractions.Settings.joyThresholdForDate)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} joy level {1} is above threshold {2}, returning null.", pawn.Name.ToStringShort, joyLevel, SocialInteractions.Settings.joyThresholdForDate));
                return null;
            }
            */

            // Check if pawn is awake and able to interact
            if (!pawn.Awake())
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is not awake, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            if (pawn.InBed())
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is in bed, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.LayDown)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is lying down, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            // Don't allow drafted pawns to start dating (would interrupt combat)
            if (pawn.Drafted)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} is drafted, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Find a suitable partner
            Pawn partner = FindPartnerFor(pawn);
            if (partner == null)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Could not find a suitable partner for pawn {0}, returning null.", pawn.Name.ToStringShort));
                return null;
            }

            // Check if both pawns can initiate/receive interaction and reserve each other
            if (!SocialInteractionUtility.CanInitiateInteraction(pawn))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} cannot initiate interaction, returning null.", pawn.Name.ToStringShort));
                return null;
            }
            
            if (!SocialInteractionUtility.CanReceiveInteraction(partner))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Partner {0} cannot receive interaction, returning null.", partner.Name.ToStringShort));
                return null;
            }
            
            if (!pawn.CanReserve(partner))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Pawn {0} cannot reserve partner {1}, returning null.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
                return null;
            }
            
            if (!partner.CanReserve(pawn))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Partner {0} cannot reserve pawn {1}, returning null.", partner.Name.ToStringShort, pawn.Name.ToStringShort));
                return null;
            }

            Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: All checks passed for pawn {0} and partner {1}. Creating GoOnDate job.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
            
            // Check if a date can actually be started before creating the job
            if (DatingManager.IsOnDate(pawn) || DatingManager.IsOnDate(partner))
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate: Cannot start date between {0} and {1} because one or both are already on a date.", pawn.Name.ToStringShort, partner.Name.ToStringShort));
                return null;
            }
            
            // Create the GoOnDate job
            Job job = JobMaker.MakeJob(SI_JobDefOf.GoOnDate, partner);
            return job;
        }

        private Pawn FindPartnerFor(Pawn pawn)
        {
            Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Searching for partner for pawn {0}.", pawn != null ? pawn.Name.ToStringShort : "NULL"));
            
            // Basic null and map checks
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null) 
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn, Map, or MapPawns is null."));
                return null;
            }
            
            // Get all pawns on the map
            List<Pawn> allPawns = pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p != null && p.Faction != null && p.Faction.IsPlayer).ToList();
            Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Found {0} pawns on map.", allPawns.Count));
            
            // Filter for potential partners
            Pawn partner = allPawns.FirstOrDefault(p => {
                // Basic checks
                if (p == null) 
                {
                    // This should not happen with our Where filter, but let's be extra safe
                    Log.Message("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Found null pawn in list (this should not happen).");
                    return false;
                }
                
                if (p == pawn) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Skipping self pawn {0}.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (p.relations == null) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} has no relations.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (!p.IsColonist) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is not a colonist.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (p.IsPrisoner) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is a prisoner.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (p.Downed) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is downed.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (!p.Awake()) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is not awake.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (p.InBed()) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is in bed.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                // Don't select drafted pawns for dating (would interrupt combat)
                if (p.Drafted) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is drafted.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (DatingManager.IsOnDate(p)) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is already on a date.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (DatingManager.IsOnDateCooldown(p)) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is on date cooldown.", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                if (!pawn.CanReserveAndReach(p, PathEndMode.InteractionCell, Danger.None)) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} cannot reserve and reach {1}.", pawn.Name != null ? pawn.Name.ToStringShort : "NULL", p.Name != null ? p.Name.ToStringShort : "NULL"));
                    return false;
                }
                
                // Relationship checks
                bool isRelated = pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, p) ||
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, p) ||
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, p);
                
                int opinion = pawn.relations.OpinionOf(p);
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Checking pawn {0}. IsRelated: {1}, Opinion: {2}", p.Name != null ? p.Name.ToStringShort : "NULL", isRelated, opinion));
                
                if (!isRelated && opinion <= 10) 
                {
                    Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is not a lover/fiance/spouse and opinion ({1}) is not > 10.", p.Name != null ? p.Name.ToStringShort : "NULL", opinion));
                    return false;
                }
                
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Pawn {0} is a potential partner for {1}. IsRelated: {2}, Opinion: {3}", p.Name != null ? p.Name.ToStringShort : "NULL", pawn.Name != null ? pawn.Name.ToStringShort : "NULL", isRelated, opinion));
                return true;
            });

            if (partner != null)
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: Found partner {0} for pawn {1}.", partner.Name.ToStringShort, pawn.Name.ToStringShort));
            }
            else
            {
                Log.Message(string.Format("[SocialInteractions] JoyGiver_GoOnDate.FindPartnerFor: No suitable partner found for pawn {0}.", pawn.Name.ToStringShort));
            }
            
            return partner;
        }
    }
}