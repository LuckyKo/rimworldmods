using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(JobDriver_LayDown), "MakeNewToils")]
    public static class JobDriver_LayDown_MakeNewToils_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(JobDriver_LayDown __instance, ref IEnumerable<Toil> __result)
        {
            Toil toil = new Toil();
            toil.initAction = delegate()
            {
                Pawn actor = __instance.pawn;
                Building_Bed bed = actor.CurrentBed();

                if (bed != null && bed.OwnersForReading.Contains(actor))
                {
                    Pawn spouse = null;
                    foreach (Pawn owner in bed.OwnersForReading)
                    {
                        if (owner != actor)
                        {
                            spouse = owner;
                            break;
                        }
                    }

                    if (spouse != null)
                    {
                        foreach (Pawn occupant in bed.CurOccupants)
                        {
                            if (occupant != actor && occupant != spouse)
                            {
                                // Found a cheater!
                                InteractionDef intDef = InteractionDef.Named("CaughtCheating");
                                actor.interactions.TryInteractWith(spouse, intDef);
                                break;
                            }
                        }
                    }
                }
            };
            __result = __result.AddItem(toil);
        }
    }
}
