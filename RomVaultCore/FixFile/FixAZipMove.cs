using Compress;
using FileScanner;
using RomVaultCore.FixFile.FixAZipCore;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVIO;
using System;
using System.Collections.Generic;
using System.Threading;

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


            FileType archiveNeeded = fixZip.FileType;
            ZipStructure structureNeeded = fixZip.ZipDatStruct;

            if (
                structureNeeded != ZipStructure.ZipTrrnt &&
                structureNeeded != ZipStructure.ZipZSTD &&
                structureNeeded != ZipStructure.ZipTDC &&
                structureNeeded != ZipStructure.SevenZipSLZMA &&
                structureNeeded != ZipStructure.SevenZipNLZMA &&
                structureNeeded != ZipStructure.SevenZipSZSTD &&
                structureNeeded != ZipStructure.SevenZipNZSTD
                )
                return ReturnCode.Cancel;

            int totalToFix = 0;
            int totalToMove = 0;

            // check that we only have fixable/correct/missing files in the file we are trying to fix.
            int indexOfCanBeFixed = -1;
            for (int fixZipIndex = 0; fixZipIndex < fixZip.ChildCount; fixZipIndex++)
            {
                RvFile fixFile = fixZip.Child(fixZipIndex);
                if (fixFile.RepStatus == RepStatus.CanBeFixed || fixFile.RepStatus == RepStatus.CanBeFixedMIA || fixFile.RepStatus == RepStatus.CorruptCanBeFixed)
                {
                    totalToFix++;
                    totalToMove++;
                    if (fixFile.Size != 0 && indexOfCanBeFixed == -1)
                        indexOfCanBeFixed = fixZipIndex;
                    continue;
                }
                if (fixFile.RepStatus == RepStatus.Correct || fixFile.RepStatus == RepStatus.CorrectMIA)
                {
                    totalToMove++;
                    continue;
                }
                if (fixFile.RepStatus == RepStatus.Missing || fixFile.RepStatus == RepStatus.MissingMIA || fixFile.RepStatus == RepStatus.NotCollected)
                    continue;

                return ReturnCode.Cancel;
            }
            // if we only found correct, we are trrntzipping so return.
            if (indexOfCanBeFixed == -1)
                return ReturnCode.Cancel;

            // next get the list of all the match files that can fix the first file in this zip
            List<RvFile> lstFixRomTable = FindSourceFile.GetFixFileList(fixZip.Child(indexOfCanBeFixed));

            // now see which of these files are a matching structure
            List<RvFile> lstFixSourceZips = new List<RvFile>();
            foreach (RvFile file in lstFixRomTable)
            {
                if (RvFile.treeType(file) == RvTreeRow.TreeSelect.Locked)
                    continue;

                RvFile parentFile = file.Parent;
                // parentFile can be null for the zero byte dummy file.
                if (parentFile == null || parentFile.FileType != archiveNeeded || parentFile.ZipStruct != structureNeeded)
                    continue;

                if (lstFixSourceZips.Contains(parentFile))
                    continue;
                lstFixSourceZips.Add(parentFile);
            }

            if (lstFixSourceZips.Count == 0)
                return ReturnCode.Cancel;

            // now try and find a matching zip
            bool found = false;
            RvFile sourceZip = null;
            for (int i = 0; i < lstFixSourceZips.Count; i++)
            {
                found = false;
                sourceZip = lstFixSourceZips[i];
                if (sourceZip.ChildCount != totalToMove)
                    continue;

                found = true;
                int sourceZipIndexTest = 0;
                for (int fixZipIndex = 0; fixZipIndex < fixZip.ChildCount; fixZipIndex++)
                {
                    RvFile fixFile = fixZip.Child(fixZipIndex);
                    if (fixFile.RepStatus == RepStatus.Missing || fixFile.RepStatus == RepStatus.MissingMIA || fixFile.RepStatus == RepStatus.NotCollected)
                        continue;

                    RvFile usingFile = sourceZip.Child(sourceZipIndexTest);

                    // check this for source and destination fix types, for a full match
                    if (usingFile.RepStatus != RepStatus.NeededForFix && usingFile.RepStatus != RepStatus.Delete) { found = false; break; }
                    // need to check that the files are an exact match
                    if (!CheckFileMove(fixFile, usingFile)) { found = false; break; }
                    sourceZipIndexTest++;
                }
                if (found)
                    break;
            }
            if (!found)
                return ReturnCode.Cancel;


            // DO ZIP MOVE.

            string fixZipFullName = fixZip.FullName;
            string fixZipTreeFullName = fixZip.TreeFullName;
            string sourceZipFullName = sourceZip.FullNameCase;
            string sourceZipTreeFullName = sourceZip.TreeFullName;
            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipTreeFullName), Path.GetFileName(fixZipTreeFullName), "", null, fixZip.FileType == FileType.Zip ? "<--ZipMove" : "<--7ZMove", Path.GetDirectoryName(sourceZipTreeFullName), Path.GetFileName(sourceZipTreeFullName), ""));

            // check the source file timestamp
            long modTimeStamp;
            try
            {
                if (File.Exists(fixZipFullName))
                {
                    string strPath = Path.GetDirectoryName(fixZipFullName);
                    string tempZipFilename = Path.Combine(strPath, $"__RomVault.{DateTime.Now.Ticks}.tmp");

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

                while (!File.Exists(fixZipFullName))
                    Thread.Sleep(50);

                FileInfo file = new FileInfo(fixZipFullName);
                fixZip.FileModTimeStamp = file.LastWriteTime;

            }
            catch (Exception ex)
            {
                error = "Error reading Source File " + ex.Message;
                return ReturnCode.FileSystemError;
            }

            // update the fixed file


            //ScannedItem zipMoving = sourceZip.fileOut();

            //fixZip.GotStatus = GotStatus.Got;
            //fixZip.ZipStruct = sourceZip.ZipStruct;
            //fixZip.FileModTimeStamp = modTimeStamp;
            //fixZip.MergeInArchive(zipMoving);

            //sourceZip.MarkAsMissing();


            int sourceZipIndex = 0;
            for (int fixZipIndex = 0; fixZipIndex < fixZip.ChildCount; fixZipIndex++)
            {
                if (fixZip.Child(fixZipIndex).RepStatus == RepStatus.Missing || fixZip.Child(fixZipIndex).RepStatus == RepStatus.MissingMIA || fixZip.Child(fixZipIndex).RepStatus == RepStatus.NotCollected)
                    continue;

                fixZip.Child(fixZipIndex).FileMergeIn(sourceZip.Child(sourceZipIndex), false);

                lstFixRomTable = FindSourceFile.GetFixFileList(fixZip.Child(fixZipIndex));
                foreach (RvFile fixingFiles in lstFixRomTable)
                {
                    string treeFullName = fixingFiles.TreeFullName;
                    if (!filesUserForFix.ContainsKey(treeFullName))
                        filesUserForFix.Add(treeFullName, fixingFiles);
                }

                if (sourceZip.Child(sourceZipIndex).FileRemove() == EFile.Delete)
                {
                    sourceZip.ChildRemove(sourceZipIndex);
                    continue;
                }
                sourceZipIndex++;
            }
            fixZip.GotStatus = GotStatus.Got;
            fixZip.ZipStruct = sourceZip.ZipStruct;

            FixFileUtils.CheckDeleteFile(sourceZip);

            totalFixed += totalToFix;

            return ReturnCode.Good;
        }



        public static ReturnCode CheckFileMoveToSort(RvFile fixZip, ref int totalFixed, out string error)
        {
            error = "";
            if (fixZip.FileType != FileType.Zip && fixZip.FileType != FileType.SevenZip)
                return ReturnCode.Cancel;

            for (int iRom = 0; iRom < fixZip.ChildCount; iRom++)
            {
                RepStatus rs = fixZip.Child(iRom).RepStatus;
                if (rs != RepStatus.MoveToSort && rs != RepStatus.Missing && rs != RepStatus.MissingMIA && rs != RepStatus.NotCollected)
                    return ReturnCode.Cancel;
            }

            // everything needs moved tosort

            ReturnCode retCode = FixFileUtils.CreateToSortDirs(fixZip, out RvFile outDir, out string toSortFileName);
            if (retCode != ReturnCode.Good)
            {
                error = "Error Creating ToSortDirs";
                return retCode;
            }

            string fixZipFullName = fixZip.FullNameCase;
            string fixZipTreeFullName = fixZip.TreeFullName;
            string toSortFullName = Path.Combine(outDir.FullName, toSortFileName);

            Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(fixZipTreeFullName), Path.GetFileName(fixZipTreeFullName), "", null, fixZip.FileType == FileType.Zip ? "ZipMove-->" : "7ZMove-->", outDir.TreeFullName, toSortFileName, ""));

            long modTimeStamp;
            try
            {
                File.Move(fixZipFullName, toSortFullName);

                while (!File.Exists(toSortFullName))
                    Thread.Sleep(50);

                FileInfo file = new FileInfo(toSortFullName);
                modTimeStamp = file.LastWriteTime;
            }
            catch (Exception ex)
            {
                error = "Error reading Source File " + ex.Message;
                return ReturnCode.FileSystemError;
            }

            ScannedFile zipMoving = fixZip.fileOut();

            RvFile toSortGame = new RvFile(fixZip.FileType)
            {
                Name = toSortFileName,
                DatStatus = DatStatus.InToSort,
                GotStatus = GotStatus.Got,
                ZipStruct = fixZip.ZipStruct,
                FileModTimeStamp = modTimeStamp
            };
            outDir.ChildAdd(toSortGame);
            toSortGame.MergeInArchive(zipMoving);

            fixZip.MarkAsMissing();
            FixFileUtils.CheckDeleteFile(fixZip);

            return ReturnCode.Good;
        }



        private static bool CheckFileMove(RvFile fixFile, RvFile usingFile)
        {
            if (fixFile.Name != usingFile.Name)
                return false;
            if (fixFile.Size != usingFile.Size)
                return false;
            if (FileScanner.FileHeaderReader.AltHeaderFile(usingFile.HeaderFileType))
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
