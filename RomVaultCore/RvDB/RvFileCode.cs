/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/
using Compress;
using FileScanner;
using RomVaultCore.Utils;
using System;
using System.Collections.Generic;

namespace RomVaultCore.RvDB
{
    public partial class RvFile
    {

        // This only addes Archives (Zip,7z).
        public void MergeInArchive(ScannedFile fileArchive)
        {
            MarkAsMissing();

            fileArchive.Sort();

            int dbChildIndex = 0;
            int scannedFileIndex = 0;
            int res;

            while (true)
            {
                RvFile dbChild = null;
                ScannedFile scannedFile = null;

                if (dbChildIndex < ChildCount && scannedFileIndex < fileArchive.Count)
                {
                    dbChild = Child(dbChildIndex);
                    scannedFile = fileArchive[scannedFileIndex];
                    res = RVSorters.CompareName(dbChild, scannedFile);
                }
                else if (scannedFileIndex < fileArchive.Count)
                {
                    scannedFile = fileArchive[scannedFileIndex];
                    res = 1;
                }
                else if (dbChildIndex < ChildCount)
                {
                    dbChild = Child(dbChildIndex);
                    res = -1;
                }
                else
                    break;

                switch (res)
                {
                    case 0:
                        // on the crazy off chance that there are 2 files in a zip with the exact same name...
                        // need to do the double name check.
                        List<ScannedFile> scannedFiles = new List<ScannedFile>();
                        int filesCount = 1;
                        scannedFiles.Add(scannedFile);

                        while (scannedFileIndex + filesCount < fileArchive.Count && RVSorters.CompareName(dbChild, fileArchive[scannedFileIndex + filesCount]) == 0)
                        {
                            scannedFiles.Add(fileArchive[scannedFileIndex + filesCount]);
                            filesCount++;
                        }
                        bool[] fileFound = new bool[filesCount];
                        for (int fileIndex = 0; fileIndex < filesCount; fileIndex++)
                        {
                            bool matched = dbChild.CompareWithAlt(scannedFiles[fileIndex], out bool altMatch);
                            if (!matched)
                                continue;
                            fileFound[fileIndex] = true;
                            dbChild.FileMergeIn(scannedFiles[fileIndex], altMatch);
                        }

                        dbChildIndex++;
                        for (int fileIndex = 0; fileIndex < filesCount; fileIndex++)
                        {
                            if (fileFound[fileIndex])
                                continue;
                            FileAdd(scannedFiles[fileIndex], dbChildIndex);
                        }
                        scannedFileIndex += filesCount;
                        break;
                    case 1:
                        // add in the file only found in the zip
                        FileAdd(scannedFile, dbChildIndex);
                        dbChildIndex++;
                        scannedFileIndex++;
                        break;
                    case -1:
                        // skip the missing file in the DB
                        dbChildIndex++;
                        break;

                }
            }
        }

        public RvFile FileAdd(ScannedFile scannedFile, int index)
        {
            RvFile retFile = new RvFile(scannedFile);
            //need to get the DatStatus of the parent file to get this files DatStatus
            DatStatus datStatus = DatStatus == DatStatus.InToSort ? DatStatus.InToSort : DatStatus.NotInDat;
            GotStatus gotStatus = scannedFile.GotStatus;
            retFile.SetDatGotStatus(datStatus, gotStatus);

            ChildAdd(retFile, index);
            return retFile;
        }

        private RvFile(ScannedFile scannedFile)
        {
            FileType = scannedFile.FileType;
            // need to set the filetype.
            Name = scannedFile.Name;
            _headerFileType = scannedFile.HeaderFileType;
            ZipFileIndex = scannedFile.Index;
            ZipFileHeaderPosition = scannedFile.LocalHeaderOffset;
            Size = scannedFile.Size;
            CRC = scannedFile.CRC;
            SHA1 = scannedFile.SHA1;
            MD5 = scannedFile.MD5;
            AltSize = scannedFile.AltSize;
            AltCRC = scannedFile.AltCRC;
            AltSHA1 = scannedFile.AltSHA1;
            AltMD5 = scannedFile.AltMD5;
            FileModTimeStamp = scannedFile.FileModTimeStamp;
            CHDVersion = scannedFile.CHDVersion;

            FileStatusSet(scannedFile.StatusFlags);

            if (!IsDirectory)
                return;

            _dirDats = new List<RvDat>(); // DAT's stored in this dir in DatRoot
            _children = new List<RvFile>(); // children items of this dir
            DirStatus = new ReportStatus(); // Counts the status of all children for reporting in the UI
        }


