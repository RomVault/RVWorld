using System;
using System.Collections.Generic;
using DATReader.DatStore;
using SortMethods;

namespace DATReader.DatClean
{
    public enum RemoveSubType
    {
        KeepAllSubDirs,
        RemoveAllSubDirs,
        RemoveAllIfNoConflicts,
        RemoveSubIfSingleFiles,
        RemoveSubIfNameMatches
    }

    public static partial class DatClean
    {
        public static void DirectoryFlattern(DatDir dDir)
        {
            List<DatDir> list = new List<DatDir>();
            DirectoryFlat(dDir, list, "");
            dDir.ChildrenClear();
            foreach (DatDir d in list)
            {
                dDir.ChildAdd(d);
            }
        }
        private static void DirectoryFlat(DatDir dDir, List<DatDir> newDir, string subDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                if (db is DatFile)
                    continue;

                DatDir lDB = (DatDir)db;
                if (lDB.DGame != null)
                {

                    lDB.Name = (string.IsNullOrWhiteSpace(subDir) ? "" : subDir + "\\") + db.Name;
                    newDir.Add(lDB);
                }
                else
                {
                    DirectoryFlat(lDB, newDir, (string.IsNullOrWhiteSpace(subDir) ? "" : subDir + "\\") + db.Name);
                }
            }
        }

        public static void ArchiveDirectoryFlattern(DatDir dDir)
        {

            if (dDir.DGame != null)
            {
                List<DatBase> list = new List<DatBase>();
                ArchiveFlat(dDir, list, "");
                dDir.ChildrenClear();
                foreach (DatBase d in list)
                {
                    dDir.ChildAdd(d);
                }
                return;
            }


            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                if (!(db is DatDir datDir))
                    continue;

                ArchiveDirectoryFlattern(datDir);
            }
        }


        private static void ArchiveFlat(DatDir dDir, List<DatBase> newDir, string subDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                string thisName = (string.IsNullOrWhiteSpace(subDir) ? "" : subDir + "/") + db.Name;
                if (db is DatFile)
                {
                    db.Name = thisName;
                    newDir.Add(db);
                    continue;
                }

                DatDir lDB = (DatDir)db;
                DatFile fDB = new DatFile(thisName + "/", FileType.UnSet);
                fDB.Size = 0;
                fDB.CRC = new byte[] { 0, 0, 0, 0 };
                newDir.Add(fDB);

                ArchiveFlat(lDB, newDir, thisName);
            }
        }


        public static void DirectorySort(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();

            dDir.ChildrenClear();
            foreach (DatDir d in arrDir)
            {
                dDir.ChildAdd(d);
            }
        }

        public static void DirectoryExpand(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            bool foundSubDir = false;
            foreach (DatBase db in arrDir)
            {
                if (CheckDir(db))
                {
                    if (db.Name.Contains("/"))
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
                        if (db.Name.Contains("/"))
                        {
                            string dirName = db.Name;
                            int split = dirName.IndexOf("/", StringComparison.Ordinal);
                            string part0 = dirName.Substring(0, split);
                            string part1 = dirName.Substring(split + 1);

                            db.Name = part1;

                            DatDir dirFind = new DatDir(part0, FileType.Dir);
                            dDir.ChildNameSearchAdd(ref dirFind);
                            
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
                        dbFile.DatStatus = DatStatus.InDatMerged;
                }
                if (db is DatDir ddir)
                {
                    CheckDeDuped(ddir);
                }

            }
        }

        private static bool CheckDir(DatBase db)
        {
            FileType dft = db.FileType;

            switch (dft)
            {
                // files inside of zips/7zips do not need to be expanded
                case FileType.FileSevenZip:
                case FileType.FileZip:
                    return false;
                // everything else should be fully expanded
                default:
                    return true;
            }
        }

        public static void AddCategory(DatDir tDat, List<string> catOrder)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir mGame))
                    continue;

                if (mGame.DGame == null)
                {
                    AddCategory(mGame, catOrder);
                    continue;
                }

                string cat = FindCategory(mGame, catOrder);
                if (cat != null)
                {
                    mGame.Name = cat + "/" + mGame.Name;
                }
            }
        }

        public static string FindCategory(DatDir mGame, List<string> catOrder)
        {
            if (mGame.DGame.Category == null || mGame.DGame.Category.Count == 0)
                return null;

            if (mGame.DGame.Category.Count == 1 && !string.IsNullOrWhiteSpace(mGame.DGame.Category[0]))
            {
                return mGame.DGame.Category[0];
            }
            int bestCat = 9999;
            foreach (string cat in mGame.DGame.Category)
            {
                if (string.IsNullOrWhiteSpace(cat))
                    continue;

                for (int i = 0; i < catOrder.Count; i++)
                {
                    if (catOrder[i].Equals(cat, StringComparison.OrdinalIgnoreCase))
                    {
                        if (i < bestCat)
                        {
                            bestCat = i;
                        }
                        break;
                    }
                }
            }
            if (bestCat != 9999)
            {
                return catOrder[bestCat];
            }
            return null;
        }
    }
}
