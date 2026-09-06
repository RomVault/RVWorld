/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using Extensions;
using RomVaultCore;
using RomVaultCore.Utils;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ROMVault
{
    public partial class FrmSettings : Form
    {
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

            txtDATVaultKey.BorderStyle = BorderStyle.FixedSingle;
        }

        int previousMIADays = 0;
        public bool MIADaysChanged = false;
        public bool previousShowNewMIA = false;
        public bool previousEnableNewMIA = false;

        private void FrmConfigLoad(object sender, EventArgs e)
        {
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

            upMIADays.Value = Settings.rvSettings.MIADays;
            chkShowNewMIA.Checked = Settings.rvSettings.ShowNewMIA;

            previousMIADays = Settings.rvSettings.MIADays;
            previousShowNewMIA = Settings.rvSettings.ShowNewMIA;

            txtDATVaultKey.Text = Settings.rvSettings.DATUpdateKey;

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

            Settings.rvSettings.MIADays = (int)upMIADays.Value;
            Settings.rvSettings.ShowNewMIA = chkShowNewMIA.Checked;

            Settings.rvSettings.DATUpdateKey = txtDATVaultKey.Text;

            Settings.WriteConfig();

            MIADaysChanged = Settings.rvSettings.MIADays != previousMIADays;

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

        private void picEye_Click(object sender, EventArgs e)
        {
            txtDATVaultKey.PasswordChar = txtDATVaultKey.PasswordChar > 0 ? (char)0 : '*';
        }

        private void btnValidate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDATVaultKey.Text))
            {
                MessageBox.Show("Please enter a DATVault key.", "DATVault Key Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool res = ExtHelper.ValidateKey(txtDATVaultKey.Text);
            MessageBox.Show((res ? "Valid" : "Invalid") + " Key", "DATVault Key Validation", MessageBoxButtons.OK, res ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }


    }
}