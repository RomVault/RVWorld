using System.IO;

namespace RVXCore.Util
{
    public static class StreamCopier
    {
        private const int bufferSize = 1024 * 128;
        private static byte[] buffer = null;

        public static void StreamCopy(Stream sIn, Stream sOut, ulong size)
        {
            if (buffer == null)
                buffer = new byte[bufferSize];

            ulong sizetogo = size;
            while (sizetogo > 0)
            {
                int sizenow = sizetogo > bufferSize ? bufferSize : (int)sizetogo;
                sIn.Read(buffer, 0, sizenow);
                sOut.Write(buffer, 0, sizenow);

                sizetogo -= (ulong)sizenow;
            }
        }
    }
}