        public void MarkAsMissing()
        {
            for (int i = 0; i < ChildCount; i++)
            {
                RvFile dbChild = Child(i);
                if (dbChild.FileRemove() == EFile.Delete)
                {
                    ChildRemove(i);
                    i--;
                }
                else
                {
                    switch (dbChild.FileType)
                    {
                        case FileType.Zip:
                        case FileType.SevenZip:
                            dbChild.MarkAsMissing();
                            break;

                        case FileType.Dir:
                            RvFile tDir = dbChild;
                            if (tDir.Tree == null)
                                tDir.MarkAsMissing();
                            break;
                    }
                }
            }
        }

        public void FileMergeIn(ScannedFile file, bool altFile)
        {
            if (altFile)
                SetAsAltFile();

            if (Size == null && file.Size != null) Size = file.Size;
            if (CRC == null && file.CRC != null) CRC = file.CRC;
            if (SHA1 == null && file.SHA1 != null) SHA1 = file.SHA1;
            if (MD5 == null && file.MD5 != null) MD5 = file.MD5;
            if (AltSize == null && file.AltSize != null) AltSize = file.AltSize;
            if (AltCRC == null && file.AltCRC != null) AltCRC = file.AltCRC;
            if (AltSHA1 == null && file.AltSHA1 != null) AltSHA1 = file.AltSHA1;
            if (AltMD5 == null && file.AltMD5 != null) AltMD5 = file.AltMD5;
            if (HeaderFileType == HeaderFileType.Nothing && file.HeaderFileType != HeaderFileType.Nothing) HeaderFileTypeSet = file.HeaderFileType;

            ZipFileIndex = file.Index;
            ZipFileHeaderPosition = file.LocalHeaderOffset;

            // need to check this does not change the FileModTimeStamp value.
            FileModTimeStamp = file.FileModTimeStamp;
            GotStatus = file.GotStatus;
            FileStatusSet(file.StatusFlags);
        }

        public ScannedFile fileOut()
        {
            ScannedFile sOut = new ScannedFile(FileType)
            {
                Name = NameCase,
                FileModTimeStamp = FileModTimeStamp,
                GotStatus = GotStatus,

                ZipStruct = ZipStruct,
                LocalHeaderOffset = ZipFileHeaderPosition,
                DeepScanned = IsDeepScanned,
                StatusFlags = _fileStatus & (FileStatus.HeaderFlags | FileStatus.VerifiedFlags),

                Index = ZipFileIndex,
                HeaderFileType = HeaderFileType,
                CHDVersion = CHDVersion
            };

            if (FileStatusIs(FileStatus.SizeFromHeader) || FileStatusIs(FileStatus.SizeVerified)) sOut.Size = Size;
            if (FileStatusIs(FileStatus.CRCFromHeader) || FileStatusIs(FileStatus.CRCVerified)) sOut.CRC = CRC;
            if (FileStatusIs(FileStatus.SHA1FromHeader) || FileStatusIs(FileStatus.SHA1Verified)) sOut.SHA1 = SHA1;
            if (FileStatusIs(FileStatus.MD5FromHeader) || FileStatusIs(FileStatus.MD5Verified)) sOut.MD5 = MD5;
            //sOut.SHA256 = SHA256;
            if (FileStatusIs(FileStatus.AltSizeFromHeader) || FileStatusIs(FileStatus.AltSizeVerified)) sOut.AltSize = AltSize;
            if (FileStatusIs(FileStatus.AltCRCFromHeader) || FileStatusIs(FileStatus.AltCRCVerified)) sOut.AltCRC = AltCRC;
            if (FileStatusIs(FileStatus.AltSHA1FromHeader) || FileStatusIs(FileStatus.AltSHA1Verified)) sOut.AltSHA1 = AltSHA1;
            if (FileStatusIs(FileStatus.AltMD5FromHeader) || FileStatusIs(FileStatus.AltMD5Verified)) sOut.AltMD5 = AltMD5;
            //sOut.AltSHA256=AltSHA256;

            if (IsDirectory)
            {
                for (int i = 0; i < ChildCount; i++)
                {
                    RvFile child = Child(i);
                    if (child.GotStatus == GotStatus.Got || child.GotStatus == GotStatus.Corrupt)
                        sOut.Add(child.fileOut());
                }
            }

            return sOut;
        }

