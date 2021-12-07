using System;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.Support.Compression.Deflate;
using Compress.Support.Compression.LZMA;
using Compress.ThreadReaders;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using FileStream = RVIO.FileStream;
using Path = RVIO.Path;
using FileInfo = RVIO.FileInfo;
using Directory = RVIO.Directory;

namespace RomVaultCore.FixFile.Util
{
    public static class Decompress7ZipFile
    {
        private const int BufferSize = 128 * 4096;

        public static ReturnCode DecompressSource7ZipFile(RvFile db7zFile, bool includeGood, out string error)
        {
            // this just checks for one file, should be changed to chek for one got file.
            if (db7zFile.ChildCount == 1)
            {
                error = "Single File";
                return ReturnCode.Good;
            }
            byte[] buffer = new byte[BufferSize];

            RvFile cacheDir = DB.RvFileCache();
            
            SevenZ sevenZipFileCaching = new SevenZ();
            ZipReturn zr1 = sevenZipFileCaching.ZipFileOpen(db7zFile.FullNameCase, db7zFile.FileModTimeStamp, true);
            if (zr1 != ZipReturn.ZipGood)
            {
                error = "Error opening 7zip file for caching";
                return ReturnCode.RescanNeeded;
            }

            RvFile outDir = new RvFile(FileType.Dir)
            {
                Name = db7zFile.Name + ".cache",
                Parent = cacheDir,
                DatStatus = DatStatus.InToSort,
                GotStatus = GotStatus.Got
            };

            int nameDirIndex = 0;
            while (cacheDir.ChildNameSearch(outDir, out int index) == 0)
            {
                nameDirIndex++;
                outDir.Name = db7zFile.Name + ".cache (" + nameDirIndex + ")";
            }
            cacheDir.ChildAdd(outDir);
            Directory.CreateDirectory(outDir.FullName);

            for (int i = 0; i < sevenZipFileCaching.LocalFilesCount(); i++)
            {
                if (sevenZipFileCaching.GetLocalFile(i).IsDirectory)
                    continue;
                RvFile thisFile = null;
                for (int j = 0; j < db7zFile.ChildCount; j++)
                {
                    if (db7zFile.Child(j).ZipFileIndex != i)
                        continue;
                    thisFile = db7zFile.Child(j);
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
                    if (thisFile.RepStatus == RepStatus.Correct || thisFile.RepStatus==RepStatus.InToSort || thisFile.RepStatus==RepStatus.MoveToSort)
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


                if (cleanedName.Length >= 248)
                {
                    string mainName = Path.GetFileNameWithoutExtension(cleanedName);
                    string extName = Path.GetExtension(cleanedName);

                    mainName = mainName.Substring(0,248 - extName.Length);
                    cleanedName = mainName + extName;
                }

                RvFile outFile = new RvFile(FileType.File)
                {
                    Name = cleanedName,
                    Size = thisFile.Size,
                    CRC = thisFile.CRC,
                    SHA1 = thisFile.SHA1,
                    MD5 = thisFile.MD5,
                    GotStatus=GotStatus.Got,
                    HeaderFileType = thisFile.HeaderFileType,
                    AltSize = thisFile.AltSize,
                    AltCRC = thisFile.AltCRC,
                    AltSHA1 = thisFile.AltSHA1,
                    AltMD5 = thisFile.AltMD5,
                    FileGroup = thisFile.FileGroup
                };

                int tryname = 0;
                while (outDir.ChildNameSearch(outFile, out int index)==0)
                {
                    tryname += 1;
                    string mainName = Path.GetFileNameWithoutExtension(cleanedName);
                    string extName = Path.GetExtension(cleanedName);
                    cleanedName = mainName + $"_{tryname}" + extName;
                    outFile.Name = cleanedName;
                }


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

                sevenZipFileCaching.ZipFileOpenReadStream(i, out Stream readStream, out ulong unCompressedSize);

                string filenameOut = Path.Combine(outDir.FullName, outFile.Name);

                if (Settings.rvSettings.DetailedFixReporting)
                {
                    string fixZipFullName = db7zFile.TreeFullName;
                    Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), thisFile.Name, thisFile.Size, "-->", outDir.FullName, "", outFile.Name));
                }

                ThreadMD5 tmd5 = null;
                ThreadSHA1 tsha1 = null;

                ThreadCRC tcrc32 = new ThreadCRC();
                if (Settings.rvSettings.FixLevel != EFixLevel.Level1)
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
                            ZipReturn zr = sevenZipFileCaching.ZipFileCloseReadStream();
                            if (zr != ZipReturn.ZipGood)
                            {
                                error = "Error Closing " + zr + " Stream :" + sevenZipFileCaching.ZipFilename;
                                return ReturnCode.FileSystemError;
                            }

                            sevenZipFileCaching.ZipFileClose();
                            writeStream.Flush();
                            writeStream.Close();
                            if (filenameOut != null)
                            {
                                File.Delete(filenameOut);
                            }

                            thisFile.GotStatus = GotStatus.Corrupt;
                            error = "Unexpected corrupt archive file found:\n" + db7zFile.FullName +
                                    "\nRun Find Fixes, and Fix to continue fixing correctly.";
                            return ReturnCode.SourceDataStreamCorrupt;
                        }

                        error = "Error reading Source File " + ex.Message;
                        return ReturnCode.FileSystemError;
                    }

                    tcrc32.Trigger(buffer, sizenow);
                    tmd5?.Trigger(buffer, sizenow);
                    tsha1?.Trigger(buffer, sizenow);
                    try
                    {
                        writeStream.Write(buffer, 0, sizenow);
                    }
                    catch (Exception e)
                    {
                        error = "Error writing out file. " + Environment.NewLine + e.Message;
                        return ReturnCode.FileSystemError;
                    }
                    tcrc32.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
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
                outFile.FileModTimeStamp = fi.LastWriteTime;

                if (bCRC != null && thisFile.CRC != null && !ArrByte.BCompare(bCRC, thisFile.CRC))
                {
                    // error in file.
                    error = "Error found in cache extract CRC";
                    return ReturnCode.SourceCheckSumMismatch;
                }
                if (bMD5 != null && thisFile.MD5 != null && !ArrByte.BCompare(bMD5, thisFile.MD5))
                {
                    // error in file.
                    error = "Error found in cache extract MD5";
                    return ReturnCode.SourceCheckSumMismatch;
                }
                if (bSHA1 != null && thisFile.SHA1 != null && !ArrByte.BCompare(bSHA1, thisFile.SHA1))
                {
                    // error in file.
                    error = "Error found in cache extract SHA1";
                    return ReturnCode.SourceCheckSumMismatch;
                }

                thisFile.FileGroup.Files.Add(outFile);

                outDir.ChildAdd(outFile);
            }

            sevenZipFileCaching.ZipFileClose();

            error = "";
            return ReturnCode.Good;
        }

    }
}
