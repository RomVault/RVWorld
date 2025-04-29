using CHDReaderTest.Flac.FlacDeps;
using CHDSharpLib.Utils;
using CUETools.Codecs.Flake;
using System;

namespace CHDSharpLib;
internal static partial class CHDReaders
{
    /*
     Source input buffer structure:

     Header:
     00     =  Size of the Meta Data to be put into the output buffer right after the header.
     01     =  Number of Audio Channel.
     02,03  =  Number of Audio sampled values per chunk.
     04,05  =  width in pixels of image.
     06,07  =  height in pixels of image.
     08,09  =  Size of the source data for the audio channels huffman trees. (set to 0xffff is using FLAC.)

     10,11  =  size of compressed audio channel 1
     12,13  =  size of compressed audio channel 2
     .
     .         (Max audio channels coded to 16)
     Total Header size = 10 + 2 * Number of Audio Channels.


     Meta Data: (Size from header 00)

     Audio Huffman Tree: (Size from header 08,09)

     Audio Compressed Data Channels: (Repeated for each Audio Channel, Size from Header starting at 10,11)

     Video Compressed Data:   Rest of Input Chuck.

    */

    internal static chd_error avHuff(byte[] buffIn, int buffInLength, byte[] buffOut, int buffOutLength, CHDCodec codec)
    {
        // extract info from the header
        if (buffInLength < 8)
            return chd_error.CHDERR_INVALID_DATA;
        uint metaDataLength = buffIn[0];
        uint audioChannels = buffIn[1];
        uint audioSamplesPerBlock = buffIn.ReadUInt16BE(2);
        uint videoWidth = buffIn.ReadUInt16BE(4);
        uint videoHeight = buffIn.ReadUInt16BE(6);

        uint sourceTotalSize = 10 + 2 * audioChannels;
        // validate that the sizes make sense
        if (buffInLength < sourceTotalSize)
            return chd_error.CHDERR_INVALID_DATA;

        sourceTotalSize += metaDataLength;

        uint audioHuffmanTreeSize = buffIn.ReadUInt16BE(8);
        if (audioHuffmanTreeSize != 0xffff)
            sourceTotalSize += audioHuffmanTreeSize;

        uint?[] audioChannelCompressedSize = new uint?[16];
        for (int chnum = 0; chnum < audioChannels; chnum++)
        {
            audioChannelCompressedSize[chnum] = buffIn.ReadUInt16BE(10 + 2 * chnum);
            sourceTotalSize += (uint)audioChannelCompressedSize[chnum];
        }

        if (sourceTotalSize >= buffInLength)
            return chd_error.CHDERR_INVALID_DATA;

        // starting offsets of source data
        uint buffInIndex = 10 + 2 * audioChannels;


        uint destOffset = 0;
        // create a header
        buffOut[0] = (byte)'c';
        buffOut[1] = (byte)'h';
        buffOut[2] = (byte)'a';
        buffOut[3] = (byte)'v';
        buffOut[4] = (byte)metaDataLength;
        buffOut[5] = (byte)audioChannels;
        buffOut[6] = (byte)(audioSamplesPerBlock >> 8);
        buffOut[7] = (byte)audioSamplesPerBlock;
        buffOut[8] = (byte)(videoWidth >> 8);
        buffOut[9] = (byte)videoWidth;
        buffOut[10] = (byte)(videoHeight >> 8);
        buffOut[11] = (byte)videoHeight;
        destOffset += 12;



        uint metaDestStart = destOffset;
        if (metaDataLength > 0)
        {
            Array.Copy(buffIn, (int)buffInIndex, buffOut, (int)metaDestStart, (int)metaDataLength);
            buffInIndex += metaDataLength;
            destOffset += metaDataLength;
        }

        uint?[] audioChannelDestStart = new uint?[16];
        for (int chnum = 0; chnum < audioChannels; chnum++)
        {
            audioChannelDestStart[chnum] = destOffset;
            destOffset += 2 * audioSamplesPerBlock;
        }
        uint videoDestStart = destOffset;


        // decode the audio channels
        if (audioChannels > 0)
        {
            // decode the audio
            chd_error err = DecodeAudio(audioChannels, audioSamplesPerBlock, buffIn, buffInIndex, audioHuffmanTreeSize, audioChannelCompressedSize, buffOut, audioChannelDestStart, codec);
            if (err != chd_error.CHDERR_NONE)
                return err;

            // advance the pointers past the data
            if (audioHuffmanTreeSize != 0xffff)
                buffInIndex += audioHuffmanTreeSize;
            for (int chnum = 0; chnum < audioChannels; chnum++)
                buffInIndex += (uint)audioChannelCompressedSize[chnum];
        }

        // decode the video data
        if (videoWidth > 0 && videoHeight > 0)
        {
            uint videostride = 2 * videoWidth;
            // decode the video
            chd_error err = DecodeVideo(videoWidth, videoHeight, buffIn, buffInIndex, (uint)buffInLength - buffInIndex, buffOut, videoDestStart, videostride, codec);
            if (err != chd_error.CHDERR_NONE)
                return err;
        }

        uint videoEnd = videoDestStart + videoWidth * videoHeight * 2;
        for (uint index = videoEnd; index < buffOutLength; index++)
            buffOut[index] = 0;

        return chd_error.CHDERR_NONE;
    }


