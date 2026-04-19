using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Compress;
using RomVaultCore.FindFix;
using RomVaultCore.Utils;
using Path = RVIO.Path;

namespace RomVaultCore.RvDB
{

    /* 
    public enum ZipStructure
    {
        None = 0,    // No structure
        ZipTrrnt = 1,
        ZipTDC = 2, 
        ZipEXO = 3, 
        SevenZipTrrnt = 4, // this is the original t7z format
        ZipZSTD = 5,
        SevenZipSLZMA = 8, // Solid-LZMA this is rv7zip today
        SevenZipNLZMA = 9, // NonSolid-LZMA
        SevenZipSZSTD = 10, // Solid-zSTD
        SevenZipNZSTD = 11, // NonSolid-zSTD
    }
    */



    /// <summary>
    /// Core node type for RomVault's in-memory database tree.
    /// </summary>
    /// <remarks>
    /// A single <see cref="RvFile"/> can represent:
    /// - a filesystem directory (<see cref="FileType.Dir"/>)
    /// - an archive container (<see cref="FileType.Zip"/> / <see cref="FileType.SevenZip"/>)
    /// - a CHD container (<see cref="FileType.CHD"/>)
    /// - a regular file (<see cref="FileType.File"/>)
    /// - a member inside a container (<see cref="FileType.FileZip"/> / <see cref="FileType.FileSevenZip"/> / <see cref="FileType.FileCHD"/>)
    ///
    /// CHD containers behave like directories in the UI and merge pipeline:
    /// the container node (<see cref="FileType.CHD"/>) holds children that represent track blobs and
    /// optional descriptors, typically as <see cref="FileType.FileCHD"/>.
    /// </remarks>
    public partial class RvFile
    {
        public string Name { get; set; } // The Name of the File or Directory
        public string FileName { get; set; } // Found filename if different from Name (Should only be differences in Case)
        public RvFile Parent; // A link to the Parent Directory
        public RvDat Dat; // the Dat that this item belongs to
        public long FileModTimeStamp = long.MinValue; // TimeStamp to match the filesystem TimeStamp, used to know if the file has been changed.


        public bool SearchFound; // ????  used in DatUpdate & FileScanning

        private HeaderFileType _headerFileType;
        public HeaderFileType HeaderFileType { get { return _headerFileType & HeaderFileType.HeaderMask; } }
        public bool HeaderFileTypeRequired { get { return (_headerFileType & HeaderFileType.Required) != 0; } }
        public HeaderFileType HeaderFileTypeSet { set { _headerFileType = value; } }

        public readonly FileType FileType;
        private DatStatus _datStatus = DatStatus.NotInDat;
        private GotStatus _gotStatus = GotStatus.NotGot;
        private RepStatus _repStatus = RepStatus.UnSet;

        /******************* RvDir ***********************/
        private readonly List<RvFile> _children; // children items of this dir
        public readonly ReportStatus DirStatus; // Counts the status of all children for reporting in the UI

        private byte _ZipDatStruct;

        public void SetZipDatStruct(ZipStructure zipStruct, bool zipStructFix)
        {
            _ZipDatStruct = (byte)zipStruct;
            if (zipStructFix) _ZipDatStruct |= 0x80;
        }

        public ZipStructure ZipDatStruct => (ZipStructure)(_ZipDatStruct & 0x7F);
        public bool ZipDatStructFix => (_ZipDatStruct & 0x80) == 0x80;

        public ZipStructure ZipStruct; // Structure of Zip Found as a file

        public ZipStructure newZipStruct => (DatStatus == DatStatus.NotInDat || DatStatus == DatStatus.InToSort)
                                                ? ZipStruct : ZipDatStruct;

        public RvTreeRow Tree; // TreeRow for UI
        public RvGame Game { get; set; } // Game info from DAT
        public string UiDisplayName { get; set; }

        private ToSortDirType _toSortType = ToSortDirType.None;

        [Flags]
        public enum ToSortDirType
        {
            None = 0x00,
            ToSortPrimary = 0x01,
            ToSortCache = 0x02,
            ToSortFileOnly = 0x04
        }

        /******************* RvFile **********************/
        public FileGroup FileGroup;

