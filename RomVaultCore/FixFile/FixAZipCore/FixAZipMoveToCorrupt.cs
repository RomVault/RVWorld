using Compress;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RVIO;
using System;

namespace RomVaultCore.FixFile.FixAZipCore
{
    internal static class FixAZipMoveToCorrupt
    {
        public static ReturnCode MoveToCorrupt(RvFile fixZip, RvFile fixZippedFile, ref RvFile toSortCorruptGame, ref ICompress toSortCorruptOut, int iRom)
        {
            if (!((fixZippedFile.DatStatus == DatStatus.InDatCollect || fixZippedFile.DatStatus == DatStatus.NotInDat || fixZippedFile.DatStatus == DatStatus.InDatMIA) && fixZippedFile.GotStatus == GotStatus.Corrupt))
            {
                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            }

            ReportError.LogOut("Moving File to Corrupt");
            ReportError.LogOut(fixZippedFile);

            if (fixZippedFile.FileType == FileType.FileSevenZip)
            {
                fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted
                return ReturnCode.Good;
            }

            string toSortFullName;
            if (toSortCorruptGame == null)
            {
                string corruptDir = Path.Combine(DB.GetToSortPrimary().Name, "Corrupt");
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
                string corruptDir = Path.Combine(DB.GetToSortPrimary().Name, "Corrupt");
                toSortFullName = Path.Combine(corruptDir, toSortCorruptGame.Name);
            }

            RvFile toSortCorruptRom = new RvFile(FileType.FileZip)
            {
                Name = fixZippedFile.Name,
                Size = fixZippedFile.Size,
                CRC = fixZippedFile.CRC
            };
            toSortCorruptRom.SetDatGotStatus(DatStatus.InToSort, GotStatus.Corrupt);
            toSortCorruptGame.ChildAdd(toSortCorruptRom);

            if (toSortCorruptOut == null)
            {
                ReturnCode returnCode1 = FixAZipFunctions.OpenOutputZip(toSortCorruptGame, 0, toSortFullName, out toSortCorruptOut, out string errorMessage1);
                if (returnCode1 != ReturnCode.Good)
                {
                    throw new FixAZip.ZipFileException(returnCode1, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode1 + Environment.NewLine + errorMessage1);
                }
            }

            string fixZipFullName = fixZip.TreeFullName;
            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, "Raw-->", "Corrupt", Path.GetFileName(toSortFullName), fixZippedFile.Name));

            ReturnCode returnCode = FixFileUtils.CopyFile(fixZip.Child(iRom), toSortCorruptOut, null, toSortCorruptRom, FindSourceFile.FixStyle.RawCopy, out string errorMessage);
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

    }
}
