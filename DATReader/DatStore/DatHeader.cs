namespace DATReader.DatStore
{
    public class DatHeader
    {
        public string Id;
        public string Filename;
        public bool MameXML;
        public string Name;
        public string Type;
        public string RootDir;
        public string Description;
        public string Subset;
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
        public bool NotZipped;

        public DatDir BaseDir;

        public DatHeader() { }

        public DatHeader(DatHeader dh)
        {
            Id=dh.Id;
            Filename=dh.Filename;
            MameXML=dh.MameXML;
            Name=dh.Name;
            Type=dh.Type;
            RootDir=dh.RootDir;
            Description=dh.Description;
            Subset = dh.Subset;
            Category=dh.Category;
            Version=dh.Version;
            Date=dh.Date;
            Author=dh.Author;
            Email=dh.Email;
            Homepage=dh.Homepage;
            URL=dh.URL;
            Comment=dh.Comment;
            Header=dh.Header;
            Compression=dh.Compression;
            MergeType=dh.MergeType;
            Split=dh.Split;
            NoDump=dh.NoDump;
            Dir=dh.Dir;
            NotZipped=dh.NotZipped;

            BaseDir = new DatDir(dh.BaseDir);
        }
    }
}