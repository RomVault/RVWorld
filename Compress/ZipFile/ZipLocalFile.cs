using System.IO;
using System.Text;
using Compress.Support.Compression.BZip2;
using Compress.Support.Compression.Deflate;
using Compress.Support.Compression.Deflate64;
using Compress.Support.Compression.LZMA;
using Compress.Support.Compression.PPmd;

namespace Compress.ZipFile
{
    internal class ZipLocalFile : LocalFile
    {
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;

        private ushort _compressionMethod;

        private ulong _compressedSize;
        internal ulong RelativeOffsetOfLocalHeader; // only in central directory

        private ulong _extraLocation;
        private byte[] _extraField;
        private ulong _dataLocation;

        internal ushort GeneralPurposeBitFlag { get; private set; }

        //internal bool TrrntZip { get; private set; }

        public override ulong? LocalHead => (GeneralPurposeBitFlag & 8) == 0 ? (ulong?)RelativeOffsetOfLocalHeader : null;

        internal ZipLocalFile()
        {
        }

        internal ZipLocalFile(string filename, TimeStamps dateTime = null)
        {
            SetStatus(LocalFileStatus.Zip64, false);
            GeneralPurposeBitFlag = 2; // Maximum Compression Deflating
            _compressionMethod = 8; // Compression Method Deflate

            if (dateTime?.ModTime == null)
            {
                HeaderLastModified = CompressUtils.TrrntzipDateTime;
            }
            else
            {
                HeaderLastModified = (long)dateTime.ModTime;
            }

            Filename = filename;
        }


        internal ZipReturn CentralDirectoryRead(Stream zipFs, ulong offset)
        {
            try
            {
                using BinaryReader br = new(zipFs, Encoding.UTF8, true);
                uint thisSignature = br.ReadUInt32();
                if (thisSignature != CentralDirectoryHeaderSignature)
                {
                    return ZipReturn.ZipCentralDirError;
                }

                br.ReadUInt16(); // Version Made By

                br.ReadUInt16(); // Version Needed To Extract


                GeneralPurposeBitFlag = br.ReadUInt16();
                _compressionMethod = br.ReadUInt16();

                ushort lastModFileTime = br.ReadUInt16();
                ushort lastModFileDate = br.ReadUInt16();
                HeaderLastModified = CompressUtils.UtcTicksFromDosDateTime(lastModFileDate, lastModFileTime);

                CRC = ReadCRC(br);

                _compressedSize = br.ReadUInt32();
                UncompressedSize = br.ReadUInt32();

                ushort fileNameLength = br.ReadUInt16();
                ushort extraFieldLength = br.ReadUInt16();
                ushort fileCommentLength = br.ReadUInt16();

                br.ReadUInt16(); // diskNumberStart
                br.ReadUInt16(); // internalFileAttributes
                br.ReadUInt32(); // externalFileAttributes

                RelativeOffsetOfLocalHeader = br.ReadUInt32();


                byte[] bFileName = br.ReadBytes(fileNameLength);
                Filename = (GeneralPurposeBitFlag & (1 << 11)) == 0
                    ? CompressUtils.GetString(bFileName)
                    : Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

                if (extraFieldLength > 0)
                {
                    byte[] extraField = br.ReadBytes(extraFieldLength);

                    ZipReturn zr = ZipExtraField.ReadExtraField(extraField, bFileName, this, ref _compressedSize, ref RelativeOffsetOfLocalHeader, true);
                    if (zr != ZipReturn.ZipGood)
                        return zr;
                }

                RelativeOffsetOfLocalHeader += offset;

                if (CompressUtils.IsCodePage437(Filename) != ((GeneralPurposeBitFlag & (1 << 11)) == 0))
                    SetStatus(LocalFileStatus.TrrntZip, false);

                if (fileCommentLength > 0)
                {
                    byte[] fileComment = br.ReadBytes(fileCommentLength);
                }

                if (Filename.Length > 0)
                {
                    char lastChar = Filename[Filename.Length - 1];
                    IsDirectory = (lastChar == '/' || lastChar == '\\');
                    if (IsDirectory && UncompressedSize > 0) SetStatus(LocalFileStatus.DirectoryLengthError);
                }
                /*
                4.4.5 compression method: (2 bytes)

                0 - (Supported) The file is stored (no compression)
                1 - The file is Shrunk
                2 - The file is Reduced with compression factor 1
                3 - The file is Reduced with compression factor 2
                4 - The file is Reduced with compression factor 3
                5 - The file is Reduced with compression factor 4
                6 - The file is Imploded
                7 - Reserved for Tokenizing compression algorithm
                8 - (Supported) The file is Deflated
                9 - (Supported) Enhanced Deflating using Deflate64(tm)
               10 - PKWARE Data Compression Library Imploding (old IBM TERSE)
               11 - Reserved by PKWARE
               12 - (Supported) File is compressed using BZIP2 algorithm
               13 - Reserved by PKWARE
               14 - (Supported) LZMA
               15 - Reserved by PKWARE
               16 - IBM z/OS CMPSC Compression
               17 - Reserved by PKWARE
               18 - File is compressed using IBM TERSE (new)
               19 - IBM LZ77 z Architecture 
               20 - deprecated (use method 93 for zstd)
               93 - (Supported, with external DLL) Zstandard (zstd) Compression 
               94 - MP3 Compression 
               95 - XZ Compression 
               96 - JPEG variant
               97 - WavPack compressed data
               98 - (Supported) PPMd version I, Rev 1
               99 - AE-x encryption marker (see APPENDIX E)
        */

                switch (_compressionMethod)
                {
                    case 0: // The file is stored (no compression)
                
                    case 8: // The file is Deflated
                    case 9: // Enhanced Deflating using Deflate64(tm)
                    case 12: // The file is BZIP2 algorithm. 
                    case 14: // LZMA
                        return ZipReturn.ZipGood;

                    case 20:
                    case 93: // Zstandard (zstd) Compression 
                        return ZipReturn.ZipGood;

                    case 98: // PPMd version I, Rev 1
                        return ZipReturn.ZipGood;

                    default:
                        return ZipReturn.ZipUnsupportedCompression;
                }
            }
            catch
            {
                return ZipReturn.ZipCentralDirError;
            }
        }

