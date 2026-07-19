using System;
using System.Xml;
using DATReader.DatStore;
using RVUtils;

namespace DATReader.Utils
{
    public static class VarFix
    {
        private const string ValidHexChar = "0123456789abcdef";

        public static bool StringYesNo(string b)
        {
            return string.Equals(b, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(b, "true", StringComparison.OrdinalIgnoreCase);
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
                if (n.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
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
            if (diskName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
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

            checksum = checksum.Trim();
            int checksumStart = checksum.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            int checksumLength = checksum.Length - checksumStart;
            if (checksumLength == 0)
            {
                return null;
            }

            if (checksumLength == 1 && checksum[checksumStart] == '-')
            {
                return null;
            }

            int paddedLength = Math.Max(checksumLength, length);
            int padding = paddedLength - checksumLength;
            int retL = paddedLength / 2;
            byte[] retB = new byte[retL];

            for (int i = 0; i < retL; i++)
            {
                int high = HexValue(GetPaddedChar(checksum, checksumStart, padding, i * 2));
                int low = HexValue(GetPaddedChar(checksum, checksumStart, padding, i * 2 + 1));
                if (high < 0 || low < 0)
                    return null;
                retB[i] = (byte)((high << 4) | low);
            }

            return retB;
        }

        private static char GetPaddedChar(string checksum, int checksumStart, int padding, int index)
        {
            return index < padding ? '0' : checksum[checksumStart + index - padding];
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9')
                return value - '0';
            if (value >= 'a' && value <= 'f')
                return value - 'a' + 10;
            if (value >= 'A' && value <= 'F')
                return value - 'A' + 10;
            return -1;
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
            return b.ToHexString();
        }


    }
}
