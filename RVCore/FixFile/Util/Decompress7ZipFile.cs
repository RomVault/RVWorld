using System;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.SevenZip.Common;
using Compress.ThreadReaders;
using Compress.ZipFile.ZLib;
using RVCore.RvDB;
using RVCore.Utils;
using FileStream = RVIO.FileStream;
using Path = RVIO.Path;
using FileInfo = RVIO.FileInfo;
using Directory = RVIO.Directory;

namespace RVCore.FixFile.Util
{
    public static class Decompress7ZipFile
    {
        private const int BufferSize = 128 * 4096;

        public static ReturnCode DecompressSource7ZipFile(RvFile zZipFileIn, bool includeGood, out string error)
        {
            byte[] buffer = new byte[BufferSize];

            RvFile cacheDir = DB.RvFileCache();

            string fileNameIn = zZipFileIn.FullName;

            SevenZ zipFileIn = new SevenZ();
            ZipReturn zr1 = zipFileIn.ZipFileOpen(fileNameIn, zZipFileIn.TimeStamp, true);
            if (zr1 != ZipReturn.ZipGood)
            {
                error = "Error opening 7zip file for caching";
                return ReturnCode.RescanNeeded;
            }

            RvFile outDir = new RvFile(FileType.Dir)
            {
                Name = zZipFileIn.Name + ".cache",
                Parent = cacheDir,
                DatStatus = DatStatus.InToSort,
                GotStatus = GotStatus.Got
            };

            int nameDirIndex = 0;
            while (cacheDir.ChildNameSearch(outDir, out int index) == 0)
            {
                nameDirIndex++;
                outDir.Name = zZipFileIn.Name + ".cache (" + nameDirIndex + ")";
            }
            cacheDir.ChildAdd(outDir);
            Directory.CreateDirectory(outDir.FullName);

            for (int i = 0; i < zipFileIn.LocalFilesCount(); i++)
            {
                if (zZipFileIn.Child(i).IsDir)
                    continue;
                RvFile thisFile = null;
                for (int j = 0; j < zZipFileIn.ChildCount; j++)
                {
                    if (zZipFileIn.Child(j).ZipFileIndex != i)
                        continue;
                    thisFile = zZipFileIn.Child(j);
                    break;
                }

                if (thisFile == null)
                {
                    error = "Error opening 7zip file for caching";
                    return ReturnCode.RescanNeeded;
                }

                bool extract = true;

                // first check to see if we have a file  version of this compressed file somewhere else.
                foreach (RvFile f in thisFile.FileGroup.Files)
                {
                    if (f.FileType == FileType.File && f.GotStatus == GotStatus.Got)
                    {
                        extract = false;
                    }
                }
                if (!extract)
                    continue;


                extract = false;
                if (includeGood)
                {
                    // if this is the file we are fixing then pull out the correct files.
                    if (thisFile.RepStatus == RepStatus.Correct)
                        extract = true;
                }

                // next check to see if we need this extracted to fix another file
                foreach (RvFile f in thisFile.FileGroup.Files)
                {
                    if (f.RepStatus == RepStatus.CanBeFixed)
                    {
                        extract = true;
                        break;
                    }
                }

                if (!extract)
                    continue;

                string cleanedName = thisFile.Name;
                cleanedName = cleanedName.Replace("/", "-");
                cleanedName = cleanedName.Replace("\\", "-");

                RvFile outFile = new RvFile(FileType.File)
                {
                    Name = cleanedName,
                    Size = thisFile.Size,
                    CRC = thisFile.CRC,
                    SHA1 = thisFile.SHA1,
                    MD5 = thisFile.MD5,
                    HeaderFileType = thisFile.HeaderFileType,
                    AltSize = thisFile.AltSize,
                    AltCRC = thisFile.AltCRC,
                    AltSHA1 = thisFile.AltSHA1,
                    AltMD5 = thisFile.AltMD5,
                    FileGroup = thisFile.FileGroup
                };

                outFile.SetStatus(DatStatus.InToSort, GotStatus.Got);
                outFile.FileStatusSet(
                    FileStatus.HeaderFileTypeFromHeader |
                    FileStatus.SizeFromHeader | FileStatus.SizeVerified |
                    FileStatus.CRCFromHeader | FileStatus.CRCVerified |
                    FileStatus.SHA1FromHeader | FileStatus.SHA1Verified |
                    FileStatus.MD5FromHeader | FileStatus.MD5Verified |
                    FileStatus.AltSizeFromHeader | FileStatus.AltSizeVerified |
                    FileStatus.AltCRCFromHeader | FileStatus.AltCRCVerified |
                    FileStatus.AltSHA1FromHeader | FileStatus.AltSHA1Verified |
                    FileStatus.AltMD5FromHeader | FileStatus.AltMD5Verified
                    , thisFile);
                outFile.RepStatus = RepStatus.NeededForFix;

                zipFileIn.ZipFileOpenReadStream(i, out Stream readStream, out ulong unCompressedSize);

                string filenameOut = Path.Combine(outDir.FullName, outFile.Name);

                if (Settings.rvSettings.DetailedFixReporting)
                {
                    string fixZipFullName = zZipFileIn.TreeFullName;
                    Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), thisFile.Name, thisFile.Size, "-->", outDir.FullName, "", outFile.Name));
                }

