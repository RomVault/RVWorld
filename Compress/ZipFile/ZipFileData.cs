using System;
using System.IO;
using System.Text;
using CodePage;
using Compress.Support.Compression.BZip2;
using Compress.Support.Compression.Deflate;
using Compress.Support.Compression.Deflate64;
using Compress.Support.Compression.Explode;
using Compress.Support.Compression.LZMA;
using Compress.Support.Compression.PPmd;
using Compress.Support.Compression.Reduce;
using Compress.Support.Compression.Shrink;
using Compress.Support.Compression.zStd;

namespace Compress.ZipFile
{
    [Flags]
    public enum LocalFileStatus
    {
        Nothing = 0x00000,
        HeadersMismatch = 0x00004,
        FilenameMisMatch = 0x00010,
        DirectoryLengthError = 0x00020,
        DateTimeMisMatch = 0x00040,

        UnknownDataSource = 0x00080,
    }


    public class ZipFileData : FileHeader
    {
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;


        public ushort VersionMadeBy { get; internal set; }
        public ushort VersionNeededToExtract { get; internal set; }

        public ushort CompressionMethod { get; internal set; }

        public ulong CompressedSize { get; internal set; }
        internal ulong RelativeOffsetOfLocalHeader; // only in central directory

        internal byte[] bFileName;
        private ulong _extraLocation;
        internal byte[] bExtraField;
        internal ulong DataLocation;
        internal byte[] bFileComment;

        public bool IsZip64 { get; internal set; }
        public bool ExtraDataFound { get; internal set; }


        private LocalFileStatus _status = LocalFileStatus.Nothing;

        internal void ClearStatus()
        {
            _status = LocalFileStatus.Nothing;
        }

        internal void SetStatus(LocalFileStatus lfs, bool set = true)
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


        internal ushort GeneralPurposeBitFlag { get; private set; }

        public override ulong? LocalHead => (GeneralPurposeBitFlag & 8) == 0 ? (ulong?)RelativeOffsetOfLocalHeader : null;

        internal ZipFileData()
        {
        }

        internal ZipFileData(string filename, long modTime = 0)
        {
            IsZip64 = false;
            ExtraDataFound = false;
            GeneralPurposeBitFlag = 2; // Maximum Compression Deflating
            CompressionMethod = 8; // Compression Method Deflate
            HeaderLastModified = modTime;
            Filename = filename;
        }


        internal static ZipReturn CentralDirectoryRead(Stream zipFs, ulong offset, out ZipFileData centralFile)
        {
            try
            {
                centralFile = new ZipFileData();
                using BinaryReader br = new(zipFs, Encoding.UTF8, true);
                uint thisSignature = br.ReadUInt32();
                if (thisSignature != CentralDirectoryHeaderSignature)
                    return ZipReturn.ZipCentralDirError;

                centralFile.VersionMadeBy = br.ReadUInt16(); // Version Made By
                centralFile.VersionNeededToExtract = br.ReadUInt16(); // Version Needed To Extract

                centralFile.GeneralPurposeBitFlag = br.ReadUInt16();
                centralFile.CompressionMethod = br.ReadUInt16();

                ushort lastModFileTime = br.ReadUInt16();
                ushort lastModFileDate = br.ReadUInt16();

                centralFile.HeaderLastModified = CompressUtils.CombineDosDateTime(lastModFileDate, lastModFileTime);

                centralFile.CRC = ReadCRC(br);

                centralFile.CompressedSize = br.ReadUInt32();
                centralFile.UncompressedSize = br.ReadUInt32();

                ushort fileNameLength = br.ReadUInt16();
                ushort extraFieldLength = br.ReadUInt16();
                ushort fileCommentLength = br.ReadUInt16();

                br.ReadUInt16(); // diskNumberStart
                br.ReadUInt16(); // internalFileAttributes
                br.ReadUInt32(); // externalFileAttributes

                centralFile.RelativeOffsetOfLocalHeader = br.ReadUInt32();


                centralFile.bFileName = br.ReadBytes(fileNameLength);
                centralFile.Filename = (centralFile.GeneralPurposeBitFlag & (1 << 11)) == 0
                    ? CodePage437.GetString(centralFile.bFileName)
                    : Encoding.UTF8.GetString(centralFile.bFileName, 0, fileNameLength);

                centralFile.IsZip64 = false;
                centralFile.ExtraDataFound = false;
                if (extraFieldLength > 0)
                {
                    centralFile.bExtraField = br.ReadBytes(extraFieldLength);
                    ZipReturn zr = ZipExtraFieldRead.ExtraFieldRead(centralFile, true);
                    if (zr != ZipReturn.ZipGood)
                        return zr;
                }

                centralFile.RelativeOffsetOfLocalHeader += offset;

                if (fileCommentLength > 0)
                {
                    centralFile.bFileComment = br.ReadBytes(fileCommentLength);
                }

                if (centralFile.Filename.Length > 0)
                {
                    char lastChar = centralFile.Filename[centralFile.Filename.Length - 1];
                    centralFile.IsDirectory = (lastChar == '/' || lastChar == '\\');
                }


                return ZipReturn.ZipGood;
            }
            catch
            {
                centralFile = null;
                return ZipReturn.ZipCentralDirError;
            }
        }

