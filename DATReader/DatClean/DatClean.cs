
using System;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void MakeDatSingleLevel(DatHeader tDatHeader)
        {
            DatBase[] db = tDatHeader.BaseDir.ToArray();
            tDatHeader.Dir = "noautodir";

            tDatHeader.BaseDir.ChildrenClear();
            DatDir root = new DatDir(DatFileType.UnSet)
            {
                Name = tDatHeader.Name,
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
                    rom.Name = dirName + "\\" + rom.Name;
                    root.ChildAdd(rom);
                }
            }
        }

        public static void DirectoryExpand(DatDir dDir, bool checkFilename = false)
        {
            DatBase[] arrDir = dDir.ToArray();
            bool foundSubDir = false;
            foreach (DatBase db in arrDir)
            {
                if (!checkFilename && !(db is DatDir))
                    continue;
                if (!db.Name.Contains("\\"))
                    continue;
                foundSubDir = true;
                break;
            }

            if (foundSubDir)
            {
                dDir.ChildrenClear();
                foreach (DatBase db in arrDir)
                {
                    if (!checkFilename && !(db is DatDir))
                    {
                        dDir.ChildAdd(db);
                        continue;
                    }
                    if (!db.Name.Contains("\\"))
                    {
                        dDir.ChildAdd(db);
                        continue;
                    }

                    string dirName = db.Name;
                    int split = dirName.IndexOf("\\", StringComparison.Ordinal);
                    string part0 = dirName.Substring(0, split);
                    string part1 = dirName.Substring(split + 1);

                    db.Name = part1;
                    DatDir dirFind = new DatDir(db.DatFileType) { Name = part0 };
                    if (dDir.ChildNameSearch(dirFind, out int index) != 0)
                    {
                        dDir.ChildAdd(dirFind);
                    }
                    else
                    {
                        dirFind = (DatDir)dDir.Child(index);
                    }
                    dirFind.ChildAdd(db);
                }

                arrDir = dDir.ToArray();
            }

            foreach (DatBase db in arrDir)
            {
                if (!(db is DatDir dbDir))
                    continue;
                DirectoryExpand(dbDir, checkFilename);
            }
        }
    }
}
