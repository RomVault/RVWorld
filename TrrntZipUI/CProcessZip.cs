using RVIO;
using Trrntzip;

namespace TrrntZipUI
{
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
                StatusCallBack = TzStatusCallBack,
                StatusLogCallBack = TzStatusLogCallBack,
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

        private void TzStatusCallBack(int processId, int percent)
        {
            StatusCallBack?.Invoke(processId, percent);
        }

        private static void TzStatusLogCallBack(int processId, string log)
        {
        }
    }
}