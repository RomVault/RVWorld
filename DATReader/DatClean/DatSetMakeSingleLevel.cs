using DATReader.DatStore;
using RVIO;
using System.Collections.Generic;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void MakeDatSingleLevel(DatHeader tDatHeader, bool useDescription, RemoveSubType subDirType, bool isFiles, bool addCategory, List<string> catOrder)
        {
            // KeepAllSubDirs, just does what it says
            // RemoveAllSubDirs, just does what it says
            // RemoveAllIfNoConflicts, does the conflict precheck and if a conflict is found switches to KeepAllSubDirs
            // RemoveSubIfSingleFile, remove the subdir if there is only one file in the subdir
            // RemoveSubIfNameMathces, remove the subdir if the filename & dir name match

            DatBase[] originalBaseDir = tDatHeader.BaseDir.ToArray();
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
                DatDir rootTest = new DatDir(rootDirName, FileType.UnSet);
                foreach (DatBase set in originalBaseDir)
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

            DatGame dGame = new DatGame { Description = tDatHeader.Description };

            DatDir outDir;
            if (isFiles)
            {
                outDir = tDatHeader.BaseDir;
            }
            else
            {
                outDir = new DatDir(rootDirName, FileType.UnSet) { DGame = dGame };
                tDatHeader.BaseDir.ChildAdd(outDir);
            }

            foreach (DatBase set in originalBaseDir)
            {
                if (!(set is DatDir dirSet))
                    continue;

                //used to stores this sets Category so we only have to find it once, for this set if needed.
                //set to "-" to say we have not found the category yet, as null is a valid category reply, if there is not category.
                string setCategory = "-";

                DatBase[] dbr = dirSet.ToArray();
                foreach (DatBase rom in dbr)
                {
                    if (subDirType == RemoveSubType.KeepAllSubDirs)  // <-- RemoveSubType.RemoveAllIfNoConflicts
                    {
                        addBackDir(isFiles, outDir, dirSet, rom);
                        continue;
                    }
                    else if (subDirType == RemoveSubType.RemoveSubIfSingleFiles)
                    {
                        if (dbr.Length != 1)
                        {
                            addBackDir(isFiles, outDir, dirSet, rom);
                            continue;
                        }
                    }
                    else if (subDirType == RemoveSubType.RemoveSubIfNameMatches) // or multiple files with the same game
                    {
                        if (dbr.Length != 1)
                        {
                            addBackDir(isFiles, outDir, dirSet, rom);
                            continue;
                        }

                        // We now need to test if the rom name is the same as the set (game) name, if it is different we want to put the directory back in.
                        // If we are adding categories the set (game/dirSet) name would have had a category added to it, so we need to is if categories are being added
                        // and if they are we need to add the category and rom name together so we can correctly test if it matches the set name.
                        string testRomName = Path.GetFileNameWithoutExtension(rom.Name);
                        if (addCategory)
                        {
                            if (setCategory == "-")
                                setCategory = FindCategory(dirSet, catOrder);
                            if (!string.IsNullOrEmpty(setCategory))
                                testRomName = setCategory + "/" + testRomName;
                        }

                        // testRomName is the rom name possibly with the category added if needed.
                        if (testRomName != dirSet.Name)
                        {
                            addBackDir(isFiles, outDir, dirSet, rom);
                            continue;
                        }
                    }

                    // here is a problem if we have added categories onto the dir (Set) name, we are about to remove the dir name and the category name with it.
                    // so we need to add the category name to the rom name instead.
                    if (addCategory)
                    {
                        if (setCategory == "-")
                            setCategory = FindCategory(dirSet, catOrder);
                        if (!string.IsNullOrEmpty(setCategory))
                            rom.Name = setCategory + "/" + rom.Name;
                    }

                    outDir.ChildAdd(rom);
                }
            }
        }


        private static void addBackDir(bool isFiles, DatDir outDir, DatDir dirSet, DatBase rom)
        {
            // if we are working with files, then we actually have to add in an extra directory layer to the tree
            if (isFiles)
            {
                // this is essentially putting the original game dir (dirSet) back in place again.

                DatDir dirFind = new DatDir(dirSet.Name, FileType.UnSet) { DGame = dirSet.DGame };
                outDir.ChildNameSearchAdd(ref dirFind);
                dirFind.ChildAdd(rom);
                return;
            }

            // if we are working in archives we can just add the extra dir layer to the rom filenames
            rom.Name = dirSet.Name + "/" + rom.Name;
            outDir.ChildAdd(rom);
        }

    }
}
