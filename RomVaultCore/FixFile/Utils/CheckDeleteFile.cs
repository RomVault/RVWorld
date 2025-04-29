using System;
using System.Diagnostics;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile.Utils
{
    public static partial class FixFileUtils
    {
        public static void CheckDeleteFile(RvFile dirDeleteCheck)
        {
            if (dirDeleteCheck.RepStatus == RepStatus.Deleted)
                return;

            // look at the directories childrens status's to figure out if the directory should be deleted.
            if (dirDeleteCheck.FileType == FileType.Dir)
            {
                if (dirDeleteCheck.GotStatus != GotStatus.Got)
                    return;

                if (dirDeleteCheck.DirStatus.HasAnyFiles() || dirDeleteCheck.DirStatus.HasFixable())
                    return;

                if (RvFile.treeType(dirDeleteCheck) == RvTreeRow.TreeSelect.Locked)
                    return;

                // check if we are at the root of the tree so that we do not delete RomRoot and ToSort
                if (dirDeleteCheck.Parent == DB.DirRoot)
                    return;

                string fullPath = dirDeleteCheck.FullName;
                try
                {
                    Debug.WriteLine("Deleting directory: " + fullPath);
                    if (Directory.Exists(fullPath))
                        Directory.Delete(fullPath);
                }
                catch (Exception e)
                {
                    //need to report this to an error window
                    Debug.WriteLine(e.ToString());
                }
            }

            // else check if this file/directory should be removed from the DB
            if (dirDeleteCheck.FileRemove() == EFile.Delete)
            {
                RvFile parent = dirDeleteCheck.Parent;
                if (parent == null)
                {
                    ReportError.UnhandledExceptionHandler("Item being deleted had no parent " + dirDeleteCheck.FullName);
                    return; // this never happens as UnhandledException Terminates the program
                }

                if (!parent.FindChild(dirDeleteCheck, out int index))
                {
                    ReportError.UnhandledExceptionHandler("Could not find self in delete code " + parent.FullName);
                }
                parent.ChildRemove(index);
                CheckDeleteFile(parent);
            }
        }

    }
}
