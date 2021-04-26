using System;
using System.Collections.Generic;
using System.IO;
using Compress;
using FileHeaderReader;
using RVCore.FindFix;
using RVCore.Utils;
using Path = RVIO.Path;

namespace RVCore.RvDB
{
    public enum FileType
    {
        Unknown,
        Dir,
        Zip,
        SevenZip,
        File,
        ZipFile,
        SevenZipFile
    }

    public enum DatStatus
    {
        InDatCollect,
        InDatMerged,
        InDatBad,
        NotInDat,
        InToSort
    }

    public enum GotStatus
    {
        NotGot,
        Got,
        Corrupt,
        FileLocked
    }

    public class RvFile
    {
        public string Name; // The Name of the File or Directory
        public string FileName; // Found filename if different from Name (Should only be differences in Case)
        public RvFile Parent; // A link to the Parent Directory
        public RvDat Dat; // the Dat that this item belongs to
        public long FileModTimeStamp; // TimeStamp to match the filesystem TimeStamp, used to know if the file has been changed.

#if dt
        public long? DatModTimeStamp; // TimeStamp from the DAT if there is one
        public long? FileCreatedTimeStamp;
        public long? DatCreatedTimeStamp;
        public long? FileLastAccessTimeStamp;
        public long? DatLastAccessTimeStamp;
#endif  

        public bool SearchFound; // ????  used in DatUpdate & FileScanning

        public HeaderFileType HeaderFileType;

        public readonly FileType FileType;
        private DatStatus _datStatus = DatStatus.NotInDat;
        private GotStatus _gotStatus = GotStatus.NotGot;
        private RepStatus _repStatus = RepStatus.UnSet;

        /******************* RvDir ***********************/
        private readonly List<RvDat> _dirDats; // DAT's stored in this dir in DatRoot
        private readonly List<RvFile> _children; // children items of this dir
        public readonly ReportStatus DirStatus; // Counts the status of all children for reporting in the UI

        public RvTreeRow Tree; // TreeRow for UI
        public RvGame Game; // Game info from DAT
        public string UiDisplayName;

        public ZipStatus ZipStatus; // if Dir is a ZIP, some fix/status of the ZIP

        /******************* RvFile **********************/
        public FileGroup FileGroup;

        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public ulong? AltSize;
        public byte[] AltCRC;
        public byte[] AltSHA1;
        public byte[] AltMD5;

        public string Merge = "";
        public string Status;
        private FileStatus _fileStatus;

        public int ZipFileIndex = -1;
        public ulong? ZipFileHeaderPosition;

        public uint? CHDVersion;

        /*************************************************/


        public RvFile(FileType type)
        {
            FileType = type;
            if (!IsDir)
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
        public string TreeFullNameCase
        {
            get
            {
                if (Parent == null)
                {
                    return string.IsNullOrWhiteSpace(FileName) ? (string.IsNullOrWhiteSpace(Name) ? "" : Name) : FileName;
                }

                return Path.Combine(Parent.TreeFullNameCase, string.IsNullOrWhiteSpace(FileName) ? Name : FileName);
            }
        }

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
            foreach (DatRule dirPathMap in Settings.rvSettings.DatRules)
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
        /// Returns the Full recursive Three name for the Dat at this RvFile Level, This should Recurse back up to
        /// RomVault (not ToSort as there should not be any DAT's in ToSort.)
        /// The Initial RomVault directory is replaced with DatRoot
        /// </summary>
        public string DatTreeFullName => GetDatTreePath(TreeFullName);

        private static string GetDatTreePath(string rootPath)
        {
            if (rootPath == "")
            {
                return "DatRoot";
            }
            if (rootPath.Substring(0, 6) == "ToSort")
            {
                return "Error";
            }
            if (rootPath.Substring(0, 8) == "RomVault")
            {
                return @"DatRoot" + rootPath.Substring(8);
            }

            return Settings.rvSettings.DatRoot;
        }


        /// <summary>
        /// This takes the DatTreeFullname from above and replaces the base Datroot directory with the
        /// Settings.rvSettings.DatRoot location
        /// </summary>
        /// <param name="rootPath">Root Dat Path supplied from DatTreeFullName</param>
        /// <returns></returns>
        public static string GetDatPhysicalPath(string rootPath)
        {
            if (rootPath == "")
            {
                return Settings.rvSettings.DatRoot;
            }
            if (rootPath.Substring(0, 6) == "ToSort")
            {
                return "Error";
            }
            if (rootPath.Substring(0, 7) == "DatRoot")
            {
                return Settings.rvSettings.DatRoot + rootPath.Substring(7);
            }

            return Settings.rvSettings.DatRoot;
        }

        public bool IsInToSort
        {
            get
            {
                RvFile upTree = this;
                while (upTree != null && upTree.Parent != DB.DirTree)
                    upTree = upTree.Parent;

                return upTree != DB.DirTree.Child(0);
            }
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
                _gotStatus = value;
                RepStatusReset();
            }
        }


