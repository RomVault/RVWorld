using System.Diagnostics;
using DATReader.DatStore;
using RVIO;

namespace DATReader.DatClean
{

    public static partial class DatClean
    {

        public static void RemoveDateTime(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                if (db is DatFile df)
                {
                    df.DateModified = null;
                    continue;
                }

                if (db is DatDir ddir)
                {
                    RemoveDateTime(ddir);
                }
            }
        }
        public static void RemoveMD5(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                if (db is DatFile df)
                {
                    df.MD5 = null;
                    continue;
                }

                if (db is DatDir ddir)
                {
                    RemoveMD5(ddir);
                }
            }
        }
        public static void RemoveSHA256(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            foreach (DatBase db in arrDir)
            {
                if (db is DatFile df)
                {
                    df.SHA256 = null;
                    continue;
                }

                if (db is DatDir ddir)
                {
                    RemoveSHA256(ddir);
                }
            }
        }

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
            retName = retName.Replace("\\", "/");
            //retName = retName.Replace("\\ ", "\\");
            retName = retName.Replace("./", "/");

            char[] charName = retName.ToCharArray();
            for (int i = 0; i < charName.Length; i++)
            {
                int c = charName[i];
                if (c == ':' || c == '*' || c == '?' || c == '<' || c == '>' || c == '|' || c == '"' || c < 32)
                    charName[i] = '-';
            }
            db.Name = new string(charName);
        }



        public static void CleanFileNamesFull(DatBase inDat)
        {
            if (!(inDat is DatDir dDir))
                return;
            DatBase[] children = ((DatDir)inDat).ToArray();
            if (children == null)
                return;


            foreach (DatBase child in children)
            {
                string originalName = child.Name;
                switch (child.FileType)
                {
                    case FileType.UnSet:
                    case FileType.File:
                    case FileType.Dir:
                        if (child.Name != null)
                        {
                            child.Name = child.Name.TrimStart(new[] { ' ' });
                            child.Name = child.Name.TrimEnd(new[] { '.', ' ' });
                        }
                        break;
                    case FileType.Zip:
                    case FileType.SevenZip:
                        if (child.Name != null)
                        {
                            child.Name = child.Name.TrimStart(new[] { ' ' });
                            child.Name = child.Name.TrimEnd(new[] { ' ' });
                        }
                        break;
                }
                if (string.IsNullOrWhiteSpace(child.Name))
                    child.Name = "_";

                if (originalName != child.Name)
                {
                    ((DatDir)inDat).ChildRemove(child);
                    ((DatDir)inDat).ChildAdd(child);
                }

                if (child.FileType == FileType.Dir)
                    CleanFileNamesFull(child);
            }

        }



        public static void FixDupes(DatDir dDir)
        {
            DatBase[] arrDir = dDir.ToArray();
            string lastName = "";
            FileType lastFileType = FileType.UnSet;
            int matchCount = 0;
            foreach (DatBase db in arrDir)
            {
                string thisName = db.Name;
                FileType fileType = db.FileType;

                if (lastFileType == fileType && lastName.ToLowerInvariant() == thisName.ToLowerInvariant())
                {
                    switch (lastFileType)
                    {
                        case FileType.Dir:
                        case FileType.Zip:
                        case FileType.SevenZip:
                            {
                                db.Name = thisName + "_" + matchCount;
                                break;
                            }
                        case FileType.UnSet:
                        case FileType.File:
                        case FileType.FileZip:
                        case FileType.FileSevenZip:
                            {
                                string path1 = Path.GetExtension(thisName);
                                string path0 = thisName.Substring(0, thisName.Length - path1.Length);

                                db.Name = path0 + "_" + matchCount + path1;
                                break;
                            }
                    }
                    matchCount += 1;
                    dDir.ChildRemove(db);
                    dDir.ChildAdd(db);
                }
                else
                {
                    matchCount = 0;
                    lastName = thisName;
                    lastFileType = fileType;
                }

                if (db is DatDir ddir)
                {
                    FixDupes(ddir);
                }
            }
        }

    }
}
