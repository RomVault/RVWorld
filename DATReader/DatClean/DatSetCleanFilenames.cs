using DATReader.DatStore;
using RVIO;

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
        public static void CleanFilenamesFixDupes(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            string lastName = "";
            int matchCount = 0;
            foreach (DatBase db in arrDir)
            {
                string thisName = db.Name;
                if (lastName == thisName)
                {
                    string path0 = Path.GetFileNameWithoutExtension(thisName);
                    string path1 = Path.GetExtension(thisName);
                    db.Name = path0 + "_" + matchCount + path1;
                    matchCount += 1;
                }
                else
                {
                    matchCount = 0;
                    lastName = thisName;
                }

                if (db is DatDir ddir)
                {
                    CleanFilenamesFixDupes(ddir);
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
