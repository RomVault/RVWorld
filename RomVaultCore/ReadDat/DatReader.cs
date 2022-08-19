/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2022                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using DATReader;
using DATReader.DatClean;
using DATReader.DatStore;
using DATReader.Utils;
using RomVaultCore.RvDB;
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

        public static RvFile ReadInDatFile(RvDat datFile, ThreadWorker thWrk, out string extraDirName)
        {
            try
            {
                _thWrk = thWrk;
                extraDirName = null;

                string datRootFullName = datFile.GetData(RvDat.DatData.DatRootFullName);
                string fullPath = RvFile.GetDatPhysicalPath(datRootFullName);
                Debug.WriteLine("Reading Dat " + fullPath);

                DatRead.ReadDat(fullPath, ReadError, out DatHeader dh);
                if (dh == null)
                    return null;

                string dirNameRule = Path.GetDirectoryName(datRootFullName) + Path.DirSeparatorChar;

                if (
                       !datFile.MultiDatOverride && dh.Dir != "noautodir" &&
                       (datFile.MultiDatsInDirectory || !string.IsNullOrEmpty(dh.RootDir))
                    )
                {
                    // if we are auto adding extra directories then create a new directory.
                    extraDirName = "";
                    if (string.IsNullOrEmpty(extraDirName) && datFile.UseDescriptionAsDirName && !string.IsNullOrWhiteSpace(dh.Description))
                        extraDirName = dh.Description;
                    if (string.IsNullOrEmpty(extraDirName))
                        extraDirName = dh.RootDir;
                    if (string.IsNullOrEmpty(extraDirName))
                        extraDirName = dh.Name;
                    if (string.IsNullOrEmpty(extraDirName))
                        extraDirName = Path.GetFileNameWithoutExtension(fullPath);

                    dirNameRule += VarFix.CleanFileName(extraDirName) + Path.DirSeparatorChar;
                }

                ReportError.LogOut($"DatRule {dirNameRule}");

                DatRule datRule = FindDatRule(dirNameRule);

                DatClean.CleanFilenames(dh.BaseDir);

                switch (datRule.Filter)
                {
                    case FilterType.CHDsOnly:
                        DatClean.RemoveNonCHD(dh.BaseDir);
                        break;
                    case FilterType.RomsOnly:
                        DatClean.RemoveCHD(dh.BaseDir);
                        break;
                }

                DatClean.RemoveNoDumps(dh.BaseDir);

                SetMergeType(datRule, dh);

                if (datRule.Merge!=MergeType.NonMerged)
                    DatClean.CheckDeDuped(dh.BaseDir);

                if (datRule.SingleArchive)
                    DatClean.MakeDatSingleLevel(dh, datRule.UseDescriptionAsDirName, datRule.SubDirType, isFile(datRule, dh));

                DatClean.RemoveUnNeededDirectories(dh.BaseDir);

                SetCompressionMethod(datRule, dh); // This sorts the files into the required dir order for the set compression type. (And also sets '\' characters to '/' in zip files.)

                DatClean.DirectoryExpand(dh.BaseDir); // this works because we only expand files, so the order inside the zip / 7z does not matter.

                DatClean.RemoveEmptyDirectories(dh.BaseDir);

                DatClean.CleanFilenamesFixDupes(dh.BaseDir); // you may get repeat filenames inside Zip's / 7Z's and they may not be sorted to find them by now.


                RvFile newDir = ExternalDatConverter.ConvertFromExternalDat(dh, datFile, datRule.HeaderType);
                return newDir;
            }
            catch (Exception e)
            {
                string datRootFullName = datFile?.GetData(RvDat.DatData.DatRootFullName);

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

                switch (mt)
                {
                    case MergeType.Merge:
                        DatClean.DatSetMakeMergeSet(dh.BaseDir, dh.MameXML);
                        break;
                    case MergeType.NonMerged:
                        DatClean.DatSetMakeNonMergeSet(dh.BaseDir);
                        DatSetStatus.SetStatus(dh.BaseDir);
                        break;
                    case MergeType.Split:
                        DatClean.DatSetMakeSplitSet(dh.BaseDir);
                        //DatClean.RemoveNotCollected(dh.BaseDir);
                        break;
                    default:
                        DatSetStatus.SetStatus(dh.BaseDir);
                        break;
                }

                DatClean.RemoveDeviceRef(dh.BaseDir);

                DatClean.RemoveDupes(dh.BaseDir, !dh.MameXML, mt != MergeType.NonMerged);
                DatClean.RemoveEmptySets(dh.BaseDir);
            }
            else
                DatSetStatus.SetStatus(dh.BaseDir);

        }

        private static bool isFile(DatRule datRule, DatHeader dh)
        {
            FileType ft = datRule.Compression;
            if (!datRule.CompressionOverrideDAT)
            {
                switch (dh.Compression?.ToLower())
                {
                    case "unzip":
                    case "file":
                        ft = FileType.Dir;
                        break;
                    case "7zip":
                    case "7z":
                        ft = FileType.SevenZip;
                        break;
                    case "zip":
                        ft = FileType.Zip;
                        break;

                }
            }

            if (Settings.rvSettings.FilesOnly)
                ft = FileType.Dir;

            return ft == FileType.Dir;
        }

        private static void SetCompressionMethod(DatRule datRule, DatHeader dh)
        {
            FileType ft = datRule.Compression;
            if (!datRule.CompressionOverrideDAT)
            {
                switch (dh.Compression?.ToLower())
                {
                    case "unzip":
                    case "file":
                        ft = FileType.Dir;
                        break;
                    case "7zip":
                    case "7z":
                        ft = FileType.SevenZip;
                        break;
                    case "zip":
                        ft = FileType.Zip;
                        break;

                }
            }

            if (Settings.rvSettings.FilesOnly)
                ft = FileType.Dir;

            switch (ft)
            {
                case FileType.Dir:
                    DatSetCompressionType.SetFile(dh.BaseDir);
                    return;
                case FileType.SevenZip:
                    DatSetCompressionType.SetZip(dh.BaseDir, true);
                    return;
                default:
                    DatSetCompressionType.SetZip(dh.BaseDir);
                    return;
            }
        }
    }
}