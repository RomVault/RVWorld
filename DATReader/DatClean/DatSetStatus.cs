using DATReader.DatStore;
using DATReader.Utils;
using RVUtils;

namespace DATReader.DatClean
{
    public static class DatSetStatus
    {
        private static readonly byte[] ZeroCrc = new byte[4];

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
            tRom.MIAStatus = MIAStatus.None;
            if (tRom.DatStatus == DatStatus.InDatMerged)
                return;
            if (string.Equals(tRom.MIA, "yes", System.StringComparison.OrdinalIgnoreCase) && tRom.Size != 0)
                tRom.MIAStatus = MIAStatus.MIAFromDat;

            if (!string.IsNullOrEmpty(tRom.Merge))
            {
                tRom.Merge = "(No-Merge) " + tRom.Merge;
            }

            if (tRom.Status == "nodump")
            {
                tRom.DatStatus = DatStatus.InDatNoDump;
                return;
            }

            if (ByteUtils.ByteArrEquals(tRom.CRC, ZeroCrc) && (tRom.Size == 0))
            {
                tRom.DatStatus = DatStatus.InDatCollect;
                return;
            }

            tRom.DatStatus = DatStatus.InDatCollect;
        }


    }
}
