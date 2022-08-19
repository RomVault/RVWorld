using System;
using DATReader.DatStore;

namespace DATReader.Utils
{
    public static class DatHasRomOf
    {
        public static bool HasRomOf(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir mGame))
                    continue;

                if (mGame.DGame == null)
                {
                    bool res = HasRomOf(mGame);
                    if (res)
                    {
                        return true;
                    }
                }
                else
                {
                    if (!String.IsNullOrWhiteSpace(mGame.DGame.RomOf))
                        return true;
                    if (!String.IsNullOrWhiteSpace(mGame.DGame.CloneOf))
                        return true;
                }

            }
            return false;
        }
    }
}