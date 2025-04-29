using CHDSharpLib.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;

namespace CHDSharpLib
{
    internal static class CHDBlockRead
    {
        // search for all COMPRESSION_SELF block, and increase the counter of the block it is referencing.
        // the first time the referenced block is decompressed a copy of its data is kept.
        // this copy is then used (instead of re-decompressing.) until the use count returns to zero
        // at which time the backup copy if removed.

        internal static void FindRepeatedBlocks(CHDHeader chd, Message consoleOut)
        {
            int totalFound = 0;
            int[] compressionCount = new int[5];
            int[] compressionSelfCount = new int[5];
            int[] compressionUniqueCount = new int[5];

            Parallel.ForEach(chd.map, me =>
            {
                if (me.comptype != compression_type.COMPRESSION_SELF)
                {
                    if ((int)me.comptype < 5)
                        Interlocked.Increment(ref compressionCount[(int)me.comptype]);
                    return;
                }
                me.selfMapEntry = chd.map[me.offset];
                switch (me.selfMapEntry.comptype)
                {
                    case compression_type.COMPRESSION_TYPE_0:
                    case compression_type.COMPRESSION_TYPE_1:
                    case compression_type.COMPRESSION_TYPE_2:
                    case compression_type.COMPRESSION_TYPE_3:
                    case compression_type.COMPRESSION_NONE:
                        break;
                    default:
                        consoleOut?.Invoke($"Error {me.selfMapEntry.comptype}");
                        break;
                }

                if (consoleOut == null)
                {
                    // this is the value that is actually needed, the rest are just for display info.
                    Interlocked.Increment(ref me.selfMapEntry.UseCount);
                }
                else
                {
                    lock (me.selfMapEntry)
                    {
                        // this is the value that is actually needed, the rest are just for display info.
                        Interlocked.Increment(ref me.selfMapEntry.UseCount);
                        if (me.selfMapEntry.UseCount == 1)
                            Interlocked.Increment(ref compressionUniqueCount[(int)me.selfMapEntry.comptype]);
                    }

                    Interlocked.Increment(ref compressionSelfCount[(int)me.selfMapEntry.comptype]);
                    Interlocked.Increment(ref totalFound);
                }
            });

            if (consoleOut != null)
            {
                consoleOut.Invoke($"Total Blocks {chd.map.Length},  Repeat Blocks {totalFound},  Output Block Size {chd.blocksize}");
                for (int i = 0; i < 5; i++)
                {
                    if (compressionCount[i] == 0 & compressionSelfCount[i] == 0)
                        continue;
                    string comp = "";
                    if (i < chd.compression.Length)
                    {
                        comp = chd.compression[i].ToString().Substring(10);
                    }
                    else if (i == 4)
                    {
                        comp = "NONE";
                    }

                    consoleOut?.Invoke($"Compression {i} : {comp} : Block Count {compressionCount[i]},  Repeat Source Block Count {compressionUniqueCount[i]},  Repeat Total Block Count {compressionSelfCount[i]}");
                }
            }
        }

        internal static void KeepMostRepeatedBlocks(CHDHeader chd, int blocksToKeep, Message consoleOut)
        {
            List<mapentry> mapentries = new List<mapentry>();
            foreach (mapentry me in chd.map)
            {
                if (me.UseCount > 0)
                {
                    me.UsageWeight = GetWeigth(chd, me) * me.UseCount;
                    mapentries.Add(me);
                }
            }

            consoleOut?.Invoke($"{mapentries.Count} repeated used blocks");
            if (mapentries.Count < blocksToKeep)
                return;

            mapentries.Sort((a, b) => b.UsageWeight.CompareTo(a.UsageWeight));

            int c = 0;
            foreach (mapentry me in mapentries)
            {
                if (c < blocksToKeep)
                {
                    c++;
                    me.KeepBufferCopy = true;
                    continue;
                }

                me.KeepBufferCopy = false;
                me.UseCount = 0;
            }

            Parallel.ForEach(chd.map, me =>
            {
                if (me.comptype != compression_type.COMPRESSION_SELF)
                    return;
                // this should never be true
                if (me.selfMapEntry == null)
                    return;

                if (me.selfMapEntry.KeepBufferCopy)
                    return;

                me.comptype = me.selfMapEntry.comptype;
                me.length = me.selfMapEntry.length;
                me.offset = me.selfMapEntry.offset;
                me.crc = me.selfMapEntry.crc;
                me.crc16 = me.selfMapEntry.crc16;
                me.selfMapEntry = null;
            });
        }
        internal static int GetWeigth(CHDHeader chd, mapentry me)
        {
            if (me.comptype == compression_type.COMPRESSION_NONE)
                return 1;

            switch (chd.compression[(int)me.comptype])
            {
                case chd_codec.CHD_CODEC_ZLIB: return 1;
                case chd_codec.CHD_CODEC_ZSTD: return 1;
                case chd_codec.CHD_CODEC_LZMA: return 23;
                case chd_codec.CHD_CODEC_HUFFMAN: return 64;
                case chd_codec.CHD_CODEC_FLAC: return (me.length == 41) ? 1 : 2;


                case chd_codec.CHD_CODEC_CD_ZLIB: return 3;
                case chd_codec.CHD_CODEC_CD_ZSTD: return 1;
                case chd_codec.CHD_CODEC_CD_LZMA: return 18;
                case chd_codec.CHD_CODEC_CD_FLAC: return (me.length == 15) ? 1 : 2;

                case chd_codec.CHD_CODEC_AVHUFF: return 1;
                default: return 1;
            }
        }

