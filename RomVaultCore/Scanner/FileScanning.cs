/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CHDSharpLib;
using FileScanner;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using Directory = RVIO.Directory;

namespace RomVaultCore.Scanner
{
    public static partial class FileScanning
    {
        private static Stopwatch _cacheSaveTimer;
        private static ThreadWorker _thWrk;
        public static RvFile StartAt;
        public static EScanLevel EScanLevel;
        private static bool _fileErrorAbort;
        private static bool _deferParentedChds;
        private static readonly List<DeferredChdScan> DeferredChdScans = new List<DeferredChdScan>();
        private static readonly List<string> ChdParentSearchRoots = new List<string>();

        public static void ScanFiles(ThreadWorker thWrk)
        {
#if !DEBUG
            try
            {
#endif
                _fileErrorAbort = false;
                _deferParentedChds = true;
                DeferredChdScans.Clear();
                ChdParentSearchRoots.Clear();
                ChdParentResolver.Reset();
                _cacheSaveTimer = new Stopwatch();
                _cacheSaveTimer.Reset();
                if (Settings.rvSettings.CacheSaveTimerEnabled)
                {
                    _cacheSaveTimer.Start();
                }

                _thWrk = thWrk;
                if (_thWrk == null)
                {
                    _cacheSaveTimer?.Stop();
                    _cacheSaveTimer = null;
                    _deferParentedChds = false;
                    DeferredChdScans.Clear();
                    ChdParentSearchRoots.Clear();
                    return;
                }


                _thWrk.Report(new bgwText("Clearing DB Status"));
                RepairStatus.ReportStatusReset(DB.DirRoot);

                _thWrk.Report(new bgwText("Finding Dir's to Scan"));
                //Next get a list of all the directories to be scanned
                List<RvFile> lstDir = new List<RvFile>();
                DBHelper.GetSelectedDirListStart(ref lstDir, StartAt);


                _thWrk.Report(new bgwText("Scanning Dir's"));
                _thWrk.Report(new bgwSetRange(lstDir.Count));
                //Scan the list of directories.
                for (int i = 0; i < lstDir.Count; i++)
                {
                    _thWrk.Report(i + 1);
                    _thWrk.Report(new bgwText("Scanning Dir : " + lstDir[i].FullName));
                    string lDir = lstDir[i].FullName;
                    if (Directory.Exists(lDir))
                    {
                        AddParentSearchRoot(lDir);
                        lstDir[i].GotStatus = GotStatus.Got;
                        CheckADir(lstDir[i], null);
                    }
                    else
                    {
                        lstDir[i].MarkAsMissing();
                    }

                    if (_thWrk.CancellationPending || _fileErrorAbort)
                    {
                        break;
                    }
                }

                ProcessDeferredChdScans();

                _thWrk.Report(new bgwText("Updating Cache"));
                DB.Write();


                _thWrk.Report(new bgwText("File Scan Complete"));
                _thWrk.Finished = true;
                _cacheSaveTimer?.Stop();
                _cacheSaveTimer = null;
                _thWrk = null;
                _deferParentedChds = false;
                DeferredChdScans.Clear();
                ChdParentSearchRoots.Clear();
#if !DEBUG
            }
            catch (Exception exc)
            {
                ReportError.UnhandledExceptionHandler(exc);

                _thWrk?.Report(new bgwText("Updating Cache"));
                DB.Write();
                _thWrk?.Report(new bgwText("Complete"));
                if (_thWrk != null) _thWrk.Finished = true;
                _thWrk = null;
                _deferParentedChds = false;
                DeferredChdScans.Clear();
                ChdParentSearchRoots.Clear();
                _cacheSaveTimer?.Stop();
                _cacheSaveTimer = null;
            }
#endif
        }


