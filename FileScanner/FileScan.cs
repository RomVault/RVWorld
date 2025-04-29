using System.Collections.Generic;
using Compress;
using Compress.SevenZip;
using Compress.ThreadReaders;
using Compress.ZipFile;
using Compress.File;
using Stream = System.IO.Stream;
using Compress.StructuredZip;
using System.Collections.Concurrent;

namespace FileScanner;


/*
 * logic to understand bool testcrc & bool deepScan
 * ------------------------------------------------
 *
 * DeepScan | Has a Header | size | crc | sha1 | md5 | altsize | altcrc | altsha1 | altmd5
 * ---------------------------------------------------------------------------------------
 *     0    |       0      |  H   |  H  |      |     |         |        |         |   
 *     0    |       1      |  H   |  H  |      |     |    V    |    V   |         |   
 *     1    |       0      |  HV  |  HV |  V   |  V  |         |        |         |   
 *     1    |       1      |  HV  |  HV |  V   |  V  |    V    |    V   |    V    |    V
 *
 * If not DeepScan, then we are hoping to just use the CRC value
 * from the archive, and do no work here, however: If we find a header with an offset
 * then we have to calculate the altcrc for the file. (if we find a header with a zero offset then
 * the crc & the altcrc would be the same so we do not calculate the altcrc in this situation.)
 *
 * If DeepScan then we will calculate the 3 main hashes and if a header with an
 * offset is found we will also calculate the alt 3 hashes.
 *
 * (Note: If we are scanning an uncompressed file we should have DeepScan=1
 * as we would not have any CRC if we did not.)
 */

public delegate void Message(string message);

public class FileScan
{

    public ZipReturn ScanArchiveFile(FileType archiveType, string filename, long timeStamp, bool deepScan, out ScannedFile scannedArchive, bool useDosDateTime = false, bool scanSHA256 = false,  Message progress = null)
    {
        ICompress file;
        switch (archiveType)
        {
            case FileType.Zip:
                file = new StructuredZip();
                break;
            case FileType.SevenZip:
                file = new SevenZ();
                break;

            case FileType.Dir:
            default:
                deepScan = true;
                file = new File();
                break;
        }

        ZipReturn zr = file.ZipFileOpen(filename, timeStamp, true);
        if (zr != ZipReturn.ZipGood)
        {
            scannedArchive = null;
            return zr;
        }

        scannedArchive = new ScannedFile(archiveType)
        {
            Name = filename,
            ZipStruct = file.ZipStruct,
            Comment = file.FileComment
        };
        scannedArchive.AddRange(new FileScan().ScanFilesInArchive(file, deepScan, useDosDateTime, scanSHA256, progress));
        file.ZipFileClose();
        return ZipReturn.ZipGood;
    }


    public ZipReturn ScanZipStream(Stream inStream, bool deepScan, out ScannedFile scannedArchive, bool useDosDateTime = false, bool scanSHA256 = false, Message progress = null)
    {
        ICompress file = new Zip();

        file.ZipFileOpen(inStream);
        scannedArchive = new ScannedFile(FileType.Zip)
        {
            Name = "",
            ZipStruct = file.ZipStruct,
            Comment = file.FileComment
        };
        scannedArchive.AddRange(new FileScan().ScanFilesInArchive(file, deepScan, useDosDateTime, scanSHA256, progress));
        file.ZipFileClose();
        return ZipReturn.ZipGood;
    }

