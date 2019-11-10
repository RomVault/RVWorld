using System;
using System.Collections.Generic;
using System.Diagnostics;
using RVCore.RvDB;
using RVCore.Utils;
using Directory = RVIO.Directory;
using Path = RVIO.Path;

namespace RVCore.Scanner
{
    public static class FileScanning
    {
        private static Stopwatch _cacheSaveTimer;
        private static long _lastUpdateTime;
        private static ThreadWorker _thWrk;
        public static RvFile StartAt;
        public static EScanLevel EScanLevel;
        private static bool _fileErrorAbort;

        public static void ScanFiles(ThreadWorker e)
        {
#if !DEBUG
            try
            {
#endif
            _fileErrorAbort = false;
            _cacheSaveTimer = new Stopwatch();
            _cacheSaveTimer.Reset();
            if (Settings.rvSettings.CacheSaveTimerEnabled)
            {
                _cacheSaveTimer.Start();
            }

            _thWrk = e;
            if (_thWrk == null)
            {
                return;
            }



            _thWrk.Report(new bgwText("Clearing DB Status"));
            RepairStatus.ReportStatusReset(DB.DirTree);

            _thWrk.Report(new bgwText("Finding Dir's to Scan"));
            //Next get a list of all the directories to be scanned
            List<RvFile> lstDir = new List<RvFile>();
            DBHelper.GetSelectedDirListStart(ref lstDir, StartAt);


            _thWrk.Report(new bgwText("Scanning Dir's"));
            _thWrk.Report(new bgwSetRange(lstDir.Count - 1));
            //Scan the list of directories.
            for (int i = 0; i < lstDir.Count; i++)
            {
                _thWrk.Report(i);
                _thWrk.Report(new bgwText("Scanning Dir : " + lstDir[i].FullName));
                string lDir = lstDir[i].FullName;
                Console.WriteLine(lDir);
                if (Directory.Exists(lDir))
                {
                    lstDir[i].GotStatus = GotStatus.Got;
                    CheckADir(lstDir[i], true);
                }
                else
                {
                    MarkAsMissing(lstDir[i]);
                }

                if (_thWrk.CancellationPending || _fileErrorAbort)
                {
                    break;
                }
            }

            _thWrk.Report(new bgwText("Updating Cache"));
            DB.Write();


            _thWrk.Report(new bgwText("File Scan Complete"));

            _thWrk = null;
#if !DEBUG
            }
            catch (Exception exc)
            {
                ReportError.UnhandledExceptionHandler(exc);

                _thWrk?.Report(new bgwText("Updating Cache"));
                DB.Write();
                _thWrk?.Report(new bgwText("Complete"));

                _thWrk = null;
            }
#endif
        }

        /// <summary>
        /// Called from 5 places:
        /// 1: ScanFiles: main top level loop.
        /// 2: MatchFound: called after a ZIP/SevenZip is matched to an item in the DB, where the zip has either changed or never been scanned or level 3 scanning
        /// 3: MatchFound: called when an directory is matched to an item in the DB that is not from a DAT. (This is a directory not found in the main tree, as main tree dir's are processes in top level loop
        /// 4: NewFileFound: called after a new unmatched ZIP/SevenZip is found.
        /// 5: NewFileFound: called after a new unmatched DIR is found.
        /// </summary>
        /// <param name="dbDir"></param>
        /// <param name="report"></param>
        private static void CheckADir(RvFile dbDir, bool report)
        {
            if (_cacheSaveTimer.Elapsed.TotalMinutes > Settings.rvSettings.CacheSaveTimePeriod)
            {
                _thWrk.Report("Saving Cache");
                DB.Write();
                _thWrk.Report("Saving Cache Complete");
                _cacheSaveTimer.Reset();
                _cacheSaveTimer.Start();
            }

            string fullDir = dbDir.FullName;
            if (report)
            {
                _thWrk.Report(new bgwText2(fullDir));
            }

            // this is a temporary rvDir structure to store the data about the actual directory/files we are scanning
            // we will first populate this variable with the real file data from the directory, and then compare it
            // with the data in dbDir.
            RvFile fileDir = null;

            Debug.WriteLine(fullDir);

            FileType ft = dbDir.FileType;

            #region "Populate fileDir"

            // if we are scanning a ZIP file then populate scanDir from the ZIP file
            switch (ft)
            {
                case FileType.Zip:
                case FileType.SevenZip:
                    fileDir = Populate.FromAZipFile(dbDir, EScanLevel, _thWrk);
                    break;

                case FileType.Dir:
                    fileDir = Populate.FromADir(dbDir, EScanLevel,_thWrk, ref _fileErrorAbort);
                    break;
                default:
                    ReportError.SendAndShow("Un supported file type in CheckADir " + ft);
                    break;
            }

            #endregion

            if (fileDir == null)
            {
                ReportError.SendAndShow("Unknown Reading File Type in Dir Scanner");
                return;
            }

            if (report)
            {
                _thWrk.Report(new bgwSetRange2(fileDir.ChildCount - 1));

                _thWrk.Report(new bgwRange2Visible(true));
            }

            if (!DBTypeGet.isCompressedDir(ft) && _thWrk.CancellationPending)
            {
                return;
            }

            Compare(dbDir, fileDir, report, true);
        }

