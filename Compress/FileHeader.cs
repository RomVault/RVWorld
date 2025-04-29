using System;

namespace Compress
{
    public class FileHeader
    {
        internal FileHeader()
        { }

        public string Filename { get; internal set; }
        public ulong UncompressedSize { get; internal set; }
        public byte[] CRC { get; internal set; }

        public bool IsDirectory { get; internal set; }

        public long LastModified => ModifiedTime ?? HeaderLastModified;
        
        public long HeaderLastModified { get; internal set; }

        public long? ModifiedTime { get; internal set; }
        public long? CreatedTime { get; internal set; }
        public long? AccessedTime { get; internal set; }

        public virtual ulong? LocalHead => null;
    }

}
