using Compress.StructuredZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using RVIO;
using System.Text;
using System.Threading;

namespace TrrntZip
{
    public class TorrentZipWorker
    {
        public int ThreadId;
        public ProcessFileStartCallback ProcessFileStartCallBack;
        public ProcessFileEndCallback ProcessFileEndCallBack;
        public StatusCallback StatusCallBack;
        public ErrorCallback ErrorCallBack;
        public PauseCancel pauseCancel;

        public int workerCount;

        public TorrentZip tz;

        public CancellationToken cancellationToken;

        public void DoWork()
        {
            tz = new TorrentZip()
            {
                StatusCallBack = StatusCallBack,
                ErrorCallBack = ErrorCallBack,
                StatusLogCallBack = null,
                ThreadId = ThreadId,
                workerCount = workerCount
            };

            /*
            CancellationTokenSource cts = new CancellationTokenSource();
            cancellationToken= cts.Token;
            cts.Cancel();
            */

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    cFile file = MainQueue.bccFile.Take(cancellationToken);

                    ProcessFileStartCallBack?.Invoke(ThreadId, file.fileId, file.filename);
                    Debug.WriteLine($"Thread {ThreadId} Starting to Process File {file.filename}");
                    TrrntZipStatus trrntZipFileStatus;

                    ZipStructure zipStructure = ZipStructure.None;
                    if (file.isDir)
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(file.filename);
                        trrntZipFileStatus = tz.Process(dirInfo, out zipStructure,file.settings, pauseCancel);
                    }
                    else
                    {
                        FileInfo fileInfo = new FileInfo(file.filename);
                        trrntZipFileStatus = tz.Process(fileInfo, out zipStructure, file.settings, pauseCancel);
                    }
                    ProcessFileEndCallBack?.Invoke(ThreadId, file.fileId, trrntZipFileStatus, zipStructure);
                    Debug.WriteLine($"Thread {ThreadId} Finished Process File {file.filename}");

                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"Thread {ThreadId} Cancelled");
            }
        }
    }
}
