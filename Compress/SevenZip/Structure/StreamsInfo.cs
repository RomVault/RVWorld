using System;
using System.IO;

namespace Compress.SevenZip.Structure
{
    public class StreamsInfo
    {
        public ulong PackPosition;
        public PackedStreamInfo[] PackedStreams;
        public Folder[] Folders;

        public void Read(BinaryReader br)
        {
            for (;;)
            {
                HeaderProperty hp = (HeaderProperty) br.ReadByte();
                switch (hp)
                {
                    case HeaderProperty.kPackInfo:
                        PackedStreamInfo.Read(br, out PackPosition, out PackedStreams);
                        continue;

                    case HeaderProperty.kUnPackInfo:
                        Folder.ReadUnPackInfo(br, out Folders);
                        continue;

                    case HeaderProperty.kSubStreamsInfo:
                        Folder.ReadSubStreamsInfo(br, ref Folders);
                        continue;

                    case HeaderProperty.kEnd:
                        return;

                    default:
                        throw new Exception(hp.ToString());
                }
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte) HeaderProperty.kMainStreamsInfo);
            PackedStreamInfo.Write(bw, PackPosition, PackedStreams);
            Folder.WriteUnPackInfo(bw, Folders);
            Folder.WriteSubStreamsInfo(bw, Folders);
            bw.Write((byte) HeaderProperty.kEnd);
        }
    }
}