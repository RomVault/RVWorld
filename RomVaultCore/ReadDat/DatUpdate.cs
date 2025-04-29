/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using DATReader.DatStore;
using RomVaultCore.RvDB;
using RomVaultCore.Storage.Dat;
using RVIO;

namespace RomVaultCore.ReadDat
{
    public static partial class DatUpdate
    {
        private static int _datCount;
        private static int _datsProcessed;
        private static ThreadWorker _thWrk;


        private static void ShowDat(string message, string filename)
        {
            _thWrk.Report(new bgwShowError(filename, message));
        }

        public static void RetMessage(string filename, string message)
        {
            _thWrk.Report(new bgwShowError(filename, message));
        }

        public static void UpdateDat(ThreadWorker thWrk)
        {
            try
            {
                _thWrk = thWrk;
                if (_thWrk == null)
                {
                    return;
                }

                _thWrk.Report(new bgwText("Clearing DB Status"));
                RepairStatus.ReportStatusReset(DB.DirRoot);

                _datCount = 0;

                DatTreeStatusStore treeStore = new DatTreeStatusStore();
                treeStore.PreStoreTreeValue(DB.DirRoot.Child(0));

                _thWrk.Report(new bgwText("Finding Dats"));

                // build a datRoot tree of the DAT's in DatRoot, and count how many dats are found
                if (!DatImportDir.RecursiveDatTree(out DatImportDir datRoot, out _datCount))
                {
                    _thWrk.Report(new bgwText("Dat Update Complete"));
                    _thWrk.Finished = true;
                    _thWrk = null;
                    return;
                }

                _thWrk.Report(new bgwText("Scanning Dats"));
                _datsProcessed = 0;

                // now compare the database DAT's with datRoot removing any old DAT's
                RemoveOldDats(DB.DirRoot.Child(0), datRoot);

                // next clean up the File status removing any old DAT's
                RemoveOldDatsCleanUpFiles(DB.DirRoot.Child(0));

                _thWrk.Report(new bgwSetRange(_datCount - 1));

                // next add in new DAT and update the files
                UpdateDirs(DB.DirRoot.Child(0), datRoot);

                // finally remove any unneeded directories from the TreeView
                RemoveOldTree(DB.DirRoot.Child(0));

                // setBackTreeValues
                treeStore.SetBackTreeValues(DB.DirRoot.Child(0), true);

                _thWrk.Report(new bgwText("Updating Cache"));
                DB.Write();

                _thWrk.Report(new bgwText("Garbage Collecting"));
                GC.Collect();

                _thWrk.Report(new bgwText("Dat Update Complete"));
                _thWrk.Finished = true;
                _thWrk = null;
            }
            catch (Exception exc)
            {
                ReportError.UnhandledExceptionHandler(exc);

                _thWrk?.Report(new bgwText("Updating Cache"));
                DB.Write();
                _thWrk?.Report(new bgwText("Complete"));

                if (_thWrk != null) _thWrk.Finished = true;
                _thWrk = null;
            }
        }




