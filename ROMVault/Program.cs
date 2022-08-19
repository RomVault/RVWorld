using System;
using System.Reflection;
using System.ServiceModel;
using System.Windows.Forms;
using RomVaultCore;
using RVServ1;

namespace ROMVault
{
    internal static class Program
    {
        private static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;
        private static readonly int VNow = Version.Build;
        public static readonly string StrVersion = Version.ToString(3);
        public static int Permissions;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            BasicHttpBinding basicHttpBinding = new BasicHttpBinding
            {
                SendTimeout = new TimeSpan(0, 1, 0),
                ReceiveTimeout = new TimeSpan(0, 1, 0),
                MaxReceivedMessageSize = 128 * 1024 * 1024
            };
            EndpointAddress endPointAddress = new EndpointAddress(@"http://services.romvault.com/RVService.svc");
            RVServiceClient rvSeriveClient = new RVServiceClient(basicHttpBinding, endPointAddress);

            if (string.IsNullOrEmpty(UISettings.Username) || string.IsNullOrEmpty(UISettings.EMail))
            {
                using (FrmRegistration fReg = new FrmRegistration())
                {
                    fReg.ShowDialog();
                }
            }

            Settings.rvSettings = new Settings();
            Settings.rvSettings = Settings.SetDefaults();

            ReportError.Username = UISettings.Username;
            ReportError.EMail = UISettings.EMail;
            ReportError.OptOut = UISettings.OptOut;
            ReportError.ErrorForm += ShowErrorForm;
            ReportError.Dialog += ShowDialog;

            try
            {
                if (!UISettings.OptOut)
                {
                    rvSeriveClient.SendUser(UISettings.Username, UISettings.EMail, VNow);
                    rvSeriveClient.StartUpV2(Version.Major, Version.Minor, Version.Build);
                }
            }
            catch
            {
            }

            try
            {
                ReportError.vMajor = Version.Major;
                ReportError.vMinor = Version.Minor;
                ReportError.vBuild = Version.Build;

                string strVersion = rvSeriveClient.LastestVersionCheck();
                string strThisVersion = $"{Version.Major}.{Version.Minor}.{Version.Build}";

                string[] strv = strVersion.Split('.');
                int vMajor = int.Parse(strv[0]);
                int vMinor = int.Parse(strv[1]);
                int vBuild = int.Parse(strv[2]);

                if (Version.Major < vMajor || (Version.Major == vMajor && Version.Minor < vMinor) || (Version.Major == vMajor && Version.Minor == vMinor && Version.Build < vBuild))
                {
                    string url = rvSeriveClient.GetUpdateLink();
                    DialogResult res = MessageBox.Show($"There is a new version v{strVersion}, do you want update now?\n{url} ", $"You are running v{strThisVersion}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                }
            }
            catch
            {
            }

            rvSeriveClient.Close();

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

        public static void ShowDialog(string text, string caption)
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }
}