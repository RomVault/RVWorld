using System;

namespace Compress
{
    public class LocalFile
    {
        internal LocalFile()
        { }

        public string Filename { get; internal set; }
        public ulong UncompressedSize { get; internal set; }
        public byte[] CRC { get; internal set; }

        public bool IsDirectory { get; internal set; }

        public long LastModified => ModifiedTime ?? HeaderLastModified;

        internal long HeaderLastModified { get; set; }
        internal long? ModifiedTime { get; set; }

        public long? CreatedTime { get; internal set; }
        public long? AccessedTime { get; internal set; }


        private LocalFileStatus _status=LocalFileStatus.Nothing;
        internal void SetStatus(LocalFileStatus lfs,bool set=true)
        {
            if (set)
                _status |= lfs;
            else
                _status &= ~lfs;
        }

        public bool GetStatus(LocalFileStatus lfs)
        {
            return (_status & lfs) != 0;
        }

        public virtual ulong? LocalHead => null;
    }

    [Flags]
    public enum LocalFileStatus
    {
        Nothing              = 0x00000,
        Zip64                = 0x00001,
        TrrntZip             = 0x00002,
        FilenameMisMatch     = 0x00010,
        DirectoryLengthError = 0x00020,
        DateTimeMisMatch     = 0x00040
    }
}
