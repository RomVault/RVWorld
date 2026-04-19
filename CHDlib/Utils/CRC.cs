using System;

namespace CHDSharpLib.Utils;

/// <summary>
/// CRC32 implementation used by CHD block verification.
/// </summary>
public class CRC
{
    /// <summary>
    /// Precomputed CRC32 lookup table.
    /// </summary>
    public static readonly uint[] CRC32Lookup;

    /// <summary>
    /// Current CRC accumulator (inverted).
    /// </summary>
    private uint _crc;

    /// <summary>
    /// Total number of bytes processed.
    /// </summary>
    private long _totalBytesRead;

    static CRC()
    {
        const uint polynomial = 0xEDB88320;
        const int crcNumTables = 8;

        unchecked
        {
            CRC32Lookup = new uint[256 * crcNumTables];
            int i;
            for (i = 0; i < 256; i++)
            {
                uint r = (uint)i;
                for (int j = 0; j < 8; j++)
                {
                    r = (r >> 1) ^ (polynomial & ~((r & 1) - 1));
                }

                CRC32Lookup[i] = r;
            }

            for (; i < 256 * crcNumTables; i++)
            {
                uint r = CRC32Lookup[i - 256];
                CRC32Lookup[i] = CRC32Lookup[r & 0xFF] ^ (r >> 8);
            }
        }
    }


    /// <summary>
    /// Creates a new CRC accumulator.
    /// </summary>
    public CRC()
    {
        Reset();
    }

    /// <summary>
    /// Resets the accumulator to its initial state.
    /// </summary>
    public void Reset()
    {
        _totalBytesRead = 0;
        _crc = 0xffffffffu;
    }


    /// <summary>
    /// Updates the CRC accumulator with a single byte.
    /// </summary>
    /// <param name="inCh">Byte value to incorporate (low 8 bits are used).</param>
    internal void UpdateCRC(int inCh)
    {
        _crc = (_crc >> 8) ^ CRC32Lookup[(byte)_crc ^ ((byte)inCh)];
    }

    /// <summary>
    /// Updates the accumulator with a block of bytes.
    /// </summary>
    /// <param name="block">Source buffer.</param>
    /// <param name="offset">Start offset in <paramref name="block"/>.</param>
    /// <param name="count">Number of bytes to process.</param>
    public void SlurpBlock(byte[] block, int offset, int count)
    {
        _totalBytesRead += count;
        uint crc = _crc;

        for (; (offset & 7) != 0 && count != 0; count--)
            crc = (crc >> 8) ^ CRC32Lookup[(byte)crc ^ block[offset++]];

        if (count >= 8)
        {
            int end = (count - 8) & ~7;
            count -= end;
            end += offset;

            while (offset != end)
            {
                crc ^= (uint)(block[offset] + (block[offset + 1] << 8) + (block[offset + 2] << 16) + (block[offset + 3] << 24));
                uint high = (uint)(block[offset + 4] + (block[offset + 5] << 8) + (block[offset + 6] << 16) + (block[offset + 7] << 24));
                offset += 8;

                crc = CRC32Lookup[(byte)crc + 0x700]
                      ^ CRC32Lookup[(byte)(crc >>= 8) + 0x600]
                      ^ CRC32Lookup[(byte)(crc >>= 8) + 0x500]
                      ^ CRC32Lookup[ /*(byte)*/(crc >> 8) + 0x400]
                      ^ CRC32Lookup[(byte)high + 0x300]
                      ^ CRC32Lookup[(byte)(high >>= 8) + 0x200]
                      ^ CRC32Lookup[(byte)(high >>= 8) + 0x100]
                      ^ CRC32Lookup[ /*(byte)*/(high >> 8) + 0x000];
            }
        }

        while (count-- != 0)
        {
            crc = (crc >> 8) ^ CRC32Lookup[(byte)crc ^ block[offset++]];
        }

        _crc = crc;

    }

    /// <summary>
    /// Gets the CRC32 result as a big-endian byte array.
    /// </summary>
    public byte[] Crc32ResultB
    {
        get
        {
            byte[] result = BitConverter.GetBytes(~_crc);
            Array.Reverse(result);
            return result;
        }
    }

    /// <summary>
    /// Gets the CRC32 result as a signed 32-bit integer.
    /// </summary>
    public Int32 Crc32Result => unchecked((Int32)(~_crc));

    /// <summary>
    /// Gets the CRC32 result as an unsigned 32-bit integer.
    /// </summary>
    public uint Crc32ResultU => ~_crc;

    /// <summary>
    /// Gets the total number of bytes processed by this accumulator.
    /// </summary>
    public Int64 TotalBytesRead => _totalBytesRead;

    /// <summary>
    /// Computes the CRC32 digest for a region of <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Source buffer.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="size">Number of bytes to process.</param>
    /// <returns>CRC32 digest.</returns>
    public static uint CalculateDigest(byte[] data, uint offset, uint size)
    {
        CRC crc = new CRC();
        // crc.Init();
        crc.SlurpBlock(data, (int)offset, (int)size);
        return crc.Crc32ResultU;
    }

    /// <summary>
    /// Verifies that the CRC32 of a region of <paramref name="data"/> matches <paramref name="digest"/>.
    /// </summary>
    /// <param name="digest">Expected CRC32 digest.</param>
    /// <param name="data">Source buffer.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="size">Number of bytes to process.</param>
    /// <returns>True when the CRC32 matches; otherwise false.</returns>
    public static bool VerifyDigest(uint digest, byte[] data, uint offset, uint size)
    {
        return (CalculateDigest(data, offset, size) == digest);
    }
}
