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

        
    }
}