        public RepStatus RepStatus
        {
            get => _repStatus;
            set
            {
                Parent?.UpdateRepStatusUpTree(_repStatus, -1);

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

                Parent?.UpdateRepStatusUpTree(_repStatus, 1);
            }
        }

        [Flags]
        private enum FileFlags
        {
            Size = 0x01,
            CRC = 0x02,
            SHA1 = 0x04,
            MD5 = 0x08,
            HeaderFileType = 0x10,
            AltSize = 0x20,
            AltCRC = 0x40,
            AltSHA1 = 0x80,
            AltMD5 = 0x100,
            Merge = 0x200,
            Status = 0x400,
            ZipFileIndex = 0x800,
            ZipFileHeader = 0x1000,
            CHDVersion = 0x2000,
            HasTree = 0x4000,
            HasGame = 0x8000,
            HasDat = 0x10000,
            HasDirDat = 0x20000,
            HasChildren = 0x40000,

            FileModTimeStamp = 0x100000, // not used always stored
            DatModTimeStamp = 0x200000,
            FileCreatedTimeStamp = 0x400000,
            DatCreatedTimeStamp = 0x800000,
            FileLastAccessTimeStamp = 0x400000,
            DatLastAccessTimeStamp = 0x800000,
        }

        /*
        public JObject WriteJson()
        {
            JObject jObj = new JObject();

            int countDirDats = _dirDats.Count;
            int countChild = _children.Count;

            // RvFile

            jObj.Add("FileType", FileType.ToString());
            jObj.Add("Name", DB.Fn(Name));
            jObj.Add("FileName", DB.Fn(FileName));
            jObj.Add("TimeStamp", TimeStamp);
            if (Dat != null) jObj.Add("Dat", Dat.DatIndex);
            jObj.Add("DatStatus", _datStatus.ToString());
            jObj.Add("GotStatus", _gotStatus.ToString());
            jObj.Add("RepStatus", _repStatus.ToString());

            if (Size != null) jObj.Add("Size", (ulong)Size);
            if (CRC != null) jObj.Add("CRC", CRC.ToHexString());
            if (SHA1 != null) jObj.Add("SHA1", SHA1.ToHexString());
            if (MD5 != null) jObj.Add("MD5", MD5.ToHexString());

            if (HeaderFileType != HeaderFileType.Nothing) jObj.Add("HeaderFileType", HeaderFileType.ToString());
            if (AltSize != null) jObj.Add("AltSize", (ulong)AltSize);
            if (AltCRC != null) jObj.Add("AltCRC", AltCRC.ToHexString());
            if (AltSHA1 != null) jObj.Add("AltSHA1", AltSHA1.ToHexString());
            if (AltMD5 != null) jObj.Add("AltMD5", AltMD5.ToHexString());
            if (!string.IsNullOrEmpty(Merge)) jObj.Add("Merge", Merge);
            if (!string.IsNullOrEmpty(Status)) jObj.Add("Status", Status);
            if (ZipFileIndex >= 0) jObj.Add("ZipFileIndex", ZipFileIndex);
            if (ZipFileHeaderPosition != null) jObj.Add("ZipFileHeaderPosition", (long)ZipFileHeaderPosition);
            if (CHDVersion != null) jObj.Add("CHDVersion", (uint)CHDVersion);

            jObj.Add("FileStatus", _fileStatus.ToString());


            if (DBTypeGet.isCompressedDir(FileType)) jObj.Add("ZipStatus", ZipStatus.ToString());


            if (Tree != null) jObj.Add("Tree", Tree.WriteJson());
            if (Game != null) jObj.Add("Game", Game.WriteJson());


            // RvDir


            if (countDirDats > 0)
            {
                JArray jArr = new JArray();
                for (int i = 0; i < countDirDats; i++)
                {
                    jArr.Add(_dirDats[i].WriteJson());
                }
                jObj.Add("Dats", jArr);
            }

            if (countChild > 0)
            {
                JArray jArr = new JArray();
                for (int i = 0; i < countChild; i++)
                {
                    jArr.Add(_children[i].WriteJson());
                }
                jObj.Add("Children", jArr);
            }

            return jObj;
        }
        */