        public static void CheckAnArchive(RvFile dbDir, bool report, int? checkIndex)
        {
            FileType ft = dbDir.FileType;
            if (!(ft == FileType.Zip || ft == FileType.SevenZip || ft == FileType.CHD))
            {
                ReportError.SendAndShow("Un supported file type in CheckADir " + ft);
                return;
            }

            if (_cacheSaveTimer != null && _cacheSaveTimer.Elapsed.TotalMinutes > Settings.rvSettings.CacheSaveTimePeriod)
            {
                _thWrk?.Report("Saving Cache");
                DB.Write();
                _thWrk?.Report("Saving Cache Complete");
                _cacheSaveTimer.Reset();
                _cacheSaveTimer.Start();
            }

            if (checkIndex != null)
                _thWrk?.Report(new bgwValue2((int)checkIndex));
            _thWrk?.Report(new bgwText2(dbDir.FullName));

            string parentPath = "";
            if (ft == FileType.CHD)
            {
                string chdPath = ResolvePhysicalPath(dbDir);
                if (ChdParentResolver.TryRegister(chdPath, out ChdContainerInfo info, out _) && info.RequiresParent)
                {
                    if (_deferParentedChds)
                    {
                        QueueDeferredChdScan(dbDir, report, checkIndex, chdPath, info.ParentSha1);
                        return;
                    }
                    if (!ChdParentResolver.TryResolveParent(chdPath, out parentPath, out string parentError))
                    {
                        MarkChdWaitingForParent(dbDir, chdPath, parentError);
                        return;
                    }
                }
            }

            CheckAnArchiveCore(dbDir, report, checkIndex, parentPath);
        }

        private static void CheckAnArchiveCore(RvFile dbDir, bool report, int? checkIndex, string parentPath)
        {
            if (dbDir.FileType == FileType.CHD && NeedsChdParentRetry(dbDir))
                dbDir.ChdStatus = null;
            ScannedFile fileArchive = Populate.FromAZipFileArchive(dbDir, EScanLevel, _thWrk, parentPath);
            if (fileArchive == null)
            {
                dbDir.MarkAsMissing();
                if (dbDir.FileType == FileType.CHD && !string.IsNullOrWhiteSpace(parentPath))
                    dbDir.ChdStatus = "Parent resolved, but CHD extraction failed; retry is required.";
                return;
            }

            if (report)
            {
                _thWrk.Report(new bgwSetRange2(fileArchive.Count));
                _thWrk.Report(new bgwRange2Visible(true));
            }
            dbDir.MergeInArchive(fileArchive);
            if (dbDir.FileType == FileType.CHD)
            {
                // FileMergeIn intentionally preserves existing diagnostics. A
                // deferred child, however, carries a temporary waiting status
                // which must be replaced by the result of the successful scan.
                dbDir.ChdStatus = fileArchive.ChdStatus;
                dbDir.ChdScanMethod = fileArchive.ChdScanMethod;
                dbDir.ChdHashMatchMode = fileArchive.ChdHashMatchMode;
                dbDir.ChdDescriptorMatch = fileArchive.ChdDescriptorMatch;
            }
        }

        private static void QueueDeferredChdScan(RvFile dbDir, bool report, int? checkIndex, string path, byte[] parentSha1)
        {
            for (int i = 0; i < DeferredChdScans.Count; i++)
            {
                if (ReferenceEquals(DeferredChdScans[i].DbDir, dbDir))
                    return;
            }
            dbDir.ChdStatus = "Waiting for parent SHA1 " + ChdParentResolver.Hex(parentSha1);
            DeferredChdScans.Add(new DeferredChdScan(dbDir, report, checkIndex, path));
        }

        private static void ProcessDeferredChdScans()
        {
            _deferParentedChds = false;
            if (DeferredChdScans.Count == 0)
                return;

            _thWrk?.Report(new bgwText("Resolving parented CHDs"));
            RegisterPhysicalParentSearchRoots();
            RegisterKnownChds(DB.DirRoot);
            ChdParentResolver.RegisterConfiguredSearchPaths();

            for (int i = 0; i < DeferredChdScans.Count; i++)
            {
                if (_thWrk?.CancellationPending == true || _fileErrorAbort)
                    break;

                DeferredChdScan pending = DeferredChdScans[i];
                if (!ChdParentResolver.TryResolveParent(pending.Path, out string parentPath, out string error))
                {
                    MarkChdWaitingForParent(pending.DbDir, pending.Path, error);
                    continue;
                }

                _thWrk?.Report(new bgwText("Scanning parented CHD : " + pending.DbDir.FullName));
                CheckAnArchiveCore(pending.DbDir, pending.Report, pending.CheckIndex, parentPath);
            }
            DeferredChdScans.Clear();
        }