        internal void CentralDirectoryWrite(Stream crcStream)
        {
            using BinaryWriter bw = new(crcStream, Encoding.UTF8, true);

            ZipExtraFieldWrite zefw = new();
            SetStatus(LocalFileStatus.Zip64,
                zefw.Zip64(UncompressedSize, _compressedSize, RelativeOffsetOfLocalHeader, true,
                out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader)
                );
            _extraField = zefw.ExtraField;

            ushort extraFieldLength = (ushort)_extraField.Length;

            byte[] bFileName;
            if (CompressUtils.IsCodePage437(Filename))
            {
                bFileName = CompressUtils.GetBytes(Filename);
            }
            else
            {
                GeneralPurposeBitFlag |= 1 << 11;
                bFileName = Encoding.UTF8.GetBytes(Filename);
            }

            ushort fileNameLength = (ushort)bFileName.Length;

            ushort versionNeededToExtract = (ushort)(GetStatus(LocalFileStatus.Zip64) ? 45 : 20);

            CompressUtils.UtcTicksToDosDateTime(HeaderLastModified, out ushort lastModFileDate, out ushort lastModFileTime);

            bw.Write(CentralDirectoryHeaderSignature);
            bw.Write((ushort)0);
            bw.Write(versionNeededToExtract);
            bw.Write(GeneralPurposeBitFlag);
            bw.Write(_compressionMethod);
            bw.Write(lastModFileTime);
            bw.Write(lastModFileDate);
            WriteCRC(bw, CRC);
            bw.Write(headerCompressedSize);
            bw.Write(headerUnCompressedSize);
            bw.Write(fileNameLength);
            bw.Write(extraFieldLength);
            bw.Write((ushort)0); // file comment length
            bw.Write((ushort)0); // disk number start
            bw.Write((ushort)0); // internal file attributes
            bw.Write((uint)0); // external file attributes
            bw.Write(headerRelativeOffsetOfLocalHeader);
            bw.Write(bFileName, 0, fileNameLength);
            bw.Write(_extraField, 0, extraFieldLength);
            // No File Comment
        }
        internal ZipReturn LocalFileHeaderRead(Stream zipFs)
        {
            try
            {
                using (BinaryReader br = new(zipFs, Encoding.UTF8, true))
                {

                    SetStatus(LocalFileStatus.TrrntZip);

                    zipFs.Position = (long)RelativeOffsetOfLocalHeader;
                    uint thisSignature = br.ReadUInt32();
                    if (thisSignature != LocalFileHeaderSignature)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    br.ReadUInt16(); // version needed to extract
                    ushort generalPurposeBitFlagLocal = br.ReadUInt16();
                    if (generalPurposeBitFlagLocal != GeneralPurposeBitFlag)
                    {
                        SetStatus(LocalFileStatus.TrrntZip, false);
                    }

                    ushort tshort = br.ReadUInt16();
                    if (tshort != _compressionMethod)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    ushort lastModFileTime = br.ReadUInt16();
                    ushort lastModFileDate = br.ReadUInt16();

                    long tTime = CompressUtils.UtcTicksFromDosDateTime(lastModFileDate, lastModFileTime);

                    if (tTime != HeaderLastModified)
                    {
                        SetStatus(LocalFileStatus.DateTimeMisMatch);
                        SetStatus(LocalFileStatus.TrrntZip, false);
                    }

                    LocalFile localHeader = new();
                    localHeader.CRC = ReadCRC(br);
                    ulong localHeaderCompressedSize = br.ReadUInt32();
                    localHeader.UncompressedSize = br.ReadUInt32();
                    ulong localRelativeOffset = 0;

                    ushort fileNameLength = br.ReadUInt16();
                    ushort extraFieldLength = br.ReadUInt16();


                    byte[] bFileName = br.ReadBytes(fileNameLength);
                    localHeader.Filename = (generalPurposeBitFlagLocal & (1 << 11)) == 0
                        ? CompressUtils.GetString(bFileName)
                        : Encoding.UTF8.GetString(bFileName, 0, fileNameLength);


                    if (extraFieldLength > 0)
                    {
                        byte[] extraField = br.ReadBytes(extraFieldLength);

                        ZipReturn zr = ZipExtraField.ReadExtraField(extraField, bFileName, localHeader, ref localHeaderCompressedSize, ref localRelativeOffset, false);
                        if (zr != ZipReturn.ZipGood)
                            return zr;
                    }

                    if (!CompressUtils.CompareString(Filename, localHeader.Filename))
                    {
                        SetStatus(LocalFileStatus.TrrntZip, false);
                        if (!CompressUtils.CompareStringSlash(Filename.ToLower(), localHeader.Filename.ToLower()))
                        {
                            SetStatus(LocalFileStatus.FilenameMisMatch);
                        }
                    }

                    _dataLocation = (ulong)zipFs.Position;

                    if ((GeneralPurposeBitFlag & 8) == 8)
                    {
                        SetStatus(LocalFileStatus.TrrntZip, false);
                        zipFs.Position += (long)_compressedSize;

                        localHeader.CRC = ReadCRC(br);
                        if (CompressUtils.ByteArrCompare(localHeader.CRC, new byte[] { 0x08, 0x07, 0x4b, 0x50 }))
                        {
                            localHeader.CRC = ReadCRC(br);
                        }

                        if (GetStatus(LocalFileStatus.Zip64))
                        {
                            localHeaderCompressedSize = br.ReadUInt64();
                            localHeader.UncompressedSize = br.ReadUInt64();
                        }
                        else
                        {
                            localHeaderCompressedSize = br.ReadUInt32();
                            localHeader.UncompressedSize = br.ReadUInt32();
                        }
                    }

                    if (CompressUtils.IsCodePage437(Filename) != ((GeneralPurposeBitFlag & (1 << 11)) == 0))
                        SetStatus(LocalFileStatus.TrrntZip, false);


                    if (!CompressUtils.ByteArrCompare(localHeader.CRC, CRC))
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    if (localHeaderCompressedSize != _compressedSize)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    if (localHeader.UncompressedSize != UncompressedSize)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    return ZipReturn.ZipGood;
                }
            }
            catch
            {
                return ZipReturn.ZipLocalFileHeaderError;
            }
        }

