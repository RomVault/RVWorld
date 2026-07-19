using DATReader.DatStore;
using System.IO;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void ClearDescription(DatDir dDir)
        {
            int childCount = dDir.Count;
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                DatBase db = dDir[childIndex];
                if (db is DatDir ddir)
                {
                    if (ddir.DGame != null)
                    {
                        if (Path.GetFileNameWithoutExtension(db.Name) == ddir.DGame.Description)
                            ddir.DGame.Description = "¤";
                        continue;
                    }

                    ClearDescription(ddir);
                }
            }
        }
    }
}
