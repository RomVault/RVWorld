namespace DATReader.DatStore
{


    public abstract class DatBase
    {
        public string Name;
        public DatStatus DatStatus = DatStatus.InDatCollect;
        public FileType FileType;
        public long? DateModified = null;

        protected DatBase(string name, FileType type)
        {
            Name = name;
            FileType = type;
        }

        protected DatBase(DatBase cp)
        {
            Name = cp.Name;
            DatStatus = cp.DatStatus;
            FileType = cp.FileType;
            DateModified = cp.DateModified;
        }
    }
}
