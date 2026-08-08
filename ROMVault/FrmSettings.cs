/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RomVaultCore.Utils;

namespace ROMVault
{
    public partial class FrmSettings : Form
    {
        private CheckBox _chkChdCache;
        private CheckBox _chkChdDebug;
        private CheckBox _chkChdStreaming;
        private CheckBox _chkChdPreferSynthetic;
        private CheckBox _chkChdCheckVersion;
        private CheckBox _chkChdStrict;
        private CheckBox _chkChdKeepCueGdi;
        private CheckBox _chkChdExportOnFix;
        private CheckBox _chkChdRecompressOnEncoderUpdate;
        private CheckBox _chkChdMultiView;
        private CheckBox _chkChdNativeVerify;
        private CheckBox _chkChdHealth;
        private CheckBox _chkChdScheduledScrub;
        private NumericUpDown _numChdParityDays;
        private NumericUpDown _numChdScheduledBatch;
        private ComboBox _cboChdHddGeometry;
        private TextBox _txtChdmanPaths;
        private TextBox _txtChdmanPin;
        private ComboBox _cboChdNumProcessors;
        private Label _lblChdmanIdentity;

        private bool _chdDatPolicyChanged;

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

            AddSettingsTabs();

            if (Settings.rvSettings.Darkness)
                Dark.dark.SetColors(this);
        }

        private void AddSettingsTabs()
        {
            TabControl tabs = new TabControl
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(ClientSize.Width, 490),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            TabPage generalTab = new TabPage("General") { AutoScroll = true };
            generalTab.Controls.AddRange(new Control[]
            {
                label1, lblDATRoot, btnDAT, textBox1, label4, cboFixLevel, label3,
                chkDebugLogs, chkCacheSaveTimer, upTime, label5, chkDoubleCheckDelete,
                chkDetailedReporting, chkSendFoundMIA, chkSendFoundMIAAnon,
                chkDeleteOldCueFiles, lblSide1, lblSide3, lblSide4, label2, label6,
                label7, cbo7zStruct, cboCores, chkDarkMode, chkDoNotReportFeedback
            });

            TabPage chdTab = new TabPage("CHD") { AutoScroll = true };
            BuildChdTab(chdTab);

            tabs.TabPages.Add(generalTab);
            tabs.TabPages.Add(chdTab);
            Controls.Add(tabs);
            tabs.SendToBack();
        }

