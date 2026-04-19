/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using RomVaultCore.RvDB;

namespace RomVaultCore.Utils
{
    /// <summary>
    /// Helper methods for mapping between directory/container types and their file-on-disk representations.
    /// </summary>
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
                case FileType.FileCHD:
                    return FileType.CHD;
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
                case FileType.CHD:
                    return FileType.FileCHD;
            }
            return FileType.Zip;
        }

        public static bool isCompressedDir(FileType fileType)
        {
            return fileType == FileType.Zip || fileType == FileType.SevenZip || fileType == FileType.CHD;
        }

        public static FileType fromExtention(string ext)
        {
            switch (ext.ToLower())
            {
                case ".7z": return FileType.SevenZip;
                case ".zip": return FileType.Zip;
                case ".chd": return FileType.CHD;
                default: return FileType.File;
            }
        }

    }
}
