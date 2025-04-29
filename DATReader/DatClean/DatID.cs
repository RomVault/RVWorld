using DATReader.DatStore;
using System.Collections.Generic;

namespace DATReader.DatClean
{

    public static partial class DatClean
    {

        public static void DatSetAddIdNumbers(DatDir tDat, string Id)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = tDat[g] as DatDir;
                if (mGame?.DGame?.Id != null)
                    Id = mGame.DGame.Id;

                tDat[g].Name = Id + " - " + tDat[g].Name;

                if (mGame != null)
                    DatSetAddIdNumbers(mGame, Id);
            }
        }

        public static void DatSetMatchIDs(DatDir tDat)
        {
            Dictionary<string, string> idNameLookup = new Dictionary<string, string>();
            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = (DatDir)tDat[g];

                if (mGame.DGame == null)
                    continue;
                if (!string.IsNullOrEmpty(mGame.DGame.Id))
                {
                    if (!idNameLookup.TryGetValue(mGame.DGame.Id, out _))
                        idNameLookup.Add(mGame.DGame.Id, mGame.Name);
                }
            }
            if (idNameLookup.Count == 0)
                return;

            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = (DatDir)tDat[g];

                if (mGame.DGame == null)
                    continue;
                if (!string.IsNullOrEmpty(mGame.DGame.CloneOfId))
                {
                    if (idNameLookup.TryGetValue(mGame.DGame.CloneOfId, out string CloneOf))
                        mGame.DGame.CloneOf = CloneOf;
                }
            }
        }
    }

}
