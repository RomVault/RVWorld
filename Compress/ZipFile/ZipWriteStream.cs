using System.IO;
using Compress.Utils;
using Compress.ZipFile.ZLib;

// UInt16 = ushort
// UInt32 = uint
// ULong = ulong

namespace Compress.ZipFile
{
    public partial class Zip
    {
        private Stream _compressionStream;

        public ZipReturn ZipFileOpenWriteStream(bool raw, bool trrntzip, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, TimeStamps timeStamp = null)
        {
            stream = null;
            if (ZipOpen != ZipOpenType.OpenWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            LocalFile lf = new LocalFile(filename, timeStamp);

            ZipReturn retVal = lf.LocalFileOpenWriteStream(_zipFs, raw, trrntzip, uncompressedSize, compressionMethod, out stream);

            _compressionStream = stream;
            _localFiles.Add(lf);

            return retVal;
        }

        public void ZipFileAddZeroLengthFile()
        {
            LocalFile.LocalFileAddZeroLengthFile(_zipFs);
        }


        public ZipReturn ZipFileCloseWriteStream(byte[] crc32)
        {
            if (_compressionStream is ZlibBaseStream dfStream)
            {
                dfStream.Flush();
                dfStream.Close();
                dfStream.Dispose();
            }
            _compressionStream = null;

            return _localFiles[_localFiles.Count - 1].LocalFileCloseWriteStream(_zipFs, crc32);
        }

        public ZipReturn ZipFileRollBack()
        {
            if (ZipOpen != ZipOpenType.OpenWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            int fileCount = _localFiles.Count;
            if (fileCount == 0)
            {
                return ZipReturn.ZipErrorRollBackFile;
            }

            LocalFile lf = _localFiles[fileCount - 1];

            _localFiles.RemoveAt(fileCount - 1);
            _zipFs.Position = (long)lf.LocalFilePos;
            return ZipReturn.ZipGood;
        }

    }
}
