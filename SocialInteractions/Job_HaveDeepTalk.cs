using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class Job_HaveDeepTalk : Job
    {
        public InteractionDef interactionDef;
        public string subject;

        public Job_HaveDeepTalk() { }

        public Job_HaveDeepTalk(JobDef def, LocalTargetInfo targetA) : base(def, targetA)
        {
        }

        public new void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref interactionDef, "interactionDef");
            Scribe_Values.Look(ref subject, "subject");
        }
    }
}