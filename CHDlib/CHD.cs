/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.IO;
using System.Text;
using File = RVIO.File;
using FileStream = RVIO.FileStream;
using Path = RVIO.Path;

namespace CHDlib
{
    public enum hdErr
    {
        HDERR_NONE,
        //HDERR_NO_INTERFACE,
        HDERR_OUT_OF_MEMORY,
        HDERR_INVALID_FILE,
        //HDERR_INVALID_PARAMETER,
        HDERR_INVALID_DATA,
        HDERR_FILE_NOT_FOUND,
        //HDERR_REQUIRES_PARENT,
        //HDERR_FILE_NOT_WRITEABLE,
        HDERR_READ_ERROR,
        //HDERR_WRITE_ERROR,
        //HDERR_CODEC_ERROR,
        //HDERR_INVALID_PARENT,
        //HDERR_SECTOR_OUT_OF_RANGE,
        HDERR_DECOMPRESSION_ERROR,
        //HDERR_COMPRESSION_ERROR,
        //HDERR_CANT_CREATE_FILE,
        HDERR_CANT_VERIFY,
        HDERR_UNSUPPORTED,

        HDERR_CANNOT_OPEN_FILE,
        HDERR_CHDMAN_NOT_FOUND
    };

    public delegate void Message(string message);
    public delegate void FileMessage(string filename, string message);

    public static class CHD
    {
        public static Message fileProcess; // returns the name of the file being processed
        public static Message fileProgress; // returns the progress of the file

        public static Message fileSystemError; // returns if the file could not be opened.
        public static FileMessage fileError; // returns the error in the file (corrupt CHD)
        public static Message generalError; // returns any other exceptions

        private const int MaxHeader = 124;

        public static hdErr CheckFile(string file, string directory, bool isLinux, ref bool deepCheck, out uint? chdVersion, out byte[] chdSHA1, out byte[] chdMD5, ref bool fileErrorAbort)
        {
            chdSHA1 = null;
            chdMD5 = null;
            chdVersion = null;
            string filename = Path.Combine(directory, file);
            fileProcess?.Invoke(filename);

            //string ext = Path.GetExtension(filename).ToLower();
            //if (ext != ".chd")
            //{
            //    return hdErr.HDERR_INVALID_FILE;
            //}

            if (!File.Exists(filename))
            {
                fileSystemError?.Invoke("File: " + filename + " Error: File Could not be opened.");
                fileErrorAbort = true;
                return hdErr.HDERR_CANNOT_OPEN_FILE;
            }

            Stream s;
            int retval = FileStream.OpenFileRead(filename, out s);
            if (retval != 0)
            {
                fileSystemError?.Invoke("File: " + filename + " Error: File Could not be opened.");
                fileErrorAbort = true;
                return hdErr.HDERR_CANNOT_OPEN_FILE;
            }
            if (s == null)
            {
                fileSystemError?.Invoke("File: " + filename + " Error: File Could not be opened.");
                fileErrorAbort = true;
                return hdErr.HDERR_CANNOT_OPEN_FILE;
            }
            if (s.Length < MaxHeader)
            {
                s.Close();
                s.Dispose();
                return hdErr.HDERR_INVALID_FILE;
            }
            hard_disk_info hdi = new hard_disk_info();
            hdErr res = ReadCHDHeader(s, ref hdi);
            if (res != hdErr.HDERR_NONE)
            {
                return res;
            }
            chdVersion = hdi.version;
            chdMD5 = hdi.md5;
            chdSHA1 = hdi.sha1;


            if (!deepCheck)
            {
                s.Close();
                s.Dispose();
                return res;
            }

            string error = null;
            if (hdi.version <4 && hdi.compression<3)
            {
                hdi.file = s;
                CHDLocalCheck clc = new CHDLocalCheck();
                res = clc.ChdCheck(fileProgress, hdi, out error);

                s.Close();
                s.Dispose();
            }
            else
            {
                s.Close();
                s.Dispose();

                CHDManCheck cmc = new CHDManCheck();
                res = cmc.ChdCheck(fileProgress, isLinux, filename, out error);
            }

            switch (res)
            {
                case hdErr.HDERR_NONE:
                    break;
                case hdErr.HDERR_CHDMAN_NOT_FOUND:
                    deepCheck = false;
                    res = hdErr.HDERR_NONE;
                    break;
                case hdErr.HDERR_DECOMPRESSION_ERROR:
                    fileError?.Invoke(filename, error);
                    break;
                case hdErr.HDERR_FILE_NOT_FOUND:
                    fileSystemError?.Invoke("File: " + filename + " Error: Not Found scan Aborted.");
                    fileErrorAbort = true;
                    break;
                default:
                    generalError?.Invoke(res + " " + error);
                    break;
            }

            return res;
        }


        private static readonly uint[] HeaderLengths = new uint[] { 0, 76, 80, 120, 108, 124 };
        private static readonly byte[] id = { (byte)'M', (byte)'C', (byte)'o', (byte)'m', (byte)'p', (byte)'r', (byte)'H', (byte)'D' };