        internal ZipReturn LocalFileHeaderReadQuick(Stream zipFs)
        {
            try
            {
                using BinaryReader br = new(zipFs, Encoding.UTF8, true);
                SetStatus(LocalFileStatus.TrrntZip);

                zipFs.Position = (long)RelativeOffsetOfLocalHeader;
                uint thisSignature = br.ReadUInt32();
                if (thisSignature != LocalFileHeaderSignature)
                {
                    return ZipReturn.ZipLocalFileHeaderError;
                }

                br.ReadUInt16(); // version needed to extract
                GeneralPurposeBitFlag = br.ReadUInt16();
                if ((GeneralPurposeBitFlag & 8) == 8)
                {
                    return ZipReturn.ZipCannotFastOpen;
                }

                _compressionMethod = br.ReadUInt16();

                ushort lastModFileTime = br.ReadUInt16();
                ushort lastModFileDate = br.ReadUInt16();
                HeaderLastModified = CompressUtils.UtcTicksFromDosDateTime(lastModFileDate, lastModFileTime);

                CRC = ReadCRC(br);
                _compressedSize = br.ReadUInt32();
                UncompressedSize = br.ReadUInt32();

                ushort fileNameLength = br.ReadUInt16();
                ushort extraFieldLength = br.ReadUInt16();

                byte[] bFileName = br.ReadBytes(fileNameLength);

                Filename = (GeneralPurposeBitFlag & (1 << 11)) == 0
                    ? CompressUtils.GetString(bFileName)
                    : Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

                SetStatus(LocalFileStatus.Zip64, false);
                if (extraFieldLength > 0)
                {
                    byte[] extraField = br.ReadBytes(extraFieldLength);

                    ulong LocalHeader = 0;
                    ZipReturn zr = ZipExtraField.ReadExtraField(extraField, bFileName, this, ref _compressedSize, ref LocalHeader, false);
                    if (zr != ZipReturn.ZipGood)
                        return zr;
                }

                if (CompressUtils.IsCodePage437(Filename) != ((GeneralPurposeBitFlag & (1 << 11)) == 0))
                    SetStatus(LocalFileStatus.TrrntZip, false);

                _dataLocation = (ulong)zipFs.Position;

                return ZipReturn.ZipGood;
            }
            catch
            {
                return ZipReturn.ZipLocalFileHeaderError;
            }
        }


