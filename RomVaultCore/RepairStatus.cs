using System;
using System.Collections.Generic;
using System.Threading;
using RomVaultCore.RvDB;

namespace RomVaultCore
{
    public enum RepStatus
    {
        // Scanning Status:
        Error,

        UnSet,

        UnScanned,

        DirCorrect,
        DirMissing,
        DirUnknown,
        DirInToSort,
        DirCorrupt,


        Missing, // a files or directory from a DAT that we do not have
        Correct, // a files or directory from a DAT that we have
        NotCollected, // a file from a DAT that is not collected that we do not have (either a merged or bad file.)
        UnNeeded, // a file from a DAT that is not collected that we do have, and so do not need. (a merged file in a child set)
        Unknown, // a file that is not in a DAT
        InToSort, // a file that is in the ToSort directory

        Corrupt, // either a Zip file that is corrupt, or a Zipped file that is corrupt
        Ignore, // a file found in the ignore list


        // Fix Status:
        CanBeFixed, // a missing file that can be fixed from another file. (Will be set to correct once it has been corrected)
        MoveToSort, // a file that is not in any DAT (Unknown) and should be moved to ToSort
        Delete, // a file that can be deleted 
        NeededForFix, // a file that is Unknown where it is, but is needed somewhere else.
        Rename, // a file that is Unknown where it is, but is needed with other name inside the same Zip.

        CorruptCanBeFixed, // a corrupt file that can be replaced and fixed from another file.
        MoveToCorrupt, // a corrupt file that should just be moved out the way to a corrupt directory in ToSort.

        Deleted, // this is a temporary value used while fixing sets, this value should never been seen.


        IncompleteRemove,
        Incomplete,


        // Reporting Only Status

        MissingMIA,
        CorrectMIA,
        CanBeFixedMIA,


        EndValue
    }

    public static class RepairStatus
    {
        public static List<RepStatus>[,,] StatusCheck;

        public static RepStatus[] DisplayOrder;