    private List<ScannedFile> ScanFilesInArchive(ICompress file, bool deepScan, bool useDosDateTime, bool scanSHA256, Message progress)
    {
        FileType scannedFileType;
        switch (file)
        {
            case Zip _: scannedFileType = FileType.FileZip; break;
            case SevenZ _: scannedFileType = FileType.FileSevenZip; break;
            case File _: scannedFileType = FileType.File; break;
            default: scannedFileType = FileType.UnSet; break;
        }

        ulong sizeTotal = 0;
        ulong sizeSoFar = 0;
        int fileCount = file.LocalFilesCount;
        for (int i = 0; i < fileCount; i++)
            sizeTotal += file.GetFileHeader(i).UncompressedSize;



        if (sizeTotal < 400000000)
            sizeTotal = 0;

        List<ScannedFile> lstFileResults = new List<ScannedFile>();
        for (int i = 0; i < fileCount; i++)
        {
            ScannedFile scannedFile = scanAFileInAnArchive(file, i, scannedFileType, deepScan, useDosDateTime, scanSHA256, progress, sizeTotal, sizeSoFar);
            lstFileResults.Add(scannedFile);
            sizeSoFar += file.GetFileHeader(i).UncompressedSize;
        }

        if (progress!=null && sizeTotal!=0)
            progress?.Invoke("");

        file.ZipFileCloseReadStream();
        return lstFileResults;
    }

    private ScannedFile scanAFileInAnArchive(ICompress file, int index, FileType scannedFileType, bool deepScan, bool useDosDateTime, bool scanSHA256, Message progress, ulong sizeTotal, ulong sizeSoFar)
    {
        FileHeader lf = file.GetFileHeader(index);

        ScannedFile scannedFile = new ScannedFile(scannedFileType)
        {
            Name = lf.Filename,
            DeepScanned = deepScan,
            Index = index,
            LocalHeaderOffset = lf.LocalHead,
            FileModTimeStamp = useDosDateTime ? lf.HeaderLastModified : lf.LastModified,
        };

        if (lf.IsDirectory)
        {
            scannedFile.HeaderFileType = HeaderFileType.Nothing;
            scannedFile.GotStatus = GotStatus.Got;
            scannedFile.Size = 0;
            scannedFile.CRC = new byte[] { 0, 0, 0, 0 };
            scannedFile.SHA1 = new byte[] { 0xda, 0x39, 0xa3, 0xee, 0x5e, 0x6b, 0x4b, 0x0d, 0x32, 0x55, 0xbf, 0xef, 0x95, 0x60, 0x18, 0x90, 0xaf, 0xd8, 0x07, 0x09 };
            scannedFile.SHA256 = new byte[] { 0xe3, 0xb0, 0xc4, 0x42, 0x98, 0xfc, 0x1c, 0x14, 0x9a, 0xfb, 0xf4, 0xc8, 0x99, 0x6f, 0xb9, 0x24, 0x27, 0xae, 0x41, 0xe4, 0x64, 0x9b, 0x93, 0x4c, 0xa4, 0x95, 0x99, 0x1b, 0x78, 0x52, 0xb8, 0x55 };
            scannedFile.MD5 = new byte[] { 0xd4, 0x1d, 0x8c, 0xd9, 0x8f, 0x00, 0xb2, 0x04, 0xe9, 0x80, 0x09, 0x98, 0xec, 0xf8, 0x42, 0x7e };

            scannedFile.StatusFlags |= FileStatus.CRCFromHeader | FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified | FileStatus.SHA256Verified;
            return scannedFile;
        }

        ZipReturn zr = file.ZipFileOpenReadStream(index, out Stream fStream, out ulong uSize);
        scannedFile.Size = uSize;
        scannedFile.StatusFlags |= FileStatus.SizeFromHeader;

        if (zr != ZipReturn.ZipGood)
            scannedFile.GotStatus = GotStatus.Corrupt;
        else
        {
            int res = CheckSumRead(fStream, scannedFile, lf.UncompressedSize, deepScan, scanSHA256, progress, sizeTotal, sizeSoFar);
            if (res != 0)
            {
                scannedFile.GotStatus = GotStatus.Corrupt;
                // corrupt zip should still returns its CRC, otherwise the corrupt file will not be push out to ToSort on a fix.
                if (scannedFile.CRC == null)
                {
                    scannedFile.CRC = lf.CRC;
                    scannedFile.StatusFlags |= FileStatus.CRCFromHeader;
                }
            }
            else
            {
                // if we are not testcrc'ing or deepScan'ing then we did not verify the data stream
                // so we assume it is good.
                if (!deepScan)
                {
                    scannedFile.CRC = lf.CRC;
                    scannedFile.StatusFlags |= FileStatus.CRCFromHeader;
                }

                if (lf.CRC == null)
                {
                    scannedFile.StatusFlags |= FileStatus.SizeVerified;
                    scannedFile.GotStatus = GotStatus.Got;
                }
                else if (ByteArrCompare(lf.CRC, scannedFile.CRC))
                {
                    scannedFile.StatusFlags |= FileStatus.SizeVerified;
                    scannedFile.StatusFlags |= FileStatus.CRCFromHeader;
                    scannedFile.GotStatus = GotStatus.Got;
                }
                else
                {
                    scannedFile.GotStatus = GotStatus.Corrupt;
                }
            }
        }

        return scannedFile;
    }


