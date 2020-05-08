using System.IO;
using Compress.Utils;

namespace Compress.ZipFile
{
    public partial class Zip
    {

        public void ZipCreateFake()
        {
            if (ZipOpen != ZipOpenType.Closed)
            {
                return;
            }

            ZipOpen = ZipOpenType.OpenFakeWrite;
        }


        public void ZipFileCloseFake(ulong fileOffset, out byte[] centralDir)
        {
            centralDir = null;
            if (ZipOpen != ZipOpenType.OpenFakeWrite)
            {
                return;
            }

            _zip64 = false;
            bool lTrrntzip = true;

            _zipFs = new MemoryStream();

            _centerDirStart = fileOffset;
            if (_centerDirStart >= 0xffffffff)
            {
                _zip64 = true;
            }

            CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true);

            foreach (LocalFile t in _localFiles)
            {
                t.CenteralDirectoryWrite(crcCs);
                _zip64 |= t.Zip64;
                lTrrntzip &= t.TrrntZip;
            }

            crcCs.Flush();
            crcCs.Close();

            _centerDirSize = (ulong)_zipFs.Position;

            _fileComment = lTrrntzip ? ZipUtils.GetBytes("TORRENTZIPPED-" + crcCs.Crc.ToString("X8")) : new byte[0];
            ZipStatus = lTrrntzip ? ZipStatus.TrrntZip : ZipStatus.None;

            crcCs.Dispose();

            if (_zip64)
            {
                _endOfCenterDir64 = fileOffset + (ulong)_zipFs.Position;
                Zip64EndOfCentralDirWrite();
                Zip64EndOfCentralDirectoryLocatorWrite();
            }
            EndOfCentralDirWrite();

            centralDir = ((MemoryStream)_zipFs).ToArray();
            _zipFs.Close();
            _zipFs.Dispose();
            ZipOpen = ZipOpenType.Closed;
        }


        public ZipReturn ZipFileAddFake(string filename, ulong fileOffset, ulong uncompressedSize, ulong compressedSize, byte[] crc32, out byte[] localHeader)
        {
            localHeader = null;

            if (ZipOpen != ZipOpenType.OpenFakeWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            LocalFile lf = new LocalFile(filename);
            _localFiles.Add(lf);

            MemoryStream ms = new MemoryStream();
            lf.LocalFileHeaderFake(fileOffset, uncompressedSize, compressedSize, crc32, ms);

            localHeader = ms.ToArray();
            ms.Close();

            return ZipReturn.ZipGood;
        }
    }
}
