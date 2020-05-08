/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System.Collections.Generic;
using System.IO;

namespace RVCore.RvDB
{
    public class RvGame
    {
        public enum GameData
        {
            Description = 1,
            RomOf = 2,
            IsBios = 3,
            Sourcefile = 4,
            CloneOf = 5,
            SampleOf = 6,
            Board = 7,
            Year = 8,
            Manufacturer = 9,

            EmuArc = 10,
            TitleId = 11,
            Publisher = 12,
            Developer = 13,
            Genre = 14,
            SubGenre = 15,
            Ratings = 16,
            Score = 17,
            Players = 18,
            Enabled = 19,
            CRC = 20,
            RelatedTo = 21,
            Source = 22

        }

        private readonly List<GameMetaData> _gameMetaData = new List<GameMetaData>();


        public bool Equals(RvGame other)
        {
            int c = _gameMetaData.Count;
            if (c != other._gameMetaData.Count)
                return false;

            for (int i = 0; i < c; i++)
            {
                if (!_gameMetaData[i].Equals(other._gameMetaData[i]))
                    return false;
            }

            return true;
        }

        /*
        public JObject WriteJson()
        {
            JObject jObj=new JObject();
            foreach (GameMetaData gameMD in _gameMetaData)
            {
             jObj.Add(gameMD.Id.ToString(),gameMD.Value);   
            }

            return jObj;
        }
        */

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)_gameMetaData.Count);
            foreach (GameMetaData gameMD in _gameMetaData)
            {
                gameMD.Write(bw);
            }
        }

        public void Read(BinaryReader br)
        {
            byte c = br.ReadByte();
            _gameMetaData.Clear();
            _gameMetaData.Capacity = c;
            for (byte i = 0; i < c; i++)
            {
                _gameMetaData.Add(new GameMetaData(br));
            }
        }

        public void AddData(GameData id, string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                return;
            }

            int pos = 0;
            while (pos < _gameMetaData.Count && _gameMetaData[pos].Id < id)
            {
                pos++;
            }

            _gameMetaData.Insert(pos, new GameMetaData(id, val));
        }

        public string GetData(GameData id)
        {
            foreach (GameMetaData gameMD in _gameMetaData)
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

        public void DeleteData(GameData id)
        {
            for (int i = 0; i < _gameMetaData.Count; i++)
            {
                if (id == _gameMetaData[i].Id)
                {
                    _gameMetaData.RemoveAt(i);
                    return;
                }
                if (id < _gameMetaData[i].Id)
                {
                    return;
                }
            }
        }

        private class GameMetaData
        {
            public GameMetaData(GameData id, string value)
            {
                Id = id;
                Value = value;
            }

            public GameMetaData(BinaryReader br)
            {
                Id = (GameData)br.ReadByte();
                Value = br.ReadString();
            }

            public GameData Id { get; }
            public string Value { get; }

            public bool Equals(GameMetaData other)
            {
                if (Id != other.Id)
                    return false;
                if (Value != other.Value)
                    return false;

                return true;
            }

            public void Write(BinaryWriter bw)
            {
                bw.Write((byte)Id);
                bw.Write(DB.Fn(Value));
            }
        }
    }
}