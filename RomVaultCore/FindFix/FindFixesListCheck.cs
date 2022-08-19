using System.Collections.Generic;
using RomVaultCore.RvDB;

namespace RomVaultCore.FindFix
{
    public static class FindFixesListCheck
    {
        public static void GroupListCheck(FileGroup[] filegroupList)
        {
            foreach (FileGroup ff in filegroupList)
            {
                if (ff.Size == 0 && ff.CRC[0] == 0 && ff.CRC[1] == 0 && ff.CRC[2] == 0 && ff.CRC[3] == 0)
                    ZeroListCheck(ff);
                else
                    ListCheck(ff);
            }
        }

        private static void ZeroListCheck(FileGroup family)
        {
            List<RvFile> files = family.Files;
            // set the found status of this file
            foreach (RvFile tFile in files)
            {
                if (treeType(tFile) == RvTreeRow.TreeSelect.Locked)
                    continue;

                switch (tFile.RepStatus)
                {
                    case RepStatus.UnScanned:
                        break;
                    case RepStatus.Missing:
                        tFile.RepStatus = tFile.DatStatus == DatStatus.InDatMIA ? RepStatus.CanBeFixedMIA : RepStatus.CanBeFixed;
                        break;
                    case RepStatus.Correct:
                    case RepStatus.CorrectMIA:
                        break;
                    case RepStatus.Corrupt:
                        if (tFile.DatStatus == DatStatus.InDatCollect)
                            tFile.RepStatus = tFile.DatStatus == DatStatus.InDatMIA ? RepStatus.CanBeFixedMIA : RepStatus.CanBeFixed; // corrupt files that are also InDatcollect are treated as missing files, and a fix should be found.
                        else
                            tFile.RepStatus = RepStatus.Delete; // all other corrupt files should be deleted or moved to tosort/corrupt
                        break;
                    case RepStatus.UnNeeded:
                    case RepStatus.Unknown:
                        tFile.RepStatus = RepStatus.Delete;
                        break;
                    case RepStatus.NotCollected:
                        break;
                    case RepStatus.InToSort:
                        tFile.RepStatus = RepStatus.Delete;
                        break;
                    case RepStatus.Ignore:
                        break; // Ignore File
                    default:
                        ReportError.SendAndShow("Unknown test status " + tFile.FullName + "," + tFile.DatStatus + "," + tFile.RepStatus);
                        break;

                }
            }
        }

