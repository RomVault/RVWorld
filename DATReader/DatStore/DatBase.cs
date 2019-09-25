namespace DATReader.DatStore
{
    public enum DatFileStatus
    {
        InDatCollect,
        InDatMerged,
        InDatBad
    }

    public enum DatFileType
    {
        UnSet, 

        Dir,
        DirRVZip,
        DirTorrentZip,
        Dir7Zip,

        File,
        FileTorrentZip,
        File7Zip
    }
    
    public abstract class DatBase
    {
        public string Name;
        
        public DatFileStatus DatStatus = DatFileStatus.InDatCollect;
        

        protected DatBase(DatFileType type)
        {
            DatFileType = type;
        }

        public DatFileType DatFileType;
    }
}
