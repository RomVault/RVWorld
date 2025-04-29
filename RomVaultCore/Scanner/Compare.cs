using System;
using FileScanner;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

/*
Pass one:

For directories and archives:
DIR,ZIP,SEVENZIP
all we check is that the type match and return good.

for ZIPfiles & 7Zfiles  (and level 3 checked files, as we will already have the CRC/SHA1/MD5)
						so check here if we have a CRC from the file. (That is enough of a check)

we check
	size
	CRC
	SHA1
	MD5
and if what we have match then return good.
we also check
	size=altSize
	CRC=altCRC
	SHA1=altSHA1
	MD5=altMD5


For FILES (anything that we did not get a CRC from the file.)
Match on the TimeStamp (as that is all we have from the file.)
(Also if the filematch in the database has only ever been level 1 scanned and we are now level 2 (or 3) scanning
then we must not match on the TimeStamp (as that is now not enough)


Pass two:
We could not confirm a match above so we need to deep scan the file and try again.

*/
namespace RomVaultCore.Scanner
{

    public static partial class FileScanning
    {
        private static class FileCompare
        {
            internal static bool Phase1Test(RvFile dbFile, ScannedFile testFile, EScanLevel eScanLevel, int indexCase, out bool MatchedAlt)
            {
                MatchedAlt = false;
                //Debug.WriteLine("Comparing Dat File " + dbFile.TreeFullName);
                //Debug.WriteLine("Comparing File     " + testFile.TreeFullName);

                int retv = indexCase == 0 ?
                    dbFile.Name.CompareTo(testFile.Name) :
                    RVSorters.CompareName(dbFile, testFile);
                if (retv != 0)
                    return false;

                FileType dbfileType = dbFile.FileType;
                FileType testFileType = testFile.FileType;

#if ZipFile
            if (dbfileType == FileType.File && testFileType == FileType.Zip) 
                testFileType = FileType.File;
#endif
                retv = Math.Sign(dbfileType.CompareTo(testFileType));
                if (retv != 0)
                    return false;

                // filetypes are now know to be the same

                // Dir's and Zip's are not deep scanned so matching here is done
                if (dbfileType == FileType.Dir || dbfileType == FileType.Zip || dbfileType == FileType.SevenZip)
                    return true;

                // check headerTypes
                if (dbFile.HeaderFileTypeRequired)
                {
                    if (dbFile.HeaderFileType != testFile.HeaderFileType)
                        return false;
                }

                // we can now fully test anything that had a CRC in the testFile
                // this is anything that came from an archive, or a file that was level 3 scanned
                if (testFile.CRC != null)
                    return CompareWithAlt(dbFile, testFile, out MatchedAlt);


                // we are now just dealing with Files that were not scanned at all already.
                // Phase 1 we will try and just do a timestamp / file size match for this.
                // but if we are scanning at a deeper level than the DB file then we cannot timestamp match.
                //
                // this could happen where we started with a level 1 scan of a file, and are now re-scanning at level 2
                if (eScanLevel != EScanLevel.Level1 && !dbFile.IsDeepScanned)
                    return false;

                if (dbFile.FileModTimeStamp != testFile.FileModTimeStamp)
                    return false;

                if (dbFile.Size == testFile.Size)
                    return true;

                if ((dbFile.Size ?? 0) + (ulong)FileHeaderReader.GetFileHeaderLength(dbFile.HeaderFileType) != testFile.Size)
                    return false;

                MatchedAlt = true;
                return true;
            }

            internal static bool Phase2Test(RvFile dbFile, ScannedFile testFile, EScanLevel eScanLevel, int indexCase, string fullDir, ThreadWorker thWrk, int fileIndex, ref bool fileErrorAbort, out bool MatchedAlt)
            {
                MatchedAlt = false;
                //Debug.WriteLine("Comparing Dat File " + dbFile.TreeFullName);
                //Debug.WriteLine("Comparing File     " + testFile.TreeFullName);
                int retv = indexCase == 0 ?
                   dbFile.Name.CompareTo(testFile.Name) :
                   RVSorters.CompareName(dbFile, testFile);
                if (retv != 0)
                    return false;

                FileType dbFileType = dbFile.FileType;
                FileType testFileType = testFile.FileType;
#if ZipFile
            if (dbFileType == FileType.File && testFileType == FileType.Zip) 
                testFileType = FileType.File;
#endif
                if (dbFileType != FileType.File || testFileType != FileType.File)
                    return false;


                thWrk.Report(new bgwValue2((int)fileIndex));
                thWrk.Report(new bgwText2(testFile.Name));
                Populate.FromAFile(testFile, fullDir, eScanLevel, thWrk, ref fileErrorAbort);
                if (fileErrorAbort)
                    return false;

                if (testFile.GotStatus == GotStatus.FileLocked)
                    return true;

                return CompareWithAlt(dbFile, testFile, out MatchedAlt);
            }


            private static bool CompareWithAlt(RvFile dbFile, ScannedFile testFile, out bool altMatch)
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


            private static bool CompareHash(RvFile dbFile, ScannedFile testFile)
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

            private static bool CompareAltHash(RvFile dbFile, ScannedFile testFile)
            {
                if (!FileScanner.FileHeaderReader.AltHeaderFile(testFile.HeaderFileType))
                    return false;

                if (dbFile.HeaderFileType != testFile.HeaderFileType)
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
        }
    }
}