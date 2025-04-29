using System.IO;
using Compress.Support.Utils;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace Compress.ZipFile
{
    public partial class Zip
    {
        public ZipReturn ZipFileOpen(string newFilename, long timestamp, bool readHeaders, int buffer = 4096)
        {
            ZipFileClose();
            _zip64 = false;
            _centralDirStart = 0;
            _centralDirSize = 0;
            _zipFileInfo = null;

            try
            {
                if (!RVIO.File.Exists(newFilename))
                {
                    ZipFileClose();
                    return ZipReturn.ZipErrorFileNotFound;
                }
                _zipFileInfo = new FileInfo(newFilename);
                if (timestamp != -1 && _zipFileInfo.LastWriteTime != timestamp)
                {
                    ZipFileClose();
                    return ZipReturn.ZipErrorTimeStamp;
                }
                int ErrorCode = FileStream.OpenFileRead(newFilename, buffer, out _zipFs);
                if (ErrorCode != 0)
                {
                    Error.ErrorCode = ErrorCode;
                    Error.ErrorMessage = RVIO.Error.ErrorMessage;
                    ZipFileClose();
                    if (ErrorCode == -2147024864)
                    {
                        return ZipReturn.ZipFileLocked;
                    }
                    return ZipReturn.ZipErrorOpeningFile;
                }
            }
            catch (IOException e)
            {
                ZipFileClose();
                Error.ErrorCode = e.HResult;
                Error.ErrorMessage = e.Message;
                return ZipReturn.ZipErrorOpeningFile;
            }
            ZipOpen = ZipOpenType.OpenRead;

            if (!readHeaders)
            {
                return ZipReturn.ZipGood;
            }


            return ZipFileReadHeaders();
        }


        public ZipReturn ZipFileOpen(Stream inStream)
        {
            ZipFileClose();
            _zip64 = false;
            _centralDirStart = 0;
            _centralDirSize = 0;
            _zipFileInfo = null;
            _zipFs = inStream;

            ZipOpen = ZipOpenType.OpenRead;
            return ZipFileReadHeaders();
        }

        internal void zipFileCloseRead()
        {
            if (_zipFs != null)
            {
                _zipFs.Close();
                _zipFs.Dispose();
            }
            ZipOpen = ZipOpenType.Closed;
        }

        internal void zipFileCloseWrite()
        {
            _zipFs.SetLength(_zipFs.Position);
            _zipFs.Flush();
            _zipFs.Close();
            _zipFs.Dispose();
            _zipFileInfo = _zipFileInfo == null ? null : new FileInfo(_zipFileInfo.FullName);

            ZipOpen = ZipOpenType.Closed;
        }


        private ZipReturn ZipFileReadHeaders()
        {
            try
            {
                ZipReturn zRet = FindEndOfCentralDirSignature();
                if (zRet != ZipReturn.ZipGood)
                {
                    ZipFileClose();
                    return zRet;
                }

                ulong endOfCentralDir = (ulong)_zipFs.Position;
                zRet = EndOfCentralDirRead();
                if (zRet != ZipReturn.ZipGood)
                {
                    ZipFileClose();
                    return zRet;
                }

                // check if ZIP64 header is required
                bool zip64Required = (_centralDirStart == 0xffffffff || _centralDirSize == 0xffffffff || _localFilesCount == 0xffff);

                // check for a ZIP64 header
                _zipFs.Position = (long)endOfCentralDir - 20;
                zRet = Zip64EndOfCentralDirectoryLocatorRead();
                if (zRet == ZipReturn.ZipGood)
                {
                    _zipFs.Position = (long)_endOfCentralDir64;
                    zRet = Zip64EndOfCentralDirRead();
                    if (zRet == ZipReturn.ZipGood)
                    {
                        _zip64 = true;
                        endOfCentralDir = _endOfCentralDir64;
                    }
                }

                if (zip64Required && !_zip64)
                {
                    ZipFileClose();
                    return ZipReturn.Zip64EndOfCentralDirError;
                }

                offset = (endOfCentralDir - _centralDirSize) - _centralDirStart;

                _centralDirStart += offset;

                // now read the central directory
                _zipFs.Position = (long)_centralDirStart;

                _HeadersCentralDir.Clear();
                _HeadersLocalFile.Clear();
                _HeadersCentralDir.Capacity = (int)_localFilesCount;
                _HeadersLocalFile.Capacity = (int)_localFilesCount;
                for (int i = 0; i < _localFilesCount; i++)
                {
                    zRet = ZipFileData.CentralDirectoryRead(_zipFs, offset, out ZipFileData headerCentralDir);
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    _HeadersCentralDir.Add(headerCentralDir);
                }

                for (int i = 0; i < _localFilesCount; i++)
                {
                    zRet = ZipFileData.LocalFileHeaderRead(_zipFs, _HeadersCentralDir[i], out ZipFileData headerLocalFile);
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    _HeadersLocalFile.Add(headerLocalFile);
                }


                for (int i = 0; i < _localFilesCount; i++)
                {
                    zRet = ValidateFileHeaders(_HeadersCentralDir[i], _HeadersLocalFile[i]);
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    _HeadersCentralDir[i].DataLocation = _HeadersLocalFile[i].DataLocation;
                }

                return ZipReturn.ZipGood;
            }
            catch
            {
                ZipFileClose();
                return ZipReturn.ZipErrorReadingFile;
            }
        }
        private static ZipReturn ValidateFileHeaders(ZipFileData HeaderCentral, ZipFileData HeaderLocal)
        {
            HeaderCentral.ClearStatus();

            if (HeaderCentral.GeneralPurposeBitFlag != HeaderLocal.GeneralPurposeBitFlag)
                HeaderCentral.SetStatus(LocalFileStatus.HeadersMismatch);

            if (HeaderCentral.CompressionMethod != HeaderLocal.CompressionMethod)
                return ZipReturn.ZipLocalFileHeaderError;

            if (HeaderCentral.HeaderLastModified != HeaderLocal.HeaderLastModified)
                HeaderCentral.SetStatus(LocalFileStatus.DateTimeMisMatch);

            if (!CompressUtils.CompareStringSlash(HeaderCentral.Filename.ToLower(), HeaderLocal.Filename.ToLower()))
                HeaderCentral.SetStatus(LocalFileStatus.FilenameMisMatch);

            if (!CompressUtils.ByteArrCompare(HeaderCentral.CRC, HeaderLocal.CRC))
                return ZipReturn.ZipLocalFileHeaderError;

            if (HeaderCentral.CompressedSize != HeaderLocal.CompressedSize)
                return ZipReturn.ZipLocalFileHeaderError;

            if (HeaderCentral.UncompressedSize != HeaderLocal.UncompressedSize)
                return ZipReturn.ZipLocalFileHeaderError;

            if (HeaderCentral.IsDirectory && HeaderCentral.UncompressedSize != 0)
                HeaderCentral.SetStatus(LocalFileStatus.DirectoryLengthError);

            /*
             4.4.5 compression method: (2 bytes)

             0 - (Supported) The file is stored (no compression)
             1 - (Supported) The file is Shrunk
             2 - (Supported) The file is Reduced with compression factor 1
             3 - (Supported) The file is Reduced with compression factor 2
             4 - (Supported) The file is Reduced with compression factor 3
             5 - (Supported) The file is Reduced with compression factor 4
             6 - (Supported) The file is Imploded
             7 - Reserved for Tokenizing compression algorithm
             8 - (Supported) The file is Deflated
             9 - (Supported) Enhanced Deflating using Deflate64(tm)
            10 - PKWARE Data Compression Library Imploding (old IBM TERSE)
            11 - Reserved by PKWARE
            12 - (Supported) File is compressed using BZIP2 algorithm
            13 - Reserved by PKWARE
            14 - (Supported) LZMA
            15 - Reserved by PKWARE
            16 - IBM z/OS CMPSC Compression
            17 - Reserved by PKWARE
            18 - File is compressed using IBM TERSE (new)
            19 - IBM LZ77 z Architecture 
            20 - deprecated (use method 93 for zstd)
            93 - Zstandard (zstd) Compression 
            94 - MP3 Compression 
            95 - XZ Compression 
            96 - JPEG variant
            97 - WavPack compressed data
            98 - (Supported) PPMd version I, Rev 1
            99 - AE-x encryption marker (see APPENDIX E)
           */

            switch (HeaderCentral.CompressionMethod)
            {
                case 0: // The file is stored (no compression)
                case 1: // The file is Shrunk
                case 2: // The file is Reduced with compression factor 1
                case 3: // The file is Reduced with compression factor 2
                case 4: // The file is Reduced with compression factor 3
                case 5: // The file is Reduced with compression factor 4
                case 6: // The file is Imploded
                case 8: // The file is Deflated
                case 9: // Enhanced Deflating using Deflate64(tm)
                case 12: // The file is BZIP2 algorithm. 
                case 14: // LZMA
                case 20:
                case 93: // Zstandard (zstd) Compression 
                case 98: // PPMd version I, Rev 1
                    break;

                default:
                    return ZipReturn.ZipUnsupportedCompression;
            }

            return ZipReturn.ZipGood;
        }

        internal string GetCRC()
        {
            using CrcCalculatorStream crcCs = new(_zipFs, true);
            byte[] buffer = new byte[_centralDirSize];
            _zipFs.Position = (long)_centralDirStart;
            crcCs.Read(buffer, 0, (int)_centralDirSize);
            crcCs.Flush();
            crcCs.Close();

            uint r = (uint)crcCs.Crc;

            return r.ToString("X8");
        }

    }
}
