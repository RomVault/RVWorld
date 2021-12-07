using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using RVIO;

namespace TrrntZip
{
    public class cFile
    {
        public int fileId;
        public string filename;
    }

    public delegate void ProcessFileStartCallback(int threadId, int fileId, string filename);
    public delegate void ProcessFileEndCallback(int threadId, int fileId, TrrntZipStatus trrntZipStatus);
    public class CProcessZip
    {
        public int ThreadId;
        public BlockingCollection<cFile> bcCfile;
        public ProcessFileStartCallback ProcessFileStartCallBack;
        public ProcessFileEndCallback ProcessFileEndCallBack;

        public StatusCallback StatusCallBack;
        public PauseCancel pauseCancel;

        public void MigrateZip()
        {
            TorrentZip tz = new TorrentZip
            {
                StatusCallBack = StatusCallBack,
                StatusLogCallBack = null,
                ThreadId = ThreadId
            };
            Debug.WriteLine($"Thread {ThreadId} Starting Up");

            foreach (cFile file in bcCfile.GetConsumingEnumerable(CancellationToken.None))
            {
                if (pauseCancel.Cancelled)
                {
                    continue;
                }
                pauseCancel.WaitOne();

                ProcessFileStartCallBack?.Invoke(ThreadId, file.fileId, file.filename);
                Debug.WriteLine($"Thread {ThreadId} Starting to Process File {file.filename}");
                FileInfo fileInfo = new FileInfo(file.filename);
                TrrntZipStatus trrntZipFileStatus = tz.Process(fileInfo, pauseCancel);
                ProcessFileEndCallBack?.Invoke(ThreadId, file.fileId, trrntZipFileStatus);
                Debug.WriteLine($"Thread {ThreadId} Finished Process File {file.filename}");
            }

            Debug.WriteLine($"Thread {ThreadId} Finished");

        }
    }
}
