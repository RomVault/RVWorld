/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RVCore.FixFile.Util;
using RVCore.RvDB;
using RVIO;

namespace RVCore.FixFile
{
    public static class Fix
    {
        public static void PerformFixes(ThreadWorker thWrk)
        {
            try
            {
                Stopwatch cacheSaveTimer = new Stopwatch();
                cacheSaveTimer.Reset();
                if (Settings.rvSettings.CacheSaveTimerEnabled)
                {
                    cacheSaveTimer.Start();
                }

                if (!Report.Set(thWrk))
                {
                    return;
                }


                Report.ReportProgress(new bgwText("Fixing Files"));

                int totalFixes = 0;
                int totalFixed = 0;
                int reportedFixed = 0;
                for (int i = 0; i < DB.DirTree.ChildCount; i++)
                {
                    RvFile tdir = DB.DirTree.Child(i);
                    totalFixes += CountFixDir(tdir, tdir.Tree.Checked == RvTreeRow.TreeSelect.Selected);
                }
                Report.ReportProgress(new bgwSetRange(totalFixes));

                List<RvFile> fileProcessQueue = new List<RvFile>();

                for (int i = 0; i < DB.DirTree.ChildCount; i++)
                {
                    RvFile tdir = DB.DirTree.Child(i);
                    ReturnCode returnCode = FixDir(tdir, tdir.Tree.Checked == RvTreeRow.TreeSelect.Selected, fileProcessQueue, ref totalFixed, ref reportedFixed, cacheSaveTimer);
                    if (returnCode != ReturnCode.Good)
                    {
                        RepairStatus.ReportStatusReset(DB.DirTree);
                        break;
                    }

                    if (Report.CancellationPending())
                    {
                        break;
                    }
                }

                Report.ReportProgress(new bgwText("Updating Cache"));
                DB.Write();
                Report.ReportProgress(new bgwText("Complete"));

                Report.Set(null);
            }
            catch (Exception exc)
            {
                ReportError.UnhandledExceptionHandler(exc);

                Report.ReportProgress(new bgwText("Updating Cache"));

                DB.Write();
                Report.ReportProgress(new bgwText("Complete"));


                Report.Set(null);
            }
        }

        private static int CountFixDir(RvFile dir, bool lastSelected)
        {
            int count = 0;

            bool thisSelected = lastSelected;
            if (dir.Tree != null)
            {
                thisSelected = dir.Tree.Checked == RvTreeRow.TreeSelect.Selected;
            }

            for (int j = 0; j < dir.ChildCount; j++)
            {
                RvFile child = dir.Child(j);

                switch (child.FileType)
                {
                    case FileType.Zip:
                    case FileType.SevenZip:
                        if (!thisSelected)
                        {
                            continue;
                        }
                        RvFile tZip = child;
                        count += tZip.DirStatus.CountCanBeFixed();

                        break;

                    case FileType.Dir:

                        count += CountFixDir(child, thisSelected);
                        break;

                    case FileType.File:
                        if (!thisSelected)
                        {
                            continue;
                        }
                        if (child.RepStatus == RepStatus.CanBeFixed)
                        {
                            count++;
                        }
                        break;
                }
            }
            return count;
        }


        private static ReturnCode FixDir(RvFile dir, bool lastSelected, List<RvFile> fileProcessQueue, ref int totalFixed, ref int reportedFixed, Stopwatch cacheSaveTimer)
        {
            //Debug.WriteLine(dir.FullName);
            bool thisSelected = lastSelected;
            if (dir.Tree != null)
            {
                thisSelected = dir.Tree.Checked == RvTreeRow.TreeSelect.Selected;
            }

            List<RvFile> lstToProcess = new List<RvFile>();
            for (int j = 0; j < dir.ChildCount; j++)
            {
                lstToProcess.Add(dir.Child(j));
            }

            foreach (RvFile child in lstToProcess)
            {
                ReturnCode returnCode = FixBase(child, thisSelected, fileProcessQueue, ref totalFixed, ref reportedFixed, cacheSaveTimer);
                if (returnCode != ReturnCode.Good)
                {
                    return returnCode;
                }

                while (fileProcessQueue.Any())
                {
                    returnCode = FixBase(fileProcessQueue[0], true, fileProcessQueue, ref totalFixed, ref reportedFixed, cacheSaveTimer);
                    if (returnCode != ReturnCode.Good)
                    {
                        return returnCode;
                    }
                    fileProcessQueue.RemoveAt(0);
                }

                if (totalFixed != reportedFixed)
                {
                    Report.ReportProgress(new bgwProgress(totalFixed));
                    reportedFixed = totalFixed;
                }
                if (Report.CancellationPending())
                {
                    break;
                }
            }
            // here we check to see if the directory we just scanned should be deleted
            FixFileUtils.CheckDeleteFile(dir);
            return ReturnCode.Good;
        }


