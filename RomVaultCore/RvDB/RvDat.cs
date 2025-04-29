/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using DATReader.DatClean;
using DATReader.DatStore;
using RomVaultCore.Storage.Dat;

namespace RomVaultCore.RvDB
{
    public enum DatUpdateStatus
    {
        Delete,
        Correct
    }
    [Flags]
    public enum DatFlags
    {
        AutoAddedDirectory = 1,
        MultiDatOverride = 2,
        MultiDatsInDirectory = 4,
        UseDescriptionAsDirName = 8,
        SingleArchive = 16,
        UseIdForName = 32
    }

    public class RvDat
    {

        public enum DatData
        {
            Id = 0,
            DatName = 1,
            DatRootFullName = 2,

            RootDir = 3,
            Description = 4,
            Category = 5,
            Version = 6,
            Date = 7,
            Author = 8,
            Email = 9,
            HomePage = 10,
            URL = 11,
            FileType = 12,
            MergeType = 13,
            SuperDat = 14,
            DirSetup = 15,
            Header = 16,
            SubDirType = 17,
            Compression = 18
        }

        private readonly List<DatMetaData> _gameMetaData = new List<DatMetaData>();
        public int DatIndex = -1;
        public DatUpdateStatus Status;
        public long TimeStamp { get; private set; }
        private DatFlags datFlags;


        public RvDat(BinaryReader br)
        {
            TimeStamp = br.ReadInt64();
            datFlags = (DatFlags)br.ReadByte();

            byte c = br.ReadByte();
            _gameMetaData.Clear();
            _gameMetaData.Capacity = c;
            for (byte i = 0; i < c; i++)
            {
                _gameMetaData.Add(new DatMetaData(br));
            }
        }


        public RvDat(DatImportDat fileDat)
        {
            DatHeader datHeaderExternal = fileDat.datHeader;

            Status = DatUpdateStatus.Correct;
            TimeStamp = fileDat.TimeStamp;

            SetData(DatData.Id, datHeaderExternal.Id);
            SetData(DatData.DatName, datHeaderExternal.Name);
            SetData(DatData.DatRootFullName, fileDat.DatFullName);

            SetData(DatData.RootDir, datHeaderExternal.RootDir);
            SetData(DatData.Description, datHeaderExternal.Description);
            SetData(DatData.Category, datHeaderExternal.Category);
            SetData(DatData.Version, datHeaderExternal.Version);
            SetData(DatData.Date, datHeaderExternal.Date);
            SetData(DatData.Author, datHeaderExternal.Author);
            SetData(DatData.Email, datHeaderExternal.Email);
            SetData(DatData.HomePage, datHeaderExternal.Homepage);
            SetData(DatData.URL, datHeaderExternal.URL);
            SetData(DatData.DirSetup, datHeaderExternal.Dir);
            SetData(DatData.Header, datHeaderExternal.Header);
            SetData(DatData.Compression, datHeaderExternal.Compression);
            SetFlags(fileDat.Flags);
            SubDirType = fileDat.SubDirType; // stored in DatData.SubDirType
        }

        public void InvalidateDatTimeStamp()
        {
            TimeStamp = long.MaxValue;
        }


        public RemoveSubType SubDirType
        {
            get
            {
                string dirType = GetData(DatData.SubDirType);
                if (string.IsNullOrWhiteSpace(dirType))
                    return RemoveSubType.KeepAllSubDirs;
                return (RemoveSubType)Convert.ToInt32(dirType);
            }
            set => SetData(DatData.SubDirType, ((int)value).ToString());
        }


        public bool Flag(DatFlags datFlag)
        {
            return (datFlags & datFlag) != 0;
        }
        public void SetFlag(DatFlags datFlag, bool value)
        {
            datFlags = datFlags & ~datFlag;
            if (value) datFlags |= datFlag;
        }
        public void SetFlags(DatFlags datFlags)
        {
            this.datFlags = datFlags;
        }
        public int FlagsCompareTo(RvDat d1)
        {
            return Math.Sign(datFlags.CompareTo(d1.datFlags));
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(TimeStamp);
            bw.Write((byte)datFlags);

            bw.Write((byte)_gameMetaData.Count);
            foreach (DatMetaData gameMD in _gameMetaData)
            {
                gameMD.Write(bw);
            }
        }

        private void SetData(DatData id, string val)
        {
            if (string.IsNullOrWhiteSpace(val))
            {
                return;
            }

            int pos = 0;
            while (pos < _gameMetaData.Count && _gameMetaData[pos].Id < id)
            {
                pos++;
            }

            if (pos >= _gameMetaData.Count || _gameMetaData[pos].Id != id)
            {
                _gameMetaData.Insert(pos, new DatMetaData(id, val));
                return;
            }

            _gameMetaData[pos] = new DatMetaData(id, val);
        }

        public string GetData(DatData id)
        {
            foreach (DatMetaData gameMD in _gameMetaData)
            {
                if (id == gameMD.Id)
                {
                    return gameMD.Value;
                }
                if (id < gameMD.Id)
                {
                    return "";
                }
            }
            return "";
        }

        private class DatMetaData
        {
            public DatMetaData(DatData id, string value)
            {
                Id = id;
                Value = value;
            }

            public DatMetaData(BinaryReader br)
            {
                Id = (DatData)br.ReadByte();
                Value = br.ReadString();
            }

            public DatData Id { get; }
            public string Value { get; }

            public void Write(BinaryWriter bw)
            {
                bw.Write((byte)Id);
                bw.Write(DB.FixNull(Value));
            }
        }
    }
}