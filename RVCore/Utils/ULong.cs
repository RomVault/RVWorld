/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;

namespace RVCore.Utils
{
    public static class ULong
    {
        public static int iCompare(ulong? a, ulong? b)
        {
            if (a == null || b == null)
            {
                ReportError.SendAndShow("comparing null ulong? ");
                return -1;
            }
            return Math.Sign(((ulong) a).CompareTo((ulong) b));
        }

        public static int iCompareNull(ulong? v0, ulong? v1)
        {
            if (v0 == null && v1 == null)
                return 0;
            if (v0 != null && v1 == null)
                return 1;
            if (v0 == null && v1 != null)
                return -1;
            return ((ulong)v0).CompareTo((ulong)v1);

        }

    }
}