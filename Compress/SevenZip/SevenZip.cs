using System.Collections.Generic;
using System.IO;
using System.Text;
using Compress.SevenZip.Structure;
using FileInfo = RVIO.FileInfo;

namespace Compress.SevenZip
{
    public partial class SevenZ : ICompress
    {

        public enum SevenZipCompressType
        {
            uncompressed,
            lzma,
            zstd
        }

        private class SevenZipLocalFile : FileHeader
        {
            public int StreamIndex;
            public ulong StreamOffset;
        }


        private List<SevenZipLocalFile> _localFiles = new();

        private FileInfo _zipFileInfo;

        private Stream _zipFs;

        private SignatureHeader _signatureHeader;



        private long _baseOffset;

        public string ZipFilename => _zipFileInfo != null ? _zipFileInfo.FullName : "";

        public long TimeStamp => _zipFileInfo?.LastWriteTime ?? 0;

        public string FileComment => null;

        public ZipOpenType ZipOpen { get; private set; }
        public ZipStructure ZipStruct { get; private set; }

        public int LocalFilesCount => _localFiles.Count;
        
        public FileHeader GetFileHeader(int i)
        {
            return _localFiles[i];
        }

        public void ZipFileCloseFailed()
        {
            switch (ZipOpen)
            {
                case ZipOpenType.Closed:
                    return;
                case ZipOpenType.OpenRead:
                    ZipFileCloseReadStream();
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
        

        public void ZipFileClose()
        {
            switch (ZipOpen)
            {
                case ZipOpenType.Closed:
                    return;
                case ZipOpenType.OpenRead:
                    ZipFileCloseReadStream();
                    if (_zipFs != null)
                    {
                        _zipFs.Close();
                        _zipFs.Dispose();
                    }
                    ZipOpen = ZipOpenType.Closed;
                    return;
                case ZipOpenType.OpenWrite:
                    CloseWriting7Zip();
                    if (_zipFileInfo != null)
                        _zipFileInfo = new FileInfo(_zipFileInfo.FullName);
                    break;
            }

            ZipOpen = ZipOpenType.Closed;
        }


        private Header _header;

        public StringBuilder HeaderReport()
        {
            StringBuilder sb = new();

            if (_header == null)
            {
                sb.AppendLine("Null Header");
                return sb;
            }

            _header.Report(ref sb);

            return sb;
        }

    }
}