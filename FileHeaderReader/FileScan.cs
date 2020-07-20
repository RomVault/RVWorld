using System.Collections.Generic;
using System.IO;
using Compress;
using Compress.ThreadReaders;

namespace FileHeaderReader
{


    /*
    need to do some checking for IsDirectory

          _localFiles[index].MD5 = new byte[]
                        {0xd4, 0x1d, 0x8c, 0xd9, 0x8f, 0x00, 0xb2, 0x04, 0xe9, 0x80, 0x09, 0x98, 0xec, 0xf8, 0x42, 0x7e};
                    _localFiles[index].SHA1 = new byte[]
                    {
                        0xda, 0x39, 0xa3, 0xee, 0x5e, 0x6b, 0x4b, 0x0d, 0x32, 0x55, 0xbf, 0xef, 0x95, 0x60, 0x18, 0x90,
                        0xaf, 0xd8, 0x07, 0x09
                    };
                    _localFiles[index].FileStatus = ZipReturn.ZipGood;

    */




    /*
     * logic to understand bool testcrc & bool deepScan
     * ------------------------------------------------
     *
     * testcrc | deepScan | Has a Header | crc | sha1 | md5 | altcrc | sha1 | md5
     * ----------------------------------------------------------------------------
     *    0    |    0     |       0      |  0  |  0   |  0  |   0    |  0   |  0
     *    0    |    0     |       1      |  0  |  0   |  0  |   1    |  0   |  0
     *    1    |    0     |       0      |  1  |  0   |  0  |   0    |  0   |  0
     *    1    |    0     |       1      |  1  |  0   |  0  |   1    |  0   |  0
     *    x    |    1     |       0      |  1  |  1   |  1  |   0    |  0   |  0
     *    x    |    1     |       1      |  1  |  1   |  1  |   1    |  1   |  1
     *
     * If testcrc & deepScan are false, then we are hoping to just use the CRC value
     * from the archive, and do no work here, however: If we find a header with an offset
     * then we have to calculate the altcrc for the file. (if we find a header with a zero offset then
     * the crc & the altcrc would be the same so we do not calculate the altcrc in this situation.)
     *
     * If testcrc is true then we will calculate the CRC value and if a header with an
     * offset is found we will also calulate the altCRC.
     *
     * If DeepScan is true then we will calulcate the 3 main hashes and if a header with an
     * offset is found we will also calulate the alt 3 hashes.
     *
     * (Note: If we are scanning an uncompressed file we should at least have testCRC set to true
     * as we would not have any CRC if we did not.)
     */

    public class FileScan
    {

        private const int Buffersize = 4096 * 1024;
        private byte[] _buffer0;
        private byte[] _buffer1;


