using RomVaultCore.RvDB;
using System.Diagnostics;

namespace RomVaultCore.Utils
{
    public static class IsFileOnly
    {
        public static bool isFileOnly(RvFile inFile)
        {
            if (Settings.rvSettings.FilesOnly)
                return true;

            string datPath = inFile.DatTreeFullName;
            if (!inFile.IsFile)
                datPath += "\\";

            DatRule datRule = ReadDat.DatReader.FindDatRule(datPath);

            if (datRule==null)
            {
                // in a ToSort file fileonly ToSort
                return inFile.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly);
            }

            string datHeaderType = null;

            RvFile rvTest = inFile;
            while (rvTest != null)
            {
                // first find if the dir or parent dir belongs to a DAT
                if (rvTest.Dat != null)
                {
                    RvDat fileDat = rvTest.Dat;
                    string compType = fileDat.GetData(RvDat.DatData.Compression);
                    datHeaderType = compType;
                    break;
                }


                // next see if the dir is at the same level as a DAT with fileonly
                int datCount = rvTest.CountDats();
                if (datCount > 0)
                {
                    for (int i = 0; i < datCount; i++)
                    {
                        RvDat dat = rvTest.DirDat(i);
                        string compType = dat.GetData(RvDat.DatData.Compression);
                        if (compType == "fileonly")
                        {
                            datHeaderType = compType;
                            break;
                        }
                    }
                    break;
                }
                rvTest = rvTest.Parent;
            }


            // if a Dat Rule is found and the Dat Rule overrides the DAT then return true.
            if (datRule.Compression == FileType.FileOnly)
                return true;

            // if there is a dat header value and the dat header contains fileonly then return true.
            if (datHeaderType != null)
                return datHeaderType.ToLower() == "fileonly";

            // the datheader was null so use the dat rule,
            // if the Dat Rule is fileonly return true.
            return datRule.Compression == FileType.FileOnly;
        }
    }
}
