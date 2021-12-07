using System.Collections.Generic;
using System.Linq;
using Compress;
using RomVaultCore.RvDB;

namespace RomVaultCore.FixFile.Util
{
    public static class FindSourceFile
    {


        public static List<RvFile> GetFixFileList(RvFile fixFile)
        {
            return fixFile.FileGroup.Files.FindAll(file => file.GotStatus == GotStatus.Got && DBHelper.CheckIfMissingFileCanBeFixedByGotFile(fixFile, file));
        }

        public static RvFile FindSourceToUseForFix(RvFile fixFile, List<RvFile> lstFixRomTable)
        {
            switch (fixFile.FileType)
            {
                // first option is
                // if we are fixing a 7Z file first try and find an uncompressed file to use
                // else try and find a zip file to use, else use a 7Z file
                case FileType.SevenZipFile:
                    {
                        RvFile retFile = lstFixRomTable.FirstOrDefault(tFile => tFile.FileType == FileType.File);
                        if (retFile != null) return retFile;

                        retFile = lstFixRomTable.FirstOrDefault(tFile => tFile.FileType == FileType.ZipFile);
                        if (retFile != null) return retFile;

                        break;
                    }

                case FileType.ZipFile:
                    {
                        RvFile retFile = lstFixRomTable.FirstOrDefault(tFile =>
                             tFile.FileType == FileType.ZipFile &&
                             (tFile.Parent.ZipStatus & ZipStatus.TrrntZip) == ZipStatus.TrrntZip &&
                             tFile.FileStatusIs(FileStatus.SHA1Verified) && tFile.FileStatusIs(FileStatus.MD5Verified));
                        if (retFile != null) return retFile;

                        retFile = lstFixRomTable.FirstOrDefault(tFile =>
                                tFile.FileType == FileType.ZipFile &&
                                (tFile.Parent.ZipStatus & ZipStatus.TrrntZip) == ZipStatus.TrrntZip
                            );
                        if (retFile != null) return retFile;

                        retFile = lstFixRomTable.FirstOrDefault(tFile => tFile.FileType == FileType.File);
                        if (retFile != null) return retFile;

                        retFile = lstFixRomTable.FirstOrDefault(tFile => tFile.FileType == FileType.ZipFile);
                        if (retFile != null) return retFile;

                        break;
                    }
                case FileType.File:
                    {
                        RvFile retFile = lstFixRomTable.FirstOrDefault(tFile => tFile.FileType == FileType.File);
                        if (retFile != null) return retFile;

                        retFile = lstFixRomTable.FirstOrDefault(tFile =>
                            tFile.FileType == FileType.ZipFile &&
                            tFile.FileStatusIs(FileStatus.SHA1Verified) && tFile.FileStatusIs(FileStatus.MD5Verified)
                        );
                        if (retFile != null) return retFile;

                        retFile = lstFixRomTable.FirstOrDefault(tFile => tFile.FileType == FileType.ZipFile);
                        if (retFile != null) return retFile;

                        break;
                    }
            }

            return lstFixRomTable[0];
        }
    }
}
