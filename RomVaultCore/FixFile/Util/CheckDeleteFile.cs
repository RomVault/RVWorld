using System;
using System.Diagnostics;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile.Util
{
   public static partial class FixFileUtils
    {
        public static void CheckDeleteFile(RvFile file)
        {
            if (file.RepStatus == RepStatus.Deleted)
            {
                return;
            }

            // look at the directories childrens status's to figure out if the directory should be deleted.
            if (file.FileType == FileType.Dir)
            {
                RvFile tDir = file;
                if (!tDir.IsDir || tDir.ChildCount != 0)
                {
                    return;
                }
                // check if we are at the root of the tree so that we do not delete RomRoot and ToSort
                if (tDir.Parent == DB.DirRoot)
                {
                    return;
                }

                string fullPath = tDir.FullName;
                try
                {
                    if (Directory.Exists(fullPath))
                    {
                        Directory.Delete(fullPath);
                    }
                }
                catch (Exception e)
                {
                    //need to report this to an error window
                    Debug.WriteLine(e.ToString());
                }
            }

            // else check if this file should be removed from the DB
            if (file.FileRemove() == EFile.Delete)
            {
                RvFile parent = file.Parent;

                if (parent == null)
                {
                    ReportError.UnhandledExceptionHandler("Item being deleted had no parent " + file.FullName);
                    return; // this never happens as UnhandledException Terminates the program
                }

                if (!parent.FindChild(file, out int index))
                {
                    ReportError.UnhandledExceptionHandler("Could not find self in delete code " + parent.FullName);
                }
                parent.ChildRemove(index);
                CheckDeleteFile(parent);
            }
        }

    }
}
