using System.Collections.Generic;
using Compress;
using Compress.SevenZip;
using Compress.ZipFile;
using FileHeaderReader;
using RVCore.RvDB;
using RVCore.Utils;
using DirectoryInfo = RVIO.DirectoryInfo;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using Path = RVIO.Path;

namespace RVCore.Scanner
{
    public static class Populate
    {
        private static FileScan _fs;

        public static RvFile FromAZipFile(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker thWrk)
        {
            RvFile fileDir = new RvFile(dbDir.FileType);
            DatStatus chechingDatStatus = dbDir.IsInToSort ? DatStatus.InToSort : DatStatus.NotInDat;

            string filename = dbDir.FullName;
            ICompress checkZ = dbDir.FileType == FileType.Zip ? new Zip() : (ICompress)new SevenZ();
            ZipReturn zr = checkZ.ZipFileOpen(filename, dbDir.FileModTimeStamp);

            if (zr == ZipReturn.ZipGood)
            {
                dbDir.ZipStatus = checkZ.ZipStatus;

                // to be Scanning a ZIP file means it is either new or has changed.
                // as the code below only calls back here if that is true.
                //
                // Level1: Only use header CRC's
                // Just get the CRC for the ZIP headers.
                //
                // Level2: Fully checksum changed only files
                // We know this file has been changed to do a full checksum scan.
                //
                // Level3: Fully checksum everything
                // So do a full checksum scan.
                if (_fs == null) _fs = new FileScan();
                List<FileScan.FileResults> fr = _fs.Scan(checkZ, false, eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3);

                // add all of the file information from the zip file into scanDir
                for (int i = 0; i < checkZ.LocalFilesCount(); i++)
                {
                    RvFile tFile = new RvFile(DBTypeGet.FileFromDir(dbDir.FileType))
                    {
                        Name = checkZ.Filename(i),
                        ZipFileIndex = i,
                        ZipFileHeaderPosition = checkZ.LocalHeader(i),
                        Size = checkZ.UncompressedSize(i),
                        CRC = checkZ.CRC32(i),
                        FileModTimeStamp = checkZ.LastModified(i)
                    };
                    // all levels read the CRC from the ZIP header
                    tFile.SetStatus(chechingDatStatus, GotStatus.Got);
                    tFile.FileStatusSet(FileStatus.SizeFromHeader | FileStatus.CRCFromHeader);

                    if (fr[i].FileStatus != ZipReturn.ZipGood)
                    {
                        thWrk.Report(new bgwShowCorrupt(fr[i].FileStatus, filename + " : " + checkZ.Filename(i)));
                        tFile.GotStatus = GotStatus.Corrupt;
                    }
                    else
                    {
                        tFile.HeaderFileType = fr[i].HeaderFileType;
                        tFile.SHA1 = fr[i].SHA1;
                        tFile.MD5 = fr[i].MD5;
                        tFile.AltSize = fr[i].AltSize;
                        tFile.AltCRC = fr[i].AltCRC;
                        tFile.AltSHA1 = fr[i].AltSHA1;
                        tFile.AltMD5 = fr[i].AltMD5;

                        tFile.FileStatusSet(
                            FileStatus.SizeVerified |
                            (fr[i].HeaderFileType != HeaderFileType.Nothing ? FileStatus.HeaderFileTypeFromHeader : 0) |
                            (fr[i].CRC != null ? FileStatus.CRCVerified : 0) |
                            (fr[i].SHA1 != null ? FileStatus.SHA1Verified : 0) |
                            (fr[i].MD5 != null ? FileStatus.MD5Verified : 0) |
                            (fr[i].AltSize != null ? FileStatus.AltSizeVerified : 0) |
                            (fr[i].AltCRC != null ? FileStatus.AltCRCVerified : 0) |
                            (fr[i].AltSHA1 != null ? FileStatus.AltSHA1Verified : 0) |
                            (fr[i].AltMD5 != null ? FileStatus.AltMD5Verified : 0)
                                         );
                    }

                    fileDir.ChildAdd(tFile);
                }
            }
            else if (zr == ZipReturn.ZipFileLocked)
            {
                thWrk.Report(new bgwShowError(filename, "Zip File Locked"));
                dbDir.FileModTimeStamp = 0;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else
            {
                thWrk.Report(new bgwShowCorrupt(zr, filename));
                dbDir.GotStatus = GotStatus.Corrupt;
            }
            checkZ.ZipFileClose();

            return fileDir;
        }

        public static RvFile FromADir(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker bgw, ref bool fileErrorAbort)
        {
            string fullDir = dbDir.FullName;
            DatStatus datStatus = dbDir.IsInToSort ? DatStatus.InToSort : DatStatus.NotInDat;

            RvFile fileDir = new RvFile(FileType.Dir);


            DirectoryInfo oDir = new DirectoryInfo(fullDir);
            DirectoryInfo[] oDirs = oDir.GetDirectories();
            FileInfo[] oFiles = oDir.GetFiles();

            // add all the subdirectories into scanDir 
            foreach (DirectoryInfo dir in oDirs)
            {
                RvFile tDir = new RvFile(FileType.Dir)
                {
                    Name = dir.Name,
                    FileModTimeStamp = dir.LastWriteTime
                };
                tDir.SetStatus(datStatus, GotStatus.Got);
                fileDir.ChildAdd(tDir);
            }

            // add all the files into scanDir
            foreach (FileInfo oFile in oFiles)
            {
                string fName = oFile.Name;
                if (fName == "__RomVault.tmp")
                {
                    File.Delete(oFile.FullName);
                    continue;
                }
                string fExt = Path.GetExtension(oFile.Name);

                FileType ft = DBTypeGet.fromExtention(fExt);

                if (Settings.rvSettings.FilesOnly)
                    ft = FileType.File;

                RvFile tFile = new RvFile(ft)
                {
                    Name = oFile.Name,
                    Size = (ulong)oFile.Length,
                    FileModTimeStamp = oFile.LastWriteTime
                };
                tFile.FileStatusSet(FileStatus.SizeVerified);
                tFile.SetStatus(datStatus, GotStatus.Got);

                if (eScanLevel == EScanLevel.Level3 && tFile.FileType == FileType.File)
                {
                    FromAFile(tFile, fullDir, eScanLevel, bgw, ref fileErrorAbort);
                }

                fileDir.ChildAdd(tFile);
            }
            return fileDir;
        }

        public static void FromAFile(RvFile file, string directory, EScanLevel eScanLevel, ThreadWorker bgw, ref bool fileErrorAbort)
        {
            string filename = Path.Combine(directory, file.Name);
            ICompress fileToScan = new Compress.File.File();
            ZipReturn zr = fileToScan.ZipFileOpen(filename, file.FileModTimeStamp);

            if (zr == ZipReturn.ZipFileLocked)
            {
                file.GotStatus = GotStatus.FileLocked;
                return;
            }

            if (zr != ZipReturn.ZipGood)
            {
                ReportError.Show("File: " + filename + " Error: " + zr + ". Scan Aborted.");
                file.GotStatus = GotStatus.FileLocked;
                fileErrorAbort = true;
                return;
            }

            if (_fs == null) _fs = new FileScan();
            List<FileScan.FileResults> fr = _fs.Scan(fileToScan, true, eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3);

            file.HeaderFileType = fr[0].HeaderFileType;
            file.Size = fr[0].Size;
            file.CRC = fr[0].CRC;
            file.SHA1 = fr[0].SHA1;
            file.MD5 = fr[0].MD5;
            file.AltSize = fr[0].AltSize;
            file.AltCRC = fr[0].AltCRC;
            file.AltSHA1 = fr[0].AltSHA1;
            file.AltMD5 = fr[0].AltMD5;


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

            if (fr[0].HeaderFileType == HeaderFileType.CHD)
            {
                CHD.CheckFile(file, directory);
                if ((eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3) && file.CHDVersion > 1)
                    Utils.ChdManCheck(file, directory, bgw, ref fileErrorAbort);
            }
            fileToScan.ZipFileClose();

        }
    }
}
