namespace CHDSharpLib.Utils;

/// <summary>
/// Bit-level reader used by CHD map decoding and Huffman/RLE decoding.
/// </summary>
internal class BitStream
{
    /// <summary>
    /// Bit buffer containing up to 32 bits of prefetched data.
    /// </summary>
    private uint buffer;

    /// <summary>
    /// Number of valid bits currently in <see cref="buffer"/>.
    /// </summary>
    private int bits;

    /// <summary>
    /// Source buffer.
    /// </summary>
    private byte[] readBuffer;

    /// <summary>
    /// Current byte offset into <see cref="readBuffer"/>.
    /// </summary>
    private int doffset;

    /// <summary>
    /// End offset (exclusive) of readable data within <see cref="readBuffer"/>.
    /// </summary>
    private int dlength;

    /// <summary>
    /// Initial offset used to compute consumed byte counts.
    /// </summary>
    private int initialOffset = 0;

    /// <summary>
    /// Indicates whether reads have gone past the end of the provided source window.
    /// </summary>
    /// <returns>True when an overflow is detected; otherwise false.</returns>
    public bool overflow()
    {
        return doffset - bits / 8 > dlength;
    }

    /// <summary>
    /// Creates a bit reader over a window of the provided source buffer.
    /// </summary>
    /// <param name="src">Source buffer.</param>
    /// <param name="offset">Start offset within <paramref name="src"/>.</param>
    /// <param name="length">Length of the readable window.</param>
    public BitStream(byte[] src, int offset, int length)
    {
        buffer = 0;
        bits = 0;
        readBuffer = src;
        doffset = initialOffset = offset;
        dlength = offset + length;
    }

    /// <summary>
    /// Fetches the requested number of bits without advancing the read position.
    /// </summary>
    /// <param name="numbits">Number of bits to read.</param>
    /// <returns>Value containing the requested bits in the low bits.</returns>
    public uint peek(int numbits)
    {
        if (numbits == 0)
            return 0;

        /* fetch data if we need more */
        if (numbits > bits)
        {
            while (bits <= 24)
            {
                if (doffset < dlength)
                    buffer |= (uint)readBuffer[doffset] << 24 - bits;
                doffset++;
                bits += 8;
            }
        }

        /* return the data */
        return buffer >> 32 - numbits;
    }

    /// <summary>
    /// Advances the read position by the specified number of bits.
    /// </summary>
    /// <param name="numbits">Number of bits to consume.</param>
    public void remove(int numbits)
    {
        buffer <<= numbits;
        bits -= numbits;
    }


    /// <summary>
    /// Fetches and consumes the requested number of bits.
    /// </summary>
    /// <param name="numbits">Number of bits to read.</param>
    /// <returns>Value containing the requested bits in the low bits.</returns>
    public uint read(int numbits)
    {
        uint result = peek(numbits);
        remove(numbits);
        return result;
    }

    /// <summary>
    /// Flushes the reader to the nearest byte boundary.
    /// </summary>
    /// <returns>Number of bytes consumed from the initial offset.</returns>
    public int flush()
    {
        while (bits >= 8)
        {
            doffset--;
            bits -= 8;
        }
        bits = 0;
        buffer = 0;
        return doffset - initialOffset;
    }


}
