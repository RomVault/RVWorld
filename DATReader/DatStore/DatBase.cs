namespace DATReader.DatStore
{
    public enum DatFileStatus
    {
        InDatCollect,
        InDatMerged,
        InDatBad,
        InDatMIA
    }

    public enum DatFileType
    {
        UnSet, 

        Dir,
        DirRVZip,
        DirTorrentZip,
        Dir7Zip,

        File,
        //FileRvZip,
        FileTorrentZip,
        File7Zip
    }
    
    public abstract class DatBase
    {
        public string Name;
        public DatFileStatus DatStatus = DatFileStatus.InDatCollect;
        

        protected DatBase(string name,DatFileType type)
        {
            Name = name;
            DatFileType = type;
        }

        public DatFileType DatFileType;
    }
}
