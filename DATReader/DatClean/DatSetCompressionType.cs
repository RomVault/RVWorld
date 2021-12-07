using System.Collections.Generic;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static class DatSetCompressionType
    {
        public static void SetZip(DatBase inDat, bool is7Zip = false)
        {
            SetZip(inDat, is7Zip, new List<DatDir>());
        }

        private static void SetZip(DatBase inDat, bool is7Zip, List<DatDir> parents)
        {
            if (parents == null)
                parents = new List<DatDir>();

            int parentCount = parents.Count;

            if (inDat is DatFile dFile)
            {
                if (dFile.isDisk)
                {
                    //go up 2 levels to find the directory of the game
                    DatDir dir = parents[parentCount - 2];
                    DatDir zipDir = parents[parentCount - 1];

                    DatDir tmpFile = new DatDir(DatFileType.Dir)
                    { Name = zipDir.Name, DGame = zipDir.DGame };

                    if (dir.ChildNameSearch(tmpFile, out int index) != 0)
                    {
                        dir.ChildAdd(tmpFile);
                    }
                    else
                    {
                        tmpFile = (DatDir)dir.Child(index);
                    }
                    dFile.DatFileType = DatFileType.File;
                    tmpFile.ChildAdd(dFile);

                }
                else
                {
                    dFile.Name = dFile.Name.Replace("\\", "/");
                    dFile.DatFileType = is7Zip ? DatFileType.File7Zip : DatFileType.FileTorrentZip;
                    parents[parentCount - 1].ChildAdd(dFile);
                }
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            if (inDat.Name != null)
            {
                inDat.Name = inDat.Name.TrimStart(new[] { ' ' });
                inDat.Name = inDat.Name.TrimEnd(new[] { ' ' });
                if (string.IsNullOrWhiteSpace(inDat.Name))
                    inDat.Name = "_";
            }

            if (parents.Count > 0)
                parents[parentCount - 1].ChildAdd(inDat);

            dDir.DatFileType = dDir.DGame == null ?
                DatFileType.Dir :
                (is7Zip ? DatFileType.Dir7Zip : DatFileType.DirTorrentZip);

            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            parents.Add(dDir);
            foreach (DatBase child in children)
            {
                SetZip(child, is7Zip, parents);
            }
            parents.RemoveAt(parentCount);
        }


        public static void SetFile(DatBase inDat)
        {
            if (inDat.Name != null)
            {
                inDat.Name = inDat.Name.TrimStart(new[] { ' ' });
                inDat.Name = inDat.Name.TrimEnd(new[] { '.', ' ' });
                if (string.IsNullOrWhiteSpace(inDat.Name))
                    inDat.Name = "_";
            }

            if (inDat is DatFile dFile)
            {
                //if (dFile.DatFileType == DatFileType.UnSet)
                dFile.DatFileType = DatFileType.File;
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            if (dDir.DGame == null)
            {
                //if (dDir.DatFileType == DatFileType.UnSet)
                dDir.DatFileType = DatFileType.Dir;
            }
            else
            {
                //if (dDir.DatFileType == DatFileType.UnSet)
                dDir.DatFileType = DatFileType.Dir;
            }

            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            foreach (DatBase child in children)
            {
                SetFile(child);
                dDir.ChildAdd(child);
            }

        }

    }
}