        internal void CentralDirectoryWrite(Stream crcStream)
        {
            using BinaryWriter bw = new(crcStream, Encoding.UTF8, true);

            ZipExtraFieldWrite zefw = new();
            IsZip64 = zefw.Zip64(UncompressedSize, CompressedSize, RelativeOffsetOfLocalHeader, true,
                            out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader);
            bExtraField = zefw.ExtraField;

            ushort extraFieldLength = (ushort)bExtraField.Length;

            if (!CodePage437.GetBytes(Filename, out byte[] bFileName))
            {
                GeneralPurposeBitFlag |= 1 << 11;
                bFileName = Encoding.UTF8.GetBytes(Filename);
            }

            ushort fileNameLength = (ushort)bFileName.Length;

            ushort versionNeededToExtract = (ushort)(CompressionMethod == 93 ? 63 : (IsZip64 ? 45 : 20));

            CompressUtils.UtcTicksToDosDateTime(HeaderLastModified, out ushort lastModFileDate, out ushort lastModFileTime);

            bw.Write(CentralDirectoryHeaderSignature);
            bw.Write((ushort)0);
            bw.Write(versionNeededToExtract);
            bw.Write(GeneralPurposeBitFlag);
            bw.Write(CompressionMethod);
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
            bw.Write(bExtraField, 0, extraFieldLength);
            // No File Comment
        }


