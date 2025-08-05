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

        public Job_HaveDeepTalk(JobDef def, LocalTargetInfo targetA, InteractionDef interactionDef, string subject) : base(def, targetA)
        {
            this.interactionDef = interactionDef;
            this.subject = subject;
        }
    }
}