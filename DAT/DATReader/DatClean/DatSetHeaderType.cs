using DATReader.DatStore;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void SetHeaderType(DatDir tDat, HeaderFileType headerFileType)
        {
            DatBase[] db = tDat.ToArray();

            foreach (DatBase d in db)
            {
                switch (d)
                {
                    case DatFile df:
                        if (df.HeaderFileType != HeaderFileType.CHD)
                            df.HeaderFileType = headerFileType;
                        break;
                    case DatDir dd:
                        SetHeaderType(dd, headerFileType);
                        break;
                }
            }
        }
    }
}
