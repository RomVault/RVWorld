using System;
using System.Runtime.CompilerServices;
namespace CHDSharpLib.Utils;

internal static class Util
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAllZeroArray(byte[] b)
    {
        if (b == null) return true;
        for (int i = 0; i < b.Length; i++)
            if (b[i] != 0) return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ByteArrEquals(byte[] b0, byte[] b1)
    {
        if (b0 != null && ReferenceEquals(b0, b1))
        {
            return true;
        }
        if ((b0 == null) || (b1 == null))
        {
            return false;
        }
        if (b0.Length != b1.Length)
        {
            return false;
        }
        return b0.AsSpan().SequenceEqual(b1);
    }

    internal static int ByteArrCompare(byte[] x, byte[] y)
    {
        for (int i = 0; i < x.Length; i++)
        {
            int v = x[i].CompareTo(y[i]);
            if (v != 0)
                return v;
        }
        return 0;
    }

    internal static bool isAscii(byte[] bytes)
    {
        foreach (byte b in bytes)
        {
            if (b != 0 && b < 32)
                return false;
        }
        return true;
    }
}
