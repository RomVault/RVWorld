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
        private readonly List<ZipLocalFile> _localFiles = new();

        private FileInfo _zipFileInfo;

        private Stream _zipFs;

        private uint _localFilesCount;

        private ulong _centralDirStart;
        private ulong _centralDirSize;
        private ulong _endOfCentralDir64;

        private bool _zip64;

        public string ZipFilename => _zipFileInfo != null ? _zipFileInfo.FullName : "";

        public long TimeStamp => _zipFileInfo?.LastWriteTime ?? 0;

        public byte[] FileComment { get; private set; }

        public Zip()
        {
           CompressUtils.EncodeSetup();
        }

        public ZipOpenType ZipOpen { get; private set; }


        public ZipStatus ZipStatus { get; private set; }

        // If writeZipType == trrntzip then it will force a trrntzip file and error out if not.
        // If writeZipType == None it will still try and make a trrntzip if everything is supplied matching trrntzip parameters.
        private OutputZipType writeZipType = OutputZipType.None;

        private ulong offset = 0;

        public int LocalFilesCount()
        {
            return _localFiles.Count;
        }

        public LocalFile GetLocalFile(int i)
        {
            return _localFiles[i];
        }
        
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
                    zipFileCloseWrite();
                    break;
            }
        }


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
