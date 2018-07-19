using System;
using System.IO;
using Compress.SevenZip.Compress.LZMA;

namespace Compress.SevenZip.Structure
{
    public class Header
    {
        public StreamsInfo StreamsInfo;
        public FileInfo FileInfo;

        public void Read(BinaryReader br)
        {
            for (;;)
            {
                HeaderProperty hp = (HeaderProperty) br.ReadByte();
                switch (hp)
                {
                    case HeaderProperty.kMainStreamsInfo:
                        StreamsInfo = new StreamsInfo();
                        StreamsInfo.Read(br);
                        break;

                    case HeaderProperty.kFilesInfo:
                        FileInfo = new FileInfo();
                        FileInfo.Read(br);
                        break;

                    case HeaderProperty.kEnd:
                        return;

                    default:
                        throw new Exception(hp.ToString());
                }
            }
        }


        private void Write(BinaryWriter bw)
        {
            bw.Write((byte) HeaderProperty.kHeader);
            StreamsInfo.Write(bw);
            FileInfo.Write(bw);
            bw.Write((byte) HeaderProperty.kEnd);
        }

        public void WriteHeader(BinaryWriter bw)
        {
            Write(bw);
        }

        public static ZipReturn ReadHeaderOrPackedHeader(Stream stream, long baseOffset, out Header header)
        {
            header = null;

            BinaryReader br = new BinaryReader(stream);

            HeaderProperty hp = (HeaderProperty) br.ReadByte();
            switch (hp)
            {
                case HeaderProperty.kEncodedHeader:
                {
                    StreamsInfo streamsInfo = new StreamsInfo();
                    streamsInfo.Read(br);

                    if (streamsInfo.Folders.Length > 1)
                    {
                        return ZipReturn.ZipUnsupportedCompression;
                    }

                    Folder firstFolder = streamsInfo.Folders[0];
                    if (firstFolder.Coders.Length > 1)
                    {
                        return ZipReturn.ZipUnsupportedCompression;
                    }

                    byte[] method = firstFolder.Coders[0].Method;
                    if (!((method.Length == 3) && (method[0] == 3) && (method[1] == 1) && (method[2] == 1))) // LZMA
                    {
                        return ZipReturn.ZipUnsupportedCompression;
                    }

                    stream.Seek(baseOffset + (long) streamsInfo.PackPosition, SeekOrigin.Begin);
                    using (LzmaStream decoder = new LzmaStream(firstFolder.Coders[0].Properties, stream))
                    {
                        ZipReturn zr = ReadHeaderOrPackedHeader(decoder, baseOffset, out header);
                        if (zr != ZipReturn.ZipGood)
                        {
                            return zr;
                        }
                    }
                    return ZipReturn.ZipGood;
                }
                case HeaderProperty.kHeader:
                {
                    header = new Header();
                    header.Read(br);
                    return ZipReturn.ZipGood;
                }
            }

            return ZipReturn.ZipCenteralDirError;
        }
    }
}