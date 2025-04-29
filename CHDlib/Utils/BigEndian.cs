using System;
using System.IO;

namespace CHDSharpLib.Utils;


public static class BigEndian
{  // Note this MODIFIES THE GIVEN ARRAY then returns a reference to the modified array.
    public static byte[] Reverse(this byte[] b)
    {
        Array.Reverse(b);
        return b;
    }

    public static UInt16 ReadUInt16BE(this BinaryReader binRdr)
    {
        return BitConverter.ToUInt16(binRdr.ReadBytesRequired(sizeof(UInt16)).Reverse(), 0);
    }

    public static Int16 ReadInt16BE(this BinaryReader binRdr)
    {
        return BitConverter.ToInt16(binRdr.ReadBytesRequired(sizeof(Int16)).Reverse(), 0);
    }


    public static UInt32 ReadUInt32BE(this BinaryReader binRdr)
    {
        return BitConverter.ToUInt32(binRdr.ReadBytesRequired(sizeof(UInt32)).Reverse(), 0);
    }
    public static UInt64 ReadUInt48BE(this BinaryReader binRdr)
    {
        return (UInt64)(binRdr.ReadByte() << 40 | binRdr.ReadByte() << 32 | binRdr.ReadByte() << 24 | binRdr.ReadByte() << 16 | binRdr.ReadByte() << 8 | binRdr.ReadByte() << 0);
    }

    public static UInt64 ReadUInt64BE(this BinaryReader binRdr)
    {
        return BitConverter.ToUInt64(binRdr.ReadBytesRequired(sizeof(UInt64)).Reverse(), 0);
    }

    public static Int32 ReadInt32BE(this BinaryReader binRdr)
    {
        return BitConverter.ToInt32(binRdr.ReadBytesRequired(sizeof(Int32)).Reverse(), 0);
    }

    public static byte[] ReadBytesRequired(this BinaryReader binRdr, int byteCount)
    {
        byte[] result = binRdr.ReadBytes(byteCount);

        if (result.Length != byteCount)
            throw new EndOfStreamException(string.Format("{0} bytes required from stream, but only {1} returned.", byteCount, result.Length));

        return result;
    }





    public static ushort ReadUInt16BE(this byte[] arr, int offset)
    {
        return (ushort)((arr[offset + 0] << 8) | arr[offset + 1]);
    }
    public static uint ReadUInt24BE(this byte[] arr, int offset)
    {
        return ((uint)arr[offset + 0] << 16) | ((uint)arr[offset + 1] << 8) | (uint)arr[offset + 2];
    }
    public static uint ReadUInt32BE(this byte[] arr, int offset)
    {
        return ((uint)arr[offset + 0] << 24) | ((uint)arr[offset + 1] << 16) | ((uint)arr[offset + 2] << 8) | (uint)arr[offset + 3];
    }
    public static ulong ReadUInt48BE(this byte[] arr, int offset)
    {
        return ((ulong)arr[offset + 0] << 40) | ((ulong)arr[offset + 1] << 32) |
               ((ulong)arr[offset + 2] << 24) | ((ulong)arr[offset + 3] << 16) | ((ulong)arr[offset + 4] << 8) | (ulong)arr[offset + 5];
    }

    public static void PutUInt16BE(this byte[] arr, int offset, uint value)
    {
        arr[offset++] = (byte)((value >> 8) & 0xFF);
        arr[offset++] = (byte)((value >> 0) & 0xFF);
    }
    public static void PutUInt24BE(this byte[] arr, int offset, uint value)
    {
        arr[offset++] = (byte)((value >> 16) & 0xFF);
        arr[offset++] = (byte)((value >> 8) & 0xFF);
        arr[offset++] = (byte)((value >> 0) & 0xFF);
    }
    public static void PutUInt48BE(this byte[] arr, int offset, ulong value)
    {
        arr[offset++] = (byte)((value >> 40) & 0xFF);
        arr[offset++] = (byte)((value >> 32) & 0xFF);
        arr[offset++] = (byte)((value >> 24) & 0xFF);
        arr[offset++] = (byte)((value >> 16) & 0xFF);
        arr[offset++] = (byte)((value >> 8) & 0xFF);
        arr[offset++] = (byte)((value >> 0) & 0xFF);
    }

}