        private static void ListCheck(FileGroup family)
        {
            List<RvFile> files = family.Files;

            List<RvFile> missingFiles = new List<RvFile>(); // files we dont have that we need

            List<RvFile> correctFiles = new List<RvFile>();   // files we have that are in the correct place
            List<RvFile> unNeededFiles = new List<RvFile>();  // files we have that are not in the correct place
            List<RvFile> inToSortFiles = new List<RvFile>();  // files we have that are in tosort
            List<RvFile> allGotFiles = new List<RvFile>();    // all files we have

            List<RvFile> corruptFiles = new List<RvFile>(); // corrupt files that we do not need, a corrupt file is missing if it is needed


            // set the found status of this file
            foreach (RvFile tFile in files)
            {
                switch (tFile.RepStatus)
                {
                    case RepStatus.UnScanned:
                        break;
                    case RepStatus.Missing:
                    case RepStatus.MissingMIA:
                        missingFiles.Add(tFile); // these are checked in step 1 to fixes from the allGotFiles List.
                        break;
                    case RepStatus.Correct:
                    case RepStatus.CorrectMIA:
                        correctFiles.Add(tFile);
                        break;
                    case RepStatus.Corrupt:
                        if (tFile.DatStatus == DatStatus.InDatCollect)
                            missingFiles.Add(tFile); // corrupt files that are also InDatcollect are treated as missing files, and a fix should be found.
                        else
                            corruptFiles.Add(tFile); // all other corrupt files should be deleted or moved to tosort/corrupt
                        break;
                    case RepStatus.UnNeeded:
                    case RepStatus.Unknown:
                        unNeededFiles.Add(tFile);
                        break;
                    case RepStatus.NotCollected:
                        break;
                    case RepStatus.InToSort:
                        inToSortFiles.Add(tFile);
                        break;
                    case RepStatus.Ignore:
                        break; // Ignore File
                    default:
                        ReportError.SendAndShow("Unknown test status " + tFile.FullName + "," + tFile.DatStatus + "," + tFile.RepStatus);
                        break;

                }
            }
            allGotFiles.AddRange(correctFiles);
            allGotFiles.AddRange(unNeededFiles);
            allGotFiles.AddRange(inToSortFiles);

            #region Step 1 Check the Missing files from the allGotFiles List.
            // check to see if we can find any of the missing files in the gotFiles list.
            // if we find them mark them as CanBeFixed, 
            // or if they are missing corrupt files set then as corruptCanBefixed

            foreach (RvFile missingFile in missingFiles)
            {
                if (treeType(missingFile) == RvTreeRow.TreeSelect.Locked)
                    continue;

                /*
               if (DBHelper.IsZeroLengthFile(missingFile))
               {
                   missingFile.RepStatus = missingFile.RepStatus == RepStatus.Corrupt ? RepStatus.CorruptCanBeFixed : RepStatus.CanBeFixed;
                   continue;
               }
               */

                foreach (RvFile gotFile in allGotFiles)
                {
                    if (!DBHelper.CheckIfMissingFileCanBeFixedByGotFile(missingFile, gotFile)) continue;
                    missingFile.RepStatus = missingFile.RepStatus == RepStatus.Corrupt ? RepStatus.CorruptCanBeFixed : (missingFile.DatStatus == DatStatus.InDatMIA ? RepStatus.CanBeFixedMIA : RepStatus.CanBeFixed);
                    break;
                }
                if (missingFile.RepStatus == RepStatus.Corrupt)
                    missingFile.RepStatus = RepStatus.MoveToCorrupt;
            }
            #endregion

            #region Step 2 Check all corrupt files.
            // if we have a correct version of the corrupt file then the corrput file can just be deleted,
            // otherwise if the corrupt file is not already in ToSort it should be moved out to ToSort.

            // we can only check corrupt files using the CRC from the ZIP header, as it is corrupt so we cannot get a correct SHA1 / MD5 to check with

            foreach (RvFile corruptFile in corruptFiles)
            {
                if (treeType(corruptFile) == RvTreeRow.TreeSelect.Locked)
                    continue;

                if (allGotFiles.Count > 0)
                    corruptFile.RepStatus = RepStatus.Delete;

                if (corruptFile.RepStatus == RepStatus.Corrupt && corruptFile.DatStatus != DatStatus.InToSort)
                    corruptFile.RepStatus = RepStatus.MoveToCorrupt;
            }
            #endregion

            #region Step 3 Check if unNeeded files are needed for a fix, otherwise delete them or move them to tosort
            foreach (RvFile unNeededFile in unNeededFiles)
            {
                /*
                // check if we have a confirmed SHA1 / MD5 match of this file, and if we do we just mark this file to be deleted.
                foreach (RvFile correctFile in correctFiles)
                {
                    if (!FindSHA1MD5MatchingFiles(unNeededFile, correctFile)) continue;
                    unNeededFile.RepStatus = RepStatus.Delete;
                    break;
                }
                if (unNeededFile.RepStatus == RepStatus.Delete) continue;
                */

                /*
                if (DBHelper.IsZeroLengthFile(unNeededFile))
                {
                    if (treeType(unNeededFile) == RvTreeRow.TreeSelect.Locked)
                        continue;

                    unNeededFile.RepStatus = RepStatus.Delete;
                    continue;
                }
                */

                // check if the unNeededFile is needed to fix a missing file
                foreach (RvFile missingFile in missingFiles)
                {
                    if (!DBHelper.CheckIfMissingFileCanBeFixedByGotFile(missingFile, unNeededFile)) continue;
                    unNeededFile.RepStatus = RepStatus.NeededForFix;
                    break;
                }
                if (unNeededFile.RepStatus == RepStatus.NeededForFix) continue;

                if (treeType(unNeededFile) == RvTreeRow.TreeSelect.Locked)
                    continue;

                // now that we know this file is not needed for a fix do a CRC only find against correct files to see if this file can be deleted.
                foreach (RvFile correctFile in correctFiles)
                {
                    if (!DBHelper.CheckIfGotfileAndMatchingFileAreFullMatches(unNeededFile, correctFile)) continue;
                    unNeededFile.RepStatus = RepStatus.Delete;
                    break;
                }
                if (unNeededFile.RepStatus == RepStatus.Delete) continue;

                // and finally see if the file is already in ToSort, and if it is deleted.
                foreach (RvFile inToSortFile in inToSortFiles)
                {
                    if (!DBHelper.CheckIfGotfileAndMatchingFileAreFullMatches(unNeededFile, inToSortFile)) continue;
                    unNeededFile.RepStatus = RepStatus.Delete;
                    break;
                }
                if (unNeededFile.RepStatus == RepStatus.Delete) continue;

                // otherwise move the file out to ToSort
                unNeededFile.RepStatus = RepStatus.MoveToSort;
            }
            #endregion

            #region Step 4 Check if ToSort files are needed for a fix, otherwise delete them or leave them in tosort
            foreach (RvFile inToSortFile in inToSortFiles)
            {
                /*
                // check if we have a confirmed SHA1 / MD5 match of this file, and if we do we just mark this file to be deleted.
                foreach (RvFile correctFile in correctFiles)
                {
                    if (!FindSHA1MD5MatchingFiles(inToSortFile, correctFile)) continue;
                    inToSortFile.RepStatus = RepStatus.Delete;
                    break;
                }
                if (inToSortFile.RepStatus == RepStatus.Delete) continue;
                */

                // check if the ToSortFile is needed to fix a missing file
                foreach (RvFile missingFile in missingFiles)
                {
                    if (treeType(missingFile) == RvTreeRow.TreeSelect.Locked)
                        continue;

                    if (!DBHelper.CheckIfMissingFileCanBeFixedByGotFile(missingFile, inToSortFile)) continue;
                    inToSortFile.RepStatus = RepStatus.NeededForFix;
                    break;
                }
                if (inToSortFile.RepStatus == RepStatus.NeededForFix)
                    continue;

                if (treeType(inToSortFile) == RvTreeRow.TreeSelect.Locked)
                    continue;


                // now that we know this file is not needed for a fix do a CRC only find against correct files to see if this file can be deleted.
                foreach (RvFile correctFile in correctFiles)
                {
                    if (!DBHelper.CheckIfGotfileAndMatchingFileAreFullMatches(inToSortFile, correctFile)) continue;
                    inToSortFile.RepStatus = RepStatus.Delete;
                    break;
                }

                // otherwise leave the file in ToSort
            }
            #endregion

            //need to check here for roms that just need renamed inside the one ZIP
            //this prevents Zips from self deadlocking

            List<RvFile> canBeFixed = new List<RvFile>();
            foreach (RvFile fLoop in files)
            {
                if (fLoop.RepStatus != RepStatus.CanBeFixed && fLoop.RepStatus!=RepStatus.CanBeFixedMIA) continue;
                canBeFixed.Add(fLoop);
            }

            foreach (RvFile fOutLoop in files)
            {
                if (fOutLoop.RepStatus != RepStatus.NeededForFix) continue;
                foreach (RvFile fInLoop in canBeFixed)
                {
                    if (!DBHelper.RomFromSameGame(fOutLoop, fInLoop)) continue;

                    if (!DBHelper.CheckIfMissingFileCanBeFixedByGotFile(fInLoop, fOutLoop)) continue;

                    fOutLoop.RepStatus = RepStatus.Rename;
                }
            }
        }

        public static RvTreeRow.TreeSelect treeType(RvFile tfile)
        {
            if (tfile == null)
                return RvTreeRow.TreeSelect.Locked;
            if (tfile.Tree != null)
            {
                return tfile.Tree.Checked;
            }

            return treeType(tfile.Parent);
        }

    }
}
