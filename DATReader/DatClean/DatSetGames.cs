using DATReader.DatStore;
using System.Collections.Generic;
using System.IO;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {


        public static void SetFilesAsGames(DatDir tDat)
        {
            // this is used so we know which dirs we added and which dirs where already in the database
            Dictionary<string, DatDir> newDirsAtThisLevel = new Dictionary<string, DatDir>();

            DatBase[] datBase = tDat.ToArray();
            foreach (DatBase dBase in datBase)
            {
                if (dBase is DatDir datDir)
                {
                    if (datDir.FileType == FileType.Dir && datDir.DGame == null)
                        SetFilesAsGames(datDir);
                    continue;
                }

                DatFile dFile = dBase as DatFile;

                // found a file
                string fName = dFile.Name;
                string gameName = Path.GetFileNameWithoutExtension(fName);

                if (newDirsAtThisLevel.TryGetValue(gameName.ToLower(), out DatDir dOut))
                {
                    tDat.ChildRemove(dFile);
                    dOut.ChildAdd(dFile);
                    continue;
                }

                DatDir newGame = new DatDir(gameName, FileType.UnSet)
                {
                    DGame = new DatGame() { Description = gameName }
                };
                if (tDat.ChildNameSearch(newGame, out int index) == 0)
                {  // Error repeat found
                    continue;
                }
                tDat.ChildRemove(dFile);
                newGame.ChildAdd(dFile);
                tDat.ChildAdd(newGame);

                newDirsAtThisLevel.Add(gameName.ToLower(), newGame);
            }
        }

        public static void SetArchivesAsGames(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir datDir))
                    continue;

                switch (datDir.FileType)
                {
                    case FileType.Dir:
                        SetArchivesAsGames(datDir);
                        break;

                    case FileType.Zip:
                    case FileType.SevenZip:

                        datDir.DGame = new DatGame()
                        {
                            Description = datDir.Name
                        };
                        break;
                }
            }
        }

        public static void SetFirstLevelDirsAsGames(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir datDir))
                    continue;


                datDir.DGame = new DatGame()
                {
                    Description = datDir.Name
                };
            }
        }
    }
}
