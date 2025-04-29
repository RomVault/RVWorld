using System.IO;
using Compress.Support.Compression.Deflate;
using Compress.Support.Utils;

// UInt16 = ushort
// UInt32 = uint
// ULong = ulong

namespace Compress.ZipFile
{
    public partial class Zip
    {
        private Stream _compressionStream;

        /*
         raw is true if we are just going to copy the raw data stream from the source to the destination zip file
         trrntzip is true if the source zip is a valid trrntzip file
         compressionMethod must be set to 8 to make a valid trrntzip file.

         if raw is false then compressionMthod must be 0,8 or 93 (zstd)
         */

        public ZipReturn ZipFileOpenWriteStream(bool raw, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, long? modTime, int? threadCount = null)
        {
            stream = null;
            if (ZipOpen != ZipOpenType.OpenWrite)
                return ZipReturn.ZipWritingToInputFile;


            ZipFileData lf = new ZipFileData(filename, modTime ?? 0);

            ZipReturn retVal = lf.LocalFileOpenWriteStream(_zipFs, raw, uncompressedSize, compressionMethod, out stream, threadCount);
            if (retVal != ZipReturn.ZipGood)
                return retVal;

            if (filename.Length > 0)
                lf.IsDirectory = (filename.Substring(filename.Length - 1, 1) == "/");

            _compressionStream = stream;
            _HeadersCentralDir.Add(lf);

            return ZipReturn.ZipGood;
        }


        public ZipReturn ZipFileCloseWriteStream(byte[] crc32)
        {
            if (_compressionStream is ZlibBaseStream dfStream)
            {
                dfStream.Close();
                dfStream.Dispose();
            }
            else if (_compressionStream is RVZstdSharp.CompressionStream dfStream2)
            {
                dfStream2.Close();
                dfStream2.Dispose();
            }


            _compressionStream = null;

            return _HeadersCentralDir[_HeadersCentralDir.Count - 1].LocalFileCloseWriteStream(_zipFs, crc32);
        }

        public ZipReturn ZipFileRollBack()
        {
            if (ZipOpen != ZipOpenType.OpenWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            int fileCount = _HeadersCentralDir.Count;
            if (fileCount == 0)
            {
                return ZipReturn.ZipErrorRollBackFile;
            }

            _HeadersCentralDir.RemoveAt(fileCount - 1);
            _zipFs.Position = (long)_HeadersCentralDir[fileCount - 1].RelativeOffsetOfLocalHeader;
            return ZipReturn.ZipGood;
        }

    }
}