        public static void InitStatusCheck()
        {
            StatusCheck = new List<RepStatus>
                [
                Enum.GetValues(typeof(FileType)).Length,
                Enum.GetValues(typeof(DatStatus)).Length,
                Enum.GetValues(typeof(GotStatus)).Length
                ];

            //sorted alphabetically
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.DirMissing };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirCorrect };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirInToSort };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirUnknown };

            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.DirMissing };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirCorrect };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirInToSort };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirUnknown };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };

            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.DirMissing };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirCorrect };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirInToSort };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirUnknown };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };

            // 3 * 6 * 4 = 72 total combinations
            //
            // 20 un-coded combinations

            //StatusCheck[(int)FileType.File, (int)DatStatus.InDatNoDump, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            //StatusCheck[(int)FileType.File, (int)DatStatus.InDatNoDump, (int)GotStatus.Got] = new List<RepStatus> { };
            //StatusCheck[(int)FileType.File, (int)DatStatus.InDatNoDump, (int)GotStatus.Corrupt] = new List<RepStatus> { };
            //StatusCheck[(int)FileType.File, (int)DatStatus.InDatNoDump, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Missing, RepStatus.CanBeFixed, RepStatus.Incomplete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct, RepStatus.IncompleteRemove, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.CorruptCanBeFixed };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.UnNeeded, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.InToSort, RepStatus.Ignore, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Unknown, RepStatus.Ignore, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };

            //StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            //StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            //StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.Corrupt] = new List<RepStatus> { };
            //StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.FileLocked] = new List<RepStatus> { };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Missing, RepStatus.CanBeFixed, RepStatus.Incomplete };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct, RepStatus.IncompleteRemove, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.CorruptCanBeFixed };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatMerged, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatMerged, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.UnNeeded, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatMerged, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InDatMerged, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.InToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Unknown, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.FileZip, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };

            //StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            //StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            //StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.Corrupt] = new List<RepStatus> { };
            //StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatNoDump, (int)GotStatus.FileLocked] = new List<RepStatus> { };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Missing, RepStatus.CanBeFixed, RepStatus.Incomplete };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct, RepStatus.IncompleteRemove, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.CorruptCanBeFixed };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatMerged, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatMerged, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.UnNeeded, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatMerged, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InDatMerged, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.InToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Unknown, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.FileSevenZip, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };


            DisplayOrder = new[]
            {
                RepStatus.Error,
                RepStatus.UnSet,
                RepStatus.UnScanned,

                //RepStatus.DirCorrect,
                //RepStatus.DirCorrectBadCase,
                //RepStatus.DirMissing,
                //RepStatus.DirUnknown,
                RepStatus.DirCorrupt,
                RepStatus.MoveToCorrupt,
                RepStatus.CorruptCanBeFixed,
                RepStatus.CanBeFixedMIA,
                RepStatus.CanBeFixed,
                RepStatus.MoveToSort,
                RepStatus.Delete,
                RepStatus.NeededForFix,
                RepStatus.Rename,
                RepStatus.Corrupt,
                RepStatus.Unknown,
                RepStatus.UnNeeded,
                RepStatus.Incomplete,
                RepStatus.Missing,
                RepStatus.MissingMIA,
                RepStatus.CorrectMIA,
                RepStatus.Correct,
                RepStatus.InToSort,
                RepStatus.NotCollected,
                RepStatus.Ignore,
                RepStatus.Deleted
            };
        }

        public static void ReportStatusReset(RvFile tFile)
        {
            tFile.RepStatusReset();
            tFile.FileGroup = null;

            FileType ftBase = tFile.FileType;
            if (ftBase != FileType.Zip && ftBase != FileType.SevenZip && ftBase != FileType.Dir)
            {
                return;
            }

            RvFile tDir = tFile;

            for (int i = 0; i < tDir.ChildCount; i++)
            {
                ReportStatusReset(tDir.Child(i));
            }
        }
    }

    public class ReportStatus
    {
        private int[] _arrRepStatus;

        private int[] EnsureStatusArray()
        {
            int[] status = Volatile.Read(ref _arrRepStatus);
            if (status != null)
                return status;

            int[] newStatus = new int[(int)RepStatus.EndValue];
            Interlocked.CompareExchange(ref _arrRepStatus, newStatus, null);
            return _arrRepStatus;
        }

        public void RepStatusArrayAddRemove(ReportStatus rs, int direction)
        {
            if (rs._arrRepStatus == null)
                return;

            int[] status = EnsureStatusArray();
            for (int i = 0; i < status.Length; i++)
            {
                status[i] += rs.Get((RepStatus)i) * direction;
            }
        }


        public static RepStatus UIStatus(MIAStatus mStat, RepStatus rStat)
        {
            if ((mStat & MIAStatus.MIAFromDat) > 0)
            {
                switch (rStat)
                {
                    case RepStatus.Missing: return RepStatus.MissingMIA;
                    case RepStatus.CanBeFixed: return RepStatus.CanBeFixedMIA;
                    case RepStatus.Correct: return RepStatus.CorrectMIA;
                }
            }

            if ((mStat & MIAStatus.MIA) > 0)
            {
                switch (rStat)
                {
                    case RepStatus.Missing: return RepStatus.MissingMIA;
                    case RepStatus.CanBeFixed: return RepStatus.CanBeFixedMIA;
                    case RepStatus.Correct:
                        return RepStatus.CorrectMIA;
                }
            }
            return rStat;
        }

        public void RepStatusAddRemove(RepStatus rs, int dir, MIAStatus mStat)
        {
            RepStatus trsNew = UIStatus(mStat, rs);
            EnsureStatusArray()[(int)trsNew] += dir;
        }

        public void RepStatusUpdate(MIAStatus mStatOld, MIAStatus mStatNew, RepStatus rsStat)
        {
            RepStatus trsOld = UIStatus(mStatOld, rsStat);
            RepStatus trsNew = UIStatus(mStatNew, rsStat);

            int[] status = EnsureStatusArray();
            Interlocked.Decrement(ref status[(int)trsOld]);
            Interlocked.Increment(ref status[(int)trsNew]);
        }

        public void RepStatusUpdate(RepStatus rsOld, RepStatus rsNew, MIAStatus mStat)
        {
            RepStatus trsOld = UIStatus(mStat, rsOld);
            RepStatus trsNew = UIStatus(mStat, rsNew);

            int[] status = EnsureStatusArray();
            Interlocked.Decrement(ref status[(int)trsOld]);
            Interlocked.Increment(ref status[(int)trsNew]);
        }

        #region "arrGotStatus Processing"

        public int Get(RepStatus v)
        {
            int[] status = Volatile.Read(ref _arrRepStatus);
            return status == null ? 0 : status[(int)v];
        }

        public int CountCorrect()
        {
            return Get(RepStatus.Correct) + Get(RepStatus.CorrectMIA);
        }

        public int CountMIA()
        {
            return Get(RepStatus.MissingMIA);
        }
        public int CountFoundMIA()
        {
            return Get(RepStatus.CorrectMIA);
        }

        public bool HasCorrect()
        {
            return CountCorrect() > 0;
        }

        public int CountMissing(bool includeMIA = false)
        {
            return Get(RepStatus.Missing) +
                   (includeMIA ? Get(RepStatus.MissingMIA) : 0) +
                   Get(RepStatus.DirCorrupt) +
                   Get(RepStatus.Corrupt) +
                   Get(RepStatus.CanBeFixed) +
                   Get(RepStatus.CanBeFixedMIA) +
                   Get(RepStatus.CorruptCanBeFixed) +
                   Get(RepStatus.MoveToCorrupt) +
                   Get(RepStatus.Incomplete);
        }

        public bool HasMissing(bool includeMIA = false)
        {
            return CountMissing(includeMIA) > 0;
        }
        public bool HasMIA()
        {
            return CountMIA() > 0;
        }


        public int CountFixesNeeded()
        {
            return Get(RepStatus.CanBeFixed) +
                   Get(RepStatus.CanBeFixedMIA) +
                   Get(RepStatus.MoveToSort) +
                   Get(RepStatus.Delete) +
                   Get(RepStatus.NeededForFix) +
                   Get(RepStatus.Rename) +
                   Get(RepStatus.CorruptCanBeFixed) +
                   Get(RepStatus.MoveToCorrupt);
        }

        public bool HasFixesNeeded()
        {
            return CountFixesNeeded() > 0;
        }
        public bool HasAllMerged()
        {
            return Get(RepStatus.NotCollected) > 0 && CountAnyFiles() == 0;
        }

        public int CountCanBeFixed()
        {
            return Get(RepStatus.CanBeFixed) +
                   Get(RepStatus.CanBeFixedMIA) +
                   Get(RepStatus.CorruptCanBeFixed);
        }

        private int CountFixable()
        {
            return Get(RepStatus.CanBeFixed) +
                   Get(RepStatus.CanBeFixedMIA) +
                   Get(RepStatus.CorruptCanBeFixed) +

                   Get(RepStatus.MoveToSort) +
                   Get(RepStatus.Delete) +
                   Get(RepStatus.MoveToCorrupt);
        }


        public bool FixCheckFilesCanBeFixed()
        {
            return (
                    Get(RepStatus.CanBeFixed) +
                    Get(RepStatus.CanBeFixedMIA) +
                    Get(RepStatus.CorruptCanBeFixed)) > 0;
        }

        public bool FixCheckHasNeededForFix()
        {
            return Get(RepStatus.NeededForFix) > 0;
        }

        public bool FixCheckHasFilesToBeRemoved()
        {
            return (
                    Get(RepStatus.MoveToSort) +
                    Get(RepStatus.Delete) +
                    Get(RepStatus.MoveToCorrupt)) > 0;
        }


        public bool HasFixable()
        {
            return CountFixable() > 0;
        }

        private int CountAnyFiles()
        {
            // this list include probably more status's than are needed, but all are here to double check I don't delete something I should not.
            return Get(RepStatus.Correct) +
                   Get(RepStatus.CorrectMIA) +
                   Get(RepStatus.UnNeeded) +
                   Get(RepStatus.Unknown) +
                   Get(RepStatus.InToSort) +
                   Get(RepStatus.Corrupt) +
                   Get(RepStatus.Ignore) +
                   Get(RepStatus.MoveToSort) +
                   Get(RepStatus.Delete) +
                   Get(RepStatus.NeededForFix) +
                   Get(RepStatus.Rename) +
                   Get(RepStatus.MoveToCorrupt);
        }

        public bool HasAnyFiles()
        {
            return CountAnyFiles() > 0;
        }

        public int CountUnknown()
        {
            return Get(RepStatus.Unknown);
        }

        public bool HasUnknown()
        {
            return CountUnknown() > 0;
        }


        public int CountInToSort()
        {
            return Get(RepStatus.InToSort);
        }

        public bool HasInToSort()
        {
            return CountInToSort() > 0;
        }

        #endregion
    }
}
