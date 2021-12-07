using System;
using System.IO;
using Compress.Support.Utils;

namespace Compress
{
    public interface ICompress
    {
        int LocalFilesCount();

        LocalFile GetLocalFile(int i);
       
        ZipOpenType ZipOpen { get; }

        ZipReturn ZipFileOpen(string newFilename, long timestamp = -1, bool readHeaders = true);

        ZipReturn ZipFileOpen(Stream inStream);
        void ZipFileClose();

        ZipReturn ZipFileOpenReadStream(int index, out Stream stream, out ulong streamSize);
        ZipReturn ZipFileOpenWriteStream(bool raw, bool trrntzip, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, TimeStamps dateTime = null);
        ZipReturn ZipFileCloseReadStream();


        ZipStatus ZipStatus { get; }

        string ZipFilename { get; }
        long TimeStamp { get; }

        void ZipFileAddZeroLengthFile();

        ZipReturn ZipFileCreate(string newFilename);
        ZipReturn ZipFileCloseWriteStream(byte[] crc32);
        void ZipFileCloseFailed();

    }
}
