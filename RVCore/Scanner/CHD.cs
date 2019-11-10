/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using RVCore.RvDB;
using RVIO;
using File = RVIO.File;
using FileStream = RVIO.FileStream;
using Path = RVIO.Path;

namespace RVCore.Scanner
{
    public static class CHD
    {
        public enum CHDManCheck
        {
            Unset,
            Good,
            Corrupt,
            CHDNotFound,
            ChdmanNotFound,
            CHDUnknownError,
            CHDReturnError
        }

        private const int MaxHeader = 124;

        private static string _result;
        private static ThreadWorker _thWrk;
        private static CHDManCheck _resultType;

        /*
        chdman - MAME Compressed Hunks of Data (CHD) manager 0.147 (Sep 18 2012)
        Raw SHA1 verification successful!
        Overall SHA1 verification successful!
        */

        private static int _outputLineCount;

        private static int _errorLines;

        public static int CheckFile(RvFile file, string directory)
        {

            string filename = Path.Combine(directory, file.Name);

            string ext = Path.GetExtension(filename).ToLower();
            if (ext != ".chd")
            {
                return 0;
            }


            if (file.Size < MaxHeader)
            {
                return 0;
            }


            Stream s;
            int retval = FileStream.OpenFileRead(filename, out s);
            if (retval != 0)
            {
                return retval;
            }
            if (s == null)
            {
                return 1;
            }

            CheckFile(s, out file.AltSHA1, out file.AltMD5, out file.CHDVersion);


            file.FileStatusSet(
                (file.AltSHA1 != null ? FileStatus.AltSHA1FromHeader : 0) |
                     (file.AltMD5 != null ? FileStatus.AltMD5FromHeader : 0)
                 );


            s.Close();
            s.Dispose();

            return 0;
        }


        private static void CheckFile(Stream s, out byte[] SHA1CHD, out byte[] MD5CHD, out uint? version)
        {
            using (BinaryReader br = new BinaryReader(s))
            {
                byte[] buff = br.ReadBytes(MaxHeader);

                CheckFile(buff, out SHA1CHD, out MD5CHD, out version);
            }
        }

