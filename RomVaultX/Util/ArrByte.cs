using System;

namespace RomVaultX.Util
{
    public static class ArrByte
    {
        public static byte[] Copy(byte[] b)
        {
            if (b == null)
            {
                return null;
            }
            byte[] retB = new byte[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                retB[i] = b[i];
            }
            return retB;
        }

        public static bool bCompare(byte[] b1, byte[] b2)
        {
            if ((b1 == null) || (b2 == null))
            {
                return false;
            }

            if (b1.Length != b2.Length)
            {
                return false;
            }

            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] != b2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static int iCompare(byte[] b1, byte[] b2)
        {
            int b1Len = b1 == null ? 0 : b1.Length;
            int b2Len = b2 == null ? 0 : b2.Length;

            int p = 0;
            for (;;)
            {
                if (b1Len == p)
                {
                    return b2Len == p ? 0 : -1;
                }
                if (b2Len == p)
                {
                    return 1;
                }
                if (b1[p] < b2[p])
                {
                    return -1;
                }
                if (b1[p] > b2[p])
                {
                    return 1;
                }
                p++;
            }
        }

        public static string ToString(byte[] b)
        {
            return b == null ? "" : BitConverter.ToString(b).ToLower().Replace("-", "");
        }
    }
}