        public void Write(BinaryWriter bw)
        {
            int countDirDats = _dirDats?.Count ?? 0;
            int countChild = _children?.Count ?? 0;

            FileFlags fFlags = 0;
            if (Tree != null) fFlags |= FileFlags.HasTree;
            if (Game != null) fFlags |= FileFlags.HasGame;
            if (Dat != null) fFlags |= FileFlags.HasDat;
            if (countDirDats > 0) fFlags |= FileFlags.HasDirDat;
            if (countChild > 0) fFlags |= FileFlags.HasChildren;

            if (Size != null) fFlags |= FileFlags.Size;
            if (CRC != null) fFlags |= FileFlags.CRC;
            if (SHA1 != null) fFlags |= FileFlags.SHA1;
            if (MD5 != null) fFlags |= FileFlags.MD5;

            if (HeaderFileType != HeaderFileType.Nothing) fFlags |= FileFlags.HeaderFileType;
            if (AltSize != null) fFlags |= FileFlags.AltSize;
            if (AltCRC != null) fFlags |= FileFlags.AltCRC;
            if (AltSHA1 != null) fFlags |= FileFlags.AltSHA1;
            if (AltMD5 != null) fFlags |= FileFlags.AltMD5;

            if (!string.IsNullOrEmpty(Merge)) fFlags |= FileFlags.Merge;
            if (!string.IsNullOrEmpty(Status)) fFlags |= FileFlags.Status;
            if (ZipFileIndex >= 0) fFlags |= FileFlags.ZipFileIndex;
            if (ZipFileHeaderPosition != null) fFlags |= FileFlags.ZipFileHeader;
            if (CHDVersion != null) fFlags |= FileFlags.CHDVersion;

#if dt
            // FileModTimeStamp  always
            if (DatModTimeStamp != null) fFlags |= FileFlags.DatModTimeStamp;
            if (FileCreatedTimeStamp != null) fFlags |= FileFlags.FileCreatedTimeStamp;
            if (DatCreatedTimeStamp != null) fFlags |= FileFlags.DatCreatedTimeStamp;
            if (FileLastAccessTimeStamp != null) fFlags |= FileFlags.FileLastAccessTimeStamp;
            if (DatLastAccessTimeStamp != null) fFlags |= FileFlags.DatLastAccessTimeStamp;
#endif

            bw.Write((uint)fFlags);


            /************* RvFile ************/

            bw.Write(DB.Fn(Name));
            bw.Write(DB.Fn(FileName));

            bw.Write(FileModTimeStamp);
#if dt
            if (DatModTimeStamp != null) bw.Write((ulong)DatModTimeStamp);
            if (FileCreatedTimeStamp != null) bw.Write((ulong)FileCreatedTimeStamp);
            if (DatCreatedTimeStamp != null) bw.Write((ulong)DatCreatedTimeStamp);
            if (FileLastAccessTimeStamp != null) bw.Write((ulong)FileLastAccessTimeStamp);
            if (DatLastAccessTimeStamp != null) bw.Write((ulong)DatLastAccessTimeStamp);
#endif

            if (Dat != null) bw.Write(Dat.DatIndex);
            bw.Write((byte)_datStatus);
            bw.Write((byte)_gotStatus);

            if (DBTypeGet.isCompressedDir(FileType)) bw.Write((byte)ZipStatus);

            /************* RvDir ************/

            Tree?.Write(bw);
            Game?.Write(bw);

            if (countDirDats > 0)
            {
                bw.Write(countDirDats);
                for (int i = 0; i < countDirDats; i++)
                {
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

            if (HeaderFileType != HeaderFileType.Nothing) bw.Write((byte)HeaderFileType);
            if (AltSize != null) bw.Write((ulong)AltSize);
            if (AltCRC != null) bw.WriteByteArray(AltCRC);
            if (AltSHA1 != null) bw.WriteByteArray(AltSHA1);
            if (AltMD5 != null) bw.WriteByteArray(AltMD5);
            if (!string.IsNullOrEmpty(Merge)) bw.Write(Merge);
            if (!string.IsNullOrEmpty(Status)) bw.Write(Status);
            if (ZipFileIndex >= 0) bw.Write(ZipFileIndex);
            if (ZipFileHeaderPosition != null) bw.Write((long)ZipFileHeaderPosition);
            if (CHDVersion != null) bw.Write((uint)CHDVersion);

            bw.Write((uint)_fileStatus);
        }

        public void Read(BinaryReader br, List<RvDat> parentDirDats)
        {
            FileFlags fFlags = (FileFlags)br.ReadUInt32();

            /************* RvFile ************/

            Name = br.ReadString();
            FileName = br.ReadString();
            FileModTimeStamp = br.ReadInt64();
            if (FileModTimeStamp != 0 && FileModTimeStamp < RVIO.FileParamConvert.FileTimeOffset)
                FileModTimeStamp += RVIO.FileParamConvert.FileTimeOffset;
#if dt
            DatModTimeStamp = (fFlags & FileFlags.DatModTimeStamp) > 0 ? br.ReadInt64() : (long?)null;
            FileCreatedTimeStamp = (fFlags & FileFlags.FileCreatedTimeStamp) > 0 ? br.ReadInt64() : (long?)null;
            DatCreatedTimeStamp = (fFlags & FileFlags.DatCreatedTimeStamp) > 0 ? br.ReadInt64() : (long?)null;
            FileLastAccessTimeStamp = (fFlags & FileFlags.FileLastAccessTimeStamp) > 0 ? br.ReadInt64() : (long?)null;
            DatLastAccessTimeStamp = (fFlags & FileFlags.DatLastAccessTimeStamp) > 0 ? br.ReadInt64() : (long?)null;
#endif

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
                    Dat = parentDirDats[index];
                }
            }


            _datStatus = (DatStatus)br.ReadByte();
            _gotStatus = (GotStatus)br.ReadByte();
            RepStatusReset();

            if (DBTypeGet.isCompressedDir(FileType)) ZipStatus = (ZipStatus)br.ReadByte();

            /************* RvDir ************/

            Tree = null;
            if ((fFlags & FileFlags.HasTree) > 0)
            {
                Tree = new RvTreeRow();
                Tree.Read(br);
            }

            Game = null;
            if ((fFlags & FileFlags.HasGame) > 0)
            {
                Game = new RvGame();
                Game.Read(br);
            }

            int count = (fFlags & FileFlags.HasDirDat) > 0 ? br.ReadInt32() : 0;
            _dirDats?.Clear();
            int progress = -1;
            for (int i = 0; i < count; i++)
            {
                RvDat dat = new RvDat { DatIndex = i };
                dat.Read(br);
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
            for (int i = 0; i < count; i++)
            {
                RvFile tChild = new RvFile((FileType)br.ReadByte()) { Parent = this };

                tChild.Read(br, parentDirDats);
                _children?.Add(tChild);
            }

            /************* RvFile ************/

            Size = (fFlags & FileFlags.Size) > 0 ? (ulong?)br.ReadUInt64() : null;
            CRC = (fFlags & FileFlags.CRC) > 0 ? br.ReadByteArray() : null;
            SHA1 = (fFlags & FileFlags.SHA1) > 0 ? br.ReadByteArray() : null;
            MD5 = (fFlags & FileFlags.MD5) > 0 ? br.ReadByteArray() : null;

            HeaderFileType = (fFlags & FileFlags.HeaderFileType) > 0 ? (HeaderFileType)br.ReadByte() : HeaderFileType.Nothing;
            AltSize = (fFlags & FileFlags.AltSize) > 0 ? (ulong?)br.ReadUInt64() : null;
            AltCRC = (fFlags & FileFlags.AltCRC) > 0 ? br.ReadByteArray() : null;
            AltSHA1 = (fFlags & FileFlags.AltSHA1) > 0 ? br.ReadByteArray() : null;
            AltMD5 = (fFlags & FileFlags.AltMD5) > 0 ? br.ReadByteArray() : null;
            Merge = (fFlags & FileFlags.Merge) > 0 ? br.ReadString() : null;
            Status = (fFlags & FileFlags.Status) > 0 ? br.ReadString() : null;
            ZipFileIndex = (fFlags & FileFlags.ZipFileIndex) > 0 ? br.ReadInt32() : -1;
            ZipFileHeaderPosition = (fFlags & FileFlags.ZipFileHeader) > 0 ? (ulong?)br.ReadUInt64() : null;
            CHDVersion = (fFlags & FileFlags.CHDVersion) > 0 ? (uint?)br.ReadInt32() : null;

            _fileStatus = (FileStatus)br.ReadUInt32();
        }


        public EFile DatRemove()
        {
            /************* RvDir ************/

            Tree = null;
            Game = null;
            _dirDats?.Clear();

            /************* RvFile ************/

            if (!FileStatusIs(FileStatus.SizeFromHeader) && !FileStatusIs(FileStatus.SizeVerified)) Size = null;
            if (!FileStatusIs(FileStatus.CRCFromHeader) && !FileStatusIs(FileStatus.CRCVerified)) CRC = null;
            if (!FileStatusIs(FileStatus.SHA1FromHeader) && !FileStatusIs(FileStatus.SHA1Verified)) SHA1 = null;
            if (!FileStatusIs(FileStatus.MD5FromHeader) && !FileStatusIs(FileStatus.MD5Verified)) MD5 = null;
            if (!FileStatusIs(FileStatus.AltSHA1FromHeader) && !FileStatusIs(FileStatus.AltSHA1Verified)) AltSHA1 = null;
            if (!FileStatusIs(FileStatus.AltMD5FromHeader) && !FileStatusIs(FileStatus.AltMD5Verified)) AltMD5 = null;

            FileStatusClear(FileStatus.SizeFromDAT | FileStatus.CRCFromDAT | FileStatus.SHA1FromDAT | FileStatus.MD5FromDAT | FileStatus.AltSHA1FromDAT | FileStatus.AltMD5FromDAT);

            Merge = "";
            Status = "";

            /************* RvFile ************/

            Dat = null;
            if (GotStatus == GotStatus.NotGot)
            {
                return EFile.Delete;
            }

            if (!string.IsNullOrEmpty(FileName))
            {
                Name = FileName;
                FileName = null;
            }
            DatStatus = DatStatus.NotInDat;
#if dt
            DatModTimeStamp = null;
#endif
            return EFile.Keep;
        }

        public void DatAdd(RvFile b, bool altFile)
        {
            if (b.IsFile)
            {
                if (altFile)
                {
                    b.SetAsAltFile();
                }

                if (Size == null && b.Size != null) Size = b.Size;
                if (CRC == null && b.CRC != null) CRC = b.CRC;
                if (SHA1 == null && b.SHA1 != null) SHA1 = b.SHA1;
                if (MD5 == null && b.MD5 != null) MD5 = b.MD5;
                if (AltSize == null && b.AltSize != null) AltSize = b.AltSize;
                if (AltCRC == null && b.AltCRC != null) AltCRC = b.AltCRC;
                if (AltSHA1 == null && b.AltSHA1 != null) AltSHA1 = b.AltSHA1;
                if (AltMD5 == null && b.AltMD5 != null) AltMD5 = b.AltMD5;

                FileStatusSet(
                    FileStatus.SizeFromDAT | FileStatus.CRCFromDAT | FileStatus.SHA1FromDAT | FileStatus.MD5FromDAT |
                        FileStatus.AltSizeFromDAT | FileStatus.AltCRCFromDAT | FileStatus.AltSHA1FromDAT | FileStatus.AltMD5FromDAT,
                    b);

                Merge = b.Merge;
                Status = b.Status;
            }

            if (b.IsDir)
            {
                Tree = b.Tree;
                Game = b.Game;
                if (_dirDats.Count > 0)
                {
                    ReportError.SendAndShow("Setting Dir with a dat list");
                }
            }


            // Parent , TimeStamp Should already be correct.

            if (GotStatus == GotStatus.NotGot)
            {
                ReportError.SendAndShow("Error Adding DAT to NotGot File " + b.GotStatus);
            }

            SetStatus(b.DatStatus, GotStatus.Got);

#if dt
            DatModTimeStamp = b.DatModTimeStamp;
#endif

            if (Name == b.Name) // case match so all is good
            {
                FileName = null;
            }
            else
            {
                FileName = Name;
                Name = b.Name;
            }

            Dat = b.Dat;
        }



        private EFile TestRemove()
        {
            FileModTimeStamp = 0;
            FileName = null;

            GotStatus = Parent.GotStatus == GotStatus.FileLocked ? GotStatus.FileLocked : GotStatus.NotGot;
            switch (DatStatus)
            {
                case DatStatus.InDatCollect:
                case DatStatus.InDatMerged:
                case DatStatus.InDatBad:
                    return EFile.Keep;

                case DatStatus.NotInDat:
                case DatStatus.InToSort:
                    return EFile.Delete; // this item should be removed from the db.
                default:
                    ReportError.SendAndShow("Unknown Set Got Status " + DatStatus);
                    return EFile.Keep;
            }
        }

        /// <summary>
        /// FileRemove
        /// If a file is deleted this will remove all the data about the file from this rvFile.
        /// If the file is also added from a dat then the rvFile should remain with just the original data from the DAT,
        /// all other non-DAT meta data will be removed, if this rvFile had AltData from a DAT this will be moved back to be
        /// the mail hash data from this rvFile.
        /// </summary>
        /// <returns>
        /// EFile.Delete:  this file should be deleted from the DB
        /// Efile.Keep:    this file should be kept in the DB as it is from a dat.
        /// </returns>
        public EFile FileRemove()
        {
            if (IsFile) // File,ZippedFile or 7zippedFile
            {
                ZipFileIndex = -1;
                ZipFileHeaderPosition = null;

                // TestRemove will also set GotStatus to NotGot. (unless the file is locked.)
                if (TestRemove() == EFile.Delete)
                    return EFile.Delete;

                // if none of the primary meta data is from the DAT delete it.
                if (!FileStatusIs(FileStatus.HeaderFileTypeFromDAT)) HeaderFileType = HeaderFileType.Nothing;
                if (!FileStatusIs(FileStatus.SizeFromDAT)) Size = null;
                if (!FileStatusIs(FileStatus.CRCFromDAT)) CRC = null;
                if (!FileStatusIs(FileStatus.SHA1FromDAT)) SHA1 = null;
                if (!FileStatusIs(FileStatus.MD5FromDAT)) MD5 = null;

                // if the Alt meta data is from the dat move it up to be the primary data.
                if (FileStatusIs(FileStatus.AltSizeFromDAT))
                {
                    Size = AltSize;
                    FileStatusSet(FileStatus.SizeFromDAT);
                }
                if (FileStatusIs(FileStatus.AltCRCFromDAT))
                {
                    CRC = AltCRC;
                    FileStatusSet(FileStatus.CRCFromDAT);
                }
                if (FileStatusIs(FileStatus.AltSHA1FromDAT))
                {
                    SHA1 = AltSHA1;
                    FileStatusSet(FileStatus.SHA1FromDAT);
                }
                if (FileStatusIs(FileStatus.AltMD5FromDAT))
                {
                    MD5 = AltMD5;
                    FileStatusSet(FileStatus.MD5FromDAT);
                }

                // remove all Alt Data.
                AltSize = null;
                AltCRC = null;
                AltSHA1 = null;
                AltMD5 = null;

                CHDVersion = null;

                FileStatusClear(
                    FileStatus.HeaderFileTypeFromHeader |
                    FileStatus.SizeFromHeader | FileStatus.CRCFromHeader | FileStatus.SHA1FromHeader | FileStatus.MD5FromHeader |
                    FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified |
                    FileStatus.AltSizeFromHeader | FileStatus.AltCRCFromHeader | FileStatus.AltSHA1FromHeader | FileStatus.AltMD5FromHeader |
                    FileStatus.AltSizeVerified | FileStatus.AltCRCVerified | FileStatus.AltSHA1Verified | FileStatus.AltMD5Verified |
                    FileStatus.AltSizeFromDAT | FileStatus.AltCRCFromDAT | FileStatus.AltSHA1FromDAT | FileStatus.AltMD5FromDAT   // AltDat data has been moved up to primary data so also remove these flags.
                    );

                return EFile.Keep;
            }

            if (IsDir)
            {
                ZipStatus = ZipStatus.None;
                return TestRemove();
            }

            FileModTimeStamp = 0;
            // This should never happen, as either IsFile or IsDir should be set.
            GotStatus = GotStatus.NotGot;
            ReportError.SendAndShow("Unknown File Remove Type");
            return EFile.Keep;
        }


        private bool TestMatch(RvFile file)
        {
            bool foundATest = false;
            if (Size != null && file.Size != null)
            {
                foundATest = true;
                if (Size != file.Size)
                    return false;
            }

            if (CRC != null && file.CRC != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(CRC, file.CRC))
                    return false;
            }

            if (SHA1 != null && file.SHA1 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(SHA1, file.SHA1))
                    return false;
            }

