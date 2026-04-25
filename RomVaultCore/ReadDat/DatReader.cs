/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using Compress;
using DATReader;
using DATReader.DatClean;
using DATReader.DatStore;
using DATReader.DatWriter;
using DATReader.Utils;
using RomVaultCore.RvDB;
using RomVaultCore.Storage.Dat;
using RVIO;

namespace RomVaultCore.ReadDat
{
    public static class DatReader
    {
        private static ThreadWorker _thWrk;


        private static void ReadError(string filename, string error)
        {
            _thWrk.Report(new bgwShowError(filename, error));
        }


        public static DatRule FindDatRule(string datName)
        {
            ReportError.LogOut($"FindDatRule: Dat Name is {datName}");

            DatRule use = null;
            int longest = -1;
            int datNameLength = datName.Length;
            foreach (DatRule s in Settings.rvSettings.DatRules)
            {
                if (s.DirKey.Length < 8 || s.DirKey.Substring(0, 8) != "RomVault")
                    continue;
                string DirKey = "DatRoot" + s.DirKey.Substring(8) + System.IO.Path.DirectorySeparatorChar;

                int dirKeyLen = DirKey.Length;
                if (dirKeyLen > datNameLength)
                    continue;

                if (datName.Substring(0, dirKeyLen) != DirKey)
                    continue;

                if (dirKeyLen < longest)
                    continue;

                longest = dirKeyLen;
                use = s;
            }
            if (use == null)
                ReportError.LogOut("Use is Null");
            else
                ReportError.LogOut($"Using Rule for Dir {use.DirKey} {use.DirPath}");
            return use;
        }

