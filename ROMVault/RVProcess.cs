using System.Diagnostics;
using System.IO;

namespace ROMVault
{
    public static class RVProcess
    {
        public static void StartURL(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public static void StartDIR(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return;
                Process.Start(new ProcessStartInfo
                {
                    Arguments = dir,
                    FileName = "explorer.exe",
                    UseShellExecute = true
                });
            }
            catch { }
        }

    }
}