        internal static ZipReturn LocalFileHeaderRead(Stream zipFs, ZipFileData CentralFile, out ZipFileData localFile)
        {
            try
            {
                localFile = new ZipFileData();
                using BinaryReader br = new(zipFs, Encoding.UTF8, true);
                localFile.RelativeOffsetOfLocalHeader = CentralFile.RelativeOffsetOfLocalHeader;

                if (zipFs.Position != (long)localFile.RelativeOffsetOfLocalHeader)
                    zipFs.Position = (long)localFile.RelativeOffsetOfLocalHeader;
                uint thisSignature = br.ReadUInt32();
                if (thisSignature != LocalFileHeaderSignature)
                    return ZipReturn.ZipLocalFileHeaderError;

                localFile.VersionNeededToExtract = br.ReadUInt16(); // version needed to extract
                localFile.GeneralPurposeBitFlag = br.ReadUInt16();

                localFile.CompressionMethod = br.ReadUInt16();

                ushort lastModFileTime = br.ReadUInt16();
                ushort lastModFileDate = br.ReadUInt16();

                localFile.HeaderLastModified = CompressUtils.CombineDosDateTime(lastModFileDate, lastModFileTime);

                localFile.CRC = ReadCRC(br);
                localFile.CompressedSize = br.ReadUInt32();
                localFile.UncompressedSize = br.ReadUInt32();

                ushort fileNameLength = br.ReadUInt16();
                ushort extraFieldLength = br.ReadUInt16();

                localFile.bFileName = br.ReadBytes(fileNameLength);
                localFile.Filename = (localFile.GeneralPurposeBitFlag & (1 << 11)) == 0
                    ? CodePage437.GetString(localFile.bFileName)
                    : Encoding.UTF8.GetString(localFile.bFileName, 0, fileNameLength);

                localFile.IsZip64 = false;
                localFile.ExtraDataFound = false;
                if (extraFieldLength > 0)
                {
                    localFile.bExtraField = br.ReadBytes(extraFieldLength);

                    ZipReturn zr = ZipExtraFieldRead.ExtraFieldRead(localFile, false);
                    if (zr != ZipReturn.ZipGood)
                        return zr;
                }

                localFile.DataLocation = (ulong)zipFs.Position;

                if ((localFile.GeneralPurposeBitFlag & 8) == 8)
                {
                    /*
                    Should be reading these values from the file after the compressed data.
                    But this part of the zip standard is so screwed up, it is a mess
                    and so instead we are just going to use the values from the central directory.
                      
                    zipFs.Position += (long)CentralFile.CompressedSize;

                    localFile.CRC = ReadCRC(br);
                    if (CompressUtils.ByteArrCompare(localFile.CRC, new byte[] { 0x08, 0x07, 0x4b, 0x50 }))
                    {
                        localFile.CRC = ReadCRC(br);
                    }

                    if (localFile.IsZip64)
                    {
                        localFile.CompressedSize = br.ReadUInt64();
                        localFile.UncompressedSize = br.ReadUInt64();
                    }
                    else
                    {
                        localFile.CompressedSize = br.ReadUInt32();
                        localFile.UncompressedSize = br.ReadUInt32();
                    }
                    */
                    localFile.CRC = CentralFile.CRC;
                    localFile.CompressedSize = CentralFile.CompressedSize;
                    localFile.UncompressedSize = CentralFile.UncompressedSize;
                }
                return ZipReturn.ZipGood;
            }
            catch
            {
                localFile = null;
                return ZipReturn.ZipLocalFileHeaderError;
            }
        }

        private void LocalFileHeaderWrite(Stream zipFs)
        {
            using BinaryWriter bw = new(zipFs, Encoding.UTF8, true);
            ZipExtraFieldWrite zefw = new();

            IsZip64 = zefw.Zip64(UncompressedSize, CompressedSize, RelativeOffsetOfLocalHeader, false,
                out uint headerUnCompressedSize, out uint headerCompressedSize,
                out uint headerRelativeOffsetOfLocalHeader);
            bExtraField = zefw.ExtraField;

            if (!CodePage437.GetBytes(Filename, out byte[] bFileName))
            {
                GeneralPurposeBitFlag |= 1 << 11;
                bFileName = Encoding.UTF8.GetBytes(Filename);
            }

            ushort versionNeededToExtract = (ushort)(CompressionMethod == 93 ? 63 : (IsZip64 ? 45 : 20));

            RelativeOffsetOfLocalHeader = (ulong)zipFs.Position;
            bw.Write(LocalFileHeaderSignature);
            bw.Write(versionNeededToExtract);
            bw.Write(GeneralPurposeBitFlag);
            bw.Write(CompressionMethod);

            CompressUtils.UtcTicksToDosDateTime(HeaderLastModified, out ushort lastModFileDate, out ushort lastModFileTime);
            bw.Write(lastModFileTime);
            bw.Write(lastModFileDate);

            // these 3 values will be set correctly after the file data has been written
            bw.Write(0xffffffff); // crc
            bw.Write(0xffffffff); // CompressedSize 32bit
            bw.Write(0xffffffff); // UncompressedSie 32bit

            ushort fileNameLength = (ushort)bFileName.Length;
            bw.Write(fileNameLength);

            ushort extraFieldLength = (ushort)bExtraField.Length;
            bw.Write(extraFieldLength);

            bw.Write(bFileName, 0, fileNameLength);

            _extraLocation = (ulong)zipFs.Position;
            bw.Write(bExtraField);
        }

