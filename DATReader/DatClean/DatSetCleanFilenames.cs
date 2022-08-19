using System.Diagnostics;
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

        private static void CleanFilename(DatBase db)
        {
            string name = db.Name;
            if (string.IsNullOrEmpty(name))
                return;

            string retName = name;
            //retName = retName.TrimStart(new[] { ' ' });
            //retName = retName.TrimEnd(new[] { '.', ' ' });
            retName = retName.Replace("/", "\\");
            //retName = retName.Replace("\\ ", "\\");
            retName = retName.Replace(".\\", "\\");

            char[] charName = retName.ToCharArray();
            for (int i = 0; i < charName.Length; i++)
            {
                int c = charName[i];
                if (c == ':' || c == '*' || c == '?' || c == '<' || c == '>' || c == '|' || c=='"' || c < 32)
                    charName[i] = '-';
            }
            db.Name = new string(charName);
        }




        public static void CleanFilenamesFixDupes(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            string lastName = "";
            DatFileType lastFileType = DatFileType.UnSet;
            int matchCount = 0;
            foreach (DatBase db in arrDir)
            {
                string thisName = db.Name;
                DatFileType fileType = db.DatFileType;

                if (lastFileType==fileType && lastName.ToLowerInvariant() == thisName.ToLowerInvariant())
                {
                    Debug.WriteLine("Found match = " + lastName + " , " + thisName);

                    string path1 = Path.GetExtension(thisName);
                    string path0 = thisName.Substring(0, thisName.Length - path1.Length);

                    db.Name = path0 + "_" + matchCount + path1;
                    Debug.WriteLine("New filename = " + db.Name);
                    matchCount += 1;
                }
                else
                {
                    matchCount = 0;
                    lastName = thisName;
                    lastFileType = fileType;
                }

                if (db is DatDir ddir)
                {
                    CleanFilenamesFixDupes(ddir);
                }
            }
        }

    }
}
