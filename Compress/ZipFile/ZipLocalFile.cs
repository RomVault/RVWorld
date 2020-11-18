using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Compress.Utils;
using Compress.ZipFile.ZLib;

namespace Compress.ZipFile
{
    internal partial class LocalFile
    {
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;

        private ushort _compressionMethod;

        private long _lastModFileTimeDate;
        private long? mTime;
        private long? cTime;
        private long? aTime;

        private string _localHeaderFilename;
        private ulong _compressedSize;
        private ulong _localHeaderCompressedSize;
        private ulong _localHeaderUncompressedSize;

        public ulong RelativeOffsetOfLocalHeader; // only in centeral directory

        private ulong _crc32Location;
        private ulong _extraLocation;
        private ulong _dataLocation;

        public LocalFile()
        {
        }

        public LocalFile(string filename, TimeStamps dateTime = null)
        {
            Zip64 = false;
            GeneralPurposeBitFlag = 2; // Maximum Compression Deflating
            _compressionMethod = 8; // Compression Method Deflate

            if (dateTime?.ModTime == null)
            {
                _lastModFileTimeDate = ZipUtils.trrntzipDateTime;
            }
            else
            {
                _lastModFileTimeDate = (long)dateTime.ModTime;
            }

            FileName = filename;
        }

        public string FileName { get; private set; }
        public ushort GeneralPurposeBitFlag { get; private set; }
        public byte[] CRC { get; private set; }
        public ulong UncompressedSize { get; private set; }

        public bool Zip64 { get; private set; }
        public bool TrrntZip { get; private set; }

        public long DateTime => mTime ?? _lastModFileTimeDate;

        public long? DateTimeCreate => cTime;
        public long? DateTimeAccess => aTime;


        public ulong LocalFilePos
        {
            get => RelativeOffsetOfLocalHeader;
            set => RelativeOffsetOfLocalHeader = value;
        }


        public ZipReturn CenteralDirectoryRead(Stream zipFs)
        {
            try
            {
                using (BinaryReader br = new BinaryReader(zipFs, Encoding.UTF8, true))
                {
                    uint thisSignature = br.ReadUInt32();
                    if (thisSignature != CentralDirectoryHeaderSignature)
                    {
                        return ZipReturn.ZipCentralDirError;
                    }

                    br.ReadUInt16(); // Version Made By

                    br.ReadUInt16(); // Version Needed To Extract

                    GeneralPurposeBitFlag = br.ReadUInt16();
                    _compressionMethod = br.ReadUInt16();
                    if (_compressionMethod != 8 && _compressionMethod != 0)
                    {
                        if (_compressionMethod != 6 && _compressionMethod != 5 && _compressionMethod != 1)
                        {
                            return ZipReturn.ZipUnsupportedCompression;
                        }
                        return ZipReturn.ZipUnsupportedCompression;
                    }

                    ushort lastModFileTime = br.ReadUInt16();
                    ushort lastModFileDate = br.ReadUInt16();
                    _lastModFileTimeDate = ZipUtils.SetDateTime(lastModFileDate, lastModFileTime);

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
                    FileName = (GeneralPurposeBitFlag & (1 << 11)) == 0
                        ? ZipUtils.GetString(bFileName)
                        : Encoding.UTF8.GetString(bFileName, 0, fileNameLength);
                    
                    if (extraFieldLength > 0)
                    {
                        byte[] extraField = br.ReadBytes(extraFieldLength);


                        ZipReturn zr = ZipExtraField.ReadLocalExtraField(extraField, bFileName, this, true);
                        if (zr != ZipReturn.ZipGood)
                            return zr;
                    }

                    if (ZipUtils.IsCodePage437(FileName) != ((GeneralPurposeBitFlag & (1 << 11)) == 0))
                        TrrntZip = false;

                    if (fileCommentLength > 0)
                    {
                        byte[] fileComment = br.ReadBytes(fileCommentLength);
                    }

                    return ZipReturn.ZipGood;
                }
            }
            catch
            {
                return ZipReturn.ZipCentralDirError;
            }
        }

