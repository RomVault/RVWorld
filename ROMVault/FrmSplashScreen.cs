/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.Windows.Forms;
using RVCore;
using RVCore.RvDB;

namespace ROMVault
{
    public partial class FrmSplashScreen : Form
    {
        private double _opacityIncrement = 0.05;
        private readonly ThreadWorker _thWrk;
        
        public FrmSplashScreen()
        {
            InitializeComponent();
            lblVersion.Text = $@"Version {Program.StrVersion} : {Application.StartupPath}";
            Opacity = 0;
            timer1.Interval = 50;

            label1.Text = $@"Registered to : {Settings.Username}                      Contact Email : {Settings.EMail}";


            _thWrk = new ThreadWorker(StartUpCode) {wReport = BgwProgressChanged, wFinal = BgwRunWorkerCompleted};
        }

        private void FrmSplashScreenShown(object sender, EventArgs e)
        {
            _thWrk.StartAsync();
            timer1.Start();
        }


        private static void StartUpCode(ThreadWorker e)
        {
            RepairStatus.InitStatusCheck();
            Settings.rvSettings=Settings.SetDefaults();
            DB.Read(e);
        }


        private void BgwProgressChanged(object e)
        {

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => BgwProgressChanged(e)));
                return;
            }

            if (e is int percent)
            {
                if (percent >= progressBar.Minimum && percent <= progressBar.Maximum)
                {
                    progressBar.Value = percent;
                }
                return;
            }
            bgwSetRange bgwSr = e as bgwSetRange;
            if (bgwSr != null)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = bgwSr.MaxVal;
                progressBar.Value = 0;
                return;
            }

            bgwText bgwT = e as bgwText;
            if (bgwT != null)
            {
                lblStatus.Text = bgwT.Text;
            }
        }

        private void BgwRunWorkerCompleted()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => BgwRunWorkerCompleted()));
                return;
            }

            _opacityIncrement = -0.1;
            timer1.Start();
        }

        private void Timer1Tick(object sender, EventArgs e)
        {
            if (_opacityIncrement > 0)
            {
                if (Opacity < 1)
                {
                    Opacity += _opacityIncrement;
                }
                else
                {
                    timer1.Stop();
                }
            }
            else
            {
                if (Opacity > 0)
                {
                    Opacity += _opacityIncrement;
                }
                else
                {
                    timer1.Stop();
                    Close();
                }
            }
        }
    }
}