namespace RomVaultCore.FixFile
{
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
