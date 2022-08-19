using System;
using System.Collections.Generic;
using Compress;
using RomVaultCore.FixFile.Util;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile
{
    internal static class FixAZipCanBeFixed
    {
        /// <summary>
        /// Fixed a missing file inside a .ZIP file.
        /// </summary>
        /// <param name="fixZip">The RvFile of the actual .ZIP file that is being fixed.</param>
        /// <param name="fixZippedFile">A temp copy of the RvFile record of the actual compressed file inside the fixZip .zip that is about to be fixed.</param>
        /// <param name="tempFixZip">Is the new output archive file that is being created to fix this zip, that will become the new zip once done</param>
        /// <param name="filesUserForFix"></param>
        /// <param name="totalFixed"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static ReturnCode CanBeFixed(RvFile fixZip, RvFile fixZippedFile, ref ICompress tempFixZip, Dictionary<string, RvFile> filesUserForFix, ref int totalFixed, out string errorMessage)
        {
            if (!(
                (fixZippedFile.DatStatus == DatStatus.InDatCollect || fixZippedFile.DatStatus==DatStatus.InDatMIA) && 
                (fixZippedFile.GotStatus == GotStatus.NotGot || fixZippedFile.GotStatus == GotStatus.Corrupt)))
            { ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " + fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus); }

            ReportError.LogOut("CanBeFixed:");
            ReportError.LogOut(fixZippedFile);

            if (tempFixZip == null)
            {
                ReturnCode ret1 = FixAZipFunctions.OpenTempFixZip(fixZip, out tempFixZip, out errorMessage);
                if (ret1 != ReturnCode.Good)
                    return ret1;
            }

            List<RvFile> lstFixRomTable = FindSourceFile.GetFixFileList(fixZippedFile);

            ReportError.LogOut("CanBeFixed: picking from");
            ReportError.ReportList(lstFixRomTable);

            if (DBHelper.IsZeroLengthFile(fixZippedFile))
            {
                RvFile fileInZ = new RvFile(FileType.ZipFile) { Size = 0 };
                ReturnCode returnCode1 = FixFileUtils.CopyFile(fileInZ, tempFixZip, null, fixZippedFile, false, out errorMessage);

                switch (returnCode1)
                {
                    case ReturnCode.Good: // correct reply to continue;
                        break;
                    case ReturnCode.Cancel:
                        return returnCode1;
                    default:
                        throw new FixAZip.ZipFileException(returnCode1, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode1 + " : " + errorMessage);
                }
            }
            else
            {
                if (lstFixRomTable.Count == 0)
                {
                    // thought we could fix it, turns out we cannot
                    fixZippedFile.GotStatus = GotStatus.NotGot;
                    errorMessage = "";
                    return ReturnCode.Good;
                }

                RvFile fileIn = FindSourceFile.FindSourceToUseForFix(fixZippedFile, lstFixRomTable);

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

                FixAZipFunctions.GetSourceDir(fileIn, out string sourceDir, out string sourceFile);

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
                    case ReturnCode.FileSystemError:
                        {
                            ReportError.LogOut($"CanBeFixed: Source File Error {errorMessage}");
                            Report.ReportProgress(new bgwShowFixError($"CanBeFixed: Source File Error {errorMessage}"));
                            return returnCode;
                        }

                    case ReturnCode.Cancel:
                        return returnCode;
                    default:
                        throw new FixAZip.ZipFileException(returnCode, fixZippedFile.FullName + " " + fixZippedFile.RepStatus + " " + returnCode + Environment.NewLine + errorMessage);
                }

            }

            //Check to see if the files used for fix, can now be set to delete
            foreach (RvFile f in lstFixRomTable)
            {
                string fn = f.TreeFullName;
                if (!filesUserForFix.ContainsKey(fn))
                    filesUserForFix.Add(fn, f);
            }
            totalFixed++;

            errorMessage = "";
            return ReturnCode.Good;
        }

    }
}
