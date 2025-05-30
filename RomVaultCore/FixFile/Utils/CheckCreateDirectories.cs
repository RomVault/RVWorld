﻿using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile.Utils
{
  public static partial class FixFileUtils
    {
        //Recurse back up the RvFile Parents, checking that the Directories exists.
        //and are marked as got in the DB
        public static void CheckCreateDirectories(RvFile file)
        {
            if (file == DB.DirRoot)
            {
                return;
            }

            string parentDir = file.FullName;
            if (Directory.Exists(parentDir) && file.GotStatus == GotStatus.Got)
            {
                return;
            }

            CheckCreateDirectories(file.Parent);
            if (!Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            DirectoryInfo pDir= new DirectoryInfo(parentDir);
            file.GotStatus = GotStatus.Got;
            file.FileModTimeStamp = pDir.LastWriteTime;
        }
    }
}