        private void LocalFileHeaderWrite(Stream zipFs)
        {
            using BinaryWriter bw = new(zipFs, Encoding.UTF8, true);
            ZipExtraFieldWrite zefw = new();
            bool zip64 = zefw.Zip64(UncompressedSize, _compressedSize, RelativeOffsetOfLocalHeader, false,
                out uint headerUnCompressedSize, out uint headerCompressedSize,
                out uint headerRelativeOffsetOfLocalHeader);
            _extraField = zefw.ExtraField;

            SetStatus(LocalFileStatus.Zip64, zip64);

            byte[] bFileName;
            if (CompressUtils.IsCodePage437(Filename))
            {
                bFileName = CompressUtils.GetBytes(Filename);
            }
            else
            {
                GeneralPurposeBitFlag |= 1 << 11;
                bFileName = Encoding.UTF8.GetBytes(Filename);
            }

            ushort versionNeededToExtract = (ushort)(GetStatus(LocalFileStatus.Zip64) ? 45 : 20);

            RelativeOffsetOfLocalHeader = (ulong)zipFs.Position;
            bw.Write(LocalFileHeaderSignature);
            bw.Write(versionNeededToExtract);
            bw.Write(GeneralPurposeBitFlag);
            bw.Write(_compressionMethod);

            CompressUtils.UtcTicksToDosDateTime(HeaderLastModified, out ushort lastModFileDate, out ushort lastModFileTime);
            bw.Write(lastModFileTime);
            bw.Write(lastModFileDate);

            // these 3 values will be set correctly after the file data has been written
            bw.Write(0xffffffff); // crc
            bw.Write(0xffffffff); // CompressedSize 32bit
            bw.Write(0xffffffff); // UncompressedSie 32bit

            ushort fileNameLength = (ushort)bFileName.Length;
            bw.Write(fileNameLength);

            ushort extraFieldLength = (ushort)_extraField.Length;
            bw.Write(extraFieldLength);

            bw.Write(bFileName, 0, fileNameLength);

            _extraLocation = (ulong)zipFs.Position;
            bw.Write(_extraField);
        }

        private void LocalFileHeaderPostCompressWrite(Stream zipFs)
        {
            // after data is written to the zip, go back and finish up the header.

            // there is a rare case where you can have a file that is very close up to the 32 bit size limit in it uncompressed size,
            // and when it compresses it get just a little bigger, and bumps over the 32 bit size limit on its compressed size.
            // so you get uncompressed size is < 0xffffffff so we did not think it needed a zip64 header.
            // but compressed size is >= 0xffffffff so we do need to zip64 header, so we have to go back and move the compressed data
            // down the file to make room in the file header for the zip64 extra data.

            ZipExtraFieldWrite zefw = new();
            bool postZip64 = zefw.Zip64(UncompressedSize, _compressedSize, RelativeOffsetOfLocalHeader, false,
                out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader);
            byte[] postExtraField = zefw.ExtraField;

            if (postZip64 != GetStatus(LocalFileStatus.Zip64))
            {
                SetStatus(LocalFileStatus.Zip64);
                FixFileForZip64(zipFs, _extraField.Length, postExtraField.Length);
            }

            _extraField = postExtraField;
            long posNow = zipFs.Position;

            zipFs.Seek((long)RelativeOffsetOfLocalHeader + 14, SeekOrigin.Begin);
            using (BinaryWriter bw = new(zipFs, Encoding.UTF8, true))
            {
                WriteCRC(bw, CRC);
                bw.Write(headerCompressedSize);
                bw.Write(headerUnCompressedSize);

                if (_extraField.Length > 0)
                {
                    zipFs.Seek((long)_extraLocation, SeekOrigin.Begin);
                    bw.Write(_extraField);
                }
            }

            zipFs.Seek(posNow, SeekOrigin.Begin);
        }


