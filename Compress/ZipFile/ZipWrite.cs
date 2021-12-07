using Compress.Support.Utils;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;


namespace Compress.ZipFile
{
  
    public partial class Zip
    {

        public ZipReturn ZipFileCreate(string newFilename)
        {
            return ZipFileCreate(newFilename, OutputZipType.None);
        }

        // OutType of Trrntzip forces that we must have a trrtnzip file made
        // OutType of None will still make a trrntzip file if everything was supplied in trrntzip format.

        public ZipReturn ZipFileCreate(string newFilename, OutputZipType outType)
        {
            if (ZipOpen != ZipOpenType.Closed)
            {
                return ZipReturn.ZipFileAlreadyOpen;
            }

            writeZipType = outType;

            CompressUtils.CreateDirForFile(newFilename);
            _zipFileInfo = new FileInfo(newFilename);

            int errorCode = FileStream.OpenFileWrite(newFilename, out _zipFs);
            if (errorCode != 0)
            {
                ZipFileClose();
                return ZipReturn.ZipErrorOpeningFile;
            }
            ZipOpen = ZipOpenType.OpenWrite;
            return ZipReturn.ZipGood;
        }

        private void zipFileCloseWrite()
        {
            bool lTrrntzip = true;

            _centralDirStart = (ulong)_zipFs.Position;

            using (CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true))
            {
                foreach (ZipLocalFile t in _localFiles)
                {
                    t.CentralDirectoryWrite(crcCs);
                    lTrrntzip &= t.GetStatus(LocalFileStatus.TrrntZip);
                }

                crcCs.Flush();
                crcCs.Close();

                _centralDirSize = (ulong)_zipFs.Position - _centralDirStart;

                FileComment = lTrrntzip ? CompressUtils.GetBytes("TORRENTZIPPED-" + crcCs.Crc.ToString("X8")) : new byte[0];
                ZipStatus = lTrrntzip ? ZipStatus.TrrntZip : ZipStatus.None;
            }

            _zip64 = false;
            _zip64 |= _centralDirStart >= 0xffffffff;
            _zip64 |= _centralDirSize >= 0xffffffff;
            _zip64 |= _localFiles.Count >= 0xffff;

            if (_zip64)
            {
                _endOfCentralDir64 = (ulong)_zipFs.Position;
                Zip64EndOfCentralDirWrite();
                Zip64EndOfCentralDirectoryLocatorWrite();
            }
            EndOfCentralDirWrite();

            _zipFs.SetLength(_zipFs.Position);
            _zipFs.Flush();
            _zipFs.Close();
            _zipFs.Dispose();
            _zipFileInfo = new FileInfo(_zipFileInfo.FullName);
            ZipOpen = ZipOpenType.Closed;
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
