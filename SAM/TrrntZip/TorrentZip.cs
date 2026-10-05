using Compress;
using Compress.StructuredZip;
using RVIO;
using System.Collections.Generic;

namespace TrrntZip
{
    public delegate void StatusCallback(int threadId, int precent);

    public delegate void LogCallback(int threadId, string log);

    public delegate void ErrorCallback(int threadId, string error);

    public class TorrentZip
    {
        //        private Settings settings;
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

        public TrrntZipStatus Process(FileInfo fi, out ZipStructure outZipStruct, Settings settings, PauseCancel pc = null)
        {
            outZipStruct = ZipStructure.None;
            if ( settings.VerboseLogging)
            {
                StatusLogCallBack?.Invoke(ThreadId, "");
            }

            StatusLogCallBack?.Invoke(ThreadId, fi.Name + " - ");

            // First open the zip (7z) file, and fail out if it is corrupt.
            TrrntZipStatus tzs = OpenZip(fi, out ICompress zipFile);
            // this will return ValidTrrntZip or CorruptZip.


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

            ZipStructure header = ZipStructure.None;
            if (zipFile is StructuredZip sz)
                header = sz.HeaderZipStruct;
            else if (zipFile is Structured7Zip s7z)
                header = s7z.HeaderZipStruct;

            if (zipFile.ZipStruct == ZipStructure.None && header != ZipStructure.None)
                tzs |= TrrntZipStatus.NeedsRepaired;

            bool compressionChanged = false;

            outZipStruct = settings.OutZip;
            if (settings.Repair)
            {
                outZipStruct = header;
                if (zipFile.ZipStruct == ZipStructure.None && header != ZipStructure.None)
                {
                    compressionChanged = true;
                }
                else
                    tzs = TrrntZipStatus.ValidTrrntzip;
            }
            else
                compressionChanged = zipFile.ZipStruct != outZipStruct;

            if (((tzs == TrrntZipStatus.ValidTrrntzip) && !compressionChanged) || settings.DryRun)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Skipping File");
                zipFile.ZipFileClose();
                return tzs;
            }

            List<ZippedFile> zippedFiles = ReadZipContent(zipFile);

            // if compressionChanged then the required file order will also have changed so need to re-sort the files.

            switch (outZipStruct)
            {
                case ZipStructure.ZipTrrnt:
                case ZipStructure.ZipZSTD:
                    TorrentZipApplyRules.CheckZipFiles(ref zippedFiles);
                    break;
                case ZipStructure.SevenZipNLZMA:
                case ZipStructure.SevenZipSLZMA:
                case ZipStructure.SevenZipNZSTD:
                case ZipStructure.SevenZipSZSTD:
                    TorrentZipApplyRules.CheckSevenZipFiles(ref zippedFiles);
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }

            StatusLogCallBack?.Invoke(ThreadId, "TorrentZipping");
            TrrntZipStatus fixedTzs = TorrentZipRebuild.ReZipFiles(zippedFiles, zipFile, outZipStruct, _buffer, StatusCallBack, StatusLogCallBack, ErrorCallBack, ThreadId, workerCount, pc, settings);

            if ((tzs | TrrntZipStatus.NeedsRepaired) == TrrntZipStatus.NeedsRepaired)
                fixedTzs |= TrrntZipStatus.NeedsRepaired;

            return fixedTzs;
        }


        private TrrntZipStatus OpenZip(FileInfo fi, out ICompress zipFile)
        {
            string ext = Path.GetExtension(fi.Name);
            switch (ext)
            {
                case ".7z":
                    zipFile = new Structured7Zip();
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

        public TrrntZipStatus Process(DirectoryInfo di, out ZipStructure outputType, Settings settings, PauseCancel pc = null)
        {
            // read in all the files & dirs
            List<ZippedFile> zippedFiles = new List<ZippedFile>();
            ReadDirContent(di, ref zippedFiles, di.FullName.Length + 1);

            // sort them
            outputType = settings.OutZip;
            switch (outputType)
            {
                case ZipStructure.ZipTrrnt:
                case ZipStructure.ZipZSTD:
                    TorrentZipApplyRules.CheckZipFiles(ref zippedFiles);
                    break;
                case ZipStructure.SevenZipNLZMA:
                case ZipStructure.SevenZipSLZMA:
                case ZipStructure.SevenZipNZSTD:
                case ZipStructure.SevenZipSZSTD:
                    TorrentZipApplyRules.CheckSevenZipFiles(ref zippedFiles);
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }

            if (settings.DryRun)
            {
                StatusLogCallBack?.Invoke(ThreadId, "Skipping File");
                return TrrntZipStatus.DryRun;
            }

            StatusLogCallBack?.Invoke(ThreadId, "TorrentZipping");
            TrrntZipStatus fixedTzs = TorrentZipMake.ZipFiles(zippedFiles, di.FullName, _buffer, StatusCallBack, StatusLogCallBack, ErrorCallBack, ThreadId, workerCount, pc, settings);
            return fixedTzs;

        }
    }
}