        internal void LocalFileHeaderFake(ulong filePosition, ulong uncompressedSize, ulong compressedSize, byte[] crc32, MemoryStream ms)
        {
            using BinaryWriter bw = new(ms, Encoding.UTF8, true);
            RelativeOffsetOfLocalHeader = filePosition;
            SetStatus(LocalFileStatus.TrrntZip);
            UncompressedSize = uncompressedSize;
            _compressedSize = compressedSize;
            CRC = crc32;

            ZipExtraFieldWrite zefw = new();
            SetStatus(LocalFileStatus.Zip64,
            zefw.Zip64(UncompressedSize, _compressedSize, RelativeOffsetOfLocalHeader, false,
                out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader)
            );
            _extraField = zefw.ExtraField;

            byte[] bFileName;
            if (CompressUtils.IsCodePage437(Filename))
            {
                bFileName = CompressUtils.GetBytes(Filename);
            }
            else
            {
                GeneralPurposeBitFlag |= 1 << 11;
                bFileName = Encoding.UTF8.GetBytes(Filename);
            }

            ushort versionNeededToExtract = (ushort)(GetStatus(LocalFileStatus.Zip64) ? 45 : 20);

            bw.Write(LocalFileHeaderSignature);
            bw.Write(versionNeededToExtract);
            bw.Write(GeneralPurposeBitFlag);
            bw.Write(_compressionMethod);

            CompressUtils.UtcTicksToDosDateTime(HeaderLastModified, out ushort lastModFileDate, out ushort lastModFileTime);
            bw.Write(lastModFileTime);
            bw.Write(lastModFileDate);

            WriteCRC(bw, CRC);
            bw.Write(headerCompressedSize);
            bw.Write(headerUnCompressedSize);

            ushort fileNameLength = (ushort)bFileName.Length;
            bw.Write(fileNameLength);

            ushort extraFieldLength = (ushort)_extraField.Length;
            bw.Write(extraFieldLength);

            bw.Write(bFileName, 0, fileNameLength);

            bw.Write(_extraField);
        }
        internal ZipReturn LocalFileOpenReadStream(Stream zipFs, bool raw, out Stream readStream, out ulong streamSize, out ushort compressionMethod)
        {
            streamSize = 0;
            compressionMethod = _compressionMethod;

            readStream = null;
            zipFs.Seek((long)_dataLocation, SeekOrigin.Begin);

            switch (_compressionMethod)
            {
                case 0:
                    readStream = zipFs;
                    streamSize = _compressedSize; // same as UncompressedSize
                    break;

                case 8:
                    if (raw)
                    {
                        readStream = zipFs;
                        streamSize = _compressedSize;
                    }
                    else
                    {
                        //readStream = new ZlibBaseStream(zipFs, CompressionMode.Decompress, CompressionLevel.Default, ZlibStreamFlavor.DEFLATE, true);
                        readStream = new System.IO.Compression.DeflateStream(zipFs, System.IO.Compression.CompressionMode.Decompress, true);
                        streamSize = UncompressedSize;
                    }

                    break;
                case 9:
                    readStream = new Deflate64Stream(zipFs, System.IO.Compression.CompressionMode.Decompress);
                    streamSize = UncompressedSize;
                    break;

                //case 10:
                //    readStream = new BlastStream(zipFs);
                //    streamSize = UncompressedSize;
                //    break;

                case 12:
                    readStream = new CBZip2InputStream(zipFs, false);
                    streamSize = UncompressedSize;
                    break;

                case 14:
                    {
                        zipFs.ReadByte(); // Major version
                        zipFs.ReadByte(); // Minor version
                        int headerSize = zipFs.ReadByte() + (zipFs.ReadByte() << 8);
                        byte[] header = new byte[headerSize];
                        zipFs.Read(header, 0, headerSize);
                        readStream = new LzmaStream(header, zipFs);
                        streamSize = UncompressedSize;
                        break;
                    }

                case 20:
                case 93:
                    readStream = new ZstdSharp.DecompressionStream(zipFs);
                    streamSize = UncompressedSize;
                    break;

                case 98:
                    {
                        int headerSize = 2;
                        byte[] header = new byte[headerSize];
                        zipFs.Read(header, 0, headerSize);
                        readStream = new PpmdStream(new PpmdProperties(header), zipFs, false);
                        streamSize = UncompressedSize;
                        break;
                    }
            }

            return readStream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
        }

