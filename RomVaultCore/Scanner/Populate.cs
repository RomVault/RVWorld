/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CHDSharpLib;
using Compress;
using FileScanner;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using DirectoryInfo = RVIO.DirectoryInfo;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using Path = RVIO.Path;

namespace RomVaultCore.Scanner
{
    public static class Populate
    {

        private static ThreadWorker _thWrk;
        private static FileScan _fileScans;
        public static ScannedFile FromAZipFileArchive(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker thWrk)
        {
            if (_fileScans == null) _fileScans = new FileScan();
            _thWrk = thWrk;

            string filename = dbDir.FullNameCase;
            FileType sType = dbDir.FileType;
            ZipReturn zr = _fileScans.ScanArchiveFile(sType, filename, dbDir.FileModTimeStamp, eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3, out ScannedFile ar, progress: FileProgress);

            if (zr == ZipReturn.ZipGood)
            {
                dbDir.ZipStruct = ar.ZipStruct;
                return ar;
            }
            else if (zr == ZipReturn.ZipFileLocked)
            {
                thWrk.Report(new bgwShowError(filename, "Zip File Locked"));
                dbDir.FileModTimeStamp = long.MinValue;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else if (zr == ZipReturn.ZipErrorOpeningFile)
            {
                thWrk.Report(new bgwShowError(filename, "Zip Error Opening File"));
                dbDir.FileModTimeStamp = long.MinValue;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else if (zr == ZipReturn.ZipErrorTimeStamp)
            {
                thWrk.Report(new bgwShowError(filename, "Zip Error File Modified"));
                dbDir.FileModTimeStamp = long.MinValue;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else
            {
                thWrk.Report(new bgwShowError(filename, CompressUtils.ZipErrorMessageText(zr)));
                dbDir.GotStatus = GotStatus.Corrupt;
            }
            return null;
        }


        public static ScannedFile FromADir(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker thWrk, int? fileIndex, ref bool fileErrorAbort)
        {
            _thWrk = thWrk;
            string fullName = dbDir.FullName;
            string fullNameCase = dbDir.FullNameCase;
            // DatStatus datStatus = dbDir.IsInToSort ? DatStatus.InToSort : DatStatus.NotInDat;

            ScannedFile fileDir = new ScannedFile(FileType.Dir);

            thWrk.Report(new bgwText("Scanning Dir : " + fullName));

            DirectoryInfo oDir = new DirectoryInfo(fullNameCase);
            DirectoryInfo[] oDirs = oDir.GetDirectories();
            FileInfo[] oFiles = oDir.GetFiles();

            // add all the subdirectories into scanDir 
            foreach (DirectoryInfo dir in oDirs)
            {
                ScannedFile tDir = new ScannedFile(FileType.Dir)
                {
                    Name = dir.Name,
                    FileModTimeStamp = dir.LastWriteTime,
                    GotStatus = GotStatus.Got
                };
                fileDir.Add(tDir);
            }


            DatRule datRule = ReadDat.DatReader.FindDatRule(dbDir.DatTreeFullName + "\\");
            List<Regex> regexList = (datRule != null && datRule.IgnoreFilesRegex != null && datRule.IgnoreFilesScanRegex.Count > 0)
                ? datRule.IgnoreFilesScanRegex
                : Settings.rvSettings.IgnoreFilesScanRegex;

            bool isFileOnly = IsFileOnly.isFileOnly(dbDir);

            // add all the files into scanDir
            foreach (FileInfo oFile in oFiles)
            {
                string fName = oFile.Name;
                if (fName.StartsWith("__RomVault.") && fName.EndsWith(".tmp"))
                {
                    try
                    {
                        File.Delete(oFile.FullName);
                    }
                    catch
                    {
                        thWrk.Report(new bgwShowError(oFile.FullName, "Could not delete, un-needed tmp file."));
                    }
                    continue;
                }

                bool found = false;
                foreach (Regex file in regexList)
                {
                    if (file.IsMatch(fName))
                    {
                        found = true;
                        continue;
                    }
                }
                if (found)
                    continue;

                string fExt = Path.GetExtension(oFile.Name);

                FileType ft = DBTypeGet.fromExtention(fExt);

                if (Settings.rvSettings.FilesOnly || dbDir.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly) || isFileOnly)
                    ft = FileType.File;

                ScannedFile tFile = new ScannedFile(ft)
                {
                    Name = oFile.Name,
                    Size = (ulong)oFile.Length,
                    FileModTimeStamp = oFile.LastWriteTime,
                    GotStatus = GotStatus.Got
                };
                tFile.FileStatusSet(FileStatus.SizeVerified);

                if (eScanLevel == EScanLevel.Level3 && tFile.FileType == FileType.File)
                {
                    if (fileIndex != null)
                        thWrk.Report(new bgwValue2((int)fileIndex));
                    thWrk.Report(new bgwText2(tFile.Name));
                    FromAFile(tFile, fullNameCase, eScanLevel, thWrk, ref fileErrorAbort);
                }

                fileDir.Add(tFile);
            }
            return fileDir;
        }
        public static void FromAFile(ScannedFile file, string directory, EScanLevel eScanLevel, ThreadWorker thWrk, ref bool fileErrorAbort)
        {
            if (_fileScans == null) _fileScans = new FileScan();

            _thWrk = thWrk;
            string filename = Path.Combine(directory, file.Name);

            thWrk.Report(new bgwText2(file.Name));
            ZipReturn zr = _fileScans.ScanArchiveFile(FileType.Dir, filename, file.FileModTimeStamp, true, out ScannedFile scannedItem, progress: FileProgress);

            if (zr == ZipReturn.ZipFileLocked)
            {
                thWrk.Report(new bgwShowError(filename, "File Locked"));
                file.GotStatus = GotStatus.FileLocked;
                return;
            }
            if (zr == ZipReturn.ZipErrorOpeningFile)
            {
                thWrk.Report(new bgwShowError(filename, "Error Opening File"));
                file.GotStatus = GotStatus.FileLocked;
                return;
            }

            if (zr != ZipReturn.ZipGood)
            {
                string error = zr.ToString();
                if (error.ToLower().StartsWith("zip"))
                    error = error.Substring(3);

                ReportError.Show($"File: {filename} Error: {error}. Scan Aborted.");
                file.GotStatus = GotStatus.FileLocked;
                fileErrorAbort = true;
                return;
            }

            if (_fileScans == null)
                _fileScans = new FileScan();

            //report

            ScannedFile fr = scannedItem[0];
            if (fr.GotStatus != GotStatus.Got)
            {
                thWrk.Report(new bgwShowError(filename, "Error Scanning File"));
                file.GotStatus = fr.GotStatus;
                return;
            }

            file.HeaderFileType = fr.HeaderFileType;
            file.Size = fr.Size;
            file.CRC = fr.CRC;
            file.SHA1 = fr.SHA1;
            file.MD5 = fr.MD5;
            file.AltSize = fr.AltSize;
            file.AltCRC = fr.AltCRC;
            file.AltSHA1 = fr.AltSHA1;
            file.AltMD5 = fr.AltMD5;
            file.GotStatus = fr.GotStatus;


            file.FileStatusSet(
                FileStatus.SizeVerified |
                (file.HeaderFileType != HeaderFileType.Nothing ? FileStatus.HeaderFileTypeFromHeader : 0) |
                (file.CRC != null ? FileStatus.CRCVerified : 0) |
                (file.SHA1 != null ? FileStatus.SHA1Verified : 0) |
                (file.MD5 != null ? FileStatus.MD5Verified : 0) |
                (file.AltSize != null ? FileStatus.AltSizeVerified : 0) |
                (file.AltCRC != null ? FileStatus.AltCRCVerified : 0) |
                (file.AltSHA1 != null ? FileStatus.AltSHA1Verified : 0) |
                (file.AltMD5 != null ? FileStatus.AltMD5Verified : 0)
            );

            if (fr.HeaderFileType == HeaderFileType.CHD)
            {
                bool deepCheck = (eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3);
                uint? chdVersion = null;
                byte[] chdSHA1 = null;
                byte[] chdMD5 = null;

                CHD.fileProcessInfo = FileProcess;
                CHD.progress = FileProgress;

                int taskCount = Environment.ProcessorCount - 1;
                if (taskCount < 2) taskCount = 2;
                if (taskCount > 8) taskCount = 8;
                CHD.taskCount = taskCount;


                chd_error result = chd_error.CHDERR_NONE;
                if (!File.Exists(filename))
                {
                    FileSystemError("File: " + filename + " Error: File Could not be opened.");
                    fileErrorAbort = true;
                    result = chd_error.CHDERR_CANNOT_OPEN_FILE;
                }

                Stream s = null;
                int retval = RVIO.FileStream.OpenFileRead(filename, RVIO.FileStream.BufSizeMax, out s);
                if (retval != 0)
                {
                    FileSystemError("File: " + filename + " Error: File Could not be opened.");
                    fileErrorAbort = true;
                    result = chd_error.CHDERR_CANNOT_OPEN_FILE;
                }
                if (s == null)
                {
                    FileSystemError("File: " + filename + " Error: File Could not be opened.");
                    fileErrorAbort = true;
                    result = chd_error.CHDERR_CANNOT_OPEN_FILE;
                }

                if (result == chd_error.CHDERR_NONE)
                {
                    result = CHD.CheckFile(s, filename, deepCheck, out chdVersion, out chdSHA1, out chdMD5);
                }
                try
                {
                    s?.Close();
                    s?.Dispose();
                }
                catch
                { }

                if (result == chd_error.CHDERR_REQUIRES_PARENT)
                {
                    deepCheck = false;
                    result = chd_error.CHDERR_NONE;
                }
                switch (result)
                {
                    case chd_error.CHDERR_NONE:
                        break;

                    case chd_error.CHDERR_INVALID_FILE:
                    case chd_error.CHDERR_INVALID_DATA:
                    case chd_error.CHDERR_READ_ERROR:
                    case chd_error.CHDERR_DECOMPRESSION_ERROR:
                    case chd_error.CHDERR_CANT_VERIFY:
                        thWrk.Report(new bgwShowError(filename, $"CHD ERROR : {result}"));
                        file.GotStatus = GotStatus.Corrupt;
                        break;
                    default:
                        ReportError.UnhandledExceptionHandler(result.ToString());
                        break;
                }
                file.CHDVersion = chdVersion;
                if (chdSHA1 != null)
                {
                    file.AltSHA1 = chdSHA1;
                    file.FileStatusSet(FileStatus.AltSHA1FromHeader);
                    if (deepCheck && result == chd_error.CHDERR_NONE)
                        file.FileStatusSet(FileStatus.AltSHA1Verified);
                }

                if (chdMD5 != null)
                {
                    file.AltMD5 = chdMD5;
                    file.FileStatusSet(FileStatus.AltMD5FromHeader);
                    if (deepCheck && result == chd_error.CHDERR_NONE)
                        file.FileStatusSet(FileStatus.AltMD5Verified);
                }

                thWrk.Report(new bgwText3(""));
            }
        }

        private static void FileProcess(string filename)
        {
            _thWrk?.Report(new bgwText2(filename));
        }
        private static void FileProgress(string status)
        {
            _thWrk?.Report(new bgwText3(status));
        }
        private static void FileSystemError(string status)
        {
            ReportError.Show(status);
        }
    }
}
