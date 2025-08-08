using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;

namespace SocialInteractions
{
    public enum DateStage
    {
        Joy,
        Lovin,
        Finished
    }

    public class Date
    {
        public Pawn Initiator;
        public Pawn Partner;
        public DateStage Stage;

        public Date(Pawn initiator, Pawn partner)
        {
            this.Initiator = initiator;
            this.Partner = partner;
            this.Stage = DateStage.Joy;
        }
    }

    public static class DatingManager
    {
        private static List<Date> dates = new List<Date>();

        public static void StartDate(Pawn initiator, Pawn partner)
        {
            if (!IsOnDate(initiator) && !IsOnDate(partner))
            {
                Log.Message(string.Format("[SocialInteractions] Starting date between {0} and {1}.", initiator.Name.ToStringShort, partner.Name.ToStringShort));
                dates.Add(new Date(initiator, partner));
            }
        }

        public static void EndDate(Pawn pawn)
        {
            Date date = GetDateWith(pawn);
            if (date != null)
            {
                Log.Message(string.Format("[SocialInteractions] Ending date for {0} and {1}.", date.Initiator.Name.ToStringShort, date.Partner.Name.ToStringShort));
                dates.RemoveAll(d => d.Initiator == pawn || d.Partner == pawn);
            }
        }

        public static bool IsOnDate(Pawn pawn)
        {
            return GetDateWith(pawn) != null;
        }

        public static Date GetDateWith(Pawn pawn)
        {
            return dates.FirstOrDefault(d => d.Initiator == pawn || d.Partner == pawn);
        }

        public static Pawn GetPartnerOnDateWith(Pawn pawn)
        {
            Date date = GetDateWith(pawn);
            if (date != null)
            {
                return date.Initiator == pawn ? date.Partner : date.Initiator;
            }
            return null;
        }

        public static Pawn GetInitiatorOfDateWith(Pawn pawn)
        {
            Date date = GetDateWith(pawn);
            if (date != null)
            {
                return date.Initiator;
            }
            return null;
        }

        public static void AdvanceDateStage(Pawn pawn)
        {
            Date date = GetDateWith(pawn);
            if (date != null)
            {
                Log.Message(string.Format("[SocialInteractions] Advancing date stage for {0} and {1}. Current stage: {2}", date.Initiator.Name.ToStringShort, date.Partner.Name.ToStringShort, date.Stage));
                if (date.Stage == DateStage.Joy)
                {
                    Log.Message("[SocialInteractions] Transitioning from Joy to Lovin stage.");
                    // End the partner's FollowAndWatch job.
                    if (date.Partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
                    {
                        date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }

                    // Attempt to transition to the lovin' stage.
                    if (CanHaveLovin(date.Initiator, date.Partner))
                    {
                        Log.Message("[SocialInteractions] Conditions for lovin' met. Assigning lovin' job.");
                        date.Stage = DateStage.Lovin;
                        Building_Bed bed = RestUtility.FindBedFor(date.Initiator);
                        Job lovinJob = JobMaker.MakeJob(JobDefOf.Lovin, date.Partner, bed);
                        date.Initiator.jobs.StartJob(lovinJob, JobCondition.InterruptForced);
                        Job lovinJobPartner = JobMaker.MakeJob(JobDefOf.Lovin, date.Initiator, bed);
                        date.Partner.jobs.StartJob(lovinJobPartner, JobCondition.InterruptForced);

                        string subject = SpeechBubbleManager.GetDateEndSubject(date.Initiator, date.Partner);
                        SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, subject);
                    }
                    else
                    {
                        Log.Message("[SocialInteractions] Conditions for lovin' not met. Ending date.");
                        // If they can't have lovin', end the date.
                        date.Stage = DateStage.Finished;
                        EndDate(pawn);
                    }
                }
                else if (date.Stage == DateStage.Lovin)
                {
                    Log.Message("[SocialInteractions] Lovin' stage finished. Ending date.");
                    // After lovin', the date is finished.
                    date.Stage = DateStage.Finished;
                    EndDate(pawn);
                }
            }
        }

        private static bool CanHaveLovin(Pawn initiator, Pawn partner)
        {
            bool hasBed = RestUtility.FindBedFor(initiator) != null && RestUtility.FindBedFor(partner) != null;
            Log.Message(string.Format("[SocialInteractions] CanHaveLovin check for {0} and {1}. HasBed: {2}", initiator.Name.ToStringShort, partner.Name.ToStringShort, hasBed));
            return hasBed;
        }
    }
}