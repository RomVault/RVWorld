namespace RomVaultCore.FixFile
{
    /// <summary>
    /// Thin adapter around <see cref="ThreadWorker"/> used by fix routines to report progress and observe cancellation.
    /// </summary>
    public static class Report

    {
        private static ThreadWorker _thWrk;


        public static bool Set(ThreadWorker thWrk)
        {
            _thWrk = thWrk;
            return _thWrk != null;
        }

        public static void ReportProgress(object prog)
        {
            _thWrk?.Report(prog);
        }

        public static bool CancellationPending()
        {
            return _thWrk.CancellationPending;
        }

    }
}
