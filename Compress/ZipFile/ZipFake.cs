using System.IO;

namespace Compress.ZipFile
{
    public partial class Zip
    {

        public void ZipCreateFake()
        {
            if (ZipOpen != ZipOpenType.Closed)
                return;

            ZipOpen = ZipOpenType.OpenFakeWrite;
        }

        internal void ZipFileFakeOpenMemoryStream()
        {
            _zipFs = new MemoryStream();
        }

        internal byte[] ZipFileFakeCloseMemoryStream()
        {
            byte[] ret = ((MemoryStream)_zipFs).ToArray();
            _zipFs.Close();
            _zipFs.Dispose();
            ZipOpen = ZipOpenType.Closed;
            return ret;
        }


        public ZipReturn ZipFileAddFake(string filename, ulong fileOffset, ulong uncompressedSize, ulong compressedSize, byte[] crc32, ushort compressionMethod, long headerLastModified, out byte[] localHeader)
        {
            localHeader = null;

            if (ZipOpen != ZipOpenType.OpenFakeWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            ZipFileData lf = new(filename);
            _HeadersCentralDir.Add(lf);

            MemoryStream ms = new();
            lf.LocalFileHeaderFake(fileOffset, uncompressedSize, compressedSize, crc32, compressionMethod, headerLastModified, ms);

            localHeader = ms.ToArray();
            ms.Close();

            return ZipReturn.ZipGood;
        }
    }
}
