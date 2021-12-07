using System;
using RVXCore.Util;

namespace RVXCore
{
    public static class RomRootDir
    {
        private static readonly string[] rootDirs;

        static RomRootDir()
        {
            if (!System.IO.File.Exists("DirPaths.conf"))
                return;
            string text = System.IO.File.ReadAllText(@"DirPaths.conf");

            text = text.Replace("\r", "");
            string[] rules= text.Split('\n');

            rootDirs=new string[256];
            for (int i = 0; i < 256; i++)
            {
                rootDirs[i] = @"RomRoot\" + VarFix.ToString((byte) i);
            }


            foreach (string rule in rules)
            {
                if (rule.Length < 6)
                    continue;

                string pStart = rule.Substring(0, 2);
                string ps0 = rule.Substring(2, 1);
                string pEnd = rule.Substring(3, 2);
                string ps1 = rule.Substring(5, 1);
                string pDir = rule.Substring(6);
                if (ps0!="-")
                    continue;
                if (ps1 != "|")
                    continue;

                int iStart = Convert.ToInt32(pStart, 16);
                int iEnd = Convert.ToInt32(pEnd, 16);

                for (int i = iStart; i <= iEnd; i++)
                    rootDirs[i] = pDir + @"\" + VarFix.ToString((byte) i);
            }
        }
        
        public static string GetRootDir(byte b0)
        {
            if (rootDirs == null)
                return @"RomRoot\" + VarFix.ToString(b0);
            else
                return rootDirs[b0];
        }

        public static string Getfilename(byte[] sha1)
        {
            return GetRootDir(sha1[0]) + @"\" +
                   VarFix.ToString(sha1[1]) + @"\" +
                   VarFix.ToString(sha1) + ".gz";
        }
    }
}
