using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Compress;
using Compress.SevenZip;
using Compress.ZipFile;
using RVIO;
using File = Compress.File.File;

namespace Trrntzip
{
    public delegate void StatusCallback(int threadId, int precent);

    public delegate void LogCallback(int threadId, string log);

    public class TorrentZip
    {
        private readonly byte[] _buffer;
        public StatusCallback StatusCallBack;
        public LogCallback StatusLogCallBack;
        public int ThreadId;

        public TorrentZip()
        {
            _buffer = new byte[1024 * 1024];
        }

        public TrrntZipStatus Process(FileInfo fi)
        {
            if (Program.VerboseLogging)
            {
                StatusLogCallBack?.Invoke(ThreadId, "");
            }

            StatusLogCallBack?.Invoke(ThreadId, fi.Name + " - ");

            // First open the zip (7z) file, and fail out if it is corrupt.
            TrrntZipStatus tzs = OpenZip(fi, out ICompress zipFile);
            // this will return ValidTrrntZip or CorruptZip.

            for (int i = 0; i < zipFile.LocalFilesCount(); i++)
            {
                Debug.WriteLine("Name = " + zipFile.Filename(i) + " , " + zipFile.UncompressedSize(i));
            }

            if ((tzs & TrrntZipStatus.CorruptZip) == TrrntZipStatus.CorruptZip)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Zip file is corrupt");
                return TrrntZipStatus.CorruptZip;
            }

            // the zip file may have found a valid trrntzip header, but we now check that all the file info
            // is actually valid, and may invalidate it being a valid trrntzip if any problem is found.

            List<ZippedFile> zippedFiles = ReadZipContent(zipFile);
            
            // check if the compression type has changed
            zipType inputType;
            switch (zipFile)
            {
                case Zip _:
                    tzs |= TorrentZipCheck.CheckZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                    inputType = zipType.zip;
                    break;
                case SevenZ _:
                    tzs |= TorrentZipCheck.CheckSevenZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                    inputType = zipType.sevenzip;
                    break;
                case File _:
                    inputType = zipType.iso;
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }

            zipType outputType = Program.OutZip == zipType.both ? inputType : Program.OutZip;
            if (outputType == zipType.iso) outputType = zipType.zip;

            bool compressionChanged = inputType != outputType;


            // if tza is now just 'ValidTrrntzip' the it is fully valid, and nothing needs to be done to it.

            if (((tzs == TrrntZipStatus.ValidTrrntzip) && !compressionChanged && !Program.ForceReZip) || Program.CheckOnly)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Skipping File");
                return tzs;
            }

            // if compressionChanged then the required file order will also have changed to need to re-sort the files.
            if (compressionChanged)
            {
                switch (outputType)
                {
                    case zipType.zip:
                        tzs |= TorrentZipCheck.CheckZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                        break;
                    case zipType.sevenzip:
                        tzs |= TorrentZipCheck.CheckSevenZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                        break;
                }
            }

            StatusLogCallBack?.Invoke(ThreadId, "TorrentZipping");
            TrrntZipStatus fixedTzs = TorrentZipRebuild.ReZipFiles(zippedFiles, zipFile, _buffer, StatusCallBack, StatusLogCallBack, ThreadId);
            return fixedTzs;
        }


        private TrrntZipStatus OpenZip(FileInfo fi, out ICompress zipFile)
        {
            string ext = Path.GetExtension(fi.Name);
            switch (ext)
            {
                case ".iso":
                    zipFile = new File();
                    break;
                case ".7z":
                    zipFile = new SevenZ();
                    break;
                default:
                    zipFile = new Zip();
                    break;
            }

            ZipReturn zr = zipFile.ZipFileOpen(fi.FullName, fi.LastWriteTime);
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

        private static List<ZippedFile> ReadZipContent(ICompress zipFile)
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