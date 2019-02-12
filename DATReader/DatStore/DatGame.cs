using System.Collections.Generic;

namespace DATReader.DatStore
{
    public class DatGame
    {
        public string Description;

        public string Manufacturer;
        public string History;
        public string CloneOf;
        public string RomOf;
        public string SampleOf;
        public string SourceFile;
        public string IsBios;
        public string IsDevice;
        public string Board;
        public string Year;
        public string Runnable;

        public List<string> device_ref;
        public List<string> slot;

        public bool IsEmuArc;
        public string TitleId;
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
    }
}