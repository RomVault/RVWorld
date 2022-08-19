using System;
using Compress;
using Compress.SevenZip;
using Compress.ZipFile;
using RomVaultCore.FixFile.Util;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile
{
    public static class FixAZipFunctions
    {


        public static ReturnCode OpenTempFixZip(RvFile fixZip,out ICompress tempFixZip,out string errorMessage)
        {
            string strPath = fixZip.Parent.FullName;
            string tempZipFilename = Path.Combine(strPath, "__RomVault.tmp");
            ReturnCode returnCode1 = OpenOutputZip(fixZip, tempZipFilename, out tempFixZip, out errorMessage);
            if (returnCode1 != ReturnCode.Good)
            {
                ReportError.LogOut($"CorrectZipFile: OutputOutput {tempZipFilename} return {returnCode1}");
                return returnCode1;
            }
            return ReturnCode.Good;
        }

        public static void GetSourceDir(RvFile fileIn, out string sourceDir, out string sourceFile)
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

        public static ReturnCode MoveToCorrupt(RvFile fixZip, RvFile fixZippedFile, ref RvFile toSortCorruptGame, ref ICompress toSortCorruptOut, int iRom)
        {
            if (!((fixZippedFile.DatStatus == DatStatus.InDatCollect || fixZippedFile.DatStatus == DatStatus.NotInDat || fixZippedFile.DatStatus==DatStatus.InDatMIA) && fixZippedFile.GotStatus == GotStatus.Corrupt))
            {
                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            }

            ReportError.LogOut("Moving File to Corrupt");
            ReportError.LogOut(fixZippedFile);

            if (fixZippedFile.FileType == FileType.SevenZipFile)
            {
                fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
                return ReturnCode.Good;
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
                ReturnCode returnCode1 = OpenOutputZip(toSortCorruptGame, toSortFullName, out toSortCorruptOut, out string errorMessage1);
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
                case ReturnCode.Cancel:
                    return returnCode;
                // doing a raw copy so not needed
                // case ReturnCode.SourceCRCCheckSumError: 
                // case ReturnCode.SourceCheckSumError:
                // case ReturnCode.DestinationCheckSumError: 
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
            }

            fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
            return ReturnCode.Good;
        }


        public static ReturnCode OpenOutputZip(RvFile fixZip, string outputZipFilename, out ICompress outputFixZip, out string errorMessage)
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
                outputFixZip = new Zip();
                zrf = ((Zip)outputFixZip).ZipFileCreate(outputZipFilename, !fixZip.IsInToSort ? OutputZipType.TrrntZip : OutputZipType.None);
            }
            else
            {
                outputFixZip = new SevenZ();
                zrf = ((SevenZ)outputFixZip).ZipFileCreateFromUncompressedSize(outputZipFilename, Settings.rvSettings.zstd ? SevenZ.SevenZipCompressType.zstd : SevenZ.SevenZipCompressType.lzma, GetUncompressedSize(fixZip));
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
                    case RepStatus.CorrectMIA:
                    case RepStatus.CanBeFixed:
                    case RepStatus.CanBeFixedMIA:
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
            if (fi.LastWriteTime != fixZip.FileModTimeStamp)
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
                FileModTimeStamp = toSortCorruptFile.LastWriteTime,
                GotStatus = GotStatus.Corrupt
            };
            toSort.Child(indexcorrupt).ChildAdd(toSortCorruptGame);

            FixFileUtils.CheckDeleteFile(fixZip);

            return ReturnCode.Good;
        }

    }
}
