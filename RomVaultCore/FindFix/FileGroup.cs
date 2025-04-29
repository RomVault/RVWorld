using System.Collections.Generic;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

namespace RomVaultCore.FindFix
{
    public class FileGroup
    {
        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public HeaderFileType HeaderFT;
        public ulong? AltSize;
        public byte[] AltCRC;
        public byte[] AltSHA1;
        public byte[] AltMD5;

        public readonly List<RvFile> Files = new List<RvFile>();

        public FileGroup(RvFile sourceFile)
        {
            Size = sourceFile.Size;
            CRC = sourceFile.CRC.Copy();
            SHA1 = sourceFile.SHA1.Copy();
            MD5 = sourceFile.MD5.Copy();
            HeaderFT = FileScanner.FileHeaderReader.AltHeaderFile(sourceFile.HeaderFileType) ? sourceFile.HeaderFileType : HeaderFileType.Nothing;
            AltSize = sourceFile.AltSize;
            AltCRC = sourceFile.AltCRC.Copy();
            AltSHA1 = sourceFile.AltSHA1.Copy();
            AltMD5 = sourceFile.AltMD5.Copy();

            Files.Add(sourceFile);
            sourceFile.FileGroup = this;
        }


        public void MergeFileIntoGroup(RvFile file)
        {
            if (Size == null && file.Size != null) Size = file.Size;
            if (CRC == null && file.CRC != null) CRC = file.CRC.Copy();
            if (SHA1 == null && file.SHA1 != null) SHA1 = file.SHA1.Copy();
            if (MD5 == null && file.MD5 != null) MD5 = file.MD5.Copy();

            if (HeaderFT == HeaderFileType.Nothing && FileScanner.FileHeaderReader.AltHeaderFile(file.HeaderFileType)) HeaderFT = file.HeaderFileType;
            if (AltSize == null && file.AltSize != null) AltSize = file.AltSize;
            if (AltCRC == null && file.AltCRC != null) AltCRC = file.AltCRC.Copy();
            if (AltSHA1 == null && file.AltSHA1 != null) AltSHA1 = file.AltSHA1.Copy();
            if (AltMD5 == null && file.AltMD5 != null) AltMD5 = file.AltMD5.Copy();

            Files.Add(file);
            file.FileGroup = this;
        }


        public void MergeAltFileIntoGroup(RvFile file)
        {
            if (HeaderFT == HeaderFileType.Nothing && FileScanner.FileHeaderReader.AltHeaderFile(file.HeaderFileType)) HeaderFT = file.HeaderFileType;
            if (AltSize == null && file.Size != null) AltSize = file.Size;
            if (AltCRC == null && file.CRC != null) AltCRC = file.CRC.Copy();
            if (AltSHA1 == null && file.SHA1 != null) AltSHA1 = file.SHA1.Copy();
            if (AltMD5 == null && file.MD5 != null) AltMD5 = file.MD5.Copy();

            Files.Add(file);
            file.FileGroup = this;
        }


        public bool FindExactMatch(RvFile file)
        {
            if (!Equal(file.Size, Size)) return false;
            if (!ArrByte.ECompare(file.CRC, CRC)) return false;
            if (!ArrByte.ECompare(file.SHA1, SHA1)) return false;
            if (!ArrByte.ECompare(file.MD5, MD5)) return false;

            // should check header file type also.

            if (!Equal(file.AltSize, AltSize)) return false;
            if (!ArrByte.ECompare(file.AltCRC, AltCRC)) return false;
            if (!ArrByte.ECompare(file.AltSHA1, AltSHA1)) return false;
            if (!ArrByte.ECompare(file.AltMD5, AltMD5)) return false;

            return true;
        }

        private bool FindAltExactMatch(RvFile file)
        {
            // should check header file type also.

            if (!Equal(file.Size, AltSize)) return false;
            if (!ArrByte.ECompare(file.CRC, AltCRC)) return false;
            if (!ArrByte.ECompare(file.SHA1, AltSHA1)) return false;
            if (!ArrByte.ECompare(file.MD5, AltMD5)) return false;

            return true;
        }

        public static bool FindExactMatch(FileGroup fGroup, RvFile file)
        {
            return fGroup.FindExactMatch(file);
        }

        public static bool FindAltExactMatch(FileGroup fGroup, RvFile file)
        {
            return fGroup.FindAltExactMatch(file);
        }

        private static bool Equal(ulong? t0, ulong? t1)
        {
            if (t0 == null || t1 == null)
                return true;
            return t0 == t1;
        }

    }
}