        public void CenteralDirectoryWrite(Stream crcStream)
        {
            using (BinaryWriter bw = new BinaryWriter(crcStream, Encoding.UTF8, true))
            {
                const uint header = 0x2014B50;

                List<byte> extraField = new List<byte>();

                uint cdUncompressedSize;
                if (UncompressedSize >= 0xffffffff)
                {
                    Zip64 = true;
                    cdUncompressedSize = 0xffffffff;
                    extraField.AddRange(BitConverter.GetBytes(UncompressedSize));
                }
                else
                {
                    cdUncompressedSize = (uint)UncompressedSize;
                }

                uint cdCompressedSize;
                if (_compressedSize >= 0xffffffff)
                {
                    Zip64 = true;
                    cdCompressedSize = 0xffffffff;
                    extraField.AddRange(BitConverter.GetBytes(_compressedSize));
                }
                else
                {
                    cdCompressedSize = (uint)_compressedSize;
                }

                uint cdRelativeOffsetOfLocalHeader;
                if (RelativeOffsetOfLocalHeader >= 0xffffffff)
                {
                    Zip64 = true;
                    cdRelativeOffsetOfLocalHeader = 0xffffffff;
                    extraField.AddRange(BitConverter.GetBytes(RelativeOffsetOfLocalHeader));
                }
                else
                {
                    cdRelativeOffsetOfLocalHeader = (uint)RelativeOffsetOfLocalHeader;
                }


                if (extraField.Count > 0)
                {
                    ushort exfl = (ushort)extraField.Count;
                    extraField.InsertRange(0, BitConverter.GetBytes((ushort)0x0001));
                    extraField.InsertRange(2, BitConverter.GetBytes(exfl));
                }

                ushort extraFieldLength = (ushort)extraField.Count;

                byte[] bFileName;
                if (ZipUtils.IsCodePage437(FileName))
                {
                    bFileName = ZipUtils.GetBytes(FileName);
                }
                else
                {
                    GeneralPurposeBitFlag |= 1 << 11;
                    bFileName = Encoding.UTF8.GetBytes(FileName);
                }

                ushort fileNameLength = (ushort)bFileName.Length;

                ushort versionNeededToExtract = (ushort)(Zip64 ? 45 : 20);

                ZipUtils.SetDateTime(_lastModFileTimeDate, out ushort lastModFileDate, out ushort lastModFileTime);

                bw.Write(header);
                bw.Write((ushort)0);
                bw.Write(versionNeededToExtract);
                bw.Write(GeneralPurposeBitFlag);
                bw.Write(_compressionMethod);
                bw.Write(lastModFileTime);
                bw.Write(lastModFileDate);
                bw.Write(CRC[3]);
                bw.Write(CRC[2]);
                bw.Write(CRC[1]);
                bw.Write(CRC[0]);
                bw.Write(cdCompressedSize);
                bw.Write(cdUncompressedSize);
                bw.Write(fileNameLength);
                bw.Write(extraFieldLength);
                bw.Write((ushort)0); // file comment length
                bw.Write((ushort)0); // disk number start
                bw.Write((ushort)0); // internal file attributes
                bw.Write((uint)0); // external file attributes
                bw.Write(cdRelativeOffsetOfLocalHeader);

                bw.Write(bFileName, 0, fileNameLength);
                bw.Write(extraField.ToArray(), 0, extraFieldLength);
                // No File Comment
            }
        }
        public ZipReturn LocalFileHeaderRead(Stream zipFs)
        {
            try
            {
                using (BinaryReader br = new BinaryReader(zipFs, Encoding.UTF8, true))
                {

                    TrrntZip = true;

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
                        TrrntZip = false;
                    }

                    ushort tshort = br.ReadUInt16();
                    if (tshort != _compressionMethod)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    ushort lastModFileTime = br.ReadUInt16();
                    ushort lastModFileDate = br.ReadUInt16();

                    long tTime = ZipUtils.SetDateTime(lastModFileDate, lastModFileTime);
                    if (tTime != _lastModFileTimeDate)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    byte[] tCRC = ReadCRC(br);
                    _localHeaderCompressedSize = br.ReadUInt32();
                    _localHeaderUncompressedSize = br.ReadUInt32();

                    ushort fileNameLength = br.ReadUInt16();
                    ushort extraFieldLength = br.ReadUInt16();


                    byte[] bFileName = br.ReadBytes(fileNameLength);
                    _localHeaderFilename = (generalPurposeBitFlagLocal & (1 << 11)) == 0
                        ? ZipUtils.GetString(bFileName)
                        : Encoding.UTF8.GetString(bFileName, 0, fileNameLength);


                    Zip64 = false;
                    if (extraFieldLength > 0)
                    {
                        byte[] extraField = br.ReadBytes(extraFieldLength);

                        ZipReturn zr = ZipExtraField.ReadLocalExtraField(extraField, bFileName, this, false);
                        if (zr != ZipReturn.ZipGood)
                            return zr;
                    }

                    if (!ZipUtils.CompareString(FileName, _localHeaderFilename))
                    {
                        TrrntZip = false;
                        if (!ZipUtils.CompareStringSlash(FileName, _localHeaderFilename))
                        {
                            return ZipReturn.ZipLocalFileHeaderError;
                        }
                    }

                    _dataLocation = (ulong)zipFs.Position;

                    if ((GeneralPurposeBitFlag & 8) == 8)
                    {
                        zipFs.Position += (long)_compressedSize;

                        tCRC = ReadCRC(br);
                        if (!ZipUtils.ByteArrCompare(tCRC, new byte[] { 0x50, 0x4b, 0x07, 0x08 }))
                        {
                            tCRC = ReadCRC(br);
                        }
                        _localHeaderCompressedSize = br.ReadUInt32();
                        _localHeaderUncompressedSize = br.ReadUInt32();
                    }
                    
                    if (ZipUtils.IsCodePage437(FileName) != ((GeneralPurposeBitFlag & (1 << 11)) == 0))
                        TrrntZip = false;
                    
                    if (!ZipUtils.ByteArrCompare(tCRC, CRC))
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    if (_localHeaderCompressedSize != _compressedSize)
                    {
                        return ZipReturn.ZipLocalFileHeaderError;
                    }

                    if (_localHeaderUncompressedSize != UncompressedSize)
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

        public ZipReturn LocalFileHeaderReadQuick(Stream zipFs)
        {
            try
            {
                using (BinaryReader br = new BinaryReader(zipFs, Encoding.UTF8, true))
                {
                    TrrntZip = true;

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
                    _lastModFileTimeDate = ZipUtils.SetDateTime(lastModFileDate, lastModFileTime);

                    CRC = ReadCRC(br);
                    _localHeaderCompressedSize = br.ReadUInt32();
                    _localHeaderUncompressedSize = br.ReadUInt32();

                    ushort fileNameLength = br.ReadUInt16();
                    ushort extraFieldLength = br.ReadUInt16();

                    byte[] bFileName = br.ReadBytes(fileNameLength);

                    _localHeaderFilename = (GeneralPurposeBitFlag & (1 << 11)) == 0
                        ? ZipUtils.GetString(bFileName)
                        : Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

                    Zip64 = false;
                    if (extraFieldLength > 0)
                    {
                        byte[] extraField = br.ReadBytes(extraFieldLength);

                        ZipReturn zr = ZipExtraField.ReadLocalExtraField(extraField, bFileName, this, false);
                        if (zr != ZipReturn.ZipGood)
                            return zr;
                    }


                    _dataLocation = (ulong)zipFs.Position;

                    FileName = _localHeaderFilename;
                    _compressedSize = _localHeaderCompressedSize;
                    UncompressedSize = _localHeaderUncompressedSize;

                    if (ZipUtils.IsCodePage437(FileName) != ((GeneralPurposeBitFlag & (1 << 11)) == 0))
                        TrrntZip = false;

                    return ZipReturn.ZipGood;
                }
            }
            catch
            {
                return ZipReturn.ZipLocalFileHeaderError;
            }
        }


        private void LocalFileHeaderWrite(Stream zipFs)
        {
            using (BinaryWriter bw = new BinaryWriter(zipFs, Encoding.UTF8, true))
            {
                Zip64 = UncompressedSize >= 0xffffffff;

                byte[] bFileName;
                if (ZipUtils.IsCodePage437(FileName))
                {
                    bFileName = ZipUtils.GetBytes(FileName);
                }
                else
                {
                    GeneralPurposeBitFlag |= 1 << 11;
                    bFileName = Encoding.UTF8.GetBytes(FileName);
                }

                ushort versionNeededToExtract = (ushort)(Zip64 ? 45 : 20);

                RelativeOffsetOfLocalHeader = (ulong)zipFs.Position;
                const uint header = 0x4034B50;
                bw.Write(header);
                bw.Write(versionNeededToExtract);
                bw.Write(GeneralPurposeBitFlag);
                bw.Write(_compressionMethod);

                ZipUtils.SetDateTime(_lastModFileTimeDate, out ushort lastModFileDate, out ushort lastModFileTime);
                bw.Write(lastModFileTime);
                bw.Write(lastModFileDate);

                _crc32Location = (ulong)zipFs.Position;

                // these 3 values will be set correctly after the file data has been written
                bw.Write(0xffffffff);
                bw.Write(0xffffffff);
                bw.Write(0xffffffff);

                ushort fileNameLength = (ushort)bFileName.Length;
                bw.Write(fileNameLength);

                ushort extraFieldLength = (ushort)(Zip64 ? 20 : 0);
                bw.Write(extraFieldLength);

                bw.Write(bFileName, 0, fileNameLength);

                _extraLocation = (ulong)zipFs.Position;
                if (Zip64)
                    bw.Write(new byte[20], 0, extraFieldLength);
            }
        }

        public void LocalFileHeaderFake(ulong filePosition, ulong uncompressedSize, ulong compressedSize, byte[] crc32, MemoryStream ms)
        {
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                RelativeOffsetOfLocalHeader = filePosition;
                TrrntZip = true;
                UncompressedSize = uncompressedSize;
                _compressedSize = compressedSize;
                CRC = crc32;

                Zip64 = UncompressedSize >= 0xffffffff || _compressedSize >= 0xffffffff;

                byte[] bFileName;
                if (ZipUtils.IsCodePage437(FileName))
                {
                    bFileName = ZipUtils.GetBytes(FileName);
                }
                else
                {
                    GeneralPurposeBitFlag |= 1 << 11;
                    bFileName = Encoding.UTF8.GetBytes(FileName);
                }

                ushort versionNeededToExtract = (ushort)(Zip64 ? 45 : 20);

                const uint header = 0x4034B50;
                bw.Write(header);
                bw.Write(versionNeededToExtract);
                bw.Write(GeneralPurposeBitFlag);
                bw.Write(_compressionMethod);

                ZipUtils.SetDateTime(_lastModFileTimeDate, out ushort lastModFileDate, out ushort lastModFileTime);
                bw.Write(lastModFileTime);
                bw.Write(lastModFileDate);

                uint tCompressedSize;
                uint tUncompressedSize;
                if (Zip64)
                {
                    tCompressedSize = 0xffffffff;
                    tUncompressedSize = 0xffffffff;
                }
                else
                {
                    tCompressedSize = (uint)_compressedSize;
                    tUncompressedSize = (uint)UncompressedSize;
                }

                bw.Write(CRC[3]);
                bw.Write(CRC[2]);
                bw.Write(CRC[1]);
                bw.Write(CRC[0]);
                bw.Write(tCompressedSize);
                bw.Write(tUncompressedSize);

                ushort fileNameLength = (ushort)bFileName.Length;
                bw.Write(fileNameLength);

                ushort extraFieldLength = (ushort)(Zip64 ? 20 : 0);
                bw.Write(extraFieldLength);

                bw.Write(bFileName, 0, fileNameLength);

                if (Zip64)
                {
                    bw.Write((ushort)0x0001); // id
                    bw.Write((ushort)16); // data length
                    bw.Write(UncompressedSize);
                    bw.Write(_compressedSize);
                }
            }
        }
        public ZipReturn LocalFileOpenReadStream(Stream zipFs, bool raw, out Stream readStream, out ulong streamSize, out ushort compressionMethod)
        {
            streamSize = 0;
            compressionMethod = _compressionMethod;

            readStream = null;
            zipFs.Seek((long)_dataLocation, SeekOrigin.Begin);

            switch (_compressionMethod)
            {
                case 8:
                    if (raw)
                    {
                        readStream = zipFs;
                        streamSize = _compressedSize;
                    }
                    else
                    {
                        readStream=new System.IO.Compression.DeflateStream(zipFs,System.IO.Compression.CompressionMode.Decompress,true);
                        //readStream = new ZlibBaseStream(zipFs, CompressionMode.Decompress, CompressionLevel.Default, ZlibStreamFlavor.DEFLATE, true);
                        streamSize = UncompressedSize;
                    }
                    break;
                case 0:
                    readStream = zipFs;
                    streamSize = _compressedSize; // same as UncompressedSize
                    break;
            }

            return readStream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
        }

        public ZipReturn LocalFileOpenWriteStream(Stream zipFs, bool raw, bool trrntZip, ulong uncompressedSize, ushort compressionMethod, out Stream writeStream)
        {
            UncompressedSize = uncompressedSize;
            _compressionMethod = compressionMethod;

            LocalFileHeaderWrite(zipFs);
            _dataLocation = (ulong)zipFs.Position;

            if (raw)
            {
                writeStream = zipFs;
                TrrntZip = trrntZip;
            }
            else
            {
                if (compressionMethod == 0)
                {
                    writeStream = zipFs;
                    TrrntZip = false;
                }
                else
                {
                    writeStream = new ZlibBaseStream(zipFs, CompressionMode.Compress, CompressionLevel.BestCompression, ZlibStreamFlavor.DEFLATE, true);
                    TrrntZip = true;
                }
            }

            return writeStream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
        }

        public ZipReturn LocalFileCloseWriteStream(Stream zipFs, byte[] crc32)
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
            WriteCompressedSize(zipFs);

            return ZipReturn.ZipGood;
        }