        private static ReturnCode FixBase(RvFile child, bool thisSelected, List<RvFile> fileProcessQueue, ref int totalFixed, ref int reportedFixed, Stopwatch cacheSaveTimer)
        {
            // skip any files that have already been deleted
            if (child.RepStatus == RepStatus.Deleted)
            {
                return ReturnCode.Good;
            }

            CheckDBWrite(cacheSaveTimer);

            string errorMessage = "";
            ReturnCode returnCode = ReturnCode.LogicError;
            switch (child.FileType)
            {
                case FileType.Zip:
                case FileType.SevenZip:
                    if (!thisSelected)
                    {
                        return ReturnCode.Good;
                    }

                    if (!string.IsNullOrEmpty(child.FileName))
                    {
                        string strDir = child.Parent.FullName;
                        File.Move(Path.Combine(strDir, child.FileName), Path.Combine(strDir, child.Name));
                        child.FileName = null;
                    }

                    returnCode = FixAZip.FixZip(child, fileProcessQueue, ref totalFixed, out errorMessage);
                    break;

                case FileType.Dir:
                    if (thisSelected)
                    {
                        if (!string.IsNullOrEmpty(child.FileName))
                        {
                            string strDir = child.Parent.FullName;
                            Directory.Move(Path.Combine(strDir, child.FileName), Path.Combine(strDir, "__RomVault.tmpDir"));
                            Directory.Move(Path.Combine(strDir, "__RomVault.tmpDir"), Path.Combine(strDir, child.Name));
                            child.FileName = null;
                        }
                    }

                    returnCode = FixDir(child, thisSelected, fileProcessQueue, ref totalFixed, ref reportedFixed, cacheSaveTimer);
                    return returnCode;

                case FileType.File:
                    if (!thisSelected)
                    {
                        return ReturnCode.Good;
                    }

                    returnCode = FixAFile.FixFile(child, fileProcessQueue, ref totalFixed, out errorMessage);
                    break;
            }
            switch (returnCode)
            {
                case ReturnCode.Good:
                    // all good, move alone.
                    break;
                case ReturnCode.RescanNeeded:
                    ReportError.Show(errorMessage);
                    break;
                case ReturnCode.LogicError:
                    ReportError.UnhandledExceptionHandler(errorMessage);
                    break;
                case ReturnCode.FileSystemError:
                    ReportError.Show(errorMessage);
                    break;
                case ReturnCode.FindFixes:
                    ReportError.Show("You Need to Find Fixes before Fixing. (Incorrect File Status's found for fixing.)");
                    break;
                case ReturnCode.SourceCheckSumMismatch:
                case ReturnCode.DestinationCheckSumMismatch:
                    ReportError.Show(errorMessage);
                    break;
                case ReturnCode.SourceDataStreamCorrupt:
                    ReportError.Show(errorMessage);
                    break;
                default:
                    ReportError.UnhandledExceptionHandler("Unknown result type " + returnCode);
                    break;
            }
            return returnCode;
        }


        private static void CheckDBWrite(Stopwatch cacheSaveTimer)
        {
            if (cacheSaveTimer.Elapsed.Minutes > Settings.rvSettings.CacheSaveTimePeriod)
            {
                Report.ReportProgress("Saving Cache");
                DB.Write();
                Report.ReportProgress("Saving Cache Complete");
                cacheSaveTimer.Reset();
                cacheSaveTimer.Start();
            }
        }
    }
}