
using System;
using DATReader.DatStore;
using RVIO;

namespace DATReader.DatClean
{
    public enum RemoveSubType
    {
        KeepAllSubDirs,
        RemoveAllSubDirs,
        RemoveAllIfNoConflicts,
        RemoveSubIfNameMatches,
        RemoveIndividualIfNoConflicts
    }


    public static partial class DatClean
    {
        public static void MakeDatSingleLevel(DatHeader tDatHeader, bool useDescription, RemoveSubType subDirType)
        {
            // KeepAllSubDirs, just does what it says
            // RemoveAllSubDirs, just does what it says
            // RemoveallIfNoConflicts, does the conflict precheck and if a conflict is found switches to KeepAllSubDirs
            // RemoveSubIfNameMatches, will remove the subdir if the rom and game name match (without extentions) and there is only one rom in the game


            DatBase[] db = tDatHeader.BaseDir.ToArray();
            tDatHeader.Dir = "noautodir";

            string rootDirName = "";
            if (string.IsNullOrEmpty(rootDirName) && useDescription &&
                !string.IsNullOrWhiteSpace(tDatHeader.Description))
                rootDirName = tDatHeader.Description;
            if (string.IsNullOrEmpty(rootDirName))
                rootDirName = tDatHeader.Name;


            // do a pre check to see if removing all the sub-dirs will give any name conflicts
            if (subDirType == RemoveSubType.RemoveAllIfNoConflicts)
            {
                bool foundRepeatFilename = false;
                DatDir rootTest = new DatDir(DatFileType.UnSet)
                {
                    Name = rootDirName,
                    DGame = new DatGame { Description = tDatHeader.Description }
                };
                foreach (DatBase set in db)
                {
                    string dirName = set.Name;
                    if (!(set is DatDir romSet))
                        continue;
                    DatBase[] dbr = romSet.ToArray();
                    foreach (DatBase rom in dbr)
                    {
                        int f = rootTest.ChildNameSearch(rom, out int _);
                        if (f == 0)
                        {
                            foundRepeatFilename = true;
                            subDirType = RemoveSubType.KeepAllSubDirs;
                            break;
                        }
                        rootTest.ChildAdd(rom);
                    }

                    if (foundRepeatFilename)
                    {
                        break;
                    }
                }
            }

            tDatHeader.BaseDir.ChildrenClear();

            DatDir root = new DatDir(DatFileType.UnSet)
            {
                Name = rootDirName,
                DGame = new DatGame { Description = tDatHeader.Description }
            };
            tDatHeader.BaseDir.ChildAdd(root);

            foreach (DatBase set in db)
            {
                string dirName = set.Name;
                if (!(set is DatDir romSet))
                    continue;
                DatBase[] dbr = romSet.ToArray();
                foreach (DatBase rom in dbr)
                {
                    if (subDirType == RemoveSubType.RemoveSubIfNameMatches)
                    {
                        if (dbr.Length != 1 || Path.GetFileNameWithoutExtension(rom.Name) != dirName)
                            rom.Name = dirName + "\\" + rom.Name;
                    }
                    else if (subDirType == RemoveSubType.KeepAllSubDirs)
                    {
                        rom.Name = dirName + "\\" + rom.Name;
                    }
                    root.ChildAdd(rom);
                }
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
                            DatDir dirFind = new DatDir(DatFileType.Dir) { Name = part0 };
                            if (dDir.ChildNameSearch(dirFind, out int index) != 0)
                            {
                                dDir.ChildAdd(dirFind);
                            }
                            else
                            {
                                dirFind = (DatDir)dDir.Child(index);
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
