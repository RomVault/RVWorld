using System;
using System.Collections.Generic;
using System.Diagnostics;
using Compress;
using RVCore.FixFile.Util;
using RVCore.RvDB;
using RVCore.Utils;
using RVIO;

namespace RVCore.FixFile
{
    public static class FixAZip
    {
        public class ZipFileException : Exception
        {
            public ZipFileException(ReturnCode rc, string message) : base(message)
            {
                returnCode = rc;
            }

            public ReturnCode returnCode { get; }
        }

        public static ReturnCode FixZip(RvFile fixZip, List<RvFile> fileProcessQueue, ref int totalFixed, out string errorMessage)
        {
            errorMessage = "";

            //Check for error status
            if (fixZip.DirStatus.HasUnknown())
            {
                return ReturnCode.FindFixes; // Error
            }
            bool needsTrrntzipped = fixZip.ZipStatus != ZipStatus.TrrntZip && fixZip.GotStatus == GotStatus.Got && fixZip.DatStatus == DatStatus.InDatCollect && (Settings.rvSettings.FixLevel == EFixLevel.TrrntZipLevel1 || Settings.rvSettings.FixLevel == EFixLevel.TrrntZipLevel2 || Settings.rvSettings.FixLevel == EFixLevel.TrrntZipLevel3);

            // file corrupt and not in tosort
            //      if file cannot be fully fixed copy to corrupt
            //      process zipfile

            if (fixZip.GotStatus == GotStatus.Corrupt && fixZip.DatStatus != DatStatus.InToSort && !fixZip.DirStatus.HasFixable())
            {
                ReturnCode moveReturnCode = FixAZipFunctions.MoveZipToCorrupt(fixZip, out errorMessage);
                if (moveReturnCode != ReturnCode.Good)
                {
                    return moveReturnCode;
                }
            }

            // has fixable
            //      process zipfile

            else if (fixZip.DirStatus.HasFixable())
            {
                // do nothing here but continue on to process zip.
            }

            // need trrntzipped
            //      process zipfile

            else if (needsTrrntzipped)
            {
                // rv7Zip format is not finalized yet so do not use
                if (!Settings.rvSettings.ConvertToRV7Z && (fixZip.FileType == FileType.SevenZip))
                //if (fixZip.FileType == FileType.SevenZip)
                {
                    needsTrrntzipped = false;
                }
                // do nothing here but continue on to process zip.
            }


            // got empty zip that should be deleted
            //      process zipfile
            else if (fixZip.GotStatus == GotStatus.Got && fixZip.GotStatus != GotStatus.Corrupt && !fixZip.DirStatus.HasAnyFiles())
            {
                // do nothing here but continue on to process zip.
            }

            // else
            //      skip this zipfile
            else
            {
                // nothing can be done to return
                return ReturnCode.Good;
            }

            if (!fixZip.DirStatus.HasFixable() && !needsTrrntzipped)
            {
                return ReturnCode.Good;
            }

            string fixZipFullName = fixZip.TreeFullName;

            if (!fixZip.DirStatus.HasFixable() && needsTrrntzipped)
            {
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), "", 0, "TrrntZipping", "", "", ""));
            }


            FixFileUtils.CheckCreateDirectories(fixZip.Parent);
            ReportError.LogOut("");
            ReportError.LogOut(fixZipFullName + " : " + fixZip.RepStatus);
            ReportError.LogOut("------------------------------------------------------------");
            Debug.WriteLine(fixZipFullName + " : " + fixZip.RepStatus);
            ReportError.LogOut("Zip File Status Before Fix:");
            for (int intLoop = 0; intLoop < fixZip.ChildCount; intLoop++)
            {
                ReportError.LogOut(fixZip.Child(intLoop));
            }
            ReportError.LogOut("");

            ReturnCode returnCode = ReturnCode.Good;
            RepStatus fileRepStatus = RepStatus.UnSet;

            ICompress tempFixZip = null;
            ICompress toSortCorruptOut = null;
            ICompress toSortZipOut = null;
            try
            {

                RvFile toSortGame = null;
                RvFile toSortCorruptGame = null;

                List<RvFile> fixZipTemp = new List<RvFile>();

                FileType fixFileType = fixZip.FileType;

                for (int iRom = 0; iRom < fixZip.ChildCount; iRom++)
                {
                    RvFile fixZippedFile = new RvFile(DBTypeGet.FileFromDir(fixFileType));
                    fixZip.Child(iRom).CopyTo(fixZippedFile);

                    fixZipTemp.Add(fixZippedFile);

                    ReportError.LogOut(fixZippedFile.RepStatus + " : " + fixZip.Child(iRom).FullName);

                    fileRepStatus = fixZippedFile.RepStatus;
                    switch (fixZippedFile.RepStatus)
                    {
                        #region Nothing to copy

                        // any file we do not have or do not want in the destination zip
                        case RepStatus.Missing:
                        case RepStatus.NotCollected:
                        case RepStatus.Rename:
                        case RepStatus.Delete:
                            if (!
                                (
                                    // got the file in the original zip but will be deleting it
                                    fixZippedFile.DatStatus == DatStatus.NotInDat && fixZippedFile.GotStatus == GotStatus.Got ||
                                    fixZippedFile.DatStatus == DatStatus.NotInDat && fixZippedFile.GotStatus == GotStatus.Corrupt ||
                                    fixZippedFile.DatStatus == DatStatus.InDatMerged && fixZippedFile.GotStatus == GotStatus.Got ||
                                    fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Got ||
                                    fixZippedFile.DatStatus == DatStatus.InToSort && fixZippedFile.GotStatus == GotStatus.Corrupt ||

                                    // do not have this file and cannot fix it here
                                    fixZippedFile.DatStatus == DatStatus.InDatCollect && fixZippedFile.GotStatus == GotStatus.NotGot ||
                                    fixZippedFile.DatStatus == DatStatus.InDatBad && fixZippedFile.GotStatus == GotStatus.NotGot ||
                                    fixZippedFile.DatStatus == DatStatus.InDatMerged && fixZippedFile.GotStatus == GotStatus.NotGot
                                )
                            )
                            {
                                ReportError.SendAndShow("Error in Fix Rom Status " + fixZippedFile.RepStatus + " : " +
                                                        fixZippedFile.DatStatus + " : " + fixZippedFile.GotStatus);
                            }

                            if (fixZippedFile.RepStatus == RepStatus.Delete)
                            {
                                if (Settings.rvSettings.DetailedFixReporting)
                                {
                                    Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), fixZippedFile.Name, fixZippedFile.Size, "Delete", "", "", ""));
                                }

                                returnCode = FixFileUtils.DoubleCheckDelete(fixZippedFile, out errorMessage);
                                if (returnCode != ReturnCode.Good)
                                {
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortGame, ref toSortZipOut);
                                    return returnCode;
                                }
                            }

                            fixZippedFile.GotStatus = GotStatus.NotGot;
                            break;

                        #endregion


                        // any files we are just moving from the original zip to the destination zip
                        case RepStatus.Correct:
                        case RepStatus.InToSort:
                        case RepStatus.NeededForFix:
                        case RepStatus.Corrupt:
                            {
                                returnCode = FixAZipFunctions.CorrectZipFile(fixZip, fixZippedFile, ref tempFixZip, iRom,
                                out errorMessage);
                                if (returnCode != ReturnCode.Good)
                                {
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortGame, ref toSortZipOut);
                                    return returnCode;
                                }

                                break;
                            }

                        case RepStatus.CanBeFixed:
                        case RepStatus.CorruptCanBeFixed:
                            {
                                returnCode = FixAZipFunctions.CanBeFixed(fixZip, fixZippedFile, ref tempFixZip, fileProcessQueue, ref totalFixed, out errorMessage);
                                if (returnCode != ReturnCode.Good)
                                {
                                    CloseZipFile(ref tempFixZip);
                                    CloseToSortGame(toSortGame, ref toSortZipOut);
                                    CloseToSortCorruptGame(toSortGame, ref toSortZipOut);
                                    return returnCode;
                                }

                                break;
                            }

                        case RepStatus.MoveToSort:
                            FixAZipFunctions.MovetoSort(fixZip, fixZippedFile, ref toSortGame, ref toSortZipOut, iRom);
                            break;

                        case RepStatus.MoveToCorrupt:
                            FixAZipFunctions.MoveToCorrupt(fixZip, fixZippedFile, ref toSortCorruptGame,
                                ref toSortCorruptOut, iRom);
                            break;

                        default:
                            ReportError.UnhandledExceptionHandler(
                                "Unknown file status found " + fixZippedFile.RepStatus + " while fixing file " +
                                fixZip.Name + " Dat Status = " + fixZippedFile.DatStatus + " GotStatus " +
                                fixZippedFile.GotStatus);
                            break;
                    }
                }

                //if ToSort Zip Made then close the zip and add this new zip to the Database
                CloseToSortGame(toSortGame, ref toSortZipOut);

                //if Corrupt Zip Made then close the zip and add this new zip to the Database
                CloseToSortCorruptGame(toSortCorruptGame, ref toSortCorruptOut);

                #region Process original Zip

                string filename = fixZip.FullName;
                if (File.Exists(filename))
                {
                    if (!File.SetAttributes(filename, FileAttributes.Normal))
                    {
                        int error = Error.GetLastError();
                        Report.ReportProgress(new bgwShowError(filename,
                            "Error Setting File Attributes to Normal. Deleting Original Fix File. Code " + error));
                    }

                    try
                    {
                        File.Delete(filename);
                    }
                    catch (Exception e)
                    {
                        errorMessage = "Error While trying to delete file " + filename + ". " + e.Message;

                        if (tempFixZip != null && tempFixZip.ZipOpen != ZipOpenType.Closed)
                        {
                            tempFixZip.ZipFileClose();
                            tempFixZip = null;
                        }

                        return ReturnCode.RescanNeeded;
                    }
                }

                #endregion

                bool checkDelete = false;

                #region process the temp Zip rename it to the original Zip

                if (tempFixZip != null && tempFixZip.ZipOpen != ZipOpenType.Closed)
                {
                    string tempFilename = tempFixZip.ZipFilename;
                    tempFixZip.ZipFileClose();

                    if (tempFixZip.LocalFilesCount() > 0)
                    {
                        // now rename the temp fix file to the correct filename
                        File.Move(tempFilename, filename);
                        FileInfo nFile = new FileInfo(filename);
                        RvFile tmpZip = new RvFile(FileType.Zip)
                        {
                            Name = Path.GetFileName(filename),
                            TimeStamp = nFile.LastWriteTime
                        };
                        tmpZip.SetStatus(fixZip.DatStatus, GotStatus.Got);

                        fixZip.FileAdd(tmpZip, false);
                        fixZip.ZipStatus = tempFixZip.ZipStatus;
                    }
                    else
                    {
                        File.Delete(tempFilename);
                        checkDelete = true;
                    }

                    tempFixZip = null;
                }
                else
                {
                    checkDelete = true;
                }

                #endregion

                #region Now put the New Game Status information into the Database.

                int intLoopFix = 0;
                foreach (RvFile tmpZip in fixZipTemp)
                {
                    tmpZip.CopyTo(fixZip.Child(intLoopFix));

                    if (fixZip.Child(intLoopFix).RepStatus == RepStatus.Deleted)
                    {
                        if (fixZip.Child(intLoopFix).FileRemove() == EFile.Delete)
                        {
                            fixZip.ChildRemove(intLoopFix);
                            continue;
                        }
                    }

                    intLoopFix++;
                }

                #endregion

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
                tempFixZip?.ZipFileCloseFailed();
                toSortZipOut?.ZipFileCloseFailed();
                toSortCorruptOut?.ZipFileCloseFailed();
                tempFixZip = null;
                toSortZipOut = null;
                toSortCorruptOut = null;

                errorMessage = ex.Message;
                return ex.returnCode;
            }
            catch (Exception ex)
            {
                tempFixZip?.ZipFileCloseFailed();
                toSortZipOut?.ZipFileCloseFailed();
                toSortCorruptOut?.ZipFileCloseFailed();
                tempFixZip = null;
                toSortZipOut = null;
                toSortCorruptOut = null;

                errorMessage = ex.Message;
                return ReturnCode.LogicError;
            }
            finally
            {
                if (tempFixZip != null)
                {
                    ReportError.UnhandledExceptionHandler($"{tempFixZip.ZipFilename} tempZipOut was left open, ZipFile= {fixZipFullName} , fileRepStatus= {fileRepStatus} , returnCode= {returnCode}");
                }
                if (toSortZipOut != null)
                {
                    ReportError.UnhandledExceptionHandler($"{toSortZipOut.ZipFilename} toSortZipOut was left open");
                }
                if (toSortCorruptOut != null)
                {
                    ReportError.UnhandledExceptionHandler($"{toSortCorruptOut.ZipFilename} toSortCorruptOut was left open");
                }

            }

        }

        private static void CloseZipFile(ref ICompress tempFixZip)
        {
            tempFixZip.ZipFileCloseFailed();
            tempFixZip = null;
        }

        private static void CloseToSortGame(RvFile toSortGame, ref ICompress toSortZipOut)
        {
            if (toSortGame != null)
            {
                toSortZipOut.ZipFileClose();

                toSortGame.TimeStamp = toSortZipOut.TimeStamp;
                toSortGame.DatStatus = DatStatus.InToSort;
                toSortGame.GotStatus = GotStatus.Got;
                toSortGame.ZipStatus = toSortZipOut.ZipStatus;
                toSortZipOut = null;

                RvFile toSort = toSortGame.Parent;
                toSort.ChildAdd(toSortGame);
            }

        }

        private static void CloseToSortCorruptGame(RvFile toSortCorruptGame, ref ICompress toSortCorruptOut)
        {
            if (toSortCorruptGame != null)
            {
                toSortCorruptOut.ZipFileClose();

                toSortCorruptGame.TimeStamp = toSortCorruptOut.TimeStamp;
                toSortCorruptGame.DatStatus = DatStatus.InToSort;
                toSortCorruptGame.GotStatus = GotStatus.Got;

                RvFile toSort = DB.RvFileToSort();
                RvFile corruptDir = new RvFile(FileType.Dir) { Name = "Corrupt", DatStatus = DatStatus.InToSort };
                int found = toSort.ChildNameSearch(corruptDir, out int indexCorrupt);
                if (found != 0)
                {
                    corruptDir.GotStatus = GotStatus.Got;
                    indexCorrupt = toSort.ChildAdd(corruptDir);
                }

                toSort.Child(indexCorrupt).ChildAdd(toSortCorruptGame);
                toSortCorruptOut = null;
            }
        }

    }
}