        private void SetAsAltFile()
        {
            if (FileStatusIs(FileStatus.SizeFromDAT))
            {
                AltSize = Size;
                FileStatusSet(FileStatus.AltSizeFromDAT);
            }
            if (FileStatusIs(FileStatus.CRCFromDAT))
            {
                AltCRC = CRC;
                FileStatusSet(FileStatus.AltCRCFromDAT);
            }
            if (FileStatusIs(FileStatus.SHA1FromDAT))
            {
                AltSHA1 = SHA1;
                FileStatusSet(FileStatus.AltSHA1FromDAT);
            }
            if (FileStatusIs(FileStatus.MD5FromDAT))
            {
                AltMD5 = MD5;
                FileStatusSet(FileStatus.AltMD5FromDAT);
            }

            Size = null;
            CRC = null;
            SHA1 = null;
            MD5 = null;
            FileStatusClear(FileStatus.SizeFromDAT | FileStatus.CRCFromDAT | FileStatus.SHA1FromDAT | FileStatus.MD5FromDAT);

        }


        public void FileCheckName(ScannedFile file)
        {
            // Don't care about bad case if the file is not in a dat.
            if (DatStatus == DatStatus.NotInDat || DatStatus == DatStatus.InToSort)
                Name = file.Name;

            FileName = Name == file.Name ? null : file.Name;
        }

        public void FileCheckName(RvFile file)
        {
            // Don't care about bad case if the file is not in a dat.
            if (DatStatus == DatStatus.NotInDat || DatStatus == DatStatus.InToSort)
                Name = file.Name;

            FileName = Name == file.Name ? null : file.Name;
        }

        /// <summary>
        /// FileRemove
        /// If a file is deleted this will remove all the data about the file from this rvFile.
        /// If the file is also added from a dat then the rvFile should remain with just the original data from the DAT,
        /// all other non-DAT meta data will be removed, if this rvFile had AltData from a DAT this will be moved back to be
        /// the mail hash data from this rvFile.
        /// </summary>
        /// <returns>
        /// EFile.Delete:  this file should be deleted from the DB
        /// Efile.Keep:    this file should be kept in the DB as it is from a dat.
        /// </returns>
        public EFile FileRemove()
        {
            if (IsFile) // File,ZippedFile or 7zippedFile
            {
                ZipFileIndex = -1;
                ZipFileHeaderPosition = null;

                // TestRemove will also set GotStatus to NotGot. (unless the file is locked.)
                if (TestRemove() == EFile.Delete)
                    return EFile.Delete;

                if (!FileStatusIs(FileStatus.DateFromDAT)) FileModTimeStamp = long.MinValue;

                // if none of the primary meta data is from the DAT delete it.
                if (!FileStatusIs(FileStatus.HeaderFileTypeFromDAT)) HeaderFileTypeSet = HeaderFileType.Nothing;
                if (!FileStatusIs(FileStatus.SizeFromDAT)) Size = null;
                if (!FileStatusIs(FileStatus.CRCFromDAT)) CRC = null;
                if (!FileStatusIs(FileStatus.SHA1FromDAT)) SHA1 = null;
                if (!FileStatusIs(FileStatus.MD5FromDAT)) MD5 = null;

                // if the Alt meta data is from the dat move it up to be the primary data.
                if (FileStatusIs(FileStatus.AltSizeFromDAT))
                {
                    Size = AltSize;
                    FileStatusSet(FileStatus.SizeFromDAT);
                }
                if (FileStatusIs(FileStatus.AltCRCFromDAT))
                {
                    CRC = AltCRC;
                    FileStatusSet(FileStatus.CRCFromDAT);
                }
                if (FileStatusIs(FileStatus.AltSHA1FromDAT))
                {
                    SHA1 = AltSHA1;
                    FileStatusSet(FileStatus.SHA1FromDAT);
                }
                if (FileStatusIs(FileStatus.AltMD5FromDAT))
                {
                    MD5 = AltMD5;
                    FileStatusSet(FileStatus.MD5FromDAT);
                }

                // remove all Alt Data.
                AltSize = null;
                AltCRC = null;
                AltSHA1 = null;
                AltMD5 = null;

                CHDVersion = null;
                FileStatusClear(FileStatus.DatAltFlags | FileStatus.HeaderFlags | FileStatus.VerifiedFlags);

                ZipStruct = ZipStructure.None;
                return EFile.Keep;
            }

            if (IsDirectory)
            {
                ZipStruct = ZipStructure.None;
                return TestRemove();
            }

            // This should never happen, as either IsFile or IsDir should be set.
            GotStatus = GotStatus.NotGot;
            FileModTimeStamp = long.MinValue;
            ReportError.SendAndShow("Unknown File Remove Type");
            return EFile.Keep;
        }

