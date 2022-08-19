/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2022                                 *
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
                case FileType.ZipFile:
                    return FileType.Zip;
                case FileType.SevenZipFile:
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
                    return FileType.ZipFile;
                case FileType.SevenZip:
                    return FileType.SevenZipFile;
            }
            return FileType.Zip;
        }

        public static bool isCompressedDir(FileType fileType)
        {
            return fileType == FileType.Zip || fileType == FileType.SevenZip;
        }

        public static FileType fromExtention(string ext)
        {
            switch (ext.ToLower())
            {
                case ".7z": return FileType.SevenZip;
                case ".zip": return FileType.Zip;
                default: return FileType.File;
            }
        }

    }
}