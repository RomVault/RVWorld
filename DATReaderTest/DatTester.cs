using System.Collections.Generic;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    public class DatTypeTester
    {
        public bool subDirFound = false;
        public bool subDirContainsDir = false;

        public bool gameContainsdir = false;

        public bool subDirFoundInGame = false;
        public bool subDirInGameContainsDir = false;
        public bool fileContainsDir = false;

        public bool cloneOf = false;
        public bool romOf = false;
        public bool fileMerge = false;

        public List<string> Status = new List<string>();

        public bool Found()
        {
            return subDirFound || subDirContainsDir || gameContainsdir || subDirFoundInGame || subDirInGameContainsDir || fileContainsDir || cloneOf || romOf || Status.Count > 0;
        }

        public string toString()
        {
            return subDirFound + "," + subDirContainsDir + "," + gameContainsdir + "," + subDirFoundInGame + "," + subDirInGameContainsDir + "," + fileContainsDir + "," + cloneOf + "," + romOf + "," + string.Join("|", Status);
        }


        public void ProcessDat(DatHeader dh)
        {
            ProcessDir(dh.BaseDir, 0, false);
        }

        private void ProcessDir(DatDir dd, int depth, bool inGame)
        {
            bool dirIsGame = dd.DGame != null;
            inGame |= dirIsGame;

            if (dd.DGame != null)
            {
                if (!string.IsNullOrWhiteSpace(dd.DGame.CloneOf))
                    cloneOf = true;
                if (!string.IsNullOrWhiteSpace(dd.DGame.RomOf))
                    romOf = true;
            }

            if (!inGame && depth > 0)
            {
                subDirFound = true;
                subDirContainsDir |= dd.Name.Contains(@"/") || dd.Name.Contains(@"\");
            }
            if (dirIsGame)
            {
                gameContainsdir |= dd.Name.Contains(@"/") || dd.Name.Contains(@"\");
            }
            if (inGame && !dirIsGame)
            {
                subDirFoundInGame = true;
                subDirInGameContainsDir |= dd.Name.Contains(@"/") || dd.Name.Contains(@"\");
            }

            //  Debug.WriteLine(depth + " " + dd.Name + "  " + inGame);
            int iCount = dd.ChildCount;
            for (int i = 0; i < iCount; i++)
            {
                DatBase db = dd.Child(i);

                DatDir ddc = db as DatDir;
                if (ddc != null)
                    ProcessDir(ddc, depth + 1, inGame);
                DatFile dfc = db as DatFile;
                if (dfc != null)
                    ProcessFile(dfc);
            }
        }

        private void ProcessFile(DatFile df)
        {
            string s = df.Name;
            fileContainsDir |= s.Contains(@"/") || s.Contains(@"\");
            if (!string.IsNullOrWhiteSpace(df.Merge))
                fileMerge = true;

            if (!string.IsNullOrWhiteSpace(df.Status))
            {
                string lc = df.Status.ToLower();
                if (!Status.Contains(lc))
                    Status.Add(lc);
            }

            //  Debug.WriteLine("     " + df.Name);
        }

    }
}

