using DATReader.DatStore;

namespace DATReader.DatClean
{

    public static partial class DatClean
    {
        public static void CleanFilenames(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                CleanFilename(db);

                if (db is DatDir ddir)
                {
                    CleanFilenames(ddir);
                }
            }
        }

        private static void CleanFilename(DatBase db)
        {
            string name = db.Name;
            if (string.IsNullOrEmpty(name))
                return;

            string retName = name;
            retName = retName.TrimStart();
            retName = retName.TrimEnd(new[]{ '.', ' ' });

            char[] charName = retName.ToCharArray();
            for (int i = 0; i < charName.Length; i++)
            {
                int c = charName[i];
                if (c == ':' || c == '*' || c == '?' || c == '<' || c == '>' || c == '|' || c < 32)
                    charName[i] = '-';
                else if (c == '/')
                    charName[i] = '\\';
            }
            db.Name=new string(charName);
        }
    }
}
