using System.Collections.Generic;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static class DatSetCompressionType
    {

        private static List<DatDir> _parents = new List<DatDir>();
        public static void SetZip(DatBase inDat, bool is7Zip = false)
        {
            int parentCount = _parents.Count;

            if (inDat is DatFile dFile)
            {
                if (dFile.isDisk)
                {
                    //go up 2 levels to find the directory of the game
                    DatDir dir = _parents[parentCount - 2];
                    DatDir zipDir = _parents[parentCount - 1];

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
                    _parents[parentCount - 1].ChildAdd(dFile);
                }
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            if (_parents.Count > 0)
                _parents[parentCount - 1].ChildAdd(inDat);

            dDir.DatFileType = dDir.DGame == null ?
                DatFileType.Dir :
                (is7Zip ? DatFileType.Dir7Zip : DatFileType.DirTorrentZip);

            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            _parents.Add(dDir);
            foreach (DatBase child in children)
            {
                SetZip(child, is7Zip);
            }
            _parents.RemoveAt(parentCount);
        }


        public static void SetFile(DatBase inDat)
        {
            if (inDat is DatFile dFile)
            {
                dFile.DatFileType = DatFileType.File;
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            if (dDir.DGame == null)
            {
                dDir.DatFileType = DatFileType.Dir;
            }
            else
            {
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
