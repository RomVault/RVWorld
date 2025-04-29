using CHDSharpLib.Utils;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CHDSharpLib;

internal static class CHDHeaders
{
    public static chd_error ReadHeaderV1(Stream file, out CHDHeader chd)
    {
        chd = new CHDHeader();

        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        chd.compression = chd.compression = new chd_codec[] { chd_codec.CHD_CODEC_ZLIB };
        uint flags = br.ReadUInt32BE();
        uint compression = br.ReadUInt32BE();
        chd.blocksize = br.ReadUInt32BE();
        chd.totalblocks = br.ReadUInt32BE();
        uint cylinders = br.ReadUInt32BE();
        uint heads = br.ReadUInt32BE();
        uint sectors = br.ReadUInt32BE();
        chd.md5 = br.ReadBytes(16);
        chd.parentmd5 = br.ReadBytes(16);


        const int HARD_DISK_SECTOR_SIZE = 512;
        chd.totalbytes = cylinders * heads * sectors * HARD_DISK_SECTOR_SIZE;
        chd.blocksize = chd.blocksize * HARD_DISK_SECTOR_SIZE;

        chd.map = new mapentry[chd.totalblocks];

        Dictionary<ulong, int> mapBack = new Dictionary<ulong, int>();

        for (int i = 0; i < chd.totalblocks; i++)
        {
            ulong tmpu = br.ReadUInt64BE();
            chd.map[i] = new mapentry();


            if (mapBack.TryGetValue(tmpu, out int v))
            {
                chd.map[i].offset = (uint)v;
                chd.map[i].length = 0;
                chd.map[i].comptype = compression_type.COMPRESSION_SELF;
                continue;
            }

            mapBack.Add(tmpu, i);

            chd.map[i].offset = tmpu & 0xfffffffffff;
            chd.map[i].length = (uint)(tmpu >> 44);
            chd.map[i].comptype = (chd.map[i].length == chd.blocksize)
                           ? compression_type.COMPRESSION_NONE
                           : compression_type.COMPRESSION_TYPE_0;
        }

        return chd_error.CHDERR_NONE;
    }

    public static chd_error ReadHeaderV2(Stream file, out CHDHeader chd)
    {
        chd = new CHDHeader();

        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        chd.compression = chd.compression = new chd_codec[] { chd_codec.CHD_CODEC_ZLIB };
        uint flags = br.ReadUInt32BE();
        uint compression = br.ReadUInt32BE();
        uint blocksizeOld = br.ReadUInt32BE(); // this is now unused
        chd.totalblocks = br.ReadUInt32BE();
        uint cylinders = br.ReadUInt32BE();
        uint heads = br.ReadUInt32BE();
        uint sectors = br.ReadUInt32BE();
        chd.md5 = br.ReadBytes(16);
        chd.parentmd5 = br.ReadBytes(16);
        chd.blocksize = br.ReadUInt32BE(); // blocksize added to header in V2

        const int HARD_DISK_SECTOR_SIZE = 512;
        chd.totalbytes = cylinders * heads * sectors * HARD_DISK_SECTOR_SIZE;

        chd.map = new mapentry[chd.totalblocks];

        Dictionary<ulong, int> mapBack = new Dictionary<ulong, int>();

        for (int i = 0; i < chd.totalblocks; i++)
        {
            ulong tmpu = br.ReadUInt64BE();
            chd.map[i] = new mapentry();


            if (mapBack.TryGetValue(tmpu, out int v))
            {
                chd.map[i].offset = (uint)v;
                chd.map[i].length = 0;
                chd.map[i].comptype = compression_type.COMPRESSION_SELF;
                continue;
            }

            mapBack.Add(tmpu, i);

            chd.map[i].offset = tmpu & 0xfffffffffff;
            chd.map[i].length = (uint)(tmpu >> 44);
            chd.map[i].comptype = (chd.map[i].length == chd.blocksize)
                           ? compression_type.COMPRESSION_NONE
                           : compression_type.COMPRESSION_TYPE_0;
        }


        return chd_error.CHDERR_NONE;
    }

