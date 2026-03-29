using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RomVaultCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// A window for displaying the progress of background operations (scanning, updating, fixing).
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private ThreadWorker? _thWrk;
        private bool _isClosing;
        private DateTime _dateTime;
        private DateTime _dateTimeLast;

        public bool ShowTimeLog { get; set; } = false;
        private bool _errorOpen = false;
        private ObservableCollection<LogEntry> _logEntries;

        /// <summary>
        /// Represents a log entry in the error/log grid.
        /// </summary>
        public class LogEntry
        {
            public string? Time { get; set; }
            public string? Log { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressWindow"/> class.
        /// </summary>
        public ProgressWindow()
        {
            InitializeComponent();
            _logEntries = new ObservableCollection<LogEntry>();
            var errorGrid = this.FindControl<DataGrid>("ErrorGrid");
            if (errorGrid != null) errorGrid.ItemsSource = _logEntries;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressWindow"/> class with a background worker.
        /// </summary>
        /// <param name="thWrk">The worker thread instance.</param>
        public ProgressWindow(ThreadWorker thWrk) : this()
        {
            _thWrk = thWrk;
            _thWrk.wReport += BgwProgressChanged;
            _thWrk.wFinal += BgwRunWorkerCompleted;
            
            _dateTime = DateTime.Now;
            _dateTimeLast = _dateTime;

            this.Closing += ProgressWindow_Closing;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Handles the window closing event.
        /// Prevents closing if the background worker is still running, unless explicitly cancelled.
        /// </summary>
        private void ProgressWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing && _thWrk != null && !_thWrk.Finished)
            {
                e.Cancel = true;
                _thWrk.Cancel();
                var btnCancel = this.FindControl<Button>("btnCancel");
                if (btnCancel != null) btnCancel.IsEnabled = false;
            }
        }

        /// <summary>
        /// Handles the Cancel button click.
        /// Cancels the background operation or closes the window if finished.
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            var btnCancel = this.FindControl<Button>("btnCancel");
            if (_isClosing || (btnCancel != null && btnCancel.Content?.ToString() == "Close"))
            {
                _isClosing = true;
                Close();
                return;
            }

            if (_thWrk != null && !_thWrk.Finished)
            {
                _thWrk.Cancel();
                if (btnCancel != null) btnCancel.IsEnabled = false;
            }
            else
            {
                _isClosing = true;
                Close();
            }
        }

        /// <summary>
        /// Updates the UI based on progress reports from the background worker.
        /// Handles progress bars, status text, and error logging.
        /// </summary>
        /// <param name="obj">The progress object sent by the worker.</param>
        private void BgwProgressChanged(object obj)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (obj is int e)
                {
                    var progressBar1 = this.FindControl<ProgressBar>("progressBar1");
                    if (progressBar1 != null) progressBar1.Value = e;
                    return;
                }

                if (obj is bgwText bgwT)
                {
                    var lblMessage = this.FindControl<TextBlock>("lblMessage");
                    if (lblMessage != null) lblMessage.Text = bgwT.Text;
                    
                    if (ShowTimeLog) TimeLogShow(bgwT.Text);
                    return;
                }

                if (obj is bgwSetRange bgwSr)
                {
                    var progressBar1 = this.FindControl<ProgressBar>("progressBar1");
                    if (progressBar1 != null)
                    {
                        progressBar1.Minimum = 0;
                        progressBar1.Maximum = bgwSr.MaxVal >= 0 ? bgwSr.MaxVal : 0;
                        progressBar1.Value = 0;
                    }
                    return;
                }
                
                if (obj is bgwText2 bgwT2)
                {
                    var lblMessage2 = this.FindControl<TextBlock>("lblMessage2");
                    if (lblMessage2 != null) 
                    {
                        lblMessage2.Text = bgwT2.Text;
                        lblMessage2.IsVisible = true;
                    }
                    return;
                }

                if (obj is bgwValue2 bgwV2)
                {
                    var progressBar2 = this.FindControl<ProgressBar>("progressBar2");
                    if (progressBar2 != null)
                    {
                         progressBar2.Value = bgwV2.Value;
                         progressBar2.IsVisible = true;
                    }
                    return;
                }

                if (obj is bgwSetRange2 bgwSr2)
                {
                    var progressBar2 = this.FindControl<ProgressBar>("progressBar2");
                    if (progressBar2 != null)
                    {
                        progressBar2.Minimum = 0;
                        progressBar2.Maximum = bgwSr2.MaxVal >= 0 ? bgwSr2.MaxVal : 0;
                        progressBar2.Value = 0;
                        progressBar2.IsVisible = true;
                    }
                    return;
                }
                
                if (obj is bgwRange2Visible bgwR2V)
                {
                    var lblMessage2 = this.FindControl<TextBlock>("lblMessage2");
                    var progressBar2 = this.FindControl<ProgressBar>("progressBar2");
                    if (lblMessage2 != null) lblMessage2.IsVisible = bgwR2V.Visible;
                    if (progressBar2 != null) progressBar2.IsVisible = bgwR2V.Visible;
                    return;
                }

                if (obj is bgwShowError bgwE)
                {
                    var errorGrid = this.FindControl<DataGrid>("ErrorGrid");
                    if (!_errorOpen)
                    {
                        _errorOpen = true;
                        this.Height = 292;
                        if (errorGrid != null)
                        {
                            errorGrid.IsVisible = true;
                        }
                    }
                    
                    _logEntries.Add(new LogEntry { Time = bgwE.error, Log = bgwE.filename });
                    return;
                }
            });
        }

        /// <summary>
        /// Logs a timed message to the error/log grid.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private void TimeLogShow(string message)
        {
            var errorGrid = this.FindControl<DataGrid>("ErrorGrid");
            if (!_errorOpen)
            {
                _errorOpen = true;
                this.Height = 292;
                if (errorGrid != null)
                {
                    errorGrid.IsVisible = true;
                }
            }

            DateTime dtNow = DateTime.Now;
            string total = Math.Round((dtNow - _dateTime).TotalSeconds, 3).ToString();
            string step = Math.Round((dtNow - _dateTimeLast).TotalSeconds, 3).ToString();
            _dateTimeLast = dtNow;

            _logEntries.Add(new LogEntry { Time = total + " (" + step + ")", Log = message });
            
            if (errorGrid != null && _logEntries.Count > 0)
            {
                errorGrid.ScrollIntoView(_logEntries[_logEntries.Count - 1], null);
            }
        }

        /// <summary>
        /// Handles the completion of the background worker.
        /// Changes the Cancel button to "Close" and enables closing the window.
        /// </summary>
        private void BgwRunWorkerCompleted()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var btnCancel = this.FindControl<Button>("btnCancel");
                if (btnCancel != null)
                {
                    btnCancel.Content = "Close";
                    btnCancel.IsEnabled = true;
                }
                _isClosing = true; // Allow closing now
            });
        }
    }
}