    private static chd_error DecodeAudio(uint channels, uint samples, byte[] buffIn, uint buffInOffset, uint treesize, uint?[] audioChannelCompressedSize, byte[] buffOut, uint?[] audioChannelDestStart, CHDCodec codec)
    {
        // if the tree size is 0xffff, the streams are FLAC-encoded
        if (treesize == 0xffff)
        {
            int blockSize = (int)samples * 2;

            // loop over channels
            for (int channelNumber = 0; channelNumber < channels; channelNumber++)
            {
                // extract the size of this channel
                uint sourceSize = audioChannelCompressedSize[channelNumber] ?? 0;

                uint? curdest = audioChannelDestStart[channelNumber];
                if (curdest != null)
                {
                    codec.AVHUFF_settings ??= new AudioPCMConfig(16, 1, 48000);
                    codec.AVHUFF_audioDecoder ??= new AudioDecoder(codec.AVHUFF_settings); //read the data and decode it in to a 1D array of samples - the buffer seems to want 2D :S
                    AudioBuffer audioBuffer = new AudioBuffer(codec.AVHUFF_settings, blockSize); //audio buffer to take decoded samples and read them to bytes.
                    int read;
                    int inPos = (int)buffInOffset;
                    int outPos = (int)audioChannelDestStart[channelNumber];

                    while (outPos < blockSize + audioChannelDestStart[channelNumber])
                    {
                        if ((read = codec.AVHUFF_audioDecoder.DecodeFrame(buffIn, inPos, (int)sourceSize)) == 0)
                            break;
                        if (codec.AVHUFF_audioDecoder.Remaining != 0)
                        {
                            codec.AVHUFF_audioDecoder.Read(audioBuffer, (int)codec.AVHUFF_audioDecoder.Remaining);
                            Array.Copy(audioBuffer.Bytes, 0, buffOut, outPos, audioBuffer.ByteLength);
                            outPos += audioBuffer.ByteLength;
                        }
                        inPos += read;
                    }

                    byte tmp;
                    for (int i = (int)audioChannelDestStart[channelNumber]; i < blockSize + audioChannelDestStart[channelNumber]; i += 2)
                    {
                        tmp = buffOut[i];
                        buffOut[i] = buffOut[i + 1];
                        buffOut[i + 1] = tmp;
                    }

                }

                // advance to the next channel's data
                buffInOffset += sourceSize;
            }
            return chd_error.CHDERR_NONE;
        }


        // if we have a non-zero tree size, extract the trees
        HuffmanDecoder m_audiohi_decoder = null;
        HuffmanDecoder m_audiolo_decoder = null;
        if (treesize != 0)
        {
            BitStream bitbuf = new BitStream(buffIn, (int)buffInOffset, (int)treesize);

            if (codec.bHuffmanHi == null) codec.bHuffmanHi = new ushort[1 << 16];
            if (codec.bHuffmanLo == null) codec.bHuffmanLo = new ushort[1 << 16];

            m_audiohi_decoder = new HuffmanDecoder(256, 16, bitbuf, codec.bHuffmanHi);
            m_audiolo_decoder = new HuffmanDecoder(256, 16, bitbuf, codec.bHuffmanLo);

            huffman_error hufferr = m_audiohi_decoder.ImportTreeRLE();
            if (hufferr != huffman_error.HUFFERR_NONE)
                return chd_error.CHDERR_INVALID_DATA;
            bitbuf.flush();
            hufferr = m_audiolo_decoder.ImportTreeRLE();
            if (hufferr != huffman_error.HUFFERR_NONE)
                return chd_error.CHDERR_INVALID_DATA;
            if (bitbuf.flush() != treesize)
                return chd_error.CHDERR_INVALID_DATA;
            buffInOffset += treesize;
        }

        // loop over channels
        for (int chnum = 0; chnum < channels; chnum++)
        {
            // only process if the data is requested
            uint? curdest = audioChannelDestStart[chnum];
            if (curdest != null)
            {
                int prevsample = 0;

                // if no huffman length, just copy the data
                if (treesize == 0)
                {
                    uint cursource = buffInOffset;
                    for (int sampnum = 0; sampnum < samples; sampnum++)
                    {
                        int delta = (buffIn[cursource + 0] << 8) | buffIn[cursource + 1];
                        cursource += 2;

                        int newsample = prevsample + delta;
                        prevsample = newsample;

                        buffOut[(uint)curdest + 0] = (byte)(newsample >> 8);
                        buffOut[(uint)curdest + 1] = (byte)newsample;
                        curdest += 2;
                    }
                }

                // otherwise, Huffman-decode the data
                else
                {
                    BitStream bitbuf = new BitStream(buffIn, (int)buffInOffset, (int)audioChannelCompressedSize[chnum]);
                    m_audiohi_decoder.AssignBitStream(bitbuf);
                    m_audiolo_decoder.AssignBitStream(bitbuf);
                    for (int sampnum = 0; sampnum < samples; sampnum++)
                    {
                        short delta = (short)(m_audiohi_decoder.DecodeOne() << 8);
                        delta |= (short)m_audiolo_decoder.DecodeOne();

                        int newsample = prevsample + delta;
                        prevsample = newsample;

                        buffOut[(uint)curdest + 0] = (byte)(newsample >> 8);
                        buffOut[(uint)curdest + 1] = (byte)newsample;
                        curdest += 2;
                    }
                    if (bitbuf.overflow())
                        return chd_error.CHDERR_INVALID_DATA;
                }
            }

            // advance to the next channel's data
            buffInOffset += (uint)audioChannelCompressedSize[chnum];
        }
        return chd_error.CHDERR_NONE;
    }