    public static chd_error ReadHeaderV3(Stream file, out CHDHeader chd)
    {
        chd = new CHDHeader();
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        uint flags = br.ReadUInt32BE();

        chd.compression = new chd_codec[] { CHDCommon.compTypeConv(br.ReadUInt32BE()) };
        chd.totalblocks = br.ReadUInt32BE(); // total number of CHD Blocks

        chd.totalbytes = br.ReadUInt64BE();  // total byte size of the image
        chd.metaoffset = br.ReadUInt64BE();

        chd.md5 = br.ReadBytes(16);
        chd.parentmd5 = br.ReadBytes(16);
        chd.blocksize = br.ReadUInt32BE();    // length of a CHD Block
        chd.rawsha1 = br.ReadBytes(20);
        chd.parentsha1 = br.ReadBytes(20);

        chd.map = new mapentry[chd.totalblocks];

        for (int i = 0; i < chd.totalblocks; i++)
        {
            chd.map[i] = new mapentry();
            chd.map[i].offset = br.ReadUInt64BE();
            chd.map[i].crc = br.ReadUInt32BE();
            chd.map[i].length = (uint)((br.ReadByte() << 8) | (br.ReadByte() << 0) | (br.ReadByte() << 16));
            mapFlags mapflag = (mapFlags)br.ReadByte();
            chd.map[i].comptype = CHDCommon.ConvMapFlagstoCompressionType(mapflag);
            if ((mapflag & mapFlags.MAP_ENTRY_FLAG_NO_CRC) != 0)
                chd.map[i].crc = null;
        }
        return chd_error.CHDERR_NONE;
    }

    public static chd_error ReadHeaderV4(Stream file, out CHDHeader chd)
    {
        chd = new CHDHeader();
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        uint flags = br.ReadUInt32BE();

        chd.compression = new chd_codec[] { CHDCommon.compTypeConv(br.ReadUInt32BE()) };
        chd.totalblocks = br.ReadUInt32BE(); // total number of CHD Blocks

        chd.totalbytes = br.ReadUInt64BE();  // total byte size of the image
        chd.metaoffset = br.ReadUInt64BE();

        chd.blocksize = br.ReadUInt32BE();    // length of a CHD Block
        chd.sha1 = br.ReadBytes(20);
        chd.parentsha1 = br.ReadBytes(20);
        chd.rawsha1 = br.ReadBytes(20);

        chd.map = new mapentry[chd.totalblocks];

        for (int i = 0; i < chd.totalblocks; i++)
        {
            chd.map[i] = new mapentry();
            chd.map[i].offset = br.ReadUInt64BE();
            chd.map[i].crc = br.ReadUInt32BE();
            chd.map[i].length = (uint)((br.ReadUInt16BE()) | (br.ReadByte() << 16));
            mapFlags mapflag = (mapFlags)br.ReadByte();
            chd.map[i].comptype = CHDCommon.ConvMapFlagstoCompressionType(mapflag);
            chd.map[i].crc = null;
        }
        return chd_error.CHDERR_NONE;
    }


    public static chd_error ReadHeaderV5(Stream file, out CHDHeader chd)
    {
        chd = new CHDHeader();
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        chd.compression = new chd_codec[4];
        for (int i = 0; i < 4; i++)
            chd.compression[i] = (chd_codec)br.ReadUInt32BE();

        chd.totalbytes = br.ReadUInt64BE();  // total byte size of the image
        ulong mapoffset = br.ReadUInt64BE();
        chd.metaoffset = br.ReadUInt64BE();

        chd.blocksize = br.ReadUInt32BE();    // length of a CHD Hunk (Block)
        uint unitbytes = br.ReadUInt32BE();
        chd.rawsha1 = br.ReadBytes(20);
        chd.sha1 = br.ReadBytes(20);
        chd.parentsha1 = br.ReadBytes(20);

        chd.totalblocks = (uint)((chd.totalbytes + chd.blocksize - 1) / chd.blocksize);

        bool chdCompressed = chd.compression[0] != chd_codec.CHD_CODEC_NONE;

        chd_error err = chdCompressed ?
                compressed_v5_map(br, mapoffset, chd.totalblocks, chd.blocksize, unitbytes, out chd.map) :
                uncompressed_v5_map(br, mapoffset, chd.totalblocks, chd.blocksize, out chd.map);

        return err;
    }


    private static chd_error uncompressed_v5_map(BinaryReader br, ulong mapoffset, uint totalblocks, uint blocksize, out mapentry[] map)
    {
        br.BaseStream.Seek((long)mapoffset, SeekOrigin.Begin);

        map = new mapentry[totalblocks];
        for (int blockIndex = 0; blockIndex < totalblocks; blockIndex++)
        {
            map[blockIndex] = new mapentry();
            map[blockIndex].comptype = compression_type.COMPRESSION_NONE;
            map[blockIndex].length = blocksize;
            map[blockIndex].offset = br.ReadUInt32BE() * blocksize;
        }
        return chd_error.CHDERR_NONE;
    }

