using System;
using System.IO;
using Compress.Utils;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace Compress.ZipFile
{
    public partial class Zip
    {

        public ZipReturn ZipFileOpen(string newFilename, long timestamp, bool readHeaders)
        {
            ZipFileClose();
            ZipStatus = ZipStatus.None;
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
                int errorCode = FileStream.OpenFileRead(newFilename, out _zipFs);
                if (errorCode != 0)
                {
                    ZipFileClose();
                    if (errorCode == 32)
                    {
                        return ZipReturn.ZipFileLocked;
                    }
                    return ZipReturn.ZipErrorOpeningFile;
                }
            }
            catch (PathTooLongException)
            {
                ZipFileClose();
                return ZipReturn.ZipFileNameToLong;
            }
            catch (IOException)
            {
                ZipFileClose();
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
            ZipStatus = ZipStatus.None;
            _zip64 = false;
            _centralDirStart = 0;
            _centralDirSize = 0;
            _zipFileInfo = null;
            _zipFs = inStream;

            ZipOpen = ZipOpenType.OpenRead;
            return ZipFileReadHeaders();
        }


        private void zipFileCloseRead()
        {
            if (_zipFs != null)
            {
                _zipFs.Close();
                _zipFs.Dispose();
            }
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
                    _zipFs.Position = (long)_endOfCenterDir64;
                    zRet = Zip64EndOfCentralDirRead();
                    if (zRet == ZipReturn.ZipGood)
                    {
                        _zip64 = true;
                        endOfCentralDir = _endOfCenterDir64;
                    }
                }

                if (zip64Required && !_zip64)
                {
                    return ZipReturn.Zip64EndOfCentralDirError;
                }

                bool trrntzip = false;

                // check if the ZIP has a valid TorrentZip file comment
                if (_fileComment.Length == 22)
                {
                    if (ZipUtils.GetString(_fileComment).Substring(0, 14) == "TORRENTZIPPED-")
                    {
                        CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true);
                        byte[] buffer = new byte[_centralDirSize];
                        _zipFs.Position = (long)_centralDirStart;
                        crcCs.Read(buffer, 0, (int)_centralDirSize);
                        crcCs.Flush();
                        crcCs.Close();

                        uint r = (uint)crcCs.Crc;
                        crcCs.Dispose();

                        string tcrc = ZipUtils.GetString(_fileComment).Substring(14, 8);
                        string zcrc = r.ToString("X8");
                        if (string.Compare(tcrc, zcrc, StringComparison.Ordinal) == 0)
                        {
                            trrntzip = true;
                        }
                    }
                }
                
                if (zip64Required != _zip64)
                    trrntzip = false;

                // now read the central directory
                _zipFs.Position = (long)_centralDirStart;

                _localFiles.Clear();
                _localFiles.Capacity = (int)_localFilesCount;
                for (int i = 0; i < _localFilesCount; i++)
                {
                    LocalFile lc = new LocalFile();
                    zRet = lc.CenteralDirectoryRead(_zipFs);
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    _zip64 |= lc.Zip64;
                    _localFiles.Add(lc);
                }

                for (int i = 0; i < _localFilesCount; i++)
                {
                    zRet = _localFiles[i].LocalFileHeaderRead(_zipFs);
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    trrntzip &= _localFiles[i].TrrntZip;
                }

                // check trrntzip file order
                if (trrntzip)
                {
                    for (int i = 0; i < _localFilesCount - 1; i++)
                    {
                        if (ZipUtils.TrrntZipStringCompare(_localFiles[i].FileName, _localFiles[i + 1].FileName) < 0)
                        {
                            continue;
                        }
                        trrntzip = false;
                        break;
                    }
                }

                // check trrntzip directories
                if (trrntzip)
                {
                    for (int i = 0; i < _localFilesCount - 1; i++)
                    {
                        // see if we found a directory
                        string filename0 = _localFiles[i].FileName;
                        if (filename0.Substring(filename0.Length - 1, 1) != "/")
                        {
                            continue;
                        }

                        // see if the next file is in that directory
                        string filename1 = _localFiles[i + 1].FileName;
                        if (filename1.Length <= filename0.Length)
                        {
                            continue;
                        }
                        if (ZipUtils.TrrntZipStringCompare(filename0, filename1.Substring(0, filename0.Length)) != 0)
                        {
                            continue;
                        }

                        // if we found a file in the directory then we do not need the directory entry
                        trrntzip = false;
                        break;
                    }
                }

                if (trrntzip)
                {
                    ZipStatus |= ZipStatus.TrrntZip;
                }

                return ZipReturn.ZipGood;
            }
            catch
            {
                ZipFileClose();
                return ZipReturn.ZipErrorReadingFile;
            }
        }

    }
}
