using System.Collections.ObjectModel;

namespace DATReader.DatStore
{
    public class DatFile : DatBase
    {
        public DatFile(string name, FileType type) : base(name, type) { }

        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public byte[] SHA256;
        public string Merge;
        public string Status;
        public string Region;
        public string MIA;
        public bool isDisk;

        public HeaderFileType HeaderFileType;

        public DatFile(DatFile df) : base(df)
        {
            Size = df.Size;
            CRC = df.CRC;
            SHA1 = df.SHA1;
            MD5 = df.MD5;
            SHA256 = df.SHA256;
            Merge = df.Merge;
            Status = df.Status;
            DateModified= df.DateModified;
            Region = df.Region;
            MIA = df.MIA;
            isDisk= df.isDisk;

            HeaderFileType = df.HeaderFileType;
        }
    }
}