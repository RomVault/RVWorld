using System;
using System.Collections.Generic;
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
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirCorrect };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.DirMissing };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirInToSort };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirUnknown };
            StatusCheck[(int)FileType.Dir, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };

            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirCorrect };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.DirMissing };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirInToSort };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirUnknown };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.Zip, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };

            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirCorrect };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.DirMissing };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirInToSort };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.DirUnknown };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.DirCorrupt };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.SevenZip, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };


            StatusCheck[(int)FileType.File, (int)DatStatus.InDatBad, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatBad, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.CorruptCanBeFixed };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Missing, RepStatus.CanBeFixed };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.UnNeeded, RepStatus.Delete, RepStatus.NeededForFix };
            StatusCheck[(int)FileType.File, (int)DatStatus.InDatMerged, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.InToSort, RepStatus.Ignore, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Unknown, RepStatus.Ignore, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.File, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };

            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatBad, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatBad, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.CorruptCanBeFixed };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Missing, RepStatus.CanBeFixed };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatMerged, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.UnNeeded, RepStatus.Delete, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InDatMerged, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.InToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Unknown, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.ZipFile, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };

            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatBad, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatBad, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.CorruptCanBeFixed };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Correct };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.FileLocked] = new List<RepStatus> { RepStatus.UnScanned };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatCollect, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Missing, RepStatus.CanBeFixed };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatMerged, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.UnNeeded, RepStatus.Delete, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InDatMerged, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.NotCollected };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InToSort, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InToSort, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.InToSort, RepStatus.NeededForFix, RepStatus.Delete };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.InToSort, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.NotInDat, (int)GotStatus.Corrupt] = new List<RepStatus> { RepStatus.Corrupt, RepStatus.MoveToCorrupt, RepStatus.Delete };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.NotInDat, (int)GotStatus.Got] = new List<RepStatus> { RepStatus.Unknown, RepStatus.Delete, RepStatus.MoveToSort, RepStatus.NeededForFix, RepStatus.Rename };
            StatusCheck[(int)FileType.SevenZipFile, (int)DatStatus.NotInDat, (int)GotStatus.NotGot] = new List<RepStatus> { RepStatus.Deleted };


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
                RepStatus.CanBeFixed,
                RepStatus.MoveToSort,
                RepStatus.Delete,
                RepStatus.NeededForFix,
                RepStatus.Rename,
                RepStatus.Corrupt,
                RepStatus.Unknown,
                RepStatus.UnNeeded,
                RepStatus.Missing,
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
        private readonly int[] _arrRepStatus = new int[(int)RepStatus.EndValue];

        public void UpdateRepStatus(ReportStatus rs, int dir)
        {
            for (int i = 0; i < _arrRepStatus.Length; i++)
            {
                _arrRepStatus[i] += rs.Get((RepStatus)i) * dir;
            }
        }

        public void UpdateRepStatus(RepStatus rs, int dir)
        {
            _arrRepStatus[(int)rs] += dir;
        }

        #region "arrGotStatus Processing"

        public int Get(RepStatus v)
        {
            return _arrRepStatus[(int)v];
        }

        public int CountCorrect()
        {
            return _arrRepStatus[(int)RepStatus.Correct];
        }

        public bool HasCorrect()
        {
            return CountCorrect() > 0;
        }

        public int CountMissing()
        {
            return _arrRepStatus[(int)RepStatus.UnScanned] +
                   _arrRepStatus[(int)RepStatus.Missing] +
                   _arrRepStatus[(int)RepStatus.DirCorrupt] +
                   _arrRepStatus[(int)RepStatus.Corrupt] +
                   _arrRepStatus[(int)RepStatus.CanBeFixed] +
                   _arrRepStatus[(int)RepStatus.CorruptCanBeFixed] +
                   _arrRepStatus[(int)RepStatus.MoveToCorrupt];
        }

        public bool HasMissing()
        {
            return CountMissing() > 0;
        }


        public int CountFixesNeeded()
        {
            return _arrRepStatus[(int)RepStatus.CanBeFixed] +
                   _arrRepStatus[(int)RepStatus.MoveToSort] +
                   _arrRepStatus[(int)RepStatus.Delete] +
                   _arrRepStatus[(int)RepStatus.NeededForFix] +
                   _arrRepStatus[(int)RepStatus.Rename] +
                   _arrRepStatus[(int)RepStatus.CorruptCanBeFixed] +
                   _arrRepStatus[(int)RepStatus.MoveToCorrupt];
        }

        public bool HasFixesNeeded()
        {
            return CountFixesNeeded() > 0;
        }
        public bool HasAllMerged()
        {
            return _arrRepStatus[(int)RepStatus.NotCollected]>0 && CountAnyFiles()==0;
        }

        public int CountCanBeFixed()
        {
            return _arrRepStatus[(int)RepStatus.CanBeFixed] +
                   _arrRepStatus[(int)RepStatus.CorruptCanBeFixed];
        }

        private int CountFixable()
        {
            return _arrRepStatus[(int)RepStatus.CanBeFixed] +
                   _arrRepStatus[(int)RepStatus.MoveToSort] +
                   _arrRepStatus[(int)RepStatus.Delete] +
                   _arrRepStatus[(int)RepStatus.CorruptCanBeFixed] +
                   _arrRepStatus[(int)RepStatus.MoveToCorrupt];
        }

        public bool HasFixable()
        {
            return CountFixable() > 0;
        }

        private int CountAnyFiles()
        {
            // this list include probably more status's than are needed, but all are here to double check I don't delete something I should not.
            return _arrRepStatus[(int)RepStatus.Correct] +
                   _arrRepStatus[(int)RepStatus.UnNeeded] +
                   _arrRepStatus[(int)RepStatus.Unknown] +
                   _arrRepStatus[(int)RepStatus.InToSort] +
                   _arrRepStatus[(int)RepStatus.Corrupt] +
                   _arrRepStatus[(int)RepStatus.Ignore] +
                   _arrRepStatus[(int)RepStatus.MoveToSort] +
                   _arrRepStatus[(int)RepStatus.Delete] +
                   _arrRepStatus[(int)RepStatus.NeededForFix] +
                   _arrRepStatus[(int)RepStatus.Rename] +
                   _arrRepStatus[(int)RepStatus.MoveToCorrupt];
        }

        public bool HasAnyFiles()
        {
            return CountAnyFiles() > 0;
        }

        public int CountUnknown()
        {
            return _arrRepStatus[(int)RepStatus.Unknown];
        }

        public bool HasUnknown()
        {
            return CountUnknown() > 0;
        }


        public int CountInToSort()
        {
            return _arrRepStatus[(int)RepStatus.InToSort];
        }

        public bool HasInToSort()
        {
            return CountInToSort() > 0;
        }

        #endregion
    }
}