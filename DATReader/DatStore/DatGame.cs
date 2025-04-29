using System.Collections.Generic;

namespace DATReader.DatStore
{
    public class DatGame
    {
        public string Id;
        public string Description;

        public string Manufacturer;
        public string History;
        public string CloneOf;
        public string CloneOfId;
        public string RomOf;
        public string SampleOf;
        public string SourceFile;
        public string IsBios;
        public string IsDevice;
        public string Board;
        public string Year;
        public string Runnable;

        public List<string> Category;
        public List<string> device_ref;

        public bool IsEmuArc;
        public string Publisher;
        public string Developer;
        public string Genre;
        public string SubGenre;
        public string Ratings;
        public string Score;
        public string Players;
        public string Enabled;
        public string CRC;
        public string Source;
        public string RelatedTo;

        //public string Comments;
        public byte[] gameHash;
        public bool found;

        public DatGame() { }

        public DatGame(DatGame dg)
        {
            Id = dg.Id;
            Description = dg.Description;

            Manufacturer = dg.Manufacturer;
            History = dg.History;
            CloneOf = dg.CloneOf;
            CloneOfId = dg.CloneOfId;
            RomOf = dg.RomOf;
            SampleOf = dg.SampleOf;
            SourceFile = dg.SourceFile;
            IsBios = dg.IsBios;
            IsDevice = dg.IsDevice;
            Board = dg.Board;
            Year = dg.Year;
            Runnable = dg.Runnable;

            //

            IsEmuArc = dg.IsEmuArc;
            Publisher = dg.Publisher;
            Developer = dg.Developer;
            Genre = dg.Genre;
            SubGenre = dg.SubGenre;
            Ratings = dg.Ratings;
            Score = dg.Score;
            Players = dg.Players;
            Enabled = dg.Enabled;
            CRC = dg.CRC;
            Source = dg.Source;
            RelatedTo = dg.RelatedTo;

            gameHash = dg.gameHash;
            found = dg.found;
        }
    }
}