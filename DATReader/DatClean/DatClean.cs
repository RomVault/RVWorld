using System;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    public enum RemoveSubType
    {
        KeepAllSubDirs,
        RemoveAllSubDirs,
        RemoveAllIfNoConflicts,
        RemoveSubIfSingleFiles,
        RemoveSubIfNameMatches // <-- invalid and removed
    }


    public static partial class DatClean
    {

        public static void DirectoryExpand(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            bool foundSubDir = false;
            foreach (DatBase db in arrDir)
            {
                if (CheckDir(db))
                {
                    if (db.Name.Contains("\\"))
                    {
                        foundSubDir = true;
                        break;
                    }
                }
            }

            if (foundSubDir)
            {
                dDir.ChildrenClear();
                foreach (DatBase db in arrDir)
                {
                    if (CheckDir(db))
                    {
                        if (db.Name.Contains("\\"))
                        {
                            string dirName = db.Name;
                            int split = dirName.IndexOf("\\", StringComparison.Ordinal);
                            string part0 = dirName.Substring(0, split);
                            string part1 = dirName.Substring(split + 1);

                            db.Name = part1;
                            DatDir dirFind = new DatDir(part0, DatFileType.Dir);
                            if (dDir.ChildNameSearch(dirFind, out int index) != 0)
                            {
                                dDir.ChildAdd(dirFind);
                            }
                            else
                            {
                                dirFind = (DatDir)dDir[index];
                            }

                            if (part1.Length > 0)
                                dirFind.ChildAdd(db);
                            continue;
                        }
                    }
                    dDir.ChildAdd(db);
                }

                arrDir = dDir.ToArray();
            }

            foreach (DatBase db in arrDir)
            {
                if (db is DatDir dbDir)
                    DirectoryExpand(dbDir);
            }
        }

        public static void RemoveDeviceRef(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            if (arrDir == null)
                return;

            foreach (DatBase db in arrDir)
            {
                if (db is DatDir ddir)
                {
                    if (ddir.DGame != null)
                        ddir.DGame.device_ref = null;
                    RemoveDeviceRef(ddir);
                }

            }
        }

        public static void CheckDeDuped(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            if (arrDir == null)
                return;

            foreach (DatBase db in arrDir)
            {
                if (db is DatFile dbFile)
                {
                    if (dbFile.Status?.ToLower() == "deduped")
                        dbFile.DatStatus = DatFileStatus.InDatMerged;
                }
                if (db is DatDir ddir)
                {
                    CheckDeDuped(ddir);
                }

            }
        }

        private static bool CheckDir(DatBase db)
        {
            DatFileType dft = db.DatFileType;

            switch (dft)
            {
                // files inside of zips/7zips do not need to be expanded
                case DatFileType.File7Zip:
                case DatFileType.FileTorrentZip:
                    return false;
                // everything else should be fully expanded
                default:
                    return true;
            }
        }
    }
}
