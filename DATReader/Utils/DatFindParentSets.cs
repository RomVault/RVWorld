using System.Collections.Generic;
using DATReader.DatStore;

namespace DATReader.Utils
{
    public static class DatFindParentSets
    {
        public static void FindParentSet(DatDir searchGame, DatDir parentDir,bool includeBios, ref List<DatDir> lstParentGames)
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

            if (parentDir.ChildNameSearch(new DatDir(searchGame.DatFileType) { Name = parentName }, out int intIndex) != 0)
                return;

            DatDir parentGame = (DatDir)parentDir.Child(intIndex);
            if (!includeBios && parentGame.DGame?.IsBios == "yes")
                return;

            lstParentGames.Add(parentGame);
            FindParentSet(parentGame, parentDir,includeBios, ref lstParentGames);
        }
    }
}
