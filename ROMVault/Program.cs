using System;
using System.Reflection;
using System.ServiceModel;
using System.Windows.Forms;
using ROMVault.RVServices;
using RVCore;

namespace ROMVault
{
    internal static class Program
    {
        private static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;
        private static readonly int VNow = Version.Build;
        public static readonly string StrVersion = $"{Version.Major}.{Version.Minor}.{Version.Build}";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            BasicHttpBinding b = new BasicHttpBinding();
            b.SendTimeout = new TimeSpan(0, 0, 10);
            b.ReceiveTimeout = new TimeSpan(0, 0, 10);
            EndpointAddress e = new EndpointAddress(@"http://services.romvault.com/RVService.svc");
            RVServiceClient s = new RVServiceClient(b, e);


            if (string.IsNullOrEmpty(Settings.Username) || string.IsNullOrEmpty(Settings.EMail))
            {
                using (FrmRegistration fReg = new FrmRegistration())
                {
                    fReg.ShowDialog();
                }
            }

            try
            {
                if (!Settings.OptOut)
                {
                    s.SendUser(Settings.Username, Settings.EMail, VNow);
                    s.StartUpV2(Version.Major,Version.Minor,Version.Build);
                }

                ReportError.vMajor = Version.Major;
                ReportError.vMinor = Version.Minor;
                ReportError.vBuild = Version.Build;

                bool v = s.UpdateCheck(Version.Major, Version.Minor, Version.Build);

                if (v)
                {
                    string url = s.GetUpdateLink();
                    MessageBox.Show("There is a new release download now from " + url);
                    //System.Diagnostics.Process.Start(url);
                    //s.Close();
                    //return;
                }

                s.Close();
            }
            catch (Exception ex)
            {

            }

            Compress.SevenZip.SevenZ.TestForZstd();

            ReportError.ErrorForm += ShowErrorForm;
            ReportError.Dialog += ShowDialog;

            Settings.rvSettings = new Settings();

#if !DEBUG
            Application.ThreadException += ReportError.UnhandledExceptionHandler;
#endif

            FrmSplashScreen progress = new FrmSplashScreen();
            progress.ShowDialog();

            progress.Dispose();

            Application.Run(new FrmMain());

            ReportError.Close();
        }

        public static void ShowErrorForm(string message)
        {
            FrmShowError fshow = new FrmShowError();
            fshow.settype(message);
            fshow.ShowDialog();
        }

        public static void ShowDialog(string text,string caption)
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }
}