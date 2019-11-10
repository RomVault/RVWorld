/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System.Collections.Generic;
using System.IO;

namespace RVCore.RvDB
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
            Header = 16
        }

        private readonly List<DatMetaData> _gameMetaData = new List<DatMetaData>();
        public int DatIndex = -1;
        public DatUpdateStatus Status;

        public long TimeStamp;
        public bool AutoAddDirectory;


        /*
        public JObject WriteJson()
        {
            JObject jObj = new JObject();
            jObj.Add("TimeStamp",TimeStamp);
            jObj.Add("AutoAddDirectory",AutoAddDirectory);

            foreach (DatMetaData gameMD in _gameMetaData)
            {
                jObj.Add(gameMD.Id.ToString(), gameMD.Value);
            }

            return jObj;
        }
        */

        public void Write(BinaryWriter bw)
        {
            bw.Write(TimeStamp);
            bw.Write(AutoAddDirectory);

            bw.Write((byte)_gameMetaData.Count);
            foreach (DatMetaData gameMD in _gameMetaData)
            {
                gameMD.Write(bw);
            }
        }

        public void Read(BinaryReader br)
        {
            TimeStamp = br.ReadInt64();
            AutoAddDirectory = br.ReadBoolean();

            byte c = br.ReadByte();
            _gameMetaData.Clear();
            _gameMetaData.Capacity = c;
            for (byte i = 0; i < c; i++)
            {
                _gameMetaData.Add(new DatMetaData(br));
            }
        }

        public void AddData(DatData id, string val)
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

            _gameMetaData.Insert(pos, new DatMetaData(id, val));
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