                ThreadMD5 tmd5 = null;
                ThreadSHA1 tsha1 = null;

                ThreadCRC tcrc32 = new ThreadCRC();
                if (Settings.rvSettings.FixLevel != EFixLevel.Level1 && Settings.rvSettings.FixLevel != EFixLevel.TrrntZipLevel1)
                {
                    tmd5 = new ThreadMD5();
                    tsha1 = new ThreadSHA1();
                }

                int errorCode = FileStream.OpenFileWrite(filenameOut, out Stream writeStream);

                ulong sizetogo = unCompressedSize;
                while (sizetogo > 0)
                {
                    int sizenow = sizetogo > BufferSize ? BufferSize : (int)sizetogo;

                    try
                    {
                        readStream.Read(buffer, 0, sizenow);
                    }
                    catch (Exception ex)
                    {
                        if (ex is ZlibException || ex is DataErrorException)
                        {
                            ZipReturn zr = zipFileIn.ZipFileCloseReadStream();
                            if (zr != ZipReturn.ZipGood)
                            {
                                error = "Error Closing " + zr + " Stream :" + zipFileIn.ZipFilename;
                                return ReturnCode.FileSystemError;
                            }

                            zipFileIn.ZipFileClose();
                            writeStream.Flush();
                            writeStream.Close();
                            if (filenameOut != null)
                            {
                                File.Delete(filenameOut);
                            }

                            thisFile.GotStatus = GotStatus.Corrupt;
                            error = "Unexpected corrupt archive file found:\n" + zZipFileIn.FullName +
                                    "\nRun Find Fixes, and Fix to continue fixing correctly.";
                            return ReturnCode.SourceDataStreamCorrupt;
                        }

                        error = "Error reading Source File " + ex.Message;
                        return ReturnCode.FileSystemError;
                    }

                    tcrc32.Trigger(buffer, sizenow);
                    tmd5?.Trigger(buffer, sizenow);
                    tsha1?.Trigger(buffer, sizenow);

                    tcrc32.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();

                    try
                    {
                        writeStream.Write(buffer, 0, sizenow);
                    }
                    catch (Exception e)
                    {
                        error = "Error writing out file. " + Environment.NewLine + e.Message;
                        return ReturnCode.FileSystemError;
                    }
                    sizetogo = sizetogo - (ulong)sizenow;
                }
                writeStream.Flush();
                writeStream.Close();
                writeStream.Dispose();
                
                tcrc32.Finish();
                tmd5?.Finish();
                tsha1?.Finish();

                byte[] bCRC = tcrc32.Hash;
                byte[] bMD5 = tmd5?.Hash;
                byte[] bSHA1 = tsha1?.Hash;

                tcrc32.Dispose();
                tmd5?.Dispose();
                tsha1?.Dispose();

                FileInfo fi = new FileInfo(filenameOut);
                outFile.TimeStamp = fi.LastWriteTime;

                if (bCRC != null && thisFile.CRC != null && !ArrByte.BCompare(bCRC, thisFile.CRC))
                {
                    // error in file.
                }
                if (bMD5 != null && thisFile.MD5 != null && !ArrByte.BCompare(bMD5, thisFile.MD5))
                {
                    // error in file.
                }
                if (bSHA1 != null && thisFile.SHA1 != null && !ArrByte.BCompare(bSHA1, thisFile.SHA1))
                {
                    // error in file.
                }

                thisFile.FileGroup.Files.Add(outFile);

                outDir.ChildAdd(outFile);
            }

            zipFileIn.ZipFileClose();

            error = "";
            return ReturnCode.Good;
        }

    }
}
