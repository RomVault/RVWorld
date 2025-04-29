using System.IO;

namespace Compress.Support.Compression.Deflate
{
    public class DeflateStream : System.IO.Compression.DeflateStream
    {
        public DeflateStream(Stream stream, System.IO.Compression.CompressionLevel compressionLevel) : base(stream, compressionLevel)
        {
        }

        public DeflateStream(Stream stream, System.IO.Compression.CompressionMode mode) : base(stream, mode)
        {
        }

        public DeflateStream(Stream stream, System.IO.Compression.CompressionLevel compressionLevel, bool leaveOpen) : base(stream, compressionLevel, leaveOpen)
        {
        }

        public DeflateStream(Stream stream, System.IO.Compression.CompressionMode mode, bool leaveOpen) : base(stream, mode, leaveOpen)
        {
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = base.Read(array, offset + totalRead, count - totalRead);
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }
            return totalRead;
        }
    }
}
