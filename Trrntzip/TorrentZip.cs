using System.Collections.Generic;
using System.Diagnostics;
using Compress;
using Compress.SevenZip;
using Compress.ZipFile;
using RVIO;

namespace Trrntzip
{
    public delegate void StatusCallback(int threadID, int precent);

    public delegate void LogCallback(int threadID, string log);

    public class TorrentZip
    {
        private readonly byte[] _buffer;
        public StatusCallback StatusCallBack;
        public LogCallback StatusLogCallBack;
        public int ThreadID;

        public TorrentZip()
        {
            _buffer = new byte[1024*1024];
        }

        public TrrntZipStatus Process(FileInfo fi)
        {
            if (Program.VerboseLogging)
            {
                StatusLogCallBack?.Invoke(ThreadID, "");
            }

            StatusLogCallBack?.Invoke(ThreadID, fi.Name + " - ");

            // First open the zip (7z) file, and fail out if it is corrupt.

            ICompress zipFile;
            TrrntZipStatus tzs = OpenZip(fi, out zipFile);
            // this will return ValidTrrntZip or CorruptZip.

            for (int i = 0; i < zipFile.LocalFilesCount(); i++)
            {
                Debug.WriteLine("Name = " + zipFile.Filename(i) + " , " + zipFile.UncompressedSize(i));
            }

            if ((tzs & TrrntZipStatus.CorruptZip) == TrrntZipStatus.CorruptZip)
            {
                StatusLogCallBack?.Invoke(ThreadID, "Zip file is corrupt");
                return TrrntZipStatus.CorruptZip;
            }

            // the zip file may have found a valid trrntzip header, but we now check that all the file info
            // is actually valid, and may invalidate it being a valid trrntzip if any problem is found.

            List<ZippedFile> zippedFiles = ReadZipContent(zipFile);
            tzs |= TorrentZipCheck.CheckZipFiles(ref zippedFiles, ThreadID, StatusLogCallBack);

            // check if the compression type has changed
            zipType inputType;
            if (zipFile is ZipFile)
            {
                inputType = zipType.zip;
            }
            else if (zipFile is SevenZ)
            {
                inputType = zipType.sevenzip;
            }
            else
            {
                return TrrntZipStatus.Unknown;
            }

            zipType outputType = Program.OutZip == zipType.both ? inputType : Program.OutZip;
            bool compressionChanged = inputType != outputType;


            // if tza is now just 'ValidTrrntzip' the it is fully valid, and nothing needs to be done to it.

            if (((tzs == TrrntZipStatus.ValidTrrntzip) && !compressionChanged && !Program.ForceReZip) || Program.CheckOnly)
            {
                StatusLogCallBack?.Invoke(ThreadID, "Skipping File");
                return TrrntZipStatus.ValidTrrntzip;
            }

            StatusLogCallBack?.Invoke(ThreadID, "TorrentZipping");
            TrrntZipStatus fixedTzs = TorrentZipRebuild.ReZipFiles(zippedFiles, zipFile, _buffer, StatusCallBack, StatusLogCallBack, ThreadID);
            return fixedTzs;
        }


        private TrrntZipStatus OpenZip(FileInfo fi, out ICompress zipFile)
        {
            string ext = Path.GetExtension(fi.Name);
            if (ext == ".7z")
            {
                zipFile = new SevenZ();
            }
            else
            {
                zipFile = new ZipFile();
            }

            ZipReturn zr = zipFile.ZipFileOpen(fi.FullName, fi.LastWriteTime, true);
            if (zr != ZipReturn.ZipGood)
            {
                return TrrntZipStatus.CorruptZip;
            }

            TrrntZipStatus tzStatus = TrrntZipStatus.Unknown;

            // first check if the file is a trrntip files
            if (zipFile.ZipStatus == ZipStatus.TrrntZip)
            {
                tzStatus |= TrrntZipStatus.ValidTrrntzip;
            }

            return tzStatus;
        }

        private List<ZippedFile> ReadZipContent(ICompress zipFile)
        {
            List<ZippedFile> zippedFiles = new List<ZippedFile>();
            for (int i = 0; i < zipFile.LocalFilesCount(); i++)
            {
                zippedFiles.Add(
                    new ZippedFile
                    {
                        Index = i,
                        Name = zipFile.Filename(i),
                        ByteCRC = zipFile.CRC32(i),
                        Size = zipFile.UncompressedSize(i)
                    }
                );
            }
            return zippedFiles;
        }
    }
}