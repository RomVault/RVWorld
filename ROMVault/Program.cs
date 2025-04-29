using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.FixFile.FixAZipCore;
using RomVaultCore.Utils;

namespace ROMVault
{
    internal static class Program
    {
        private static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;
        public static string strVersion;

        public static FrmMain frmMain;

        private static Mutex mutex = null;

        [STAThread]
        private static void Main()
        {
            strVersion = $"{Version.Major}.{Version.Minor}.{Version.Build}";
            if (Version.Revision > 0)
                strVersion += $" WIP{Version.Revision}";


            Application.SetCompatibleTextRenderingDefault(false);

            string appName = Assembly.GetEntryAssembly().Location;
            appName = Path.GetDirectoryName(appName);
            appName = appName.Replace("\\", "_");
            appName = appName.Replace("/", "_");
            appName = appName.Replace(":", "_");
            appName = appName.Replace(".", "_");

            mutex = new Mutex(true, appName, out bool createdNew);
            if (!createdNew)
            {
                DialogResult res = MessageBox.Show($"You cannot run two copies of the same instance of RomVault.", $"You are already running RomVault", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Settings.rvSettings = new Settings();
            Settings.rvSettings = Settings.SetDefaults(out string errorReadingSettings);

            if (!string.IsNullOrWhiteSpace(errorReadingSettings))
                MessageBox.Show(errorReadingSettings, "Error Reading Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);

            ReportError.ErrorForm += ShowErrorForm;
            ReportError.Dialog += ShowDialog;

         
            Dark.dark.darkEnabled = Settings.rvSettings.Darkness;

            if (!Settings.rvSettings.Darkness)
            {
                Application.EnableVisualStyles();
            }
#if !DEBUG
            Application.ThreadException += ReportError.UnhandledExceptionHandler;
#endif

            FrmSplashScreen progress = new FrmSplashScreen();
            progress.ShowDialog();
            progress.Dispose();


            FindSourceFile.SetFixOrderSettings();

            RootDirsCreate.CheckDatRoot();
            RootDirsCreate.CheckRomRoot();
            RootDirsCreate.CheckToSort();

            frmMain = new FrmMain();
            Application.Run(frmMain);

            ReportError.Close();
        }

        public static void ShowErrorForm(string message)
        {
            FrmShowError fshow = new FrmShowError();
            fshow.settype(message);
            fshow.ShowDialog();
        }

        public static void ShowDialog(string text, string caption)
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }
}