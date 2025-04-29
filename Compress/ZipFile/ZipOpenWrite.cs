using Compress.Support.Utils;
using System.IO;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;


namespace Compress.ZipFile
{

    public partial class Zip
    {
        // OutType of Trrntzip forces that we must have a trrtnzip file made
        // OutType of None will still make a trrntzip file if everything was supplied in trrntzip format.

        public ZipReturn ZipFileCreate(string newFilename)
        {
            if (ZipOpen != ZipOpenType.Closed)
            {
                Error.ErrorMessage = "ZipFileCreate: Zip file already open";
                Error.ErrorCode = -1;
                return ZipReturn.ZipFileAlreadyOpen;
            }

            CompressUtils.CreateDirForFile(newFilename);
            _zipFileInfo = new FileInfo(newFilename);

            int retVal = FileStream.OpenFileWrite(newFilename, FileStream.BufSizeMax, out _zipFs);
            if (retVal != 0)
            {
                Error.ErrorMessage = RVIO.Error.ErrorMessage;
                Error.ErrorCode = retVal;
                ZipFileClose();
                return ZipReturn.ZipErrorOpeningFile;
            }

            Error.ErrorMessage = "";
            Error.ErrorCode = 0;
            ZipOpen = ZipOpenType.OpenWrite;
            return ZipReturn.ZipGood;
        }

        public ZipReturn ZipFileCreate(Stream zipFs)
        {
            if (ZipOpen != ZipOpenType.Closed)
            {
                Error.ErrorMessage = "ZipFileCreate: Zip file already open";
                Error.ErrorCode = -1;
                return ZipReturn.ZipFileAlreadyOpen;
            }

            _zipFs = zipFs;

            Error.ErrorMessage = "";
            Error.ErrorCode = 0;
            ZipOpen = ZipOpenType.OpenWrite;
            return ZipReturn.ZipGood;
        }

        internal int CentralDirectoryWrite()
        {
            return CentralDirectoryWrite((ulong)_zipFs.Position);
        }

        internal int CentralDirectoryWrite(ulong centralDirStart)
        {
            _centralDirStart = centralDirStart;
            ulong localCentralDirStart = (ulong)_zipFs.Position;
            int crc;
            using (CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true))
            {
                foreach (ZipFileData t in _HeadersCentralDir)
                    t.CentralDirectoryWrite(crcCs);

                crcCs.Flush();
                crc = crcCs.Crc;
            }
            _centralDirSize = (ulong)_zipFs.Position - localCentralDirStart;
            return crc;

        }
        internal void EndOfCentralDirectoryWrite(ulong fileOffset = 0)
        {
            _zip64 = false;
            _zip64 |= _centralDirStart >= 0xffffffff;
            _zip64 |= _centralDirSize >= 0xffffffff;
            _zip64 |= _HeadersCentralDir.Count >= 0xffff;

            if (_zip64)
            {
                _endOfCentralDir64 = fileOffset + (ulong)_zipFs.Position;
                Zip64EndOfCentralDirWrite();
                Zip64EndOfCentralDirectoryLocatorWrite();
            }
            EndOfCentralDirWrite();

        }


        public void ZipFileCloseFailed()
        {
            switch (ZipOpen)
            {
                case ZipOpenType.Closed:
                    return;
                case ZipOpenType.OpenRead:
                    if (_zipFs != null)
                    {
                        _zipFs.Close();
                        _zipFs.Dispose();
                    }
                    break;
                case ZipOpenType.OpenWrite:
                    _zipFs.Flush();
                    _zipFs.Close();
                    _zipFs.Dispose();
                    if (_zipFileInfo != null)
                        RVIO.File.Delete(_zipFileInfo.FullName);
                    _zipFileInfo = null;
                    break;
            }

            ZipOpen = ZipOpenType.Closed;
        }

    }
}
