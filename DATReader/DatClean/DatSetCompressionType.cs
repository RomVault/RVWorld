using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static class DatSetCompressionType
    {
        public static void SetTorrentZip(DatBase inDat)
        {
            if (inDat is DatFile dFile)
            {
                dFile.Name = dFile.Name.Replace("\\", "/");
                dFile.DatFileType = DatFileType.FileTorrentZip;
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
                dDir.DatFileType = DatFileType.DirTorrentZip;
            }

            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            foreach (DatBase child in children)
            {
                SetTorrentZip(child);
                dDir.ChildAdd(child);
            }

        }
        public static void Set7Zip(DatBase inDat)
        {
            if (inDat is DatFile dFile)
            {
                dFile.Name = dFile.Name.Replace("\\", "/");
                dFile.DatFileType = DatFileType.File7Zip;
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
                dDir.DatFileType = DatFileType.Dir7Zip;
            }

            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            foreach (DatBase child in children)
            {
                Set7Zip(child);
                dDir.ChildAdd(child);
            }

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
