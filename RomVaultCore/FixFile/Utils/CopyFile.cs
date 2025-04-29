/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.ComponentModel;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.Support.Compression.Deflate;
using Compress.Support.Compression.LZMA;
using Compress.ThreadReaders;
using Compress.ZipFile;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using static RomVaultCore.FixFile.FixAZipCore.FindSourceFile;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace RomVaultCore.FixFile.Utils
{
    public enum ReturnCode
    {
        Good,
        RescanNeeded,
        FindFixesMissingFileGroups,
        FindFixesInvalidStatus,
        TreeStructureError,
        LogicError,
        FileSystemError,
        SourceDataStreamCorrupt,
        SourceCheckSumMismatch,
        DestinationCheckSumMismatch,
        ToSortNotFound,
        CannotMove,
        Cancel
    }


    public static partial class FixFileUtils
    {
        private const int BufferSize = 32 * 1024 * 1024;
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
        /// <param name="fileOut">This is the actual output file</param>
        /// <param name="fixStyle">if true then we will do a raw copy, this is so that we can copy corrupt zips</param>
        /// <param name="error">This is the returned error message if this copy fails</param>
        /// <returns>
        /// 
        /// zipFileOut should be set if we are fixing to an archive
        /// filenameOut should be set if we are fixing to a file
        /// Only one of these 2 variables should be set.
        /// 
        /// ReturnCode.Good is the valid return code otherwise we have an error.
        ///
        /// </returns>
        public static ReturnCode CopyFile(RvFile fileIn, ICompress zipFileOut, string filenameOut, RvFile fileOut, FixStyle fixStyle, out string error)
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

            // trusting that this logic is correct, as it is now not double checked anywhere else.
            bool rawCopy = fixStyle == FixStyle.RawCopy;

            ulong streamSize = 0;
            ushort inCompressionMethod = 8;

            bool isZeroLengthFile = DBHelper.IsZeroLengthFile(fileOut);
            byte[] properties = null;
            if (!isZeroLengthFile)
            {
                //check that the in and out file match
                retC = CheckInputAndOutputFile(fileIn, fileOut, out error);
                if (retC != ReturnCode.Good)
                {
                    return retC;
                }

                //Find and Check/Open Input Files
                retC = OpenInputStream(fileIn, rawCopy, out zipFileIn, out readStream, out streamSize, out inCompressionMethod, out properties, out error);
                if (retC != ReturnCode.Good)
                {
                    return retC;
                }
            }

            retC = GetOutputCompressionMethod(zipFileOut, fileOut, inCompressionMethod, rawCopy, out ushort outCompressionMethod);
            if (retC != ReturnCode.Good)
                return retC;

            if (rawCopy && inCompressionMethod != outCompressionMethod)
                return ReturnCode.LogicError;


            //Find and Check/Open Output Files
            long dateTimeOut = fileOut.FileModTimeStamp != long.MinValue && fileOut.FileStatusIs(FileStatus.DateFromDAT)
                ? fileOut.FileModTimeStamp : fileIn.FileModTimeStamp;

            retC = OpenOutputStream(fileOut, fileIn, zipFileOut, filenameOut, outCompressionMethod, rawCopy, dateTimeOut, properties, out Stream writeStream, out error);
            if (retC != ReturnCode.Good)
                return retC;

            byte[] bCRC;
            byte[] bMD5 = null;
            byte[] bSHA1 = null;
            if (!isZeroLengthFile)
            {
                #region Do Data Tranfer


                if (!rawCopy)
                {
                    tcrc32 = new ThreadCRC();
                    if (Settings.rvSettings.FixLevel != EFixLevel.Level1)
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
                            if ((fileIn.FileType == FileType.FileZip || fileIn.FileType == FileType.FileSevenZip) && zipFileIn != null)
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
                                readStream?.Close();
                                readStream?.Dispose();
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

                    tcrc32?.Trigger(_buffer, sizenow);
                    tmd5?.Trigger(_buffer, sizenow);
                    tsha1?.Trigger(_buffer, sizenow);
                    try
                    {
                        writeStream.Write(_buffer, 0, sizenow);
                    }
                    catch (Exception e)
                    {
                        error = "Copy File: Error writing out file. " + Environment.NewLine + e.Message;
                        return ReturnCode.FileSystemError;
                    }
                    tcrc32?.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
                    sizetogo = sizetogo - (ulong)sizenow;


                    if (Report.CancellationPending())
                    {
                        tcrc32?.Dispose();
                        tmd5?.Dispose();
                        tsha1?.Dispose();

                        if ((fileIn.FileType == FileType.FileZip || fileIn.FileType == FileType.FileSevenZip) && zipFileIn != null)
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
                            readStream.Dispose();
                        }

                        if (fileOut.FileType == FileType.FileZip || fileOut.FileType == FileType.FileSevenZip)
                        {
                            zipFileOut.ZipFileCloseFailed();
                        }
                        else
                        {
                            writeStream.Flush();
                            writeStream.Close();
                            writeStream.Dispose();
                            File.Delete(filenameOut);
                        }

                        return ReturnCode.Cancel;
                    }
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

                if ((fileIn.FileType == FileType.FileZip || fileIn.FileType == FileType.FileSevenZip) && zipFileIn != null)
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
                    readStream.Dispose();
                }

                #endregion
            }
            else
            {
                bCRC = VarFix.CleanMD5SHA1("00000000", 8);
                bMD5 = VarFix.CleanMD5SHA1("d41d8cd98f00b204e9800998ecf8427e", 32);
                bSHA1 = VarFix.CleanMD5SHA1("da39a3ee5e6b4b0d3255bfef95601890afd80709", 40);
            }

            #region close the WriteStream

            if (fileOut.FileType == FileType.FileZip || fileOut.FileType == FileType.FileSevenZip)
            {
                ZipReturn zr = zipFileOut.ZipFileCloseWriteStream(bCRC);
                if (zr != ZipReturn.ZipGood)
                {
                    error = "Error Closing Stream " + zr;
                    return ReturnCode.FileSystemError;
                }
                fileOut.ZipFileIndex = zipFileOut.LocalFilesCount - 1;
                fileOut.ZipFileHeaderPosition = zipFileOut.GetFileHeader(fileOut.ZipFileIndex).LocalHead;
                fileOut.FileModTimeStamp = zipFileOut.GetFileHeader(fileOut.ZipFileIndex).LastModified;
            }
            else
            {
                writeStream.Flush();
                writeStream.Close();
                writeStream.Dispose();
                FileInfo fi = new FileInfo(filenameOut);
                fileOut.FileModTimeStamp = fi.LastWriteTime;
            }

            #endregion

            if (!isZeroLengthFile && !rawCopy)
            {
                retC = ValidateFileIn(fileIn, bCRC, bSHA1, bMD5, out error);
                if (retC != ReturnCode.Good)
                {
                    return retC;
                }
            }

            retC = ValidateFileOut(fileIn, fileOut, rawCopy, bCRC, bSHA1, bMD5, out error);
            if (retC != ReturnCode.Good)
            {
                return retC;
            }


            return ReturnCode.Good;
        }

        private static ReturnCode GetOutputCompressionMethod(ICompress zipFileOut, RvFile fileOut, ushort inCompressionMethod, bool rawCopy, out ushort outCompressionMethod)
        {
            outCompressionMethod = 0;

            if (zipFileOut == null)
            {
                outCompressionMethod = 0;
                return ReturnCode.Good;
            }

            if (zipFileOut.ZipStruct == ZipStructure.None)
            {
                if (fileOut.FileType == FileType.FileZip)
                {
                    outCompressionMethod = rawCopy ? inCompressionMethod : (ushort)8;
                    return ReturnCode.Good;
                }
                else
                    return ReturnCode.LogicError;
            }
            outCompressionMethod = StructuredArchive.GetCompressionType(zipFileOut.ZipStruct);
            return outCompressionMethod == ushort.MaxValue ? ReturnCode.LogicError : ReturnCode.Good;
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

        private static ReturnCode OpenInputStream(RvFile fileIn, bool rawCopy, out ICompress zipFileIn, out Stream readStream, out ulong streamSize, out ushort compressionMethod, out byte[] properties, out string error)
        {
            zipFileIn = null;
            readStream = null;
            streamSize = 0;
            compressionMethod = 0;
            properties = null;

            if (fileIn.FileType == FileType.FileZip || fileIn.FileType == FileType.FileSevenZip) // Input is a ZipFile
            {
                RvFile zZipFileIn = fileIn.Parent;
                if (zZipFileIn.FileType != DBTypeGet.DirFromFile(fileIn.FileType))
                {
                    error = "File Open but Source File is not correct type, Logic Error.";
                    return ReturnCode.LogicError;
                }

                string fileNameIn = zZipFileIn.FullNameCase;
                ZipReturn zr1;


                if (zZipFileIn.FileType == FileType.SevenZip)
                {
                    zipFileIn = new SevenZ();
                    zr1 = zipFileIn.ZipFileOpen(fileNameIn, zZipFileIn.FileModTimeStamp, false, FileStream.BufSizeMax);
                }
                else
                {
                    zipFileIn = new Zip();
                    zr1 = zipFileIn.ZipFileOpen(fileNameIn, zZipFileIn.FileModTimeStamp, fileIn.ZipFileHeaderPosition == null, FileStream.BufSizeMax);
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
                    case ZipReturn.ZipFileLocked:
                        error = "Source file is locked by another app " + fileNameIn;
                        return ReturnCode.FileSystemError;
                    default:
                        error = "Error Open Zip File " + fileNameIn;
                        if (zipFileIn is Zip)
                            error += $" ({zr1}, {Error.ErrorMessage})";
                        return ReturnCode.FileSystemError;
                }

                if (fileIn.FileType == FileType.FileSevenZip)
                {
                    ZipReturn zErr = ((SevenZ)zipFileIn).ZipFileOpenReadStream(fileIn.ZipFileIndex, rawCopy, out readStream, out streamSize, out compressionMethod, out properties);
                    if (zErr != ZipReturn.ZipGood)
                    {
                        error = $"Error Opening Zip File {fileNameIn}. ({zErr})";
                        return ReturnCode.LogicError;
                    }
                }
                else
                {
                    if (fileIn.ZipFileHeaderPosition != null)
                    {
                        ZipReturn zErr = ((Zip)zipFileIn).ZipFileOpenReadStreamFromLocalHeaderPointer((ulong)fileIn.ZipFileHeaderPosition, rawCopy, out readStream, out streamSize, out compressionMethod);
                        if (zErr != ZipReturn.ZipGood)
                        {
                            error = $"Error Opening Zip File {fileNameIn}, Full scan this file. Read Stream Quick ({zErr}, {Error.ErrorMessage})";
                            return ReturnCode.RescanNeeded;
                        }
                    }
                    else
                    {
                        ZipReturn zErr = ((Zip)zipFileIn).ZipFileOpenReadStream(fileIn.ZipFileIndex, rawCopy, out readStream, out streamSize, out compressionMethod);
                        if (zErr != ZipReturn.ZipGood)
                        {
                            error = $"Error Opening Zip File {fileNameIn}, Full scan this file. Read Stream ({zErr}, {Error.ErrorMessage})";
                            return ReturnCode.RescanNeeded;
                        }
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
        private static ReturnCode OpenOutputStream(RvFile fileOut, RvFile fileIn, ICompress zipFileOut, string filenameOut, ushort compressionMethod, bool rawCopy, long? modTime, byte[] properties, out Stream writeStream, out string error)
        {
            writeStream = null;

            if (fileOut.FileType == FileType.FileZip || fileOut.FileType == FileType.FileSevenZip)
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

                ZipReturn zr = ZipReturn.ZipGood;
                if (zipFileOut is SevenZ zipFileOutSevenZ)
                    zr = zipFileOutSevenZ.ZipFileOpenWriteStream(rawCopy, fileOut.Name, (ulong)fileIn.Size, compressionMethod, properties, out writeStream, modTime, Settings.rvSettings.zstdCompCount);
                else
                    zr = zipFileOut.ZipFileOpenWriteStream(rawCopy, fileOut.Name, (ulong)fileIn.Size, compressionMethod, out writeStream, modTime, Settings.rvSettings.zstdCompCount);
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
                int errorCode = FileStream.OpenFileWrite(filenameOut, FileStream.BufSizeMax, out writeStream);
                if (errorCode != 0)
                {
                    error = new Win32Exception(errorCode).Message + ". " + filenameOut;
                    return ReturnCode.FileSystemError;
                }
            }


            error = "";
            return ReturnCode.Good;
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
            if (fileOut.FileType == FileType.FileZip || fileOut.FileType == FileType.FileSevenZip)
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


            if (fileOut.AltSize == null && fileIn.AltSize != null)
            {
                fileOut.AltSize = fileIn.AltSize;
            }
            if (fileOut.AltCRC == null && fileIn.AltCRC != null)
            {
                fileOut.AltCRC = fileIn.AltCRC;
            }
            if (fileOut.AltSHA1 == null && fileIn.AltSHA1 != null)
            {
                fileOut.AltSHA1 = fileIn.AltSHA1;
            }
            if (fileOut.AltMD5 == null && fileIn.AltMD5 != null)
            {
                fileOut.AltMD5 = fileIn.AltMD5;
            }

            if (fileOut.HeaderFileType == HeaderFileType.Nothing && fileIn.HeaderFileType != HeaderFileType.Nothing)
            {
                fileOut.HeaderFileTypeSet = fileIn.HeaderFileType; // if the fileout was Nothing, then it did not have a required flag, so it is OK to just set it to the fileInValue
            }

            fileOut.CHDVersion = fileIn.CHDVersion;

            fileOut.FileStatusSet(FileStatus.HeaderFileTypeFromHeader |
                FileStatus.AltSizeFromHeader | FileStatus.AltSizeVerified |
                FileStatus.AltCRCFromHeader | FileStatus.AltCRCVerified |
                FileStatus.AltSHA1FromHeader | FileStatus.AltSHA1Verified |
                FileStatus.AltMD5FromHeader | FileStatus.AltMD5Verified, fileIn);

            error = "";
            return ReturnCode.Good;
        }


    }
}