        private void LocalFileHeaderPostWrite(Stream zipFs)
        {
            // after data is written to the zip, go back and finish up the header.

            // there is a rare case where you can have a file that is very close up to the 32 bit size limit in it uncompressed size,
            // and when it compresses it get just a little bigger, and bumps over the 32 bit size limit on its compressed size.
            // so you get uncompressed size is < 0xffffffff so we did not think it needed a zip64 header.
            // but compressed size is >= 0xffffffff so we do need to zip64 header, so we have to go back and move the compressed data
            // down the file to make room in the file header for the zip64 extra data.

            ZipExtraFieldWrite zefw = new();
            bool postZip64 = zefw.Zip64(UncompressedSize, CompressedSize, RelativeOffsetOfLocalHeader, false,
                out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader);
            byte[] postExtraField = zefw.ExtraField;

            if (postZip64 != IsZip64)
            {
                IsZip64 = true;
                FixFileForZip64(zipFs, bExtraField.Length, postExtraField.Length);
            }

            bExtraField = postExtraField;
            long posNow = zipFs.Position;

            zipFs.Seek((long)RelativeOffsetOfLocalHeader + 14, SeekOrigin.Begin);
            using (BinaryWriter bw = new(zipFs, Encoding.UTF8, true))
            {
                WriteCRC(bw, CRC);
                bw.Write(headerCompressedSize);
                bw.Write(headerUnCompressedSize);

                if (bExtraField.Length > 0)
                {
                    zipFs.Seek((long)_extraLocation, SeekOrigin.Begin);
                    bw.Write(bExtraField);
                }
            }

            zipFs.Seek(posNow, SeekOrigin.Begin);
        }


        internal void LocalFileHeaderFake(ulong filePosition, ulong uncompressedSize, ulong compressedSize, byte[] crc32, ushort compressionMethod, long headerLastModified, MemoryStream ms)
        {
            using BinaryWriter bw = new(ms, Encoding.UTF8, true);
            RelativeOffsetOfLocalHeader = filePosition;
            UncompressedSize = uncompressedSize;
            CompressedSize = compressedSize;
            CompressionMethod = compressionMethod;
            HeaderLastModified = headerLastModified;
            CRC = crc32;

            ZipExtraFieldWrite zefw = new();
            IsZip64 = zefw.Zip64(UncompressedSize, CompressedSize, RelativeOffsetOfLocalHeader, false,
                            out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader);
            bExtraField = zefw.ExtraField;

            if (!CodePage437.GetBytes(Filename, out byte[] bFileName))
            {
                GeneralPurposeBitFlag |= 1 << 11;
                bFileName = Encoding.UTF8.GetBytes(Filename);
            }

            ushort versionNeededToExtract = (ushort)(CompressionMethod == 93 ? 63 : (IsZip64 ? 45 : 20));

            bw.Write(LocalFileHeaderSignature);
            bw.Write(versionNeededToExtract);
            bw.Write(GeneralPurposeBitFlag);
            bw.Write(CompressionMethod);

            CompressUtils.UtcTicksToDosDateTime(HeaderLastModified, out ushort lastModFileDate, out ushort lastModFileTime);
            bw.Write(lastModFileTime);
            bw.Write(lastModFileDate);

            WriteCRC(bw, CRC);
            bw.Write(headerCompressedSize);
            bw.Write(headerUnCompressedSize);

            ushort fileNameLength = (ushort)bFileName.Length;
            bw.Write(fileNameLength);

            ushort extraFieldLength = (ushort)bExtraField.Length;
            bw.Write(extraFieldLength);

            bw.Write(bFileName, 0, fileNameLength);

            bw.Write(bExtraField);
        }