        internal ZipReturn LocalFileOpenWriteStream(Stream zipFs, bool raw, ulong uncompressedSize, ushort compressionMethod, out Stream writeStream)
        {
            UncompressedSize = uncompressedSize;
            _compressedSize = 0;
            RelativeOffsetOfLocalHeader = 0;
            _compressionMethod = compressionMethod;

            LocalFileHeaderWrite(zipFs);
            _dataLocation = (ulong)zipFs.Position;

            writeStream = null;
            if (raw)
            {
                writeStream = zipFs;
            }
            else
            {
                if (compressionMethod == 0)
                {
                    writeStream = zipFs;
                }
                else if (compressionMethod == 93)
                {
                    writeStream = new ZstdSharp.CompressionStream(zipFs, 19);
                }
                else if (compressionMethod == 8)
                {
                    writeStream = new ZlibBaseStream(zipFs, CompressionMode.Compress, CompressionLevel.BestCompression, ZlibStreamFlavor.DEFLATE, true);
                }
            }

            return writeStream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
        }

        internal ZipReturn LocalFileCloseWriteStream(Stream zipFs, byte[] crc32)
        {

            _compressedSize = (ulong)zipFs.Position - _dataLocation;

            if (_compressedSize == 0 && UncompressedSize == 0)
            {
                LocalFileAddZeroLengthFile(zipFs);
                _compressedSize = (ulong)zipFs.Position - _dataLocation;
            }
            else if (_compressedSize == 0 && UncompressedSize != 0)
            {
                return ZipReturn.ZipErrorWritingToOutputStream;
            }

            CRC = crc32;
            LocalFileHeaderPostCompressWrite(zipFs);

            return ZipReturn.ZipGood;
        }

        private void FixFileForZip64(Stream zipFs, int oldExtraFieldLength, int newExtraFieldLength)
        {
            long posNow = zipFs.Position;
            using (BinaryWriter bw = new(zipFs, Encoding.UTF8, true))
            {
                zipFs.Seek((long)RelativeOffsetOfLocalHeader + 4, SeekOrigin.Begin);
                ushort versionNeededToExtract = 45;
                bw.Write(versionNeededToExtract);

                zipFs.Seek((long)RelativeOffsetOfLocalHeader + 28, SeekOrigin.Begin);
                bw.Write((ushort)newExtraFieldLength);
            }

            int expandBy = newExtraFieldLength - oldExtraFieldLength;
            ExpandFile(zipFs, (long)_extraLocation, posNow, expandBy);
            zipFs.Position = posNow + expandBy;
        }

        private static void ExpandFile(Stream stream, long offset, long length, int extraBytes)
        {
            const int bufferSize = 40960;
            byte[] buffer = new byte[bufferSize];
            // Expand file
            long pos = length;
            while (pos > offset)
            {
                int toRead = pos - bufferSize >= offset ? bufferSize : (int)(pos - offset);
                pos -= toRead;
                stream.Position = pos;
                stream.Read(buffer, 0, toRead);
                stream.Position = pos + extraBytes;
                stream.Write(buffer, 0, toRead);
            }
        }

        internal static void LocalFileAddZeroLengthFile(Stream zipFs)
        {
            zipFs.WriteByte(03);
            zipFs.WriteByte(00);
        }

        private static byte[] ReadCRC(BinaryReader br)
        {
            byte[] tCRC = new byte[4];
            tCRC[3] = br.ReadByte();
            tCRC[2] = br.ReadByte();
            tCRC[1] = br.ReadByte();
            tCRC[0] = br.ReadByte();
            return tCRC;
        }

        private static void WriteCRC(BinaryWriter bw, byte[] CRC)
        {
            bw.Write(CRC[3]);
            bw.Write(CRC[2]);
            bw.Write(CRC[1]);
            bw.Write(CRC[0]);

        }

    }

}
