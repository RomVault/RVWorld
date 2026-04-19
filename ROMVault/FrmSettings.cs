/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.Utils;
using RomVaultCore.RvDB;

namespace ROMVault
{
    /// <summary>
    /// Application settings dialog (scan, fix, compression, CHD, UI, and performance options).
    /// </summary>
    public partial class FrmSettings : Form
    {
        private CheckBox chkChdCache;
        private CheckBox chkChdDebug;
        private CheckBox chkChdKeepCueGdi;
        private CheckBox chkChdExportOnFix;
        private CheckBox chkChdStreaming;
        private CheckBox chkChdPreferSynthetic;
        private NumericUpDown upChdDvdHunk;
        private ComboBox cboChdNumProcessors;
        private Button btnPurgeChdCache;
        private bool _loadingSettings;
        public FrmSettings()
        {
            InitializeComponent();

            cboFixLevel.Items.Clear();
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

            if (Settings.rvSettings.Darkness)
                Dark.dark.SetColors(this);

            int padding = 10;
            int groupWidth = tabCHD.Width - 20;

            // --- Scanning Group ---
            GroupBox grpScanning = new GroupBox();
            grpScanning.Text = "Scanning & Cache";
            grpScanning.Left = padding;
            grpScanning.Top = padding;
            grpScanning.Width = groupWidth;
            grpScanning.Height = 160;
            grpScanning.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tabCHD.Controls.Add(grpScanning);

            chkChdCache = new CheckBox { Text = "Enable CHD scan cache", Left = 15, Top = 25, AutoSize = true };
            grpScanning.Controls.Add(chkChdCache);

            chkChdDebug = new CheckBox { Text = "Write CHD scan debug logs", Left = 15, Top = 50, AutoSize = true };
            grpScanning.Controls.Add(chkChdDebug);

            chkChdStreaming = new CheckBox { Text = "Enable CHD streaming (experimental)", Left = 15, Top = 75, AutoSize = true };
            chkChdStreaming.CheckedChanged += (s, e) =>
            {
                if (_loadingSettings)
                    return;
                if (chkChdStreaming.Checked)
                    MessageBox.Show("Enables CHD streaming hashing without extracting (DVD/ISO and some CD/GDI when metadata is available).", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            grpScanning.Controls.Add(chkChdStreaming);

            // --- Fixing Group ---
            GroupBox grpFixing = new GroupBox();
            grpFixing.Text = "Fixing & Policy";
            grpFixing.Left = padding;
            grpFixing.Top = grpScanning.Bottom + padding;
            grpFixing.Width = groupWidth;
            grpFixing.Height = 230;
            grpFixing.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tabCHD.Controls.Add(grpFixing);

            chkChdExportOnFix = new CheckBox { Text = "Allow exporting tracks during Fix", Left = 15, Top = 25, AutoSize = true };
            chkChdExportOnFix.CheckedChanged += (s, e) =>
            {
                if (_loadingSettings)
                    return;
                if (chkChdExportOnFix.Checked)
                    MessageBox.Show("This will extract track files from CHD into your ROM folders during fixing. It is off by default because it defeats CHD container storage.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            grpFixing.Controls.Add(chkChdExportOnFix);

            chkChdKeepCueGdi = new CheckBox { Text = "Default: Keep .cue / .gdi alongside CHDs", Left = 15, Top = 50, AutoSize = true };
            chkChdKeepCueGdi.CheckedChanged += (s, e) =>
            {
                if (_loadingSettings)
                    return;
                if (chkChdKeepCueGdi.Checked)
                    MessageBox.Show("When enabled, CHD sets are modeled as a folder containing:\r\n- <set>.chd\r\n- <set>.cue/.gdi (sidecar)\r\nThis takes effect after Refresh All DATs.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            grpFixing.Controls.Add(chkChdKeepCueGdi);

            Label lblDvdHunk = new Label { Text = "DVD hunk size (KiB)", Left = 15, Top = 80, AutoSize = true };
            grpFixing.Controls.Add(lblDvdHunk);
            upChdDvdHunk = new NumericUpDown { Left = 130, Top = 77, Minimum = 0, Maximum = 1024, Increment = 32, Width = 90 };
            grpFixing.Controls.Add(upChdDvdHunk);

            Label lblChdNp = new Label { Text = "chdman processors (-np)", Left = 15, Top = 110, AutoSize = true };
            grpFixing.Controls.Add(lblChdNp);
            cboChdNumProcessors = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 165, Top = 107, Width = 85 };
            cboChdNumProcessors.Items.Add("Auto");
            for (int i = 1; i <= 64; i++)
                cboChdNumProcessors.Items.Add(i.ToString());
            grpFixing.Controls.Add(cboChdNumProcessors);

            // --- Maintenance Group ---
            GroupBox grpMaint = new GroupBox();
            grpMaint.Text = "Maintenance";
            grpMaint.Left = padding;
            grpMaint.Top = grpFixing.Bottom + padding;
            grpMaint.Width = groupWidth;
            grpMaint.Height = 70;
            grpMaint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tabCHD.Controls.Add(grpMaint);

            btnPurgeChdCache = new Button { Text = "Purge CHD Scan Cache", Left = 15, Top = 25, Width = 200, Height = 28 };
            btnPurgeChdCache.Click += (s, e) =>
            {
                try
                {
                    string baseTempDir = DB.GetToSortCache()?.FullName ?? Environment.CurrentDirectory;
                    string dir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdscanCache");
                    if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
                    MessageBox.Show("CHD scan cache purged.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show("Failed to purge cache: " + ex.Message, "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            grpMaint.Controls.Add(btnPurgeChdCache);
        }

        private void FrmConfigLoad(object sender, EventArgs e)
        {
            _loadingSettings = true;
            lblDATRoot.Text = Settings.rvSettings.DatRoot;
            cboFixLevel.SelectedIndex = (int)Settings.rvSettings.FixLevel;

            textBox1.Text = "";
            foreach (string file in Settings.rvSettings.IgnoreFiles)
            {
                textBox1.Text += file + Environment.NewLine;
            }
            chkSendFoundMIA.Checked = Settings.rvSettings.MIACallback;
            chkSendFoundMIAAnon.Checked = Settings.rvSettings.MIAAnon;

            chkDetailedReporting.Checked = Settings.rvSettings.DetailedFixReporting;
            chkDoubleCheckDelete.Checked = Settings.rvSettings.DoubleCheckDelete;
            chkCacheSaveTimer.Checked = Settings.rvSettings.CacheSaveTimerEnabled;
            upTime.Value = Settings.rvSettings.CacheSaveTimePeriod;
            chkDebugLogs.Checked = Settings.rvSettings.DebugLogsEnabled;
            chkDeleteOldCueFiles.Checked = Settings.rvSettings.DeleteOldCueFiles;
            cboCores.SelectedIndex = Settings.rvSettings.zstdCompCount >= cboCores.Items.Count ? 0 : Settings.rvSettings.zstdCompCount;
            cbo7zStruct.SelectedIndex = Settings.rvSettings.sevenZDefaultStruct;
            chkDarkMode.Checked = Settings.rvSettings.Darkness;
            chkDoNotReportFeedback.Checked = Settings.rvSettings.DoNotReportFeedback;

            chkChdCache.Checked = Settings.rvSettings.ChdScanCacheEnabled;
            chkChdDebug.Checked = Settings.rvSettings.ChdDebug;
            chkChdExportOnFix.Checked = Settings.rvSettings.ChdExportTracksOnFix;
            chkChdStreaming.Checked = Settings.rvSettings.ChdStreaming;
            chkChdKeepCueGdi.Checked = Settings.rvSettings.ChdKeepCueGdi;
            upChdDvdHunk.Value = Settings.rvSettings.ChdDvdHunkSizeKiB;
            int np = Settings.rvSettings.ChdNumProcessors;
            if (np < 0) np = 0;
            if (np > 64) np = 0;
            cboChdNumProcessors.SelectedIndex = np;
            _loadingSettings = false;
        }

        private void BtnCancelClick(object sender, EventArgs e)
        {
            Close();
        }

        private void BtnOkClick(object sender, EventArgs e)
        {
            Settings.rvSettings.DatRoot = lblDATRoot.Text;
            Settings.rvSettings.FixLevel = (EFixLevel)cboFixLevel.SelectedIndex;
            string strtxt = textBox1.Text;
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

            Settings.rvSettings.DetailedFixReporting = chkDetailedReporting.Checked;
            Settings.rvSettings.DoubleCheckDelete = chkDoubleCheckDelete.Checked;
            Settings.rvSettings.DebugLogsEnabled = chkDebugLogs.Checked;
            Settings.rvSettings.CacheSaveTimerEnabled = chkCacheSaveTimer.Checked;
            Settings.rvSettings.CacheSaveTimePeriod = (int)upTime.Value;

            Settings.rvSettings.MIACallback = chkSendFoundMIA.Checked;
            Settings.rvSettings.MIAAnon = chkSendFoundMIAAnon.Checked;
            Settings.rvSettings.DeleteOldCueFiles = chkDeleteOldCueFiles.Checked;

            Settings.rvSettings.zstdCompCount = cboCores.SelectedIndex;

            Settings.rvSettings.sevenZDefaultStruct = cbo7zStruct.SelectedIndex;
            Settings.rvSettings.Darkness = chkDarkMode.Checked;

            Settings.rvSettings.DoNotReportFeedback = chkDoNotReportFeedback.Checked;

            Settings.rvSettings.ChdScanCacheEnabled = chkChdCache.Checked;
            Settings.rvSettings.ChdDebug = chkChdDebug.Checked;
            Settings.rvSettings.ChdExportTracksOnFix = chkChdExportOnFix.Checked;
            Settings.rvSettings.ChdStreaming = chkChdStreaming.Checked;
            Settings.rvSettings.ChdKeepCueGdi = chkChdKeepCueGdi.Checked;
            Settings.rvSettings.ChdDvdHunkSizeKiB = (int)upChdDvdHunk.Value;
            Settings.rvSettings.ChdNumProcessors = cboChdNumProcessors.SelectedIndex;

            Settings.WriteConfig(Settings.rvSettings);
            Close();
        }

        private void BtnDatClick(object sender, EventArgs e)
        {
            FolderBrowserDialog browse = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                Description = "Select a folder for DAT Root",
                RootFolder = Environment.SpecialFolder.MyComputer,
                SelectedPath = Settings.rvSettings.DatRoot
            };

            if (browse.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            lblDATRoot.Text = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, browse.SelectedPath);
        }

        private void chkSendFoundMIA_CheckedChanged(object sender, EventArgs e)
        {
            chkSendFoundMIAAnon.Enabled = chkSendFoundMIA.Checked;
        }

    }
}
