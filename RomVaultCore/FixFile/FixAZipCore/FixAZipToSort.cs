using System;
using System.Collections.Generic;
using System.Linq;
using Compress;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RVIO;
using static RomVaultCore.FixFile.FixAZipCore.FindSourceFile;

namespace RomVaultCore.FixFile.FixAZipCore
{
    internal static class FixAZipToSort
    {
        public static ReturnCode MoveToSort(RvFile fixZip, RvFile fixZippedFile, ref RvFile toSortGame, ref ICompress toSortZipOut, int iRom, Dictionary<string, RvFile> filesUsedForFix, out string errorMessage)
        {
            //if (!((fixZippedFile.DatStatus == DatStatus.NotInDat || fixZippedFile.DatStatus==DatStatus.InDatMerged) && fixZippedFile.GotStatus == GotStatus.Got))
            //{
            //    ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
            //}

            ReportError.LogOut("MovetoSort:");
            ReportError.LogOut(fixZippedFile);
            // move the rom out to the To Sort Directory

            string toSortFullName;
            if (toSortGame == null)
            {
                ReturnCode retCode = FixFileUtils.CreateToSortDirs(fixZip, out RvFile outDir, out string toSortFileName);
                if (retCode != ReturnCode.Good)
                {
                    errorMessage = "Error Creating ToSortDirs";
                    return retCode;
                }

                ZipStructure newFileStruct = fixZip.ZipStruct;

                if ((fixZip.ZipStruct == ZipStructure.None && fixZip.FileType == FileType.SevenZip) || newFileStruct == ZipStructure.SevenZipTrrnt)
                    newFileStruct = Settings.rvSettings.getDefault7ZStruct;

                toSortGame = new RvFile(fixZip.FileType)
                {
                    Parent = outDir,
                    Name = toSortFileName,
                    DatStatus = DatStatus.InToSort,
                    GotStatus = GotStatus.Got,
                    ZipStruct = newFileStruct
                };
                toSortFullName = Path.Combine(outDir.FullName, toSortGame.Name);
            }
            else
                toSortFullName = toSortZipOut.ZipFilename;

            RvFile toSortRom = new RvFile(fixZippedFile.FileType);
            fixZippedFile.CopyTo(toSortRom);
            toSortRom.Dat = null;
            toSortRom.SetDatGotStatus(DatStatus.InToSort, GotStatus.Got);

            ReturnCode returnCode;

            if (toSortZipOut == null)
            {
                returnCode = FixAZipFunctions.OpenOutputZip(toSortGame, FixAZipFunctions.GetMoveToSortUncompressedSize(fixZip), toSortFullName, out toSortZipOut, out errorMessage);
                if (returnCode != ReturnCode.Good)
                {
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
                }
            }

            List<RvFile> lstFixRomTable = GetFixFileList(fixZippedFile);

            RvFile[] filesIn = FindSourceToUseForFix(fixZip, fixZippedFile, lstFixRomTable, out FixStyle fixStyle);

            RvFile fileIn = null;
            if (fixZip.FileType == FileType.Zip && fixZip.ZipStruct == ZipStructure.None)
            {
                fileIn = fixZip.Child(iRom);
                fixStyle = FixStyle.RawCopy;
            }

            if (fileIn == null)
                fileIn = filesIn.Contains(fixZip.Child(iRom)) ? fixZip.Child(iRom) : filesIn.FirstOrDefault();

            if (fixStyle == FixStyle.ExtractToCache)
            {
                ReturnCode returnCode1 = Decompress7ZipFile.DecompressSource7ZipFile(fileIn.Parent, true, filesUsedForFix, out errorMessage);
                if (returnCode1 != ReturnCode.Good)
                {
                    ReportError.LogOut($"DecompressSource7Zip: {fileIn.Parent.FileName} return {returnCode1}");
                    return returnCode1;
                }
                lstFixRomTable = GetFixFileList(fixZippedFile);
                fileIn = FindSourceToUseForFix(fixZip, fixZippedFile, lstFixRomTable, out fixStyle).FirstOrDefault();


                if (fixStyle == FixStyle.ExtractToCache)
                {
                    ReportError.LogOut($"DecompressSource7Zip: {fileIn.Parent.FileName} return {returnCode1}");
                    return ReturnCode.FileSystemError;
                }
            }

            string fixZipFullName = fixZip.TreeFullName;

            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, "Raw-->", Path.GetDirectoryName(toSortFullName), Path.GetFileName(toSortFullName), toSortRom.Name));

            returnCode = FixFileUtils.CopyFile(fileIn, toSortZipOut, null, toSortRom, fixStyle, out errorMessage);
            switch (returnCode)
            {
                case ReturnCode.Good: // correct reply to continue;
                    break;
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
            }
            fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted

            // this should not be done here, should be done once the file is closed
            fixZippedFile.FileGroup?.Files.Add(toSortRom);

            toSortGame.ChildAdd(toSortRom);

            foreach (RvFile f in lstFixRomTable)
            {
                string fn = f.TreeFullName;
                if (!filesUsedForFix.ContainsKey(fn))
                    filesUsedForFix.Add(fn, f);
            }


            return ReturnCode.Good;
        }

    }
}
