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
    internal static class FixAZipCanBeFixed
    {
        /// <summary>
        /// Fixed a missing file inside a .ZIP file.
        /// </summary>
        /// <param name="fixZip">The RvFile of the actual .ZIP file that is being fixed.</param>
        /// <param name="fixZippedFile">A temp copy of the RvFile record of the actual compressed file inside the fixZip .zip that is about to be fixed.</param>
        /// <param name="tempFixZip">Is the new output archive file that is being created to fix this zip, that will become the new zip once done</param>
        /// <param name="filesUsedForFix"></param>
        /// <param name="totalFixed"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static ReturnCode CanBeFixed(bool copyOriginal, RvFile fixZip, RvFile fixZippedFile, ref ICompress tempFixZip, int iRom, Dictionary<string, RvFile> filesUsedForFix, ref int totalFixed, out string errorMessage)
        {
            string logMsg = copyOriginal ? "CorrectZipFile" : "CanBeFixed";
            if (copyOriginal)
            {
                if (!(
                fixZippedFile.DatStatus == DatStatus.InDatCollect && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InDatMIA && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InDatMerged && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.NotInDat && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Got ||
                fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Corrupt))
                { ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus); }
            }
            else
            {
                if (!(
                    (fixZippedFile.DatStatus == DatStatus.InDatCollect || fixZippedFile.DatStatus == DatStatus.InDatMIA) &&
                    (fixZippedFile.GotStatus == GotStatus.NotGot || fixZippedFile.GotStatus == GotStatus.Corrupt)))
                { ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus); }

            }
            ReportError.LogOut($"{logMsg}:");
            ReportError.LogOut(fixZippedFile);

            if (tempFixZip == null)
            {
                string tempZipFilename = Path.Combine(fixZip.Parent.FullName, $"__RomVault.tmp");
                ReturnCode returnCode1 = FixAZipFunctions.OpenOutputZip(fixZip, FixAZipFunctions.GetUncompressedSize(fixZip), tempZipFilename, out tempFixZip, out errorMessage);
                if (returnCode1 != ReturnCode.Good)
                {
                    tempZipFilename = Path.Combine(fixZip.Parent.FullName, $"__RomVault.{DateTime.UtcNow.Ticks}.tmp");
                    returnCode1 = FixAZipFunctions.OpenOutputZip(fixZip, FixAZipFunctions.GetUncompressedSize(fixZip), tempZipFilename, out tempFixZip, out errorMessage);
                    if (returnCode1 != ReturnCode.Good)
                    {
                        ReportError.LogOut($"{logMsg}: OutputOutput {tempZipFilename} return {returnCode1}");
                        return returnCode1;
                    }
                }
            }



            List<RvFile> lstFixRomTable = null;
            bool isZeroLengthFile = DBHelper.IsZeroLengthFile(fixZippedFile);
            if (!isZeroLengthFile)
            {
                lstFixRomTable = GetFixFileList(fixZippedFile);
                if (!copyOriginal)
                {
                    ReportError.LogOut($"{logMsg}: picking from");
                    ReportError.ReportList(lstFixRomTable);
                }

                if (lstFixRomTable.Count == 0)
                {
                    // thought we could fix it, turns out we cannot
                    fixZippedFile.GotStatus = GotStatus.NotGot;
                    errorMessage = "";
                    return ReturnCode.Good;
                }
            }

            RvFile fixFileSource;
            FixStyle fixStyle;
            if (isZeroLengthFile)
            {
                fixFileSource = new RvFile(FileType.File) { Size = 0 };
                fixStyle = FixStyle.Zero;
            }
            else if (copyOriginal && fixZip.FileType == FileType.Zip && fixZip.newZipStruct == ZipStructure.None)
            {
                fixFileSource = fixZip.Child(iRom);
                fixStyle = FixStyle.RawCopy;
            }
            else
            {
                RvFile[] fixFileSourceList = FindSourceToUseForFix(fixZip, fixZippedFile, lstFixRomTable, out fixStyle);
                fixFileSource = copyOriginal && fixFileSourceList.Contains(fixZip.Child(iRom)) ? fixZip.Child(iRom) : fixFileSourceList.FirstOrDefault();
            }
            if (fixStyle == FixStyle.ExtractToCache)
            {
                ReturnCode returnCode1 = Decompress7ZipFile.DecompressSource7ZipFile(fixFileSource.Parent, copyOriginal, filesUsedForFix, out errorMessage);
                if (returnCode1 != ReturnCode.Good)
                {
                    ReportError.LogOut($"DecompressSource7Zip: {fixFileSource.Parent.FileName} return {returnCode1}");
                    return returnCode1;
                }
                lstFixRomTable = GetFixFileList(fixZippedFile);
                fixFileSource = FindSourceToUseForFix(fixZip, fixZippedFile, lstFixRomTable, out fixStyle).FirstOrDefault();

                if (fixStyle == FixStyle.ExtractToCache)
                {
                    ReportError.LogOut($"DecompressSource7Zip: {fixFileSource.Parent.FileName} return {returnCode1}");
                    return ReturnCode.FileSystemError;
                }
            }

            ReportError.LogOut($"{logMsg}: Copying from");
            ReportError.LogOut(fixFileSource);


            if (!isZeroLengthFile && (!copyOriginal || Settings.rvSettings.DetailedFixReporting))
            {
                string fixZipFullName = fixZip.TreeFullName;
                string tMsg = copyOriginal ? "<" : "";
                FixFileUtils.GetSourceDir(fixFileSource, out string sourceDir, out string sourceFile);
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, fixStyle == FixStyle.RawCopy ? $"{tMsg}<--Raw" : $"<{tMsg}--Compress", sourceDir, sourceFile, fixFileSource.Name));
            }

            if (!copyOriginal)
                fixZippedFile.FileTestFix(fixFileSource);

            RepStatus originalStatus = fixZippedFile.RepStatus;
            ReturnCode returnCode = FixFileUtils.CopyFile(fixFileSource, tempFixZip, null, fixZippedFile, fixStyle, out errorMessage);
            switch (returnCode)
            {
                case ReturnCode.Good: // correct reply so continue;
                    if (copyOriginal && originalStatus == RepStatus.NeededForFix)
                        fixZippedFile.RepStatus = RepStatus.NeededForFix;

                    break;
                case ReturnCode.RescanNeeded:
                    ReportError.LogOut($"{logMsg}: RescanNeeded");
                    return returnCode;

                case ReturnCode.SourceDataStreamCorrupt:
                    {
                        ReportError.LogOut($"{logMsg}: Source Data Stream Corrupt /  CRC Error");
                        Report.ReportProgress(new bgwShowFixError("CRC Error"));
                        fixFileSource.GotStatus = GotStatus.Corrupt;
                        return returnCode;
                    }
                case ReturnCode.SourceCheckSumMismatch:
                    {
                        ReportError.LogOut($"{logMsg}: Source Checksum Mismatch / Fix file CRC was not as expected");
                        Report.ReportProgress(new bgwShowFixError("Fix file CRC was not as expected"));
                        return returnCode;
                    }
                case ReturnCode.DestinationCheckSumMismatch:
                    {
                        ReportError.LogOut($"{logMsg}: Destination Checksum Mismatch / Destination file CRC was not as expected");
                        Report.ReportProgress(new bgwShowFixError("Destination file CRC was not as expected"));
                        return returnCode;
                    }
                case ReturnCode.FileSystemError:
                    {
                        ReportError.LogOut($"{logMsg}: Source File Error {errorMessage}");
                        Report.ReportProgress(new bgwShowFixError($"{logMsg}: Source File Error {errorMessage}"));
                        return returnCode;
                    }

                case ReturnCode.Cancel:
                    return returnCode;
                default:
                    throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
            }


            //Check to see if the files used for fix, can now be set to delete
            if (lstFixRomTable != null)
                foreach (RvFile f in lstFixRomTable)
                {
                    string fn = f.TreeFullName;
                    if (!filesUsedForFix.ContainsKey(fn))
                        filesUsedForFix.Add(fn, f);
                }
            if (!copyOriginal)
                totalFixed++;

            errorMessage = "";
            return ReturnCode.Good;
        }

    }
}
