using System;
using System.Collections.Generic;
using Compress;
using Compress.SevenZip;
using Compress.ZipFile;
using RVCore.FixFile.Util;
using RVCore.RvDB;
using RVIO;

namespace RVCore.FixFile
{
    public static class FixAZipFunctions
    {
        public static ReturnCode CorrectZipFile(RvFile fixZip, RvFile fixZippedFile, ref ICompress tempFixZip, int iRom, out string errorMessage)
        {
            if (!
                (
                    fixZippedFile.DatStatus == DatStatus.InDatCollect && fixZippedFile.GotStatus == GotStatus.Got ||
                    fixZippedFile.DatStatus == DatStatus.InDatMerged && fixZippedFile.GotStatus == GotStatus.Got ||
                    fixZippedFile.DatStatus == DatStatus.NotInDat && fixZippedFile.GotStatus == GotStatus.Got ||
                    fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Got ||
                    fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Corrupt
                )
            )
            {
                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            }

            ReportError.LogOut("CorrectZipFile:");
            ReportError.LogOut(fixZippedFile);

            if (fixZippedFile.GotStatus == GotStatus.Corrupt && fixZippedFile.FileType == FileType.SevenZipFile)
            {
                fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
                errorMessage = "";
                return ReturnCode.Good;
            }


            if (tempFixZip == null)
            {
                string strPath = fixZip.Parent.FullName;
                string tempZipFilename = Path.Combine(strPath, "__RomVault.tmp");
                ReturnCode returnCode1 = OpenOutputZip(fixZip, tempZipFilename, out tempFixZip, out errorMessage);
                if (returnCode1 != ReturnCode.Good)
                {
                    ReportError.LogOut($"CorrectZipFile: OutputOutput {tempZipFilename} return {returnCode1}");
                    return returnCode1;
                }
            }

            bool rawcopy = fixZippedFile.RepStatus == RepStatus.InToSort || fixZippedFile.RepStatus == RepStatus.Corrupt;

            RvFile fileIn = fixZip.Child(iRom);

            if (fileIn.FileType == FileType.SevenZipFile)
            {
                List<RvFile> fixFiles = FindSourceFile.GetFixFileList(fixZippedFile);
                ReportError.LogOut("CorrectZipFile: picking from");
                ReportError.ReportList(fixFiles);

                fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, fixFiles);

                if (fileIn.FileType == FileType.SevenZipFile)
                {
                    ReturnCode returnCode1 = Decompress7ZipFile.DecompressSource7ZipFile(fixZip, true, out errorMessage);
                    if (returnCode1 != ReturnCode.Good)
                    {
                        ReportError.LogOut($"DecompressSource7Zip: OutputOutput {fixZip.FileName} return {returnCode1}");
                        return returnCode1;
                    }

                    fixFiles = FindSourceFile.GetFixFileList(fixZippedFile);
                    fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, fixFiles);
                }
            }

            ReportError.LogOut("Copying from");
            ReportError.LogOut(fileIn);

            GetSourceDir(fileIn, out string sourceDir, out string sourceFile);

