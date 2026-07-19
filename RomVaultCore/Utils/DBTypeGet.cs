/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using RomVaultCore.RvDB;

namespace RomVaultCore.Utils
{
    public class DBTypeGet
    {
        public static FileType DirFromFile(FileType ft)
        {
            switch (ft)
            {
                case FileType.File:
                    return FileType.Dir;
                case FileType.FileZip:
                    return FileType.Zip;
                case FileType.FileSevenZip:
                    return FileType.SevenZip;
            }
            return FileType.Zip;
        }

        public static FileType FileFromDir(FileType ft)
        {
            switch (ft)
            {
                case FileType.Dir:
                    return FileType.File;
                case FileType.Zip:
                    return FileType.FileZip;
                case FileType.SevenZip:
                    return FileType.FileSevenZip;
            }
            return FileType.Zip;
        }

        public static bool isCompressedDir(FileType fileType)
        {
            return fileType == FileType.Zip || fileType == FileType.SevenZip;
        }

        public static FileType fromExtention(string ext)
        {
            if (string.Equals(ext, ".7z", System.StringComparison.OrdinalIgnoreCase))
                return FileType.SevenZip;
            if (string.Equals(ext, ".zip", System.StringComparison.OrdinalIgnoreCase))
                return FileType.Zip;
            return FileType.File;
        }

    }
}