        private static void RemoveOldDats(RvFile dbDir, DatImportDir datDir)
        {
            RvFile lDir = dbDir;
            if (!lDir.IsDirectory)
            {
                return;
            }

            // now compare the old and new dats removing any old dats
            // in the current directory
            #region check and remove DATs

            int dbDirDatCount = lDir.DirDatCount;
            int datDirDatCount = datDir?.DatFilesCount ?? 0;
            int dbDirIndex = 0;
            int datDirIndex = 0;

            while (true)
            {
                RvDat dbDat = null;
                int res;

                if (dbDirIndex < dbDirDatCount && datDirIndex < datDirDatCount)
                {
                    dbDat = lDir.DirDat(dbDirIndex);
                    DatImportDat fileDat = datDir.DirDat(datDirIndex);
                    //TODO check this
                    res = DBHelper.DatCompare(dbDat, fileDat);
                }
                else if (datDirIndex < datDirDatCount)
                {
                    //this is a new dat that we have now found at the end of the list
                    //fileDat = tmpDir.DirDat(scanIndex);
                    res = 1;
                }
                else if (dbDirIndex < dbDirDatCount)
                {
                    dbDat = lDir.DirDat(dbDirIndex);
                    res = -1;
                }
                else
                    break;

                switch (res)
                {
                    case 0:
                        if (dbDat != null)
                            dbDat.Status = DatUpdateStatus.Correct;
                        dbDirIndex++;
                        datDirIndex++;
                        break;

                    case 1:
                        // this is a new dat that we will add next time around
                        datDirIndex++;
                        break;
                    case -1:
                        if (dbDat != null)
                            dbDat.Status = DatUpdateStatus.Delete;
                        dbDirDatCount = lDir.DirDatRemove(dbDirIndex);
                        break;
                }
            }

            #endregion

            // now scan the child directory structure of this directory
            #region compare all directories
            int dbDirChildCount = lDir.ChildCount;
            int datDirChildount = datDir?.ChildDirsCount ?? 0;
            dbDirIndex = 0;
            datDirIndex = 0;

            while (true)
            {
                RvFile dbChild = null;
                DatImportDir fileChild = null;
                int res;

                if (dbDirIndex < dbDirChildCount && datDirIndex < datDirChildount)
                {
                    dbChild = lDir.Child(dbDirIndex);
                    fileChild = datDir.ChildDir(datDirIndex);
                    //TODO check this
                    res = RVSorters.CompareDirName(dbChild, fileChild);
                }
                else if (datDirIndex < datDirChildount)
                {
                    //found a new directory on the end of the list
                    //fileChild = tmpDir.Child(scanIndex);
                    res = 1;
                }
                else if (dbDirIndex < dbDirChildCount)
                {
                    dbChild = lDir.Child(dbDirIndex);
                    res = -1;
                }
                else
                    break;

                switch (res)
                {
                    case 0:
                        // found a matching directory in DatRoot So recurse back into it
                        RemoveOldDats(dbChild, fileChild);
                        dbDirIndex++;
                        datDirIndex++;
                        break;

                    case 1:
                        // found a new directory will be added later
                        datDirIndex++;
                        break;
                    case -1:
                        if (dbChild?.FileType == FileType.Dir && dbChild.Dat == null)
                        {
                            RemoveOldDats(dbChild, null);
                        }
                        dbDirIndex++;
                        break;
                }
            }
            #endregion
        }



        private static EFile RemoveOldDatsCleanUpFiles(RvFile dbDir)
        {
            if (dbDir.Dat != null)
            {
                if (dbDir.Dat.Status == DatUpdateStatus.Correct)
                {
                    return EFile.Keep;
                }

                if (dbDir.Dat.Status == DatUpdateStatus.Delete)
                {
                    if (dbDir.DatRemove() == EFile.Delete)
                    {
                        return EFile.Delete; //delete
                    }
                }
            }

            FileType ft = dbDir.FileType;
            // if we are checking a dir or zip recurse into it.
            if (ft != FileType.Zip && ft != FileType.Dir && ft != FileType.SevenZip)
            {
                return EFile.Keep;
            }

            RvFile tDir = dbDir;
            if (!tDir.IsDirectory)
                return EFile.Delete;

            // remove all DATStatus's here they will get set back correctly when adding dats back in below.
            dbDir.DatStatus = DatStatus.NotInDat;

            for (int i = 0; i < tDir.ChildCount; i++)
            {
                if (RemoveOldDatsCleanUpFiles(tDir.Child(i)) == EFile.Keep)
                {
                    continue;
                }
                tDir.ChildRemove(i);
                i--;
            }
            if ((ft == FileType.Zip || ft == FileType.SevenZip) && dbDir.GotStatus == GotStatus.Corrupt)
            {
                return EFile.Keep;
            }

            // if this directory is now empty it should be deleted
            return tDir.ChildCount == 0 ? EFile.Delete : EFile.Keep;
        }