        public static bool ReadInDatFile(DatImportDat datFile, ThreadWorker thWrk)
        {
            try
            {
                _thWrk = thWrk;
                string extraDirName = null;

                string datRootFullName = datFile.DatFullName;
                string fullPath = RvFile.GetDatPhysicalPath(datRootFullName);
                Debug.WriteLine("Reading Dat " + fullPath);

                DatRead.ReadDat(fullPath, ReadError, out DatHeader datHeader);
                if (datHeader == null)
                {
                    datFile.datHeader = null;
                    return false;
                }

                string dirNameRule = Path.GetDirectoryName(datRootFullName) + Path.DirSeparatorChar;

                if (
                       !datFile.Flag(DatFlags.MultiDatOverride) && datHeader.Dir != "noautodir" &&
                       (datFile.Flag(DatFlags.MultiDatsInDirectory) || !string.IsNullOrEmpty(datHeader.RootDir))
                    )
                {
                    // if we are auto adding extra directories then create a new directory.
                    extraDirName = "";
                    if (string.IsNullOrEmpty(extraDirName) && datFile.Flag(DatFlags.UseDescriptionAsDirName) && !string.IsNullOrWhiteSpace(datHeader.Description))
                        extraDirName = datHeader.Description;
                    if (string.IsNullOrEmpty(extraDirName))
                        extraDirName = datHeader.RootDir;
                    if (string.IsNullOrEmpty(extraDirName))
                        extraDirName = datHeader.Name;
                    if (string.IsNullOrEmpty(extraDirName))
                        extraDirName = Path.GetFileNameWithoutExtension(fullPath);

                    dirNameRule += VarFix.CleanFileName(extraDirName) + Path.DirSeparatorChar;
                }

                bool outputTestDATs = false;
                string outDirName=datFile.DatFullName.Substring(8).Replace("\\","-");

                ReportError.LogOut($"DatRule {dirNameRule}");

                DatRule datRule = FindDatRule(dirNameRule);

                // 1
                DatClean.CleanFilenames(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-01.dat", datHeader);


                // 2
                switch (datRule.Filter)
                {
                    case FilterType.CHDsOnly:
                        DatClean.RemoveNonCHD(datHeader.BaseDir);
                        break;
                    case FilterType.RomsOnly:
                        DatClean.RemoveCHD(datHeader.BaseDir);
                        break;
                }

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-02.dat", datHeader);

                // 3
                DatClean.RemoveNoDumps(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-03.dat", datHeader);

                // 4
                if (datRule.UseIdForName)
                    DatClean.DatSetAddIdNumbers(datHeader.BaseDir, "");

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-04.dat", datHeader);


                // 5
                if (datRule.AddCategorySubDirs)
                    DatClean.AddCategory(datHeader.BaseDir, datRule.CategoryOrder);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-05.dat", datHeader);

                // 6
                SetMergeType(datRule, datHeader);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-06.dat", datHeader);

                // 7
                DatSetStatus.SetStatus(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-07.dat", datHeader);

                // 8
                if (datRule.Merge != MergeType.NonMerged)
                    DatClean.CheckDeDuped(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-08.dat", datHeader);

                // 9
                GetCompressionMethod(datRule, datHeader, out FileType ft, out ZipStructure zs);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-09.dat", datHeader);

                // Set the compression methods.
                if (datRule.Compression == FileType.FileOnly)
                {
                    ft = FileType.Dir;
                    zs = ZipStructure.None;
                }

                // 10
                if (datRule.SingleArchive)
                {

                    DatClean.DirectoryFlattern(datHeader.BaseDir);
                    if (outputTestDATs)
                        DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-10a.dat", datHeader);

                    DatClean.MakeDatSingleLevel(datHeader, datRule.UseDescriptionAsDirName, datRule.SubDirType, ft == FileType.Dir, datRule.AddCategorySubDirs, datRule.CategoryOrder);
                }

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-10.dat", datHeader);

                // 11: SetFileTypes / This also sorts the dirs into there type sort orders
                DatSetCompressionType.SetType(datHeader.BaseDir, ft, zs, datRule.ConvertWhileFixing);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-11.dat", datHeader);

                // 12: Remove unneeded directories from Zip's / 7Z's 
                DatClean.RemoveUnNeededDirectories(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-12.dat", datHeader);

                // 13: Remove DateTime from anything not TDC or EXO
                DatClean.RemoveAllDateTime(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-13.dat", datHeader);

                // 14: Remove CHD's from Zip's / 7Z's
                DatSetMoveCHDs.MoveUpCHDs(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-14.dat", datHeader);

                // 15: Directory expand the items not in Zip's / 7Z's
                DatClean.DirectoryExpand(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-15.dat", datHeader);

                // 16: Clean Filenames
                DatClean.CleanFileNamesFull(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-16.dat", datHeader);

                // 17: FixDupes
                DatClean.FixDupes(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-17.dat", datHeader);

                // 18: Remove empty directories
                DatClean.RemoveEmptyDirectories(datHeader.BaseDir);

                if (outputTestDATs)
                    DatXMLWriter.WriteDat($"D:\\outPath\\{outDirName}-18.dat", datHeader);



                if (!string.IsNullOrWhiteSpace(extraDirName))
                {
                    datHeader.BaseDir.Name = VarFix.CleanFileName(extraDirName);
                    DatDir newDirectory = new DatDir("", FileType.Dir);
                    newDirectory.ChildAdd(datHeader.BaseDir);
                    datHeader.BaseDir = newDirectory;
                    datFile.SetFlag(DatFlags.AutoAddedDirectory, true);
                }
                else
                {
                    datFile.SetFlag(DatFlags.AutoAddedDirectory, false);
                }

                datFile.headerType = datRule.HeaderType;
                datFile.datHeader = datHeader;

                HeaderFileType headerFileType = FileScanner.FileHeaderReader.GetFileTypeFromHeader(datHeader.Header);
                if (headerFileType != HeaderFileType.Nothing)
                {
                    switch (datFile.headerType)
                    {
                        case HeaderType.Optional:
                            // Do Nothing
                            break;
                        case HeaderType.Headerless:
                            // remove header
                            headerFileType = HeaderFileType.Nothing;
                            break;
                        case HeaderType.Headered:
                            headerFileType |= HeaderFileType.Required;
                            break;
                    }
                }

                DatClean.SetExt(datHeader.BaseDir, headerFileType);

                DatClean.ClearDescription(datHeader.BaseDir);
                return true;
            }
            catch (Exception e)
            {
                string datRootFullName = datFile?.DatFullName;
                throw new Exception("Error is DAT " + datRootFullName + " " + e.Message);
            }
        }

        private static void SetMergeType(DatRule datRule, DatHeader dh)
        {
            bool hasRom = DatHasRomOf.HasRomOf(dh.BaseDir);
            if (hasRom)
            {
                MergeType mt = datRule.Merge;
                if (!datRule.MergeOverrideDAT)
                {
                    switch (dh.MergeType?.ToLower())
                    {
                        case "full":
                            mt = MergeType.Merge;
                            break;
                        case "nonmerge":
                            mt = MergeType.NonMerged;
                            break;
                        case "split":
                            mt = MergeType.Split;
                            break;
                    }
                }

                DatClean.DatSetMatchIDs(dh.BaseDir);

                switch (mt)
                {
                    case MergeType.Merge:
                        DatClean.DatSetMakeMergeSet(dh.BaseDir, dh.MameXML);
                        break;
                    case MergeType.NonMerged:
                        DatClean.DatSetMakeNonMergeSet(dh.BaseDir);
                        break;
                    case MergeType.Split:
                        DatClean.DatSetMakeSplitSet(dh.BaseDir);
                        break;
                    default:
                        break;
                }

                DatClean.RemoveDeviceRef(dh.BaseDir);

                DatClean.RemoveDupes(dh.BaseDir, !dh.MameXML, mt != MergeType.NonMerged);
                DatClean.RemoveEmptySets(dh.BaseDir);
            }

        }


        private static void GetCompressionMethod(DatRule datRule, DatHeader dh, out FileType ft, out ZipStructure zs)
        {
            ft = datRule.Compression == FileType.FileOnly ? FileType.File : datRule.Compression;

            zs = datRule.CompressionSub;
            if (!datRule.CompressionOverrideDAT && datRule.Compression != FileType.FileOnly)
            {
                switch (dh.Compression?.ToLower())
                {
                    case "tdc":
                        ft = FileType.Zip;
                        zs = ZipStructure.ZipTDC;
                        break;

                    case "unzip":
                    case "file":
                    case "fileonly":
                        ft = FileType.Dir;
                        zs = ZipStructure.None;
                        break;
                    case "7zip":
                    case "7z":
                        ft = FileType.SevenZip;
                        zs = ZipStructure.SevenZipSLZMA;
                        break;
                    case "zip":
                        ft = FileType.Zip;
                        zs = ZipStructure.ZipTrrnt;
                        break;

                }
            }
        }
    }
}