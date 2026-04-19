using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace ROMVault.Avalonia.Views;

/// <summary>
    /// Window for configuring global application settings.
    /// Handles DAT root location, fix levels, file ignore rules, and other preferences.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
            InitializeControls();
            LoadSettings();
        }

        /// <summary>
        /// Sets up the dropdown items and event handlers.
        /// </summary>
        private void InitializeControls()
        {
            cboFixLevel.Items.Add("Level 1 - Fast copy Match on CRC");
            cboFixLevel.Items.Add("Level 2 - Fast copy if SHA1 scanned");
            cboFixLevel.Items.Add("Level 3 - Uncompress/Hash/Compress");

            cboCores.Items.Add("Auto");
            for (int i = 1; i <= 64; i++)
                cboCores.Items.Add(i.ToString());

            cbo7zStruct.Items.Add("LZMA Solid - rv7z");
            cbo7zStruct.Items.Add("LZMA Non-Solid");
            cbo7zStruct.Items.Add("ZSTD Solid");
            cbo7zStruct.Items.Add("ZSTD Non-Solid");

            cboChdNumProcessors.Items.Clear();
            cboChdNumProcessors.Items.Add("Auto");
            for (int i = 1; i <= 64; i++)
                cboChdNumProcessors.Items.Add(i.ToString());

            chkSendFoundMIA.Click += (s, e) => chkSendFoundMIAAnon.IsEnabled = chkSendFoundMIA.IsChecked == true;

            chkChdExportOnFix.Click += async (s, e) =>
            {
                if (chkChdExportOnFix.IsChecked == true)
                {
                    await MessageBoxWindow.ShowInfo(this, "This will extract track files from CHD into your ROM folders during fixing. It is off by default because it defeats CHD container storage.", "RomVault");
                }
            };
            chkChdKeepCueGdi.Click += async (s, e) =>
            {
                if (chkChdKeepCueGdi.IsChecked == true)
                {
                    await MessageBoxWindow.ShowInfo(this, "When enabled, CHD sets are modeled as a folder containing:\n- <set>.chd\n- <set>.cue/.gdi (sidecar)\nThis takes effect after Update DATs.", "RomVault");
                }
            };
            chkChdStreaming.Click += async (s, e) =>
            {
                if (chkChdStreaming.IsChecked == true)
                {
                    await MessageBoxWindow.ShowInfo(this, "Enables CHD streaming hashing without extracting (DVD/ISO and some CD/GDI when metadata is available).", "RomVault");
                }
            };
            chkChdPreferSynthetic.Click += async (s, e) =>
            {
                if (chkChdPreferSynthetic.IsChecked == true)
                {
                    await MessageBoxWindow.ShowInfo(this, "Prefer synthetic CUE/GDI when metadata-only descriptors are acceptable (no strict hashes).", "RomVault");
                }
            };
            btnPurgeChdCache.Click += async (s, e) =>
            {
                try
                {
                    string baseTempDir = DB.GetToSortCache()?.FullName ?? Environment.CurrentDirectory;
                    string dir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdscanCache");
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                    await MessageBoxWindow.ShowInfo(this, "CHD scan cache purged.", "RomVault");
                }
                catch (Exception ex)
                {
                    await MessageBoxWindow.ShowInfo(this, "Failed to purge cache: " + ex.Message, "RomVault");
                }
            };
            
            btnDAT.Click += BtnDatClick;
            btnOK.Click += BtnOkClick;
            btnCancel.Click += BtnCancelClick;
        }

        /// <summary>
        /// Loads the current settings into the UI controls.
        /// </summary>
        private void LoadSettings()
        {
            lblDATRoot.Text = Settings.rvSettings.DatRoot;
            cboFixLevel.SelectedIndex = (int)Settings.rvSettings.FixLevel;

            textBox1.Text = "";
            foreach (string file in Settings.rvSettings.IgnoreFiles)
            {
                textBox1.Text += file + Environment.NewLine;
            }
            chkSendFoundMIA.IsChecked = Settings.rvSettings.MIACallback;
            chkSendFoundMIAAnon.IsChecked = Settings.rvSettings.MIAAnon;
            chkSendFoundMIAAnon.IsEnabled = Settings.rvSettings.MIACallback;

            chkDetailedReporting.IsChecked = Settings.rvSettings.DetailedFixReporting;
            chkDoubleCheckDelete.IsChecked = Settings.rvSettings.DoubleCheckDelete;
            chkCacheSaveTimer.IsChecked = Settings.rvSettings.CacheSaveTimerEnabled;
            upTime.Value = Settings.rvSettings.CacheSaveTimePeriod;
            chkDebugLogs.IsChecked = Settings.rvSettings.DebugLogsEnabled;
            chkDeleteOldCueFiles.IsChecked = Settings.rvSettings.DeleteOldCueFiles;
            
            cboCores.SelectedIndex = Settings.rvSettings.zstdCompCount >= cboCores.ItemCount ? 0 : Settings.rvSettings.zstdCompCount;
            cbo7zStruct.SelectedIndex = Settings.rvSettings.sevenZDefaultStruct;
            chkDarkMode.IsChecked = Settings.rvSettings.Darkness;
            chkDoNotReportFeedback.IsChecked = Settings.rvSettings.DoNotReportFeedback;

            chkChdCache.IsChecked = Settings.rvSettings.ChdScanCacheEnabled;
            chkChdDebug.IsChecked = Settings.rvSettings.ChdDebug;
            chkChdExportOnFix.IsChecked = Settings.rvSettings.ChdExportTracksOnFix;
            chkChdStreaming.IsChecked = Settings.rvSettings.ChdStreaming;
            chkChdKeepCueGdi.IsChecked = Settings.rvSettings.ChdKeepCueGdi;
            upChdDvdHunk.Value = Settings.rvSettings.ChdDvdHunkSizeKiB;
            int np = Settings.rvSettings.ChdNumProcessors;
            if (np < 0) np = 0;
            if (np > 64) np = 0;
            cboChdNumProcessors.SelectedIndex = np;
        }

        /// <summary>
        /// Opens a folder picker to select the DAT root directory.
        /// </summary>
        private async void BtnDatClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a folder for DAT Root",
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            string selectedPath = folders[0].Path.LocalPath;
            lblDATRoot.Text = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, selectedPath);
        }

        /// <summary>
        /// Saves the settings and closes the window.
        /// </summary>
        private void BtnOkClick(object? sender, RoutedEventArgs e)
        {
            Settings.rvSettings.DatRoot = lblDATRoot.Text;
            Settings.rvSettings.FixLevel = (EFixLevel)cboFixLevel.SelectedIndex;
            
            string strtxt = textBox1.Text ?? "";
            strtxt = strtxt.Replace("\r", "");
            string[] strsplit = strtxt.Split('\n');

            Settings.rvSettings.IgnoreFiles = new List<string>(strsplit);
            for (int i = 0; i < Settings.rvSettings.IgnoreFiles.Count; i++)
            {
                Settings.rvSettings.IgnoreFiles[i] = Settings.rvSettings.IgnoreFiles[i].Trim();
                if (string.IsNullOrEmpty(Settings.rvSettings.IgnoreFiles[i]))
                {
                    Settings.rvSettings.IgnoreFiles.RemoveAt(i);
                    i--;
                }
            }
            Settings.rvSettings.SetRegExRules();

            Settings.rvSettings.DetailedFixReporting = chkDetailedReporting.IsChecked == true;
            Settings.rvSettings.DoubleCheckDelete = chkDoubleCheckDelete.IsChecked == true;
            Settings.rvSettings.DebugLogsEnabled = chkDebugLogs.IsChecked == true;
            Settings.rvSettings.CacheSaveTimerEnabled = chkCacheSaveTimer.IsChecked == true;
            Settings.rvSettings.CacheSaveTimePeriod = (int)(upTime.Value ?? 10);

            Settings.rvSettings.MIACallback = chkSendFoundMIA.IsChecked == true;
            Settings.rvSettings.MIAAnon = chkSendFoundMIAAnon.IsChecked == true;
            Settings.rvSettings.DeleteOldCueFiles = chkDeleteOldCueFiles.IsChecked == true;

            Settings.rvSettings.zstdCompCount = cboCores.SelectedIndex;

            Settings.rvSettings.sevenZDefaultStruct = cbo7zStruct.SelectedIndex;
            Settings.rvSettings.Darkness = chkDarkMode.IsChecked == true;

            Settings.rvSettings.DoNotReportFeedback = chkDoNotReportFeedback.IsChecked == true;

            Settings.rvSettings.ChdScanCacheEnabled = chkChdCache.IsChecked == true;
            Settings.rvSettings.ChdDebug = chkChdDebug.IsChecked == true;
            Settings.rvSettings.ChdExportTracksOnFix = chkChdExportOnFix.IsChecked == true;
            Settings.rvSettings.ChdStreaming = chkChdStreaming.IsChecked == true;
            Settings.rvSettings.ChdKeepCueGdi = chkChdKeepCueGdi.IsChecked == true;
            Settings.rvSettings.ChdDvdHunkSizeKiB = (int)(upChdDvdHunk.Value ?? 0);
            Settings.rvSettings.ChdNumProcessors = cboChdNumProcessors.SelectedIndex;

            Settings.WriteConfig(Settings.rvSettings);
            
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = Settings.rvSettings.Darkness ? ThemeVariant.Dark : ThemeVariant.Light;
            }
            Close();
        }

        /// <summary>
        /// Closes the window without saving changes.
        /// </summary>
        private void BtnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
