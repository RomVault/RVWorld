using System;
using System.Text;

namespace Compress.ZipFile
{
    public static class ZipUtils
    {
        // according to the zip documents, zip filesname are stored as MS-DOS Code Page 437.
        // (Unless the uncode flag is set, in which case they are stored as UTF-8.
        private static Encoding enc = Encoding.GetEncoding(437);

        public static string GetString(byte[] byteArr)
        {
            return enc.GetString(byteArr);
        }

        // to test if a filename can be stored as codepage 437 we take the filename string
        // convert it to bytes using the 437 code page, and then convert it back to a string
        // and we see if we lost characters as a result of the conversion there and back.
        internal static bool IsCodePage437(string s)
        {
            byte[] bOut = enc.GetBytes(s);
            string sOut = enc.GetString(bOut);

            return CompareString(s, sOut);
        }

        internal static byte[] GetBytes(string s)
        {
            return enc.GetBytes(s);
        }

        internal static bool CompareString(string s1, string s2)
        {
            char[] c1 = s1.ToCharArray();
            char[] c2 = s2.ToCharArray();

            if (c1.Length != c2.Length)
            {
                return false;
            }

            for (int i = 0; i < c1.Length; i++)
            {
                if (c1[i] != c2[i])
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool CompareStringSlash(string s1, string s2)
        {
            char[] c1 = s1.ToCharArray();
            char[] c2 = s2.ToCharArray();

            if (c1.Length != c2.Length)
            {
                return false;
            }

            for (int i = 0; i < c1.Length; i++)
            {
                if (c1[i] == '/') c1[i] = '\\';
                if (c2[i] == '/') c2[i] = '\\';
                if (c1[i] != c2[i])
                {
                    return false;
                }
            }
            return true;
        }


        internal static bool ByteArrCompare(byte[] b0, byte[] b1)
        {
            if ((b0 == null) || (b1 == null))
            {
                return false;
            }
            if (b0.Length != b1.Length)
            {
                return false;
            }

            for (int i = 0; i < b0.Length; i++)
            {
                if (b0[i] != b1[i])
                {
                    return false;
                }
            }
            return true;
        }


        internal static int TrrntZipStringCompare(string string1, string string2)
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

                if (byte1 >= 65 && byte1 <= 90)
                {
                    byte1 += 0x20;
                }
                if (byte2 >= 65 && byte2 <= 90)
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

        public static string ZipErrorMessageText(ZipReturn zS)
        {
            string ret = "Unknown";
            switch (zS)
            {
                case ZipReturn.ZipGood:
                    ret = "";
                    break;
                case ZipReturn.ZipFileCountError:
                    ret = "The number of file in the Zip does not mach the number of files in the Zips Centeral Directory";
                    break;
                case ZipReturn.ZipSignatureError:
                    ret = "An unknown Signature Block was found in the Zip";
                    break;
                case ZipReturn.ZipExtraDataOnEndOfZip:
                    ret = "Extra Data was found on the end of the Zip";
                    break;
                case ZipReturn.ZipUnsupportedCompression:
                    ret = "An unsupported Compression method was found in the Zip, if you recompress this zip it will be usable";
                    break;
                case ZipReturn.ZipLocalFileHeaderError:
                    ret = "Error reading a zipped file header information";
                    break;
                case ZipReturn.ZipCentralDirError:
                    ret = "There is an error in the Zip Centeral Directory";
                    break;
                case ZipReturn.ZipReadingFromOutputFile:
                    ret = "Trying to write to a Zip file open for output only";
                    break;
                case ZipReturn.ZipWritingToInputFile:
                    ret = "Tring to read from a Zip file open for input only";
                    break;
                case ZipReturn.ZipErrorGettingDataStream:
                    ret = "Error creating Data Stream";
                    break;
                case ZipReturn.ZipCRCDecodeError:
                    ret = "CRC error";
                    break;
                case ZipReturn.ZipDecodeError:
                    ret = "Error unzipping a file";
                    break;
            }

            return ret;
        }
        public const long fileTimeToUTCTime = 504911232000000000;
        public const long epochTimeToUTCTime = 621355968000000000;
        public const long trrntzipDateTime = 629870671200000000;

        private const long TicksPerMillisecond = 10000;
        private const long TicksPerSecond = TicksPerMillisecond * 1000;

        public static void SetDateTime(long ticks, out ushort DosFileDate, out ushort DosFileTime)
        {
            DateTime DateTime = new DateTime(ticks, DateTimeKind.Unspecified);
            DosFileDate = (ushort)((DateTime.Day & 0x1f) | ((DateTime.Month & 0x0f) << 5) | (((DateTime.Year - 1980) & 0x7f) << 9));
            DosFileTime = (ushort)(((DateTime.Second >> 1) & 0x1f) | ((DateTime.Minute & 0x3f) << 5) | ((DateTime.Hour & 0x1f) << 11));
        }

        public static long SetDateTime(ushort DosFileDate, ushort DosFileTime)
        {
            if (DosFileDate == 0)
                return 0;

            int second = (DosFileTime & 0x1f) << 1;
            int minute = (DosFileTime >> 5) & 0x3f;
            int hour = (DosFileTime >> 11) & 0x1f;

            int day = DosFileDate & 0x1f;
            int month = (DosFileDate >> 5) & 0x0f;
            int year = ((DosFileDate >> 9) & 0x7f) + 1980;

            // valid hours 0 to 23
            // valid minutes 0 to 59
            // valid seconds 0 to 59
            // valid month 1 to 12
            // valid day 1 to 31

            if (hour > 23 || minute > 59 || second > 59 || month < 1 || month > 12 || day < 1 || day > 31)
                return 0;

            try
            {
                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).Ticks;
            }
            catch
            {
                return 0;
            }
        }

        public static long FileTimeToUTCTime(long ticks)
        {
            return ticks + fileTimeToUTCTime;
        }

        public static long SetDateTimeFromUnixSeconds(int seconds)
        {
            return seconds * TicksPerSecond + epochTimeToUTCTime;
        }

    }
}
