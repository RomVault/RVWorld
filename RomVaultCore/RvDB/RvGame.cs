/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using DATReader.DatStore;
using System.Collections.Generic;
using System.IO;

namespace RomVaultCore.RvDB
{
    public class RvGame
    {
        public enum GameData
        {
            Id = 11,
            Description = 1,
            RomOf = 2,
            IsBios = 3,
            Sourcefile = 4,
            CloneOf = 5,
            CloneOfId = 24,
            SampleOf = 6,
            Board = 7,
            Year = 8,
            Manufacturer = 9,

            EmuArc = 10,
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
            Source = 22,

            Category = 23,

        }

        private readonly List<GameMetaData> _gameMetaData = new List<GameMetaData>();
             


        public RvGame(BinaryReader br)
        {
            byte c = br.ReadByte();
            _gameMetaData.Clear();
            _gameMetaData.Capacity = c;
            for (byte i = 0; i < c; i++)
            {
                _gameMetaData.Add(new GameMetaData(br));
            }
        }


        public RvGame(string description)
        {
            AddData(GameData.Description, description);
        }

        public RvGame(DatGame dGame)
        {
            CheckAttribute(dGame.Id, GameData.Id);
            CheckAttribute(dGame.Description, GameData.Description);
            CheckAttribute(dGame.Category == null ? null : string.Join(" | ", dGame.Category), GameData.Category);
            CheckAttribute(dGame.RomOf, GameData.RomOf);
            CheckAttribute(dGame.IsBios, GameData.IsBios);
            CheckAttribute(dGame.SourceFile, GameData.Sourcefile);
            CheckAttribute(dGame.CloneOf, GameData.CloneOf);
            CheckAttribute(dGame.CloneOfId, GameData.CloneOfId);
            CheckAttribute(dGame.SampleOf, GameData.SampleOf);
            CheckAttribute(dGame.Board, GameData.Board);
            CheckAttribute(dGame.Year, GameData.Year);
            CheckAttribute(dGame.Manufacturer, GameData.Manufacturer);

            if (dGame.IsEmuArc)
            {
                AddData(GameData.EmuArc, "yes");
                CheckAttribute(dGame.Publisher, GameData.Publisher);
                CheckAttribute(dGame.Developer, GameData.Developer);
                CheckAttribute(dGame.Genre, GameData.Genre);
                CheckAttribute(dGame.SubGenre, GameData.SubGenre);
                CheckAttribute(dGame.Ratings, GameData.Ratings);
                CheckAttribute(dGame.Score, GameData.Score);
                CheckAttribute(dGame.Players, GameData.Players);
                CheckAttribute(dGame.Enabled, GameData.Enabled);
                CheckAttribute(dGame.CRC, GameData.CRC);
                CheckAttribute(dGame.RelatedTo, GameData.RelatedTo);
                CheckAttribute(dGame.Source, GameData.Source);
            }
        }
        private void CheckAttribute( string source, GameData gParam)
        {
            if (string.IsNullOrWhiteSpace(source))
                return;
            AddData(gParam, source);
        }

        /*
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
        */

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)_gameMetaData.Count);
            foreach (GameMetaData gameMD in _gameMetaData)
            {
                gameMD.Write(bw);
            }
        }

        private void AddData(GameData id, string val)
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
                bw.Write(DB.FixNull(Value));
            }
        }
    }
}