        private static void CheckFile(byte[] buff, out byte[] SHA1CHD, out byte[] MD5CHD, out uint? version)
        {
            SHA1CHD = null;
            MD5CHD = null;
            version = null;
            uint compression = 0;

            byte[] header = {(byte) 'M', (byte) 'C', (byte) 'o', (byte) 'm', (byte) 'p', (byte) 'r', (byte) 'H', (byte) 'D'};

            for (int i = 0; i < header.Length; i++)
            {
                if (buff[i] != header[i])
                {
                    return;
                }
            }

            uint length = ReadUInt32(buff, 8);
            version = ReadUInt32(buff, 12);

            if (version > 5)
            {
                return;
            }

            switch (version)
            {
                case 1:
                    if (length != 76)
                    {
                        return;
                    }

                    //    V1 header:
                    //    [  0] char   tag[8];        // 'MComprHD'
                    //    [  8] UINT32 length;        // length of header (including tag and length fields)
                    //    [ 12] UINT32 version;       // drive format version
                    //    [ 16] UINT32 flags;         // flags (see below)
                    //    [ 20] UINT32 compression;   // compression type
                    compression = ReadUInt32(buff, 20);
                    if (compression == 0)
                    {
                        return;
                    }

                    //    [ 24] UINT32 hunksize;      // 512-byte sectors per hunk
                    //    [ 28] UINT32 totalhunks;    // total # of hunks represented
                    //    [ 32] UINT32 cylinders;     // number of cylinders on hard disk
                    //    [ 36] UINT32 heads;         // number of heads on hard disk
                    //    [ 40] UINT32 sectors;       // number of sectors on hard disk
                    //    [ 44] UINT8  md5[16];       // MD5 checksum of raw data
                    MD5CHD = ReadBytes(buff, 44, 16);
                    //    [ 60] UINT8  parentmd5[16]; // MD5 checksum of parent file
                    //    [ 76] (V1 header length)

                    return;

                case 2:
                    if (length != 80)
                    {
                        return;
                    }

                    //    V2 header:
                    //    [  0] char   tag[8];        // 'MComprHD'
                    //    [  8] UINT32 length;        // length of header (including tag and length fields)
                    //    [ 12] UINT32 version;       // drive format version
                    //    [ 16] UINT32 flags;         // flags (see below)
                    //    [ 20] UINT32 compression;   // compression type
                    compression = ReadUInt32(buff, 20);
                    if (compression == 0)
                    {
                        return;
                    }

                    //    [ 24] UINT32 hunksize;      // seclen-byte sectors per hunk
                    //    [ 28] UINT32 totalhunks;    // total # of hunks represented
                    //    [ 32] UINT32 cylinders;     // number of cylinders on hard disk
                    //    [ 36] UINT32 heads;         // number of heads on hard disk
                    //    [ 40] UINT32 sectors;       // number of sectors on hard disk
                    //    [ 44] UINT8  md5[16];       // MD5 checksum of raw data
                    MD5CHD = ReadBytes(buff, 44, 16);
                    //    [ 60] UINT8  parentmd5[16]; // MD5 checksum of parent file
                    //    [ 76] UINT32 seclen;        // number of bytes per sector
                    //    [ 80] (V2 header length)
                    return;

                case 3:
                    if (length != 120)
                    {
                        return;
                    }

                    //    V3 header:
                    //    [  0] char   tag[8];        // 'MComprHD'
                    //    [  8] UINT32 length;        // length of header (including tag and length fields)
                    //    [ 12] UINT32 version;       // drive format version
                    //    [ 16] UINT32 flags;         // flags (see below)
                    //    [ 20] UINT32 compression;   // compression type
                    compression = ReadUInt32(buff, 20);
                    if (compression == 0)
                    {
                        return;
                    }
                    //    [ 24] UINT32 totalhunks;    // total # of hunks represented
                    //    [ 28] UINT64 logicalbytes;  // logical size of the data (in bytes)
                    //    [ 36] UINT64 metaoffset;    // offset to the first blob of metadata
                    //    [ 44] UINT8  md5[16];       // MD5 checksum of raw data
                    MD5CHD = ReadBytes(buff, 44, 16);
                    //    [ 60] UINT8  parentmd5[16]; // MD5 checksum of parent file
                    //    [ 76] UINT32 hunkbytes;     // number of bytes per hunk
                    //    [ 80] UINT8  sha1[20];      // SHA1 checksum of raw data
                    SHA1CHD = ReadBytes(buff, 80, 20);
                    //    [100] UINT8  parentsha1[20];// SHA1 checksum of parent file
                    //    [120] (V3 header length)
                    return;

                case 4:
                    if (length != 108)
                    {
                        return;
                    }

                    //    V4 header:
                    //    [  0] char   tag[8];        // 'MComprHD'
                    //    [  8] UINT32 length;        // length of header (including tag and length fields)
                    //    [ 12] UINT32 version;       // drive format version
                    //    [ 16] UINT32 flags;         // flags (see below)
                    //    [ 20] UINT32 compression;   // compression type
                    compression = ReadUInt32(buff, 20);
                    if (compression == 0)
                    {
                        return;
                    }
                    //    [ 24] UINT32 totalhunks;    // total # of hunks represented
                    //    [ 28] UINT64 logicalbytes;  // logical size of the data (in bytes)
                    //    [ 36] UINT64 metaoffset;    // offset to the first blob of metadata
                    //    [ 44] UINT32 hunkbytes;     // number of bytes per hunk
                    //    [ 48] UINT8  sha1[20];      // combined raw+meta SHA1
                    SHA1CHD = ReadBytes(buff, 48, 20);
                    //    [ 68] UINT8  parentsha1[20];// combined raw+meta SHA1 of parent
                    //    [ 88] UINT8  rawsha1[20];   // raw data SHA1
                    //    [108] (V4 header length)
                    return;

                case 5:
                    if (length != 124)
                    {
                        return;
                    }

                    //    V5 header:
                    //    [  0] char   tag[8];        // 'MComprHD'
                    //    [  8] UINT32 length;        // length of header (including tag and length fields)
                    //    [ 12] UINT32 version;       // drive format version
                    //    [ 16] UINT32 compressors[4];// which custom compressors are used?
                    uint compression0 = ReadUInt32(buff, 16);
                    uint compression1 = ReadUInt32(buff, 20);
                    uint compression2 = ReadUInt32(buff, 24);
                    uint compression3 = ReadUInt32(buff, 28);
                    if (compression0 == 0 && compression1 == 0 && compression2 == 0 && compression3 == 0)
                    {
                        return;
                    }
                    //    [ 32] UINT64 logicalbytes;  // logical size of the data (in bytes)
                    //    [ 40] UINT64 mapoffset;     // offset to the map
                    //    [ 48] UINT64 metaoffset;    // offset to the first blob of metadata
                    //    [ 56] UINT32 hunkbytes;     // number of bytes per hunk (512k maximum)
                    //    [ 60] UINT32 unitbytes;     // number of bytes per unit within each hunk
                    //    [ 64] UINT8  rawsha1[20];   // raw data SHA1
                    //    [ 84] UINT8  sha1[20];      // combined raw+meta SHA1
                    SHA1CHD = ReadBytes(buff, 84, 20);
                    //    [104] UINT8  parentsha1[20];// combined raw+meta SHA1 of parent
                    //    [124] (V5 header length)
                    return;
            }
        }


