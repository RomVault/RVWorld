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
                    RomCheckCollect(mFile);
                    continue;
                }
            }
        }

        /*
         * In the mame Dat:
         * status="nodump" has a size but no CRC
         * status="baddump" has a size and crc
         */


        internal static void RomCheckCollect(DatFile tRom)
        {
            if (tRom.DatStatus == DatStatus.InDatMerged)
                return;

            if (!string.IsNullOrEmpty(tRom.Merge))
            {
                tRom.Merge = "(No-Merge) " + tRom.Merge;
            }

            if (tRom.Status == "nodump")
            {
                tRom.DatStatus = DatStatus.InDatNoDump;
                return;
            }
            if (tRom.MIA?.ToLower() == "yes" && tRom.Size!=0)
            {
                tRom.DatStatus = DatStatus.InDatMIA;
                return;
            }

            if (ArrByte.bCompare(tRom.CRC, new byte[] { 0, 0, 0, 0 }) && (tRom.Size == 0))
            {
                tRom.DatStatus = DatStatus.InDatCollect;
                return;
            }

            tRom.DatStatus = DatStatus.InDatCollect;
        }


    }
}