        private static hdErr ReadCHDHeader(Stream file, ref hard_disk_info hardDisk)
        {
            for (int i = 0; i < id.Length; i++)
            {
                byte b = (byte)file.ReadByte();
                if (b != id[i])
                {
                    return hdErr.HDERR_INVALID_FILE;
                }
            }

            using (BinaryReader br = new BinaryReader(file, Encoding.UTF8, true))
            {
                hardDisk.length = br.ReadUInt32BE();
                hardDisk.version = br.ReadUInt32BE();
                if (HeaderLengths[hardDisk.version] != hardDisk.length)
                {
                    return hdErr.HDERR_INVALID_DATA;
                }
                switch (hardDisk.version)
                {
                    case 1:
                        {
                            hardDisk.flags = br.ReadUInt32BE();
                            hardDisk.compression = br.ReadUInt32BE();
                            UInt32 blocksize = br.ReadUInt32BE();
                            hardDisk.totalblocks = br.ReadUInt32BE(); // total number of CHD Blocks
                            UInt32 cylinders = br.ReadUInt32BE();
                            UInt32 heads = br.ReadUInt32BE();
                            UInt32 sectors = br.ReadUInt32BE();
                            hardDisk.md5 = br.ReadBytes(16);
                            hardDisk.parentmd5 = br.ReadBytes(16);

                            const int HARD_DISK_SECTOR_SIZE = 512;
                            hardDisk.totalbytes = cylinders * heads * sectors * HARD_DISK_SECTOR_SIZE;
                            hardDisk.blocksize = blocksize * HARD_DISK_SECTOR_SIZE;

                            break;
                        }

                    case 2:
                        {
                            hardDisk.flags = br.ReadUInt32BE();
                            hardDisk.compression = br.ReadUInt32BE();
                            UInt32 blocksize = br.ReadUInt32BE();
                            hardDisk.totalblocks = br.ReadUInt32BE();
                            UInt32 cylinders = br.ReadUInt32BE();
                            UInt32 heads = br.ReadUInt32BE();
                            UInt32 sectors = br.ReadUInt32BE();
                            hardDisk.md5 = br.ReadBytes(16);
                            hardDisk.parentmd5 = br.ReadBytes(16);
                            hardDisk.blocksize = br.ReadUInt32BE();

                            const int HARD_DISK_SECTOR_SIZE = 512;
                            hardDisk.totalbytes = cylinders * heads * sectors * HARD_DISK_SECTOR_SIZE;
                            break;
                        }

                    case 3:
                        hardDisk.flags = br.ReadUInt32BE();
                        hardDisk.compression = br.ReadUInt32BE();
                        hardDisk.totalblocks = br.ReadUInt32BE();  // total number of CHD Blocks

                        hardDisk.totalbytes = br.ReadUInt64BE();  // total byte size of the image
                        hardDisk.metaoffset = br.ReadUInt64BE();

                        hardDisk.md5 = br.ReadBytes(16);
                        hardDisk.parentmd5 = br.ReadBytes(16);
                        hardDisk.blocksize = br.ReadUInt32BE();    // length of a CHD Block
                        hardDisk.sha1 = br.ReadBytes(20);
                        hardDisk.parentsha1 = br.ReadBytes(20);
                        break;
                    case 4:
                        hardDisk.flags = br.ReadUInt32BE();
                        hardDisk.compression = br.ReadUInt32BE();
                        hardDisk.totalblocks = br.ReadUInt32BE();  // total number of CHD Blocks

                        hardDisk.totalbytes = br.ReadUInt64BE();  // total byte size of the image
                        hardDisk.metaoffset = br.ReadUInt64BE();

                        hardDisk.blocksize = br.ReadUInt32BE();    // length of a CHD Block
                        hardDisk.sha1 = br.ReadBytes(20);
                        hardDisk.parentsha1 = br.ReadBytes(20);
                        hardDisk.rawsha1 = br.ReadBytes(20);
                        break;

                    case 5:

                        //    V5 header:
                        hardDisk.compressions = new UInt32[4];
                        for (int i = 0; i < 4; i++)
                            hardDisk.compressions[i] = br.ReadUInt32BE();

                        hardDisk.totalbytes = br.ReadUInt64BE();  // total byte size of the image
                        hardDisk.mapoffset = br.ReadUInt64BE();    // offset to the map
                        hardDisk.metaoffset = br.ReadUInt64BE();

                        hardDisk.blocksize = br.ReadUInt32BE();    // length of a CHD Block
                        hardDisk.unitbytes = br.ReadUInt32BE(); // number of bytes per unit within each hunk
                        hardDisk.rawsha1 = br.ReadBytes(20);
                        hardDisk.sha1 = br.ReadBytes(20);
                        hardDisk.parentsha1 = br.ReadBytes(20);

                        return hdErr.HDERR_NONE;

                    default:
                        return hdErr.HDERR_UNSUPPORTED;
                }

                return hdErr.HDERR_NONE;
            }
        }

    }
}