        private void FixFileForZip64(Stream zipFs)
        {
            long posNow = zipFs.Position;
            using (BinaryWriter bw = new BinaryWriter(zipFs, Encoding.UTF8, true))
            {
                // _crc32Location - 10  needs set to 45  
                zipFs.Seek((long)_crc32Location - 10, SeekOrigin.Begin);
                ushort versionNeededToExtract = 45;
                bw.Write(versionNeededToExtract);

                zipFs.Seek((long)_crc32Location + 14, SeekOrigin.Begin);
                ushort extraFieldLength = 20;
                bw.Write(extraFieldLength);
            }
            ExpandFile(zipFs, (long)_extraLocation, posNow, 20);
            zipFs.Position = posNow + 20;
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

        private void WriteCompressedSize(Stream zipFs)
        {
            if (_compressedSize >= 0xffffffff && !Zip64)
            {
                Zip64 = true;
                FixFileForZip64(zipFs);
            }


            long posNow = zipFs.Position;
            zipFs.Seek((long)_crc32Location, SeekOrigin.Begin);
            using (BinaryWriter bw = new BinaryWriter(zipFs, Encoding.UTF8, true))
            {
                uint tCompressedSize;
                uint tUncompressedSize;
                if (Zip64)
                {
                    tCompressedSize = 0xffffffff;
                    tUncompressedSize = 0xffffffff;
                }
                else
                {
                    tCompressedSize = (uint)_compressedSize;
                    tUncompressedSize = (uint)UncompressedSize;
                }

                bw.Write(CRC[3]);
                bw.Write(CRC[2]);
                bw.Write(CRC[1]);
                bw.Write(CRC[0]);
                bw.Write(tCompressedSize);
                bw.Write(tUncompressedSize);


                // also need to write extradata
                if (Zip64)
                {
                    zipFs.Seek((long)_extraLocation, SeekOrigin.Begin);
                    bw.Write((ushort)0x0001); // id
                    bw.Write((ushort)16); // data length
                    bw.Write(UncompressedSize);
                    bw.Write(_compressedSize);
                }
            }

            zipFs.Seek(posNow, SeekOrigin.Begin);
        }

        public static void LocalFileAddZeroLengthFile(Stream zipFs)
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
    }

}
