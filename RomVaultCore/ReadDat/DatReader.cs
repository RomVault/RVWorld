/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using Compress;
using DATReader;
using DATReader.DatClean;
using DATReader.DatStore;
using DATReader.Utils;
using RomVaultCore.RvDB;
using RomVaultCore.Storage.Dat;
using RVIO;

namespace RomVaultCore.ReadDat
{
    /// <summary>
    /// Loads DAT files and applies DAT-driven rules to the database tree.
    /// </summary>
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
                if (string.IsNullOrWhiteSpace(s.DirKey))
                    continue;

                string ruleKey = s.DirKey.Trim().Replace('/', '\\');
                while (ruleKey.EndsWith("\\", StringComparison.Ordinal))
                    ruleKey = ruleKey.Substring(0, ruleKey.Length - 1);

                const string rvRoot = "RomVault";
                string DirKey;
                if (ruleKey.Equals(rvRoot, StringComparison.OrdinalIgnoreCase))
                    DirKey = "DatRoot" + System.IO.Path.DirectorySeparatorChar;
                else if (ruleKey.StartsWith(rvRoot + "\\", StringComparison.OrdinalIgnoreCase))
                    DirKey = "DatRoot" + ruleKey.Substring(rvRoot.Length) + System.IO.Path.DirectorySeparatorChar;
                else
                    continue;

                int dirKeyLen = DirKey.Length;
                if (dirKeyLen > datNameLength)
                    continue;

                if (!string.Equals(datName.Substring(0, dirKeyLen), DirKey, StringComparison.OrdinalIgnoreCase))
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

                ReportError.LogOut($"DatRule {dirNameRule}");

                DatRule datRule = FindDatRule(dirNameRule);
                DatRule globalRule = FindDatRule("DatRoot" + System.IO.Path.DirectorySeparatorChar);

                // 1
                DatClean.CleanFilenames(datHeader.BaseDir);

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

                // 3
                DatClean.RemoveNoDumps(datHeader.BaseDir);

                // 4
                if (datRule.UseIdForName)
                    DatClean.DatSetAddIdNumbers(datHeader.BaseDir, "");

                // 5
                if (datRule.AddCategorySubDirs)
                    DatClean.AddCategory(datHeader.BaseDir, datRule.CategoryOrder);

                // 6
                SetMergeType(datRule, datHeader);

                // 7
                if (datRule.Merge != MergeType.NonMerged)
                    DatClean.CheckDeDuped(datHeader.BaseDir);


                GetCompressionMethod(datRule, datHeader, out FileType ft, out ZipStructure zs);

                // Set the compression methods.
                if (datRule.Compression == FileType.FileOnly)
                {
                    ft = FileType.Dir;
                    zs = ZipStructure.None;
                }

                // 8
                // CHD container mode is per-set; forcing single-archive at DAT level collapses
                // all sets into one giant CHD container and breaks expected game grouping.
                if (datRule.SingleArchive && ft != FileType.CHD)
                    DatClean.MakeDatSingleLevel(datHeader, datRule.UseDescriptionAsDirName, datRule.SubDirType, ft == FileType.Dir, datRule.AddCategorySubDirs, datRule.CategoryOrder);

                // 9: SetFileTypes / This also sorts the dirs into there type sort orders
                DatSetCompressionType.ChdStrictCueGdi = globalRule?.ChdStrictCueGdi ?? datRule.ChdStrictCueGdi;
                DatSetCompressionType.ChdKeepCueGdi = Settings.rvSettings.ChdKeepCueGdi;
                DatSetCompressionType.SetType(datHeader.BaseDir, ft, zs, datRule.ConvertWhileFixing);

                // 10: Remove unneeded directories from Zip's / 7Z's 
                DatClean.RemoveUnNeededDirectories(datHeader.BaseDir);

                // 11: Remove DateTime from anything not TDC or EXO
                DatClean.RemoveAllDateTime(datHeader.BaseDir);

                // 12: Remove CHD's from Zip's / 7Z's
                DatSetMoveCHDs.MoveUpCHDs(datHeader.BaseDir);

                // 13: Directory expand the items not in Zip's / 7Z's
                DatClean.DirectoryExpand(datHeader.BaseDir);

                // 14: Clean Filenames
                DatClean.CleanFileNamesFull(datHeader.BaseDir);

                // 15: FixDupes
                DatClean.FixDupes(datHeader.BaseDir);

                // 16: Remove empty directories
                DatClean.RemoveEmptyDirectories(datHeader.BaseDir);



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

            DatSetStatus.SetStatus(dh.BaseDir);
        }


        private static void GetCompressionMethod(DatRule datRule, DatHeader dh, out FileType ft, out ZipStructure zs)
        {
            ft = datRule.Compression == FileType.FileOnly ? FileType.File : datRule.Compression;

            zs = datRule.CompressionSub;
            if (datRule.DiscArchiveAsCHD)
            {
                ft = FileType.CHD;
                zs = ZipStructure.None;
                return;
            }
            if (!datRule.CompressionOverrideDAT && datRule.Compression!=FileType.FileOnly)
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
