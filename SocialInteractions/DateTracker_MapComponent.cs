using RimWorld;
using Verse;

namespace SocialInteractions
{
    public class DateTracker_MapComponent : MapComponent
    {
        public DateTracker_MapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Check every second
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                foreach (Pawn pawn in this.map.mapPawns.AllPawns)
                {
                    if (DatingManager.IsOnDate(pawn))
                    {
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        if (initiator != null && (initiator.CurJob == null || initiator.CurJob.def.joyKind == null))
                        {
                            // Initiator is no longer doing a joy activity, so end the date.
                            DatingManager.AdvanceDateStage(pawn);
                        }
                    }
                }
            }
        }
    }
}
