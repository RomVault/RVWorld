using System;
using System.IO;

namespace CHDSharpLib.Utils;


/// <summary>
/// Big-endian binary helper and extension methods used by CHD readers.
/// </summary>
public static class BigEndian
{
    /// <summary>
    /// Reverses the input array in place and returns it.
    /// </summary>
    /// <param name="b">Array to reverse.</param>
    /// <returns>The same array instance after reversal.</returns>
    public static byte[] Reverse(this byte[] b)
    {
        Array.Reverse(b);
        return b;
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer in big-endian order.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <returns>Parsed value.</returns>
    public static UInt16 ReadUInt16BE(this BinaryReader binRdr)
    {
        return BitConverter.ToUInt16(binRdr.ReadBytesRequired(sizeof(UInt16)).Reverse(), 0);
    }

    /// <summary>
    /// Reads a 16-bit signed integer in big-endian order.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <returns>Parsed value.</returns>
    public static Int16 ReadInt16BE(this BinaryReader binRdr)
    {
        return BitConverter.ToInt16(binRdr.ReadBytesRequired(sizeof(Int16)).Reverse(), 0);
    }


    /// <summary>
    /// Reads a 32-bit unsigned integer in big-endian order.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <returns>Parsed value.</returns>
    public static UInt32 ReadUInt32BE(this BinaryReader binRdr)
    {
        return BitConverter.ToUInt32(binRdr.ReadBytesRequired(sizeof(UInt32)).Reverse(), 0);
    }

    /// <summary>
    /// Reads a 48-bit unsigned integer in big-endian order.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <returns>Parsed value.</returns>
    public static UInt64 ReadUInt48BE(this BinaryReader binRdr)
    {
        return (UInt64)(binRdr.ReadByte() << 40 | binRdr.ReadByte() << 32 | binRdr.ReadByte() << 24 | binRdr.ReadByte() << 16 | binRdr.ReadByte() << 8 | binRdr.ReadByte() << 0);
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer in big-endian order.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <returns>Parsed value.</returns>
    public static UInt64 ReadUInt64BE(this BinaryReader binRdr)
    {
        return BitConverter.ToUInt64(binRdr.ReadBytesRequired(sizeof(UInt64)).Reverse(), 0);
    }

    /// <summary>
    /// Reads a 32-bit signed integer in big-endian order.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <returns>Parsed value.</returns>
    public static Int32 ReadInt32BE(this BinaryReader binRdr)
    {
        return BitConverter.ToInt32(binRdr.ReadBytesRequired(sizeof(Int32)).Reverse(), 0);
    }

    /// <summary>
    /// Reads exactly <paramref name="byteCount"/> bytes, throwing if the stream ends early.
    /// </summary>
    /// <param name="binRdr">Binary reader to read from.</param>
    /// <param name="byteCount">Number of bytes required.</param>
    /// <returns>Array containing the requested bytes.</returns>
    public static byte[] ReadBytesRequired(this BinaryReader binRdr, int byteCount)
    {
        byte[] result = binRdr.ReadBytes(byteCount);

        if (result.Length != byteCount)
            throw new EndOfStreamException(string.Format("{0} bytes required from stream, but only {1} returned.", byteCount, result.Length));

        return result;
    }





    /// <summary>
    /// Reads a 16-bit unsigned integer from a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Source array.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Parsed value.</returns>
    public static ushort ReadUInt16BE(this byte[] arr, int offset)
    {
        return (ushort)((arr[offset + 0] << 8) | arr[offset + 1]);
    }

    /// <summary>
    /// Reads a 24-bit unsigned integer from a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Source array.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Parsed value.</returns>
    public static uint ReadUInt24BE(this byte[] arr, int offset)
    {
        return ((uint)arr[offset + 0] << 16) | ((uint)arr[offset + 1] << 8) | (uint)arr[offset + 2];
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Source array.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Parsed value.</returns>
    public static uint ReadUInt32BE(this byte[] arr, int offset)
    {
        return ((uint)arr[offset + 0] << 24) | ((uint)arr[offset + 1] << 16) | ((uint)arr[offset + 2] << 8) | (uint)arr[offset + 3];
    }

    /// <summary>
    /// Reads a 48-bit unsigned integer from a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Source array.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Parsed value.</returns>
    public static ulong ReadUInt48BE(this byte[] arr, int offset)
    {
        return ((ulong)arr[offset + 0] << 40) | ((ulong)arr[offset + 1] << 32) |
               ((ulong)arr[offset + 2] << 24) | ((ulong)arr[offset + 3] << 16) | ((ulong)arr[offset + 4] << 8) | (ulong)arr[offset + 5];
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer into a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Destination array.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="value">Value to write.</param>
    public static void PutUInt16BE(this byte[] arr, int offset, uint value)
    {
        arr[offset++] = (byte)((value >> 8) & 0xFF);
        arr[offset++] = (byte)((value >> 0) & 0xFF);
    }

    /// <summary>
    /// Writes a 24-bit unsigned integer into a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Destination array.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="value">Value to write (low 24 bits are used).</param>
    public static void PutUInt24BE(this byte[] arr, int offset, uint value)
    {
        arr[offset++] = (byte)((value >> 16) & 0xFF);
        arr[offset++] = (byte)((value >> 8) & 0xFF);
        arr[offset++] = (byte)((value >> 0) & 0xFF);
    }

    /// <summary>
    /// Writes a 48-bit unsigned integer into a byte array in big-endian order.
    /// </summary>
    /// <param name="arr">Destination array.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="value">Value to write (low 48 bits are used).</param>
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
