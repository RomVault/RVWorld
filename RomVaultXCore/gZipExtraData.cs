using FileHeaderReader;
using RVIO;
using RVXCore.DB;
using RVXCore.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVXCore
{
    internal static class gZipExtraData
    {
        public static RvFile fromGZip(string filename, byte[] bytes, ulong compressedSize)
        {
            RvFile retFile = new RvFile();
            retFile.CompressedSize = compressedSize;

            retFile.SHA1 = VarFix.CleanMD5SHA1(Path.GetFileNameWithoutExtension(filename), 40);
            retFile.MD5 = new byte[16];
            Array.Copy(bytes, 0, retFile.MD5, 0, 16);
            retFile.CRC = new byte[4];
            Array.Copy(bytes, 16, retFile.CRC, 0, 4);
            retFile.Size = BitConverter.ToUInt64(bytes, 20);

            if (bytes.Length == 28)
                return retFile;

            retFile.AltType = (HeaderFileType)bytes[28];
            retFile.AltMD5 = new byte[16];
            Array.Copy(bytes, 29, retFile.AltMD5, 0, 16);
            retFile.AltSHA1 = new byte[20];
            Array.Copy(bytes, 45, retFile.AltSHA1, 0, 20);
            retFile.AltCRC = new byte[4];
            Array.Copy(bytes, 65, retFile.AltCRC, 0, 4);
            retFile.AltSize = BitConverter.ToUInt64(bytes, 69);

            return retFile;
        }

        public static byte[] SetExtraData(RvFile gFile)
        {
            bool alt = FileHeaderReader.FileHeaderReader.AltHeaderFile(gFile.AltType);
            byte[] retData = alt
                ? new byte[77]
                : new byte[28];

            Array.Copy(gFile.MD5, 0, retData, 0, 16);
            Array.Copy(gFile.CRC, 0, retData, 16, 4);
            Array.Copy(BitConverter.GetBytes(gFile.Size), 0, retData, 20, 8);

            if (!alt)
                return retData;

            retData[28] = (byte)gFile.AltType;
            Array.Copy(gFile.AltMD5, 0, retData, 29, 16);
            Array.Copy(gFile.AltSHA1, 0, retData, 45, 20);
            Array.Copy(gFile.AltCRC, 0, retData, 65, 4);
            Array.Copy(BitConverter.GetBytes((ulong)gFile.AltSize), 0, retData, 69, 8);

            return retData;
        }
    }
}
