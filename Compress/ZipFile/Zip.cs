using System;
using System.Collections.Generic;
using System.IO;
using FileInfo = RVIO.FileInfo;

// UInt16 = ushort
// UInt32 = uint
// ULong = ulong

namespace Compress.ZipFile
{
    public partial class Zip : ICompress
    { 
        private readonly List<LocalFile> _localFiles = new List<LocalFile>();


        private FileInfo _zipFileInfo;


        private byte[] _fileComment;
        private Stream _zipFs;

        private uint _localFilesCount;

        private bool _zip64;

        public string ZipFilename => _zipFileInfo != null ? _zipFileInfo.FullName : "";

        public long TimeStamp => _zipFileInfo?.LastWriteTime ?? 0;

        public ZipOpenType ZipOpen { get; private set; }


        public ZipStatus ZipStatus { get; set; }

        public int LocalFilesCount()
        {
            return _localFiles.Count;
        }

        public string Filename(int i)
        {
            return _localFiles[i].FileName;
        }

        public ulong UncompressedSize(int i)
        {
            return _localFiles[i].UncompressedSize;
        }

        public ulong? LocalHeader(int i)
        {
            return (_localFiles[i].GeneralPurposeBitFlag & 8) == 0 ? (ulong?)_localFiles[i].RelativeOffsetOfLocalHeader : null;
        }

        public byte[] CRC32(int i)
        {
            return _localFiles[i].CRC;
        }

        public bool IsDirectory(int i)
        {
            try
            {
                if (_localFiles[i].UncompressedSize != 0)
                    return false;
                string filename = _localFiles[i].FileName;
                char lastChar = filename[filename.Length - 1];
                return lastChar == '/' || lastChar == '\\';
            }
            catch (Exception ex)
            {
                ArgumentException argEx = new ArgumentException("Error in file " + _zipFileInfo?.FullName + " : " + ex.Message, ex.InnerException);
                throw argEx;
            }

        }

        public long LastModified(int i)
        {
            return _localFiles[i].DateTime;
        }

        public long? Created(int i)
        {
            return _localFiles[i].DateTimeCreate;
        }

        public long? Accessed(int i)
        {
            return _localFiles[i].DateTimeAccess;
        }

        public void ZipFileClose()
        {
            if (ZipOpen == ZipOpenType.Closed)
            {
                return;
            }

            if (ZipOpen == ZipOpenType.OpenRead)
            {
                zipFileCloseRead();
                return;
            }

            zipFileCloseWrite();
        }

        public byte[] Filecomment => _fileComment;

        /*
        public void BreakTrrntZip(string filename)
        {
            _zipFs = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite);
            using (BinaryReader zipBr = new BinaryReader(_zipFs,Encoding.UTF8,true))
            {
            _zipFs.Position = _zipFs.Length - 22;
            byte[] fileComment = zipBr.ReadBytes(22);
            if (GetString(fileComment).Substring(0, 14) == "TORRENTZIPPED-")
            {
                _zipFs.Position = _zipFs.Length - 8;
                _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
                _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
            }
            }
            _zipFs.Flush();
            _zipFs.Close();
        }
        */

        ~Zip()
        {
            ZipFileClose();
        }



    }
}
