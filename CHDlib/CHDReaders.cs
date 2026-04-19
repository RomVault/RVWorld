using CHDReaderTest.Flac.FlacDeps;
using CHDSharpLib.Utils;
using Compress.Support.Compression.LZMA;
using Compress.Support.Compression.zStd;
using CUETools.Codecs.Flake;
using System;
using System.IO;
using System.IO.Compression;

namespace CHDSharpLib;

/// <summary>
/// Decoder function signature used by <see cref="CHDBlockRead"/> for CHD block decompression.
/// </summary>
/// <param name="buffIn">Input buffer containing compressed data.</param>
/// <param name="buffInLength">Length of compressed data within <paramref name="buffIn"/>.</param>
/// <param name="buffOut">Output buffer for decompressed data.</param>
/// <param name="buffOutLength">Expected decompressed output length.</param>
/// <param name="codec">Reusable codec state for the current operation.</param>
/// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
internal delegate chd_error CHDReader(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec);

/// <summary>
/// CHD block reader implementations for the supported compression codecs.
/// </summary>
/// <remarks>
/// Each method matches <see cref="CHDReader"/> and is selected based on the CHD header codec slots.
/// </remarks>
internal static partial class CHDReaders
{

    /// <summary>
    /// Decompresses a block using zlib/deflate.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error zlib(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        return zlib(buffIn, 0, buffInLength, buffOut, buffOutLength);
    }

    /// <summary>
    /// Decompresses a deflate payload from a region within <paramref name="buffIn"/>.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInStart">Start offset within <paramref name="buffIn"/>.</param>
    /// <param name="buffInLength">Number of bytes to read from <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    private static chd_error zlib(byte[] buffIn, int buffInStart, int buffInLength, byte[] buffOut, int buffOutLength)
    {
        using var memStream = new MemoryStream(buffIn, buffInStart, buffInLength, false);
        using var compStream = new DeflateStream(memStream, CompressionMode.Decompress, true);
        int bytesRead = 0;
        while (bytesRead < buffOutLength)
        {
            int bytes = compStream.Read(buffOut, bytesRead, buffOutLength - bytesRead);
            if (bytes == 0)
                return chd_error.CHDERR_INVALID_DATA;
            bytesRead += bytes;
        }
        return chd_error.CHDERR_NONE;
    }


    /// <summary>
    /// Decompresses a block using zstd.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error zstd(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        return zstd(buffIn, 0, buffInLength, buffOut, buffOutLength);
    }

    /// <summary>
    /// Decompresses a zstd payload from a region within <paramref name="buffIn"/>.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInStart">Start offset within <paramref name="buffIn"/>.</param>
    /// <param name="buffInLength">Number of bytes to read from <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    private static chd_error zstd(byte[] buffIn, int buffInStart, int buffInLength, byte[] buffOut, int buffOutLength)
    {
        using var memStream = new MemoryStream(buffIn, buffInStart, buffInLength, false);
        using var compStream = new RVZStdSharp(memStream);
        int bytesRead = 0;
        while (bytesRead < buffOutLength)
        {
            int bytes = compStream.Read(buffOut, bytesRead, buffOutLength - bytesRead);
            if (bytes == 0)
                return chd_error.CHDERR_INVALID_DATA;
            bytesRead += bytes;
        }
        return chd_error.CHDERR_NONE;
    }





    /// <summary>
    /// Decompresses a block using LZMA.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error lzma(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        return lzma(buffIn, 0, buffInLength, buffOut, buffOutLength, codec);
    }

    /// <summary>
    /// Decompresses an LZMA payload from a region within <paramref name="buffIn"/>.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInStart">Start offset within <paramref name="buffIn"/>.</param>
    /// <param name="compsize">Compressed payload length.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    private static chd_error lzma(byte[] buffIn, int buffInStart, int compsize, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        //hacky header creator
        byte[] properties = new byte[5];
        int posStateBits = 2;
        int numLiteralPosStateBits = 0;
        int numLiteralContextBits = 3;
        int dictionarySize = buffOutLength;
        properties[0] = (byte)((posStateBits * 5 + numLiteralPosStateBits) * 9 + numLiteralContextBits);
        for (int j = 0; j < 4; j++)
            properties[1 + j] = (Byte)((dictionarySize >> (8 * j)) & 0xFF);

        if (codec.blzma == null)
            codec.blzma = new byte[dictionarySize];

        using var memStream = new MemoryStream(buffIn, buffInStart, compsize, false);
        using Stream compStream = new LzmaStream(properties, memStream, -1, -1, null, false, codec.blzma);
        int bytesRead = 0;
        while (bytesRead < buffOutLength)
        {
            int bytes = compStream.Read(buffOut, bytesRead, buffOutLength - bytesRead);
            if (bytes == 0)
                return chd_error.CHDERR_INVALID_DATA;
            bytesRead += bytes;
        }

        return chd_error.CHDERR_NONE;
    }




    /// <summary>
    /// Decompresses a block encoded with CHD's Huffman scheme.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error huffman(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        if (codec.bHuffman == null)
            codec.bHuffman = new ushort[1 << 16];

        BitStream bitbuf = new BitStream(buffIn, 0, buffInLength);
        HuffmanDecoder hd = new HuffmanDecoder(256, 16, bitbuf, codec.bHuffman);

        if (hd.ImportTreeHuffman() != huffman_error.HUFFERR_NONE)
            return chd_error.CHDERR_INVALID_DATA;

        for (int j = 0; j < buffOutLength; j++)
        {
            buffOut[j] = (byte)hd.DecodeOne();
        }
        return chd_error.CHDERR_NONE;
    }


    /// <summary>
    /// Decompresses a FLAC-encoded block (used by CD FLAC codecs).
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error flac(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        byte endianType = buffIn[0];
        //CHD adds a leading char to indicate endian. Not part of the flac format.
        bool swapEndian = (endianType == 'B'); //'L'ittle / 'B'ig
        return flac(buffIn, 1, buffInLength, buffOut, buffOutLength, swapEndian, codec, out _);
    }

    /// <summary>
    /// Decompresses a raw FLAC payload from a region within <paramref name="buffIn"/>.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed data.</param>
    /// <param name="buffInStart">Start offset within <paramref name="buffIn"/>.</param>
    /// <param name="buffInLength">Number of bytes available from <paramref name="buffInStart"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed output.</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="swapEndian">If true, swaps 16-bit endianness of decoded PCM samples.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <param name="srcPos">Updated source offset into <paramref name="buffIn"/> after decoding.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    private static chd_error flac(byte[] buffIn, int buffInStart, int buffInLength, byte[] buffOut, int buffOutLength, bool swapEndian, CHDCodec codec, out int srcPos)
    {
        codec.FLAC_settings ??= new AudioPCMConfig(16, 2, 44100);
        codec.FLAC_audioDecoder ??= new AudioDecoder(codec.FLAC_settings);
        codec.FLAC_audioBuffer ??= new AudioBuffer(codec.FLAC_settings, buffOutLength); //audio buffer to take decoded samples and read them to bytes.

        srcPos = buffInStart;
        int dstPos = 0;
        //this may require some error handling. Hopefully the while condition is reliable
        while (dstPos < buffOutLength)
        {
            int read = codec.FLAC_audioDecoder.DecodeFrame(buffIn, srcPos, buffInLength - srcPos);
            codec.FLAC_audioDecoder.Read(codec.FLAC_audioBuffer, (int)codec.FLAC_audioDecoder.Remaining);
            Array.Copy(codec.FLAC_audioBuffer.Bytes, 0, buffOut, dstPos, codec.FLAC_audioBuffer.ByteLength);
            dstPos += codec.FLAC_audioBuffer.ByteLength;
            srcPos += read;
        }

        //Nanook - hack to support 16bit byte flipping - tested passes hunk CRC test
        if (swapEndian)
        {
            byte tmp;
            for (int i = 0; i < buffOutLength; i += 2)
            {
                tmp = buffOut[i];
                buffOut[i] = buffOut[i + 1];
                buffOut[i + 1] = tmp;
            }
        }
        return chd_error.CHDERR_NONE;
    }



    /******************* CD decoders **************************/



    /// <summary>
    /// Maximum sector payload in bytes for CD frames.
    /// </summary>
    private const int CD_MAX_SECTOR_DATA = 2352;

    /// <summary>
    /// Subcode payload in bytes for CD frames.
    /// </summary>
    private const int CD_MAX_SUBCODE_DATA = 96;

    /// <summary>
    /// Combined sector+subcode frame size.
    /// </summary>
    private static readonly int CD_FRAME_SIZE = CD_MAX_SECTOR_DATA + CD_MAX_SUBCODE_DATA;

    /// <summary>
    /// CD sync header bytes used when reconstructing sectors with regenerated ECC.
    /// </summary>
    private static readonly byte[] s_cd_sync_header = new byte[] { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };

    /// <summary>
    /// Decodes CD zlib blocks where sector and subcode streams are compressed separately.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed CD frame data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed frames (sector + subcode).</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error cdzlib(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        /* determine header bytes */
        int frames = buffOutLength / CD_FRAME_SIZE;
        int complen_bytes = (buffOutLength < 65536) ? 2 : 3;
        int ecc_bytes = (frames + 7) / 8;
        int header_bytes = ecc_bytes + complen_bytes;

        /* extract compressed length of base */
        int complen_base = (buffIn[ecc_bytes + 0] << 8) | buffIn[ecc_bytes + 1];
        if (complen_bytes > 2)
            complen_base = (complen_base << 8) | buffIn[ecc_bytes + 2];

        codec.bSector ??= new byte[frames * CD_MAX_SECTOR_DATA];
        codec.bSubcode ??= new byte[frames * CD_MAX_SUBCODE_DATA];

        chd_error err = zlib(buffIn, (int)header_bytes, complen_base, codec.bSector, frames * CD_MAX_SECTOR_DATA);
        if (err != chd_error.CHDERR_NONE)
            return err;

        err = zlib(buffIn, header_bytes + complen_base, buffInLength - header_bytes - complen_base, codec.bSubcode, frames * CD_MAX_SUBCODE_DATA);
        if (err != chd_error.CHDERR_NONE)
            return err;

        /* reassemble the data */
        for (int framenum = 0; framenum < frames; framenum++)
        {
            Array.Copy(codec.bSector, framenum * CD_MAX_SECTOR_DATA, buffOut, framenum * CD_FRAME_SIZE, CD_MAX_SECTOR_DATA);
            Array.Copy(codec.bSubcode, framenum * CD_MAX_SUBCODE_DATA, buffOut, framenum * CD_FRAME_SIZE + CD_MAX_SECTOR_DATA, CD_MAX_SUBCODE_DATA);

            // reconstitute the ECC data and sync header 
            int sectorStart = framenum * CD_FRAME_SIZE;
            if ((buffIn[framenum / 8] & (1 << (framenum % 8))) != 0)
            {
                Array.Copy(s_cd_sync_header, 0, buffOut, sectorStart, s_cd_sync_header.Length);
                cdRom.ecc_generate(buffOut, sectorStart);
            }
        }
        return chd_error.CHDERR_NONE;
    }

    /// <summary>
    /// Decodes CD zstd blocks where sector and subcode streams are compressed separately.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed CD frame data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed frames (sector + subcode).</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error cdzstd(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        /* determine header bytes */
        int frames = buffOutLength / CD_FRAME_SIZE;
        int complen_bytes = (buffOutLength < 65536) ? 2 : 3;
        int ecc_bytes = (frames + 7) / 8;
        int header_bytes = ecc_bytes + complen_bytes;

        /* extract compressed length of base */
        int complen_base = (buffIn[ecc_bytes + 0] << 8) | buffIn[ecc_bytes + 1];
        if (complen_bytes > 2)
            complen_base = (complen_base << 8) | buffIn[ecc_bytes + 2];

        codec.bSector ??= new byte[frames * CD_MAX_SECTOR_DATA];
        codec.bSubcode ??= new byte[frames * CD_MAX_SUBCODE_DATA];

        chd_error err = zstd(buffIn, (int)header_bytes, complen_base, codec.bSector, frames * CD_MAX_SECTOR_DATA);
        if (err != chd_error.CHDERR_NONE)
            return err;

        err = zstd(buffIn, header_bytes + complen_base, buffInLength - header_bytes - complen_base, codec.bSubcode, frames * CD_MAX_SUBCODE_DATA);
        if (err != chd_error.CHDERR_NONE)
            return err;

        /* reassemble the data */
        for (int framenum = 0; framenum < frames; framenum++)
        {
            Array.Copy(codec.bSector, framenum * CD_MAX_SECTOR_DATA, buffOut, framenum * CD_FRAME_SIZE, CD_MAX_SECTOR_DATA);
            Array.Copy(codec.bSubcode, framenum * CD_MAX_SUBCODE_DATA, buffOut, framenum * CD_FRAME_SIZE + CD_MAX_SECTOR_DATA, CD_MAX_SUBCODE_DATA);

            // reconstitute the ECC data and sync header 
            int sectorStart = framenum * CD_FRAME_SIZE;
            if ((buffIn[framenum / 8] & (1 << (framenum % 8))) != 0)
            {
                Array.Copy(s_cd_sync_header, 0, buffOut, sectorStart, s_cd_sync_header.Length);
                cdRom.ecc_generate(buffOut, sectorStart);
            }
        }
        return chd_error.CHDERR_NONE;
    }


    /// <summary>
    /// Decodes CD LZMA blocks where the sector stream is LZMA and the subcode stream is zlib.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed CD frame data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed frames (sector + subcode).</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error cdlzma(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        /* determine header bytes */
        int frames = buffOutLength / CD_FRAME_SIZE;
        int complen_bytes = (buffOutLength < 65536) ? 2 : 3;
        int ecc_bytes = (frames + 7) / 8;
        int header_bytes = ecc_bytes + complen_bytes;

        /* extract compressed length of base */
        int complen_base = ((buffIn[ecc_bytes + 0] << 8) | buffIn[ecc_bytes + 1]);
        if (complen_bytes > 2)
            complen_base = (complen_base << 8) | buffIn[ecc_bytes + 2];

        codec.bSector ??= new byte[frames * CD_MAX_SECTOR_DATA];
        codec.bSubcode ??= new byte[frames * CD_MAX_SUBCODE_DATA];

        chd_error err = lzma(buffIn, header_bytes, complen_base, codec.bSector, frames * CD_MAX_SECTOR_DATA, codec);
        if (err != chd_error.CHDERR_NONE)
            return err;

        err = zlib(buffIn, header_bytes + complen_base, buffInLength - header_bytes - complen_base, codec.bSubcode, frames * CD_MAX_SUBCODE_DATA);
        if (err != chd_error.CHDERR_NONE)
            return err;

        /* reassemble the data */
        for (int framenum = 0; framenum < frames; framenum++)
        {
            Array.Copy(codec.bSector, framenum * CD_MAX_SECTOR_DATA, buffOut, framenum * CD_FRAME_SIZE, CD_MAX_SECTOR_DATA);
            Array.Copy(codec.bSubcode, framenum * CD_MAX_SUBCODE_DATA, buffOut, framenum * CD_FRAME_SIZE + CD_MAX_SECTOR_DATA, CD_MAX_SUBCODE_DATA);

            // reconstitute the ECC data and sync header 
            int sectorStart = framenum * CD_FRAME_SIZE;
            if ((buffIn[framenum / 8] & (1 << (framenum % 8))) != 0)
            {
                Array.Copy(s_cd_sync_header, 0, buffOut, sectorStart, s_cd_sync_header.Length);
                cdRom.ecc_generate(buffOut, sectorStart);
            }
        }
        return chd_error.CHDERR_NONE;
    }


    /// <summary>
    /// Decodes CD FLAC blocks where audio sectors are FLAC-compressed and subcode is zlib-compressed.
    /// </summary>
    /// <param name="buffIn">Input buffer containing compressed CD frame data.</param>
    /// <param name="buffInLength">Length of compressed data in <paramref name="buffIn"/>.</param>
    /// <param name="buffOut">Destination buffer for decompressed frames (sector + subcode).</param>
    /// <param name="buffOutLength">Expected decompressed output length.</param>
    /// <param name="codec">Reusable codec state for the current operation.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error cdflac(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        int frames = buffOutLength / CD_FRAME_SIZE;

        codec.bSector ??= new byte[frames * CD_MAX_SECTOR_DATA];
        codec.bSubcode ??= new byte[frames * CD_MAX_SUBCODE_DATA];

        chd_error err = flac(buffIn, 0, buffInLength, codec.bSector, frames * CD_MAX_SECTOR_DATA, true, codec, out int pos);
        if (err != chd_error.CHDERR_NONE)
            return err;

        err = zlib(buffIn, pos, buffInLength - pos, codec.bSubcode, frames * CD_MAX_SUBCODE_DATA);
        if (err != chd_error.CHDERR_NONE)
            return err;

        /* reassemble the data */
        for (int framenum = 0; framenum < frames; framenum++)
        {
            Array.Copy(codec.bSector, framenum * CD_MAX_SECTOR_DATA, buffOut, framenum * CD_FRAME_SIZE, CD_MAX_SECTOR_DATA);
            Array.Copy(codec.bSubcode, framenum * CD_MAX_SUBCODE_DATA, buffOut, framenum * CD_FRAME_SIZE + CD_MAX_SECTOR_DATA, CD_MAX_SUBCODE_DATA);
        }
        return chd_error.CHDERR_NONE;
    }
}
