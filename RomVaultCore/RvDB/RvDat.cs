/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2022                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using DATReader.DatClean;

namespace RomVaultCore.RvDB
{
    public enum DatUpdateStatus
    {
        Delete,
        Correct
    }

    public class RvDat
    {
        public enum DatData
        {
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
            SubDirType = 17
        }

        private readonly List<DatMetaData> _gameMetaData = new List<DatMetaData>();
        public int DatIndex = -1;
        public DatUpdateStatus Status;

        public RemoveSubType SubDirType
        {
            get
            {
                string dirType = GetData(DatData.SubDirType);
                if (string.IsNullOrWhiteSpace(dirType))
                    return RemoveSubType.KeepAllSubDirs;
                return (RemoveSubType)Convert.ToInt32(dirType);
            }
            set => SetData(DatData.SubDirType,((int)value).ToString());
        }

        public long TimeStamp;
        public bool MultiDatOverride;
        public bool MultiDatsInDirectory;
        public bool AutoAddedDirectory;
        public bool UseDescriptionAsDirName;
        public bool SingleArchive;

        public void Write(BinaryWriter bw)
        {
            bw.Write(TimeStamp);
            byte bools = (byte)
                (
                    (AutoAddedDirectory ? 1 : 0) |
                    (MultiDatOverride ? 2 : 0) |
                    (MultiDatsInDirectory ? 4 : 0) |
                    (UseDescriptionAsDirName ? 8 : 0) |
                    (SingleArchive ? 16 : 0)
                 );
            bw.Write(bools);

            bw.Write((byte)_gameMetaData.Count);
            foreach (DatMetaData gameMD in _gameMetaData)
            {
                gameMD.Write(bw);
            }
        }

        public void Read(BinaryReader br)
        {
            TimeStamp = br.ReadInt64();
            if (TimeStamp < RVIO.FileParamConvert.FileTimeOffset)
                TimeStamp += RVIO.FileParamConvert.FileTimeOffset;

            byte bools = br.ReadByte();
            AutoAddedDirectory = (bools & 1) == 1;
            MultiDatOverride = (bools & 2) == 2;
            MultiDatsInDirectory = (bools & 4) == 4;
            UseDescriptionAsDirName = (bools & 8) == 8;
            SingleArchive = (bools & 16) == 16;

            byte c = br.ReadByte();
            _gameMetaData.Clear();
            _gameMetaData.Capacity = c;
            for (byte i = 0; i < c; i++)
            {
                _gameMetaData.Add(new DatMetaData(br));
            }
        }

        public void SetData(DatData id, string val)
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
                bw.Write(DB.Fn(Value));
            }
        }
    }
}