using System.IO;

namespace Compress.Support.Compression.zStd
{
    // C# version of zstd
    internal class zStdSharp : ZstdSharp.DecompressionStream
    {
        long pos = 0;
        public zStdSharp(Stream stream, int bufferSize = 0) : base(stream, bufferSize)
        {
            pos = 0;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            pos += read;
            return read;
        }

        public override bool CanSeek => true;

        public override long Position { get => pos; set => base.Position = value; }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long readLen;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        if (offset < pos)
                        {
                            // error connot go backwards
                            return -1;
                        }
                        readLen = offset - pos;
                        break;
                    }

                case SeekOrigin.Current:
                    {
                        readLen = offset;
                        break;
                    }
                default:
                    {
                        // unknown origin
                        return -1;
                    }
            }

            byte[] buffer = new byte[4096];
            while(readLen>0)
            {
                int count = readLen > 4096 ? 4096 : (int)readLen;
                int read = Read(buffer, 0, count);
                readLen -= read;
            }
            return pos;
        }
    }
}
