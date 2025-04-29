using System;
using System.Xml;
using DATReader.DatStore;

namespace DATReader.Utils
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
            return ULong(n?.InnerText ?? "");
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

                ulong res;
                if (UInt64.TryParse(n, out res))
                    return res;

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static string CleanCHD(XmlNode n)
        {
            return CleanCHD(n?.InnerText);
        }
        public static string CleanCHD(string n)
        {
            string diskName = n ?? "";
            if (diskName.ToLower().EndsWith(".chd"))
                diskName = diskName.Substring(0, diskName.Length - 4);
            return diskName;


            //if (diskName.Length < 4 || diskName.Substring(diskName.Length - 4).ToLower() != ".chd")
            //    diskName += ".chd";
            //return diskName;
        }

        public static string String(XmlNode n)
        {
            return n?.InnerText ?? "";
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
            return CleanMD5SHA1(n?.InnerText, length);
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

            try
            {
                for (int i = 0; i < retL; i++)
                {
                    retB[i] = Convert.ToByte(checksum.Substring(i * 2, 2), 16);
                }

                return retB;
            }
            catch
            {
                return null;
            }
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
                if ((c == ':') || (c == '*') || (c == '?') || (c == '<') || (c == '>') || (c == '|') || (c == '"') || (c == '\\') || (c == '/') || (c < 32))
                {
                    charName[i] = crep;
                }
            }
            return new string(charName);
        }

        public static string ToLower(XmlNode n)
        {
            return ToLower(n?.InnerText ?? "");
        }

        public static string ToLower(string name)
        {
            return name?.ToLower() ?? "";
        }


        public static string ToString(byte[] b)
        {
            return b == null ? "" : BitConverter.ToString(b).ToLower().Replace("-", "");
        }


    }
}