    private const int Buffersize = 4096 * 1024;
    private static BlockingCollection<byte[]> bytebuffer = new BlockingCollection<byte[]>();
    static byte[] getbuffer()
    {
        if (bytebuffer.TryTake(out byte[] buffer))
            return buffer;

        return new byte[Buffersize];
    }
    static void putbuffer(byte[] buffer)
    {
        bytebuffer.Add(buffer);
    }

    public int CheckSumRead(Stream inStream, ScannedFile scannedFile, ulong totalSize, bool fullScan, bool scanSHA256, Message progress, ulong sizetotal, ulong sizeSoFar)
    {
        byte[] _buffer0 = getbuffer();
        byte[] _buffer1 = getbuffer();

        scannedFile.MD5 = null;
        scannedFile.SHA1 = null;
        scannedFile.CRC = null;
        scannedFile.SHA256 = null;

        ThreadReadBuffer lbuffer = null;

        ThreadCRC tcrc32 = null;
        ThreadMD5 tmd5 = null;
        ThreadSHA1 tsha1 = null;
        ThreadSHA256 tsha256 = null;

        ThreadCRC altcrc32 = null;
        ThreadMD5 atlmd5 = null;
        ThreadSHA1 altsha1 = null;
        ThreadSHA256 altsha256 = null;

        try
        {
            int maxHeaderSize = 128;
            long sizetogo = (long)totalSize;
            int sizenow = maxHeaderSize < sizetogo ? maxHeaderSize : (int)sizetogo;
            if (sizenow > 0)
                inStream.Read(_buffer0, 0, sizenow);

            scannedFile.HeaderFileType = FileHeaderReader.GetType(_buffer0, sizenow, out int actualHeaderSize);

            // if the file has no header then just use the main hash checkers.
            if (scannedFile.HeaderFileType == HeaderFileType.Nothing || actualHeaderSize == 0)
            {
                // no header found & not reading hashes.
                if (!fullScan)
                {
                    putbuffer(_buffer0);
                    putbuffer(_buffer1);
                    return 0;
                }
                // no header found so just push the initial buffer read into the hash checkers.
                // and then continue with the rest of the file.
                tcrc32 = new ThreadCRC();
                tmd5 = new ThreadMD5();
                tsha1 = new ThreadSHA1();
                if (scanSHA256) tsha256 = new ThreadSHA256();
                tcrc32.Trigger(_buffer0, sizenow);
                tmd5?.Trigger(_buffer0, sizenow);
                tsha1?.Trigger(_buffer0, sizenow);
                tsha256?.Trigger(_buffer0, sizenow);
                tcrc32.Wait();
                tmd5?.Wait();
                tsha1?.Wait();
                tsha256?.Wait();

                sizetogo -= sizenow;
            }
            else
            {
                // header found
                scannedFile.StatusFlags |= FileStatus.HeaderFileTypeFromHeader;
                scannedFile.AltSize = (ulong)((long)totalSize - actualHeaderSize);
                scannedFile.StatusFlags |= FileStatus.AltSizeFromHeader | FileStatus.AltSizeVerified;

                //setup main hash checkers
                altcrc32 = new ThreadCRC();
                if (fullScan)
                {
                    tcrc32 = new ThreadCRC();
                    tmd5 = new ThreadMD5();
                    tsha1 = new ThreadSHA1();
                    atlmd5 = new ThreadMD5();
                    altsha1 = new ThreadSHA1();
                    if (scanSHA256) tsha256 = new ThreadSHA256();
                }

                if (sizenow > actualHeaderSize)
                {
                    // Already read more than the header, so we need to split what we read into the 2 hash checkers

                    // first scan the header part from what we have already read.
                    // scan what we read so far with just the main hashers
                    tcrc32?.Trigger(_buffer0, actualHeaderSize);
                    tmd5?.Trigger(_buffer0, actualHeaderSize);
                    tsha1?.Trigger(_buffer0, actualHeaderSize);
                    tsha256?.Trigger(_buffer0, sizenow);
                    tcrc32?.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
                    tsha256?.Wait();

                    // put the rest of what we read into the second buffer, and scan with all hashers
                    int restSize = sizenow - actualHeaderSize;
                    for (int i = 0; i < restSize; i++)
                    {
                        _buffer1[i] = _buffer0[actualHeaderSize + i];
                    }
                    tcrc32?.Trigger(_buffer1, restSize);
                    tmd5?.Trigger(_buffer1, restSize);
                    tsha1?.Trigger(_buffer1, restSize);
                    tsha256?.Trigger(_buffer1, sizenow);
                    altcrc32.Trigger(_buffer1, restSize);
                    atlmd5?.Trigger(_buffer1, restSize);
                    altsha1?.Trigger(_buffer1, restSize);
                    altsha256?.Trigger(_buffer1, restSize);

                    tcrc32?.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
                    tsha256?.Wait();
                    altcrc32.Wait();
                    atlmd5?.Wait();
                    altsha1?.Wait();
                    altsha256?.Wait();

                    sizetogo -= sizenow;
                }
                else
                {
                    // Read less than the length of the header so read the rest of the header.
                    // then continue to reader the full rest of the file.

                    // scan what we read so far
                    tcrc32?.Trigger(_buffer0, sizenow);
                    tmd5?.Trigger(_buffer0, sizenow);
                    tsha1?.Trigger(_buffer0, sizenow);
                    tsha256?.Trigger(_buffer0, sizenow);
                    tcrc32?.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
                    tsha256?.Wait();

                    sizetogo -= sizenow;

                    // now read the rest of the header.
                    sizenow = actualHeaderSize - sizenow;
                    inStream.Read(_buffer0, 0, sizenow);

                    // scan the rest of the header
                    tcrc32?.Trigger(_buffer0, sizenow);
                    tmd5?.Trigger(_buffer0, sizenow);
                    tsha1?.Trigger(_buffer0, sizenow);
                    tsha256?.Trigger(_buffer0, sizenow);
                    tcrc32?.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
                    tsha256?.Wait();

                    sizetogo -= sizenow;
                }
            }

            lbuffer = new ThreadReadBuffer(inStream);

            // Pre load the first buffer0
            int sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
            if (sizeNext > 0)
                inStream.Read(_buffer0, 0, sizeNext);

            int sizebuffer = sizeNext;
            sizetogo -= sizeNext;
            bool whichBuffer = true;

            bool doReporting = progress != null && sizetotal > 0;
            int persentReporting = -1;
            if (doReporting)
            {
                int persentNow = (int)(sizeSoFar * 100 / sizetotal);
                progress?.Invoke($"Hashing: {persentNow}%");
                persentReporting = persentNow;
            }

            while (sizebuffer > 0 && !lbuffer.errorState)
            {
                sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;

                if (sizeNext > 0)
                {
                    lbuffer.Trigger(whichBuffer ? _buffer1 : _buffer0, sizeNext);
                }


                if (doReporting)
                {
                    int persentNow = (int)((sizeSoFar + totalSize - (ulong)sizetogo) * 100 / sizetotal);
                    if (persentNow > persentReporting)
                    {
                        progress?.Invoke($"Hashing: {persentNow}%");
                        persentReporting = persentNow;
                    }
                }


                byte[] buffer = whichBuffer ? _buffer0 : _buffer1;
                tcrc32?.Trigger(buffer, sizebuffer);
                tmd5?.Trigger(buffer, sizebuffer);
                tsha1?.Trigger(buffer, sizebuffer);
                tsha256?.Trigger(buffer, sizebuffer);

                altcrc32?.Trigger(buffer, sizebuffer);
                atlmd5?.Trigger(buffer, sizebuffer);
                altsha1?.Trigger(buffer, sizebuffer);
                altsha256?.Trigger(buffer, sizebuffer);


                if (sizeNext > 0)
                {
                    lbuffer.Wait();
                }
                tcrc32?.Wait();
                tmd5?.Wait();
                tsha1?.Wait();
                tsha256?.Wait();
                altcrc32?.Wait();
                atlmd5?.Wait();
                altsha1?.Wait();
                altsha256?.Wait();

                sizebuffer = sizeNext;
                sizetogo -= sizeNext;
                whichBuffer = !whichBuffer;
            }

            lbuffer.Finish();
            tcrc32?.Finish();
            tmd5?.Finish();
            tsha1?.Finish();
            tsha256?.Finish();
            altcrc32?.Finish();
            atlmd5?.Finish();
            altsha1?.Finish();
            altsha256?.Finish();
        }
        catch
        {
            lbuffer?.Dispose();
            tcrc32?.Dispose();
            tmd5?.Dispose();
            tsha1?.Dispose();
            tsha256?.Dispose();
            altcrc32?.Dispose();
            atlmd5?.Dispose();
            altsha1?.Dispose();
            altsha256?.Dispose();

            putbuffer(_buffer0);
            putbuffer(_buffer1);

            return 0x17; // need to remember what this number is for
        }

        if (lbuffer.errorState)
        {
            lbuffer.Dispose();
            tcrc32?.Dispose();
            tmd5?.Dispose();
            tsha1?.Dispose();
            tsha256?.Dispose();
            altcrc32?.Dispose();
            atlmd5?.Dispose();
            altsha1?.Dispose();
            altsha256?.Dispose();

            putbuffer(_buffer0);
            putbuffer(_buffer1);

            return 0x17; // need to remember what this number is for
        }

        if (tcrc32 != null) scannedFile.StatusFlags |= FileStatus.CRCVerified;
        if (tmd5 != null) scannedFile.StatusFlags |= FileStatus.MD5Verified;
        if (tsha1 != null) scannedFile.StatusFlags |= FileStatus.SHA1Verified;
        if (tsha256 != null) scannedFile.StatusFlags |= FileStatus.SHA256Verified;
        if (altcrc32 != null) scannedFile.StatusFlags |= FileStatus.AltCRCVerified;
        if (atlmd5 != null) scannedFile.StatusFlags |= FileStatus.AltMD5Verified;
        if (altsha1 != null) scannedFile.StatusFlags |= FileStatus.AltSHA1Verified;
        if (altsha256 != null) scannedFile.StatusFlags |= FileStatus.AltSHA256Verified;

        scannedFile.CRC = tcrc32?.Hash;
        scannedFile.MD5 = tmd5?.Hash;
        scannedFile.SHA1 = tsha1?.Hash;
        scannedFile.SHA256 = tsha256?.Hash;
        scannedFile.AltCRC = altcrc32?.Hash;
        scannedFile.AltMD5 = atlmd5?.Hash;
        scannedFile.AltSHA1 = altsha1?.Hash;
        scannedFile.AltSHA256 = altsha256?.Hash;

        lbuffer.Dispose();
        tcrc32?.Dispose();
        tmd5?.Dispose();
        tsha1?.Dispose();
        tsha256?.Dispose();
        altcrc32?.Dispose();
        atlmd5?.Dispose();
        altsha1?.Dispose();
        altsha256?.Dispose();

        putbuffer(_buffer0);
        putbuffer(_buffer1);

        return 0;
    }




    private static bool ByteArrCompare(byte[] b0, byte[] b1)
    {
        if (b0 == null || b1 == null)
        {
            return false;
        }
        if (b0.Length != b1.Length)
        {
            return false;
        }

        for (int i = 0; i < b0.Length; i++)
        {
            if (b0[i] != b1[i])
            {
                return false;
            }
        }
        return true;
    }
}