        internal ZipReturn LocalFileOpenReadStream(Stream zipFs, bool raw, out Stream readStream, out ulong streamSize, out ushort compressionMethod)
        {
            compressionMethod = CompressionMethod;
            if (zipFs.Position != (long)DataLocation)
                zipFs.Position = (long)DataLocation;
            if (raw)
            {
                readStream = zipFs;
                streamSize = CompressedSize;
                return readStream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
            }

            return OpenStream(zipFs, CompressionMethod, CompressedSize, UncompressedSize, GeneralPurposeBitFlag, out streamSize, out readStream);
        }

        public static ZipReturn OpenStream(Stream zipFs, int CompressionMethod, ulong CompressedSize, ulong UncompressedSize, ushort GeneralPurposeBitFlag, out ulong streamSize, out Stream readStream)
        {
            streamSize = 0;
            readStream = null;

            switch (CompressionMethod)
            {
                case 0:
                    readStream = zipFs;
                    streamSize = CompressedSize; // same as UncompressedSize
                    break;

                case 1:
                    readStream = new UnShrink(zipFs, CompressedSize, UncompressedSize, out ZipReturn result);
                    if (result != ZipReturn.ZipGood)
                    {
                        readStream.Close();
                        readStream.Dispose();
                        readStream = null;
                        streamSize = 0;
                        return result;
                    }
                    streamSize = UncompressedSize;
                    break;
                case 2:
                case 3:
                case 4:
                case 5:
                    readStream = new UnReduce(zipFs, CompressedSize, UncompressedSize, CompressionMethod - 1);
                    streamSize = UncompressedSize;
                    break;
                case 6:
                    readStream = new ExplodeStream(zipFs, CompressedSize, UncompressedSize, GeneralPurposeBitFlag);
                    streamSize = UncompressedSize;
                    break;

                case 8:

                    //readStream = new ZlibBaseStream(zipFs, CompressionMode.Decompress, CompressionLevel.Default, ZlibStreamFlavor.DEFLATE, true);
                    readStream = new DeflateStream(zipFs, System.IO.Compression.CompressionMode.Decompress, true);
                    streamSize = UncompressedSize;
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
                    readStream = new RVZStdSharp(zipFs);
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

        internal ZipReturn LocalFileOpenWriteStream(Stream zipFs, bool raw, ulong uncompressedSize, ushort compressionMethod, out Stream writeStream, int? threadCount)
        {
            UncompressedSize = uncompressedSize;
            CompressedSize = 0;
            RelativeOffsetOfLocalHeader = 0;
            CompressionMethod = compressionMethod;

            LocalFileHeaderWrite(zipFs);
            DataLocation = (ulong)zipFs.Position;

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
                    writeStream = new RVZstdSharp.CompressionStream(zipFs, 19);
                    ((RVZstdSharp.CompressionStream)writeStream).SetParameter(RVZstdSharp.Unsafe.ZSTD_cParameter.ZSTD_c_nbWorkers, CompressUtils.SetThreadCount(threadCount));
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
            CompressedSize = (ulong)zipFs.Position - DataLocation;

            if (CompressedSize == 0 && UncompressedSize == 0 && CompressionMethod == 8)
            {
                LocalFileAddZeroLengthFile(zipFs);
                CompressedSize = (ulong)zipFs.Position - DataLocation;
            }
            else if (CompressedSize == 0 && UncompressedSize != 0)
            {
                return ZipReturn.ZipErrorWritingToOutputStream;
            }

            CRC = crc32;
            LocalFileHeaderPostWrite(zipFs);

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