        private static void UpdateDirs(RvFile dbDir, DatImportDir datDir)
        {
            AddTheDatsInThisDir(dbDir, datDir); // this checks / adds any DAT found at this level in the tree.

            int dbDirChildCount = dbDir.ChildCount;
            int datDirChildCount = datDir.ChildDirsCount;

            int dbDirIndex = 0;
            int datDirIndex = 0;

            dbDir.DatStatus = DatStatus.InDatCollect;

            // if everything else is correct, I don't think this check is needed.
            if (dbDir.Tree == null)
            {
                Debug.WriteLine("Adding Tree View to " + dbDir.Name);
                dbDir.Tree = new RvTreeRow();
            }

            while (true)
            {
                RvFile dbChild = null;
                DatImportDir fileChild = null;
                int res;

                if (dbDirIndex < dbDirChildCount && datDirIndex < datDirChildCount)
                {
                    dbChild = dbDir.Child(dbDirIndex);
                    fileChild = datDir.ChildDir(datDirIndex);
                    //TODO check this
                    res = RVSorters.CompareDirName(dbChild, fileChild);
                    Debug.WriteLine("Checking " + dbChild.Name + " : and " + fileChild.Name + " : " + res);
                }
                else if (datDirIndex < datDirChildCount)
                {
                    fileChild = datDir.ChildDir(datDirIndex);
                    res = 1;
                    Debug.WriteLine("Checking : and " + fileChild.Name + " : " + res);
                }
                else if (dbDirIndex < dbDirChildCount)
                {
                    dbChild = dbDir.Child(dbDirIndex);
                    res = -1;
                }
                else
                    break;

                switch (res)
                {
                    case 0:
                        // found a matching directory in DatRoot So recurse back into it
                        if (dbChild.DatStatus == DatStatus.InDatCollect)
                        {
                            ReportError.Show($"DAT directory conflict: An auto-created virtual directory is using the same name as a real directory. Resolve the conflict and refresh DATs:\n\n{dbChild.DatTreeFullName}");
                        }
                        else
                        {
                            if (dbChild.GotStatus == GotStatus.Got)
                            {
                                if (dbChild.Name != fileChild.Name) // check if the case of the Item in the DB is different from the Dat Root Actual filename
                                {
                                    if (!string.IsNullOrEmpty(dbChild.FileName)) // if we do not already have a different case name stored
                                    {
                                        dbChild.FileName = dbChild.Name; // copy the DB filename to the FileName
                                    }
                                    else // We already have a different case filename found in RomRoot
                                    {
                                        if (dbChild.FileName == fileChild.Name) // check if the DatRoot name does now match the name in the DB Filename
                                        {
                                            dbChild.FileName = null; // if it does undo the BadCase Flag
                                        }
                                    }
                                    dbChild.Name = fileChild.Name; // Set the db Name to match the DatRoot Name.
                                }
                            }
                            else
                            {
                                dbChild.Name = fileChild.Name;
                            }

                            if (dbDir.Tree == null)
                                dbDir.Tree = new RvTreeRow();

                            UpdateDirs(dbChild, fileChild);
                        }

                        dbDirIndex++;
                        datDirIndex++;
                        break;

                    case 1:
                        // found a new directory in Dat
                        RvFile tDir = dbDir.DatAddDirectory(fileChild.Name, dbDirIndex);
                        Debug.WriteLine("Adding new Dir and Calling back in to check this DIR " + tDir.Name);
                        UpdateDirs(tDir, fileChild);
                        dbDirChildCount++;
                        dbDirIndex++;
                        datDirIndex++;
                        break;
                    case -1:
                        // all files 
                        dbDirIndex++;
                        break;
                }
            }
        }



        /// <summary>
        ///     Add the new DAT's into the DAT list
        ///     And merge in the new DAT data into the database
        /// </summary>
        /// <param name="dbDir">The Current database dir</param>
        /// <param name="datDir">A temp directory containing the DAT found in this directory in DatRoot</param>
        private static void AddTheDatsInThisDir(RvFile dbDir, DatImportDir datDir)
        {
            int dbDirDatCount = dbDir.DirDatCount;
            int datDirDatCount = datDir.DatFilesCount;
            int dbDirDatIndex = 0;
            int datDirDatIndex = 0;

            Debug.WriteLine("");
            Debug.WriteLine("Scanning for Adding new DATS");
            while (true)
            {
                RvDat dbDat = null;
                DatImportDat fileDat = null;
                int res;

                if (dbDirDatIndex < dbDirDatCount && datDirDatIndex < datDirDatCount)
                {
                    dbDat = dbDir.DirDat(dbDirDatIndex);
                    fileDat = datDir.DirDat(datDirDatIndex);
                    //TODO check this
                    res = DBHelper.DatCompare(dbDat, fileDat);
                    Debug.WriteLine("Checking " + dbDat.GetData(RvDat.DatData.DatRootFullName) + " : and " + fileDat.DatFullName + " : " + res);
                }
                else if (datDirDatIndex < datDirDatCount)
                {
                    fileDat = datDir.DirDat(datDirDatIndex);
                    res = 1;
                    Debug.WriteLine("Checking : and " + fileDat.DatFullName + " : " + res);
                }
                else if (dbDirDatIndex < dbDirDatCount)
                {
                    dbDat = dbDir.DirDat(dbDirDatIndex);
                    res = -1;
                    Debug.WriteLine("Checking " + dbDat.GetData(RvDat.DatData.DatRootFullName) + " : and : " + res);
                }
                else
                    break;


                switch (res)
                {
                    case 0:
                        _datsProcessed++;
                        _thWrk.Report(_datsProcessed);
                        _thWrk.Report(new bgwText("Dat : " + Path.GetFileNameWithoutExtension(fileDat?.DatFullName)));


                        Debug.WriteLine("Correct");
                        // Should already be set as correct above
                        if (dbDat != null)
                            dbDat.Status = DatUpdateStatus.Correct;
                        dbDirDatIndex++;
                        datDirDatIndex++;
                        break;

                    case 1:
                        _datsProcessed++;
                        _thWrk.Report(_datsProcessed);
                        _thWrk.Report(new bgwText("Scanning New Dat : " + Path.GetFileNameWithoutExtension(fileDat?.DatFullName)));


                        Debug.WriteLine("Adding new DAT");
                        if (LoadNewDat(dbDir, fileDat))
                        {
                            dbDirDatIndex++;
                            dbDirDatCount++;
                        }
                        datDirDatIndex++;
                        break;

                    case -1:
                        // This should not happen as deleted dat have been removed above
                        //dbIndex++;
                        ReportError.SendAndShow("ERROR Deleting a DAT that should already be deleted.");
                        break;
                }
            }
        }

