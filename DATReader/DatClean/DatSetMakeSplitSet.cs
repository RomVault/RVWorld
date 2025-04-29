using System.Collections.Generic;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void DatSetMakeSplitSet(DatDir tDat)
        {
            // look for merged roms, check if a rom exists in a parent set where the Name,Size and CRC all match.

            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = (DatDir)tDat[g];

                if (mGame.DGame == null)
                {
                    DatSetMakeSplitSet(mGame);
                }
                else
                {
                    // find all parents of this game
                    List<DatDir> lstParentGames = new List<DatDir>();
                    DatFindParentSets.FindParentSet(mGame, tDat, true, ref lstParentGames);

                    // if no parents are found then just set all children as kept
                    if (lstParentGames.Count == 0)
                    {
                        continue;
                    }
                    else
                    {
                        for (int r0 = 0; r0 < mGame.Count; r0++)
                        {
                            DatFile dr0 = (DatFile)mGame[r0];
                            if (dr0.Status == "nodump")
                                continue;

                            bool found = FindRomInParent(dr0, lstParentGames);
                            if (found)
                                SetRomAsMerged(dr0);
                        }
                    }
                }
            }
        }

        private static void SetRomAsMerged(DatFile tRom)
        {
            if (string.IsNullOrEmpty(tRom.Merge))
            {
                tRom.Merge = "(Auto Merged)";
            }
            tRom.DatStatus = DatStatus.InDatMerged;
        }

    }
}
