namespace DATReader.DatStore
{
    public class DatHeader
    {
        public string Filename;
        public string Name;
        public string Type;
        public string RootDir;
        public string Description;
        public string Category;
        public string Version;
        public string Date;
        public string Author;
        public string Email;
        public string Homepage;
        public string URL;
        public string Comment;
        public string Header;
        public string Compression;
        public string MergeType;
        public string Split;  //ROMCenter
        public string NoDump;
        public string Dir;
        public bool IsSuperDat;
        public bool NotZipped;

        public DatDir BaseDir;
    }
}