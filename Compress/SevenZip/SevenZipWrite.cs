using System.Collections.Generic;
using System.IO;
using System.Text;
using Compress.SevenZip.Structure;
using Compress.Support.Compression.LZMA;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace Compress.SevenZip
{
    public partial class SevenZ
    {
        private Stream _compressStream;

        public class outStreams
        {
            public SevenZipCompressType compType;
            public byte[] Method;
            public byte[] Properties;
            public ulong packedStart;
            public ulong packedSize;
            public List<UnpackedStreamInfo> unpackedStreams;
        }

        public List<outStreams> _packedOutStreams;

        public ZipReturn ZipFileCreate(string newFilename)
        {
            return ZipFileCreate(newFilename, SevenZipCompressType.lzma);
        }

        public ZipReturn ZipFileCreateFromUncompressedSize(string newFilename, SevenZipCompressType ctype, ulong unCompressedSize)
        {
            return ZipFileCreate(newFilename, ctype, GetDictionarySizeFromUncompressedSize(unCompressedSize));
        }

        private SevenZipCompressType _compType;

        public ZipReturn ZipFileCreate(string newFilename, SevenZipCompressType compressOutput, int dictionarySize = 1 << 24, int numFastBytes = 64)
        {
            if (ZipOpen != ZipOpenType.Closed)
            {
                return ZipReturn.ZipFileAlreadyOpen;
            }

            CompressUtils.CreateDirForFile(newFilename);
            _zipFileInfo = new FileInfo(newFilename);

            int errorCode = FileStream.OpenFileWrite(newFilename, out _zipFs);
            if (errorCode != 0)
            {
                ZipFileClose();
                return ZipReturn.ZipErrorOpeningFile;
            }
            ZipOpen = ZipOpenType.OpenWrite;

            _signatureHeader = new SignatureHeader();
            _header = new Header();

            using (BinaryWriter bw = new(_zipFs, Encoding.UTF8, true))
            {
                _signatureHeader.Write(bw);
            }
            _baseOffset = _zipFs.Position;


            _packedOutStreams = new List<outStreams>();

            _compType = compressOutput;

#if solid
            outStreams newStream = new()
            {
                packedStart = (ulong)_zipFs.Position,
                compType = compressOutput,
                packedSize = 0,
                unpackedStreams = new List<UnpackedStreamInfo>()
            };
            switch (compressOutput)
            {
                case SevenZipCompressType.lzma:
                    LzmaEncoderProperties ep = new(true, dictionarySize, numFastBytes);
                    LzmaStream lzs = new(ep, false, _zipFs);

                    newStream.Method = new byte[] { 3, 1, 1 };
                    newStream.Properties = lzs.Properties;
                    _compressStream = lzs;
                    break;


                case SevenZipCompressType.zstd:


                    Stream zss = new ZstdSharp.CompressionStream(_zipFs, 18);
                    
                    newStream.Method = new byte[] { 4, 247, 17, 1 };
                    newStream.Properties = new byte[] { 1, 5, 19, 0, 0 };
                    _compressStream = zss;
                    break;

                case SevenZipCompressType.uncompressed:
                    newStream.Method = new byte[] { 0 };
                    newStream.Properties = null;
                    _compressStream = _zipFs;
                    break;
            }

            _packedOutStreams.Add(newStream);
#endif
            return ZipReturn.ZipGood;
        }

        public void ZipFileAddDirectory(string filename)
        {
            string fName = filename;
            if (fName.Substring(fName.Length - 1, 1) == @"/")
                fName = fName.Substring(0, fName.Length - 1);

            SevenZipLocalFile lf = new()
            {
                Filename = fName,
                UncompressedSize = 0,
                IsDirectory = true
            };
            _localFiles.Add(lf);
            unpackedStreamInfo = null;
        }

        public void ZipFileAddZeroLengthFile()
        {
            // do nothing here for 7zip
        }



        UnpackedStreamInfo unpackedStreamInfo;
        public ZipReturn ZipFileOpenWriteStream(bool raw, bool trrntzip, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, TimeStamps dateTime)
        {
            // check if we are writing a directory
            if (uncompressedSize == 0 && filename.Substring(filename.Length - 1, 1) == "/")
            {
                ZipFileAddDirectory(filename);
                stream = null;
                return ZipReturn.ZipGood;
            }

            SevenZipLocalFile localFile = new()
            {
                Filename = filename,
                UncompressedSize = uncompressedSize
            };
            _localFiles.Add(localFile);

            if (uncompressedSize == 0)
            {
                unpackedStreamInfo = null;
                stream = null;
                return ZipReturn.ZipGood;
            }


#if !solid

            outStreams newStream = new()
            {
                packedStart = (ulong)_zipFs.Position,
                compType = _compType,
                packedSize = 0,
                unpackedStreams = new List<UnpackedStreamInfo>()
            };
            switch (_compType)
            {
                case SevenZipCompressType.lzma:

                    LzmaEncoderProperties ep = new(true, GetDictionarySizeFromUncompressedSize(uncompressedSize), 64);
                    LzmaStream lzs = new(ep, false, _zipFs);
                    newStream.Method = new byte[] { 3, 1, 1 };
                    newStream.Properties = lzs.Properties;
                    _compressStream = lzs;
                    break;

                case SevenZipCompressType.zstd:

                    ZstdSharp.CompressionStream zss = new(_zipFs, 19);
                    newStream.Method = new byte[] { 4, 247, 17, 1 };
                    newStream.Properties = new byte[] { 1, 5, 19, 0, 0 };
                    _compressStream = zss;
                    break;

                case SevenZipCompressType.uncompressed:
                    newStream.Method = new byte[] { 0 };
                    newStream.Properties = null;
                    _compressStream = _zipFs;
                    break;
            }

            _packedOutStreams.Add(newStream);
#endif

            unpackedStreamInfo = new UnpackedStreamInfo { UnpackedSize = uncompressedSize };
            _packedOutStreams[_packedOutStreams.Count - 1].unpackedStreams.Add(unpackedStreamInfo);

            stream = _compressStream;
            return ZipReturn.ZipGood;
        }


        public ZipReturn ZipFileCloseWriteStream(byte[] crc32)
        {
            SevenZipLocalFile localFile = _localFiles[_localFiles.Count - 1];
            localFile.CRC = new[] { crc32[3], crc32[2], crc32[1], crc32[0] };

            if (unpackedStreamInfo != null)
                unpackedStreamInfo.Crc = Util.BytesToUint(localFile.CRC);

#if !solid
            if (unpackedStreamInfo != null)
            {
                if (_packedOutStreams[_packedOutStreams.Count - 1].compType != SevenZipCompressType.uncompressed)
                {
                    _compressStream.Flush();
                    _compressStream.Close();
                }
                _packedOutStreams[_packedOutStreams.Count - 1].packedSize = (ulong)_zipFs.Position - _packedOutStreams[_packedOutStreams.Count - 1].packedStart;
            }
#endif

            return ZipReturn.ZipGood;
        }


        private static readonly int[] DictionarySizes =
        {
            0x10000,
            0x18000,
            0x20000,
            0x30000,
            0x40000,
            0x60000,
            0x80000,
            0xc0000,

            0x100000,
            0x180000,
            0x200000,
            0x300000,
            0x400000,
            0x600000,
            0x800000,
            0xc00000,

            0x1000000,
            0x1800000,
            0x2000000,
            0x3000000,
            0x4000000,
            0x6000000
        };


        private static int GetDictionarySizeFromUncompressedSize(ulong unCompressedSize)
        {
            foreach (int v in DictionarySizes)
            {
                if ((ulong)v >= unCompressedSize)
                    return v;
            }

            return DictionarySizes[DictionarySizes.Length - 1];
        }
    }
}
