namespace DATReader.DatStore
{
    public class DatFile : DatBase
    {
        public DatFile(string name, DatFileType type) : base(name, type) { }

        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public byte[] SHA256;
        public string Merge;
        public string Status;
        public string DateModified;
        public string Region;
        public string MIA;
        public bool isDisk;

    }
}