        private void BuildChdTab(TabPage tab)
        {
            const int margin = 10;
            int groupWidth = Math.Max(400, ClientSize.Width - 35);

            GroupBox scanning = new GroupBox
            {
                Text = "Scanning and validation",
                Location = new System.Drawing.Point(margin, margin),
                Size = new System.Drawing.Size(groupWidth, 300)
            };
            _chkChdCache = AddCheckBox(scanning, "Cache CHD scan results for this session", 24);
            _chkChdDebug = AddCheckBox(scanning, "Write CHD scan debug logs", 47);
            _chkChdStreaming = AddCheckBox(scanning, "Stream logical CHD data when supported", 70);
            _chkChdPreferSynthetic = AddCheckBox(scanning, "Prefer metadata-generated descriptors when a DAT has none", 93);
            _chkChdCheckVersion = AddCheckBox(scanning, "Check the CHD format version during scans", 116);
            _chkChdMultiView = AddCheckBox(scanning, "Store proven equivalent CUE/BIN and ISO representations in one reversible CHD", 139);
            _chkChdNativeVerify = AddCheckBox(scanning, "Allow native streaming only after an external parity check", 162);
            _chkChdHealth = AddCheckBox(scanning, "Keep a local redacted CHD health database for scheduled parity checks", 185);
            _chkChdScheduledScrub = AddCheckBox(scanning, "Enable bounded scheduled scrubs (runs only when explicitly started)", 208);
            AddLabel(scanning, "External parity expires after (days):", 239);
            _numChdParityDays = new NumericUpDown
            {
                Location = new System.Drawing.Point(230, 235),
                Size = new System.Drawing.Size(75, 21),
                Minimum = 1,
                Maximum = 3650
            };
            scanning.Controls.Add(_numChdParityDays);
            AddLabel(scanning, "Maximum CHDs per scheduled run:", 266);
            _numChdScheduledBatch = new NumericUpDown
            {
                Location = new System.Drawing.Point(230, 262),
                Size = new System.Drawing.Size(75, 21),
                Minimum = 1,
                Maximum = 10000
            };
            scanning.Controls.Add(_numChdScheduledBatch);
            tab.Controls.Add(scanning);

            GroupBox policy = new GroupBox
            {
                Text = "Disc policy and chdman",
                Location = new System.Drawing.Point(margin, scanning.Bottom + margin),
                Size = new System.Drawing.Size(groupWidth, 355)
            };
            _chkChdStrict = AddCheckBox(policy, "Require matching .cue/.gdi/.toc descriptors", 24);
            _chkChdKeepCueGdi = AddCheckBox(policy, "Keep .cue/.gdi/.toc descriptors alongside CHDs (SBI is embedded)", 47);
            _chkChdExportOnFix = AddCheckBox(policy, "Allow track export while fixing", 70);
            _chkChdRecompressOnEncoderUpdate = AddCheckBox(policy, "Recompress standard CHDs after every newer validated chdman (otherwise only relevant writer fixes)", 93);

            AddLabel(policy, "Standard profile v6: authenticated exact views, zstd/cdzs Playback, archival codecs, TOC/SBI, HDD, and LaserDisc", 121);

            _lblChdmanIdentity = new Label
            {
                Text = ChdmanDiagnostics.GetSummary(false),
                Location = new System.Drawing.Point(14, 144),
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(groupWidth - 28, 45)
            };
            policy.Controls.Add(_lblChdmanIdentity);

            AddLabel(policy, "HDD geometry:", 199);
            _cboChdHddGeometry = AddComboBox(policy, 180, 195, 155, "Auto by profile", "Canonical exact", "Emulator compatible", "Preserve if possible");

            AddLabel(policy, "chdman processors (-np):", 229);
            _cboChdNumProcessors = AddComboBox(policy, 180, 225, 90, "Auto");
            for (int i = 1; i <= 64; i++)
                _cboChdNumProcessors.Items.Add(i.ToString());

            AddLabel(policy, "Additional chdman paths (one per line):", 258);
            _txtChdmanPaths = new TextBox
            {
                Location = new System.Drawing.Point(230, 254),
                Size = new System.Drawing.Size(Math.Max(180, groupWidth - 245), 45),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            policy.Controls.Add(_txtChdmanPaths);
            AddLabel(policy, "Pinned tool SHA-256:", 310);
            _txtChdmanPin = new TextBox
            {
                Location = new System.Drawing.Point(150, 306),
                Size = new System.Drawing.Size(Math.Max(250, groupWidth - 165), 21)
            };
            policy.Controls.Add(_txtChdmanPin);
            tab.Controls.Add(policy);

            GroupBox maintenance = new GroupBox
            {
                Text = "Maintenance",
                Location = new System.Drawing.Point(margin, policy.Bottom + margin),
                Size = new System.Drawing.Size(groupWidth, 238)
            };
            Button purgeCache = new Button
            {
                Text = "Clear CHD scan cache",
                Location = new System.Drawing.Point(14, 23),
                Size = new System.Drawing.Size(180, 26)
            };
            purgeCache.Click += PurgeChdCache;
            maintenance.Controls.Add(purgeCache);
            Button testChdman = new Button
            {
                Text = "Re-test chdman",
                Location = new System.Drawing.Point(205, 23),
                Size = new System.Drawing.Size(150, 26)
            };
            testChdman.Click += ReTestChdman;
            maintenance.Controls.Add(testChdman);
            Button openChdLogs = new Button
            {
                Text = "Open CHD log folder",
                Location = new System.Drawing.Point(14, 56),
                Size = new System.Drawing.Size(180, 26)
            };
            openChdLogs.Click += OpenChdLogFolder;
            maintenance.Controls.Add(openChdLogs);
            Button addTool = new Button
            {
                Text = "Add and pin chdman...",
                Location = new System.Drawing.Point(205, 56),
                Size = new System.Drawing.Size(150, 26)
            };
            addTool.Click += AddAndPinChdman;
            maintenance.Controls.Add(addTool);
            Button toolReport = new Button
            {
                Text = "Toolchain report",
                Location = new System.Drawing.Point(366, 23),
                Size = new System.Drawing.Size(140, 26)
            };
            toolReport.Click += ShowToolchainReport;
            maintenance.Controls.Add(toolReport);
            Button scrub = new Button
            {
                Text = "Full scrub folder...",
                Location = new System.Drawing.Point(366, 56),
                Size = new System.Drawing.Size(140, 26)
            };
            scrub.Click += FullScrubFolder;
            maintenance.Controls.Add(scrub);
            Button health = new Button
            {
                Text = "Health DB summary",
                Location = new System.Drawing.Point(14, 91),
                Size = new System.Drawing.Size(180, 26)
            };
            health.Click += ShowHealthDatabase;
            maintenance.Controls.Add(health);
            Button planScrubs = new Button
            {
                Text = "Plan due scrubs...",
                Location = new System.Drawing.Point(205, 91),
                Size = new System.Drawing.Size(150, 26)
            };
            planScrubs.Click += PlanScheduledScrubs;
            maintenance.Controls.Add(planScrubs);
            Button runScrubs = new Button
            {
                Text = "Run due scrubs...",
                Location = new System.Drawing.Point(366, 91),
                Size = new System.Drawing.Size(140, 26)
            };
            runScrubs.Click += RunScheduledScrubs;
            maintenance.Controls.Add(runScrubs);
            Button createParity = new Button
            {
                Text = "Create recovery volume...",
                Location = new System.Drawing.Point(14, 124),
                Size = new System.Drawing.Size(180, 26)
            };
            createParity.Click += CreateRecoveryVolume;
            maintenance.Controls.Add(createParity);
            Button verifyParity = new Button
            {
                Text = "Verify recovery volume...",
                Location = new System.Drawing.Point(205, 124),
                Size = new System.Drawing.Size(150, 26)
            };
            verifyParity.Click += VerifyRecoveryVolume;
            maintenance.Controls.Add(verifyParity);
            Button repairParity = new Button
            {
                Text = "Recover CHDs...",
                Location = new System.Drawing.Point(366, 124),
                Size = new System.Drawing.Size(140, 26)
            };
            repairParity.Click += RepairFromRecoveryVolume;
            maintenance.Controls.Add(repairParity);
            Button planConversion = new Button
            {
                Text = "Plan conversion...",
                Location = new System.Drawing.Point(14, 157),
                Size = new System.Drawing.Size(180, 26)
            };
            planConversion.Click += PlanChdConversion;
            maintenance.Controls.Add(planConversion);
            Button runConversion = new Button
            {
                Text = "Resume conversion...",
                Location = new System.Drawing.Point(205, 157),
                Size = new System.Drawing.Size(150, 26)
            };
            runConversion.Click += RunChdConversion;
            maintenance.Controls.Add(runConversion);
            Button conversionStatus = new Button
            {
                Text = "Conversion status...",
                Location = new System.Drawing.Point(366, 157),
                Size = new System.Drawing.Size(140, 26)
            };
            conversionStatus.Click += ShowChdConversionStatus;
            maintenance.Controls.Add(conversionStatus);
            AddLabel(maintenance, "Recovery and conversion are transactional; damaged/source CHDs are never edited in place.", 195);
            tab.Controls.Add(maintenance);
        }

        private static CheckBox AddCheckBox(Control parent, string text, int top)
        {
            CheckBox checkBox = new CheckBox
            {
                Text = text,
                Location = new System.Drawing.Point(14, top),
                AutoSize = true
            };
            parent.Controls.Add(checkBox);
            return checkBox;
        }

        private static void AddLabel(Control parent, string text, int top)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Location = new System.Drawing.Point(14, top),
                AutoSize = true
            });
        }

        private static ComboBox AddComboBox(Control parent, int left, int top, int width, params string[] items)
        {
            ComboBox comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new System.Drawing.Point(left, top),
                Size = new System.Drawing.Size(width, 21)
            };
            comboBox.Items.AddRange(items);
            parent.Controls.Add(comboBox);
            return comboBox;
        }

        private static void PurgeChdCache(object sender, EventArgs e)
        {
            try
            {
                Populate.PurgeChdScanCache();
                MessageBox.Show("The in-memory CHD scan cache was cleared.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The CHD scan cache could not be purged: " + ex.Message,
                    "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReTestChdman(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                ChdmanDiagnostics.ClearCapabilityCache();
                _lblChdmanIdentity.Text = ChdmanDiagnostics.GetSummary(true);
                MessageBox.Show(_lblChdmanIdentity.Text, "chdman Capability Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The chdman capability test failed: " + ex.Message, "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private static void OpenChdLogFolder(object sender, EventArgs e)
        {
            string directory = Populate.GetChdScanDebugDirectory();
            if (!Directory.Exists(directory))
            {
                MessageBox.Show(
                    "No CHD diagnostic logs have been written. Enable 'Write CHD scan debug logs' and scan a CHD first.",
                    "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("The CHD diagnostic log folder could not be opened: " + ex.Message,
                    "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddAndPinChdman(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "chdman executable|chdman.exe|Executables|*.exe|All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string path = dialog.FileName;
                List<string> paths = ParseToolPaths(_txtChdmanPaths.Text);
                if (!paths.Exists(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase)))
                    paths.Add(path);
                _txtChdmanPaths.Text = string.Join(Environment.NewLine, paths.ToArray());
                string sha;
                string error;
                if (ChdmanDiagnostics.TryGetBinarySha256(path, out sha, out error))
                    _txtChdmanPin.Text = sha;
                else
                    MessageBox.Show("Could not identify that chdman: " + error, "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowToolchainReport(object sender, EventArgs e)
        {
            Settings.rvSettings.ChdmanPaths = ParseToolPaths(_txtChdmanPaths.Text);
            Settings.rvSettings.ChdPinnedToolSha256 = _txtChdmanPin.Text.Trim();
            Cursor = Cursors.WaitCursor;
            try { MessageBox.Show(ChdmanDiagnostics.GetToolchainReport(true), "chdman toolchains", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally { Cursor = Cursors.Default; }
        }

        private void FullScrubFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder to fully scrub all CHDs";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                Cursor = Cursors.WaitCursor;
                try
                {
                    string report;
                    int code = ChdScrubber.Scrub(dialog.SelectedPath, ChdScrubMode.Full, out report);
                    MessageBox.Show(report, code == 0 ? "CHD scrub passed" : "CHD scrub found problems", MessageBoxButtons.OK,
                        code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                finally { Cursor = Cursors.Default; }
            }
        }

        private static void ShowHealthDatabase(object sender, EventArgs e)
        {
            MessageBox.Show(ChdHealthStore.BuildReport(), "CHD health database", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PlanScheduledScrubs(object sender, EventArgs e)
        {
            WithSelectedChdFolder("Select a CHD collection to plan", delegate(string root)
            {
                int code = ChdScrubScheduler.Plan(root, (int)_numChdParityDays.Value, (int)_numChdScheduledBatch.Value, out _, out string report);
                MessageBox.Show(report, code == 0 ? "CHD scrub plan" : "CHD scrub plan failed", MessageBoxButtons.OK,
                    code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        private void RunScheduledScrubs(object sender, EventArgs e)
        {
            if (!_chkChdScheduledScrub.Checked)
            {
                MessageBox.Show("Enable bounded scheduled scrubs first. Planning remains available while the scheduler is disabled.",
                    "Scheduled CHD scrub disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            WithSelectedChdFolder("Select a CHD collection to scrub", delegate(string root)
            {
                int code = ChdScrubScheduler.RunDue(root, ChdScrubMode.Full, (int)_numChdParityDays.Value, (int)_numChdScheduledBatch.Value, out string report);
                MessageBox.Show(report, code == 0 ? "Scheduled CHD scrub" : "Scheduled CHD scrub found problems", MessageBoxButtons.OK,
                    code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        private void CreateRecoveryVolume(object sender, EventArgs e)
        {
            WithSelectedChdFolder("Select the CHD collection", delegate(string root)
            {
                using (SaveFileDialog save = new SaveFileDialog { Filter = "RomVault CHD recovery volume|*.rvpar", DefaultExt = "rvpar", AddExtension = true })
                {
                    if (save.ShowDialog(this) != DialogResult.OK) return;
                    int code = ChdCollectionParity.Create(root, save.FileName, 8, 1024 * 1024, out string report);
                    MessageBox.Show(report, code == 0 ? "Recovery volume created" : "Recovery volume failed", MessageBoxButtons.OK,
                        code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            });
        }

        private void VerifyRecoveryVolume(object sender, EventArgs e)
        {
            if (!TrySelectRecoveryVolume(out string volume)) return;
            WithSelectedChdFolder("Select the CHD collection", delegate(string root)
            {
                int code = ChdCollectionParity.Verify(volume, root, out string report);
                MessageBox.Show(report, code == 0 ? "Recovery volume verified" : "Recovery volume mismatch", MessageBoxButtons.OK,
                    code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        private void RepairFromRecoveryVolume(object sender, EventArgs e)
        {
            if (!TrySelectRecoveryVolume(out string volume)) return;
            WithSelectedChdFolder("Select the damaged CHD collection", delegate(string root)
            {
                WithSelectedChdFolder("Select an empty/dedicated folder for recovered candidates", delegate(string output)
                {
                    int code = ChdCollectionParity.Repair(volume, root, output, out string report);
                    MessageBox.Show(report, code == 0 ? "CHD recovery candidates written" : "CHD recovery failed", MessageBoxButtons.OK,
                        code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                });
            });
        }

        private void PlanChdConversion(object sender, EventArgs e)
        {
            DialogResult choice = MessageBox.Show("Choose the target profile:\r\n\r\nYes = Playback (zstd/cdzs)\r\nNo = Archive (maximum safe compression)",
                "CHD conversion profile", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel) return;
            ChdStorageProfile target = choice == DialogResult.Yes ? ChdStorageProfile.Playback : ChdStorageProfile.Archive;
            WithSelectedChdFolder("Select the CHD collection to plan", delegate(string root)
            {
                using (SaveFileDialog save = new SaveFileDialog { Filter = "RomVault CHD conversion queue|*.rvchdqueue", DefaultExt = "rvchdqueue", AddExtension = true })
                {
                    if (save.ShowDialog(this) != DialogResult.OK) return;
                    int code = ChdConversionQueue.Create(root, target, save.FileName, out string report);
                    MessageBox.Show(report, code == 0 ? "CHD conversion plan" : "CHD conversion plan has blocked items", MessageBoxButtons.OK,
                        code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            });
        }

        private void RunChdConversion(object sender, EventArgs e)
        {
            if (!TrySelectConversionQueue(out string queue)) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                int code = ChdConversionQueue.Run(queue, (int)_numChdScheduledBatch.Value, out string report);
                MessageBox.Show(report, code == 0 ? "CHD conversion queue" : "CHD conversion queue has failures", MessageBoxButtons.OK,
                    code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void ShowChdConversionStatus(object sender, EventArgs e)
        {
            if (!TrySelectConversionQueue(out string queue)) return;
            int code = ChdConversionQueue.Status(queue, out string report);
            MessageBox.Show(report, code == 0 ? "CHD conversion queue" : "CHD conversion queue error", MessageBoxButtons.OK,
                code == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private bool TrySelectConversionQueue(out string queue)
        {
            using (OpenFileDialog open = new OpenFileDialog { Filter = "RomVault CHD conversion queue|*.rvchdqueue|All files|*.*" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) { queue = ""; return false; }
                queue = open.FileName;
                return true;
            }
        }

        private bool TrySelectRecoveryVolume(out string volume)
        {
            using (OpenFileDialog open = new OpenFileDialog { Filter = "RomVault CHD recovery volume|*.rvpar|All files|*.*" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) { volume = ""; return false; }
                volume = open.FileName;
                return true;
            }
        }

        private void WithSelectedChdFolder(string description, Action<string> action)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = description })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Cursor = Cursors.WaitCursor;
                try { action(dialog.SelectedPath); }
                finally { Cursor = Cursors.Default; }
            }
        }

        private static List<string> ParseToolPaths(string text)
        {
            List<string> result = new List<string>();
            string[] lines = (text ?? "").Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string value = lines[i].Trim().Trim('"');
                if (value.Length > 0 && !result.Exists(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
                    result.Add(value);
            }
            return result;
        }

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

            _chkChdCache.Checked = Settings.rvSettings.ChdScanCacheEnabled;
            _chkChdDebug.Checked = Settings.rvSettings.ChdDebug;
            _chkChdStreaming.Checked = Settings.rvSettings.ChdStreaming;
            _chkChdPreferSynthetic.Checked = Settings.rvSettings.ChdPreferSynthetic;
            _chkChdCheckVersion.Checked = Settings.rvSettings.CheckCHDVersion;
            _chkChdStrict.Checked = Settings.rvSettings.ChdStrictCueGdi;
            _chkChdKeepCueGdi.Checked = Settings.rvSettings.ChdKeepCueGdi;
            _chkChdExportOnFix.Checked = Settings.rvSettings.ChdExportTracksOnFix;
            _chkChdRecompressOnEncoderUpdate.Checked = Settings.rvSettings.ChdRecompressOnEncoderUpdate;
            _chkChdMultiView.Checked = Settings.rvSettings.ChdMultiView;
            _chkChdNativeVerify.Checked = Settings.rvSettings.ChdNativeVerification;
            _chkChdHealth.Checked = Settings.rvSettings.ChdHealthDatabase;
            _chkChdScheduledScrub.Checked = Settings.rvSettings.ChdScheduledScrub;
            _numChdParityDays.Value = Math.Max(_numChdParityDays.Minimum, Math.Min(_numChdParityDays.Maximum, Settings.rvSettings.ChdExternalParityDays));
            _numChdScheduledBatch.Value = Math.Max(_numChdScheduledBatch.Minimum, Math.Min(_numChdScheduledBatch.Maximum, Settings.rvSettings.ChdScheduledScrubBatch));
            _cboChdHddGeometry.SelectedIndex = (int)Settings.rvSettings.ChdHddGeometry;
            _txtChdmanPaths.Text = string.Join(Environment.NewLine, (Settings.rvSettings.ChdmanPaths ?? new List<string>()).ToArray());
            _txtChdmanPin.Text = Settings.rvSettings.ChdPinnedToolSha256 ?? "";
            int processors = Settings.rvSettings.ChdNumProcessors;
            _cboChdNumProcessors.SelectedIndex = processors >= 0 && processors < _cboChdNumProcessors.Items.Count
                ? processors
                : 0;
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

            _chdDatPolicyChanged = Settings.rvSettings.ChdStrictCueGdi != _chkChdStrict.Checked ||
                                   Settings.rvSettings.ChdKeepCueGdi != _chkChdKeepCueGdi.Checked ||
                                   Settings.rvSettings.ChdMultiView != _chkChdMultiView.Checked;

            Settings.rvSettings.ChdScanCacheEnabled = _chkChdCache.Checked;
            Settings.rvSettings.ChdDebug = _chkChdDebug.Checked;
            Settings.rvSettings.ChdStreaming = _chkChdStreaming.Checked;
            Settings.rvSettings.ChdPreferSynthetic = _chkChdPreferSynthetic.Checked;
            Settings.rvSettings.CheckCHDVersion = _chkChdCheckVersion.Checked;
            Settings.rvSettings.ChdStrictCueGdi = _chkChdStrict.Checked;
            Settings.rvSettings.ChdKeepCueGdi = _chkChdKeepCueGdi.Checked;
            Settings.rvSettings.ChdExportTracksOnFix = _chkChdExportOnFix.Checked;
            Settings.rvSettings.ChdRecompressOnEncoderUpdate = _chkChdRecompressOnEncoderUpdate.Checked;
            Settings.rvSettings.ChdNumProcessors = _cboChdNumProcessors.SelectedIndex;
            Settings.rvSettings.ChdMultiView = _chkChdMultiView.Checked;
            Settings.rvSettings.ChdNativeVerification = _chkChdNativeVerify.Checked;
            Settings.rvSettings.ChdHealthDatabase = _chkChdHealth.Checked;
            Settings.rvSettings.ChdScheduledScrub = _chkChdScheduledScrub.Checked;
            Settings.rvSettings.ChdExternalParityDays = (int)_numChdParityDays.Value;
            Settings.rvSettings.ChdScheduledScrubBatch = (int)_numChdScheduledBatch.Value;
            Settings.rvSettings.ChdHddGeometry = (ChdHddGeometryMode)Math.Max(0, _cboChdHddGeometry.SelectedIndex);
            Settings.rvSettings.ChdmanPaths = ParseToolPaths(_txtChdmanPaths.Text);
            Settings.rvSettings.ChdPinnedToolSha256 = _txtChdmanPin.Text.Trim();

            SynchronizeRootChdRule();

            Settings.WriteConfig();
            if (_chdDatPolicyChanged)
                DatUpdate.InvalidateAllDATs(DB.DirRoot.Child(0), "RomVault");
            Close();
        }

        private static void SynchronizeRootChdRule()
        {
            if (Settings.rvSettings.DatRules == null)
                return;

            foreach (DatRule rule in Settings.rvSettings.DatRules)
            {
                if (!string.Equals(rule.DirKey, "RomVault", StringComparison.OrdinalIgnoreCase))
                    continue;

                rule.ChdStrictCueGdi = Settings.rvSettings.ChdStrictCueGdi;
                rule.ChdKeepCueGdi = Settings.rvSettings.ChdKeepCueGdi;
                rule.ChdMultiView = Settings.rvSettings.ChdMultiView;
                rule.ChdHddGeometry = Settings.rvSettings.ChdHddGeometry;
                break;
            }
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
