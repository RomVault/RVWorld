using System.Collections.Generic;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVIO;

namespace RomVaultCore.FixFile.Util
{
    public static partial class FixFileUtils
    {
        private static string dirNow = System.IO.Directory.GetCurrentDirectory();

        public static ReturnCode DoubleCheckDelete(RvFile fileDeleting, out string errorMessage)
        {
            errorMessage = "";

            if (!Settings.rvSettings.DoubleCheckDelete)
            {
                return ReturnCode.Good;
            }


            ReportError.LogOut("Double Check deleting file ");
            ReportError.LogOut(fileDeleting);

            if (DBHelper.IsZeroLengthFile(fileDeleting))
            {
                return ReturnCode.Good;
            }

            List<RvFile> lstFixRomTable = new List<RvFile>();
            List<RvFile> family = fileDeleting.FileGroup.Files;
            foreach (RvFile file in family)
            {
                if (file.GotStatus == GotStatus.Got && DBHelper.CheckIfMissingFileCanBeFixedByGotFile(fileDeleting, file))
                    lstFixRomTable.Add(file);
            }

            RvFile fileToCheck = null;
            int i = 0;
            while (i < lstFixRomTable.Count && fileToCheck == null)
            {
                switch (lstFixRomTable[i].RepStatus)
                {
                    case RepStatus.Delete:
                        i++;
                        break;
                    case RepStatus.Unknown:
                    case RepStatus.Correct:
                    case RepStatus.InToSort:
                    case RepStatus.Rename:
                    case RepStatus.NeededForFix:
                    case RepStatus.MoveToSort:
                    case RepStatus.Ignore:
                        fileToCheck = lstFixRomTable[i];
                        break;
                    default:

                        ReportError.LogOut("Double Check Delete Error Unknown " + lstFixRomTable[i].FullName + " " + lstFixRomTable[i].RepStatus);
                        ReportError.UnhandledExceptionHandler("Unknown double check delete status " + lstFixRomTable[i].RepStatus);
                        break;
                }
            }
            //ReportError.LogOut("Found Files when double check deleting");
            //foreach (RvFile t in lstFixRomTable)
            //    ReportError.LogOut(t);

            if (fileToCheck == null)
            {
                ReportError.UnhandledExceptionHandler("Double Check Delete could not find the correct file. (" + fileDeleting.FullName + ")");
                //this line of code never gets called because the above line terminates the program.
                return ReturnCode.LogicError;
            }

            //if it is a file then 
            // check it exists and the filestamp matches
            //if it is a ZipFile then
            // check the parent zip exists and the filestamp matches

            switch (fileToCheck.FileType)
            {
                case FileType.ZipFile:
                case FileType.SevenZipFile:
                    {
                        string fullPathCheckDelete = fileToCheck.Parent.FullNameCase;
                        if (!File.Exists(fullPathCheckDelete))
                        {
                            errorMessage = "Deleting " + fileDeleting.FullName + " Correct file not found. Resan for " + fullPathCheckDelete;
                            return ReturnCode.RescanNeeded;
                        }
                        FileInfo fi = new FileInfo(fullPathCheckDelete);
                        if (fi.LastWriteTime != fileToCheck.Parent.FileModTimeStamp)
                        {
                            errorMessage = "Deleting " + fileDeleting.FullName + " Correct file timestamp not found. Resan for " + fileToCheck.FullName;
                            return ReturnCode.RescanNeeded;
                        }

                        // same zip file so is it OK
                        if (fileToCheck.Parent == fileDeleting.Parent)
                            break;

                        //check if the path for the file being deleted is the same as the file we are checking we have.
                        string fullPathToFileBeingDeleted = fileDeleting.Parent.FullNameCase;

                        fullPathCheckDelete = RelativePath.MakeRelative(dirNow,fullPathCheckDelete);
                        fullPathToFileBeingDeleted = RelativePath.MakeRelative(dirNow,fullPathToFileBeingDeleted);

                        if (fullPathCheckDelete==fullPathToFileBeingDeleted)
                        {
                            errorMessage = "Delete Check found multiple tree paths to the same file.\nTree structure should be fixed:\n\n1st Path = "+fileDeleting.Parent.TreeFullName+"\n\n2nd Path = "+fileToCheck.Parent.TreeFullName;
                            return ReturnCode.TreeStructureError;
                        }

                        break;
                    }
                case FileType.File:
                    {
                        string fullPathCheckDelete = fileToCheck.FullNameCase;
                        if (!File.Exists(fullPathCheckDelete))
                        {
                            errorMessage = "Deleting " + fileDeleting.FullName + " Correct file not found. Resan for " + fullPathCheckDelete;
                            return ReturnCode.RescanNeeded;
                        }
                        FileInfo fi = new FileInfo(fullPathCheckDelete);
                        if (fi.LastWriteTime != fileToCheck.FileModTimeStamp)
                        {
                            errorMessage = "Deleting " + fileDeleting.FullName + " Correct file timestamp not found. Resan for " + fileToCheck.FullName;
                            return ReturnCode.RescanNeeded;
                        }

                        //check if the path for the file being deleted is the same as the file we are checking we have.
                        string fullPathToFileBeingDeleted = fileDeleting.FullNameCase;

                        fullPathCheckDelete = RelativePath.MakeRelative(dirNow, fullPathCheckDelete);
                        fullPathToFileBeingDeleted = RelativePath.MakeRelative(dirNow, fullPathToFileBeingDeleted);

                        if (fullPathCheckDelete == fullPathToFileBeingDeleted)
                        {
                            errorMessage = "Delete Check found multiple tree paths to the same file.\nTree structure should be fixed:\n\n1st Path = " + fileDeleting.TreeFullName + "\n\n2nd Path = " + fileToCheck.TreeFullName;
                            return ReturnCode.TreeStructureError;
                        }

                        break;
                    }
                default:
                    ReportError.UnhandledExceptionHandler("Unknown double check delete status " + fileToCheck.RepStatus);
                    break;
            }
            
            return ReturnCode.Good;
        }

    }
}
