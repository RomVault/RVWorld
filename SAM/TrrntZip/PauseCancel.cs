using System.Threading;

namespace TrrntZip
{
    public class PauseCancel
    {
        public bool Paused { get; private set; } = false;
        public bool StopHard { get; private set; } = false;
        public bool StopSoft { get; private set; } = false;

        public bool Stopping => StopHard || StopSoft;
        private ManualResetEvent mrse;

        public PauseCancel()
        {
            mrse = new ManualResetEvent(true);
            StopHard = false; 
            StopSoft = false;
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
            StopHard = true;
            UnPause();
        }
         
        public void SoftCancel()
        {
            StopSoft = true;
            UnPause();
        }

        public void ResetCancel()
        {
            StopHard = false;
            StopSoft = false;
            UnPause();
        }

        public void WaitOne()
        {
            mrse.WaitOne();
        }
    }
}
