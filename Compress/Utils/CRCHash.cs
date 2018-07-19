using System.Security.Cryptography;

namespace Compress.Utils
{
    public class CRCHash : HashAlgorithm
    {
        private readonly CRC _crc32 = new CRC();

        public override void Initialize()
        {
            _crc32.Reset();
        }
        protected override void HashCore(byte[] buffer, int start, int length)
        {
            _crc32.SlurpBlock(buffer, start, length);
        }
        protected override byte[] HashFinal()
        {
            return _crc32.Crc32ResultB;
        }
    }
}