        private EFile TestRemove()
        {
            if (!FileStatusIs(FileStatus.DateFromDAT))
            {
                FileModTimeStamp = long.MinValue;
            }
            FileName = null;

            GotStatus = Parent.GotStatus == GotStatus.FileLocked ? GotStatus.FileLocked : GotStatus.NotGot;
            switch (DatStatus)
            {
                case DatStatus.InDatCollect:
                case DatStatus.InDatMerged:
                case DatStatus.InDatNoDump:
                case DatStatus.InDatMIA:
                    return EFile.Keep;

                case DatStatus.NotInDat:
                case DatStatus.InToSort:
                    return EFile.Delete; // this item should be removed from the db.
                default:
                    ReportError.SendAndShow("Unknown Set Got Status " + DatStatus);
                    return EFile.Keep;
            }
        }




        private bool CompareWithAlt(ScannedFile testFile, out bool altMatch)
        {
            if (FileModTimeStamp != long.MinValue && FileModTimeStamp != 0)
                if (FileModTimeStamp != testFile.FileModTimeStamp)
                {
                    altMatch = false;
                    return false;
                }

            if (CompareHash(testFile))
            {
                altMatch = false;
                return true;
            }

            if (CompareAltHash(testFile))
            {
                altMatch = true;
                return true;
            }

            altMatch = false;
            return false;
        }
        private bool CompareHash(ScannedFile testFile)
        {
            //Debug.WriteLine("Comparing Dat File " + dbFile.TreeFullName);
            //Debug.WriteLine("Comparing File     " + testFile.TreeFullName);

            bool testFound = false;
            int retv;
            if (Size != null && testFile.Size != null)
            {
                retv = ULong.iCompare(Size, testFile.Size);
                if (retv != 0)
                    return false;

                //special zero size test case, if the dat size is 0 and the testfile size is 0
                //and there are no other hash values in the dat, then assume it is a match.
                if (testFile.Size == 0 && CRC == null && SHA1 == null && MD5 == null)
                    return true;
            }


            if (CRC != null && testFile.CRC != null)
            {
                testFound = true;
                retv = ArrByte.ICompare(CRC, testFile.CRC);
                if (retv != 0)
                    return false;
            }

            if (SHA1 != null && testFile.SHA1 != null)
            {
                testFound = true;
                retv = ArrByte.ICompare(SHA1, testFile.SHA1);
                if (retv != 0)
                    return false;
            }

            if (MD5 != null && testFile.MD5 != null)
            {
                testFound = true;
                retv = ArrByte.ICompare(MD5, testFile.MD5);
                if (retv != 0)
                    return false;
            }

            return testFound;
        }
        private bool CompareAltHash(ScannedFile testFile)
        {
            if (!FileScanner.FileHeaderReader.AltHeaderFile(testFile.HeaderFileType))
                return false;

            if (HeaderFileType != testFile.HeaderFileType)
                return false;


            bool testFound = false;
            int retv;
            if (Size != null && testFile.AltSize != null)
            {
                retv = ULong.iCompare(Size, testFile.AltSize);
                if (retv != 0)
                    return false;
            }

            if (CRC != null && testFile.AltCRC != null)
            {
                testFound = true;
                retv = ArrByte.ICompare(CRC, testFile.AltCRC);
                if (retv != 0)
                    return false;
            }

            if (SHA1 != null && testFile.AltSHA1 != null)
            {
                testFound = true;
                retv = ArrByte.ICompare(SHA1, testFile.AltSHA1);
                if (retv != 0)
                    return false;
            }

            if (MD5 != null && testFile.AltMD5 != null)
            {
                testFound = true;
                retv = ArrByte.ICompare(MD5, testFile.AltMD5);
                if (retv != 0)
                    return false;
            }

            return testFound;
        }


        //************************************
        //
        //   THESE ALL NEED TO GO
        //
        //************************************

