/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
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
    }
}