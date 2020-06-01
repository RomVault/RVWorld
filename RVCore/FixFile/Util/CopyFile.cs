/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.ComponentModel;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.SevenZip.Common;
using Compress.ThreadReaders;
using Compress.Utils;
using Compress.ZipFile;
using Compress.ZipFile.ZLib;
using RVCore.RvDB;
using RVCore.Utils;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace RVCore.FixFile.Util
{
    public enum ReturnCode
    {
        Good,
        RescanNeeded,
        FindFixes,
        LogicError,
        FileSystemError,
        SourceDataStreamCorrupt,
        SourceCheckSumMismatch,
        DestinationCheckSumMismatch,
        ToSortNotFound
    }


    public static partial class FixFileUtils
    {
        private const int BufferSize = 4096 * 128;
        private static byte[] _buffer;

        // This Function returns:
        // Good            : Everything Worked Correctly
        // RescanNeeded     : Something unexpectedly changed in the files, so Stop fixing and prompt user to rescan.
        // LogicError       : This Should never happen and is a logic problem in the code.
        // FileSystemError  : Something is wrong with the files, like it was locked and could not be opened.
        // SourceDataStreamCorrupt : This happens when either zlib returns ZlibException, or the CRC does not match the extracted zip.
        // SourceCheckSumMismatch  : If the extracted files does not match its expected SHA1 or MD5
        // DestinationCheckSumMismatch : If the extracted files does not match the file to be fixed expected CRC,SHA1 or MD5.


        /// <summary>
        ///     Performs the RomVault File Copy, with the source and destination being files or zipped files
        /// </summary>
        /// <param name="fileIn">This is the file being copied, it may be a zipped file or a regular file system file</param>
        /// <param name="zipFileOut">This is the zip file that is being written to.</param>
        /// <param name="filenameOut">This is the name of the file to be written to if we are just making a file</param>
        /// <param name="fileOut">This is the actual output filename</param>
        /// <param name="forceRaw">if true then we will do a raw copy, this is so that we can copy corrupt zips</param>
        /// <param name="error">This is the returned error message if this copy fails</param>
        /// <returns>ReturnCode.Good is the valid return code otherwise we have an error</returns>
        public static ReturnCode CopyFile(RvFile fileIn, ICompress zipFileOut, string filenameOut, RvFile fileOut, bool forceRaw, out string error)
        {

            if (zipFileOut == null && filenameOut == null)
            {
                throw new Exception("Error in CopyFile: Both Outputs are null");
            }
            if (zipFileOut != null && filenameOut != null)
            {
                throw new Exception("Error in CopyFile: Both Outputs are set");
            }

            ICompress zipFileIn = null;
            Stream readStream = null;
            ThreadCRC tcrc32 = null;
            ThreadMD5 tmd5 = null;
            ThreadSHA1 tsha1 = null;


            ReturnCode retC;
            error = "";

            if (_buffer == null)
            {
                _buffer = new byte[BufferSize];
            }

            bool rawCopy = TestRawCopy(fileIn, fileOut, forceRaw);

            ulong streamSize = 0;
            ushort compressionMethod = 8;
            bool sourceTrrntzip;



            bool isZeroLengthFile = DBHelper.IsZeroLengthFile(fileOut);
            if (!isZeroLengthFile)
            {
                //check that the in and out file match
                retC = CheckInputAndOutputFile(fileIn, fileOut, out error);
                if (retC != ReturnCode.Good)
                {
                    return retC;
                }

                //Find and Check/Open Input Files
                retC = OpenInputStream(fileIn, rawCopy, out zipFileIn, out sourceTrrntzip, out readStream, out streamSize, out compressionMethod, out error);
                if (retC != ReturnCode.Good)
                {
                    return retC;
                }
            }
            else
            {
                sourceTrrntzip = true;
            }

            if (!rawCopy && (Settings.rvSettings.FixLevel == EFixLevel.TrrntZipLevel1 || Settings.rvSettings.FixLevel == EFixLevel.TrrntZipLevel2 || Settings.rvSettings.FixLevel == EFixLevel.TrrntZipLevel3))
            {
                compressionMethod = 8;
            }

            //Find and Check/Open Output Files
            retC = OpenOutputStream(fileOut, fileIn, zipFileOut, filenameOut, compressionMethod, rawCopy, sourceTrrntzip,null, out Stream writeStream, out error);
            if (retC != ReturnCode.Good)
            {
                return retC;
            }

            byte[] bCRC;
            byte[] bMD5 = null;
            byte[] bSHA1 = null;
            if (!isZeroLengthFile)
            {
                #region Do Data Tranfer


                if (!rawCopy)
                {
                    tcrc32 = new ThreadCRC();
                    if (Settings.rvSettings.FixLevel != EFixLevel.Level1 && Settings.rvSettings.FixLevel != EFixLevel.TrrntZipLevel1)
                    {
                        tmd5 = new ThreadMD5();
                        tsha1 = new ThreadSHA1();
                    }
                }

                ulong sizetogo = streamSize;

                while (sizetogo > 0)
                {
                    int sizenow = sizetogo > BufferSize ? BufferSize : (int)sizetogo;

                    try
                    {
                        readStream.Read(_buffer, 0, sizenow);
                    }
                    catch (Exception ex)
                    {
                        if (ex is ZlibException || ex is DataErrorException)
                        {
                            if ((fileIn.FileType == FileType.ZipFile || fileIn.FileType == FileType.SevenZipFile) && zipFileIn != null)
                            {
                                ZipReturn zr = zipFileIn.ZipFileCloseReadStream();
                                if (zr != ZipReturn.ZipGood)
                                {
                                    error = "Error Closing " + zr + " Stream :" + zipFileIn.ZipFilename;
                                    return ReturnCode.FileSystemError;
                                }

                                zipFileIn.ZipFileClose();
                            }
                            else
                            {
                                readStream.Close();
                            }

                            writeStream.Flush();
                            writeStream.Close();
                            if (filenameOut != null)
                            {
                                File.Delete(filenameOut);
                            }

                            error = "Unexpected corrupt archive file found:\n" + fileIn.FullName +
                                    "\nRun Find Fixes, and Fix to continue fixing correctly.";
                            return ReturnCode.SourceDataStreamCorrupt;
                        }

                        error = "Error reading Source File " + ex.Message;
                        return ReturnCode.FileSystemError;
                    }

                    if (!rawCopy)
                    {
                        tcrc32.Trigger(_buffer, sizenow);
                        tmd5?.Trigger(_buffer, sizenow);
                        tsha1?.Trigger(_buffer, sizenow);

                        tcrc32.Wait();
                        tmd5?.Wait();
                        tsha1?.Wait();
                    }
                    try
                    {
                        writeStream.Write(_buffer, 0, sizenow);
                    }
                    catch (Exception e)
                    {
                        error = "Error writing out file. " + Environment.NewLine + e.Message;
                        return ReturnCode.FileSystemError;
                    }
                    sizetogo = sizetogo - (ulong)sizenow;
                }
                writeStream.Flush();

                #endregion

                #region Collect Checksums

                // if we did a full copy then we just calculated all the checksums while doing the copy
                if (!rawCopy)
                {
                    tcrc32.Finish();
                    tmd5?.Finish();
                    tsha1?.Finish();

                    bCRC = tcrc32.Hash;
                    bMD5 = tmd5?.Hash;
                    bSHA1 = tsha1?.Hash;

                    tcrc32.Dispose();
                    tmd5?.Dispose();
                    tsha1?.Dispose();
                }
                // if we raw copied and the source file has been FileChecked then we can trust the checksums in the source file
                else
                {
                    bCRC = fileIn.CRC.Copy();
                    if (fileIn.FileStatusIs(FileStatus.MD5Verified))
                    {
                        bMD5 = fileIn.MD5.Copy();
                    }
                    if (fileIn.FileStatusIs(FileStatus.SHA1Verified))
                    {
                        bSHA1 = fileIn.SHA1.Copy();
                    }
                }

                #endregion

                #region close the ReadStream

                if ((fileIn.FileType == FileType.ZipFile || fileIn.FileType == FileType.SevenZipFile) && zipFileIn != null)
                {
                    ZipReturn zr = zipFileIn.ZipFileCloseReadStream();
                    if (zr != ZipReturn.ZipGood)
                    {
                        error = "Error Closing " + zr + " Stream :" + zipFileIn.ZipFilename;
                        return ReturnCode.FileSystemError;
                    }
                    zipFileIn.ZipFileClose();
                }
                else
                {
                    readStream.Close();
                }

                #endregion
            }
            else
            {
                CopyZeroLengthFile(fileOut, zipFileOut, out bCRC, out bMD5, out bSHA1);
            }

            #region close the WriteStream

            if (fileOut.FileType == FileType.ZipFile || fileOut.FileType == FileType.SevenZipFile)
            {
                ZipReturn zr = zipFileOut.ZipFileCloseWriteStream(bCRC);
                if (zr != ZipReturn.ZipGood)
                {
                    error = "Error Closing Stream " + zr;
                    return ReturnCode.FileSystemError;
                }
                fileOut.ZipFileIndex = zipFileOut.LocalFilesCount() - 1;
                fileOut.ZipFileHeaderPosition = zipFileOut.LocalHeader(fileOut.ZipFileIndex);
                fileOut.FileModTimeStamp = 629870671200000000;
            }
            else
            {
                writeStream.Flush();
                writeStream.Close();
                FileInfo fi = new FileInfo(filenameOut);
                fileOut.FileModTimeStamp = fi.LastWriteTime;
            }

            #endregion

            if (!isZeroLengthFile)
            {
                if (!rawCopy)
                {
                    retC = ValidateFileIn(fileIn, bCRC, bSHA1, bMD5, out error);
                    if (retC != ReturnCode.Good)
                    {
                        return retC;
                    }
                }
            }

            retC = ValidateFileOut(fileIn, fileOut, rawCopy, bCRC, bSHA1, bMD5, out error);
            if (retC != ReturnCode.Good)
            {
                return retC;
            }


            return ReturnCode.Good;
        }

        //Raw Copy
        // Returns True is a raw copy can be used
        // Returns False is a full recompression is required

        public static bool TestRawCopy(RvFile fileIn, RvFile fileOut, bool forceRaw)
        {
            if (fileIn == null || fileOut == null)
            {
                return false;
            }

            if (fileIn.FileType != FileType.ZipFile || fileOut.FileType != FileType.ZipFile)
            {
                return false;
            }

            if (fileIn.Parent == null)
            {
                return false;
            }

            if (forceRaw)
            {
                return true;
            }

            bool trrntzipped = (fileIn.Parent.ZipStatus & ZipStatus.TrrntZip) == ZipStatus.TrrntZip;

            bool deepchecked = fileIn.FileStatusIs(FileStatus.SHA1Verified) && fileIn.FileStatusIs(FileStatus.MD5Verified);

            switch (Settings.rvSettings.FixLevel)
            {
                case EFixLevel.TrrntZipLevel1:
                    return trrntzipped;
                case EFixLevel.TrrntZipLevel2:
                    return trrntzipped && deepchecked;
                case EFixLevel.TrrntZipLevel3:
                    return false;

                case EFixLevel.Level1:
                    return true;
                case EFixLevel.Level2:
                    return deepchecked;
                case EFixLevel.Level3:
                    return false;
            }

            return false;
        }

        private static ReturnCode CheckInputAndOutputFile(RvFile fileIn, RvFile fileOut, out string error)
        {
            // need to check for matching headers types here also.

            if (fileOut.FileStatusIs(FileStatus.SizeFromDAT) && fileOut.Size != null && fileIn.Size != fileOut.Size)
            {
                error = "Source and destination Size does not match. Logic Error.";
                return ReturnCode.LogicError;
            }

            if (fileOut.FileStatusIs(FileStatus.CRCFromDAT) && fileOut.CRC != null && !ArrByte.BCompare(fileIn.CRC, fileOut.CRC))
            {
                error = "Source and destination CRC does not match. Logic Error.";
                return ReturnCode.LogicError;
            }

            if (fileOut.FileStatusIs(FileStatus.SHA1FromDAT) && fileIn.FileStatusIs(FileStatus.SHA1Verified))
            {
                if (fileIn.SHA1 != null && fileOut.SHA1 != null && !ArrByte.BCompare(fileIn.SHA1, fileOut.SHA1))
                {
                    error = "Source and destination SHA1 does not match. Logic Error.";
                    return ReturnCode.LogicError;
                }
            }
            if (fileOut.FileStatusIs(FileStatus.MD5FromDAT) && fileIn.FileStatusIs(FileStatus.MD5Verified))
            {
                if (fileIn.MD5 != null && fileOut.MD5 != null && !ArrByte.BCompare(fileIn.MD5, fileOut.MD5))
                {
                    error = "Source and destination SHA1 does not match. Logic Error.";
                    return ReturnCode.LogicError;
                }
            }
            error = "";
            return ReturnCode.Good;
        }

        private static ReturnCode OpenInputStream(RvFile fileIn, bool rawCopy, out ICompress zipFileIn, out bool sourceTrrntzip, out Stream readStream, out ulong streamSize, out ushort compressionMethod, out string error)
        {
            zipFileIn = null;
            sourceTrrntzip = false;
            readStream = null;
            streamSize = 0;
            compressionMethod = 0;

            if (fileIn.FileType == FileType.ZipFile || fileIn.FileType == FileType.SevenZipFile) // Input is a ZipFile
            {
                RvFile zZipFileIn = fileIn.Parent;
                if (zZipFileIn.FileType != DBTypeGet.DirFromFile(fileIn.FileType))
                {
                    error = "File Open but Source File is not correct type, Logic Error.";
                    return ReturnCode.LogicError;
                }

                string fileNameIn = zZipFileIn.FullName;
                ZipReturn zr1;


                if (zZipFileIn.FileType == FileType.SevenZip)
                {
                    sourceTrrntzip = false;
                    zipFileIn = new SevenZ();
                    zr1 = zipFileIn.ZipFileOpen(fileNameIn, zZipFileIn.FileModTimeStamp);
                }
                else
                {
                    sourceTrrntzip = (zZipFileIn.ZipStatus & ZipStatus.TrrntZip) == ZipStatus.TrrntZip;
                    zipFileIn = new Zip();
                    zr1 = zipFileIn.ZipFileOpen(fileNameIn, zZipFileIn.FileModTimeStamp, fileIn.ZipFileHeaderPosition == null);
                }

                switch (zr1)
                {
                    case ZipReturn.ZipGood:
                        break;
                    case ZipReturn.ZipErrorFileNotFound:
                        error = "File not found, Rescan required before fixing " + fileIn.Name;
                        return ReturnCode.RescanNeeded;
                    case ZipReturn.ZipErrorTimeStamp:
                        error = "File has changed, Rescan required before fixing " + fileIn.Name;
                        return ReturnCode.RescanNeeded;
                    default:
                        error = "Error Open Zip" + zr1 + ", with File " + fileIn.DatTreeFullName;
                        return ReturnCode.FileSystemError;
                }

                if (fileIn.FileType == FileType.SevenZipFile)
                {
                    ((SevenZ)zipFileIn).ZipFileOpenReadStream(fileIn.ZipFileIndex, out readStream, out streamSize);
                }
                else
                {
                    if (fileIn.ZipFileHeaderPosition != null)
                    {
                        ((Zip)zipFileIn).ZipFileOpenReadStreamQuick((ulong)fileIn.ZipFileHeaderPosition, rawCopy, out readStream, out streamSize, out compressionMethod);
                    }
                    else
                    {
                        ((Zip)zipFileIn).ZipFileOpenReadStream(fileIn.ZipFileIndex, rawCopy, out readStream, out streamSize, out compressionMethod);
                    }
                }
            }
            else // Input is a regular file
            {
                string fileNameIn = fileIn.FullName;
                if (!File.Exists(fileNameIn))
                {
                    error = "Rescan needed, File Not Found :" + fileNameIn;
                    return ReturnCode.RescanNeeded;
                }
                FileInfo fileInInfo = new FileInfo(fileNameIn);
                if (fileInInfo.LastWriteTime != fileIn.FileModTimeStamp)
                {
                    error = "Rescan needed, File Changed :" + fileNameIn;
                    return ReturnCode.RescanNeeded;
                }
                int errorCode = FileStream.OpenFileRead(fileNameIn, out readStream);
                if (errorCode != 0)
                {
                    error = new Win32Exception(errorCode).Message + ". " + fileNameIn;
                    return ReturnCode.FileSystemError;
                }
                if (fileIn.Size == null)
                {
                    error = "Null File Size found in Fixing File :" + fileNameIn;
                    return ReturnCode.LogicError;
                }
                streamSize = (ulong)fileIn.Size;
                if ((ulong)readStream.Length != streamSize)
                {
                    error = "Rescan needed, File Length Changed :" + fileNameIn;
                    return ReturnCode.RescanNeeded;
                }
            }

            error = "";
            return ReturnCode.Good;
        }

        // if we are fixing a zip/7z file then we use zipFileOut to open an output compressed stream
        // if we are just making a file then we use filenameOut to open an output filestream
        private static ReturnCode OpenOutputStream(RvFile fileOut, RvFile fileIn, ICompress zipFileOut, string filenameOut, ushort compressionMethod, bool rawCopy, bool sourceTrrntzip, long? dateTime, out Stream writeStream, out string error)
        {
            writeStream = null;

            if (fileOut.FileType == FileType.ZipFile || fileOut.FileType == FileType.SevenZipFile)
            {
                // if ZipFileOut == null then we have not open the output zip yet so open it from writing.
                if (zipFileOut == null)
                {
                    error = "zipFileOut cannot be null";
                    return ReturnCode.FileSystemError;
                }

                if (zipFileOut.ZipOpen != ZipOpenType.OpenWrite)
                {
                    error = "Output Zip File is not set to OpenWrite, Logic Error.";
                    return ReturnCode.LogicError;
                }

                if (fileIn.Size == null)
                {
                    error = "Null File Size found in Fixing File :" + fileIn.FullName;
                    return ReturnCode.LogicError;
                }
                TimeStamps ts = null;
                if (dateTime != null)
                {
                    ts = new TimeStamps {ModTime = dateTime};
                }

                ZipReturn zr = zipFileOut.ZipFileOpenWriteStream(rawCopy, sourceTrrntzip, fileOut.Name, (ulong)fileIn.Size, compressionMethod, out writeStream, ts);
                if (zr != ZipReturn.ZipGood)
                {
                    error = "Error Opening Write Stream " + zr;
                    return ReturnCode.FileSystemError;
                }
            }
            else
            {
                if (File.Exists(filenameOut) && fileOut.GotStatus != GotStatus.Corrupt)
                {
                    error = "Rescan needed, File Changed :" + filenameOut;
                    return ReturnCode.RescanNeeded;
                }
                int errorCode = FileStream.OpenFileWrite(filenameOut, out writeStream);
                if (errorCode != 0)
                {
                    error = new Win32Exception(errorCode).Message + ". " + filenameOut;
                    return ReturnCode.FileSystemError;
                }
            }


            error = "";
            return ReturnCode.Good;
        }



        private static void CopyZeroLengthFile(RvFile fileOut, ICompress zipFileOut, out byte[] bCRC, out byte[] bMD5, out byte[] bSHA1)
        {
            // Zero Length File (Directory in a Zip)
            if (fileOut.FileType == FileType.ZipFile || fileOut.FileType == FileType.SevenZipFile)
            {
                zipFileOut.ZipFileAddZeroLengthFile();
            }
            bCRC = VarFix.CleanMD5SHA1("00000000", 8);
            bMD5 = VarFix.CleanMD5SHA1("d41d8cd98f00b204e9800998ecf8427e", 32);
            bSHA1 = VarFix.CleanMD5SHA1("da39a3ee5e6b4b0d3255bfef95601890afd80709", 40);
        }

        private static ReturnCode ValidateFileIn(RvFile fileIn, byte[] bCRC, byte[] bSHA1, byte[] bMD5, out string error)
        {
            if (!ArrByte.BCompare(bCRC, fileIn.CRC))
            {
                fileIn.GotStatus = GotStatus.Corrupt;
                error = "Source CRC does not match Source Data stream, corrupt Zip";
                return ReturnCode.SourceDataStreamCorrupt;
            }

            fileIn.FileStatusSet(FileStatus.CRCVerified | FileStatus.SizeVerified);

            if (bMD5 != null)
            {
                // check to see if we have a MD5 from the DAT file
                if (fileIn.FileStatusIs(FileStatus.MD5FromDAT))
                {
                    if (fileIn.MD5 == null)
                    {
                        error = "Should have an filein MD5 from Dat but not found. Logic Error.";
                        return ReturnCode.LogicError;
                    }

                    if (!ArrByte.BCompare(fileIn.MD5, bMD5))
                    {
                        error = "Source file did not match MD5";
                        return ReturnCode.SourceCheckSumMismatch;
                    }

                    fileIn.FileStatusSet(FileStatus.MD5Verified);
                }
                // check to see if we have an MD5 (not from the DAT) so must be from previously scanning this file.
                else if (fileIn.MD5 != null)
                {
                    if (!ArrByte.BCompare(fileIn.MD5, bMD5))
                    {
                        // if we had an MD5 from a preview scan and it now does not match, something has gone really bad.
                        error = "The MD5 found does not match a previously scanned MD5, this should not happen, unless something got corrupt.";
                        return ReturnCode.LogicError;
                    }
                }
                else
                {
                    fileIn.MD5 = bMD5;
                    fileIn.FileStatusSet(FileStatus.MD5Verified);
                }
            }

            if (bSHA1 != null)
            {
                // check to see if we have a SHA1 from the DAT file
                if (fileIn.FileStatusIs(FileStatus.SHA1FromDAT))
                {
                    if (fileIn.SHA1 == null)
                    {
                        error = "Should have an filein SHA1 from Dat but not found. Logic Error.";
                        return ReturnCode.LogicError;
                    }

                    if (!ArrByte.BCompare(fileIn.SHA1, bSHA1))
                    {
                        error = "Source file did not match SHA1";
                        return ReturnCode.SourceCheckSumMismatch;
                    }

                    fileIn.FileStatusSet(FileStatus.SHA1Verified);
                }
                // check to see if we have an SHA1 (not from the DAT) so must be from previously scanning this file.
                else if (fileIn.SHA1 != null)
                {
                    if (!ArrByte.BCompare(fileIn.SHA1, bSHA1))
                    {
                        // if we had an SHA1 from a preview scan and it now does not match, something has gone really bad.
                        error = "The SHA1 found does not match a previously scanned SHA1, this should not happen, unless something got corrupt.";
                        return ReturnCode.LogicError;
                    }
                }
                else
                {
                    fileIn.SHA1 = bSHA1;
                    fileIn.FileStatusSet(FileStatus.SHA1Verified);
                }
            }

            error = "";
            return ReturnCode.Good;
        }

        private static ReturnCode ValidateFileOut(RvFile fileIn, RvFile fileOut, bool rawCopy, byte[] bCRC, byte[] bSHA1, byte[] bMD5, out string error)
        {
            if (fileOut.FileType == FileType.ZipFile || fileOut.FileType == FileType.SevenZipFile)
            {
                fileOut.FileStatusSet(FileStatus.SizeFromHeader | FileStatus.CRCFromHeader);
            }

            if (fileOut.FileStatusIs(FileStatus.CRCFromDAT) && fileOut.CRC != null && !ArrByte.BCompare(fileOut.CRC, bCRC))
            {
                error = "CRC checksum error. Level 2 scan your files";
                return ReturnCode.DestinationCheckSumMismatch;
            }

            fileOut.CRC = bCRC;
            if (!rawCopy || fileIn.FileStatusIs(FileStatus.CRCVerified))
            {
                fileOut.FileStatusSet(FileStatus.CRCVerified);
            }


            if (bSHA1 != null)
            {
                if (fileOut.FileStatusIs(FileStatus.SHA1FromDAT) && fileOut.SHA1 != null && !ArrByte.BCompare(fileOut.SHA1, bSHA1))
                {

                    error = "SHA1 checksum error. Level 2 scan your files";
                    return ReturnCode.DestinationCheckSumMismatch;
                }

                fileOut.SHA1 = bSHA1;
                fileOut.FileStatusSet(FileStatus.SHA1Verified);
            }

            if (bMD5 != null)
            {
                if (fileOut.FileStatusIs(FileStatus.MD5FromDAT) && fileOut.MD5 != null && !ArrByte.BCompare(fileOut.MD5, bMD5))
                {
                    error = "MD5 checksum error. Level 2 scan your files";
                    return ReturnCode.DestinationCheckSumMismatch;
                }
                fileOut.MD5 = bMD5;
                fileOut.FileStatusSet(FileStatus.MD5Verified);
            }

            if (fileIn.Size != null)
            {
                fileOut.Size = fileIn.Size;
                fileOut.FileStatusSet(FileStatus.SizeVerified);
            }


            fileOut.GotStatus = fileIn.GotStatus == GotStatus.Corrupt ? GotStatus.Corrupt : GotStatus.Got;

            fileOut.FileStatusSet(FileStatus.SizeVerified);

            if (fileOut.AltSHA1 == null && fileIn.AltSHA1 != null)
            {
                fileOut.AltSHA1 = fileIn.AltSHA1;
            }
            if (fileOut.AltMD5 == null && fileIn.AltMD5 != null)
            {
                fileOut.AltMD5 = fileIn.AltMD5;
            }


            fileOut.CHDVersion = fileIn.CHDVersion;

            fileOut.FileStatusSet(FileStatus.AltSHA1FromHeader | FileStatus.AltMD5FromHeader | FileStatus.AltSHA1Verified | FileStatus.AltMD5Verified, fileIn);

            error = "";
            return ReturnCode.Good;
        }


    }
}