        public ulong? Size { get; set; }
        public byte[] CRC { get; set; }
        public byte[] SHA1 { get; set; }
        public byte[] MD5 { get; set; }
        public ulong? AltSize { get; set; }
        public byte[] AltCRC { get; set; }
        public byte[] AltSHA1 { get; set; }
        public byte[] AltMD5 { get; set; }

        public string Merge { get; set; }
        public string Status { get; set; }
        private FileStatus _fileStatus;

        public int ZipFileIndex { get; set; } = -1;
        public ulong? ZipFileHeaderPosition;

        /// <summary>
        /// Parsed CHD version when this node represents a CHD container.
        /// </summary>
        public uint? CHDVersion;
        /// <summary>
        /// Status string describing CHD scan health and match outcome.
        /// </summary>
        public string ChdStatus { get; set; }
        /// <summary>
        /// Scan method used to derive member hashes (e.g. streaming vs extractcd).
        /// </summary>
        public string ChdScanMethod { get; set; }
        /// <summary>
        /// Strategy used to match CHD members to expected DAT entries.
        /// </summary>
        public string ChdHashMatchMode { get; set; }
        /// <summary>
        /// Descriptor match status (external/synthetic/true) when a CUE/GDI is involved.
        /// </summary>
        public string ChdDescriptorMatch { get; set; }

        // Properties for UI Binding
        public string Description => Game?.GetData(RvGame.GameData.Description) ?? Dat?.GetData(RvDat.DatData.Description);
        public string DateTime => FileModTimeStamp == long.MinValue ? "" : new System.DateTime(FileModTimeStamp).ToString("g");
        public string FileModDate => DateTime;
        public string CRC32 => CRC.ToHexString();
        public string SHA1Hex => SHA1.ToHexString();
        public string MD5Hex => MD5.ToHexString();
        public string AltCRC32 => AltCRC.ToHexString();
        public string AltSHA1Hex => AltSHA1.ToHexString();
        public string AltMD5Hex => AltMD5.ToHexString();
        public int ZipIndex => ZipFileIndex;
        public int InstanceCount => 0;

        public string GameName
        {
            get
            {
                string res = string.IsNullOrEmpty(FileName) ? Name : Name + " (Found: " + FileName + ")";
                if (IsChdTrustSatisfied)
                    res += " [Trusted Container]";
                if (!string.IsNullOrEmpty(ChdStatus))
                    res += " [" + ChdStatus + "]";
                return res;
            }
        }

        public bool IsChdTrustSatisfied =>
            FileType == FileType.CHD &&
            string.Equals(ChdHashMatchMode, "TrustContainer", StringComparison.OrdinalIgnoreCase);

        public string ChdFullStatus
        {
            get
            {
                if (FileType != FileType.CHD) return null;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("CHD Info:");
                if (CHDVersion.HasValue) sb.AppendLine($"- Version: V{CHDVersion}");
                if (!string.IsNullOrEmpty(ChdScanMethod)) sb.AppendLine($"- Scan Method: {ChdScanMethod}");
                if (!string.IsNullOrEmpty(ChdHashMatchMode)) sb.AppendLine($"- Hash Match: {ChdHashMatchMode}");
                if (IsChdTrustSatisfied) sb.AppendLine("- Trust Indicator: Satisfied by container trust");
                if (!string.IsNullOrEmpty(ChdDescriptorMatch)) sb.AppendLine($"- Descriptor Match: {ChdDescriptorMatch}");
                if (!string.IsNullOrEmpty(ChdStatus)) sb.AppendLine($"- Status: {ChdStatus}");
                return sb.ToString().TrimEnd();
            }
        }

        /*************************************************/




        public RvFile(FileType type)
        {
            FileType = type;
            if (!IsDirectory)
                return;

            _dirDats = new List<RvDat>(); // DAT's stored in this dir in DatRoot
            _children = new List<RvFile>(); // children items of this dir
            DirStatus = new ReportStatus(); // Counts the status of all children for reporting in the UI
        }


        /// <summary>
        /// Returns the Full recursive Tree name of this RvFile, This should Recurse back up to
        /// RomVault or a ToSort Directory
        /// </summary>
        public string TreeFullName
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
        public string TreeBarName
        {
            get
            {
                if (Parent == null)
                {
                    return Name ?? "";
                }
                string pName = Parent.TreeBarName;
                if (string.IsNullOrEmpty(pName))
                    return Name;
                return Parent.TreeBarName + "|" + Name;
            }
        }
        public string TreeFullNameCase
        {
            get
            {
                if (Parent == null)
                {
                    return NameCase ?? "";
                }

                return Path.Combine(Parent.TreeFullNameCase, NameCase);
            }
        }

