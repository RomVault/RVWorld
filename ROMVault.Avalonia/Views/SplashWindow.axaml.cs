using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using System;
using System.Reflection;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// Splash screen window displayed during application startup.
    /// Handles database loading and initialization in a background thread.
    /// </summary>
    public partial class SplashWindow : Window
    {
        private double _opacityIncrement = 0.05;
        private readonly ThreadWorker _thWrk;
        private readonly DispatcherTimer _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplashWindow"/> class.
        /// Sets up the version label, fade timer, and background worker.
        /// </summary>
        public SplashWindow()
        {
            InitializeComponent();
            
            var version = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0, 0);
            string strVersion = $"{version.Major}.{version.Minor}.{version.Build}";
            if (version.Revision > 0)
                strVersion += $" WIP{version.Revision}";
            
            var lblVersion = this.FindControl<TextBlock>("lblVersion");
            if (lblVersion != null)
                lblVersion.Text = $"Version {strVersion} : {AppDomain.CurrentDomain.BaseDirectory}";

            Opacity = 0;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _timer.Tick += Timer1Tick;

            _thWrk = new ThreadWorker(StartUpCode) { wReport = BgwProgressChanged, wFinal = BgwRunWorkerCompleted };
            
            Opened += SplashWindow_Opened;
        }

        /// <summary>
        /// Event handler for when the window is opened.
        /// Starts the background worker and the fade-in timer.
        /// </summary>
        private void SplashWindow_Opened(object? sender, EventArgs e)
        {
            _thWrk.StartAsync();
            _timer.Start();
        }

        /// <summary>
        /// The main startup code executed on the background thread.
        /// Initializes repair status and reads the database.
        /// </summary>
        /// <param name="thWrk">The worker thread instance.</param>
        private static void StartUpCode(ThreadWorker thWrk)
        {
            RepairStatus.InitStatusCheck();
            DB.Read(thWrk);
            if (DB.DirRoot != null)
            {
                RepairStatus.ReportStatusReset(DB.DirRoot);
            }
        }

        /// <summary>
        /// Updates the UI based on progress reports from the background worker.
        /// Updates the progress bar and status label.
        /// </summary>
        /// <param name="e">The progress object sent by the worker.</param>
        private void BgwProgressChanged(object e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var progressBar = this.FindControl<ProgressBar>("progressBar");
                var lblStatus = this.FindControl<TextBlock>("lblStatus");

                if (e is int percent)
                {
                    if (progressBar != null && percent >= progressBar.Minimum && percent <= progressBar.Maximum)
                    {
                        progressBar.Value = percent;
                    }
                    return;
                }
                
                if (e is bgwSetRange bgwSr)
                {
                    if (progressBar != null)
                    {
                        progressBar.Minimum = 0;
                        progressBar.Maximum = bgwSr.MaxVal;
                        progressBar.Value = 0;
                    }
                    return;
                }

                if (e is bgwText bgwT)
                {
                    if (lblStatus != null)
                    {
                        lblStatus.Text = bgwT.Text;
                    }
                }
            });
        }

        /// <summary>
        /// Handles the completion of the background worker.
        /// Triggers the fade-out animation.
        /// </summary>
        private void BgwRunWorkerCompleted()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _opacityIncrement = -0.1;
                _timer.Start();
            });
        }

        /// <summary>
        /// Timer tick event handler for fade-in and fade-out animations.
        /// Closes the window when fade-out is complete.
        /// </summary>
        private void Timer1Tick(object? sender, EventArgs e)
        {
            if (_opacityIncrement > 0)
            {
                if (Opacity < 1)
                {
                    Opacity += _opacityIncrement;
                }
                else
                {
                    _timer.Stop();
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
                    _timer.Stop();
                    Close();
                }
            }
        }
    }
}
