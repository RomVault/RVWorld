using CodePage;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FileInfo = RVIO.FileInfo;

// UInt16 = ushort
// UInt32 = uint
// ULong = ulong

namespace Compress.ZipFile
{
    public partial class Zip : ICompress
    {
        private FileInfo _zipFileInfo;

        private Stream _zipFs;


        private readonly List<ZipFileData> _HeadersLocalFile = new();
        private readonly List<ZipFileData> _HeadersCentralDir = new();

        public int LocalFilesCount => _HeadersCentralDir.Count;

        public FileHeader GetFileHeader(int i) => _HeadersCentralDir[i];

        public FileHeader GetLocalFileData(int i) => _HeadersLocalFile[i];




        private uint _localFilesCount;

        internal ulong _centralDirStart;
        internal ulong _centralDirSize;
        private ulong _endOfCentralDir64;

        private bool _zip64;

        public string ZipFilename => _zipFileInfo != null ? _zipFileInfo.FullName : "";

        public long TimeStamp => _zipFileInfo?.LastWriteTime ?? 0;

        private byte[] _fileComment;

        public string FileComment { 
            get { return CodePage437.GetString(_fileComment); }
            set { _fileComment = CodePage437.GetBytes(value); }
        }

        public Zip()
        {
        }

        public ZipOpenType ZipOpen { get; private set; }

        public ZipStructure ZipStruct => ZipStructure.None;

        internal ulong offset = 0;
        internal bool ExtraDataFoundOnEndOfFile = false;

        public void ZipFileClose()
        {
            switch (ZipOpen)
            {
                case ZipOpenType.Closed:
                    return;

                case ZipOpenType.OpenRead:
                    zipFileCloseRead();
                    return;

                default:
                    CentralDirectoryWrite();
                    EndOfCentralDirectoryWrite();
                    zipFileCloseWrite();
                    break;
            }
        }




        public void BreakTrrntZip(string filename)
        {
            _zipFs = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite);
            using (BinaryReader zipBr = new BinaryReader(_zipFs, Encoding.UTF8, true))
            {
                _zipFs.Position = _zipFs.Length - 22;
                byte[] fileComment = zipBr.ReadBytes(22);
                string testComment = Encoding.UTF8.GetString(fileComment);
                if (testComment.Substring(0, 14) == "TORRENTZIPPED-")
                {
                    _zipFs.Position = _zipFs.Length - 8;
                    _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
                    _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
                }
                else
                {
                    _zipFs.Position = _zipFs.Length - 15;
                    fileComment = zipBr.ReadBytes(15);
                    testComment = Encoding.UTF8.GetString(fileComment);
                    if (testComment.Substring(0, 7) == "RVZSTD-")
                    {
                        _zipFs.Position = _zipFs.Length - 8;
                        _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
                        _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
                    }
                }
            }
            _zipFs.Flush();
            _zipFs.Close();

        }


        ~Zip()
        {
            ZipFileClose();
        }



    }
}