        private static bool LoadNewDat(RvFile dbDir, DatImportDat fileDat)
        {
            // Read the new Dat File into newDatFile
            if (!DatReader.ReadInDatFile(fileDat, _thWrk))
            {
                ReportError.Show("Error reading Dat " + fileDat.DatFullName);
                return false;
            }


            if (fileDat.datHeader.BaseDir.Count == 0)
            {
                return false;
            }

            if (dbDir.Tree == null)
            {
                dbDir.Tree = new RvTreeRow();
            }

            RvDat thisDat = new RvDat(fileDat);

            string errorMessage;
            if (MergeInDat(dbDir, fileDat.datHeader.BaseDir, thisDat, out RvDat conflictDat, true, out errorMessage))
            {
                ReportError.Show($"Dat Merge conflict occured Cache contains {conflictDat.GetData(RvDat.DatData.DatRootFullName)} new dat {fileDat.DatFullName} is trying to use the same directory and so will be ignored.\nPlease report this to RomVault Discord:\n{errorMessage}");
                return false;
            }

            //SetInDat(thisDirectory);

            // Add the new Dat 
            dbDir.DirDatAdd(thisDat);

            // Merge the files/directories in the Dat
            MergeInDat(dbDir, fileDat.datHeader.BaseDir, thisDat, out _, false, out _);
            return true;
        }

