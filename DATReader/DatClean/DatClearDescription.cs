using DATReader.DatStore;
using System.IO;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void ClearDescription(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
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
