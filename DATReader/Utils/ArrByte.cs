namespace DATReader.Utils
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
        public static bool nCompare(byte[] b1, byte[] b2)
        {
            if ((b1 == null) || (b2 == null))
            {
                return true;
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



        //https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa#24343727
        private static readonly uint[] Lookup32 = CreateLookup32();

        private static uint[] CreateLookup32()
        {
            uint[] result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2").ToLower();
                result[i] = s[0] + ((uint)s[1] << 16);
            }
            return result;
        }
        private static string ByteArrayToHexViaLookup32(byte[] bytes)
        {
            char[] result = new char[bytes.Length * 2];
            int c = 0;
            foreach (byte b in bytes)
            {
                uint val = Lookup32[b];
                result[c++] = (char)val;
                result[c++] = (char)(val >> 16);
            }
            return new string(result);
        }

        public static string ToHexNullString(this byte[] b)
        {
            return b == null ? null : ByteArrayToHexViaLookup32(b);
        }


        public static string ToHexString(this byte[] b)
        {
            return b == null ? "" : ByteArrayToHexViaLookup32(b);
        }
    }
}
