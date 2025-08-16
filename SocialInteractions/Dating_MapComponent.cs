using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class Dating_MapComponent : MapComponent
    {
        public Dating_MapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Check for pawns with the SI_Naked hediff that are not in the lovin' job
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                if (pawn.health.hediffSet.HasHediff(HediffDef.Named("SI_Naked")))
                {
                    if (pawn.jobs.curDriver == null || !(pawn.jobs.curDriver is JobDriver_DateLovin))
                    {
                        SLog.Message(string.Format("[SocialInteractions] Found pawn {0} with SI_Naked hediff but not in lovin' job. Removing hediff.", pawn.LabelShort));
                        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                        if (hediff != null)
                        {
                            pawn.health.RemoveHediff(hediff);
                        }
                    }
                }
            }
        }
    }
}
