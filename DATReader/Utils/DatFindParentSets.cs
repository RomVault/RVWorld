using System.Collections.Generic;
using DATReader.DatStore;

namespace DATReader.Utils
{
    public static class DatFindParentSets
    {
        public static void FindParentSet(DatDir searchGame, DatDir parentDir, bool includeBios, ref List<DatDir> lstParentGames)
        {
            if (searchGame.DGame == null)
            {
                return;
            }

            string parentName = searchGame.DGame.RomOf;
            if (string.IsNullOrEmpty(parentName) || (parentName == searchGame.Name))
            {
                parentName = searchGame.DGame.CloneOf;
            }
            if (string.IsNullOrEmpty(parentName) || (parentName == searchGame.Name))
            {
                return;
            }

            if (parentDir.ChildNameSearch(new DatDir(parentName, searchGame.FileType), out int intIndex) != 0)
                return;

            DatDir parentGame = (DatDir)parentDir[intIndex];
            if (!includeBios && parentGame.DGame?.IsBios == "yes")
                return;

            if (lstParentGames.Contains(parentGame))
                return;

            lstParentGames.Add(parentGame);
            FindParentSet(parentGame, parentDir, includeBios, ref lstParentGames);
        }
    }
}