    private static chd_error DecodeVideo(uint width, uint height, byte[] buffIn, uint buffInOffset, uint buffInLength, byte[] buffOut, uint buffOutOffset, uint dstride, CHDCodec codec)
    {
        // if the high bit of the first byte is set, we decode losslessly
        if ((buffIn[buffInOffset] & 0x80) == 0)
            return chd_error.CHDERR_INVALID_DATA;

        // skip the first byte
        BitStream bitbuf = new BitStream(buffIn, (int)buffInOffset, (int)buffInLength);
        bitbuf.read(8);

        if (codec.bHuffmanY == null) codec.bHuffmanY = new ushort[1 << 16];
        if (codec.bHuffmanCB == null) codec.bHuffmanCB = new ushort[1 << 16];
        if (codec.bHuffmanCR == null) codec.bHuffmanCR = new ushort[1 << 16];

        HuffmanDecoderRLE m_ycontext = new HuffmanDecoderRLE(256 + 16, 16, bitbuf, codec.bHuffmanY);
        HuffmanDecoderRLE m_cbcontext = new HuffmanDecoderRLE(256 + 16, 16, bitbuf, codec.bHuffmanCB);
        HuffmanDecoderRLE m_crcontext = new HuffmanDecoderRLE(256 + 16, 16, bitbuf, codec.bHuffmanCR);

        // import the tables
        huffman_error hufferr = m_ycontext.ImportTreeRLE();
        if (hufferr != huffman_error.HUFFERR_NONE)
            return chd_error.CHDERR_INVALID_DATA;
        bitbuf.flush();
        hufferr = m_cbcontext.ImportTreeRLE();
        if (hufferr != huffman_error.HUFFERR_NONE)
            return chd_error.CHDERR_INVALID_DATA;
        bitbuf.flush();
        hufferr = m_crcontext.ImportTreeRLE();
        if (hufferr != huffman_error.HUFFERR_NONE)
            return chd_error.CHDERR_INVALID_DATA;
        bitbuf.flush();

        // decode to the destination
        m_ycontext.Reset();
        m_cbcontext.Reset();
        m_crcontext.Reset();

        for (int dy = 0; dy < height; dy++)
        {
            uint row = buffOutOffset + (uint)dy * dstride;
            for (int dx = 0; dx < width / 2; dx++)
            {
                buffOut[row + 0] = (byte)m_ycontext.DecodeOne();
                buffOut[row + 1] = (byte)m_cbcontext.DecodeOne();
                buffOut[row + 2] = (byte)m_ycontext.DecodeOne();
                buffOut[row + 3] = (byte)m_crcontext.DecodeOne();
                row += 4;
            }
            m_ycontext.FlushRLE();
            m_cbcontext.FlushRLE();
            m_crcontext.FlushRLE();
        }

        // check for errors if we overflowed or decoded too little data
        if (bitbuf.overflow() || bitbuf.flush() != buffInLength)
            return chd_error.CHDERR_INVALID_DATA;
        return chd_error.CHDERR_NONE;
    }



}

