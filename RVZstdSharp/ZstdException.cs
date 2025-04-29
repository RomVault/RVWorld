using System;
using RVZstdSharp.Unsafe;

namespace RVZstdSharp
{
    public class ZstdException : Exception
    {
        public ZstdException(ZSTD_ErrorCode code, string message) : base(message)
            => Code = code;

        public ZSTD_ErrorCode Code { get; }
    }
}