        public List<FileResults> Scan(ICompress file, bool testcrc, bool deepScan)
        {
            List<FileResults> lstFileResults = new List<FileResults>();
            int fileCount = file.LocalFilesCount();
            for (int i = 0; i < fileCount; i++)
            {
                FileResults fileResults = new FileResults();
                if (file.IsDirectory(i))
                {
                    fileResults.HeaderFileType = HeaderFileType.Nothing;
                    fileResults.FileStatus = ZipReturn.ZipGood;
                    fileResults.Size = 0;
                    fileResults.CRC = new byte[] { 0, 0, 0, 0 };
                    fileResults.SHA1 = new byte[] { 0xda, 0x39, 0xa3, 0xee, 0x5e, 0x6b, 0x4b, 0x0d, 0x32, 0x55, 0xbf, 0xef, 0x95, 0x60, 0x18, 0x90, 0xaf, 0xd8, 0x07, 0x09 };
                    fileResults.MD5 = new byte[] { 0xd4, 0x1d, 0x8c, 0xd9, 0x8f, 0x00, 0xb2, 0x04, 0xe9, 0x80, 0x09, 0x98, 0xec, 0xf8, 0x42, 0x7e };

                    lstFileResults.Add(fileResults);
                    continue;
                }

                ZipReturn zr = file.ZipFileOpenReadStream(i, out Stream fStream, out fileResults.Size);
                if (zr != ZipReturn.ZipGood)
                    fileResults.FileStatus = zr;
                else
                {
                    int res = CheckSumRead(fStream, fileResults, file.UncompressedSize(i), testcrc, deepScan);
                    if (res != 0)
                        fileResults.FileStatus = ZipReturn.ZipDecodeError;
                    else
                    {
                        if (!testcrc)
                            fileResults.CRC = file.CRC32(i);
                        // if we are not testcrc'ing or deepScan'ing then we did not verify the data stream
                        // so we assume it is good.
                        fileResults.FileStatus =
                            !(testcrc || deepScan) || ByteArrCompare(file.CRC32(i), fileResults.CRC)
                                ? ZipReturn.ZipGood
                                : ZipReturn.ZipCRCDecodeError;
                    }
                }

                lstFileResults.Add(fileResults);
            }
            file.ZipFileCloseReadStream();
            return lstFileResults;
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

        private int CheckSumRead(Stream inStream, FileResults fileResults, ulong totalSize, bool testcrc, bool testDeep)
        {
            if (_buffer0 == null)
            {
                _buffer0 = new byte[Buffersize];
                _buffer1 = new byte[Buffersize];
            }

            fileResults.MD5 = null;
            fileResults.SHA1 = null;
            fileResults.CRC = null;

            ThreadLoadBuffer lbuffer = null;

            ThreadCRC tcrc32 = null;
            ThreadMD5 tmd5 = null;
            ThreadSHA1 tsha1 = null;

            ThreadCRC altCrc32 = null;
            ThreadMD5 altMd5 = null;
            ThreadSHA1 altSha1 = null;

            try
            {
                int maxHeaderSize = 128;
                long sizetogo = (long)totalSize;
                int sizenow = maxHeaderSize < sizetogo ? maxHeaderSize : (int)sizetogo;
                if (sizenow>0)
                    inStream.Read(_buffer0, 0, sizenow);
    
                fileResults.HeaderFileType = FileHeaderReader.GetType(_buffer0, sizenow, out int actualHeaderSize);


                // if the file has no header then just use the main hash checkers.
                if (fileResults.HeaderFileType == HeaderFileType.Nothing || actualHeaderSize == 0)
                {
                    // no header found & not reading hashes.
                    if (!(testcrc || testDeep))
                        return 0;

                    // no header found so just push the initial buffer read into the hash checkers.
                    // and then continue with the rest of the file.
                    tcrc32 = new ThreadCRC();
                    if (testDeep)
                    {
                        tmd5 = new ThreadMD5();
                        tsha1 = new ThreadSHA1();
                    }
                    tcrc32.Trigger(_buffer0, sizenow);
                    tmd5?.Trigger(_buffer0, sizenow);
                    tsha1?.Trigger(_buffer0, sizenow);
                    tcrc32.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();

                    sizetogo -= sizenow;
                }
                else
                {
                    // header found
                    fileResults.AltSize = (ulong)((long)totalSize - actualHeaderSize);
                    //setup main hash checkers
                    if (testcrc || testDeep)
                        tcrc32 = new ThreadCRC();

                    altCrc32 = new ThreadCRC();
                    if (testDeep)
                    {
                        tmd5 = new ThreadMD5();
                        tsha1 = new ThreadSHA1();
                        altMd5 = new ThreadMD5();
                        altSha1 = new ThreadSHA1();
                    }

                    if (sizenow > actualHeaderSize)
                    {
                        // Already read more than the header, so we need to split what we read into the 2 hash checkers

                        // first scan the header part from what we have already read.
                        // scan what we read so far with just the main hashers
                        tcrc32?.Trigger(_buffer0, actualHeaderSize);
                        tmd5?.Trigger(_buffer0, actualHeaderSize);
                        tsha1?.Trigger(_buffer0, actualHeaderSize);
                        tcrc32?.Wait();
                        tmd5?.Wait();
                        tsha1?.Wait();

                        // put the rest of what we read into the second buffer, and scan with all hashers
                        int restSize = sizenow - actualHeaderSize;
                        for (int i = 0; i < restSize; i++)
                        {
                            _buffer1[i] = _buffer0[actualHeaderSize + i];
                        }
                        tcrc32?.Trigger(_buffer1, restSize);
                        tmd5?.Trigger(_buffer1, restSize);
                        tsha1?.Trigger(_buffer1, restSize);
                        altCrc32.Trigger(_buffer1, restSize);
                        altMd5?.Trigger(_buffer1, restSize);
                        altSha1?.Trigger(_buffer1, restSize);

                        tcrc32?.Wait();
                        tmd5?.Wait();
                        tsha1?.Wait();
                        altCrc32.Wait();
                        altMd5?.Wait();
                        altSha1?.Wait();

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
                        tcrc32?.Wait();
                        tmd5?.Wait();
                        tsha1?.Wait();

                        sizetogo -= sizenow;

                        // now read the rest of the header.
                        sizenow = actualHeaderSize - sizenow;
                        inStream.Read(_buffer0, 0, sizenow);

                        // scan the rest of the header
                        tcrc32?.Trigger(_buffer0, sizenow);
                        tmd5?.Trigger(_buffer0, sizenow);
                        tsha1?.Trigger(_buffer0, sizenow);
                        tcrc32?.Wait();
                        tmd5?.Wait();
                        tsha1?.Wait();

                        sizetogo -= sizenow;
                    }
                }

                lbuffer = new ThreadLoadBuffer(inStream);

                // Pre load the first buffer0
                int sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
                if(sizeNext>0)
                    inStream.Read(_buffer0, 0, sizeNext);

                int sizebuffer = sizeNext;
                sizetogo -= sizeNext;
                bool whichBuffer = true;

                while (sizebuffer > 0 && !lbuffer.errorState)
                {
                    sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;

                    if (sizeNext > 0)
                    {
                        lbuffer.Trigger(whichBuffer ? _buffer1 : _buffer0, sizeNext);
                    }

                    byte[] buffer = whichBuffer ? _buffer0 : _buffer1;
                    tcrc32?.Trigger(buffer, sizebuffer);
                    tmd5?.Trigger(buffer, sizebuffer);
                    tsha1?.Trigger(buffer, sizebuffer);

                    altCrc32?.Trigger(buffer, sizebuffer);
                    altMd5?.Trigger(buffer, sizebuffer);
                    altSha1?.Trigger(buffer, sizebuffer);


                    if (sizeNext > 0)
                    {
                        lbuffer.Wait();
                    }
                    tcrc32?.Wait();
                    tmd5?.Wait();
                    tsha1?.Wait();
                    altCrc32?.Wait();
                    altMd5?.Wait();
                    altSha1?.Wait();

                    sizebuffer = sizeNext;
                    sizetogo -= sizeNext;
                    whichBuffer = !whichBuffer;
                }

                lbuffer.Finish();
                tcrc32?.Finish();
                tmd5?.Finish();
                tsha1?.Finish();
                altCrc32?.Finish();
                altMd5?.Finish();
                altSha1?.Finish();


            }
            catch
            {
                lbuffer?.Dispose();
                tcrc32?.Dispose();
                tmd5?.Dispose();
                tsha1?.Dispose();
                altCrc32?.Dispose();
                altMd5?.Dispose();
                altSha1?.Dispose();

                return 0x17; // need to remember what this number is for
            }

            if (lbuffer.errorState)
            {
                lbuffer.Dispose();
                tcrc32?.Dispose();
                tmd5?.Dispose();
                tsha1?.Dispose();
                altCrc32?.Dispose();
                altMd5?.Dispose();
                altSha1?.Dispose();

                return 0x17; // need to remember what this number is for
            }

            fileResults.CRC = tcrc32?.Hash;
            fileResults.SHA1 = tsha1?.Hash;
            fileResults.MD5 = tmd5?.Hash;
            fileResults.AltCRC = altCrc32?.Hash;
            fileResults.AltSHA1 = altSha1?.Hash;
            fileResults.AltMD5 = altMd5?.Hash;

            lbuffer.Dispose();
            tcrc32?.Dispose();
            tmd5?.Dispose();
            tsha1?.Dispose();
            altCrc32?.Dispose();
            altMd5?.Dispose();
            altSha1?.Dispose();

            return 0;
        }




        public class FileResults
        {
            public HeaderFileType HeaderFileType;
            public ZipReturn FileStatus;
            public ulong Size;
            public byte[] CRC;
            public byte[] SHA1;
            public byte[] MD5;

            public ulong? AltSize;
            public byte[] AltCRC;
            public byte[] AltSHA1;
            public byte[] AltMD5;
        }
    }
}
