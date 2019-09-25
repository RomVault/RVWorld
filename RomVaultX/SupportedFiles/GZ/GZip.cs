using System;
using System.IO;
using System.Text;
using Compress;
using Compress.ThreadReaders;
using Compress.ZipFile.ZLib;
using FileHeaderReader;
using RomVaultX.DB;
using Directory = RVIO.Directory;
using File = RVIO.File;
using FileStream = RVIO.FileStream;
using Path = RVIO.Path;

namespace RomVaultX.SupportedFiles.GZ
{
    public class GZip
    {
        private const int Buffersize = 1024 * 1024;
        private static byte[] Buffer0;
        private static byte[] Buffer1;

        private string _filename;
        public byte[] crc;
        public byte[] sha1Hash;
        public byte[] md5Hash;
        public HeaderFileType altType;
        public byte[] altcrc;
        public byte[] altsha1Hash;
        public byte[] altmd5Hash;

        public ulong uncompressedSize;
        public ulong? uncompressedAltSize;
        public ulong compressedSize;
        public long datapos;

        private Stream _zipFs;

        public GZip()
        {
        }

        public GZip(RvFile tFile)
        {
            altType = tFile.AltType;
            crc = tFile.CRC;
            md5Hash = tFile.MD5;
            sha1Hash = tFile.SHA1;
            uncompressedSize = tFile.Size;
            altcrc = tFile.AltCRC;
            altsha1Hash = tFile.AltSHA1;
            altmd5Hash = tFile.AltMD5;
            uncompressedAltSize = tFile.AltSize;
        }

        public ZipReturn ReadGZip(string filename, bool deepScan)
        {
            _filename = "";
            if (!File.Exists(filename))
            {
                return ZipReturn.ZipErrorFileNotFound;
            }
            _filename = filename;

            int errorCode = FileStream.OpenFileRead(filename, out _zipFs);
            if (errorCode != 0)
            {
                if (errorCode == 32)
                {
                    return ZipReturn.ZipFileLocked;
                }
                return ZipReturn.ZipErrorOpeningFile;
            }
            return ReadBody(deepScan);
        }

        public ZipReturn ReadGZip(Stream gZipStream, bool deepScan)
        {
            _zipFs = gZipStream;
            return ReadBody(deepScan);
        }