        internal static void FindBlockReaders(CHDHeader chd)
        {
            chd.chdReader = new CHDReader[chd.compression.Length];
            for (int i = 0; i < chd.compression.Length; i++)
                chd.chdReader[i] = GetReaderFromCodec(chd.compression[i]);
        }

        private static CHDReader GetReaderFromCodec(chd_codec chdCodec)
        {
            switch (chdCodec)
            {
                case chd_codec.CHD_CODEC_ZLIB: return CHDReaders.zlib;
                case chd_codec.CHD_CODEC_ZSTD: return CHDReaders.zstd;
                case chd_codec.CHD_CODEC_LZMA: return CHDReaders.lzma;
                case chd_codec.CHD_CODEC_HUFFMAN: return CHDReaders.huffman;
                case chd_codec.CHD_CODEC_FLAC: return CHDReaders.flac;
                case chd_codec.CHD_CODEC_CD_ZLIB: return CHDReaders.cdzlib;
                case chd_codec.CHD_CODEC_CD_ZSTD: return CHDReaders.cdzstd;
                case chd_codec.CHD_CODEC_CD_LZMA: return CHDReaders.cdlzma;
                case chd_codec.CHD_CODEC_CD_FLAC: return CHDReaders.cdflac;
                case chd_codec.CHD_CODEC_AVHUFF: return CHDReaders.avHuff;
                default: return null;
            }
        }

        internal static chd_error ReadBlock(mapentry mapentry, ArrayPool arrPool, CHDReader[] compression, CHDCodec codec, byte[] buffOut, int buffOutLength)
        {
            bool checkCrc = true;

            switch (mapentry.comptype)
            {
                case compression_type.COMPRESSION_TYPE_0:
                case compression_type.COMPRESSION_TYPE_1:
                case compression_type.COMPRESSION_TYPE_2:
                case compression_type.COMPRESSION_TYPE_3:
                    {
                        lock (mapentry)
                        {
                            if (mapentry.buffOutCache == null)
                            {
                                chd_error ret = chd_error.CHDERR_UNSUPPORTED_FORMAT;
                                ret = compression[(int)mapentry.comptype].Invoke(mapentry.buffIn, (int)mapentry.length, buffOut, buffOutLength, codec);

                                if (ret != chd_error.CHDERR_NONE)
                                    return ret;

                                // if this block is re-used keep a copy of it.
                                if (mapentry.UseCount > 0)
                                {
                                    mapentry.buffOutCache = arrPool.Rent();
                                    Array.Copy(buffOut, 0, mapentry.buffOutCache, 0, buffOutLength);
                                }
                                break;
                            }

                            Array.Copy(mapentry.buffOutCache, 0, buffOut, 0, (int)buffOutLength);

                            Interlocked.Decrement(ref mapentry.UseCount);
                            if (mapentry.UseCount == 0)
                            {
                                arrPool.Return(mapentry.buffOutCache);
                                mapentry.buffOutCache = null;
                            }
                            checkCrc = false;
                        }
                        break;
                    }
                case compression_type.COMPRESSION_NONE:
                    {
                        lock (mapentry)
                        {
                            if (mapentry.buffOutCache == null)
                            {
                                Array.Copy(mapentry.buffIn, 0, buffOut, 0, buffOutLength);

                                if (mapentry.UseCount > 0)
                                {
                                    mapentry.buffOutCache = arrPool.Rent();
                                    Array.Copy(buffOut, 0, mapentry.buffOutCache, 0, buffOutLength);
                                }
                                break;
                            }


                            Array.Copy(mapentry.buffOutCache, 0, buffOut, 0, (int)buffOutLength);
                            Interlocked.Decrement(ref mapentry.UseCount);
                            if (mapentry.UseCount == 0)
                            {
                                arrPool.Return(mapentry.buffOutCache);
                                mapentry.buffOutCache = null;
                            }

                            checkCrc = false;
                        }
                        break;
                    }

                case compression_type.COMPRESSION_MINI:
                    {
                        byte[] tmp = BitConverter.GetBytes(mapentry.offset);
                        for (int i = 0; i < 8; i++)
                        {
                            buffOut[i] = tmp[7 - i];
                        }

                        for (int i = 8; i < buffOutLength; i++)
                        {
                            buffOut[i] = buffOut[i - 8];
                        }

                        break;
                    }

                case compression_type.COMPRESSION_SELF:
                    {
                        chd_error retcs = ReadBlock(mapentry.selfMapEntry, arrPool, compression, codec, buffOut, buffOutLength);
                        if (retcs != chd_error.CHDERR_NONE)
                            return retcs;
                        // check CRC in the read_block_into_cache call
                        checkCrc = false;
                        break;
                    }
                default:
                    return chd_error.CHDERR_DECOMPRESSION_ERROR;

            }

            if (checkCrc)
            {
                if (mapentry.crc != null && !CRC.VerifyDigest((uint)mapentry.crc, buffOut, 0, (uint)buffOutLength))
                    return chd_error.CHDERR_DECOMPRESSION_ERROR;
                if (mapentry.crc16 != null && CRC16.calc(buffOut, (int)buffOutLength) != mapentry.crc16)
                    return chd_error.CHDERR_DECOMPRESSION_ERROR;
            }
            return chd_error.CHDERR_NONE;
        }

    }
}
