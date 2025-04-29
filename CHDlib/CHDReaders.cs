using CHDReaderTest.Flac.FlacDeps;
using CHDSharpLib.Utils;
using Compress.Support.Compression.LZMA;
using Compress.Support.Compression.zStd;
using CUETools.Codecs.Flake;
using System;
using System.IO;
using System.IO.Compression;

namespace CHDSharpLib;

internal delegate chd_error CHDReader(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec);

internal static partial class CHDReaders
{

    internal static chd_error zlib(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        return zlib(buffIn, 0, buffInLength, buffOut, buffOutLength);
    }
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


    internal static chd_error zstd(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        return zstd(buffIn, 0, buffInLength, buffOut, buffOutLength);
    }
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






    internal static chd_error lzma(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        return lzma(buffIn, 0, buffInLength, buffOut, buffOutLength, codec);
    }
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





    internal static chd_error flac(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        byte endianType = buffIn[0];
        //CHD adds a leading char to indicate endian. Not part of the flac format.
        bool swapEndian = (endianType == 'B'); //'L'ittle / 'B'ig
        return flac(buffIn, 1, buffInLength, buffOut, buffOutLength, swapEndian, codec, out _);
    }


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



    private const int CD_MAX_SECTOR_DATA = 2352;
    private const int CD_MAX_SUBCODE_DATA = 96;
    private static readonly int CD_FRAME_SIZE = CD_MAX_SECTOR_DATA + CD_MAX_SUBCODE_DATA;

    private static readonly byte[] s_cd_sync_header = new byte[] { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };

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