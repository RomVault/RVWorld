using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CHDlib
{
    internal class hard_disk_info
    {
        public UInt32 length;       /* length of header data */
        public UInt32 version;      /* drive format version */

        public UInt32 flags;        /* flags field */
        public UInt32 compression;  /* compression type */
        public UInt32[] compressions;  /* compression type array*/
        public UInt32 totalblocks;  /* total # of blocks represented */

        public byte[] md5;          /* MD5 checksum for this drive */
        public byte[] parentmd5;    /* MD5 checksum for parent drive */
        public byte[] sha1;         /* SHA1 checksum for this drive */
        public byte[] parentsha1;   /* SHA1 checksum for parent drive */
        public byte[] rawsha1;
        public UInt64 mapoffset;
        public UInt64 metaoffset;

        public UInt32 blocksize;     /* number of bytes per hunk */
        public UInt32 unitbytes;

        public UInt64 totalbytes;




        public Stream file;

        public mapentry[] map;
    }
    [Flags]
    internal enum mapFlags
    {
        MAP_ENTRY_FLAG_TYPE_MASK = 0x000f,      /* what type of hunk */
        MAP_ENTRY_FLAG_NO_CRC = 0x0010,         /* no CRC is present */

        MAP_ENTRY_TYPE_INVALID = 0x0000,        /* invalid type */
        MAP_ENTRY_TYPE_COMPRESSED = 0x0001,     /* standard compression */
        MAP_ENTRY_TYPE_UNCOMPRESSED = 0x0002,   /* uncompressed data */
        MAP_ENTRY_TYPE_MINI = 0x0003,           /* mini: use offset as raw data */
        MAP_ENTRY_TYPE_SELF_HUNK = 0x0004,      /* same as another hunk in this file */
        MAP_ENTRY_TYPE_PARENT_HUNK = 0x0005     /* same as a hunk in the parent file */
    }


    internal class mapentry
    {
        public UInt64 offset;
        public UInt32 crc;
        public UInt64 length;
        public mapFlags flags;

        public int UseCount;
        public byte[] BlockCache = null;
    }

}