        private static void Compare(RvFile dbDir, RvFile fileDir, bool report, bool enableCancel)
        {
            string fullDir = dbDir.FullName;
            FileType ft = dbDir.FileType;

            // now we scan down the dbDir and the scanDir, comparing them.
            // if we find a match we mark dbDir as found.
            // if we are missing a file in fileDir we mark that file in dbDir as missing.
            // if we find extra files in fileDir we add it to dbDir and mark it as unknown.
            // we also recurse into any sub directories.
            int dbIndex = 0;
            int fileIndex = 0;


            while (dbIndex < dbDir.ChildCount || fileIndex < fileDir.ChildCount)
            {
                RvFile dbChild = null;
                RvFile fileChild = null;
                int res = 0;

                if (dbIndex < dbDir.ChildCount && fileIndex < fileDir.ChildCount)
                {
                    dbChild = dbDir.Child(dbIndex);
                    fileChild = fileDir.Child(fileIndex);
                    res = DBHelper.CompareName(dbChild, fileChild);
                }
                else if (fileIndex < fileDir.ChildCount)
                {
                    //Get any remaining filedir's
                    fileChild = fileDir.Child(fileIndex);
                    res = 1;
                }
                else if (dbIndex < dbDir.ChildCount)
                {
                    //Get any remaining dbDir's
                    dbChild = dbDir.Child(dbIndex);
                    res = -1;
                }

                if (report)
                {
                    if (fileChild != null)
                    {
                        long timenow = DateTime.Now.Ticks;
                        if (timenow - _lastUpdateTime > TimeSpan.TicksPerSecond / 10)
                        {
                            _lastUpdateTime = timenow;
                            _thWrk.Report(new bgwValue2(fileIndex));
                            _thWrk.Report(new bgwText2(Path.Combine(fullDir, fileChild.Name)));
                        }
                    }
                }

                // if this file was found in the DB
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
                        List<RvFile> files = new List<RvFile>();
                        int dbsCount = 1;
                        int filesCount = 1;

                        dbs.Add(dbChild);
                        files.Add(fileChild);

                        while (dbIndex + dbsCount < dbDir.ChildCount && DBHelper.CompareName(dbChild, dbDir.Child(dbIndex + dbsCount)) == 0)
                        {
                            dbs.Add(dbDir.Child(dbIndex + dbsCount));
                            dbsCount += 1;
                        }
                        while (fileIndex + filesCount < fileDir.ChildCount && DBHelper.CompareName(fileChild, fileDir.Child(fileIndex + filesCount)) == 0)
                        {
                            files.Add(fileDir.Child(fileIndex + filesCount));
                            filesCount += 1;
                        }

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

                                bool matched = Scanner.Compare.Phase1Test(dbs[indexdb], files[indexfile], EScanLevel, out bool matchedAlt);
                                if (!matched)
                                    continue;

                                MatchFound(dbs[indexdb], files[indexfile], matchedAlt);
                                dbs[indexdb].SearchFound = true;
                                files[indexfile].SearchFound = true;
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

                                bool matched = Scanner.Compare.Phase2Test(dbs[indexdb], files[indexfile], EScanLevel, fullDir, _thWrk, ref _fileErrorAbort, out bool matchedAlt);
                                if (!matched)
                                {
                                    continue;
                                }

                                MatchFound(dbs[indexdb], files[indexfile], matchedAlt);
                                dbs[indexdb].SearchFound = true;
                                files[indexfile].SearchFound = true;
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
                            if (NewFileFound(files[indexfile], dbDir, dbIndex))
                                dbIndex++;
                        }

                        fileIndex += filesCount;
                        break;
                    case 1:
                        if (NewFileFound(fileChild, dbDir, dbIndex))
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
                if (enableCancel && !DBTypeGet.isCompressedDir(ft) && _thWrk.CancellationPending)
                {
                    return;
                }
            }
        }