        private static void RegisterKnownChds(RvFile node)
        {
            if (node == null)
                return;
            if (node.FileType == FileType.CHD)
            {
                string path = ResolvePhysicalPath(node);
                if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                    ChdParentResolver.TryRegister(path, out _, out _);
            }
            if (!node.IsDirectory)
                return;
            for (int i = 0; i < node.ChildCount; i++)
                RegisterKnownChds(node.Child(i));
        }

        private static void MarkChdWaitingForParent(RvFile dbDir, string path, string error)
        {
            if (dbDir == null)
                return;
            dbDir.MarkAsMissing();
            dbDir.GotStatus = GotStatus.Got;
            dbDir.ChdStatus = "Parent required: " + error;
            _thWrk?.Report(new bgwShowError(path, dbDir.ChdStatus));
        }

        private static bool NeedsChdParentRetry(RvFile file)
        {
            if (file?.FileType != FileType.CHD)
                return false;
            if (file.GotStatus == GotStatus.Corrupt || string.IsNullOrWhiteSpace(file.ChdScanMethod))
                return true;
            string status = file.ChdStatus ?? "";
            return status.StartsWith("Waiting for parent", StringComparison.OrdinalIgnoreCase) ||
                   status.StartsWith("Parent required", StringComparison.OrdinalIgnoreCase) ||
                   status.StartsWith("Parent resolved", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddParentSearchRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            string fullPath;
            try { fullPath = System.IO.Path.GetFullPath(path); }
            catch { fullPath = path; }
            if (!ChdParentSearchRoots.Exists(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase)))
                ChdParentSearchRoots.Add(fullPath);
        }

        private static void RegisterPhysicalParentSearchRoots()
        {
            for (int i = 0; i < ChdParentSearchRoots.Count; i++)
                ChdParentResolver.RegisterDirectory(ChdParentSearchRoots[i], true);

            RvFile root = DB.DirRoot;
            for (int i = 0; root != null && i < root.ChildCount; i++)
            {
                string physicalRoot = root.Child(i)?.FullName;
                if (!string.IsNullOrWhiteSpace(physicalRoot) && System.IO.Directory.Exists(physicalRoot))
                    ChdParentResolver.RegisterDirectory(physicalRoot, true);
            }
        }

        private static string ResolvePhysicalPath(RvFile file)
        {
            string path = file?.FullNameCase;
            if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                return path;
            return file?.FullName ?? path ?? "";
        }

        private sealed class DeferredChdScan
        {
            public DeferredChdScan(RvFile dbDir, bool report, int? checkIndex, string path)
            {
                DbDir = dbDir;
                Report = report;
                CheckIndex = checkIndex;
                Path = path;
            }

            public RvFile DbDir { get; }
            public bool Report { get; }
            public int? CheckIndex { get; }
            public string Path { get; }
        }

