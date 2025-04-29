using System;
using Directory = RVIO.Directory;
using Path = RVIO.Path;

namespace Compress
{
    public static class CompressUtils
    {
        /*
        private static int _zstdCompCount;
        public static int zstdCompCount
        {
            get
            {
                return _zstdCompCount == 0 ? Math.Max(Environment.ProcessorCount - 2, 1) : _zstdCompCount;
            }
            set
            {
                _zstdCompCount = Math.Max(value, 0);
            }
        }
        */
        internal static int SetThreadCount(int? threadCount)
        {
            if (threadCount == null)
                return Environment.ProcessorCount - 2;
            if (threadCount <= 0) 
                return Environment.ProcessorCount - 2;

            return (int)threadCount;
        }


        public static void CreateDirForFile(string sFilename)
        {
            string strTemp = Path.GetDirectoryName(sFilename);

            if (string.IsNullOrEmpty(strTemp))
            {
                return;
            }

            if (Directory.Exists(strTemp))
            {
                return;
            }

            Directory.CreateDirectory(strTemp);
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


        public static bool ByteArrCompare(byte[] b0, byte[] b1)
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

        public static ZipReturn GetFile(this ICompress zip, int index, out byte[] data)
        {
            ZipReturn res = zip.ZipFileOpenReadStream(index, out System.IO.Stream stream, out ulong streamSize);
            if (res != ZipReturn.ZipGood)
            {
                zip.ZipFileCloseReadStream();
                data = null;
                return res;
            }
            data = new byte[streamSize];
            stream.Read(data, 0, (int)streamSize);
            if (zip is not SevenZip.SevenZ)
                res = zip.ZipFileCloseReadStream();
            return res;
        }


        public static string ZipErrorMessageText(ZipReturn zipReturn)
        {
            string ret = zipReturn.ToString();
            switch (zipReturn)
            {
                case ZipReturn.ZipGood:
                    ret = "";
                    break;
                case ZipReturn.ZipFileCountError:
                    ret = "The number of file in the Zip does not mach the number of files in the Zips Central Directory";
                    break;
                case ZipReturn.ZipSignatureError:
                    ret = "An unknown Signature Block was found in the Zip";
                    break;
                case ZipReturn.ZipExtraDataOnEndOfZip:
                    ret = "Extra Data was found on the end of the Zip";
                    break;
                case ZipReturn.ZipUnsupportedCompression:
                    ret = "An unsupported Compression method was found in the Zip, if you re-compress this zip it will be usable";
                    break;
                case ZipReturn.ZipLocalFileHeaderError:
                    ret = "Error reading a zipped file header information";
                    break;
                case ZipReturn.ZipCentralDirError:
                    ret = "There is an error in the Zip Central Directory";
                    break;
                case ZipReturn.ZipReadingFromOutputFile:
                    ret = "Trying to write to a Zip file open for output only";
                    break;
                case ZipReturn.ZipWritingToInputFile:
                    ret = "Trying to read from a Zip file open for input only";
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


        private const long FileTimeToUtcTime = 504911232000000000;
        private const long EpochTimeToUtcTime = 621355968000000000;

        private const long TicksPerMillisecond = 10000;
        private const long TicksPerSecond = TicksPerMillisecond * 1000;



        public static void UtcTicksToDosDateTime(long ticks, out ushort dosFileDate, out ushort dosFileTime)
        {
            if (ticks <= 0xffffffff)
            {
                dosFileDate = (ushort)((ticks >> 16) & 0xffff);
                dosFileTime = (ushort)(ticks & 0xffff);
                return;
            }

            DateTime dateTime = new(ticks, DateTimeKind.Unspecified);
            dosFileDate = (ushort)((dateTime.Day & 0x1f) | ((dateTime.Month & 0x0f) << 5) | (((dateTime.Year - 1980) & 0x7f) << 9));
            dosFileTime = (ushort)(((dateTime.Second >> 1) & 0x1f) | ((dateTime.Minute & 0x3f) << 5) | ((dateTime.Hour & 0x1f) << 11));
        }


        public static string zipDateTimeToString(long? zipFileDateTime)
        {
            if (zipFileDateTime == null || zipFileDateTime == 0 || zipFileDateTime == long.MinValue)
                return "";

            if (zipFileDateTime > 0xffffffff)
            {
                if (zipFileDateTime < DateTime.MinValue.Ticks || zipFileDateTime > DateTime.MaxValue.Ticks)
                    return "";

                var t = new DateTime((long)zipFileDateTime);

                return $"{t.Year:D4}/{t.Month:D2}/{t.Day:D2} {t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}";
            }

            ushort dosFileDate = (ushort)((zipFileDateTime >> 16) & 0xffff);
            ushort dosFileTime = (ushort)(zipFileDateTime & 0xffff);

            int second = (dosFileTime & 0x1f) << 1;
            int minute = (dosFileTime >> 5) & 0x3f;
            int hour = (dosFileTime >> 11) & 0x1f;

            int day = dosFileDate & 0x1f;
            int month = (dosFileDate >> 5) & 0x0f;
            int year = ((dosFileDate >> 9) & 0x7f) + 1980;

            return $"{year:D4}/{month:D2}/{day:D2} {hour:D2}:{minute:D2}:{second:D2}";
        }


        internal static long CombineDosDateTime(ushort dosFileDate, ushort dosFileTime)
        {
            return (dosFileDate << 16) | dosFileTime;
        }

        public static long UtcTicksToNtfsDateTime(long ticks)
        {
            return ticks - FileTimeToUtcTime;
        }

        public static long UtcTicksFromNtfsDateTime(long ntfsTicks)
        {
            return ntfsTicks + FileTimeToUtcTime;
        }

        public static int UtcTicksToUnixDateTime(long ticks)
        {
            return (int)((ticks - EpochTimeToUtcTime) / TicksPerSecond);
        }

        public static long UtcTicksFromUnixDateTime(int linuxSeconds)
        {
            return linuxSeconds * TicksPerSecond + EpochTimeToUtcTime;
        }

    }
}