        private static void MatchFound(RvFile dbChild, RvFile fileChild, bool altMatch)
        {
            // only check a zip if the filestamp has changed, we asume it is the same if the filestamp has not changed.
            switch (dbChild.FileType)
            {
                case FileType.Zip:
                case FileType.SevenZip:
                    if (dbChild.TimeStamp != fileChild.TimeStamp || EScanLevel == EScanLevel.Level3 || EScanLevel == EScanLevel.Level2 && !Utils.IsDeepScanned(dbChild))
                    {
                        MarkAsMissing(dbChild);
                        dbChild.FileAdd(fileChild, false);
                        CheckADir(dbChild, false);
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
                        CheckADir(tDir, true);
                    }
                    if (_fileErrorAbort)
                    {
                        return;
                    }
                    dbChild.FileAdd(fileChild, false);
                    break;
                case FileType.File:
                case FileType.ZipFile:
                case FileType.SevenZipFile:
                    dbChild.FileAdd(fileChild, altMatch);
                    break;
                default:
                    throw new Exception("Unsuported file type " + dbChild.FileType);
            }
        }

        private static bool NewFileFound(RvFile fileChild, RvFile dbDir, int dbIndex)
        {
            if (fileChild == null)
            {
                ReportError.SendAndShow("Error in File Scanning Code.");
                return true;
            }

            // this could be an unknown file, or dirctory.
            // if item is a directory add the directory and call back in again

            // add the newly found item
            switch (fileChild.FileType)
            {
                case FileType.Zip:
                case FileType.SevenZip:
                    dbDir.ChildAdd(fileChild, dbIndex);
                    CheckADir(fileChild, false);
                    return true;
                case FileType.Dir:
                    dbDir.ChildAdd(fileChild, dbIndex);
                    CheckADir(fileChild, true);
                    return true;
                case FileType.File:
                    // if we have not read the files CRC in the checking code, we need to read it now.
                    if (fileChild.GotStatus != GotStatus.FileLocked)
                    {
                        if (!Utils.IsDeepScanned(fileChild))
                        {
                            Populate.FromAFile(fileChild, dbDir.FullName, EScanLevel,_thWrk, ref _fileErrorAbort);
                        }
                    }
                    dbDir.ChildAdd(fileChild, dbIndex);
                    return true;
                case FileType.ZipFile:
                    dbDir.ChildAdd(fileChild, dbIndex);
                    return true;
                case FileType.SevenZipFile:
                    if (fileChild.Name.EndsWith("/"))
                        return false;
                    dbDir.ChildAdd(fileChild, dbIndex);
                    return true;
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
                        MarkAsMissing(dbChild);
                        break;
                    case FileType.Dir:
                        RvFile tDir = dbChild;
                        if (tDir.Tree == null)
                        {
                            MarkAsMissing(tDir);
                        }
                        break;
                }
                dbIndex++;
            }
        }



        private static void MarkAsMissing(RvFile dbDir)
        {
            for (int i = 0; i < dbDir.ChildCount; i++)
            {
                RvFile dbChild = dbDir.Child(i);

                if (dbChild.FileRemove() == EFile.Delete)
                {
                    dbDir.ChildRemove(i);
                    i--;
                }
                else
                {
                    switch (dbChild.FileType)
                    {
                        case FileType.Zip:
                        case FileType.SevenZip:
                            MarkAsMissing(dbChild);
                            break;

                        case FileType.Dir:
                            RvFile tDir = dbChild;
                            if (tDir.Tree == null)
                            {
                                MarkAsMissing(tDir);
                            }
                            break;
                    }
                }
            }
        }

    }
}