        /// <summary>
        /// Called from 3 places:
        /// 1: ScanFiles: main top level loop.
        /// 2: MatchFound: called when an directory is matched to an item in the DB that is not from a DAT. (This is a directory not found in the main tree, as main tree dir's are processes in top level loop
        /// 3: NewFileFound: called after a new unmatched DIR is found.
        /// </summary>
        /// <param name="dbDir"></param>
        /// <param name="report"></param>
        private static void CheckADir(RvFile dbDir, int? checkIndex)
        {
            if (dbDir.FileType != FileType.Dir)
            {
                ReportError.SendAndShow("Un supported file type in CheckADir " + dbDir.FileType);
                return;
            }

            if (_cacheSaveTimer != null && _cacheSaveTimer.Elapsed.TotalMinutes > Settings.rvSettings.CacheSaveTimePeriod)
            {
                _thWrk?.Report("Saving Cache");
                DB.Write();
                _thWrk?.Report("Saving Cache Complete");
                _cacheSaveTimer.Reset();
                _cacheSaveTimer.Start();
            }

            if (checkIndex != null)
                _thWrk?.Report(new bgwValue2((int)checkIndex));
            _thWrk?.Report(new bgwText2(dbDir.FullName));

            // this is a ScannedITem structure to store the data about the actual directory/files we are scanning
            // we will first populate this variable with the real file data from the directory, and then compare it
            // with the data in dbDir.
            ScannedFile fileDir = Populate.FromADir(dbDir, EScanLevel, _thWrk, checkIndex, ref _fileErrorAbort);

            if (fileDir == null)
            {
                ReportError.SendAndShow("Unknown Reading File Type in Dir Scanner");
                return;
            }

            _thWrk.Report(new bgwSetRange2(fileDir.Count));
            _thWrk.Report(new bgwRange2Visible(true));

            if (_thWrk.CancellationPending)
            {
                return;
            }

            fileDir.Sort();

            // now we scan down the dbDir and the fileDir, comparing them.
            // if we find a match we mark dbDir as found.
            // if we are missing a file in fileDir we mark that file in dbDir as missing.
            // if we find extra files in fileDir we add it to dbDir and mark it as unknown.
            // we also recurse into any sub directories.
            int dbIndex = 0;
            int fileIndex = 0;


            while (true)
            {
                RvFile dbChild = null;
                ScannedFile fileChild = null;
                int res = 0;

                if (dbIndex < dbDir.ChildCount && fileIndex < fileDir.Count)
                {
                    dbChild = dbDir.Child(dbIndex);
                    fileChild = fileDir[fileIndex];
                    res = RVSorters.CompareName(dbChild, fileChild);
                }
                else if (fileIndex < fileDir.Count)
                {
                    //Get any remaining filedir's
                    fileChild = fileDir[fileIndex];
                    res = 1;
                }
                else if (dbIndex < dbDir.ChildCount)
                {
                    //Get any remaining dbDir's
                    dbChild = dbDir.Child(dbIndex);
                    res = -1;
                }
                else
                    break;


                switch (res)
                {
                    case 0:

                        if (dbChild == null || fileChild == null)
                        {
                            ReportError.SendAndShow("Error in File Scanning Code.");
                            break;
                        }

                        //Complete MultiName Compare
                        List<RvFile> dbs = new List<RvFile>();
                        List<ScannedFile> files = new List<ScannedFile>();
                        int dbsCount = 1;
                        int filesCount = 1;

                        dbs.Add(dbChild);
                        files.Add(fileChild);

                        while (dbIndex + dbsCount < dbDir.ChildCount && RVSorters.CompareName(dbChild, dbDir.Child(dbIndex + dbsCount)) == 0)
                        {
                            dbs.Add(dbDir.Child(dbIndex + dbsCount));
                            dbsCount++;
                        }
                        while (fileIndex + filesCount < fileDir.Count && RVSorters.CompareName(fileChild, fileDir[fileIndex + filesCount]) == 0)
                        {
                            files.Add(fileDir[fileIndex + filesCount]);
                            filesCount++;
                        }

                        // should make the SearchFound values stored in a local array here.

                        bool caseTest = files.Count > 1;
                        // if we only have one file, we don't need to test twice.
                        // so we need to do a case sensitive match first and then a case insensitive match
                        // indexCase=0 means do full case filename test
                        // indexCase=1 means do case insensitive test
                        for (int indexCase = (caseTest ? 0 : 1); indexCase < 2; indexCase += 1)
                        {
                            for (int indexfile = 0; indexfile < filesCount; indexfile++)
                            {
                                if (files[indexfile].SearchFound)
                                {
                                    continue;
                                }

                                for (int indexdb = 0; indexdb < dbsCount; indexdb++)
                                {
                                    if (dbs[indexdb].SearchFound)
                                    {
                                        continue;
                                    }

                                    bool matched = FileCompare.Phase1Test(dbs[indexdb], files[indexfile], EScanLevel, indexCase, out bool matchedAlt);
                                    if (!matched)
                                        continue;

                                    if (MatchFound(dbs[indexdb], files[indexfile], matchedAlt, fileIndex))
                                    {
                                        if (checkIndex != null)
                                            _thWrk?.Report(new bgwValue2((int)checkIndex));
                                        _thWrk?.Report(new bgwText2(dbDir.FullName));
                                        _thWrk.Report(new bgwSetRange2(fileDir.Count));
                                        _thWrk.Report(new bgwRange2Visible(true));
                                    }
                                    dbs[indexdb].SearchFound = true;
                                    files[indexfile].SearchFound = true;
                                    break;
                                }

                                if (files[indexfile].SearchFound)
                                {
                                    continue;
                                }

                                for (int indexdb = 0; indexdb < dbsCount; indexdb++)
                                {
                                    if (dbs[indexdb].SearchFound)
                                    {
                                        continue;
                                    }

                                    bool matched = FileCompare.Phase2Test(dbs[indexdb], files[indexfile], EScanLevel, indexCase, dbDir.FullNameCase, _thWrk, fileIndex, ref _fileErrorAbort, out bool matchedAlt);
                                    if (!matched)
                                    {
                                        continue;
                                    }

                                    if (MatchFound(dbs[indexdb], files[indexfile], matchedAlt, fileIndex))
                                    {
                                        if (checkIndex != null)
                                            _thWrk?.Report(new bgwValue2((int)checkIndex));
                                        _thWrk?.Report(new bgwText2(dbDir.FullName));
                                        _thWrk.Report(new bgwSetRange2(fileDir.Count));
                                        _thWrk.Report(new bgwRange2Visible(true));
                                    }
                                    dbs[indexdb].SearchFound = true;
                                    files[indexfile].SearchFound = true;
                                    break;
                                }
                            }
                        }

                        for (int indexdb = 0; indexdb < dbsCount; indexdb++)
                        {
                            if (dbs[indexdb].SearchFound)
                            {
                                dbIndex++;
                                continue;
                            }
                            DBFileNotFound(dbs[indexdb], dbDir, ref dbIndex);
                        }

                        for (int indexfile = 0; indexfile < filesCount; indexfile++)
                        {
                            if (files[indexfile].SearchFound)
                            {
                                continue;
                            }
                            if (NewFileFound(files[indexfile], dbDir, dbIndex, fileIndex))
                            {
                                if (checkIndex != null)
                                    _thWrk?.Report(new bgwValue2((int)checkIndex));
                                _thWrk?.Report(new bgwText2(dbDir.FullName));
                                _thWrk.Report(new bgwSetRange2(fileDir.Count));
                                _thWrk.Report(new bgwRange2Visible(true));
                            }

                            dbIndex++;
                        }

                        fileIndex += filesCount;
                        break;
                    case 1:
                        if (NewFileFound(fileChild, dbDir, dbIndex, fileIndex))
                        {
                            if (checkIndex != null)
                                _thWrk?.Report(new bgwValue2((int)checkIndex));
                            _thWrk?.Report(new bgwText2(dbDir.FullName));
                            _thWrk.Report(new bgwSetRange2(fileDir.Count));
                            _thWrk.Report(new bgwRange2Visible(true));
                        }
                        dbIndex++;
                        fileIndex++;
                        break;
                    case -1:
                        DBFileNotFound(dbChild, dbDir, ref dbIndex);
                        break;
                }

                if (_fileErrorAbort)
                {
                    return;
                }
                if (_thWrk.CancellationPending)
                {
                    return;
                }
            }
        }

