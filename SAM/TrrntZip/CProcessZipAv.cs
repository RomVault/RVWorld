using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Compress.StructuredZip;
using RVIO;

namespace TrrntZip
{
    public delegate void ProcessorClosingCallback(int threadId);

    public class CProcessZipAv
    {
        public int ThreadId;
        public ProcessFileStartCallback ProcessFileStartCallBack;
        public ProcessFileEndCallback ProcessFileEndCallBack;

        public ProcessorClosingCallback ProcessorClosingCallBack;
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

            while (true)
            {
                // Blocked on Take() means this worker is idle and waiting for work.
                cFile file = MainQueue.bccFile.Take();
                MainQueue.WorkerBusy();
                bool busy = true;

                try
                {
                    lock (Workers.lockObj)
                    {
                        if (file.fileId == -1 && file.filename == "Removing")
                        {
                            if (Workers.workers > Workers.requestedWorkers)
                            {
                                Debug.WriteLine($"Thread {ThreadId} Closing Down");
                                ProcessorClosingCallBack?.Invoke(ThreadId);
                                Workers.workers--;
                                MainQueue.WorkerIdle();
                                busy = false;
                                break;
                            }
                            else
                                continue;
                        }
                    }

                    if (pauseCancel != null && pauseCancel.Cancelled)
                    {
                        ProcessFileEndCallBack?.Invoke(ThreadId, file.fileId, TrrntZipStatus.Cancel, ZipStructure.None);
                        continue;
                    }
                    if (pauseCancel != null)
                        pauseCancel.WaitOne();

                    ProcessFileStartCallBack?.Invoke(ThreadId, file.fileId, file.filename);
                    Debug.WriteLine($"Thread {ThreadId} Starting to Process File {file.filename}");
                    TrrntZipStatus trrntZipFileStatus;

                    ZipStructure zipStructure = ZipStructure.None;
                    tz.workerCount = workerCount;
                    if (file.isDir)
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(file.filename);
                        trrntZipFileStatus = tz.Process(dirInfo, out zipStructure, file.settings, pauseCancel);
                    }
                    else
                    {
                        FileInfo fileInfo = new FileInfo(file.filename);
                        trrntZipFileStatus = tz.Process(fileInfo, out zipStructure, file.settings, pauseCancel);
                    }
                    ProcessFileEndCallBack?.Invoke(ThreadId, file.fileId, trrntZipFileStatus, zipStructure);
                    Debug.WriteLine($"Thread {ThreadId} Finished Process File {file.filename}");


                    lock (Workers.lockObj)
                    {
                        if (Workers.workers > Workers.requestedWorkers)
                        {
                            Debug.WriteLine($"Thread {ThreadId} Closing Down");
                            ProcessorClosingCallBack?.Invoke(ThreadId);
                            Workers.workers--;
                            break;
                        }
                    }
                }
                finally
                {
                    // Going back to wait on Take(), so this worker is idle again.
                    if (busy)
                        MainQueue.WorkerIdle();
                }
            }

            Debug.WriteLine($"Thread {ThreadId} Finished");
        }
    }
}