        public string NameCase => string.IsNullOrWhiteSpace(FileName) ? Name : FileName;

        /// <summary>
        /// Returns te PhysicalPath of this RvFile, it first calls the above TreeFullName
        /// and then uses the returned string to look in dir DatRules for a remapping
        /// of part of the relative directory name to be mapped to another location.
        /// </summary>
        public string FullName => GetPhysicalPath(TreeFullName);

        public string FullNameCase => GetPhysicalPath(TreeFullNameCase);

        public static string GetPhysicalPath(string dirTree)
        {
            if (dirTree == null)
                return null;

            string strFullPath = "";
            int lenFound = 0;
            foreach (DirMapping dirPathMap in Settings.rvSettings.DirMappings)
            {
                if (string.IsNullOrWhiteSpace(dirPathMap.DirPath))
                    continue;

                string dirKey = dirPathMap.DirKey;
                int dirKeyLen = dirKey.Length;

                if (dirTree.Length == dirKeyLen && string.Compare(dirTree, dirKey, StringComparison.Ordinal) == 0)
                {
                    if (lenFound < dirKeyLen)
                    {
                        string dirPath = dirPathMap.DirPath;
                        lenFound = dirKeyLen;
                        strFullPath = dirPath;
                        continue;
                    }
                }

                if (dirTree.Length > dirKeyLen && string.Compare(dirTree.Substring(0, dirKeyLen + 1), dirKey + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) == 0)
                {
                    if (lenFound < dirKeyLen)
                    {
                        string dirPath = dirPathMap.DirPath;
                        lenFound = dirKeyLen;
                        strFullPath = Path.Combine(dirPath, dirTree.Substring(dirKeyLen + 1));
                    }
                }
            }

            if (strFullPath == "")
                strFullPath = dirTree;
            return strFullPath;
        }




        /// <summary>
        /// Returns the Full recursive Tree name for the Dat at this RvFile Level, This should Recurse back up to
        /// RomVault (not ToSort as there should not be any DAT's in ToSort.)
        /// The Initial RomVault directory is replaced with DatRoot
        /// </summary>
        public string DatTreeFullName => GetDatTreePath(TreeFullName);

