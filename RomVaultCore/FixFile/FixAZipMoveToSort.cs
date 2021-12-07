using System;
using System.Collections.Generic;
using Compress;
using RomVaultCore.FixFile.Util;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile
{
    internal static class FixAZipMoveToSort
    {
        public static ReturnCode MovetoSort(RvFile fixZip, RvFile fixZippedFile, ref RvFile toSortGame, ref ICompress toSortZipOut, int iRom, Dictionary<string, RvFile> filesUserForFix)
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
                ReturnCode retCode = FixFileUtils.CreateToSortDirs(fixZip, out RvFile outDir, out string toSortFileName);
                if (retCode != ReturnCode.Good)
                    return retCode;

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


            ReturnCode returnCode;
            string errorMessage;

            if (toSortZipOut == null)
            {
                returnCode = FixAZipFunctions.OpenOutputZip(toSortGame, toSortFullName, out toSortZipOut, out errorMessage);
                if (returnCode != ReturnCode.Good)
                {
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
                }
            }

            RvFile fileIn = fixZip.Child(iRom);
            List<RvFile> lstFixRomTable = null;

            if (fileIn.FileType == FileType.SevenZipFile && fileIn.Size > 0)
            {
                lstFixRomTable = FindSourceFile.GetFixFileList(fixZippedFile);
                ReportError.LogOut("CorrectZipFile: picking from");
                ReportError.ReportList(lstFixRomTable);

                fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, lstFixRomTable);

                if (fileIn.FileType == FileType.SevenZipFile)
                {
                    ReturnCode returnCode1 = Decompress7ZipFile.DecompressSource7ZipFile(fixZip, true, out errorMessage);
                    if (returnCode1 != ReturnCode.Good)
                    {
                        ReportError.LogOut($"DecompressSource7Zip: OutputOutput {fixZip.FileName} return {returnCode1}");
                        return returnCode1;
                    }

                    lstFixRomTable = FindSourceFile.GetFixFileList(fixZippedFile);
                    fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, lstFixRomTable);
                }
            }

            string fixZipFullName = fixZip.TreeFullName;

            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, "Raw-->", Path.GetDirectoryName(toSortFullName), Path.GetFileName(toSortFullName), toSortRom.Name));

            returnCode = FixFileUtils.CopyFile(fileIn, toSortZipOut, null, toSortRom, true, out errorMessage);
            switch (returnCode)
            {
                case ReturnCode.Good: // correct reply to continue;
                    break;
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
            }
            fixZippedFile.GotStatus = GotStatus.NotGot; // Changes RepStatus to Deleted

            fixZippedFile.FileGroup.Files.Add(toSortRom);
            toSortGame.ChildAdd(toSortRom);

            if (lstFixRomTable != null)
                foreach (RvFile f in lstFixRomTable)
                {
                    string fn = f.TreeFullName;
                    if (!filesUserForFix.ContainsKey(fn))
                        filesUserForFix.Add(fn, f);
                }


            return ReturnCode.Good;
        }

    }
}
