using DATReader.DatStore;
using RVIO;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void MakeDatSingleLevel(DatHeader tDatHeader, bool useDescription, RemoveSubType subDirType, bool isFiles)
        {
            // KeepAllSubDirs, just does what it says
            // RemoveAllSubDirs, just does what it says
            // RemoveAllIfNoConflicts, does the conflict precheck and if a conflict is found switches to KeepAllSubDirs
            // RemoveSubsIfSingleFile, remove the subdir if there is only one file in the subdir

            DatBase[] db = tDatHeader.BaseDir.ToArray();
            tDatHeader.Dir = "noautodir";

            string rootDirName = "";
            if (string.IsNullOrEmpty(rootDirName) && useDescription && !string.IsNullOrWhiteSpace(tDatHeader.Description))
                rootDirName = tDatHeader.Description;
            if (string.IsNullOrEmpty(rootDirName))
                rootDirName = tDatHeader.Name;


            // do a pre check to see if removing all the sub-dirs will give any name conflicts
            if (subDirType == RemoveSubType.RemoveAllIfNoConflicts)
            {
                bool foundRepeatFilename = false;
                DatDir rootTest = new DatDir(rootDirName, DatFileType.UnSet);
                foreach (DatBase set in db)
                {
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

            DatDir root;
            if (isFiles)
            {
                root = tDatHeader.BaseDir;
            }
            else
            {
                root = new DatDir(rootDirName, DatFileType.UnSet)
                {
                    DGame = new DatGame { Description = tDatHeader.Description }
                };
                tDatHeader.BaseDir.ChildAdd(root);
            }

            foreach (DatBase set in db)
            {
                string dirName = set.Name;
                if (!(set is DatDir romSet))
                    continue;
                DatBase[] dbr = romSet.ToArray();
                foreach (DatBase rom in dbr)
                {
                    if (subDirType == RemoveSubType.RemoveSubIfSingleFiles)
                    {
                        if (dbr.Length != 1)
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

    }
}