        private ZipReturn ReadBody(bool deepScan)
        {
            using (BinaryReader zipBr = new BinaryReader(_zipFs, Encoding.UTF8, true))
            {
                byte ID1 = zipBr.ReadByte();
                byte ID2 = zipBr.ReadByte();

                if ((ID1 != 0x1f) || (ID2 != 0x8b))
                {
                    _zipFs.Close();
                    return ZipReturn.ZipSignatureError;
                }

                byte CM = zipBr.ReadByte();
                if (CM != 8)
                {
                    _zipFs.Close();
                    return ZipReturn.ZipUnsupportedCompression;
                }

                byte FLG = zipBr.ReadByte();


                uint MTime = zipBr.ReadUInt32();
                byte XFL = zipBr.ReadByte();
                byte OS = zipBr.ReadByte();

                //if FLG.FEXTRA set
                if ((FLG & 0x4) == 0x4)
                {
                    int XLen = zipBr.ReadInt16();
                    byte[] bytes = zipBr.ReadBytes(XLen);

                    if (XLen == 28)
                    {
                        md5Hash = new byte[16];
                        Array.Copy(bytes, 0, md5Hash, 0, 16);
                        crc = new byte[4];
                        Array.Copy(bytes, 16, crc, 0, 4);
                        uncompressedSize = BitConverter.ToUInt64(bytes, 20);
                    }

                    if (XLen == 77)
                    {
                        md5Hash = new byte[16];
                        Array.Copy(bytes, 0, md5Hash, 0, 16);
                        crc = new byte[4];
                        Array.Copy(bytes, 16, crc, 0, 4);
                        uncompressedSize = BitConverter.ToUInt64(bytes, 20);
                        altType = (HeaderFileType)bytes[28];
                        altmd5Hash = new byte[16];
                        Array.Copy(bytes, 29, altmd5Hash, 0, 16);
                        altsha1Hash = new byte[20];
                        Array.Copy(bytes, 45, altsha1Hash, 0, 20);
                        altcrc = new byte[4];
                        Array.Copy(bytes, 65, altcrc, 0, 4);
                        uncompressedAltSize = BitConverter.ToUInt64(bytes, 69);
                    }
                }

                //if FLG.FNAME set
                if ((FLG & 0x8) == 0x8)
                {
                    int XLen = zipBr.ReadInt16();
                    byte[] bytes = zipBr.ReadBytes(XLen);
                }

                //if FLG.FComment set
                if ((FLG & 0x10) == 0x10)
                {
                    int XLen = zipBr.ReadInt16();
                    byte[] bytes = zipBr.ReadBytes(XLen);
                }

                //if FLG.FHCRC set
                if ((FLG & 0x2) == 0x2)
                {
                    uint crc16 = zipBr.ReadUInt16();
                }
            }

            compressedSize = (ulong)(_zipFs.Length - _zipFs.Position) - 8;

            datapos = _zipFs.Position;
            if (deepScan)
            {
                if (Buffer0 == null)
                {
                    Buffer0 = new byte[Buffersize];
                }

                if (Buffer1 == null)
                {
                    Buffer1 = new byte[Buffersize];
                }

                Stream ds = new ZlibBaseStream(_zipFs, CompressionMode.Decompress, CompressionLevel.Default, ZlibStreamFlavor.DEFLATE, true);

                ThreadLoadBuffer lbuffer = new ThreadLoadBuffer(ds);

                ThreadCRC crc32 = new ThreadCRC();
                ThreadMD5 md5 = new ThreadMD5();
                ThreadSHA1 sha1 = new ThreadSHA1();

                ulong uncompressedRead = 0;
                int bufferRead = ds.Read(Buffer0, 0, Buffersize);
                uncompressedRead += (ulong)bufferRead;
                bool whichBuffer = true;

                while (bufferRead > 0)
                {
                    // trigger the buffer loading worker
                    lbuffer.Trigger(whichBuffer ? Buffer1 : Buffer0, Buffersize);

                    byte[] buffer = whichBuffer ? Buffer0 : Buffer1;

                    // trigger the hashing workers
                    crc32.Trigger(buffer, bufferRead);
                    md5.Trigger(buffer, bufferRead);
                    sha1.Trigger(buffer, bufferRead);

                    lbuffer.Wait();
                    crc32.Wait();
                    md5.Wait();
                    sha1.Wait();

                    // setup next loop around
                    bufferRead = lbuffer.SizeRead;
                    uncompressedRead += (ulong)bufferRead;
                    whichBuffer = !whichBuffer;
                }

                // tell all the workers we are finished
                lbuffer.Finish();
                crc32.Finish();
                md5.Finish();
                sha1.Finish();

                // get the results
                byte[] testcrc = crc32.Hash;
                byte[] testmd5 = md5.Hash;
                byte[] testsha1 = sha1.Hash;

                // cleanup
                lbuffer.Dispose();
                crc32.Dispose();
                md5.Dispose();
                sha1.Dispose();

                ds.Close();
                ds.Dispose();

                if (uncompressedSize != 0)
                {
                    if (uncompressedSize != uncompressedRead)
                    {
                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                {
                    uncompressedSize = uncompressedRead;
                }

                if (crc != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (crc[i] == testcrc[i])
                        {
                            continue;
                        }

                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                {
                    crc = testcrc;
                }

                if (md5Hash != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (md5Hash[i] == testmd5[i])
                        {
                            continue;
                        }

                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                {
                    md5Hash = testmd5;
                }

                if (sha1Hash != null)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        if (sha1Hash[i] == testsha1[i])
                        {
                            continue;
                        }

                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                {
                    sha1Hash = testsha1;
                }

            }

            _zipFs.Position = _zipFs.Length - 8;
            byte[] gzcrc;
            uint gzLength;
            using (BinaryReader zipBr = new BinaryReader(_zipFs, Encoding.UTF8, true))
            {
                gzcrc = zipBr.ReadBytes(4);
                gzLength = zipBr.ReadUInt32();
            }

            if (crc != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (gzcrc[3 - i] == crc[i])
                    {
                        continue;
                    }

                    _zipFs.Close();
                    return ZipReturn.ZipDecodeError;
                }
            }
            else
            {
                crc = new[] { gzcrc[3], gzcrc[2], gzcrc[1], gzcrc[0] };
            }

            if (uncompressedSize != 0)
            {
                if (gzLength != (uncompressedSize & 0xffffffff))
                {
                    _zipFs.Close();
                    return ZipReturn.ZipDecodeError;
                }
            }

            return ZipReturn.ZipGood;
        }

        public ZipReturn GetStream(out Stream st)
        {
            _zipFs.Position = datapos;

            st = new ZlibBaseStream(_zipFs, CompressionMode.Decompress, CompressionLevel.Default, ZlibStreamFlavor.DEFLATE, true);

            return ZipReturn.ZipGood;
        }

        public ZipReturn GetRawStream(out Stream st)
        {
            st = null;
            if (!File.Exists(_filename))
            {
                return ZipReturn.ZipErrorFileNotFound;
            }

            int errorCode = FileStream.OpenFileRead(_filename, out st);
            if (errorCode != 0)
            {
                if (errorCode == 32)
                {
                    return ZipReturn.ZipFileLocked;
                }
                return ZipReturn.ZipErrorOpeningFile;
            }

            st.Position = datapos;

            return ZipReturn.ZipGood;
        }

        public void Close()
        {
            if (_zipFs == null)
            {
                return;
            }

            _zipFs.Close();
            _zipFs.Dispose();
            _zipFs = null;
        }

        public ZipReturn WriteGZip(string filename, Stream sInput, bool isCompressedStream)
        {
            CreateDirForFile(filename);

            Stream _zipFs;
            FileStream.OpenFileWrite(filename, out _zipFs);
            using (BinaryWriter zipBw = new BinaryWriter(_zipFs, Encoding.UTF8, true))
            {
                zipBw.Write((byte) 0x1f); // ID1 = 0x1f
                zipBw.Write((byte) 0x8b); // ID2 = 0x8b
                zipBw.Write((byte) 0x08); // CM  = 0x08
                zipBw.Write((byte) 0x04); // FLG = 0x04
                zipBw.Write((uint) 0); // MTime = 0
                zipBw.Write((byte) 0x00); // XFL = 0x00
                zipBw.Write((byte) 0xff); // OS  = 0x00

                // writing FEXTRA
                if (FileHeaderReader.FileHeaderReader.AltHeaderFile(altType))
                {
                    zipBw.Write((short) 77); // XLEN 16+4+8+1+16+20+4+8
                }
                else
                {
                    zipBw.Write((short) 28); // XLEN 16+4+8
                }

                zipBw.Write(md5Hash); // 16 bytes
                zipBw.Write(crc); // 4 bytes
                zipBw.Write(uncompressedSize); // 8 bytes

                if (FileHeaderReader.FileHeaderReader.AltHeaderFile(altType))
                {
                    zipBw.Write((byte) altType); // 1
                    zipBw.Write(altmd5Hash); // 16
                    zipBw.Write(altsha1Hash); // 20
                    zipBw.Write(altcrc); // 4
                    zipBw.Write((ulong) uncompressedAltSize); // 8
                }


                if (Buffer0 == null)
                {
                    Buffer0 = new byte[Buffersize];
                }

                ulong dataStartPos = (ulong) zipBw.BaseStream.Position;
                if (isCompressedStream)
                {
                    ulong sizetogo = compressedSize;
                    while (sizetogo > 0)
                    {
                        int sizenow = sizetogo > Buffersize ? Buffersize : (int) sizetogo;
                        sInput.Read(Buffer0, 0, sizenow);
                        _zipFs.Write(Buffer0, 0, sizenow);
                        sizetogo -= (ulong) sizenow;
                    }
                }
                else
                {
                    if (uncompressedSize == 0)
                    {
                        _zipFs.WriteByte(03);
                        _zipFs.WriteByte(00);
                    }
                    else
                    {

                        ulong sizetogo = uncompressedSize;
                        Stream writeStream =new ZlibBaseStream(_zipFs, CompressionMode.Compress,CompressionLevel.BestCompression, ZlibStreamFlavor.DEFLATE, true);
                        while (sizetogo > 0)
                        {
                            int sizenow = sizetogo > Buffersize ? Buffersize : (int) sizetogo;
                            sInput.Read(Buffer0, 0, sizenow);
                            writeStream.Write(Buffer0, 0, sizenow);
                            sizetogo -= (ulong) sizenow;
                        }

                        writeStream.Flush();
                        writeStream.Close();
                        writeStream.Dispose();
                    }
                }

                compressedSize = (ulong) zipBw.BaseStream.Position - dataStartPos;

                zipBw.Write(crc[3]);
                zipBw.Write(crc[2]);
                zipBw.Write(crc[1]);
                zipBw.Write(crc[0]);
                zipBw.Write((uint) uncompressedSize);
                zipBw.Flush();
                zipBw.Close();
            }

            _zipFs.Close();

            return ZipReturn.ZipGood;
        }

        private static void CreateDirForFile(string sFilename)
        {
            string strTemp = Path.GetDirectoryName(sFilename);

            if (string.IsNullOrEmpty(strTemp))
            {
                return;
            }

            if (Directory.Exists(strTemp))
            {
                return;
            }


            while ((strTemp.Length > 0) && !Directory.Exists(strTemp))
            {
                int pos = strTemp.LastIndexOf(Path.DirectorySeparatorChar);
                if (pos < 0)
                {
                    pos = 0;
                }
                strTemp = strTemp.Substring(0, pos);
            }

            while (sFilename.IndexOf(Path.DirectorySeparatorChar, strTemp.Length + 1) > 0)
            {
                strTemp = sFilename.Substring(0, sFilename.IndexOf(Path.DirectorySeparatorChar, strTemp.Length + 1));
                Directory.CreateDirectory(strTemp);
            }
        }
    }
}