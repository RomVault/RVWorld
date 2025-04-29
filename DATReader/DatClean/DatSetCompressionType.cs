using Compress;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static class DatSetCompressionType
    {


        public static FileType[] GetFileTypeFromDir = new FileType[]
        {
            FileType.UnSet,
            FileType.File,
            FileType.FileZip,
            FileType.FileSevenZip
        };

        public static void SetType(DatBase inDat, FileType fileType, ZipStructure zs, bool fix)
        {
            if (inDat is DatFile dFile)
            {
                dFile.FileType = GetFileTypeFromDir[(int)fileType];
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            if (dDir.DGame == null || fileType == FileType.Dir)
            {
                dDir.FileType = FileType.Dir;
            }
            else
            {
                if (dDir.FileType!=FileType.UnSet)
                {
                    if (dDir.FileType == FileType.Dir)
                    {
                        fileType = FileType.Dir;
                        zs = ZipStructure.None;
                    }
                    if (dDir.FileType == FileType.Zip)
                    {
                        fileType = FileType.Zip;
                        zs = ZipStructure.ZipTrrnt;
                    }
                    if (dDir.FileType == FileType.SevenZip)
                    {
                        fileType = FileType.SevenZip;
                        zs = ZipStructure.SevenZipNZSTD;
                    }
                }
                dDir.FileType = fileType;

                ZipStructure zsChecked = IsTrrntzipDateTimes(dDir, zs) ? ZipStructure.ZipTrrnt : zs;
                dDir.SetDatStruct(zsChecked, fix);
            }


            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            foreach (DatBase child in children)
            {
                SetType(child, fileType, zs, fix);
                dDir.ChildAdd(child);
            }

        }


        private static bool IsTrrntzipDateTimes(DatDir dDir, ZipStructure zs)
        {
            if (dDir.FileType != FileType.Zip || zs != ZipStructure.ZipTDC)
                return false;

            DatBase[] children = dDir.ToArray();
            foreach (DatBase child in children)
            {
                if (child is DatFile)
                {
                    if (child.DateModified != Compress.StructuredZip.StructuredZip.TrrntzipDosDateTime)
                        return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }


    }
}