            if (Settings.rvSettings.DetailedFixReporting)
            {
                string fixZipFullName = fixZip.TreeFullName;

                bool rawCopy = FixFileUtils.TestRawCopy(fileIn, fixZippedFile, rawcopy);
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, rawCopy ? "<<--Raw" : "<<--Compress", sourceDir, sourceFile, fileIn.Name));
            }

            RepStatus originalStatus = fixZippedFile.RepStatus;
            ReturnCode returnCode = FixFileUtils.CopyFile(fileIn, tempFixZip, null, fixZippedFile, rawcopy, out errorMessage);

            switch (returnCode)
            {
                case ReturnCode.Good: // correct reply to continue;
                    if (originalStatus == RepStatus.NeededForFix)
                    {
                        fixZippedFile.RepStatus = RepStatus.NeededForFix;
                    }
                    break;
                case ReturnCode.SourceDataStreamCorrupt:
                    {
                        ReportError.LogOut($"CorrectZipFile: Source Data Stream Corrupt /  CRC Error");
                        Report.ReportProgress(new bgwShowFixError("CRC Error"));
                        RvFile tFile = fixZip.Child(iRom);
                        tFile.GotStatus = GotStatus.Corrupt;
                        break;
                    }

                case ReturnCode.SourceCheckSumMismatch:
                    {
                        ReportError.LogOut($"CorrectZipFile: Source Checksum Mismatch / Fix file CRC was not as expected");
                        Report.ReportProgress(new bgwShowFixError("Fix file CRC was not as expected"));
                        break;
                    }
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + " : " + errorMessage);
            }

            return returnCode;
        }


        /// <summary>
        /// Fixed a missing file inside a .ZIP file.
        /// </summary>
        /// <param name="fixZip">The RvFile of the actual .ZIP file that is being fixed.</param>
        /// <param name="fixZippedFile">A temp copy of the RvFile record of the actual compressed file inside the fixZip .zip that is about to be fixed.</param>
        /// <param name="tempFixZip">Is the new output archive file that is being created to fix this zip, that will become the new zip once done</param>
        /// <param name="fileProcessQueue"></param>
        /// <param name="totalFixed"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>

        public static ReturnCode CanBeFixed(RvFile fixZip, RvFile fixZippedFile, ref ICompress tempFixZip, List<RvFile> fileProcessQueue, ref int totalFixed, out string errorMessage)
        {
            if (!(fixZippedFile.DatStatus == DatStatus.InDatCollect && (fixZippedFile.GotStatus == GotStatus.NotGot || fixZippedFile.GotStatus == GotStatus.Corrupt)))
            {
                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            }

            ReportError.LogOut("CanBeFixed:");
            ReportError.LogOut(fixZippedFile);

            if (tempFixZip == null)
            {
                string strPath = fixZip.Parent.FullName;
                string tempZipFilename = Path.Combine(strPath, "__RomVault.tmp");
                ReturnCode returnCode1 = OpenOutputZip(fixZip, tempZipFilename, out tempFixZip, out errorMessage);
                if (returnCode1 != ReturnCode.Good)
                {
                    ReportError.LogOut($"CanBeFixed: OutputOutput {tempZipFilename} return {returnCode1}");
                    return returnCode1;
                }
            }

            List<RvFile> lstFixRomTable = FindSourceFile.GetFixFileList(fixZippedFile);
            ReportError.LogOut("CanBeFixed: picking from");
            ReportError.ReportList(lstFixRomTable);

            if (DBHelper.IsZeroLengthFile(fixZippedFile))
            {
                RvFile fileIn = new RvFile(FileType.ZipFile) { Size = 0 };
                ReturnCode returnCode = FixFileUtils.CopyFile(fileIn, tempFixZip, null, fixZippedFile, false, out errorMessage);

                switch (returnCode)
                {
                    case ReturnCode.Good: // correct reply to continue;
                        break;
                    default:
                        throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + " : " + errorMessage);
                }

                //Check to see if the files used for fix, can now be set to delete
                FixFileUtils.CheckFilesUsedForFix(lstFixRomTable, fileProcessQueue, false);
                totalFixed++;

                return ReturnCode.Good;
            }

            if (lstFixRomTable.Count > 0)
            {
                RvFile fileIn = lstFixRomTable[0];

                fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, lstFixRomTable);

                if (fileIn.FileType == FileType.SevenZipFile)
                {
                    ReturnCode returnCode1 = Decompress7ZipFile.DecompressSource7ZipFile(fileIn.Parent, false, out errorMessage);
                    if (returnCode1 != ReturnCode.Good)
                    {
                        ReportError.LogOut($"DecompressSource7Zip: OutputOutput {fixZip.FileName} return {returnCode1}");
                        return returnCode1;
                    }
                    lstFixRomTable = FindSourceFile.GetFixFileList(fixZippedFile);
                    fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, lstFixRomTable);
                }

                ReportError.LogOut("CanBeFixed: Copying from");
                ReportError.LogOut(fileIn);

                GetSourceDir(fileIn, out string sourceDir, out string sourceFile);

                string fixZipFullName = fixZip.TreeFullName;

                bool rawCopy = FixFileUtils.TestRawCopy(fileIn, fixZippedFile, false);
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, rawCopy ? "<--Raw" : "<--Compress", sourceDir, sourceFile, fileIn.Name));

                fixZippedFile.FileTestFix(fileIn);

                ReturnCode returnCode = FixFileUtils.CopyFile(fileIn, tempFixZip, null, fixZippedFile, false, out errorMessage);
                switch (returnCode)
                {
                    case ReturnCode.Good: // correct reply so continue;
                        break;
                    case ReturnCode.RescanNeeded:
                        ReportError.LogOut($"CanBeFixed: RescanNeeded");
                        return returnCode;

                    case ReturnCode.SourceDataStreamCorrupt:
                        {
                            ReportError.LogOut($"CanBeFixed: Source Data Stream Corrupt /  CRC Error");
                            Report.ReportProgress(new bgwShowFixError("CRC Error"));
                            fileIn.GotStatus = GotStatus.Corrupt;
                            return returnCode;
                        }
                    case ReturnCode.SourceCheckSumMismatch:
                        {
                            ReportError.LogOut($"CanBeFixed: Source Checksum Mismatch / Fix file CRC was not as expected");
                            Report.ReportProgress(new bgwShowFixError("Fix file CRC was not as expected"));
                            return returnCode;
                        }
                    case ReturnCode.DestinationCheckSumMismatch:
                        {
                            ReportError.LogOut($"CanBeFixed: Destination Checksum Mismatch / Destination file CRC was not as expected");
                            Report.ReportProgress(new bgwShowFixError("Destination file CRC was not as expected"));
                            return returnCode;
                        }
                    default:
                        throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
                }

                //Check to see if the files used for fix, can now be set to delete
                FixFileUtils.CheckFilesUsedForFix(lstFixRomTable, fileProcessQueue, false);
                totalFixed++;
            }
            else
            // thought we could fix it, turns out we cannot
            {
                fixZippedFile.GotStatus = GotStatus.NotGot;
            }
            errorMessage = "";
            return ReturnCode.Good;
        }

        private static void GetSourceDir(RvFile fileIn, out string sourceDir, out string sourceFile)
        {
            string ts = fileIn.Parent.FullName;
            if (fileIn.FileType == FileType.ZipFile || fileIn.FileType == FileType.SevenZipFile)
            {
                sourceDir = Path.GetDirectoryName(ts);
                sourceFile = Path.GetFileName(ts);
            }
            else
            {
                sourceDir = ts;
                sourceFile = "";
            }

        }

        public static void MovetoSort(RvFile fixZip, RvFile fixZippedFile, ref RvFile toSortGame, ref ICompress toSortZipOut, int iRom)
        {
            if (!(fixZippedFile.DatStatus == DatStatus.NotInDat && fixZippedFile.GotStatus == GotStatus.Got))
            {
                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            }

            ReportError.LogOut("MovetoSort:");
            ReportError.LogOut(fixZippedFile);
            // move the rom out to the To Sort Directory

            string toSortFullName;
            if (toSortGame == null)
            {
                FixFileUtils.CreateToSortDirs(fixZip, out RvFile outDir, out string toSortFileName);

                toSortGame = new RvFile(fixZip.FileType)
                {
                    Parent = outDir,
                    Name = toSortFileName,
                    DatStatus = DatStatus.InToSort,
                    GotStatus = GotStatus.Got
                };
                toSortFullName = Path.Combine(outDir.FullName, toSortGame.Name);
            }
            else
                toSortFullName = toSortZipOut.ZipFilename;

            // this needs header / alt info added.
            RvFile toSortRom = new RvFile(fixZippedFile.FileType)
            {
                Name = fixZippedFile.Name,
                Size = fixZippedFile.Size,
                CRC = fixZippedFile.CRC,
                SHA1 = fixZippedFile.SHA1,
                MD5 = fixZippedFile.MD5,
                HeaderFileType = fixZippedFile.HeaderFileType,
                AltSize = fixZippedFile.AltSize,
                AltCRC = fixZippedFile.AltCRC,
                AltSHA1 = fixZippedFile.AltSHA1,
                AltMD5 = fixZippedFile.AltMD5,
                FileGroup = fixZippedFile.FileGroup
            };
            toSortRom.SetStatus(DatStatus.InToSort, GotStatus.Got);
            toSortRom.FileStatusSet(
                FileStatus.HeaderFileTypeFromHeader |
                FileStatus.SizeFromHeader | FileStatus.SizeVerified |
                FileStatus.CRCFromHeader | FileStatus.CRCVerified |
                FileStatus.SHA1FromHeader | FileStatus.SHA1Verified |
                FileStatus.MD5FromHeader | FileStatus.MD5Verified |
                FileStatus.AltSizeFromHeader | FileStatus.AltSizeVerified |
                FileStatus.AltCRCFromHeader | FileStatus.AltCRCVerified |
                FileStatus.AltSHA1FromHeader | FileStatus.AltSHA1Verified |
                FileStatus.AltMD5FromHeader | FileStatus.AltMD5Verified
                , fixZippedFile);

            toSortGame.ChildAdd(toSortRom);

            ReturnCode returnCode;
            string errorMessage;

            if (toSortZipOut == null)
            {
                returnCode = OpenOutputZip(fixZip, toSortFullName, out toSortZipOut, out errorMessage);
                if (returnCode != ReturnCode.Good)
                {
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
                }
            }


            string fixZipFullName = fixZip.TreeFullName;

            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, "Raw-->", Path.GetDirectoryName(toSortFullName), Path.GetFileName(toSortFullName), toSortRom.Name));

            returnCode = FixFileUtils.CopyFile(fixZip.Child(iRom), toSortZipOut, null, toSortRom, true, out errorMessage);
            switch (returnCode)
            {
                case ReturnCode.Good: // correct reply to continue;
                    break;
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
            }
            fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
        }

        public static void MoveToCorrupt(RvFile fixZip, RvFile fixZippedFile, ref RvFile toSortCorruptGame, ref ICompress toSortCorruptOut, int iRom)
        {
            if (!((fixZippedFile.DatStatus == DatStatus.InDatCollect || fixZippedFile.DatStatus == DatStatus.NotInDat) && fixZippedFile.GotStatus == GotStatus.Corrupt))
            {
                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            }

            ReportError.LogOut("Moving File to Corrupt");
            ReportError.LogOut(fixZippedFile);

            if (fixZippedFile.FileType == FileType.SevenZipFile)
            {
                fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
                return;
            }

            string toSortFullName;
            if (toSortCorruptGame == null)
            {
                string corruptDir = Path.Combine(DB.ToSort(), "Corrupt");
                if (!Directory.Exists(corruptDir))
                {
                    Directory.CreateDirectory(corruptDir);
                }

                toSortFullName = Path.Combine(corruptDir, fixZip.Name);
                string toSortFileName = fixZip.Name;
                int fileC = 0;
                while (File.Exists(toSortFullName))
                {
                    fileC++;
                    string fName = Path.GetFileNameWithoutExtension(fixZip.Name);
                    string fExt = Path.GetExtension(fixZip.Name);
                    toSortFullName = Path.Combine(corruptDir, fName + fileC + fExt);
                    toSortFileName = fixZip.Name + fileC;
                }

                toSortCorruptGame = new RvFile(FileType.Zip)
                {
                    Name = toSortFileName,
                    DatStatus = DatStatus.InToSort,
                    GotStatus = GotStatus.Got
                };
            }
            else
            {
                string corruptDir = Path.Combine(DB.ToSort(), "Corrupt");
                toSortFullName = Path.Combine(corruptDir, toSortCorruptGame.Name);
            }

            RvFile toSortCorruptRom = new RvFile(FileType.ZipFile)
            {
                Name = fixZippedFile.Name,
                Size = fixZippedFile.Size,
                CRC = fixZippedFile.CRC
            };
            toSortCorruptRom.SetStatus(DatStatus.InToSort, GotStatus.Corrupt);
            toSortCorruptGame.ChildAdd(toSortCorruptRom);

            if (toSortCorruptOut == null)
            {
                ReturnCode returnCode1 = OpenOutputZip(fixZip, toSortFullName, out toSortCorruptOut, out string errorMessage1);
                if (returnCode1 != ReturnCode.Good)
                {
                    throw new FixAZip.ZipFileException(returnCode1, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode1 + Environment.NewLine + errorMessage1);
                }
            }


            string fixZipFullName = fixZip.TreeFullName;
            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, "Raw-->", "Corrupt", Path.GetFileName(toSortFullName), fixZippedFile.Name));

            ReturnCode returnCode = FixFileUtils.CopyFile(fixZip.Child(iRom), toSortCorruptOut, null, toSortCorruptRom, true, out string errorMessage);
            switch (returnCode)
            {
                case ReturnCode.Good: // correct reply to continue;
                    break;

                // doing a raw copy so not needed
                // case ReturnCode.SourceCRCCheckSumError: 
                // case ReturnCode.SourceCheckSumError:
                // case ReturnCode.DestinationCheckSumError: 
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
            }

            fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
        }


        private static ReturnCode OpenOutputZip(RvFile fixZip, string outputZipFilename, out ICompress outputFixZip, out string errorMessage)
        {
            outputFixZip = null;
            if (Path.GetFileName(outputZipFilename) == "__RomVault.tmp")
            {
                if (File.Exists(outputZipFilename))
                {
                    File.Delete(outputZipFilename);
                }
            }
            else if (File.Exists(outputZipFilename))
            {
                errorMessage = "Rescan needed, Unkown existing file found :" + outputZipFilename;
                return ReturnCode.RescanNeeded;
            }

            ZipReturn zrf;
            if (fixZip.FileType == FileType.Zip)
            {
                outputFixZip = new ZipFile();
                zrf = outputFixZip.ZipFileCreate(outputZipFilename);
            }
            else
            {
                outputFixZip = new SevenZ();
                zrf = ((SevenZ)outputFixZip).ZipFileCreateFromUncompressedSize(outputZipFilename, GetUncompressedSize(fixZip));
            }

            if (zrf != ZipReturn.ZipGood)
            {
                errorMessage = "Error Opening Write Stream " + zrf;
                return ReturnCode.FileSystemError;
            }

            errorMessage = "";
            return ReturnCode.Good;
        }


        private static ulong GetUncompressedSize(RvFile fixZip)
        {
            ulong uncompressedSize = 0;
            for (int i = 0; i < fixZip.ChildCount; i++)
            {
                RvFile sevenZippedFile = fixZip.Child(i);
                switch (sevenZippedFile.RepStatus)
                {
                    case RepStatus.Correct:
                    case RepStatus.CanBeFixed:
                    case RepStatus.CorruptCanBeFixed:
                        uncompressedSize += sevenZippedFile.FileGroup.Size ?? 0;
                        break;
                    case RepStatus.Missing:
                        break;
                    default:
                        break;
                }
            }

            return uncompressedSize;
        }


        public static ReturnCode MoveZipToCorrupt(RvFile fixZip, out string errorMessage)
        {
            errorMessage = "";

            string fixZipFullName = fixZip.FullName;
            if (!File.Exists(fixZipFullName))
            {
                errorMessage = "File for move to corrupt not found " + fixZip.FullName;
                return ReturnCode.RescanNeeded;
            }
            FileInfo fi = new FileInfo(fixZipFullName);
            if (fi.LastWriteTime != fixZip.TimeStamp)
            {
                errorMessage = "File for move to corrupt timestamp not correct " + fixZip.FullName;
                return ReturnCode.RescanNeeded;
            }

            string corruptDir = Path.Combine(DB.ToSort(), "Corrupt");
            if (!Directory.Exists(corruptDir))
            {
                Directory.CreateDirectory(corruptDir);
            }

            RvFile toSort = DB.RvFileToSort();
            RvFile corruptDirNew = new RvFile(FileType.Dir) { Name = "Corrupt", DatStatus = DatStatus.InToSort };
            int found = toSort.ChildNameSearch(corruptDirNew, out int indexcorrupt);
            if (found != 0)
            {
                corruptDirNew.GotStatus = GotStatus.Got;
                indexcorrupt = toSort.ChildAdd(corruptDirNew);
            }

            string toSortFullName = Path.Combine(corruptDir, fixZip.Name);
            string toSortFileName = fixZip.Name;
            int fileC = 0;
            while (File.Exists(toSortFullName))
            {
                fileC++;

                string fName = Path.GetFileNameWithoutExtension(fixZip.Name);
                string fExt = Path.GetExtension(fixZip.Name);
                toSortFullName = Path.Combine(corruptDir, fName + fileC + fExt);
                toSortFileName = fixZip.Name + fileC;
            }

            if (!File.SetAttributes(fixZipFullName, FileAttributes.Normal))
            {
                int error = Error.GetLastError();
                Report.ReportProgress(new bgwShowError(fixZipFullName, "Error Setting File Attributes to Normal. Before Moving To Corrupt. Code " + error));
            }


            File.Move(fixZipFullName, toSortFullName);
            FileInfo toSortCorruptFile = new FileInfo(toSortFullName);

            RvFile toSortCorruptGame = new RvFile(FileType.Zip)
            {
                Name = toSortFileName,
                DatStatus = DatStatus.InToSort,
                TimeStamp = toSortCorruptFile.LastWriteTime,
                GotStatus = GotStatus.Corrupt
            };
            toSort.Child(indexcorrupt).ChildAdd(toSortCorruptGame);

            FixFileUtils.CheckDeleteFile(fixZip);

            return ReturnCode.Good;
        }

    }
}
