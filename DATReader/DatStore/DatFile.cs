namespace DATReader.DatStore
{
    public class DatFile :DatBase
    {
        public DatFile(DatFileType type) : base(type)
        {
        }

        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public string Merge;
        public string Status;
        public string Date;
        public string Region;
        public bool isDisk;

    }
}