using RomVaultCore.FindFix;
using RomVaultCore.FixFile.Util;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVIO;
using System;
using System.Collections.Generic;

namespace RomVaultCore.FixFile
{
    internal static class FixAZipMove
    {

        public static ReturnCode CheckFileMove(RvFile fixZip, Dictionary<string, RvFile> filesUserForFix, ref int totalFixed, out string error)
        {
            error = "";
            //return ReturnCode.Cancel;
            if (fixZip.FileType != FileType.Zip && fixZip.FileType != FileType.SevenZip)
                return ReturnCode.Cancel;

            bool zipTypeSet = fixZip.FileType == FileType.Zip ? Settings.rvSettings.ConvertToTrrntzip : Settings.rvSettings.ConvertToRV7Z;

            FileType archiveNeeded = fixZip.FileType;

            int totalToFix = 0;
            // first check if every rom in the zip file we are about to try and fix can be fixed.
            // (This is good as a start, but should be just match to the copying file in the future.)
            int indexOfCanBeFixed = -1;
            for (int iRom = 0; iRom < fixZip.ChildCount; iRom++)
            {
                if (fixZip.Child(iRom).RepStatus == RepStatus.CanBeFixed ||
                    fixZip.Child(iRom).RepStatus == RepStatus.CanBeFixedMIA
                    )
                {
                    totalToFix++;
                    if (indexOfCanBeFixed == -1)
                        indexOfCanBeFixed = iRom;
                    continue;
                }
                if (fixZip.Child(iRom).RepStatus == RepStatus.Correct ||
                    fixZip.Child(iRom).RepStatus == RepStatus.CorrectMIA
                    )
                    continue;

                return ReturnCode.Cancel;
            }
            // if we only found correct, we a trrntzipping so return.
            if(indexOfCanBeFixed==-1)
                return ReturnCode.Cancel;

            // next get the list of all the match files that can fix the first file in this zip
            List<RvFile> lstFixRomTable = FindSourceFile.GetFixFileList(fixZip.Child(indexOfCanBeFixed));

            // now see which of these files are zip files
            List<RvFile> lstFixSourceZips = new List<RvFile>();
            foreach (RvFile file in lstFixRomTable)
            {
                if (FindFixesListCheck.treeType(file) == RvTreeRow.TreeSelect.Locked)
                    continue;

                RvFile parentFile = file.Parent;
                // parentFile can be null for the zero byte dummy file.
                if (parentFile == null || parentFile.FileType != archiveNeeded)
                    continue;

                if (zipTypeSet && parentFile.ZipStatus != Compress.ZipStatus.TrrntZip)
                    continue;

                if (lstFixSourceZips.Contains(parentFile))
                    continue;
                lstFixSourceZips.Add(parentFile);
            }

            if (lstFixSourceZips.Count == 0)
                return ReturnCode.Cancel;

            // now try and find a matching zip
            for (int i = 0; i < lstFixSourceZips.Count; i++)
            {
                RvFile testZip = lstFixSourceZips[i];
                if (testZip.ChildCount != fixZip.ChildCount)
                    continue;
                bool found = true;
                for (int j = 0; j < fixZip.ChildCount; j++)
                {
                    RvFile fixFile = fixZip.Child(j);
                    RvFile usingFile = testZip.Child(j);

                    // check this for source and destination fix types, for a full match
                    if (usingFile.RepStatus != RepStatus.NeededForFix && usingFile.RepStatus != RepStatus.Delete) { found = false; break; }
                    // need to check that the files are an exact match
                    if (!CheckFileMove(fixFile, usingFile)) { found = false; break; }
                }
                if (!found)
                    continue;

                string fixZipFullName = fixZip.FullName;
                string sourceZipFullName = testZip.FullNameCase;
                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipFullName), Path.GetFileName(fixZipFullName), "", null, "<--ZipMove", Path.GetDirectoryName(sourceZipFullName), Path.GetFileName(sourceZipFullName), ""));

                // check the source file timestamp


                try
                {
                    if (File.Exists(fixZipFullName))
                    {
                        string strPath = Path.GetDirectoryName(fixZipFullName);
                        string tempZipFilename = Path.Combine(strPath, "__RomVault.tmp");

                        if (File.Exists(tempZipFilename))
                            File.Delete(tempZipFilename);
                        File.Move(sourceZipFullName, tempZipFilename);
                        if (File.Exists(fixZipFullName))
                            File.Delete(fixZipFullName);
                        File.Move(tempZipFilename, fixZipFullName);
                    }
                    else
                    {
                        File.Move(sourceZipFullName, fixZipFullName);
                    }
                }
                catch(Exception ex)
                {
                    error = "Error reading Source File " + ex.Message;
                    return ReturnCode.FileSystemError;
                }

                // update the fixed file
                int intLoopFix = 0;
                for (int j = 0; j < fixZip.ChildCount; j++)
                {
                    fixZip.Child(j).FileAdd(testZip.Child(intLoopFix), false);

                    lstFixRomTable = FindSourceFile.GetFixFileList(fixZip.Child(j));
                    foreach (RvFile fixingFiles in lstFixRomTable)
                    {
                        string treeFullName = fixingFiles.TreeFullName;
                        if (!filesUserForFix.ContainsKey(treeFullName))
                            filesUserForFix.Add(treeFullName, fixingFiles);
                    }

                    if (testZip.Child(intLoopFix).FileRemove() == EFile.Delete)
                    {
                        testZip.ChildRemove(intLoopFix);
                        continue;
                    }
                    intLoopFix++;
                }
                fixZip.GotStatus = GotStatus.Got;
                fixZip.ZipStatus = testZip.ZipStatus;
                fixZip.FileModTimeStamp = testZip.FileModTimeStamp;

                FixFileUtils.CheckDeleteFile(testZip);

                totalFixed += totalToFix;

                return ReturnCode.Good;
            }

            return ReturnCode.Cancel;
        }
        private static bool CheckFileMove(RvFile fixFile, RvFile usingFile)
        {
            if (fixFile.Name != usingFile.Name)
                return false;
            if (fixFile.Size != usingFile.Size)
                return false;
            if (FileHeaderReader.FileHeaderReader.AltHeaderFile(usingFile.HeaderFileType))
                return false;
            if (!usingFile.FileStatusIs(FileStatus.CRCVerified))
                return false;
            if (!usingFile.FileStatusIs(FileStatus.SHA1Verified))
                return false;
            if (!usingFile.FileStatusIs(FileStatus.MD5Verified))
                return false;
            if (fixFile.CRC != null && !ArrByte.BCompare(fixFile.CRC, usingFile.CRC))
                return false;
            if (fixFile.MD5 != null && !ArrByte.BCompare(fixFile.MD5, usingFile.MD5))
                return false;
            if (fixFile.SHA1 != null && !ArrByte.BCompare(fixFile.SHA1, usingFile.SHA1))
                return false;

            return true;
        }

    }
}
