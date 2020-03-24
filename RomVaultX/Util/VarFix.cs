using System;
using System.Xml;
using FileHeaderReader;

namespace RomVaultX.Util
{
    public static class VarFix
    {
        private const string ValidHexChar = "0123456789abcdef";

        public static bool StringYesNo(string b)
        {
            return (b != null) && ((b.ToLower() == "yes") || (b.ToLower() == "true"));
        }

        public static ulong? ULong(XmlNode n)
        {
            return ULong(n == null ? "" : n.InnerText);
        }

        public static ulong? ULong(string n)
        {
            if (string.IsNullOrEmpty(n))
            {
                return null;
            }

            if (n == "-")
            {
                return null;
            }

            try
            {
                if ((n.Length >= 2) && (n.Substring(0, 2).ToLower() == "0x"))
                {
                    return Convert.ToUInt64(n.Substring(2), 16);
                }

                return Convert.ToUInt64(n);
            }
            catch
            {
                return null;
            }
        }

        public static string String(XmlNode n)
        {
            return String(n == null ? "" : n.InnerText);
        }

        public static string String(string n)
        {
            return n ?? "";
        }

        private static string CleanCheck(string crc, int length)
        {
            string retcrc = crc ?? "";
            retcrc = retcrc.ToLower().Trim();

            if ((retcrc.Length >= 2) && (retcrc.Substring(0, 2).ToLower() == "0x"))
            {
                retcrc = retcrc.Substring(2);
            }

            if (retcrc == "-")
            {
                retcrc = "00000000";
            }

            for (int i = 0; i < retcrc.Length; i++)
            {
                if (ValidHexChar.IndexOf(retcrc.Substring(i, 1), StringComparison.Ordinal) < 0)
                {
                    return "";
                }
            }


            retcrc = new string('0', length) + retcrc;
            retcrc = retcrc.Substring(retcrc.Length - length);

            return retcrc;
        }


        //CleanMD5SHA1 with a null or empty string will return null
        public static byte[] CleanMD5SHA1(XmlNode n, int length)
        {
            return CleanMD5SHA1(n == null ? null : n.InnerText, length);
        }

        public static byte[] CleanMD5SHA1(string checksum, int length)
        {
            if (string.IsNullOrEmpty(checksum))
            {
                return null;
            }

            checksum = checksum.ToLower().Trim();

            if (checksum.Length >= 2)
            {
                if (checksum.Substring(0, 2) == "0x")
                {
                    checksum = checksum.Substring(2);
                }
            }


            if (string.IsNullOrEmpty(checksum))
            {
                return null;
            }

            if (checksum == "-")
            {
                return null;
            }

            //if (checksum.Length % 2 == 1)
            //    checksum = "0" + checksum;

            //if (checksum.Length != length)
            //    return null;

            while (checksum.Length < length)
            {
                checksum = "0" + checksum;
            }

            int retL = checksum.Length / 2;
            byte[] retB = new byte[retL];

            for (int i = 0; i < retL; i++)
            {
                retB[i] = Convert.ToByte(checksum.Substring(i * 2, 2), 16);
            }

            return retB;
        }


        public static string CleanFullFileName(XmlNode n)
        {
            return CleanFullFileName(n == null ? "" : n.InnerText);
        }

        public static string CleanFullFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            string retName = name;
            retName = retName.TrimStart();
            retName = retName.TrimEnd('.', ' ');

            char[] charName = retName.ToCharArray();
            for (int i = 0; i < charName.Length; i++)
            {
                int c = charName[i];
                if ((c == ':') || (c == '*') || (c == '?') || (c == '<') || (c == '>') || (c == '|') || (c == '"') || (c < 32))
                {
                    charName[i] = '-';
                }
                else if (c == '\\')
                {
                    charName[i] = '/';
                }
            }
            return new string(charName);
        }

        public static string CleanFileName(XmlNode n)
        {
            return CleanFileName(n == null ? "" : n.InnerText);
        }

        public static string CleanFileName(string name, char crep = '-')
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            string retName = name;
            retName = retName.TrimStart();
            retName = retName.TrimEnd('.', ' ');

            char[] charName = retName.ToCharArray();
            for (int i = 0; i < charName.Length; i++)
            {
                int c = charName[i];
                if ((c == ':') || (c == '*') || (c == '?') || (c == '<') || (c == '>') || (c == '|') || (c == '\\') || (c == '/') || (c == '"') || (c < 32))
                {
                    charName[i] = crep;
                }
            }
            return new string(charName);
        }

        public static string ToLower(XmlNode n)
        {
            return ToLower(n == null ? "" : n.InnerText);
        }

        public static string ToLower(string name)
        {
            return name == null ? "" : name.ToLower();
        }


        public static string PCombine(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1))
            {
                return path2;
            }
            if (string.IsNullOrEmpty(path2))
            {
                return path1;
            }

            return path1 + "/" + path2;
        }

        public static string ToString(byte[] b)
        {
            return b == null ? "" : BitConverter.ToString(b).ToLower().Replace("-", "");
        }

        public static string ToString(byte b)
        {
            return ToString(new[] { b });
        }

        public static object ToDBString(byte[] b)
        {
            return b == null ? DBNull.Value : (object)BitConverter.ToString(b).ToLower().Replace("-", "");
        }

        public static ulong? FixLong(object v)
        {
            return v == DBNull.Value ? null : (ulong?)Convert.ToInt64(v);
        }

        public static HeaderFileType FixFileType(object v)
        {
            return v == DBNull.Value ? HeaderFileType.Nothing : (HeaderFileType)Convert.ToInt32(v);
        }

        public static int CompareName(string var1, string var2)
        {
            int retv = TrrntZipStringCompare(var1, var2);
            return retv;
        }

        private static int TrrntZipStringCompare(string string1, string string2)
        {
            char[] bytes1 = string1.ToCharArray();
            char[] bytes2 = string2.ToCharArray();

            int pos1 = 0;
            int pos2 = 0;

            for (; ; )
            {
                if (pos1 == bytes1.Length)
                {
                    return pos2 == bytes2.Length ? 0 : -1;
                }
                if (pos2 == bytes2.Length)
                {
                    return 1;
                }

                int byte1 = bytes1[pos1++];
                int byte2 = bytes2[pos2++];

                if ((byte1 >= 65) && (byte1 <= 90))
                {
                    byte1 += 0x20;
                }
                if ((byte2 >= 65) && (byte2 <= 90))
                {
                    byte2 += 0x20;
                }

                if (byte1 < byte2)
                {
                    return -1;
                }
                if (byte1 > byte2)
                {
                    return 1;
                }
            }
        }
    }
}