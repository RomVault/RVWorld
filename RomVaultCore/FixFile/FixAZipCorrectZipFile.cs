using System;
using System.Collections.Generic;
using Compress;
using RomVaultCore.FixFile.Util;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile
{
    internal static class FixAZipCorrectZipFile
    {
        /// <summary>
        /// Fixed a missing file inside a .ZIP file.
        /// </summary>
        /// <param name="fixZip">The RvFile of the actual .ZIP file that is being fixed.</param>
        /// <param name="fixZippedFile">A temp copy of the RvFile record of the actual compressed file inside the fixZip .zip that is about to be fixed.</param>
        /// <param name="tempFixZip">Is the new output archive file that is being created to fix this zip, that will become the new zip once done</param>
        /// <param name="iRom"></param>
        /// <param name="filesUserForFix"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static ReturnCode CorrectZipFile(RvFile fixZip, RvFile fixZippedFile, ref ICompress tempFixZip, int iRom, Dictionary<string, RvFile> filesUserForFix, out string errorMessage)
        {
            if (!(
                fixZippedFile.DatStatus == DatStatus.InDatCollect && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InDatMerged && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.NotInDat && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Corrupt))
            { ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus); }

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
                ReturnCode ret1 = FixAZipFunctions.OpenTempFizZip(fixZip, out tempFixZip, out errorMessage);
                if (ret1 != ReturnCode.Good)
                    return ret1;
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

            ReportError.LogOut("Copying from");
            ReportError.LogOut(fileIn);

            FixAZipFunctions.GetSourceDir(fileIn, out string sourceDir, out string sourceFile);

            bool rawCopyForce = fixZippedFile.RepStatus == RepStatus.InToSort || fixZippedFile.RepStatus == RepStatus.Corrupt;

            if (Settings.rvSettings.DetailedFixReporting)
            {
                string fixZipFullName = fixZip.TreeFullName;

                bool rawCopy = FixFileUtils.TestRawCopy(fileIn, fixZippedFile, rawCopyForce);
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, rawCopy ? "<<--Raw" : "<<--Compress", sourceDir, sourceFile, fileIn.Name));
            }

            RepStatus originalStatus = fixZippedFile.RepStatus;
            ReturnCode returnCode = FixFileUtils.CopyFile(fileIn, tempFixZip, null, fixZippedFile, rawCopyForce, out errorMessage);

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
                case ReturnCode.FileSystemError:
                    {
                        ReportError.LogOut($"CorrectZipFile: Source File Error {errorMessage}");
                        Report.ReportProgress(new bgwShowFixError($"CorrectZipFile: Source File Error {errorMessage}"));
                        return returnCode;
                    }

                case ReturnCode.Cancel:
                    return returnCode;
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + " : " + errorMessage);
            }



            if (lstFixRomTable != null)
                foreach (RvFile f in lstFixRomTable)
                {
                    string fn = f.TreeFullName;
                    if (!filesUserForFix.ContainsKey(fn))
                        filesUserForFix.Add(fn, f);
                }


            return returnCode;
        }


    }
}