        private static string GetDatTreePath(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                return "DatRoot";

            const string rvRoot = "RomVault";
            if (rootPath.Equals(rvRoot, StringComparison.OrdinalIgnoreCase))
                return "DatRoot";

            if (rootPath.StartsWith(rvRoot + "\\", StringComparison.OrdinalIgnoreCase) ||
                rootPath.StartsWith(rvRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "DatRoot" + rootPath.Substring(rvRoot.Length);
            }

            return "Error";
        }


        /// <summary>
        /// This takes the DatTreeFullname from above and replaces the base Datroot directory with the
        /// Settings.rvSettings.DatRoot location
        /// </summary>
        /// <param name="rootPath">Root Dat Path supplied from DatTreeFullName</param>
        /// <returns></returns>
        public static string GetDatPhysicalPath(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                return Settings.rvSettings.DatRoot;

            if (rootPath.StartsWith("ToSort", StringComparison.OrdinalIgnoreCase))
                return "Error";

            if (rootPath.StartsWith("DatRoot", StringComparison.OrdinalIgnoreCase))
                return Settings.rvSettings.DatRoot + rootPath.Substring("DatRoot".Length);

            return Settings.rvSettings.DatRoot;
        }

        public bool IsInToSort => DatStatus == DatStatus.InToSort;

        public static RvTreeRow.TreeSelect treeType(RvFile tfile)
        {
            if (tfile == null)
                return RvTreeRow.TreeSelect.Locked;
            if (tfile.Tree != null)
            {
                return tfile.Tree.Checked;
            }

            return treeType(tfile.Parent);
        }

        public DatStatus DatStatus
        {
            set
            {
                _datStatus = value;
                RepStatusReset();
            }
            get => _datStatus;
        }


        public GotStatus GotStatus
        {
            get => _gotStatus;
            set
            {
                if (DatStatus == DatStatus.InDatMIA && value == GotStatus.Got)
                {
                    Debug.WriteLine("GotMIA");
                }

                _gotStatus = value;
                RepStatusReset();
            }
        }


        public RepStatus RepStatus
        {
            get => _repStatus;
            set
            {
                RepStatus OldRepStatus = _repStatus;

                List<RepStatus> rs = RepairStatus.StatusCheck[(int)FileType, (int)_datStatus, (int)_gotStatus];
                if (rs == null || !rs.Contains(value))
                {
                    ReportError.SendAndShow(FullName + " , " + FileType + " , " + _datStatus + " , " + _gotStatus + " from: " + _repStatus + " to: " + value);
                    _repStatus = RepStatus.Error;
                }
                else
                {
                    _repStatus = value;
                }

                if (OldRepStatus == _repStatus)
                    return;

                Parent?.RepStatusUpTreeUpdate(OldRepStatus, _repStatus);
            }
        }

        [Flags]
        private enum FileFlags
        {
            Size = 1 << 0, // 0x01,
            CRC = 1 << 1, // 0x02,
            SHA1 = 1 << 2, // 0x04,
            MD5 = 1 << 3, // 0x08,
            HeaderFileType = 1 << 4, // 0x10,
            AltSize = 1 << 5, //0x20,
            AltCRC = 1 << 6, //0x40,
            AltSHA1 = 1 << 7, //0x80,
            AltMD5 = 1 << 8, //0x100,
            Merge = 1 << 9, //0x200,
            Status = 1 << 10, //0x400,
            ZipFileIndex = 1 << 11, //0x800,
            ZipFileHeader = 1 << 12, //0x1000,
            CHDVersion = 1 << 13, //0x2000,
            HasTree = 1 << 14, //0x4000,
            HasGame = 1 << 15, //0x8000,
            HasDat = 1 << 16, //0x10000,
            HasDirDat = 1 << 17, //0x20000,
            HasChildren = 1 << 18, //0x40000,
            ToSortStatus = 1 << 19, //0x80000,

            //    FileModTimeStamp = 1 << 20, //0x100000, // not used always stored
            //    DatModTimeStamp = 1 << 21, //0x200000,
            //    FileCreatedTimeStamp = 1 << 22, //0x400000,
            //    DatCreatedTimeStamp = 1 << 23, //0x800000,
        }


        public void Write(BinaryWriter bw)
        {
            int countDirDats = _dirDats?.Count ?? 0;
            int countChild = _children?.Count ?? 0;

            FileFlags fFlags = 0;

            if (Dat != null) fFlags |= FileFlags.HasDat;
            if (Tree != null) fFlags |= FileFlags.HasTree;
            if (Game != null) fFlags |= FileFlags.HasGame;
            if (countDirDats > 0) fFlags |= FileFlags.HasDirDat;
            if (countChild > 0) fFlags |= FileFlags.HasChildren;

            /************* RvFile ************/

            if (Size != null) fFlags |= FileFlags.Size;
            if (CRC != null) fFlags |= FileFlags.CRC;
            if (SHA1 != null) fFlags |= FileFlags.SHA1;
            if (MD5 != null) fFlags |= FileFlags.MD5;

            if (_headerFileType != HeaderFileType.Nothing) fFlags |= FileFlags.HeaderFileType;
            if (AltSize != null) fFlags |= FileFlags.AltSize;
            if (AltCRC != null) fFlags |= FileFlags.AltCRC;
            if (AltSHA1 != null) fFlags |= FileFlags.AltSHA1;
            if (AltMD5 != null) fFlags |= FileFlags.AltMD5;

            if (!string.IsNullOrEmpty(Merge)) fFlags |= FileFlags.Merge;
            if (!string.IsNullOrEmpty(Status)) fFlags |= FileFlags.Status;
            if (ZipFileIndex >= 0) fFlags |= FileFlags.ZipFileIndex;
            if (ZipFileHeaderPosition != null) fFlags |= FileFlags.ZipFileHeader;
            if (CHDVersion != null) fFlags |= FileFlags.CHDVersion;

            if (_toSortType != ToSortDirType.None) fFlags |= FileFlags.ToSortStatus;

            bw.Write((uint)fFlags);


            /************* RvFile ************/

            bw.Write(DB.FixNull(Name));
            bw.Write(DB.FixNull(FileName));

            bw.Write(FileModTimeStamp);

            if (Dat != null) bw.Write(Dat.DatIndex);
            bw.Write((byte)_datStatus);
            bw.Write((byte)_gotStatus);

            if (DBTypeGet.isCompressedDir(FileType))
            {
                bw.Write(_ZipDatStruct);
                bw.Write((byte)ZipStruct);
            }
            /************* RvDir ************/

            Tree?.Write(bw);
            Game?.Write(bw);

            if (countDirDats > 0)
            {
                bw.Write(countDirDats);
                for (int i = 0; i < countDirDats; i++)
                {
                    _dirDats[i].DatIndex = i;
                    _dirDats[i].Write(bw);
                }
            }

            if (countChild > 0)
            {
                bw.Write(countChild);
                for (int i = 0; i < countChild; i++)
                {
                    bw.Write((byte)_children[i].FileType);
                    _children[i].Write(bw);
                }
            }

            /************* RvFile ************/

            if (Size != null) bw.Write((ulong)Size);
            if (CRC != null) bw.WriteByteArray(CRC);
            if (SHA1 != null) bw.WriteByteArray(SHA1);
            if (MD5 != null) bw.WriteByteArray(MD5);

            if (_headerFileType != HeaderFileType.Nothing) bw.Write((byte)_headerFileType);
            if (AltSize != null) bw.Write((ulong)AltSize);
            if (AltCRC != null) bw.WriteByteArray(AltCRC);
            if (AltSHA1 != null) bw.WriteByteArray(AltSHA1);
            if (AltMD5 != null) bw.WriteByteArray(AltMD5);

            if (!string.IsNullOrEmpty(Merge)) bw.Write(Merge);
            if (!string.IsNullOrEmpty(Status)) bw.Write(Status);
            if (ZipFileIndex >= 0) bw.Write(ZipFileIndex);
            if (ZipFileHeaderPosition != null) bw.Write((long)ZipFileHeaderPosition);
            if (CHDVersion != null) bw.Write((uint)CHDVersion);

            if (_toSortType != ToSortDirType.None) bw.Write((byte)_toSortType);

            bw.Write((uint)_fileStatus);
        }

        public RvFile(BinaryReader br) : this(br, null, null, true) { }

        private RvFile(BinaryReader br, List<RvDat> parentDirDats, RvFile parent, bool baseDir = false)
        {
            if (parent == null) // this is the top of the tree so set to FileType.Dir
                FileType = FileType.Dir;
            else
                FileType = (FileType)br.ReadByte();

            if (IsDirectory)
            {
                _dirDats = new List<RvDat>(); // DAT's stored in this dir in DatRoot
                _children = new List<RvFile>(); // children items of this dir
                DirStatus = new ReportStatus(); // Counts the status of all children for reporting in the UI
            }
            Parent = parent;

            FileFlags fFlags = (FileFlags)br.ReadUInt32();

            /************* RvFile ************/

            Name = br.ReadString();
            FileName = br.ReadString();
            FileModTimeStamp = br.ReadInt64();

            Dat = null;
            if ((fFlags & FileFlags.HasDat) > 0)
            {
                int index = br.ReadInt32();
                if (index == -1)
                {
                    ReportError.SendAndShow("Dat found without an index");
                }
                else
                {
                    //if(parentDirDats!=null && index<parentDirDats.Count)
                    Dat = parentDirDats[index];
                }
            }

            // 2024/08/03 - any item in ToSort should have a datStatus of InToSort
            _datStatus = (DatStatus)br.ReadByte();
            if (parent != null && parent._datStatus == DatStatus.InToSort)
                _datStatus = DatStatus.InToSort;

            _gotStatus = (GotStatus)br.ReadByte();
            
            List<RepStatus> rs = RepairStatus.StatusCheck[(int)FileType, (int)_datStatus, (int)_gotStatus];
            _repStatus = rs?[0] ?? RepStatus.Error;

            if (DBTypeGet.isCompressedDir(FileType))
            {
                if (DBVersion.VersionNow < 3)
                {
                    if (DatStatus == DatStatus.InDatCollect)
                    {
                        switch (FileType)
                        {
                            case FileType.Zip:
                                SetZipDatStruct(ZipStructure.ZipTrrnt, true);
                                break;
                            case FileType.SevenZip:
                                SetZipDatStruct(ZipStructure.SevenZipSLZMA, true);
                                break;
                            default:
                                SetZipDatStruct(ZipStructure.ZipTrrnt, true);
                                break;
                        }
                    }
                }
                else
                {
                    _ZipDatStruct = br.ReadByte();
                }

                ZipStruct = (ZipStructure)br.ReadByte();

                // 2024-01-08 : Added to fix change in ZipStructure enum
                if (FileType == FileType.SevenZip && ZipStruct == ZipStructure.ZipTrrnt)
                    ZipStruct = ZipStructure.SevenZipSLZMA;

                // 2023-02-06 : Added to fix unknown bug in cache format coming from old versions
                if (FileType == FileType.SevenZip && DatStatus == DatStatus.InDatCollect && ZipDatStruct == ZipStructure.ZipTrrnt)
                    SetZipDatStruct(ZipStructure.SevenZipSLZMA, true);
            }

            /************* RvDir ************/

            Tree = null;
            if ((fFlags & FileFlags.HasTree) > 0)
            {
                Tree = new RvTreeRow(br);
            }
            Game = null;
            if ((fFlags & FileFlags.HasGame) > 0)
            {
                Game = new RvGame(br);
            }

            int count = (fFlags & FileFlags.HasDirDat) > 0 ? br.ReadInt32() : 0;
            _dirDats?.Clear();
            int progress = -1;
            for (int i = 0; i < count; i++)
            {
                RvDat dat = new RvDat(br) { DatIndex = i };
                _dirDats?.Add(dat);

                string datname = TreeFullName + @"\" + dat.GetData(RvDat.DatData.DatName);
                if (datname.Length >= 9 && datname.Substring(0, 9) == @"RomVault\")
                {
                    datname = datname.Substring(9);
                }

                int nextProgress = (int)(br.BaseStream.Position / DB.DivideProgress);
                if (progress != nextProgress)
                {
                    progress = nextProgress;
                    DB.ThWrk.Report(new bgwText("Loading: " + datname));
                    DB.ThWrk.Report(progress);
                }
            }

            if ((_dirDats?.Count ?? 0) > 0)
            {
                parentDirDats = _dirDats;
            }

            count = (fFlags & FileFlags.HasChildren) > 0 ? br.ReadInt32() : 0;
            _children?.Clear();

            // 2024/08/03 - any item in ToSort should have a datStatus of InToSort
            // So force all root ToSort items to be InToSort, and then all there child items will now 
            // also be set to InToSort with the above code change.

            for (int i = 0; i < count; i++)
            {
                if (baseDir && i > 0)
                    _datStatus = DatStatus.InToSort;

                RvFile tChild = new RvFile(br, parentDirDats, this);
                
                _children.Insert(i, tChild);
                DirStatus.RepStatusAddRemove(tChild.RepStatus, 1);
                if (tChild.IsDirectory)
                    DirStatus.RepStatusArrayAddRemove(tChild.DirStatus, 1);
            }
            if (baseDir)
                _datStatus = DatStatus.InDatCollect;

            /************* RvFile ************/

            Size = (fFlags & FileFlags.Size) > 0 ? (ulong?)br.ReadUInt64() : null;
            CRC = (fFlags & FileFlags.CRC) > 0 ? br.ReadByteArray() : null;
            SHA1 = (fFlags & FileFlags.SHA1) > 0 ? br.ReadByteArray() : null;
            MD5 = (fFlags & FileFlags.MD5) > 0 ? br.ReadByteArray() : null;

            _headerFileType = (fFlags & FileFlags.HeaderFileType) > 0 ? (HeaderFileType)br.ReadByte() : HeaderFileType.Nothing;
            AltSize = (fFlags & FileFlags.AltSize) > 0 ? (ulong?)br.ReadUInt64() : null;
            AltCRC = (fFlags & FileFlags.AltCRC) > 0 ? br.ReadByteArray() : null;
            AltSHA1 = (fFlags & FileFlags.AltSHA1) > 0 ? br.ReadByteArray() : null;
            AltMD5 = (fFlags & FileFlags.AltMD5) > 0 ? br.ReadByteArray() : null;
            Merge = (fFlags & FileFlags.Merge) > 0 ? br.ReadString() : null;
            Status = (fFlags & FileFlags.Status) > 0 ? br.ReadString() : null;
            ZipFileIndex = (fFlags & FileFlags.ZipFileIndex) > 0 ? br.ReadInt32() : -1;
            ZipFileHeaderPosition = (fFlags & FileFlags.ZipFileHeader) > 0 ? (ulong?)br.ReadUInt64() : null;
            CHDVersion = (fFlags & FileFlags.CHDVersion) > 0 ? (uint?)br.ReadInt32() : null;

            _toSortType = (fFlags & FileFlags.ToSortStatus) > 0 ? (ToSortDirType)br.ReadByte() : ToSortDirType.None;

            _fileStatus = (FileStatus)br.ReadUInt32();

            // fixing missing flag
            if (HeaderFileType != HeaderFileType.Nothing && (AltSize != null || AltCRC != null || AltSHA1 != null || AltMD5 != null))
                FileStatusSet(FileStatus.HeaderFileTypeFromHeader);
        }





        // this is only used to copy a file.
        [Obsolete("deprecated")]
        public void CopyTo(RvFile c)
        {
            c.Size = Size;
            c.CRC = CRC;
            c.SHA1 = SHA1;
            c.MD5 = MD5;
            c.Merge = Merge;
            c.Status = Status;
            c._fileStatus = _fileStatus;
            c.AltSize = AltSize;
            c.AltCRC = AltCRC;
            c.AltSHA1 = AltSHA1;
            c.AltMD5 = AltMD5;

            c.ZipFileIndex = ZipFileIndex;
            c.ZipFileHeaderPosition = ZipFileHeaderPosition;

            c.CHDVersion = CHDVersion;

            // think this is good enough
            if (c._headerFileType == HeaderFileType.Nothing)
                c._headerFileType = _headerFileType;

            c.Name = Name;
            c.FileName = FileName;
            //c.Parent = Parent;
            c.Dat = Dat;
            c.FileModTimeStamp = FileModTimeStamp;

            c._datStatus = _datStatus;
            c._gotStatus = _gotStatus;
            c.RepStatus = RepStatus;
            c.FileGroup = FileGroup;

            if (c._datStatus == DatStatus.InDatMIA && c._gotStatus == GotStatus.Got)
            {
                Debug.WriteLine("Found MIA");
            }
        }


        public string FileNameInsideGame()
        {
            if (Game != null || Dat != null)
            {
                return Name;
            }

            return Path.Combine(Parent.FileNameInsideGame(), Name);
        }

        public void SetDatGotStatus(DatStatus dt, GotStatus flag)
        {
            _datStatus = dt;
            _gotStatus = flag;
            if (_datStatus == DatStatus.InDatMIA && _gotStatus == GotStatus.Got)
            {
            }
            RepStatusReset();
        }

        public void RepStatusReset()
        {
            SearchFound = false;
            if (Parent != null && (RepStatus == RepStatus.UnSet || RepStatus == RepStatus.Unknown || RepStatus == RepStatus.Ignore) &&
                FileType == FileType.File && GotStatus == GotStatus.Got && DatStatus == DatStatus.NotInDat)
            {
                DatRule datRule = ReadDat.DatReader.FindDatRule(DatTreeFullName);
                List<Regex> regexList = datRule != null ? datRule.IgnoreFilesRegex : Settings.rvSettings.IgnoreFilesRegex;
                if (regexList != null)
                {
                    foreach (Regex file in regexList)
                    {
                        if (file.IsMatch(Name))
                        {
                            RepStatus = RepStatus.Ignore;
                            return;
                        }
                    }
                }
            }

            List<RepStatus> rs = RepairStatus.StatusCheck[(int)FileType, (int)_datStatus, (int)_gotStatus];
            RepStatus = rs?[0] ?? RepStatus.Error;
        }


        /****************** RvDir ***********************/
        public bool IsDirectory => FileType == FileType.Dir || FileType == FileType.Zip || FileType == FileType.SevenZip || FileType == FileType.CHD;

        public int DirDatCount => _dirDats.Count;
        public int ChildCount => _children?.Count ?? 0;


        public RvFile Child(int index)
        {
            return _children[index];
        }

        public int ChildAdd(RvFile child)
        {
            ChildNameSearch(child, out int index);
            ChildAdd(child, index);
            return index;
        }

        // This in time should become private
        public void ChildAdd(RvFile child, int index)
        {
            if (
                FileType == FileType.Dir && child.FileType == FileType.FileZip ||
                FileType == FileType.Zip && child.FileType != FileType.FileZip ||
                FileType == FileType.SevenZip && child.FileType != FileType.FileSevenZip ||
                FileType == FileType.CHD && child.FileType != FileType.FileCHD
            )
            {
                ReportError.SendAndShow("Trying to add a " + child.FileType + " to a " + FileType);
            }

            _children.Insert(index, child);
            child.Parent = this;
            RepStatusUpTreeAddRemove(child, 1);
        }

        // This in time should become private
        public void ChildRemove(int index)
        {
            RepStatusUpTreeAddRemove(_children[index], -1);
            if (_children[index].Parent == this)
            {
                _children[index].Parent = null;
            }
            _children.RemoveAt(index);
        }


        public int ChildNameSearch(FileType type, string name, out int index)
        {
            return BinarySearch.ListSearch(_children, new RvFile(type) { Name = name }, RVSorters.CompareName, out index);
        }

        public int ChildNameSearch(RvFile lName, out int index)
        {
            return BinarySearch.ListSearch(_children, lName, RVSorters.CompareName, out index);
        }


        public bool FindChild(RvFile lName, out int index)
        {
            if (ChildNameSearch(lName, out index) != 0)
            {
                ReportError.UnhandledExceptionHandler("Could not find self in Parent " + FullName);
                return false;
            }

            do
            {
                if (_children[index] == lName)
                {
                    return true;
                }
                index++;
            } while (index < _children.Count && RVSorters.CompareName(lName, _children[index]) == 0);

            return false;
        }


        private void RepStatusUpTreeUpdate(RepStatus rStatOld, RepStatus rStatNew)
        {
            DirStatus.RepStatusUpdate(rStatOld, rStatNew);
            Parent?.RepStatusUpTreeUpdate(rStatOld, rStatNew);
        }


        // this is called when adding/removing a child item to/from a directory
        private void RepStatusUpTreeAddRemove(RvFile addRemoveItem, int addRemove)
        {
            // add the status of this child item.
            DirStatus.RepStatusAddRemove(addRemoveItem.RepStatus, addRemove);
            if (addRemoveItem.IsDirectory)
            {
                // if this is a directory then also add in (or subtract out) the array of status's of the new child item
                DirStatus.RepStatusArrayAddRemove(addRemoveItem.DirStatus, addRemove);
            }

            Parent?.RepStatusUpTreeAddRemove(addRemoveItem, addRemove);
        }

        public void ToSortStatusSet(ToSortDirType flag)
        {
            _toSortType |= flag;
        }
        public void ToSortStatusClear(ToSortDirType flag)
        {
            _toSortType &= ~flag;
        }
        public bool ToSortStatusIs(ToSortDirType flag)
        {
            RvFile TestDir = this;
            while (TestDir.Parent != DB.DirRoot)
                TestDir = TestDir.Parent;

            return (TestDir._toSortType & flag) == flag;
        }

        /****************** RvFile ********************/
        public bool IsFile => FileType == FileType.File || FileType == FileType.FileZip || FileType == FileType.FileSevenZip || FileType == FileType.FileCHD;

        public void FileStatusSet(FileStatus flag)
        {
            _fileStatus |= flag;
        }

        public void FileStatusSet(FileStatus flag, RvFile copyFrom)
        {
            _fileStatus |= flag & copyFrom._fileStatus;
        }


        public void FileStatusClear(FileStatus flag)
        {
            _fileStatus &= ~flag;
        }

        public bool FileStatusIs(FileStatus flag)
        {
            return (_fileStatus & flag) == flag;
        }

        public static RvFile MakeFileZero()
        {
            RvFile fileZero = new RvFile(FileType.File);
            fileZero.Name = "ZeroFile";
            fileZero.Size = 0;

            fileZero.CRC = VarFix.CleanMD5SHA1("00000000", 8);
            fileZero.MD5 = VarFix.CleanMD5SHA1("d41d8cd98f00b204e9800998ecf8427e", 32);
            fileZero.SHA1 = VarFix.CleanMD5SHA1("da39a3ee5e6b4b0d3255bfef95601890afd80709", 40);

            fileZero.GotStatus = GotStatus.Got;
            fileZero.DatStatus = DatStatus.InToSort;
            return fileZero;
        }


    }
}
