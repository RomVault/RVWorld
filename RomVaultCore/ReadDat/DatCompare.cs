/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using DATReader.DatStore;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using System;

namespace RomVaultCore.ReadDat
{
    public static partial class DatUpdate
    {
        private static class DatCompare
        {
            internal static bool DatMergeCompare(RvFile dbFile, DatBase testFile, int indexCase, out bool altMatch)
            {
                altMatch = false;

                //TODO check this
                int retv = indexCase == 0 ?
                    dbFile.Name.CompareTo(testFile.Name) :
                    RVSorters.CompareName(dbFile, testFile);
                if (retv != 0)
                {
                    return false;
                }

                FileType dbFileType = dbFile.FileType;
                FileType newFileType = testFile.FileType;
                retv = Math.Sign(dbFileType.CompareTo(newFileType));
                if (retv != 0)
                {
                    return false;
                }

                // filetypes are now know to be the same

                // Dir's and Zip's are not deep scanned so matching here is done
                if (dbFileType == FileType.Dir || dbFileType == FileType.Zip || dbFileType == FileType.SevenZip)
                {
                    return true;
                }

                // check headerTypes
                if (HeaderFileTypeRequired(((DatFile)testFile).HeaderFileType))
                {
                    if (dbFile.HeaderFileType != HeaderFileTypeHeaderOnly(((DatFile)testFile).HeaderFileType))
                        return false;
                }

                // This is temporary, and should probably be fixed to compare with name only match.
                if (dbFile.GotStatus == GotStatus.FileLocked)
                    return true;


                long? datTicks = testFile.DateModified;
                if (datTicks != null && datTicks != dbFile.FileModTimeStamp)
                    return false;

                return CompareWithAlt((DatFile)testFile, dbFile, out altMatch);
            }

            private static bool CompareWithAlt(DatFile dbFile, RvFile testFile, out bool altMatch)
            {
                if (CompareHash(dbFile, testFile))
                {
                    altMatch = false;
                    return true;
                }

                if (CompareAltHash(dbFile, testFile))
                {
                    altMatch = true;
                    return true;
                }

                altMatch = false;
                return false;
            }


            private static bool CompareHash(DatFile dbFile, RvFile testFile)
            {
                //Debug.WriteLine("Comparing Dat File " + dbFile.TreeFullName);
                //Debug.WriteLine("Comparing File     " + testFile.TreeFullName);

                bool testFound = false;
                int retv;
                if (dbFile.Size != null && testFile.Size != null)
                {
                    retv = ULong.iCompare(dbFile.Size, testFile.Size);
                    if (retv != 0)
                        return false;

                    //special zero size test case, if the dat size is 0 and the testfile size is 0
                    //and there are no other hash values in the dat, then assume it is a match.
                    if (testFile.Size == 0 && dbFile.CRC == null && dbFile.SHA1 == null && dbFile.MD5 == null)
                    {
                        return true;
                    }
                }


                if (dbFile.CRC != null && testFile.CRC != null)
                {
                    testFound = true;
                    retv = ArrByte.ICompare(dbFile.CRC, testFile.CRC);
                    if (retv != 0)
                        return false;
                }

                if (dbFile.SHA1 != null && testFile.SHA1 != null)
                {
                    testFound = true;
                    retv = ArrByte.ICompare(dbFile.SHA1, testFile.SHA1);
                    if (retv != 0)
                        return false;
                }

                if (dbFile.MD5 != null && testFile.MD5 != null)
                {
                    testFound = true;
                    retv = ArrByte.ICompare(dbFile.MD5, testFile.MD5);
                    if (retv != 0)
                        return false;
                }

                return testFound;
            }


            private static bool CompareAltHash(DatFile dbFile, RvFile testFile)
            {
                if (!FileScanner.FileHeaderReader.AltHeaderFile(testFile.HeaderFileType))
                    return false;

                if (HeaderFileTypeHeaderOnly(dbFile.HeaderFileType) != testFile.HeaderFileType)
                    return false;


                bool testFound = false;
                int retv;
                if (dbFile.Size != null && testFile.AltSize != null)
                {
                    retv = ULong.iCompare(dbFile.Size, testFile.AltSize);
                    if (retv != 0)
                        return false;
                }

                if (dbFile.CRC != null && testFile.AltCRC != null)
                {
                    testFound = true;
                    retv = ArrByte.ICompare(dbFile.CRC, testFile.AltCRC);
                    if (retv != 0)
                        return false;
                }

                if (dbFile.SHA1 != null && testFile.AltSHA1 != null)
                {
                    testFound = true;
                    retv = ArrByte.ICompare(dbFile.SHA1, testFile.AltSHA1);
                    if (retv != 0)
                        return false;
                }

                if (dbFile.MD5 != null && testFile.AltMD5 != null)
                {
                    testFound = true;
                    retv = ArrByte.ICompare(dbFile.MD5, testFile.AltMD5);
                    if (retv != 0)
                        return false;
                }

                return testFound;
            }


            private static HeaderFileType HeaderFileTypeHeaderOnly(HeaderFileType headerFileType)
            { return headerFileType & HeaderFileType.HeaderMask; }

            private static bool HeaderFileTypeRequired(HeaderFileType headerFileType)
            { return (headerFileType & HeaderFileType.Required) != 0; }

        }
    }
}
