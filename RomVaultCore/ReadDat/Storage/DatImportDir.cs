using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVIO;
using System.Collections.Generic;
using Path = RVIO.Path;

namespace RomVaultCore.Storage.Dat
{
    public class DatImportDir
    {
        public readonly string Name;
        private DatImportDir Parent;

        private readonly List<DatImportDir> _importChildDirs = new List<DatImportDir>();
        private readonly List<DatImportDat> _importDatFiles = new List<DatImportDat>();

        private DatImportDir(string name)
        {
            Name = name;
        }


        private string TreeFullName
        {
            get
            {
                if (Parent == null)
                {
                    return Name ?? "";
                }
                return Path.Combine(Parent.TreeFullName, Name);
            }
        }
        private string DatTreeFullName => GetDatTreePath(TreeFullName);

        private static string GetDatTreePath(string rootPath)
        {
            if (rootPath == "")
            {
                return "DatRoot";
            }
            if (rootPath.StartsWith("RomVault"))
            {
                return @"DatRoot" + rootPath.Substring(8);
            }

            return "Error";
        }

        #region DatImportDir


        internal int ChildDirsCount => _importChildDirs?.Count ?? 0;
        internal DatImportDir ChildDir(int index)
        {
            return _importChildDirs[index];
        }
        private int ChildDirAdd(DatImportDir child)
        {
            BinarySearch.ListSearch(_importChildDirs, child, RVSorters.CompareDirName, out int index);
            _importChildDirs.Insert(index, child);
            child.Parent = this;
            return index;
        }

        private void ChildDirRemove(int index)
        {
            if (_importChildDirs[index].Parent == this)
            {
                _importChildDirs[index].Parent = null;
            }
            _importChildDirs.RemoveAt(index);
        }
        #endregion


        #region DatImportDat
        internal int DatFilesCount => _importDatFiles.Count;

        internal DatImportDat DirDat(int index)
        {
            return _importDatFiles[index];
        }

        private void DirDatAdd(DatImportDat dat)
        {
            BinarySearch.ListSearch(_importDatFiles, dat, RVSorters.CompareDatName, out int index);
            _importDatFiles.Insert(index, dat);
        }
        #endregion










        internal static bool RecursiveDatTree(out DatImportDir datRoot, out int _datCount)
        {
            datRoot = new DatImportDir("RomVault");

            // build a datRoot tree of the DAT's in DatRoot, and count how many dats are found
            return RecursiveDatTree(datRoot, out _datCount);
        }

        private static bool RecursiveDatTree(DatImportDir tDir, out int datCount)
        {
            datCount = 0;
            string strPath = RvFile.GetDatPhysicalPath(tDir.DatTreeFullName);

            if (!Directory.Exists(strPath))
            {
                ReportError.Show($"Path: {strPath} Not Found.");
                return false;
            }

            DirectoryInfo oDir = new DirectoryInfo(strPath);

            List<FileInfo> lFilesIn = new List<FileInfo>();

            FileInfo[] oFilesIn = oDir.GetFiles("*.dat");
            lFilesIn.AddRange(oFilesIn);
            oFilesIn = oDir.GetFiles("*.xml");
            lFilesIn.AddRange(oFilesIn);
            oFilesIn = oDir.GetFiles("*.datz");
            lFilesIn.AddRange(oFilesIn);

            datCount += lFilesIn.Count;
            foreach (FileInfo file in lFilesIn)
            {
                DatImportDat tDat = new DatImportDat();
                tDat.DatFullName = Path.Combine(tDir.DatTreeFullName, file.Name);
                tDat.TimeStamp = file.LastWriteTime;

                // this works passing in the full DirectoryName of the Dat, because the rules
                // has a directory separator character added to the end of them,
                // so they match up to the directory name in this full Directory Name.
                DatRule datRule = DatReader.FindDatRule(tDat.DatFullName);

                tDat.SetFlag(DatFlags.MultiDatOverride, datRule.MultiDATDirOverride);
                tDat.SetFlag(DatFlags.UseDescriptionAsDirName, datRule.UseDescriptionAsDirName);
                tDat.SetFlag(DatFlags.SingleArchive, datRule.SingleArchive);
                tDat.SetFlag(DatFlags.UseIdForName, datRule.UseIdForName);
                tDat.SubDirType = datRule.SubDirType;
                tDir.DirDatAdd(tDat);
            }

            if (tDir.DatFilesCount > 1)
            {
                for (int i = 0; i < tDir.DatFilesCount; i++)
                {
                    tDir.DirDat(i).SetFlag(DatFlags.MultiDatsInDirectory, true);
                }
            }

            DirectoryInfo[] oSubDir = oDir.GetDirectories();

            foreach (DirectoryInfo t in oSubDir)
            {
                DatImportDir cDir = new DatImportDir(t.Name);
                int index = tDir.ChildDirAdd(cDir);

                RecursiveDatTree(cDir, out int retDatCount);
                datCount += retDatCount;

                if (retDatCount == 0)
                {
                    tDir.ChildDirRemove(index);
                }
            }

            return true;
        }

    }
}