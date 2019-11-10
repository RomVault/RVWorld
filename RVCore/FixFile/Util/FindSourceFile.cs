using System.Collections.Generic;
using Compress;
using RVCore.RvDB;

namespace RVCore.FixFile.Util
{
    public static class FindSourceFile
    {


        public static List<RvFile> GetFixFileList(RvFile fixFile)
        {
            List<RvFile> lstFixRomTable = new List<RvFile>();
            List<RvFile> family = fixFile.FileGroup.Files;
            foreach (RvFile file in family)
            {
                if (file.GotStatus == GotStatus.Got && DBHelper.CheckIfMissingFileCanBeFixedByGotFile(fixFile, file))
                    lstFixRomTable.Add(file);
            }

            return lstFixRomTable;
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
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];
                            if (tFile.FileType == FileType.File)
                            {
                                return lstFixRomTable[fIndex];
                            }
                        }
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];
                            if (tFile.FileType == FileType.ZipFile)
                            {
                                return lstFixRomTable[fIndex];
                            }
                        }

                        return lstFixRomTable[0];
                    }

                case FileType.ZipFile:
                    {
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];

                            if (tFile.FileType == FileType.ZipFile)
                            {
                                bool trrntzipped = (tFile.Parent.ZipStatus & ZipStatus.TrrntZip) == ZipStatus.TrrntZip;
                                bool deepchecked = tFile.FileStatusIs(FileStatus.SHA1Verified) && tFile.FileStatusIs(FileStatus.MD5Verified);

                                if (trrntzipped && deepchecked)
                                    return lstFixRomTable[fIndex];
                            }
                        }
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];

                            if (tFile.FileType == FileType.ZipFile)
                            {
                                bool trrntzipped = (tFile.Parent.ZipStatus & ZipStatus.TrrntZip) == ZipStatus.TrrntZip;

                                if (trrntzipped)
                                    return lstFixRomTable[fIndex];
                            }
                        }
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];
                            if (tFile.FileType == FileType.File)
                            {
                                return lstFixRomTable[fIndex];
                            }
                        }
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];

                            if (tFile.FileType == FileType.ZipFile)
                            {
                                return lstFixRomTable[fIndex];
                            }
                        }
                        break;
                    }
                case FileType.File:
                    {
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];
                            if (tFile.FileType == FileType.File)
                            {
                                return lstFixRomTable[fIndex];
                            }
                        }

                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];

                            if (tFile.FileType == FileType.ZipFile)
                            {
                                bool deepchecked = tFile.FileStatusIs(FileStatus.SHA1Verified) && tFile.FileStatusIs(FileStatus.MD5Verified);

                                if (deepchecked)
                                    return lstFixRomTable[fIndex];
                            }
                        }
                        for (int fIndex = 0; fIndex < lstFixRomTable.Count; fIndex++)
                        {
                            RvFile tFile = lstFixRomTable[fIndex];

                            if (tFile.FileType == FileType.ZipFile)
                            {
                                return lstFixRomTable[fIndex];
                            }
                        }
                        break;
                    }
            }

            return lstFixRomTable[0];
        }
    }
}
