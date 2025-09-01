using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(HistoryEventsManager), "RecordEvent")]
    public static class HistoryEventsManager_Patch
    {
        public static void Postfix(HistoryEvent historyEvent)
        {
            // We only care about the Bonded event for now
            if (historyEvent.def != HistoryEventDefOf.Bonded)
            {
                return;
            }

            Pawn doer = historyEvent.args.GetArg<Pawn>(HistoryEventArgsNames.Doer);
            if (doer == null || !doer.IsColonistPlayerControlled)
            {
                return;
            }

            string subject = " bonded with an animal";

            SocialInteractions.HandleMonologue(doer, subject);
        }
    }
}