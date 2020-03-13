/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2013                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using RVCore.RvDB;
using RVCore.Scanner;
using DATReader.Utils;
using RVIO;

namespace RVCore.ReadDat
{
    public static class DatUpdate
    {
        private static int _datCount;
        private static int _datsProcessed;
        private static ThreadWorker _thWrk;

        private static void ShowDat(string message, string filename)
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
                RepairStatus.ReportStatusReset(DB.DirTree);

                _datCount = 0;

                _thWrk.Report(new bgwText("Finding Dats"));
                RvFile datRoot = new RvFile(FileType.Dir) { Name = "RomVault", DatStatus = DatStatus.InDatCollect };

                // build a datRoot tree of the DAT's in DatRoot, and count how many dats are found
                if (!RecursiveDatTree(datRoot, out _datCount))
                {
                    _thWrk.Report(new bgwText("Dat Update Complete"));
                    _thWrk = null;
                    return;
                }

                _thWrk.Report(new bgwText("Scanning Dats"));
                _datsProcessed = 0;

                // now compare the database DAT's with datRoot removing any old DAT's
                RemoveOldDats(DB.DirTree.Child(0), datRoot);

                // next clean up the File status removing any old DAT's
                RemoveOldDatsCleanUpFiles(DB.DirTree.Child(0));

                _thWrk.Report(new bgwSetRange(_datCount - 1));

                // next add in new DAT and update the files
                UpdateDatList(DB.DirTree.Child(0), datRoot);

                // finally remove any unneeded directories from the TreeView
                RemoveOldTree(DB.DirTree.Child(0));

                _thWrk.Report(new bgwText("Updating Cache"));
                DB.Write();