        private static bool MatchFound(RvFile dbChild, ScannedFile fileChild, bool altMatch, int fileIndex)
        {
            bool retDirFound = false;
            // only check a zip if the filestamp has changed, we asume it is the same if the filestamp has not changed.
            switch (dbChild.FileType)
            {
                case FileType.Zip:
                case FileType.SevenZip:
                case FileType.CHD:
                    if (dbChild.FileModTimeStamp != fileChild.FileModTimeStamp ||
                        EScanLevel == EScanLevel.Level3 ||
                        EScanLevel == EScanLevel.Level2 && (!dbChild.IsDeepScanned || NeedsChdParentRetry(dbChild)))
                    {
                        dbChild.MarkAsMissing();
                        dbChild.FileMergeIn(fileChild, false);
                        CheckAnArchive(dbChild, false, fileIndex);
                    }
                    else
                    // this is still needed incase the filenames case (upper/lower characters) have changed, but nothing else
                    {
                        dbChild.FileCheckName(fileChild);
                    }
                    break;
                case FileType.Dir:
                    RvFile tDir = dbChild;
                    if (tDir.Tree == null) // do not recurse into directories that are in the tree, as they are processed by the top level code.
                    {
                        CheckADir(tDir, fileIndex);
                        retDirFound = true;
                    }
                    if (_fileErrorAbort)
                        return true;

                    dbChild.FileMergeIn(fileChild, false);
                    break;
                case FileType.File:
                    //case FileType.ZipFile:
                    //case FileType.SevenZipFile:
                    dbChild.FileMergeIn(fileChild, altMatch);
                    break;
                default:
                    throw new Exception("Unsuported file type " + dbChild.FileType);
            }

            return retDirFound;
        }

