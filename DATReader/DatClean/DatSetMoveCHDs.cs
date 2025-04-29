using DATReader.DatStore;
using System;
using System.Collections.Generic;

namespace DATReader.DatClean
{
    public static class DatSetMoveCHDs
    {

        public static void MoveUpCHDs(DatBase inDat, List<DatDir> parents = null)
        {
            if (parents == null)
                parents = new List<DatDir>();

            int parentCount = parents.Count;

            if (inDat is DatFile dFile)
            {
                if (dFile.FileType!=FileType.File && dFile.isDisk)
                {
                    //go up 2 levels to find the directory of the game
                    //if two levels are not available (this is where file/single level archive has been selected in the rules) then just replace up one level.
                    DatDir dir = parents[Math.Max(0,parentCount - 2)];
                    DatDir zipDir = parents[parentCount - 1];

                    zipDir.ChildRemove(dFile);

                    DatDir tmpFile = new DatDir(zipDir.Name, FileType.Dir) { DGame = zipDir.DGame };
                    dir.ChildNameSearchAdd(ref tmpFile);

                    dFile.FileType = FileType.File;
                    tmpFile.ChildAdd(dFile);
                }

                return;
            }

            if (!(inDat is DatDir dDir))
                return;


            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            parents.Add(dDir);

            foreach (DatBase child in children)
            {
                MoveUpCHDs(child, parents);
            }
            parents.RemoveAt(parentCount);
        }

    }

}

