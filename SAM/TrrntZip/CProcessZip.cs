using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Compress.StructuredZip;
using RVIO;

namespace TrrntZip
{
    public class cFile
    {
        public int fileId;
        public string filename;
        public bool isDir;

        public Settings settings;
    }

    public delegate void ProcessFileStartCallback(int threadId, int fileId, string filename, long size);
    public delegate void ProcessFileEndCallback(int threadId, int fileId, TrrntZipStatus trrntZipStatus, ZipStructure zipStructure);
    public class CProcessZip
    {
        public int ThreadId;
        public ProcessFileStartCallback ProcessFileStartCallBack;
        public ProcessFileEndCallback ProcessFileEndCallBack;

        public StatusCallback StatusCallBack;
        public ErrorCallback ErrorCallBack;
        public PauseCancel pauseCancel;

        public int workerCount;

        public TorrentZip tz;

        public void MigrateZip()
        {
            tz = new TorrentZip()
            {
                StatusCallBack = StatusCallBack,
                ErrorCallBack = ErrorCallBack,
                StatusLogCallBack = null,
                ThreadId = ThreadId,
                workerCount = workerCount
            };
            Debug.WriteLine($"Thread {ThreadId} Starting Up");


            foreach (cFile file in MainQueue.bccFile.GetConsumingEnumerable(CancellationToken.None))
            {
                if (pauseCancel != null && pauseCancel.StopHard)
                {
                    ProcessFileEndCallBack?.Invoke(ThreadId, file.fileId, TrrntZipStatus.Cancel, ZipStructure.None);
                    continue;
                }
                if (pauseCancel != null)
                    pauseCancel.WaitOne();

                TrrntZipStatus trrntZipFileStatus;

                ZipStructure zipStructure = ZipStructure.None;
                if (file.isDir)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(file.filename);
                    ProcessFileStartCallBack?.Invoke(ThreadId, file.fileId, file.filename, -1);
                    trrntZipFileStatus = tz.Process(dirInfo, file.fileId, out zipStructure, file.settings, pauseCancel);
                }
                else
                {
                    FileInfo fileInfo = new FileInfo(file.filename);
                    ProcessFileStartCallBack?.Invoke(ThreadId, file.fileId, file.filename, fileInfo.Length);
                    trrntZipFileStatus = tz.Process(fileInfo, file.fileId, out zipStructure, file.settings, pauseCancel);
                }
                ProcessFileEndCallBack?.Invoke(ThreadId, file.fileId, trrntZipFileStatus, zipStructure);
                Debug.WriteLine($"Thread {ThreadId} Finished Process File {file.filename}");
            }

            Debug.WriteLine($"Thread {ThreadId} Finished");

        }
    }
}
