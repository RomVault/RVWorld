using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RVUtils;

public static class ByteUtils
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByteArray(this BinaryWriter bw, byte[] b)
    {
        bw.Write((byte)b.Length);
        bw.Write(b);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadByteArray(this BinaryReader br)
    {
        byte len = br.ReadByte();
        return br.ReadBytes(len);
    }

    public static byte[] Copy(this byte[] b)
    {
        if (b == null)
        {
            return null;
        }
        byte[] retB = new byte[b.Length];
        Array.Copy(b, 0, retB, 0, b.Length);
        return retB;
    }

    public static byte[] Copy(this byte[] b, int index, int count)
    {
        if (b == null)
        {
            return null;
        }
        byte[] retB = new byte[count];
        Array.Copy(b, index, retB, 0, count);
        return retB;
    }


    public static bool IsAllZeroArray(byte[] b)
    {
        if (b == null) return true;
        for (int i = 0; i < b.Length; i++)
            if (b[i] != 0) return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ByteArrEqualsQuick(byte[] b0, byte[] b1)
    {
        return b0.AsSpan().SequenceEqual(b1);
    }


    // was BCompare
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ByteArrEquals(byte[] b0, byte[] b1)
    {
        if ((b0 == null) || (b1 == null)) return false;
        if (b0.Length != b1.Length) return false;

        return b0.AsSpan().SequenceEqual(b1);
    }

    // was NCompare
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ByteArrEqualsNull(byte[] b1, byte[] b2)
    {
        if ((b1 == null) || (b2 == null)) return true;
        if (b1.Length != b2.Length) return false;

        return b1.AsSpan().SequenceEqual(b2);
    }

    public static int ByteArrCompare(byte[] b0, byte[] b1)
    {
        int b1Len = b0 == null ? 0 : b0.Length;
        int b2Len = b1 == null ? 0 : b1.Length;


        int p = 0;
        for (; ; )
        {
            if (b1Len == p) return b2Len == p ? 0 : -1;
            if (b2Len == p) return 1;
            if (b0[p] < b1[p]) return -1;
            if (b0[p] > b1[p]) return 1;
            p++;
        }
    }

    public static int ByteArrCompareSort(byte[] b0, byte[] b1)
    {
        for (int i = 0; i < b0.Length; i++)
        {
            int v = b0[i].CompareTo(b1[i]);
            if (v != 0)
                return v;
        }
        return 0;
    }

    public static bool isAscii(byte[] bytes)
    {
        foreach (byte b in bytes)
        {
            if (b != 0 && b < 32)
                return false;
        }
        return true;
    }


    //https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa#24343727
    private static readonly uint[] Lookup32 = CreateLookup32();

    private static uint[] CreateLookup32()
    {
        uint[] result = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            string s = i.ToString("X2").ToLower();
            result[i] = s[0] + ((uint)s[1] << 16);
        }
        return result;
    }
    private static string ByteArrayToHexViaLookup32(byte[] bytes)
    {
        char[] result = new char[bytes.Length * 2];
        int c = 0;
        foreach (byte b in bytes)
        {
            uint val = Lookup32[b];
            result[c++] = (char)val;
            result[c++] = (char)(val >> 16);
        }
        return new string(result);
    }

    public static string ToHexString(this byte[] b)
    {
        return b == null ? "" : ByteArrayToHexViaLookup32(b);
    }
}
