using System;
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
        private SevenZipCompressType zCompType;
        private bool zIsSolid;

        private const int numFastBytes = 64;
        private int _dictionarySize = 1 << 24;

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
            return ZipFileCreate(newFilename, ZipStructure.SevenZipSLZMA);
        }

        public ZipReturn ZipFileCreateFromUncompressedSize(string newFilename, ZipStructure ctype, ulong unCompressedSize)
        {
            return ZipFileCreate(newFilename, ctype, GetDictionarySizeFromUncompressedSize(unCompressedSize));
        }

        public ZipReturn ZipFileCreate(string newFilename, ZipStructure compressOutput, int dictionarySize = 1 << 24)
        {
            if (ZipOpen != ZipOpenType.Closed)
            {
                return ZipReturn.ZipFileAlreadyOpen;
            }

            CompressUtils.CreateDirForFile(newFilename);
            _zipFileInfo = new FileInfo(newFilename);

            int errorCode = FileStream.OpenFileWrite(newFilename, FileStream.BufSizeMax, out _zipFs);
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

            ZipStruct = compressOutput;
            switch (ZipStruct)
            {
                case ZipStructure.SevenZipSLZMA:
                    zCompType = SevenZipCompressType.lzma;
                    zIsSolid = true;
                    break;
                case ZipStructure.SevenZipNLZMA:
                    zCompType = SevenZipCompressType.lzma;
                    zIsSolid = false;
                    break;
                case ZipStructure.SevenZipSZSTD:
                    zCompType = SevenZipCompressType.zstd;
                    zIsSolid = true;
                    break;
                case ZipStructure.SevenZipNZSTD:
                    zCompType = SevenZipCompressType.zstd;
                    zIsSolid = false;
                    break;
                default:
                    zCompType = SevenZipCompressType.lzma;
                    zIsSolid = true;
                    break;
            }

            _dictionarySize = dictionarySize;

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

        UnpackedStreamInfo unpackedStreamInfo;
        public ZipReturn ZipFileOpenWriteStream(bool raw, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, long? modTime = null, int? threadCount = null)
        {
            return ZipFileOpenWriteStream(raw, filename, uncompressedSize, compressionMethod, null, out stream, modTime, threadCount);
        }

        public ZipReturn ZipFileOpenWriteStream(bool raw, string filename, ulong uncompressedSize, ushort compressionMethod, byte[] properties, out Stream stream, long? modTime, int? threadCount = null)
        {
            stream = null;

            switch (zCompType)
            {
                case SevenZipCompressType.lzma: if (compressionMethod != 14) return ZipReturn.ZipTrrntzipIncorrectCompressionUsed; break;
                case SevenZipCompressType.zstd: if (compressionMethod != 93) return ZipReturn.ZipTrrntzipIncorrectCompressionUsed; break;
            }

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

            // not solid or first file of a solid archive
            if (!zIsSolid || _packedOutStreams.Count == 0)
            {
                outStreams newStream = new()
                {
                    packedStart = (ulong)_zipFs.Position,
                    compType = zCompType,
                    packedSize = 0,
                    unpackedStreams = new List<UnpackedStreamInfo>()
                };
                switch (zCompType)
                {
                    case SevenZipCompressType.lzma:
                        if (raw)
                        {
                            newStream.Method = [3, 1, 1];
                            newStream.Properties = properties;
                            _compressStream = _zipFs;
                        }
                        else
                        {
                            int dictionarySize = zIsSolid ? _dictionarySize : GetDictionarySizeFromUncompressedSize(uncompressedSize);
                            LzmaEncoderProperties ep = new(true, dictionarySize, numFastBytes);
                            LzmaStream lzs = new(ep, false, _zipFs);
                            newStream.Method = [3, 1, 1];
                            newStream.Properties = lzs.Properties;
                            _compressStream = lzs;
                        }
                        break;

                    case SevenZipCompressType.zstd:
                        if (raw)
                        {
                            newStream.Method = [4, 247, 17, 1];
                            newStream.Properties = [1, 5, 19, 0, 0];
                            _compressStream = _zipFs;
                        }
                        else
                        {
                            RVZstdSharp.CompressionStream zss = new RVZstdSharp.CompressionStream(_zipFs, 19);
                            zss.SetParameter(RVZstdSharp.Unsafe.ZSTD_cParameter.ZSTD_c_nbWorkers, CompressUtils.SetThreadCount(threadCount));
                            newStream.Method = [4, 247, 17, 1];
                            newStream.Properties = [1, 5, 19, 0, 0];
                            _compressStream = zss;
                        }
                        break;

                    case SevenZipCompressType.uncompressed:
                        newStream.Method = [0];
                        newStream.Properties = null;
                        _compressStream = _zipFs;
                        break;
                }

                _packedOutStreams.Add(newStream);
            }

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

            if (!zIsSolid)
            {
                if (unpackedStreamInfo != null)
                {
                    if (_compressStream is LzmaStream dfStream)
                    {
                        dfStream.Close();
                        dfStream.Dispose();
                    }
                    else if (_compressStream is RVZstdSharp.CompressionStream dfStream2)
                    {
                        dfStream2.Close();
                        dfStream2.Dispose();
                    }

                    _packedOutStreams[_packedOutStreams.Count - 1].packedSize = (ulong)_zipFs.Position - _packedOutStreams[_packedOutStreams.Count - 1].packedStart;
                }
            }

            return ZipReturn.ZipGood;
        }


        private static readonly int[] DictionarySizes =
        [
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
        ];


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