        private static uint ReadUInt32(byte[] b, int p)
        {
            return
                ((uint) b[p + 0] << 24) |
                ((uint) b[p + 1] << 16) |
                ((uint) b[p + 2] << 8) |
                b[p + 3];
        }

        private static byte[] ReadBytes(byte[] b, int p, int length)
        {
            byte[] ret = new byte[length];
            for (int i = 0; i < length; i++)
            {
                ret[i] = b[p + i];
            }
            return ret;
        }


        public static CHDManCheck ChdmanCheck(string filename, ThreadWorker thWrk, out string result)
        {
            _thWrk = thWrk;
            _result = "";
            _resultType = CHDManCheck.Unset;

            string chdExe = "chdman.exe";
            if (Settings.IsUnix)
            {
                chdExe = "chdman";
            }

            string chdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, chdExe);
            if (!File.Exists(chdPath))
            {
                result = chdExe + " Not Found.";
                return CHDManCheck.ChdmanNotFound;
            }

            if (!File.Exists(filename))
            {
                result = filename + " Not Found.";
                return CHDManCheck.CHDNotFound;
            }

            string shortName = NameFix.GetShortPath(filename);


            using (Process exeProcess = new Process())
            {
                exeProcess.StartInfo.FileName = chdPath;
                ReportError.LogOut("CHD: FileName :" + exeProcess.StartInfo.FileName);


                exeProcess.StartInfo.Arguments = "verify -i \"" + shortName + "\"";
                ReportError.LogOut("CHD: Arguments :" + exeProcess.StartInfo.Arguments);

                // Set UseShellExecute to false for redirection.
                exeProcess.StartInfo.UseShellExecute = false;
                // Stops the Command window from popping up.
                exeProcess.StartInfo.CreateNoWindow = true;

                // Redirect the standard output.
                // This stream is read asynchronously using an event handler.
                exeProcess.StartInfo.RedirectStandardOutput = true;
                exeProcess.StartInfo.RedirectStandardError = true;

                // Set our event handler to asynchronously read the process output.
                exeProcess.OutputDataReceived += CHDOutputHandler;
                exeProcess.ErrorDataReceived += CHDErrorHandler;

                _outputLineCount = 0;
                _errorLines = 0;

                ReportError.LogOut("CHD: Scanning Starting");
                exeProcess.Start();

                // Start the asynchronous read of the process output stream.
                exeProcess.BeginOutputReadLine();
                exeProcess.BeginErrorReadLine();

                // Wait for the process finish.
                exeProcess.WaitForExit();
                ReportError.LogOut("CHD: Scanning Finished");
            }

