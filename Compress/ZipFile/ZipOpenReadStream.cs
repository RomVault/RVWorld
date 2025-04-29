using System.IO;

namespace Compress.ZipFile
{
    public partial class Zip
    {
        public ZipReturn ZipFileOpenReadStream(int index, out Stream stream, out ulong streamSize)
        {
            return ZipFileOpenReadStream(index, false, out stream, out streamSize, out ushort _);
        }

        public ZipReturn ZipFileOpenReadStream(int index, bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
        {
            ZipFileCloseReadStream();

            if (ZipOpen != ZipOpenType.OpenRead)
            {
                stream = null;
                streamSize = 0;
                compressionMethod = 0;
                return ZipReturn.ZipReadingFromOutputFile;
            }

            ZipReturn zRet = _HeadersLocalFile[index].LocalFileOpenReadStream(_zipFs, raw, out stream, out streamSize, out compressionMethod);
            _compressionStream = stream;
            return zRet;
        }


        public ZipReturn ZipFileOpenReadStreamFromLocalHeaderPointer(ulong localIndexOffset, bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
        {
            ZipFileCloseReadStream();


            ZipFileData CentralFile = new ZipFileData
            {
                RelativeOffsetOfLocalHeader = localIndexOffset,
                CompressedSize = 0,
                UncompressedSize = 0
            };

            ZipReturn zRet = ZipFileData.LocalFileHeaderRead(_zipFs, CentralFile, out ZipFileData localFile);
            if (zRet != ZipReturn.ZipGood)
            {
                stream = null;
                streamSize = 0;
                compressionMethod = 0;
                return zRet;
            }
            if ((localFile.GeneralPurposeBitFlag & 8) == 8)
            {
                stream = null;
                streamSize = 0;
                compressionMethod = 0;
                return ZipReturn.ZipCannotFastOpen;
            }

            zRet = localFile.LocalFileOpenReadStream(_zipFs, raw, out stream, out streamSize, out compressionMethod);
            _compressionStream = stream;
            return zRet;
        }


        public ZipReturn ZipFileCloseReadStream()
        {
            if (_compressionStream == null)
                return ZipReturn.ZipGood;

            if (_compressionStream != _zipFs)
            {
                _compressionStream.Close();
                _compressionStream.Dispose();
            }

            _compressionStream = null;

            return ZipReturn.ZipGood;
        }


    }
}
