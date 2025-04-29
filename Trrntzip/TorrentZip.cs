using System.Collections.Generic;
using Compress;
using Compress.SevenZip;
using Compress.StructuredZip;
using Compress.ZipFile;
using RVIO;

namespace TrrntZip
{
    public delegate void StatusCallback(int threadId, int precent);

    public delegate void LogCallback(int threadId, string log);

    public delegate void ErrorCallback(int threadId, string error);

    public class TorrentZip
    {
        private readonly byte[] _buffer;
        public StatusCallback StatusCallBack;
        public LogCallback StatusLogCallBack;
        public ErrorCallback ErrorCallBack;
        public int ThreadId;
        public int workerCount;

        public TorrentZip()
        {
            _buffer = new byte[1024 * 1024];
        }

        public TrrntZipStatus Process(FileInfo fi, PauseCancel pc = null)
        {
            if (Program.VerboseLogging)
            {
                StatusLogCallBack?.Invoke(ThreadId, "");
            }

            StatusLogCallBack?.Invoke(ThreadId, fi.Name + " - ");

            // First open the zip (7z) file, and fail out if it is corrupt.
            TrrntZipStatus tzs = OpenZip(fi, out ICompress zipFile);
            // this will return ValidTrrntZip or CorruptZip.

            /*
            for (int i = 0; i < zipFile.LocalFilesCount; i++)
            {
                FileHeader lf = zipFile.GetFileHeader(i);
                Debug.WriteLine("Name = " + lf.Filename + " , " + lf.UncompressedSize);
            }
            */

            if ((tzs & TrrntZipStatus.SourceFileLocked) == TrrntZipStatus.SourceFileLocked)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Zip file Locked");
                return TrrntZipStatus.SourceFileLocked;
            }
            if ((tzs & TrrntZipStatus.CorruptZip) == TrrntZipStatus.CorruptZip)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Zip file is corrupt");
                return TrrntZipStatus.CorruptZip;
            }
            if ((tzs & TrrntZipStatus.CatchError) == TrrntZipStatus.CatchError)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Zip Worker Error Caught");
                return TrrntZipStatus.CatchError;
            }


            // the zip file may have found a valid trrntzip header, but we now check that all the file info
            // is actually valid, and may invalidate it being a valid trrntzip if any problem is found.

            List<ZippedFile> zippedFiles = ReadZipContent(zipFile);

            ZipStructure outputType = Program.OutZip;

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
                case Compress.File.File _:
                    inputType = zipType.file;
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }


            bool compressionChanged = zipFile.ZipStruct != outputType;


            // if tza is now just 'ValidTrrntzip' the it is fully valid, and nothing needs to be done to it.

            if (((tzs == TrrntZipStatus.ValidTrrntzip) && !compressionChanged && !Program.ForceReZip) || Program.CheckOnly)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Skipping File");
                zipFile.ZipFileClose();
                return tzs;
            }

            // if compressionChanged then the required file order will also have changed so need to re-sort the files.
            if (compressionChanged)
            {
                switch (outputType)
                {
                    case ZipStructure.ZipTrrnt:
                    case ZipStructure.ZipZSTD:
                        tzs |= TorrentZipCheck.CheckZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                        break;
                    case ZipStructure.SevenZipNLZMA:
                    case ZipStructure.SevenZipSLZMA:
                    case ZipStructure.SevenZipNZSTD:
                    case ZipStructure.SevenZipSZSTD:
                        tzs |= TorrentZipCheck.CheckSevenZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                        break;
                    default:
                        return TrrntZipStatus.Unknown;
                }
            }

            StatusLogCallBack?.Invoke(ThreadId, "TorrentZipping");
            TrrntZipStatus fixedTzs = TorrentZipRebuild.ReZipFiles(zippedFiles, zipFile, _buffer, StatusCallBack, StatusLogCallBack, ErrorCallBack, ThreadId, workerCount, pc);
            return fixedTzs;
        }


        private TrrntZipStatus OpenZip(FileInfo fi, out ICompress zipFile)
        {
            string ext = Path.GetExtension(fi.Name);
            switch (ext)
            {
                case ".7z":
                    zipFile = new SevenZ();
                    break;
                case ".zip":
                    zipFile = new StructuredZip();
                    break;
                default:
                    zipFile = new Compress.File.File();
                    break;
            }

            ZipReturn zr = zipFile.ZipFileOpen(fi.FullName, fi.LastWriteTime);
            if (zr == ZipReturn.ZipFileLocked)
            {
                return TrrntZipStatus.SourceFileLocked;
            }
            if (zr != ZipReturn.ZipGood)
            {
                return TrrntZipStatus.CorruptZip;
            }

            TrrntZipStatus tzStatus = TrrntZipStatus.Unknown;

            // first check if the file is a trrntip files
            if (zipFile.ZipStruct == ZipStructure.ZipTrrnt ||
                zipFile.ZipStruct == ZipStructure.ZipZSTD ||
                zipFile.ZipStruct == ZipStructure.SevenZipSLZMA ||
                zipFile.ZipStruct == ZipStructure.SevenZipNLZMA ||
                zipFile.ZipStruct == ZipStructure.SevenZipSZSTD ||
                zipFile.ZipStruct == ZipStructure.SevenZipNZSTD
                )
            {
                tzStatus |= TrrntZipStatus.ValidTrrntzip;
            }

            return tzStatus;
        }

        private static List<ZippedFile> ReadZipContent(ICompress zipFile)
        {
            List<ZippedFile> zippedFiles = new List<ZippedFile>();
            for (int i = 0; i < zipFile.LocalFilesCount; i++)
            {
                FileHeader lf = zipFile.GetFileHeader(i);
                zippedFiles.Add(
                    new ZippedFile
                    {
                        Index = i,
                        Name = lf.Filename,
                        ByteCRC = lf.CRC,
                        Size = lf.UncompressedSize
                    }
                );
            }
            return zippedFiles;
        }

        private static void ReadDirContent(DirectoryInfo diMaster, ref List<ZippedFile> files, int stripLength)
        {
            DirectoryInfo[] arrDi = diMaster.GetDirectories();
            FileInfo[] arrFi = diMaster.GetFiles();

            if (arrDi.Length == 0 && arrFi.Length == 0)
            {
                string name = (diMaster.FullName + "/").Substring(stripLength);
                if (name == "")
                    return;
                files.Add(new ZippedFile() { Name = name, Size = 0 });
                return;
            }

            foreach (DirectoryInfo di in arrDi)
                ReadDirContent(di, ref files, stripLength);

            foreach (FileInfo fi in arrFi)
                files.Add(new ZippedFile() { Name = fi.FullName.Substring(stripLength), Size = (ulong)fi.Length });

        }

        public TrrntZipStatus Process(DirectoryInfo di, PauseCancel pc = null)
        {
            // read in all the files & dirs
            List<ZippedFile> zippedFiles = new List<ZippedFile>();
            ReadDirContent(di, ref zippedFiles, di.FullName.Length + 1);

            // sort them
            ZipStructure outputType = Program.OutZip;
            switch (outputType)
            {
                case ZipStructure.ZipTrrnt:
                case ZipStructure.ZipZSTD:
                    TorrentZipCheck.CheckZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                    break;
                case ZipStructure.SevenZipNLZMA:
                case ZipStructure.SevenZipSLZMA:
                case ZipStructure.SevenZipNZSTD:
                case ZipStructure.SevenZipSZSTD:
                    TorrentZipCheck.CheckSevenZipFiles(ref zippedFiles, ThreadId, StatusLogCallBack);
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }


            StatusLogCallBack?.Invoke(ThreadId, "TorrentZipping");
            TrrntZipStatus fixedTzs = TorrentZipMake.ZipFiles(zippedFiles, di.FullName, _buffer, StatusCallBack, StatusLogCallBack, ErrorCallBack, ThreadId, workerCount, pc);
            return fixedTzs;

        }
    }
}