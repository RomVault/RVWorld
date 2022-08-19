using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatClean
{
    public static class DatSetStatus
    {

        public static void SetStatus(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                DatBase nf = tDat[g];
                if (nf is DatDir mDir)
                {
                    SetStatus(mDir);
                    continue;
                }
                if (nf is DatFile mFile)
                {
                    RomCheckCollect(mFile, false);
                    continue;
                }
            }
        }

        /*
         * In the mame Dat:
         * status="nodump" has a size but no CRC
         * status="baddump" has a size and crc
         */


        internal static void RomCheckCollect(DatFile tRom, bool merge)
        {
            if (merge)
            {
                if (string.IsNullOrEmpty(tRom.Merge))
                {
                    tRom.Merge = "(Auto Merged)";
                }
                tRom.DatStatus = DatFileStatus.InDatMerged;
                return;
            }

            if (!string.IsNullOrEmpty(tRom.Merge))
            {
                tRom.Merge = "(No-Merge) " + tRom.Merge;
            }

            if (tRom.Status == "nodump")
            {
                tRom.DatStatus = DatFileStatus.InDatBad;
                return;
            }
            if (tRom.MIA?.ToLower() == "yes")
            {
                tRom.DatStatus = DatFileStatus.InDatMIA;
                return;
            }

            if (ArrByte.bCompare(tRom.CRC, new byte[] { 0, 0, 0, 0 }) && (tRom.Size == 0))
            {
                tRom.DatStatus = DatFileStatus.InDatCollect;
                return;
            }

            tRom.DatStatus = DatFileStatus.InDatCollect;
        }


    }
}