            result = _result;

            if (_resultType == CHDManCheck.Unset)
            {
                _resultType = CHDManCheck.Good;
            }

            _thWrk.Report(new bgwText3(""));

            ReportError.LogOut("CHD: returning result " + _resultType + " " + result);

            return _resultType;
        }

        private static void CHDOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the process command output.
            if (string.IsNullOrEmpty(outLine.Data))
            {
                return;
            }

            string sOut = outLine.Data;
            //ReportError.LogOut("CHDOutput: " + _outputLineCount + " : " + sOut);
            switch (_outputLineCount)
            {
                case 0:
                    if (!Regex.IsMatch(sOut, @"^chdman - MAME Compressed Hunks of Data \(CHD\) manager ([0-9\.]+) \(.*\)"))
                    {
                        _result = "Incorrect startup of CHDMan :" + sOut;
                        _resultType = CHDManCheck.CHDReturnError;
                    }
                    break;
                case 1:
                    if (sOut != "Raw SHA1 verification successful!")
                    {
                        _result = "Raw SHA1 check failed :" + sOut;
                        _resultType = CHDManCheck.CHDReturnError;
                    }
                    break;
                case 2:
                    if (sOut != "Overall SHA1 verification successful!")
                    {
                        _result = "Overall SHA1 check failed :" + sOut;
                        _resultType = CHDManCheck.CHDReturnError;
                    }
                    break;
                default:
                    _result = "Unexpected output from chdman :" + sOut;
                    _resultType = CHDManCheck.CHDUnknownError;
                    break;
            }

            _outputLineCount++;
        }

        private static void CHDErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the process command output.
            if (string.IsNullOrEmpty(outLine.Data))
            {
                return;
            }

            // We can get fed multiple lines worth of data because of \r line feeds
            string[] sLines = outLine.Data.Split(new[] {"\r"}, StringSplitOptions.None);

            foreach (string sLine in sLines)
            {
                if (string.IsNullOrEmpty(sLine))
                {
                    continue;
                }

                ReportError.LogOut("CHDError: " + sLine);
                _thWrk.Report(new bgwText3(sLine));

                if (_resultType != CHDManCheck.Unset)
                {
                    if (_errorLines > 0)
                    {
                        _errorLines -= 1;
                        _result += "\r\n" + sLine;
                    }
                }
                else if (Regex.IsMatch(sLine, @"^No verification to be done; CHD has (uncompressed|no checksum)"))
                {
                    _result = sLine;
                    _resultType = CHDManCheck.Corrupt;
                }
                else if (Regex.IsMatch(sLine, @"^Error (opening|reading) CHD file.*"))
                {
                    _result = sLine;
                    _resultType = CHDManCheck.Corrupt;
                }
                else if (Regex.IsMatch(sLine, @"^Error opening parent CHD file .*:"))
                {
                    _result = sLine;
                    _resultType = CHDManCheck.Corrupt;
                }
                else if (Regex.IsMatch(sLine, @"^Error: (Raw|Overall) SHA1 in header"))
                {
                    _result = sLine;
                    _resultType = CHDManCheck.Corrupt;
                }
                else if (Regex.IsMatch(sLine, @"^Out of memory"))
                {
                    _result = sLine;
                    _resultType = CHDManCheck.Corrupt;
                }
                // Verifying messages are a non-error
                else if (Regex.IsMatch(sLine, @"Verifying, \d+\.\d+\% complete\.\.\."))
                {
                }
                else
                {
                    _result = "Unknown message : " + sLine;
                    _resultType = CHDManCheck.CHDUnknownError;
                }
            }
        }
    }
}