        private static bool NewFileFound(ScannedFile fileChild, RvFile dbDir, int dbIndex, int fileIndex)
        {
            if (fileChild == null)
            {
                ReportError.SendAndShow("Error in File Scanning Code.");
                return false;
            }

            // this could be an unknown file, or dirctory.
            // if item is a directory add the directory and call back in again

            // add the newly found item
            switch (fileChild.FileType)
            {
                case FileType.Zip:
                case FileType.SevenZip:
                case FileType.CHD:
                    {
                        RvFile newChild = dbDir.FileAdd(fileChild, dbIndex);
                        CheckAnArchive(newChild, false, fileIndex);
                        return false;
                    }
                case FileType.Dir:
                    {
                        RvFile newChild = dbDir.FileAdd(fileChild, dbIndex);
                        CheckADir(newChild, fileIndex);
                        return true;
                    }
                case FileType.File:
                    {
                        // if we have not read the files CRC in the checking code, we need to read it now.
                        if (fileChild.GotStatus != GotStatus.FileLocked)
                        {
                            if (!fileChild.DeepScanned)
                            {
                                _thWrk.Report(new bgwValue2((int)fileIndex));
                                _thWrk.Report(new bgwText2(fileChild.Name));
                                Populate.FromAFile(fileChild, dbDir.FullNameCase, EScanLevel, _thWrk, ref _fileErrorAbort);
                            }
                        }
                        dbDir.FileAdd(fileChild, dbIndex);
                        return false;
                    }
                default:
                    throw new Exception("Unsuported file type " + fileChild.FileType);
            }
        }

        private static void DBFileNotFound(RvFile dbChild, RvFile dbDir, ref int dbIndex)
        {
            if (dbChild == null)
            {
                ReportError.SendAndShow("Error in File Scanning Code.");
                return;
            }

            if (dbChild.FileRemove() == EFile.Delete)
            {
                dbDir.ChildRemove(dbIndex);
            }
            else
            {
                switch (dbChild.FileType)
                {
                    case FileType.Zip:
                    case FileType.SevenZip:
                    case FileType.CHD:
                        dbChild.MarkAsMissing();
                        break;
                    case FileType.Dir:
                        RvFile tDir = dbChild;
                        if (tDir.Tree == null)
                            tDir.MarkAsMissing();

                        break;
                }
                dbIndex++;
            }
        }
    }
}