    private static chd_error compressed_v5_map(BinaryReader br, ulong mapoffset, uint totalBlocks, uint blocksize, uint unitbytes, out mapentry[] map)
    {
        map = new mapentry[totalBlocks];

        /* read the reader */
        br.BaseStream.Seek((long)mapoffset, SeekOrigin.Begin);
        uint mapbytes = br.ReadUInt32BE();   //0
        ulong firstoffs = br.ReadUInt48BE(); //4
        ushort mapcrc = br.ReadUInt16BE();   //10
        byte lengthbits = br.ReadByte();     //12
        byte selfbits = br.ReadByte();       //13
        byte parentbits = br.ReadByte();     //14
        br.ReadByte();                       //15 not used

        byte[] compressed_arr = new byte[mapbytes];
        br.BaseStream.Read(compressed_arr, 0, (int)mapbytes);

        BitStream bitbuf = new BitStream(compressed_arr, 0, (int)mapbytes);

        /* first decode the compression types */
        HuffmanDecoder decoder = new HuffmanDecoder(16, 8, bitbuf);
        if (decoder == null)
        {
            return chd_error.CHDERR_OUT_OF_MEMORY;
        }

        huffman_error err = decoder.ImportTreeRLE();
        if (err != huffman_error.HUFFERR_NONE)
        {
            return chd_error.CHDERR_DECOMPRESSION_ERROR;
        }

        int repcount = 0;
        compression_type lastcomp = 0;
        for (uint blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            map[blockIndex] = new mapentry();
            if (repcount > 0)
            {
                map[blockIndex].comptype = lastcomp;
                repcount--;
            }
            else
            {
                compression_type val = (compression_type)decoder.DecodeOne();
                if (val == compression_type.COMPRESSION_RLE_SMALL)
                {
                    map[blockIndex].comptype = lastcomp;
                    repcount = 2 + (int)decoder.DecodeOne();
                }
                else if (val == compression_type.COMPRESSION_RLE_LARGE)
                {
                    map[blockIndex].comptype = lastcomp;
                    repcount = 2 + 16 + ((int)decoder.DecodeOne() << 4);
                    repcount += (int)decoder.DecodeOne();
                }
                else
                    map[blockIndex].comptype = lastcomp = val;
            }
        }

        /* then iterate through the hunks and extract the needed data */
        uint last_self = 0;
        ulong last_parent = 0;
        ulong curoffset = firstoffs;
        for (uint blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            ulong offset = curoffset;
            uint length = 0;
            ushort crc16 = 0;
            switch (map[blockIndex].comptype)
            {
                /* base types */
                case compression_type.COMPRESSION_TYPE_0:
                case compression_type.COMPRESSION_TYPE_1:
                case compression_type.COMPRESSION_TYPE_2:
                case compression_type.COMPRESSION_TYPE_3:
                    curoffset += length = bitbuf.read(lengthbits);
                    crc16 = (ushort)bitbuf.read(16);
                    break;

                case compression_type.COMPRESSION_NONE:
                    curoffset += length = blocksize;
                    crc16 = (ushort)bitbuf.read(16);
                    break;

                case compression_type.COMPRESSION_SELF:
                    last_self = (uint)(offset = bitbuf.read(selfbits));
                    break;

                /* pseudo-types; convert into base types */
                case compression_type.COMPRESSION_SELF_1:
                    last_self++;
                    goto case compression_type.COMPRESSION_SELF_0;

                case compression_type.COMPRESSION_SELF_0:
                    map[blockIndex].comptype = compression_type.COMPRESSION_SELF;
                    offset = last_self;
                    break;

                case compression_type.COMPRESSION_PARENT_SELF:
                    map[blockIndex].comptype = compression_type.COMPRESSION_PARENT;
                    last_parent = offset = (((ulong)blockIndex) * ((ulong)blocksize)) / unitbytes;
                    break;

                case compression_type.COMPRESSION_PARENT:
                    offset = bitbuf.read(parentbits);
                    last_parent = offset;
                    break;

                case compression_type.COMPRESSION_PARENT_1:
                    last_parent += blocksize / unitbytes;
                    goto case compression_type.COMPRESSION_PARENT_0;
                case compression_type.COMPRESSION_PARENT_0:
                    map[blockIndex].comptype = compression_type.COMPRESSION_PARENT;
                    offset = last_parent;
                    break;
            }
            map[blockIndex].length = length;
            map[blockIndex].offset = offset;
            map[blockIndex].crc16 = crc16;
        }


        /* verify the final CRC */
        byte[] rawmap = new byte[totalBlocks * 12];
        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            int rawmapIndex = blockIndex * 12;
            rawmap[rawmapIndex] = (byte)map[blockIndex].comptype;
            rawmap.PutUInt24BE(rawmapIndex + 1, map[blockIndex].length);
            rawmap.PutUInt48BE(rawmapIndex + 4, map[blockIndex].offset);
            rawmap.PutUInt16BE(rawmapIndex + 10, (uint)map[blockIndex].crc16);
        }
        if (CRC16.calc(rawmap, (int)totalBlocks * 12) != mapcrc)
            return chd_error.CHDERR_DECOMPRESSION_ERROR;

        return chd_error.CHDERR_NONE;
    }

}