            if (MD5 != null && file.MD5 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(MD5, file.MD5))
                    return false;
            }

            return foundATest;
        }
        private bool TestMatchAlt(RvFile file)
        {
            bool foundATest = false;
            if (Size != null && file.AltSize != null)
            {
                foundATest = true;
                if (Size != file.AltSize)
                    return false;
            }

            if (CRC != null && file.AltCRC != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(CRC, file.AltCRC))
                    return false;
            }

            if (SHA1 != null && file.AltSHA1 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(SHA1, file.AltSHA1))
                    return false;

            }
            if (MD5 != null && file.AltMD5 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(MD5, file.AltMD5))
                    return false;
            }

            return foundATest;
        }

        public void FileTestFix(RvFile file)
        {
            if (TestMatch(file))
                return;
            if (!TestMatchAlt(file))
                return;

            if (AltSize != null || AltCRC != null || AltSHA1 != null || AltMD5 != null)
            {
                //error
            }
            SetAsAltFile();

        }

        private void SetAsAltFile()
        {
            if (FileStatusIs(FileStatus.SizeFromDAT))
            {
                AltSize = Size;
                FileStatusSet(FileStatus.AltSizeFromDAT);
            }
            if (FileStatusIs(FileStatus.CRCFromDAT))
            {
                AltCRC = CRC;
                FileStatusSet(FileStatus.AltCRCFromDAT);
            }
            if (FileStatusIs(FileStatus.SHA1FromDAT))
            {
                AltSHA1 = SHA1;
                FileStatusSet(FileStatus.AltSHA1FromDAT);
            }
            if (FileStatusIs(FileStatus.MD5FromDAT))
            {
                AltMD5 = MD5;
                FileStatusSet(FileStatus.AltMD5FromDAT);
            }

            Size = null;
            CRC = null;
            SHA1 = null;
            MD5 = null;
            FileStatusClear(FileStatus.SizeFromDAT | FileStatus.CRCFromDAT | FileStatus.SHA1FromDAT | FileStatus.MD5FromDAT);

        }

        public void FileAdd(RvFile file, bool altFile)
        {
            if (file.IsFile)
            {
                if (altFile)
                {
                    SetAsAltFile();
                }

                if (Size == null && file.Size != null) Size = file.Size;
                if (CRC == null && file.CRC != null) CRC = file.CRC;
                if (SHA1 == null && file.SHA1 != null) SHA1 = file.SHA1;
                if (MD5 == null && file.MD5 != null) MD5 = file.MD5;
                if (AltSize == null && file.AltSize != null) AltSize = file.AltSize;
                if (AltCRC == null && file.AltCRC != null) AltCRC = file.AltCRC;
                if (AltSHA1 == null && file.AltSHA1 != null) AltSHA1 = file.AltSHA1;
                if (AltMD5 == null && file.AltMD5 != null) AltMD5 = file.AltMD5;
                if (HeaderFileType == HeaderFileType.Nothing && file.HeaderFileType != HeaderFileType.Nothing) HeaderFileType = file.HeaderFileType;

                CHDVersion = file.CHDVersion;


                FileStatusSet(
                    FileStatus.HeaderFileTypeFromHeader |
                    FileStatus.SizeFromHeader | FileStatus.CRCFromHeader | FileStatus.SHA1FromHeader | FileStatus.MD5FromHeader | FileStatus.AltSizeFromHeader | FileStatus.AltCRCFromHeader | FileStatus.AltSHA1FromHeader | FileStatus.AltMD5FromHeader |
                    FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified | FileStatus.AltSizeVerified | FileStatus.AltCRCVerified | FileStatus.AltSHA1Verified | FileStatus.AltMD5Verified,
                    file);

                ZipFileIndex = file.ZipFileIndex;
                ZipFileHeaderPosition = file.ZipFileHeaderPosition;
            }


            FileModTimeStamp = file.FileModTimeStamp;
            FileCheckName(file);
            if (file.GotStatus == GotStatus.NotGot)
            {
                ReportError.SendAndShow("Error setting got to a NotGot File");
            }
            GotStatus = file.GotStatus;
        }

        public void FileCheckName(RvFile file)
        {
            // Don't care about bad case if the file is not in a dat.
            if (DatStatus == DatStatus.NotInDat || DatStatus == DatStatus.InToSort)
            {
                Name = file.Name;
            }

            FileName = Name == file.Name ? null : file.Name;
        }

        // this is only used to copy a file.
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


            c.Name = Name;
            c.FileName = FileName;
            //c.Parent = Parent;
            c.Dat = Dat;
            c.FileModTimeStamp = FileModTimeStamp;
