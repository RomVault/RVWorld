using RVIO;
using Trrntzip;

namespace TrrntZipUI
{
    public delegate void GetNextFileCallback(int threadId, out int fileId, out string filename);
    public delegate void SetFileStatusCallback(int processId, int fileId, TrrntZipStatus trrntZipStatus);

    public class CProcessZip
    {
        public int ThreadId;
        public GetNextFileCallback GetNextFileCallBack;
        public StatusCallback StatusCallBack;
        public SetFileStatusCallback SetFileStatusCallBack;

        public void MigrateZip()
        {
            TorrentZip tz = new TorrentZip
            {
                StatusCallBack = StatusCallBack,
                StatusLogCallBack = null,
                ThreadId = ThreadId
            };


            int fileId = 0;
            string filename = null;

            // get the first file
            GetNextFileCallBack?.Invoke(ThreadId, out fileId, out filename);

            while (!string.IsNullOrEmpty(filename))
            {
                FileInfo fi = new FileInfo(filename);

                TrrntZipStatus trrntZipFileStatus = tz.Process(fi);

                SetFileStatusCallBack?.Invoke(ThreadId, fileId, trrntZipFileStatus);

                // get the next file
                GetNextFileCallBack?.Invoke(ThreadId, out fileId, out filename);
            }
        }
    }
}