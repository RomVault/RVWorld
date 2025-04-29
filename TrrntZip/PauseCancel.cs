using System.Threading;

namespace TrrntZip
{

    public class PauseCancel
    {
        public bool Paused { get; private set; } = false;
        public bool Cancelled { get; private set; } = false;

        private ManualResetEvent mrse;

        public PauseCancel()
        {
            mrse = new ManualResetEvent(true);
            Cancelled = false;
        }

        public void Pause()
        {
            Paused = true;
            mrse.Reset();
        }

        public void UnPause()
        {
            Paused = false;
            mrse.Set();
        }

        public void Cancel()
        {
            Cancelled = true;
            UnPause();
        }

        public void ResetCancel()
        {
            Cancelled = false;
            UnPause();
        }

        public void WaitOne()
        {
            mrse.WaitOne();
        }

    }

}
