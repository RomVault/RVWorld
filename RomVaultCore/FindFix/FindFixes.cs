using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ByteSortedList;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using StorageList;

namespace RomVaultCore.FindFix
{
    public static class FindFixes
    {
        private static ThreadWorker _thWrk;
        private static int _progressCounter;

        private static bool checkCancel()
        {
            if (_thWrk != null && _thWrk.CancellationPending)
            {
                _thWrk.Report(new bgwText("Cancelling"));
                RepairStatus.ReportStatusReset(DB.DirRoot);
                _thWrk.Report(new bgwText("Cancelled"));
                _thWrk.Finished = true;
                _thWrk = null;
                return true;
            }

            return false;
        }

        public static void ScanFiles(ThreadWorker thWrk)
        {
            try
            {
                _thWrk = thWrk;
                if (_thWrk == null) return;
                _progressCounter = 0;

                _thWrk.Report(new bgwSetRange(5));

                _thWrk.Report(new bgwText("Clearing DB Status"));
                _thWrk.Report(_progressCounter++);
                RepairStatus.ReportStatusReset(DB.DirRoot);

                if (checkCancel()) return;

                _thWrk.Report(new bgwText("Getting Selected Files"));
                _thWrk.Report(_progressCounter++);


                List<RvFile> filesGot = new List<RvFile>();
                List<RvFile> filesMissing = new List<RvFile>();
                GetSelectedFiles(DB.DirRoot, true, filesGot, filesMissing);
                if (checkCancel()) return;


                _thWrk.Report(new bgwText("Group all files by CRC (Sorted)"));
                _thWrk.Report(_progressCounter++);
                MergeGotFiles(filesGot, out FileGroup[] fileGroupsCRCSorted);
                if (checkCancel()) return;

                _thWrk.Report(new bgwText("Creating Secondary Indexes"));
                _thWrk.Report(_progressCounter++);


                FileGroup[] fileGroupsSHA1Sorted = null;
                FileGroup[] fileGroupsMD5Sorted = null;
                FileGroup[] fileGroupsAltCRCSorted = null;
                FileGroup[] fileGroupsAltSHA1Sorted = null;
                FileGroup[] fileGroupsAltMD5Sorted = null;

                Thread t1 = new Thread(() => FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindSHA1, FamilySortSHA1, out fileGroupsSHA1Sorted));
                Thread t2 = new Thread(() => FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindMD5, FamilySortMD5, out fileGroupsMD5Sorted));
                Thread t3 = new Thread(() => FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindAltCRC, FamilySortAltCRC, out fileGroupsAltCRCSorted));
                Thread t4 = new Thread(() => FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindAltSHA1, FamilySortAltSHA1, out fileGroupsAltSHA1Sorted));
                Thread t5 = new Thread(() => FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindAltMD5, FamilySortAltMD5, out fileGroupsAltMD5Sorted));

                t1.Start();
                t2.Start();
                t3.Start();
                t4.Start();
                t5.Start();

                t1.Join();
                t2.Join();
                t3.Join();
                t4.Join();
                t5.Join();

                /*
                _thWrk.Report(new bgwText("Index creation on got SHA1"));
                _thWrk.Report(_progressCounter++);
                FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindSHA1, FamilySortSHA1, out FileGroup[] fileGroupsSHA1Sorted);
                if (checkCancel()) return;

                _thWrk.Report(new bgwText("Index creation on got MD5"));
                _thWrk.Report(_progressCounter++);
                FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindMD5, FamilySortMD5, out FileGroup[] fileGroupsMD5Sorted);
                if (checkCancel()) return;

                // next make another sorted list of got files on the AltCRC
                // these are the same FileGroup classes as in the fileGroupsCRCSorted List, just sorted by AltCRC
                // if the files does not have an altCRC then it is not added to this list.
                _thWrk.Report(new bgwText("Index creation on got AltCRC"));
                _thWrk.Report(_progressCounter++);
                FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindAltCRC, FamilySortAltCRC, out FileGroup[] fileGroupsAltCRCSorted);
                if (checkCancel()) return;

                _thWrk.Report(new bgwText("Index creation on got AltSHA1"));
                _thWrk.Report(_progressCounter++);
                FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindAltSHA1, FamilySortAltSHA1, out FileGroup[] fileGroupsAltSHA1Sorted);
                if (checkCancel()) return;

                _thWrk.Report(new bgwText("Index creation on got AltMD5"));
                _thWrk.Report(_progressCounter++);
                FastArraySort.SortWithFilter(fileGroupsCRCSorted, FindAltMD5, FamilySortAltMD5, out FileGroup[] fileGroupsAltMD5Sorted);
                
                */
                if (checkCancel()) return;

                _thWrk.Report(new bgwText("Merging in missing file list"));
                _thWrk.Report(_progressCounter++);
                // try and merge the missing File list into the FileGroup classes
                // using the altCRC sorted list and then the CRCSorted list
                MergeInMissingFiles(fileGroupsCRCSorted, fileGroupsSHA1Sorted, fileGroupsMD5Sorted, fileGroupsAltCRCSorted, fileGroupsAltSHA1Sorted, fileGroupsAltMD5Sorted, filesMissing);
                if (checkCancel()) return;

                int totalAfterMerge = fileGroupsCRCSorted.Length;

                _thWrk.Report(new bgwText("Finding Fixes"));
                _thWrk.Report(_progressCounter++);
                FindFixesListCheck.GroupListCheck(fileGroupsCRCSorted);

                ClearPartial.checkGroups.Clear();
                ClearPartial.CheckRemovePartial(DB.DirRoot.Child(0));
                ClearPartial.checkAllGroups();

                _thWrk.Report(new bgwText("Complete (Unique Files " + totalAfterMerge + ")"));
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

        internal static void GetSelectedFiles(RvFile val, bool selected, List<RvFile> gotFiles, List<RvFile> missingFiles)
        {
            if (selected)
            {
                RvFile rvFile = val;
                if (rvFile.IsFile)
                {
                    switch (rvFile.GotStatus)
                    {
                        case GotStatus.Got:
                        case GotStatus.Corrupt:
                            gotFiles.Add(rvFile);
                            break;
                        case GotStatus.NotGot:
                            missingFiles.Add(rvFile);
                            break;
                    }
                }
            }

            RvFile rvVal = val;
            if (!rvVal.IsDirectory) return;

            for (int i = 0; i < rvVal.ChildCount; i++)
            {
                bool nextSelect = selected;
                if (rvVal.Tree != null)
                    nextSelect = rvVal.Tree.Checked != RvTreeRow.TreeSelect.UnSelected;
                GetSelectedFiles(rvVal.Child(i), nextSelect, gotFiles, missingFiles);
            }
        }

        // we are adding got files so we can assume a few things:
        //  these got files may be level 1 or level 2 scanned.
        //
        //  If they are just level 1 then there may or may not be SHA1 / MD5 info, which is unvalidated.
        //  So: We will always have CRC & Size info at a minimum and may also have SHA1 / MD5
        //
        //  Next: Due to the possilibity of CRC hash collisions 
        //  we could find matching CRC that have different SHA1 & MD5
        //  so we should seach for one or more matching CRC/Size sets.
        //  then check to see if we have anything else that matches and then either:
        //  add the rom to an existing set or make a new set.


        internal static void MergeGotFiles(IEnumerable<RvFile> gotFilesSortedByCRC, out FileGroup[] fileGroups)
        {
            ByteSortedList<FileGroup, RvFile> listFileGroupsOut = new ByteSortedList<FileGroup, RvFile>(getByteFunc, CompareCRC, newFunc, mergeFunc);

            // insert a zero byte file.
            RvFile fileZero = RvFile.MakeFileZero();
            listFileGroupsOut.AddFind(fileZero);

            int pCount = Environment.ProcessorCount - 1;
            if (pCount == 0)
                pCount = 1;

            ParallelOptions po = new ParallelOptions { MaxDegreeOfParallelism = pCount };

            Parallel.ForEach(gotFilesSortedByCRC, po, (file, state) =>
            {
                if (file.CRC == null)
                    return;
                listFileGroupsOut.AddFindWithExact(file, exactFunc);
                if (_thWrk != null && _thWrk.CancellationPending)
                {
                    state.Break();
                }
            });
            if (_thWrk != null && _thWrk.CancellationPending)
            {
                fileGroups = null;
                return;
            }
            fileGroups = listFileGroupsOut.ToArray();
        }
        private static byte getByteFunc(RvFile v1)
        {
            return v1.CRC[0];
        }
        private static FileGroup newFunc(RvFile file)
        {
            return new FileGroup(file);
        }
        private static void mergeFunc(RvFile file, FileGroup group)
        {
            group.MergeFileIntoGroup(file);
        }
        private static bool exactFunc(RvFile file, FileGroup group)
        {
            return group.FindExactMatch(file);
        }





        public static void MergeInMissingFiles(FileGroup[] mergedCRCFamily, FileGroup[] mergedSHA1Family, FileGroup[] mergedMD5Family,
                                                FileGroup[] mergedAltCRCFamily, FileGroup[] mergedAltSHA1Family, FileGroup[] mergedAltMD5Family, List<RvFile> missingFiles)
        {
            foreach (RvFile f in missingFiles)
            {
                if (_thWrk.CancellationPending)
                    return;

                //first try and match on CRC
                //if (f.CRC != null)
                //{
                if (f.AltSize != null || f.AltCRC != null || f.AltSHA1 != null || f.AltMD5 != null)
                {
                    throw new InvalidOperationException("Missing files cannot have alt values");
                }


                if (f.HeaderFileType != HeaderFileType.Nothing)
                {

                    if (f.CRC != null)
                    {
                        bool found = FindMissingOnAlt(f, CompareAltCRC, mergedAltCRCFamily);
                        if (found)
                            continue;
                    }

                    if (f.SHA1 != null)
                    {
                        bool found = FindMissingOnAlt(f, CompareAltSHA1, mergedAltSHA1Family);
                        if (found)
                            continue;
                    }
                    if (f.MD5 != null)
                    {
                        bool found = FindMissingOnAlt(f, CompareAltMD5, mergedAltMD5Family);
                        if (found)
                            continue;
                    }
                }

                if (f.CRC != null)
                {
                    bool found = FindMissing(f, CompareCRC, mergedCRCFamily);
                    if (found)
                        continue;
                }
                if (f.SHA1 != null)
                {
                    bool found = FindMissing(f, CompareSHA1, mergedSHA1Family);
                    if (found)
                        continue;
                }
                if (f.MD5 != null)
                {
                    bool found = FindMissing(f, CompareMD5, mergedMD5Family);
                    if (found)
                        continue;
                }

                if (f.CRC == null && f.SHA1 == null && f.MD5 == null)
                {

                    if (f.Size == 0)
                    {
                        mergedCRCFamily[0].MergeFileIntoGroup(f);
                    }
                    else if (f.Size == null && f.Name.Length > 1 && f.Name.Substring(f.Name.Length - 1, 1) == "/")
                    {
                        mergedCRCFamily[0].MergeFileIntoGroup(f);
                    }
                }
            }
        }



        private static bool FindMissing(RvFile f, Compare comp, FileGroup[] mergedFamily)
        {
            bool found = FindMatch(mergedFamily, f, comp, FileGroup.FindExactMatch, out List<int> index);

            if (index.Count > 1)
            {
                // if there is more than one exact match this means there is kind of a big mess going on, and things should
                // probably be level 2 scanned, will just use the first found set, but should probably report in the error log
                // that things are not looking good.
            }

            if (!found)
                return false;

            mergedFamily[index[0]].MergeFileIntoGroup(f);
            return true;

        }

        private static bool FindMissingOnAlt(RvFile f, Compare comp, FileGroup[] mergedFamily)
        {
            bool found = FindMatch(mergedFamily, f, comp, FileGroup.FindAltExactMatch, out List<int> index);
            if (!found)
                return false;

            mergedFamily[index[0]].MergeAltFileIntoGroup(f);
            return true;

        }


        internal delegate bool ExactMatch(FileGroup fTest, RvFile file);
        internal static bool FindMatch(FileGroup[] fileGroups, RvFile file, Compare comp, ExactMatch match, out List<int> listIndex)
        {
            int intBottom = 0;
            int intTop = fileGroups.Length;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop) / 2;

                FileGroup ff = fileGroups[intMid];
                intRes = comp(file, ff);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            int index = intMid;

            listIndex = new List<int>();

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while (index > 0 && intRes1 == 0)
                {
                    FileGroup ff = fileGroups[index - 1];
                    intRes1 = comp(file, ff);

                    if (intRes1 != 0) continue;
                    index--;
                }

                int indexFirst = index;

                intTop = fileGroups.Length;
                intRes1 = 0;
                while (index < intTop && intRes1 == 0)
                {
                    FileGroup ff = fileGroups[index];
                    intRes1 = comp(file, ff);
                    if (intRes1 != 0) continue;
                    if (match(ff, file))
                    {
                        listIndex.Add(index);
                    }
                    index++;
                }

                if (listIndex.Count == 0)
                {
                    listIndex.Add(indexFirst);
                    intRes = -1;
                }
            }
            // if the search is greater than the closest match move one up the list
            else
            {
                if (intRes > 0)
                {
                    index++;
                }
                listIndex.Add(index);
            }

            return intRes == 0;
        }

        internal static bool FindMatch(List<FileGroup> fileGroups, RvFile file, Compare comp, ExactMatch match, out List<int> listIndex)
        {
            int gCount = fileGroups.Count;

            int intBottom = 0;
            int intTop = gCount;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop) / 2;

                FileGroup ff = fileGroups[intMid];
                intRes = comp(file, ff);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            int index = intMid;

            listIndex = new List<int>();

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while (index > 0 && intRes1 == 0)
                {
                    FileGroup ff = fileGroups[index - 1];
                    intRes1 = comp(file, ff);

                    if (intRes1 != 0) continue;
                    index--;
                }

                int indexFirst = index;

                intTop = gCount;
                intRes1 = 0;
                while (index < intTop && intRes1 == 0)
                {
                    FileGroup ff = fileGroups[index];
                    intRes1 = comp(file, ff);
                    if (intRes1 != 0) continue;
                    if (match(ff, file))
                    {
                        listIndex.Add(index);
                    }
                    index++;
                }

                if (listIndex.Count == 0)
                {
                    listIndex.Add(indexFirst);
                    intRes = -1;
                }
            }
            // if the search is greater than the closest match move one up the list
            else
            {
                if (intRes > 0)
                {
                    index++;
                }
                listIndex.Add(index);
            }

            return intRes == 0;
        }


        internal delegate int Compare(RvFile file, FileGroup fileGroup);

        private static int CompareCRC(RvFile file, FileGroup fileGroup)
        {
            return ArrByte.ICompare(file.CRC, fileGroup.CRC);
        }
        private static int CompareSHA1(RvFile file, FileGroup fileGroup)
        {
            return ArrByte.ICompare(file.SHA1, fileGroup.SHA1);
        }
        private static int CompareMD5(RvFile file, FileGroup fileGroup)
        {
            return ArrByte.ICompare(file.MD5, fileGroup.MD5);
        }
        private static int CompareAltCRC(RvFile file, FileGroup fileGroup)
        {
            return ArrByte.ICompare(file.CRC, fileGroup.AltCRC);
        }
        private static int CompareAltSHA1(RvFile file, FileGroup fileGroup)
        {
            return ArrByte.ICompare(file.SHA1, fileGroup.AltSHA1);
        }
        private static int CompareAltMD5(RvFile file, FileGroup fileGroup)
        {
            return ArrByte.ICompare(file.MD5, fileGroup.AltMD5);
        }


        public static bool FindSHA1(FileGroup fileGroup)
        {
            return fileGroup.SHA1 != null;
        }
        public static bool FindMD5(FileGroup fileGroup)
        {
            return fileGroup.MD5 != null;
        }
        public static bool FindAltCRC(FileGroup fileGroup)
        {
            return fileGroup.AltCRC != null;
        }
        public static bool FindAltSHA1(FileGroup fileGroup)
        {
            return fileGroup.AltSHA1 != null;
        }
        public static bool FindAltMD5(FileGroup fileGroup)
        {
            return fileGroup.AltMD5 != null;
        }


        public static int FamilySortSHA1(FileGroup fileGroup1, FileGroup fileGroup2)
        {
            return ArrByte.ICompare(fileGroup1.SHA1, fileGroup2.SHA1);
        }
        public static int FamilySortMD5(FileGroup fileGroup1, FileGroup fileGroup2)
        {
            return ArrByte.ICompare(fileGroup1.MD5, fileGroup2.MD5);
        }
        public static int FamilySortAltCRC(FileGroup fileGroup1, FileGroup fileGroup2)
        {
            return ArrByte.ICompare(fileGroup1.AltCRC, fileGroup2.AltCRC);
        }
        public static int FamilySortAltSHA1(FileGroup fileGroup1, FileGroup fileGroup2)
        {
            return ArrByte.ICompare(fileGroup1.AltSHA1, fileGroup2.AltSHA1);
        }
        public static int FamilySortAltMD5(FileGroup fileGroup1, FileGroup fileGroup2)
        {
            return ArrByte.ICompare(fileGroup1.AltMD5, fileGroup2.AltMD5);
        }


    }
}
