using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Compress;
using FileScanner;
using RomVaultCore.FixFile.FixAZipCore;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVIO;

namespace RomVaultCore.FixFile
{
    public static partial class FixAZip
    {

        public static ReturnCode FixZipNew(RvFile fixZip, List<RvFile> fileProcessQueue, ref int totalFixed, out string errorMessage)
        {
            errorMessage = "";

            //Check for error status
            if (fixZip.DirStatus.HasUnknown())
            {
                return ReturnCode.FindFixesInvalidStatus; // Error
            }


            // need to add code to check if we want a trrntzip or an rvzip

            bool needsTrrntzipped =
                            fixZip.GotStatus == GotStatus.Got &&
                            fixZip.DatStatus == DatStatus.InDatCollect &&
                            fixZip.ZipDatStructFix &&
                            fixZip.ZipStruct != fixZip.ZipDatStruct;

            // file corrupt and not in tosort
            //      if file cannot be fully fixed copy to corrupt
            //      process zipfile

            if (fixZip.GotStatus == GotStatus.Corrupt && fixZip.DatStatus != DatStatus.InToSort && !fixZip.DirStatus.HasFixable())
            {
                ReturnCode moveReturnCode = FixAZipFunctions.MoveZipToCorrupt(fixZip, out errorMessage);
                if (moveReturnCode != ReturnCode.Good)
                {
                    errorMessage = $@"Move Zip To Corrupt Error with {moveReturnCode}";
                    return moveReturnCode;
                }
            }

            // if we have files that can be fixed then we will fix now. And trrntzip if needed.
            if (!fixZip.DirStatus.FixCheckFilesCanBeFixed())
            {
                //if has Needed for Fix then do not fix it yet.
                if (fixZip.DirStatus.FixCheckHasNeededForFix())
                    return ReturnCode.Good;

                //if we have any files that should be removed, or trrntzip if needed.
                if (!fixZip.DirStatus.FixCheckHasFilesToBeRemoved() && !needsTrrntzipped)
                {
                    RenameZipFileIfCaseRenameNeeded(fixZip);

                    return ReturnCode.Good;
                }
            }

            string fixZipFullName = fixZip.TreeFullName;

            if (!fixZip.DirStatus.HasFixable() && needsTrrntzipped)
            {
                string fixType = fixZip.ZipDatStruct.ToString();
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), "", null, fixType, "", "", ""));
            }


            FixFileUtils.CheckCreateDirectories(fixZip.Parent);

            string filename = fixZip.FullName;
            if (fixZip.GotStatus == GotStatus.NotGot)
            {
                if (File.Exists(filename))
                {
                    errorMessage = "Unexpected file found in directory. Rescan needed.\n" + filename;
                    return ReturnCode.RescanNeeded;
                }
            }

            if (Settings.rvSettings.DebugLogsEnabled)
            {
                ReportError.LogOut("");
                ReportError.LogOut(fixZipFullName + " : " + fixZip.RepStatus);
                ReportError.LogOut("------------------------------------------------------------");
                Debug.WriteLine(fixZipFullName + " : " + fixZip.RepStatus);
                ReportError.LogOut("Zip File Status Before Fix:");

                int deleteCount = 0;
                int moveToSortCount = 0;
                int notThereCount = 0;
                for (int intLoop = 0; intLoop < fixZip.ChildCount; intLoop++)
                {
                    var fixZipFile = fixZip.Child(intLoop);
                    ReportError.LogOut(fixZipFile);
                    if (fixZipFile.RepStatus == RepStatus.MoveToSort || fixZipFile.RepStatus == RepStatus.MoveToCorrupt)
                        moveToSortCount++;
                    if (fixZipFile.RepStatus == RepStatus.Delete)
                        deleteCount++;
                    if (fixZipFile.RepStatus == RepStatus.Missing || fixZip.RepStatus == RepStatus.NotCollected)
                        notThereCount++;
                }
                ReportError.LogOut($"MoveToSortCount {moveToSortCount} , DeleteCount {deleteCount} , NotThereCount {notThereCount}");
                ReportError.LogOut("");
            }
            ReturnCode returnCode;
            RepStatus fileRepStatus = RepStatus.UnSet;

            Dictionary<string, RvFile> filesUsedForFix = new Dictionary<string, RvFile>();
            returnCode = FixAZipMove.CheckFileMove(fixZip, filesUsedForFix, ref totalFixed, out errorMessage);
            if (returnCode == ReturnCode.Good)
            {
                List<RvFile> usedFiles = filesUsedForFix.Values.ToList();
                FixFileUtils.CheckFilesUsedForFix(usedFiles, fileProcessQueue, false);
                return ReturnCode.Good;
            }
            else if (returnCode != ReturnCode.Cancel)
            {
                errorMessage = $@"Check File Move Error with {returnCode}-{errorMessage}";
                return returnCode;
            }
            returnCode = FixAZipMove.CheckFileMoveToSort(fixZip, ref totalFixed, out errorMessage);
            if (returnCode == ReturnCode.Good)
            {
                return ReturnCode.Good;
            }
            else if (returnCode != ReturnCode.Cancel)
            {
                errorMessage = $@"Check File MovetoSort Error with {returnCode}";
                return returnCode;
            }

            for (int iRom = 0; iRom < fixZip.ChildCount; iRom++)
            {
                var getChild = fixZip.Child(iRom);
                if (getChild.RepStatus == RepStatus.Missing || getChild.RepStatus == RepStatus.MissingMIA || getChild.RepStatus == RepStatus.NotCollected)
                    continue;
                if (getChild.FileGroup == null)
                    return ReturnCode.FindFixesMissingFileGroups;
            }



            ICompress tempFixZip = null;
            ICompress toSortCorruptOut = null;
            ICompress toSortZipOut = null;
            ReportError.procLog("FixAZip: Initialize Zip Files");
            try
            {
                RvFile toSortGame = null;
                RvFile toSortCorruptGame = null;

                ScannedFile fixZipTemp = new ScannedFile(fixZip.FileType);

                //                FileType fixFileType = fixZip.FileType;

                for (int iRom = 0; iRom < fixZip.ChildCount; iRom++)
                {
                    RvFile fixingChild = fixZip.Child(iRom);

                    ReportError.LogOut(fixingChild.RepStatus + " : " + fixingChild.FullName);
                    ReportError.procLog("FixAZip: " + fixingChild.RepStatus + " : " + fixingChild.FullName);


                    fileRepStatus = fixingChild.RepStatus;
                    switch (fixingChild.RepStatus)
                    {
                        // any file we do not have or do not want in the destination zip
                        case RepStatus.Missing:
                        case RepStatus.MissingMIA:
                        case RepStatus.NotCollected:
                        case RepStatus.Rename:
                        case RepStatus.Delete:
                        case RepStatus.Incomplete:
                            if (!
                                (
                                    // got the file in the original zip but will be deleting it
                                    fixingChild.DatStatus == DatStatus.NotInDat && fixingChild.GotStatus == GotStatus.Got ||
                                    fixingChild.DatStatus == DatStatus.NotInDat && fixingChild.GotStatus == GotStatus.Corrupt ||
                                    fixingChild.DatStatus == DatStatus.InDatMerged && fixingChild.GotStatus == GotStatus.Got ||
                                    fixingChild.DatStatus == DatStatus.InToSort && fixingChild.GotStatus == GotStatus.Got ||
                                    fixingChild.DatStatus == DatStatus.InToSort && fixingChild.GotStatus == GotStatus.Corrupt ||

                                    // do not have this file and cannot fix it here
                                    fixingChild.DatStatus == DatStatus.InDatCollect && fixingChild.GotStatus == GotStatus.NotGot ||
                                    fixingChild.DatStatus == DatStatus.InDatMIA && fixingChild.GotStatus == GotStatus.NotGot ||
                                    fixingChild.DatStatus == DatStatus.InDatMIA && fixingChild.GotStatus == GotStatus.Got ||  // this can happen if an MIA is in a incomplete keep only complete zip, and you have another copy of the MIA rom else where.
                                    fixingChild.DatStatus == DatStatus.InDatNoDump && fixingChild.GotStatus == GotStatus.NotGot ||
                                    fixingChild.DatStatus == DatStatus.InDatMerged && fixingChild.GotStatus == GotStatus.NotGot ||

                                     // this is a correct got file in an Incomplete set
                                     fixingChild.DatStatus == DatStatus.InDatCollect && fixingChild.GotStatus == GotStatus.Got
                                )
                            )
                            {
                                ReportError.SendAndShow($"Error in Fix Rom Status {fixingChild.RepStatus} : {fixingChild.DatStatus} : {fixingChild.GotStatus}");
                            }
                            ReportError.procLog("FixAZip: Entered Missing/delete");
                            if (fixingChild.RepStatus == RepStatus.Delete)
                            {
                                if (Settings.rvSettings.DetailedFixReporting)
                                {
                                    Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixingChild.Name, fixingChild.Size, "Delete", "", "", ""));
                                }

                                ReportError.procLog("FixAZip: DoubleCheckDelete");
                                returnCode = FixFileUtils.DoubleCheckDelete(fixZip.Child(iRom), out errorMessage);
                                if (returnCode != ReturnCode.Good)
                                {
                                    ReportError.procLog($"FixAZip: Error DoubleCheckDelete {returnCode}, closing Zips");
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);
                                    ReportError.procLog($"FixAZip: Closed Files returning error");
                                    errorMessage += $"\nDouble Check Delete Error with {returnCode}";
                                    return returnCode;
                                }
                            }
                            break;

                        // any files we are just moving from the original zip to the destination zip
                        case RepStatus.Correct:
                        case RepStatus.CorrectMIA:
                        case RepStatus.InToSort:
                        case RepStatus.NeededForFix:
                        case RepStatus.Corrupt:
                            {
                                if (fixingChild.GotStatus == GotStatus.Corrupt && fixingChild.FileType == FileType.FileSevenZip)
                                {
                                    // Changes RepStatus to Deleted
                                    break;
                                }

                                ReportError.procLog($"FixAZip: Calling Can Be fixed, correct");
                                returnCode = FixAZipCanBeFixed.CanBeFixed(true, fixZip, fixingChild, ref tempFixZip, iRom, filesUsedForFix, ref totalFixed, out errorMessage);
                                ReportError.procLog($"FixAZip: Returning from Can Be fixed");
                                if (returnCode != ReturnCode.Good)
                                {
                                    ReportError.procLog($"FixAZip: Returning from Can Be fixed error {returnCode}, closing zips");
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);
                                    ReportError.procLog($"FixAZip: Closed Files returning error");
                                    errorMessage += $"\nCorrectZipFile Error with {returnCode}";
                                    return returnCode;
                                }
                                break;
                            }

                        case RepStatus.CanBeFixed:
                        case RepStatus.CanBeFixedMIA:
                        case RepStatus.CorruptCanBeFixed:
                            {
                                ReportError.procLog($"FixAZip: Calling Can Be fixed, Fixing");
                                returnCode = FixAZipCanBeFixed.CanBeFixed(false, fixZip, fixingChild, ref tempFixZip, 0, filesUsedForFix, ref totalFixed, out errorMessage);
                                ReportError.procLog($"FixAZip: Returning from Can Be fixed");
                                if (returnCode != ReturnCode.Good)
                                {
                                    ReportError.procLog($"FixAZip: Returning from Can Be fixed error {returnCode}, closing zips");
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);
                                    ReportError.procLog($"FixAZip: Closed Files returning error");
                                    errorMessage += $"\nCanBeFixed Error with {returnCode}";
                                    return returnCode;
                                }
                                break;
                            }
                        case RepStatus.MoveToSort:
                            {
                                ReportError.procLog($"FixAZip: Calling MoveToSort");
                                returnCode = FixAZipToSort.MoveToSort(fixZip, fixingChild, ref toSortGame, ref toSortZipOut, iRom, filesUsedForFix, out errorMessage);
                                ReportError.procLog($"FixAZip: Returned from MoveToSort");
                                if (returnCode != ReturnCode.Good)
                                {
                                    ReportError.procLog($"FixAZip: Returning from Can Be fixed error {returnCode}, closing zips");
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);
                                    ReportError.procLog($"FixAZip: Closed Files returning error");
                                    errorMessage += $"\nMoveToSort Error with {returnCode}";
                                    return returnCode;
                                }
                                break;
                            }

                        case RepStatus.MoveToCorrupt:
                            ReportError.procLog($"FixAZip: Calling MoveToCorrupt");
                            returnCode = FixAZipMoveToCorrupt.MoveToCorrupt(fixZip, fixingChild, ref toSortCorruptGame, ref toSortCorruptOut, iRom);
                            ReportError.procLog($"FixAZip: Returning from MoveToCorrupt");
                            if (returnCode != ReturnCode.Good)
                            {
                                ReportError.procLog($"FixAZip: Returning from Move To Corrupt error {returnCode}, closing zips");
                                CloseZipFile(ref tempFixZip);
                                CloseToSortGame(toSortGame, ref toSortZipOut);
                                CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);
                                ReportError.procLog($"FixAZip: Closed Files returning error");
                                errorMessage += $"\nMoveToCorrupt Error with {returnCode}";
                                return returnCode;
                            }
                            break;

                        default:
                            ReportError.UnhandledExceptionHandler($"Unknown file status found {fixingChild.RepStatus} while fixing file {fixZip.Name} Dat Status = {fixingChild.DatStatus} GotStatus {fixingChild.GotStatus}");
                            break;
                    }

                    if (Report.CancellationPending())
                    {
                        ReportError.procLog($"FixAZip: Cancellation Pending Closing All");
                        try { tempFixZip?.ZipFileCloseFailed(); } catch { }; tempFixZip = null;
                        try { toSortZipOut?.ZipFileCloseFailed(); } catch { }; toSortZipOut = null;
                        try { toSortCorruptOut?.ZipFileCloseFailed(); } catch { }; toSortCorruptOut = null;
                        ReportError.procLog($"FixAZip: Cancellation Pending Returning");

                        errorMessage = "Cancel";
                        return ReturnCode.Cancel;
                    }

                }

                //if ToSort Zip Made then close the zip and add this new zip to the Database
                ReportError.procLog($"FixAZip: ClosetoSortGame");
                returnCode = CloseToSortGame(toSortGame, ref toSortZipOut);
                if (returnCode != ReturnCode.Good)
                {
                    try { tempFixZip?.ZipFileCloseFailed(); } catch { }; tempFixZip = null;
                    try { toSortZipOut?.ZipFileCloseFailed(); } catch { }; toSortZipOut = null;
                    try { toSortCorruptOut?.ZipFileCloseFailed(); } catch { }; toSortCorruptOut = null;
                    errorMessage += $"\nErrorClosing ToSort Game {returnCode}";
                    return returnCode;
                }

                //if Corrupt Zip Made then close the zip and add this new zip to the Database
                ReportError.procLog($"FixAZip: Close Corrupt Game");
                returnCode = CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);
                if (returnCode != ReturnCode.Good)
                {
                    try { tempFixZip?.ZipFileCloseFailed(); } catch { }; tempFixZip = null;
                    try { toSortZipOut?.ZipFileCloseFailed(); } catch { }; toSortZipOut = null;
                    try { toSortCorruptOut?.ZipFileCloseFailed(); } catch { }; toSortCorruptOut = null;
                    errorMessage += $"\nMoveToCorrupt Corrupt Game {returnCode}";
                    return returnCode;
                }


                #region Process original Zip

                ReportError.procLog($"FixAZip: test {filename} exists");
                if (File.Exists(filename))
                {
                    ReportError.procLog($"FixAZip: {filename} exists setting attributes");
                    if (!File.SetAttributes(filename, FileAttributes.Normal))
                    {
                        Report.ReportProgress(new bgwShowError(filename, $"Error Setting File Attributes to Normal. Deleting Original Fix File. Code {RVIO.Error.ErrorMessage}"));
                    }

                    try
                    {
                        ReportError.procLog($"FixAZip: {filename} deleting");
                        File.Delete(filename);
                    }
                    catch (Exception e)
                    {
                        ReportError.procLog($"FixAZip: {filename} failed to delete");
                        errorMessage = $"Error While trying to delete file {filename}. {e.Message}";

                        if (tempFixZip != null && tempFixZip.ZipOpen != ZipOpenType.Closed)
                        {
                            tempFixZip.ZipFileClose();
                            tempFixZip = null;
                        }

                        ReportError.procLog($"FixAZip: returning from failed to delete");
                        return ReturnCode.RescanNeeded;
                    }
                }

                #endregion

                bool checkDelete = false;

                #region process the temp Zip rename it to the original Zip

                if (tempFixZip != null && tempFixZip.ZipOpen != ZipOpenType.Closed)
                {
                    ReportError.procLog($"FixAZip: cleaning up tempFixZip");
                    string tempFilename = tempFixZip.ZipFilename;
                    tempFixZip.ZipFileClose();

                    if (tempFixZip.LocalFilesCount > 0)
                    {
                        // now rename the temp fix file to the correct filename
                        File.Move(tempFilename, filename);
                        FileInfo nFile = new FileInfo(filename);
                        RvFile tmpZip = new RvFile(FileType.Zip)
                        {
                            Name = Path.GetFileName(filename),
                            FileModTimeStamp = nFile.LastWriteTime
                        };
                        tmpZip.SetDatGotStatus(fixZip.DatStatus, GotStatus.Got);

                        fixZip.FileMergeIn(tmpZip, false);
                        fixZip.ZipStruct = tempFixZip.ZipStruct;
                    }
                    else
                    {
                        File.Delete(tempFilename);
                        checkDelete = true;
                    }
                    ReportError.procLog($"FixAZip: cleaning up tempFixZip Finished");

                }
                else
                {
                    checkDelete = true;
                }

                tempFixZip = null;

                #endregion

                #region Now put the New Game Status information into the Database.

                /*  this needs fixed
                int intLoopFix = 0;
                ReportError.procLog($"FixAZip: putting back data");
                foreach (RvFile tmpZip in fixZipTemp)
                {
                    tmpZip.CopyTo(fixZip.Child(intLoopFix));

                    if (fixZip.Child(intLoopFix).GotStatus == GotStatus.NotGot)
                    {
                        if (fixZip.Child(intLoopFix).FileRemove() == EFile.Delete)
                        {
                            fixZip.ChildRemove(intLoopFix);
                            continue;
                        }
                    }

                    intLoopFix++;
                }
                */

                #endregion

                List<RvFile> usedFiles = filesUsedForFix.Values.ToList();

                ReportError.procLog($"FixAZip: check files used to fix.");
                FixFileUtils.CheckFilesUsedForFix(usedFiles, fileProcessQueue, false);

                if (checkDelete)
                {
                    FixFileUtils.CheckDeleteFile(fixZip);
                }

                ReportError.LogOut("");
                ReportError.LogOut("Zip File Status After Fix:");
                for (int intLoop = 0; intLoop < fixZip.ChildCount; intLoop++)
                {
                    ReportError.LogOut(fixZip.Child(intLoop));
                }

                ReportError.LogOut("");

                return ReturnCode.Good;

            }
            catch (ZipFileException ex)
            {
                ReportError.procLog($"FixAZip: Error on ZipfileException, Closing tempFixZip");
                try { tempFixZip?.ZipFileCloseFailed(); } catch { }; tempFixZip = null;
                ReportError.procLog($"FixAZip: Error on ZipfileException, Closing toSortZipOut");
                try { toSortZipOut?.ZipFileCloseFailed(); } catch { }; toSortZipOut = null;
                ReportError.procLog($"FixAZip: Error on ZipfileException, Closing toSortCorruptOut");
                try { toSortCorruptOut?.ZipFileCloseFailed(); } catch { }; toSortCorruptOut = null;
                ReportError.procLog($"FixAZip: Error on ZipfileException, nulling out.");

                errorMessage = "In Fix Zip, ZipFileException:\n" + ex.Message + "\nat\n:" + ex.StackTrace;
                return ex.returnCode;
            }
            catch (Exception ex)
            {
                ReportError.procLog($"FixAZip: Error Exception, Closing tempFixZip");
                try { tempFixZip?.ZipFileCloseFailed(); } catch { }; tempFixZip = null;
                ReportError.procLog($"FixAZip: Error Exception, Closing toSortZipOut");
                try { toSortZipOut?.ZipFileCloseFailed(); } catch { }; toSortZipOut = null;
                ReportError.procLog($"FixAZip: Error Exception, Closing toSortCorruptOut");
                try { toSortCorruptOut?.ZipFileCloseFailed(); } catch { }; toSortCorruptOut = null;
                ReportError.procLog($"FixAZip: Error Exception, nulling out.");

                errorMessage = "In Fix Zip:\n" + ex.Message + "\nat\n:" + ex.StackTrace;
                return ReturnCode.LogicError;
            }
            finally
            {
                ReportError.procLog($"FixAZip: hitting final.");
                if (tempFixZip != null)
                {
                    ReportError.UnhandledExceptionHandler($"{errorMessage}\n\n{tempFixZip.ZipFilename} tempZipOut was left open, ZipFile= {fixZipFullName} , fileRepStatus= {fileRepStatus} , returnCode= {returnCode}");
                }
                if (toSortZipOut != null)
                {
                    ReportError.UnhandledExceptionHandler($"{errorMessage}\n\n{toSortZipOut.ZipFilename} toSortZipOut was left open");
                }
                if (toSortCorruptOut != null)
                {
                    ReportError.UnhandledExceptionHandler($"{errorMessage}\n\n{toSortCorruptOut.ZipFilename} toSortCorruptOut was left open");
                }

            }

        }

    }
}