                _thWrk.Report(new bgwText("Dat Update Complete"));
                _thWrk = null;
            }
            catch (Exception exc)
            {
                ReportError.UnhandledExceptionHandler(exc);

                _thWrk?.Report(new bgwText("Updating Cache"));
                DB.Write();
                _thWrk?.Report(new bgwText("Complete"));

                _thWrk = null;
            }
        }

        private static bool RecursiveDatTree(RvFile tDir, out int datCount)
        {
            datCount = 0;
            string strPath = RvFile.GetDatPhysicalPath(tDir.DatTreeFullName);

            if (!Directory.Exists(strPath))
            {
                ReportError.Show("Path: " + strPath + " Not Found.");
                return false;
            }

            DirectoryInfo oDir = new DirectoryInfo(strPath);

            FileInfo[] oFilesIn = oDir.GetFiles("*.dat", false);
            datCount += oFilesIn.Length;
            foreach (FileInfo file in oFilesIn)
            {
                RvDat tDat = new RvDat();
                tDat.AddData(RvDat.DatData.DatRootFullName, Path.Combine(tDir.DatTreeFullName, file.Name));
                tDat.TimeStamp = file.LastWriteTime;

                string datRootFullName = tDat.GetData(RvDat.DatData.DatRootFullName);
                DatRule datRule = DatReader.FindDatRule(datRootFullName);
                tDat.MultiDatOverride = datRule.MultiDATDirOverride;
                tDat.UseDescriptionAsDirName = datRule.UseDescriptionAsDirName;
                tDat.SingleArchive = datRule.SingleArchive;

                tDir.DirDatAdd(tDat);
            }


            oFilesIn = oDir.GetFiles("*.xml", false);
            datCount += oFilesIn.Length;
            foreach (FileInfo file in oFilesIn)
            {
                RvDat tDat = new RvDat();
                tDat.AddData(RvDat.DatData.DatRootFullName, Path.Combine(tDir.DatTreeFullName, file.Name));
                tDat.TimeStamp = file.LastWriteTime;

                string datRootFullName = tDat.GetData(RvDat.DatData.DatRootFullName);
                DatRule datRule = DatReader.FindDatRule(datRootFullName);
                tDat.MultiDatOverride = datRule.MultiDATDirOverride;
                tDat.UseDescriptionAsDirName = datRule.UseDescriptionAsDirName;
                tDat.SingleArchive = datRule.SingleArchive;

                tDir.DirDatAdd(tDat);
            }

            if (tDir.DirDatCount > 1)
            {
                for (int i = 0; i < tDir.DirDatCount; i++)
                {
                    tDir.DirDat(i).MultiDatsInDirectory = true;
                }
            }

            DirectoryInfo[] oSubDir = oDir.GetDirectories(false);

            foreach (DirectoryInfo t in oSubDir)
            {
                RvFile cDir = new RvFile(FileType.Dir) { Name = t.Name, DatStatus = DatStatus.InDatCollect };
                int index = tDir.ChildAdd(cDir);

                RecursiveDatTree(cDir, out int retDatCount);
                datCount += retDatCount;

                if (retDatCount == 0)
                {
                    tDir.ChildRemove(index);
                }
            }

            return true;
        }


        private static void RemoveOldDats(RvFile dbDir, RvFile tmpDir)
        {
            // now compare the old and new dats removing any old dats
            // in the current directory

            RvFile lDir = dbDir;
            if (!lDir.IsDir)
            {
                return;
            }

            int dbIndex = 0;
            int scanIndex = 0;

            while (dbIndex < lDir.DirDatCount || scanIndex < tmpDir.DirDatCount)
            {
                RvDat dbDat = null;
                int res = 0;

                if (dbIndex < lDir.DirDatCount && scanIndex < tmpDir.DirDatCount)
                {
                    dbDat = lDir.DirDat(dbIndex);
                    RvDat fileDat = tmpDir.DirDat(scanIndex);
                    res = DBHelper.DatCompare(dbDat, fileDat);
                }
                else if (scanIndex < tmpDir.DirDatCount)
                {
                    //this is a new dat that we have now found at the end of the list
                    //fileDat = tmpDir.DirDat(scanIndex);
                    res = 1;
                }
                else if (dbIndex < lDir.DirDatCount)
                {
                    dbDat = lDir.DirDat(dbIndex);
                    res = -1;
                }

                switch (res)
                {
                    case 0:
                        if (dbDat != null)
                            dbDat.Status = DatUpdateStatus.Correct;
                        dbIndex++;
                        scanIndex++;
                        break;

                    case 1:
                        // this is a new dat that we will add next time around
                        scanIndex++;
                        break;
                    case -1:
                        if (dbDat != null)
                            dbDat.Status = DatUpdateStatus.Delete;
                        lDir.DirDatRemove(dbIndex);
                        break;
                }
            }

            // now scan the child directory structure of this directory
            dbIndex = 0;
            scanIndex = 0;

            while (dbIndex < lDir.ChildCount || scanIndex < tmpDir.ChildCount)
            {
                RvFile dbChild = null;
                RvFile fileChild = null;
                int res = 0;

                if (dbIndex < lDir.ChildCount && scanIndex < tmpDir.ChildCount)
                {
                    dbChild = lDir.Child(dbIndex);
                    fileChild = tmpDir.Child(scanIndex);
                    res = DBHelper.CompareName(dbChild, fileChild);
                }
                else if (scanIndex < tmpDir.ChildCount)
                {
                    //found a new directory on the end of the list
                    //fileChild = tmpDir.Child(scanIndex);
                    res = 1;
                }
                else if (dbIndex < lDir.ChildCount)
                {
                    dbChild = lDir.Child(dbIndex);
                    res = -1;
                }
                switch (res)
                {
                    case 0:
                        // found a matching directory in DatRoot So recurse back into it
                        RemoveOldDats(dbChild, fileChild);
                        dbIndex++;
                        scanIndex++;
                        break;

                    case 1:
                        // found a new directory will be added later
                        scanIndex++;
                        break;
                    case -1:
                        if (dbChild?.FileType == FileType.Dir && dbChild.Dat == null)
                        {
                            RemoveOldDats(dbChild, new RvFile(FileType.Dir));
                        }
                        dbIndex++;
                        break;
                }
            }
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
            if (!tDir.IsDir)
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


        private static void UpdateDatList(RvFile dbDir, RvFile tmpDir)
        {
            AddNewDats(dbDir, tmpDir);
            UpdateDirs(dbDir, tmpDir);
        }

        /// <summary>
        ///     Add the new DAT's into the DAT list
        ///     And merge in the new DAT data into the database
        /// </summary>
        /// <param name="dbDir">The Current database dir</param>
        /// <param name="tmpDir">A temp directory containing the DAT found in this directory in DatRoot</param>
        private static void AddNewDats(RvFile dbDir, RvFile tmpDir)
        {
            int dbIndex = 0;
            int scanIndex = 0;

            Debug.WriteLine("");
            Debug.WriteLine("Scanning for Adding new DATS");
            while (dbIndex < dbDir.DirDatCount || scanIndex < tmpDir.DirDatCount)
            {
                RvDat dbDat = null;
                RvDat fileDat = null;
                int res = 0;

                if (dbIndex < dbDir.DirDatCount && scanIndex < tmpDir.DirDatCount)
                {
                    dbDat = dbDir.DirDat(dbIndex);
                    fileDat = tmpDir.DirDat(scanIndex);
                    res = DBHelper.DatCompare(dbDat, fileDat);
                    Debug.WriteLine("Checking " + dbDat.GetData(RvDat.DatData.DatRootFullName) + " : and " + fileDat.GetData(RvDat.DatData.DatRootFullName) + " : " + res);
                }
                else if (scanIndex < tmpDir.DirDatCount)
                {
                    fileDat = tmpDir.DirDat(scanIndex);
                    res = 1;
                    Debug.WriteLine("Checking : and " + fileDat.GetData(RvDat.DatData.DatRootFullName) + " : " + res);
                }
                else if (dbIndex < dbDir.DirDatCount)
                {
                    dbDat = dbDir.DirDat(dbIndex);
                    res = -1;
                    Debug.WriteLine("Checking " + dbDat.GetData(RvDat.DatData.DatRootFullName) + " : and : " + res);
                }

                switch (res)
                {
                    case 0:
                        _datsProcessed++;
                        _thWrk.Report(_datsProcessed);
                        _thWrk.Report(new bgwText("Dat : " + Path.GetFileNameWithoutExtension(fileDat?.GetData(RvDat.DatData.DatRootFullName))));


                        Debug.WriteLine("Correct");
                        // Should already be set as correct above
                        if (dbDat != null)
                            dbDat.Status = DatUpdateStatus.Correct;
                        dbIndex++;
                        scanIndex++;
                        break;

                    case 1:
                        _datsProcessed++;
                        _thWrk.Report(_datsProcessed);
                        _thWrk.Report(new bgwText("Scanning New Dat : " + Path.GetFileNameWithoutExtension(fileDat?.GetData(RvDat.DatData.DatRootFullName))));


                        Debug.WriteLine("Adding new DAT");
                        if (LoadNewDat(fileDat, dbDir))
                        {
                            dbIndex++;
                        }
                        scanIndex++;
                        break;

                    case -1:
                        // This should not happen as deleted dat have been removed above
                        //dbIndex++;
                        ReportError.SendAndShow("ERROR Deleting a DAT that should already be deleted.");
                        break;
                }
            }
        }


        private static bool LoadNewDat(RvDat fileDat, RvFile thisDirectory)
        {
            // Read the new Dat File into newDatFile
            RvFile newDatFile = DatReader.ReadInDatFile(fileDat, _thWrk);

            // If we got a valid Dat File back
            if (newDatFile?.Dat == null)
            {
                ReportError.Show("Error reading Dat " + fileDat.GetData(RvDat.DatData.DatRootFullName));
                return false;
            }

            if (
                    !fileDat.MultiDatOverride &&
                    newDatFile.Dat.GetData(RvDat.DatData.DirSetup) != "noautodir" &&
                    (
                        fileDat.MultiDatsInDirectory ||
                        !string.IsNullOrEmpty(newDatFile.Dat.GetData(RvDat.DatData.RootDir))
                    )
                )
            {
                // if we are auto adding extra directories then create a new directory.
                string dirName = "";
                if (string.IsNullOrEmpty(dirName) && fileDat.UseDescriptionAsDirName && !string.IsNullOrWhiteSpace(newDatFile.Dat.GetData(RvDat.DatData.Description)))
                    dirName = newDatFile.Dat.GetData(RvDat.DatData.Description);
                if (string.IsNullOrEmpty(dirName) && !string.IsNullOrEmpty(newDatFile.Dat.GetData(RvDat.DatData.RootDir)))
                    dirName = newDatFile.Dat.GetData(RvDat.DatData.RootDir);
                if (string.IsNullOrEmpty(dirName))
                    dirName= newDatFile.Dat.GetData(RvDat.DatData.DatName);
                newDatFile.Name = VarFix.CleanFileName(dirName);

                newDatFile.DatStatus = DatStatus.InDatCollect;
                newDatFile.Tree = new RvTreeRow();

                RvFile newDirectory = new RvFile(FileType.Dir) { Dat = newDatFile.Dat };

                // add the DAT into this directory
                newDirectory.ChildAdd(newDatFile);
                newDatFile = newDirectory;

                newDatFile.Dat.AutoAddedDirectory = true;
            }
            else
            {
                newDatFile.Dat.AutoAddedDirectory = false;
            }

            if (thisDirectory.Tree == null)
            {
                thisDirectory.Tree = new RvTreeRow();
            }

            if (MergeInDat(thisDirectory, newDatFile, out RvDat conflictDat, true))
            {
                ReportError.Show("Dat Merge conflict occured Cache contains " + conflictDat.GetData(RvDat.DatData.DatRootFullName) + " new dat " + newDatFile.Dat.GetData(RvDat.DatData.DatRootFullName) + " is trying to use the same directory and so will be ignored.");
                return false;
            }

            //SetInDat(thisDirectory);

            // Add the new Dat 
            thisDirectory.DirDatAdd(newDatFile.Dat);

            // Merge the files/directories in the Dat
            MergeInDat(thisDirectory, newDatFile, out conflictDat, false);
            return true;
        }

        private static bool MergeInDat(RvFile dbDat, RvFile newDat, out RvDat conflict, bool checkOnly)
        {
            conflict = null;
            int dbIndex = 0;
            int newIndex = 0;
            while (dbIndex < dbDat.ChildCount || newIndex < newDat.ChildCount)
            {
                RvFile dbChild = null;
                RvFile newDatChild = null;
                int res = 0;

                if (dbIndex < dbDat.ChildCount && newIndex < newDat.ChildCount)
                {
                    dbChild = dbDat.Child(dbIndex); // are files
                    newDatChild = newDat.Child(newIndex); // is from a dat item
                    res = DBHelper.CompareName(dbChild, newDatChild);
                }
                else if (newIndex < newDat.ChildCount)
                {
                    newDatChild = newDat.Child(newIndex);
                    res = 1;
                }
                else if (dbIndex < dbDat.ChildCount)
                {
                    dbChild = dbDat.Child(dbIndex);
                    res = -1;
                }

                if (res == 0)
                {
                    if (dbChild == null || newDatChild == null)
                    {
                        ShowDat("Error in Logic", dbDat.FullName);
                        break;
                    }


                    List<RvFile> dbDats = new List<RvFile>();
                    List<RvFile> newDats = new List<RvFile>();
                    int dbDatsCount = 1;
                    int newDatsCount = 1;


                    dbDats.Add(dbChild);
                    newDats.Add(newDatChild);

                    while (dbIndex + dbDatsCount < dbDat.ChildCount && DBHelper.CompareName(dbChild, dbDat.Child(dbIndex + dbDatsCount)) == 0)
                    {
                        dbDats.Add(dbDat.Child(dbIndex + dbDatsCount));
                        dbDatsCount += 1;
                    }
                    while (newIndex + newDatsCount < newDat.ChildCount && DBHelper.CompareName(newDatChild, newDat.Child(newIndex + newDatsCount)) == 0)
                    {
                        newDats.Add(newDat.Child(newIndex + newDatsCount));
                        newDatsCount += 1;
                    }

                    if (dbDatsCount > 1 || newDatsCount > 1)
                    {
                        ReportError.SendAndShow("Double Name Found");
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
                            return true;
                        }

                        ShowDat("Unknown Update Dat Status " + dbChild.DatStatus, dbDat.FullName);
                        break;
                    }

                    if (!checkOnly)
                    {
                        for (int indexNewDats = 0; indexNewDats < newDatsCount; indexNewDats++)
                        {
                            if (newDats[indexNewDats].SearchFound)
                            {
                                continue;
                            }

                            for (int indexDbDats = 0; indexDbDats < dbDatsCount; indexDbDats++)
                            {
                                if (dbDats[indexDbDats].SearchFound)
                                {
                                    continue;
                                }

                                bool matched = Compare.DatMergeCompare(dbDats[indexDbDats], newDats[indexNewDats], out bool altMatch);
                                if (!matched)
                                {
                                    continue;
                                }

                                dbDats[indexDbDats].DatAdd(newDats[indexNewDats], altMatch);

                                FileType ft = dbChild.FileType;

                                if (ft == FileType.Zip || ft == FileType.SevenZip || ft == FileType.Dir)
                                {
                                    RvFile dChild = dbChild;
                                    RvFile dNewChild = newDatChild;
                                    MergeInDat(dChild, dNewChild, out conflict, false);
                                }

                                dbDats[indexDbDats].SearchFound = true;
                                newDats[indexNewDats].SearchFound = true;
                            }
                        }

                        for (int indexNewDats = 0; indexNewDats < newDatsCount; indexNewDats++)
                        {
                            if (newDats[indexNewDats].SearchFound)
                            {
                                continue;
                            }

                            dbDat.ChildAdd(newDats[indexNewDats], dbIndex);
                            dbChild = dbDat.Child(dbIndex);
                            SetMissingStatus(dbChild);

                            dbIndex++;
                        }
                    }

                    dbIndex += dbDatsCount;
                    newIndex += newDatsCount;
                }

                if (res == 1)
                {
                    if (!checkOnly)
                    {
                        dbDat.ChildAdd(newDatChild, dbIndex);
                        dbChild = dbDat.Child(dbIndex);
                        SetMissingStatus(dbChild);

                        dbIndex++;
                    }
                    newIndex++;
                }

                if (res == -1)
                {
                    dbIndex++;
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


        private static void UpdateDirs(RvFile dbDir, RvFile fileDir)
        {
            int dbIndex = 0;
            int scanIndex = 0;

            dbDir.DatStatus = DatStatus.InDatCollect;
            if (dbDir.Tree == null)
            {
                Debug.WriteLine("Adding Tree View to " + dbDir.Name);
                dbDir.Tree = new RvTreeRow();
            }


            Debug.WriteLine("");
            Debug.WriteLine("Now scanning dirs");

            while (dbIndex < dbDir.ChildCount || scanIndex < fileDir.ChildCount)
            {
                RvFile dbChild = null;
                RvFile fileChild = null;
                int res = 0;

                if (dbIndex < dbDir.ChildCount && scanIndex < fileDir.ChildCount)
                {
                    dbChild = dbDir.Child(dbIndex);
                    fileChild = fileDir.Child(scanIndex);
                    res = DBHelper.CompareName(dbChild, fileChild);
                    Debug.WriteLine("Checking " + dbChild.Name + " : and " + fileChild.Name + " : " + res);
                }
                else if (scanIndex < fileDir.ChildCount)
                {
                    fileChild = fileDir.Child(scanIndex);
                    res = 1;
                    Debug.WriteLine("Checking : and " + fileChild.Name + " : " + res);
                }
                else if (dbIndex < dbDir.ChildCount)
                {
                    dbChild = dbDir.Child(dbIndex);
                    res = -1;
                }
                switch (res)
                {
                    case 0:
                        // found a matching directory in DatRoot So recurse back into it

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

                        UpdateDatList(dbChild, fileChild);
                        dbIndex++;
                        scanIndex++;
                        break;

                    case 1:
                        // found a new directory in Dat
                        RvFile tDir = new RvFile(FileType.Dir)
                        {
                            Name = fileChild.Name,
                            Tree = new RvTreeRow(),
                            DatStatus = DatStatus.InDatCollect
                        };
                        dbDir.ChildAdd(tDir, dbIndex);
                        Debug.WriteLine("Adding new Dir and Calling back in to check this DIR " + tDir.Name);
                        UpdateDatList(tDir, fileChild);

                        dbIndex++;
                        scanIndex++;
                        break;
                    case -1:
                        // all files 
                        dbIndex++;
                        break;
                }
            }
        }

        private static void RemoveOldTree(RvFile dbFile)
        {
            RvFile dbDir = dbFile;
            if (!dbDir.IsDir)
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
            if (!dbDir.IsDir)
            {
                return;
            }

            int dats = dbDir.DirDatCount;
            if (dats > 0)
            {
                for (int i = 0; i < dats; i++)
                {
                    RvDat testDat = dbDir.DirDat(i);
                    string datFullName = testDat.GetData(RvDat.DatData.DatRootFullName);
                    if (datPath.Length > datFullName.Length)
                        continue;
                    if (datFullName.Substring(0, datPath.Length) != datPath)
                        continue;

                    testDat.TimeStamp = long.MaxValue;
                }
            }


            for (int i = 0; i < dbDir.ChildCount; i++)
            {
                CheckAllDatsInternal(dbDir.Child(i), datPath);
            }
        }
    }
}