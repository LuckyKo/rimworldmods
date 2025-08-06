using Verse;
using System.Collections.Generic;
using System;

namespace SocialInteractions
{
    public static class DatingManager
    {
        private static List<Tuple<Pawn, Pawn>> datingCouples = new List<Tuple<Pawn, Pawn>>();

        public static void StartDate(Pawn pawn1, Pawn pawn2)
        {
            if (!IsOnDate(pawn1) && !IsOnDate(pawn2))
            {
                datingCouples.Add(new Tuple<Pawn, Pawn>(pawn1, pawn2));
            }
        }

        public static void EndDate(Pawn pawn)
        {
            datingCouples.RemoveAll(t => t.Item1 == pawn || t.Item2 == pawn);
        }

        public static bool IsOnDate(Pawn pawn)
        {
            return GetPartnerOnDateWith(pawn) != null;
        }

        public static Pawn GetPartnerOnDateWith(Pawn pawn)
        {
            foreach (var couple in datingCouples)
            {
                if (couple.Item1 == pawn) return couple.Item2;
                if (couple.Item2 == pawn) return couple.Item1;
            }
            return null;
        }
    }
}