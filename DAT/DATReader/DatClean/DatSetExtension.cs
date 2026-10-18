using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void SetExt(DatDir tDat)
        {
            DatBase[] db = tDat.ToArray();
            tDat.ChildrenClear();

            foreach (DatBase d in db)
            {
                switch (d)
                {
                    case DatFile df:

                        if (df.HeaderFileType == HeaderFileType.CHD)
                        {
                            df.Name += ".chd";
                            if (!string.IsNullOrEmpty(df.Merge))
                                df.Merge += ".chd";
                        }

                        tDat.ChildAdd(df);
                        break;
                    case DatDir dd:
                        dd.Name = dd.Name + GetExt(dd.FileType);
                        tDat.ChildAdd(dd);
                        SetExt(dd);
                        break;
                }
            }
        }

        private static string GetExt(FileType dft)
        {
            switch (dft)
            {
                case FileType.Dir:
                    return "";
                case FileType.Zip:
                    return ".zip";
                case FileType.SevenZip:
                    return ".7z";
                default:
                    return "";
            }
        }
    }
}