        [Obsolete("deprecated")]
        public void FileMergeIn(RvFile file, bool altFile)
        {
            if (file.GotStatus == GotStatus.NotGot)
                ReportError.SendAndShow("Error setting got to a NotGot File");

            if (file.IsFile)
            {
                if (altFile)
                    SetAsAltFile();

                if (Size == null && file.Size != null) Size = file.Size;
                if (CRC == null && file.CRC != null) CRC = file.CRC;
                if (SHA1 == null && file.SHA1 != null) SHA1 = file.SHA1;
                if (MD5 == null && file.MD5 != null) MD5 = file.MD5;
                if (AltSize == null && file.AltSize != null) AltSize = file.AltSize;
                if (AltCRC == null && file.AltCRC != null) AltCRC = file.AltCRC;
                if (AltSHA1 == null && file.AltSHA1 != null) AltSHA1 = file.AltSHA1;
                if (AltMD5 == null && file.AltMD5 != null) AltMD5 = file.AltMD5;
                if (HeaderFileType == HeaderFileType.Nothing && file.HeaderFileType != HeaderFileType.Nothing) HeaderFileTypeSet = file.HeaderFileType;
                if (CHDVersion == null && file.CHDVersion != null) CHDVersion = file.CHDVersion;


                FileStatusSet(FileStatus.HeaderFlags | FileStatus.VerifiedFlags, file);

                ZipFileIndex = file.ZipFileIndex;
                ZipFileHeaderPosition = file.ZipFileHeaderPosition;
            }

            FileCheckName(file);

            // this catches corrupt CHD (files) from being reset to valid
            if (file.FileType == FileType.File && FileModTimeStamp == file.FileModTimeStamp)
            {
                if (file.GotStatus == GotStatus.Corrupt)
                    GotStatus = file.GotStatus;
                return;
            }
            FileModTimeStamp = file.FileModTimeStamp;
            GotStatus = file.GotStatus;
        }

        public bool IsDeepScanned
        {
            get
            {
                if (IsFile)
                {
                    if (Settings.rvSettings.CheckCHDVersion && HeaderFileType == HeaderFileType.CHD && CHDVersion == null)
                        return false;
                    if (FileStatusIs(FileStatus.AltCRCFromHeader) && !FileStatusIs(FileStatus.AltCRCVerified))
                        return false;
                    if (FileStatusIs(FileStatus.AltSHA1FromHeader) && !FileStatusIs(FileStatus.AltSHA1Verified))
                        return false;
                    if (FileStatusIs(FileStatus.AltMD5FromHeader) && !FileStatusIs(FileStatus.AltMD5Verified))
                        return false;

                    return FileStatusIs(FileStatus.SizeVerified) &&
                           FileStatusIs(FileStatus.CRCVerified) &&
                           FileStatusIs(FileStatus.SHA1Verified) &&
                           FileStatusIs(FileStatus.MD5Verified);
                }

                // is a dir
                for (int i = 0; i < ChildCount; i++)
                {
                    RvFile zFile = Child(i);
                    if (zFile.IsFile && zFile.GotStatus == GotStatus.Got && !zFile.IsDeepScanned)
                        return false;
                }
                return true;
            }
        }

        public void FileTestFix(RvFile file)
        {
            if (TestMatch(file))
                return;
            if (!TestMatchAlt(file))
                return;

            if (AltSize != null || AltCRC != null || AltSHA1 != null || AltMD5 != null)
            {
                //error
            }
            SetAsAltFile();

        }
        private bool TestMatch(RvFile file)
        {
            bool foundATest = false;
            if (Size != null && file.Size != null)
            {
                foundATest = true;
                if (Size != file.Size)
                    return false;
            }

            if (CRC != null && file.CRC != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(CRC, file.CRC))
                    return false;
            }

            if (SHA1 != null && file.SHA1 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(SHA1, file.SHA1))
                    return false;
            }

            if (MD5 != null && file.MD5 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(MD5, file.MD5))
                    return false;
            }

            return foundATest;
        }
        private bool TestMatchAlt(RvFile file)
        {
            bool foundATest = false;
            if (Size != null && file.AltSize != null)
            {
                foundATest = true;
                if (Size != file.AltSize)
                    return false;
            }

            if (CRC != null && file.AltCRC != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(CRC, file.AltCRC))
                    return false;
            }

            if (SHA1 != null && file.AltSHA1 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(SHA1, file.AltSHA1))
                    return false;

            }
            if (MD5 != null && file.AltMD5 != null)
            {
                foundATest = true;
                if (!ArrByte.BCompare(MD5, file.AltMD5))
                    return false;
            }

            return foundATest;
        }




    }
}