        private static bool MergeInDat(RvFile dbDir, DatDir fileDir, RvDat thisRvDat, out RvDat conflict, bool checkOnly, out string errorMessage)
        {
            errorMessage = "";

            conflict = null;
            int dbDirChildCount = dbDir.ChildCount;
            int fileDirCount = fileDir.Count;
            int dbDirChildIndex = 0;
            int fileDirIndex = 0;
            while (true)
            {
                RvFile dbChild = null;
                DatBase newDatChild = null;
                int res;

                if (dbDirChildIndex < dbDirChildCount && fileDirIndex < fileDirCount)
                {
                    dbChild = dbDir.Child(dbDirChildIndex); // are files
                    newDatChild = fileDir[fileDirIndex]; // is from a dat item
                    //TODO check this
                    res = RVSorters.CompareName(dbChild, newDatChild);
                }
                else if (fileDirIndex < fileDirCount)
                {
                    newDatChild = fileDir[fileDirIndex];
                    res = 1;
                }
                else if (dbDirChildIndex < dbDirChildCount)
                {
                    dbChild = dbDir.Child(dbDirChildIndex);
                    res = -1;
                }
                else
                    break;

                if (res == 0)
                {
                    if (dbChild == null || newDatChild == null)
                    {
                        ShowDat("Error in Logic", dbDir.FullName);
                        break;
                    }


                    List<RvFile> dbDats = new List<RvFile>();
                    int dbDatsCount = 1;


                    dbDats.Add(dbChild);

                    //TODO check this
                    while (dbDirChildIndex + dbDatsCount < dbDirChildCount && RVSorters.CompareName(dbChild, dbDir.Child(dbDirChildIndex + dbDatsCount)) == 0)
                    {
                        dbDats.Add(dbDir.Child(dbDirChildIndex + dbDatsCount));
                        dbDatsCount++;
                    }

                    for (int indexdb = 0; indexdb < dbDatsCount; indexdb++)
                    {
                        if (dbDats[indexdb].DatStatus == DatStatus.NotInDat)
                        {
                            continue;
                        }

                        if (checkOnly)
                        {
                            conflict = dbChild.Dat;
                            errorMessage = $"Found Status: {dbDats[indexdb].DatStatus}\n datName: {dbDats[indexdb].TreeFullName}\n";
                            return true;
                        }

                        ShowDat("Unknown Update Dat Status " + dbChild.DatStatus, dbDir.FullName);
                        break;
                    }

                    if (!checkOnly)
                    {
                        bool caseTest = dbDats.Count > 1;
                        bool found = false;
                        for (int indexCase = (caseTest ? 0 : 1); indexCase < 2; indexCase += 1)
                        {
                            for (int indexDbDats = 0; indexDbDats < dbDatsCount; indexDbDats++)
                            {
                                if (dbDats[indexDbDats].SearchFound)
                                {
                                    continue;
                                }

                                bool matched = DatCompare.DatMergeCompare(dbDats[indexDbDats], newDatChild, indexCase, out bool altMatch);
                                if (!matched)
                                {
                                    continue;
                                }

                                dbDats[indexDbDats].DatMergeIn(newDatChild, thisRvDat, altMatch);

                                FileType ft = dbDats[indexDbDats].FileType;

                                if (ft == FileType.Zip || ft == FileType.SevenZip || ft == FileType.Dir)
                                {
                                    MergeInDat(dbDats[indexDbDats], (DatDir)newDatChild, thisRvDat, out conflict, false, out _);
                                }

                                dbDats[indexDbDats].SearchFound = true;
                                found = true;
                                break;
                            }
                            if (found)
                                break;
                        }

                        if (!found)
                        {
                            dbChild = dbDir.DatAdd(newDatChild, thisRvDat, dbDirChildIndex);
                            SetMissingStatus(dbChild);
                            dbDirChildCount++;
                            dbDirChildIndex++;
                        }
                    }

                    dbDirChildIndex += dbDatsCount;
                    fileDirIndex++;
                }

                if (res == 1)
                {
                    if (!checkOnly)
                    {
                        dbChild = dbDir.DatAdd(newDatChild, thisRvDat, dbDirChildIndex);
                        SetMissingStatus(dbChild);
                        dbDirChildCount++;
                        dbDirChildIndex++;
                    }
                    fileDirIndex++;
                }

                if (res == -1)
                {
                    dbDirChildIndex++;
                }
            }
            return false;
        }

        private static void SetMissingStatus(RvFile dbChild)
        {
            if (dbChild.FileRemove() == EFile.Delete)
            {
                ReportError.SendAndShow("Error is Set Missing Status in DatUpdate");
                return;
            }


            FileType ft = dbChild.FileType;
            if (ft == FileType.Zip || ft == FileType.SevenZip || ft == FileType.Dir)
            {
                RvFile dbDir = dbChild;
                for (int i = 0; i < dbDir.ChildCount; i++)
                {
                    SetMissingStatus(dbDir.Child(i));
                }
            }
        }

        private static void RemoveOldTree(RvFile dbFile)
        {
            RvFile dbDir = dbFile;
            if (!dbDir.IsDirectory)
            {
                return;
            }

            if (dbDir.DatStatus == DatStatus.NotInDat && dbDir.Tree != null)
            {
                dbDir.Tree = null;
            }

            for (int i = 0; i < dbDir.ChildCount; i++)
            {
                RemoveOldTree(dbDir.Child(i));
            }
        }


        public static void CheckAllDats(RvFile dbFile, string romVaultPath)
        {
            CheckAllDatsInternal(dbFile, "DatRoot" + romVaultPath.Substring(8));
        }

        private static void CheckAllDatsInternal(RvFile dbFile, string datPath)
        {
            RvFile dbDir = dbFile;
            if (!dbDir.IsDirectory)
                return;

            int dats = dbDir.DirDatCount;
            if (dats > 0)
            {
                string datFullPath = dbFile.DatTreeFullName;
                if (datPath.Length <= datFullPath.Length)
                {
                    if (datFullPath.Substring(0, datPath.Length) == datPath)
                    {
                        for (int i = 0; i < dats; i++)
                            dbDir.DirDat(i).InvalidateDatTimeStamp();
                    }
                }
            }
            if (dbFile.Dat != null)
            {
                string datFullName = dbFile.Dat.GetData(RvDat.DatData.DatRootFullName);
                if (datPath.Length <= datFullName.Length)
                {
                    if (datFullName.Substring(0, datPath.Length) == datPath)
                        dbFile.Dat.InvalidateDatTimeStamp();
                }
            }

            for (int i = 0; i < dbDir.ChildCount; i++)
                CheckAllDatsInternal(dbDir.Child(i), datPath);
        }
    }
}