#if dt
            c.DatModTimeStamp = DatModTimeStamp;
#endif
            c._datStatus = _datStatus;
            c._gotStatus = _gotStatus;
            c.RepStatus = RepStatus;
            c.FileGroup = FileGroup;
        }

        public string SuperDatFileName()
        {
            return SuperDatFileName(Dat);
        }

        private string SuperDatFileName(RvDat dat)
        {
            if (dat.AutoAddedDirectory)
            {
                if (Parent?.Parent == null || Parent.Parent.Dat != dat)
                {
                    return Name;
                }
            }
            else
            {
                if (Parent == null || Parent.Dat != dat)
                {
                    return Name;
                }
            }
            return Path.Combine(Parent.SuperDatFileName(dat), Name);
        }

        public string FileNameInsideGame()
        {
            if (Game != null || Dat != null)
            {
                return Name;
            }

            return Path.Combine(Parent.FileNameInsideGame(), Name);
        }

        public void SetStatus(DatStatus dt, GotStatus flag)
        {
            _datStatus = dt;
            _gotStatus = flag;
            RepStatusReset();
        }

        public void RepStatusReset()
        {
            SearchFound = false;
            if ((RepStatus == RepStatus.UnSet || RepStatus == RepStatus.Unknown || RepStatus == RepStatus.Ignore) &&
                FileType == FileType.File && GotStatus == GotStatus.Got && DatStatus == DatStatus.NotInDat)
            {
                foreach (string file in Settings.rvSettings.IgnoreFiles)
                {
                    if (Name == file)
                    {
                        RepStatus = RepStatus.Ignore;
                        return;
                    }
                }
            }

            List<RepStatus> rs = RepairStatus.StatusCheck[(int)FileType, (int)_datStatus, (int)_gotStatus];
            RepStatus = rs?[0] ?? RepStatus.Error;
        }


        /****************** RvDir ***********************/
        public bool IsDir => FileType == FileType.Dir || FileType == FileType.Zip || FileType == FileType.SevenZip;

        public int DirDatCount => _dirDats.Count;
        public int ChildCount => _children?.Count ?? 0;


        public RvDat DirDat(int index)
        {
            return _dirDats[index];
        }

        public void DirDatAdd(RvDat dat)
        {
            DirDatSearch(dat, out int index);
            _dirDats.Insert(index, dat);
            for (int i = 0; i < _dirDats.Count; i++)
            {
                _dirDats[i].DatIndex = i;
            }
        }

        public void DirDatRemove(int index)
        {
            _dirDats.RemoveAt(index);
            for (int i = 0; i < _dirDats.Count; i++)
            {
                _dirDats[i].DatIndex = i;
            }
        }

        private void DirDatSearch(RvDat dat, out int index)
        {
            int intBottom = 0;
            int intTop = _dirDats.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while (intBottom < intTop && intRes != 0)
            {
                intMid = (intBottom + intTop) / 2;

                intRes = DBHelper.DatCompare(dat, _dirDats[intMid]);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while (index > 0 && intRes1 == 0)
                {
                    intRes1 = DBHelper.DatCompare(dat, _dirDats[index - 1]);
                    if (intRes1 == 0)
                    {
                        index--;
                    }
                }
            }
            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
            {
                index++;
            }
        }

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

        public void ChildAdd(RvFile child, int index)
        {
            if (
                FileType == FileType.Dir && child.FileType == FileType.ZipFile ||
                FileType == FileType.Zip && child.FileType != FileType.ZipFile
            )
            {
                ReportError.SendAndShow("Typing to add a " + child.FileType + " to a " + FileType);
            }

            _children.Insert(index, child);
            child.Parent = this;
            UpdateRepStatusArrUpTree(child, 1);
        }

        public void ChildRemove(int index)
        {
            UpdateRepStatusArrUpTree(_children[index], -1);
            if (_children[index].Parent == this)
            {
                _children[index].Parent = null;
            }
            _children.RemoveAt(index);
        }

        public int ChildNameSearch(RvFile lName, out int index)
        {
            int intBottom = 0;
            int intTop = _children.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while (intBottom < intTop && intRes != 0)
            {
                intMid = (intBottom + intTop) / 2;

                intRes = DBHelper.CompareName(lName, _children[intMid]);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while (index > 0 && intRes1 == 0)
                {
                    intRes1 = DBHelper.CompareName(lName, _children[index - 1]);
                    if (intRes1 == 0)
                    {
                        index--;
                    }
                }
            }
            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
            {
                index++;
            }

            return intRes;
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
            } while (index < _children.Count && DBHelper.CompareName(lName, _children[index]) == 0);

            return false;
        }


        private void UpdateRepStatusUpTree(RepStatus rStat, int dir)
        {
            DirStatus.UpdateRepStatus(rStat, dir);
            Parent?.UpdateRepStatusUpTree(rStat, dir);
        }

        private void UpdateRepStatusArrUpTree(RvFile child, int dir)
        {
            DirStatus.UpdateRepStatus(child.RepStatus, dir);
            if (child.IsDir)
            {
                DirStatus.UpdateRepStatus(child.DirStatus, dir);
            }

            Parent?.UpdateRepStatusArrUpTree(child, dir);
        }


        /****************** RvFile ********************/
        public bool IsFile => FileType == FileType.File || FileType == FileType.ZipFile || FileType == FileType.SevenZipFile;

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


    }
}