using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Compress;
using Compress.ZipFile;
using DATReader.DatClean;
using RomVaultCore;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

namespace ROMVault
{
    public partial class FrmDirectorySettings : Form
    {
        private Color _cMagenta = Color.FromArgb(255, 214, 255);
        private Color _cGreen = Color.FromArgb(214, 255, 214);
        private Color _cYellow = Color.FromArgb(255, 255, 214);

        public bool ChangesMade;

        private DatRule _rule;
        private readonly ToolTip tooltip;

        public FrmDirectorySettings()
        {
            InitializeComponent();
            cboFileType.Items.Clear();
            cboFileType.Items.Add("Uncompressed");
            cboFileType.Items.Add("Zip");
            cboFileType.Items.Add("SevenZip");
            cboFileType.Items.Add("Mixed (Archive as File)");

            cboMergeType.Items.Clear();
            cboMergeType.Items.Add("Nothing");
            cboMergeType.Items.Add("Split");
            cboMergeType.Items.Add("Merge");
            cboMergeType.Items.Add("NonMerge");


            cboFilterType.Items.Clear();
            cboFilterType.Items.Add("Roms & CHDs");
            cboFilterType.Items.Add("Roms Only");
            cboFilterType.Items.Add("CHDs Only");


            cboDirType.Items.Clear();
            cboDirType.Items.Add("Use subdirs for all sets");
            cboDirType.Items.Add("Do not use subdirs for sets");
            cboDirType.Items.Add("Use subdirs for rom name conflicts");
            cboDirType.Items.Add("Use subdirs for multi-rom sets");
            cboDirType.Items.Add("Use subdirs for multi-rom sets or set/rom name mismatches");

            cboHeaderType.Items.Clear();
            cboHeaderType.Items.Add("Optional");
            cboHeaderType.Items.Add("Headered");
            cboHeaderType.Items.Add("Headerless");

            tooltip = new ToolTip
            {
                InitialDelay = 1000,
                ReshowDelay = 500
            };
            tooltip.AutoPopDelay = 32767;

            tooltip.SetToolTip(chkFileTypeOverride, "Checking this will force the selected archive type to be used.\nIf unchecked and if the DAT specifies an archive type the DATs archive type will override this setting.");
            tooltip.SetToolTip(chkMergeTypeOverride, "Checking this will force the selected merge type to be used.\nIf unchecked and if the DAT specifies a merge type the DATs merge type will override this setting.");

            tooltip.SetToolTip(chkMultiDatDirOverride, "If two or more DATs share a directory RomVault will automatically\nmake a sub-directory for each DAT so that they do not conflict with each other.\nChecking this will stop sub-directories being automatically added.");
            tooltip.SetToolTip(chkSingleArchive, "Checking this will turn the DATs in these directories into single archives.\n These archives will contain an internal sub-directories for each set in the DATs.\n(Don't use this with 'File' archive type as it will do nothing useful.)");
            tooltip.SetToolTip(chkUseDescription, "For the auto generated directories names & single archive names.\nRomVault will us the 'name' in the header of the DAT,\nChecking this will switch to using the 'description' in the header.\nIf there is no 'description' the 'name' tag will be used.\nIf there is no 'name' tag, the DAT filename will be used.");

            if (Settings.rvSettings.Darkness)
            {
                Dark.dark.SetColors(this);
                _cMagenta = Color.FromArgb((int)(255 * 0.8), (int)(214 * 0.8), (int)(255 * 0.8));
                _cGreen = Color.FromArgb((int)(214 * 0.8), (int)(255 * 0.8), (int)(214 * 0.8));
                _cYellow = Color.FromArgb((int)(255 * 0.8), (int)(255 * 0.8), (int)(214 * 0.8));

                DataGridGames.DefaultCellStyle.ForeColor = Color.Black;
            }

        }

        public void SetLocation(string dLocation)
        {
            _rule = FindRule(dLocation);
            SetDisplay();
            UpdateGrid();
        }

        private bool _displayType;
        public void SetDisplayType(bool type)
        {
            _displayType = type;
            btnDelete.Visible = type;

            foreach (object c in Controls)
            {
                if ((c is Control ct) && (ct.Top > 360))
                {
                    ct.Visible = !type;
                }
            }
            MinimumSize = new Size(709, type ? 335 : 500);
            Height = type ? 335 : 620;
            FormBorderStyle = type ? FormBorderStyle.FixedSingle : FormBorderStyle.Sizable;

        }

        private static DatRule FindRule(string dLocation)
        {
            foreach (DatRule t in Settings.rvSettings.DatRules)
            {
                if (string.Compare(t.DirKey, dLocation, StringComparison.Ordinal) == 0)
                    return t;
            }

            return new DatRule { DirKey = dLocation, IgnoreFiles = new List<string>() };
        }

        private void SetCompressionTypeFromArchive()
        {
            cboCompression.Items.Clear();
            switch (cboFileType.SelectedIndex)
            {
                case 0:
                    chkFileTypeOverride.Enabled = true;
                    cboCompression.Enabled = false;
                    chkConvertWhenFixing.Enabled = false;
                    break;
                case 1:
                    chkFileTypeOverride.Enabled = true;
                    cboCompression.Items.Add("Deflate - Trrntzip");
                    cboCompression.Items.Add("ZSTD");
                    cboCompression.Enabled = true;
                    chkConvertWhenFixing.Enabled = true;
                    if (_rule.CompressionSub == ZipStructure.ZipTrrnt)
                        cboCompression.SelectedIndex = 0;
                    else if (_rule.CompressionSub == ZipStructure.ZipZSTD)
                        cboCompression.SelectedIndex = 1;
                    else
                        cboCompression.SelectedIndex = 0;
                    break;
                case 2:
                    chkFileTypeOverride.Enabled = true;
                    cboCompression.Items.Add("LZMA Solid - rv7z");
                    cboCompression.Items.Add("LZMA Non-Solid");
                    cboCompression.Items.Add("ZSTD Solid");
                    cboCompression.Items.Add("ZSTD Non-Solid");
                    cboCompression.Enabled = true;
                    chkConvertWhenFixing.Enabled = true;
                    if (_rule.CompressionSub == ZipStructure.SevenZipSLZMA)
                        cboCompression.SelectedIndex = 0;
                    else if (_rule.CompressionSub == ZipStructure.SevenZipNLZMA)
                        cboCompression.SelectedIndex = 1;
                    else if (_rule.CompressionSub == ZipStructure.SevenZipSZSTD)
                        cboCompression.SelectedIndex = 2;
                    else if (_rule.CompressionSub == ZipStructure.SevenZipNZSTD)
                        cboCompression.SelectedIndex = 3;
                    else
                        cboCompression.SelectedIndex = 0;
                    break;
                case 3:
                    chkFileTypeOverride.Enabled = false;
                    cboCompression.Enabled = false;
                    chkConvertWhenFixing.Enabled = false;
                    break;
            }
        }


        private void cboFileType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetCompressionTypeFromArchive();
        }

        private void SetDisplay()
        {
            txtDATLocation.Text = _rule.DirKey;

            cboFileType.SelectedIndex = _rule.Compression == FileType.FileOnly ? 3 : (int)_rule.Compression - 1;
            chkFileTypeOverride.Checked = _rule.CompressionOverrideDAT;

            SetCompressionTypeFromArchive();
            chkConvertWhenFixing.Checked = _rule.ConvertWhileFixing;

            cboMergeType.SelectedIndex = (int)_rule.Merge;
            chkMergeTypeOverride.Checked = _rule.MergeOverrideDAT;

            cboFilterType.SelectedIndex = (int)_rule.Filter;

            chkMultiDatDirOverride.Checked = _rule.MultiDATDirOverride;
            chkUseDescription.Checked = _rule.UseDescriptionAsDirName;
            chkUseIdForName.Checked = _rule.UseIdForName;


            chkSingleArchive.Checked = _rule.SingleArchive;

            cboDirType.Enabled = chkSingleArchive.Checked;
            cboDirType.SelectedIndex = (int)_rule.SubDirType;

            cboHeaderType.SelectedIndex = (int)_rule.HeaderType;

            textBox1.Text = "";
            foreach (string file in _rule.IgnoreFiles)
            {
                textBox1.Text += file + Environment.NewLine;
            }

            chkCompleteOnly.Checked = _rule.CompleteOnly;

            chkAddCategorySubDirs.Checked = _rule.AddCategorySubDirs;
            if (_rule.AddCategorySubDirs)
                SetCategoryList();
        }


        private void UpdateGrid()
        {
            if (Settings.IsMono && DataGridGames.RowCount > 0)
            {
                DataGridGames.CurrentCell = DataGridGames[0, 0];
            }


            DataGridGames.Rows.Clear();
            foreach (DatRule t in Settings.rvSettings.DatRules)
            {
                DataGridGames.Rows.Add();
                int row = DataGridGames.Rows.Count - 1;
                DataGridGames.Rows[row].Tag = t;
                DataGridGames.Rows[row].Cells[0].Value = t.DirKey;
                DataGridGames.Rows[row].Cells[1].Value = t.CompressionSub;
                DataGridGames.Rows[row].Cells[2].Value = t.Merge;
                DataGridGames.Rows[row].Cells[3].Value = t.SingleArchive ? rvImages1.Tick : rvImages1.unTick;


                if (t.DirPath == "ToSort")
                {
                    for (int i = 0; i < 4; i++)
                        DataGridGames.Rows[row].Cells[i].Style.BackColor = _cMagenta;
                }
                else if (t == _rule)
                {
                    for (int i = 0; i < 4; i++)
                        DataGridGames.Rows[row].Cells[i].Style.BackColor = _cGreen;
                }
                else if (t.DirKey.Length > _rule.DirKey.Length)
                {
                    if (t.DirKey.Substring(0, _rule.DirKey.Length + 1) == _rule.DirKey + "\\")
                    {
                        for (int i = 0; i < 4; i++)
                            DataGridGames.Rows[row].Cells[i].Style.BackColor = _cYellow;
                    }
                }
            }

            for (int j = 0; j < DataGridGames.Rows.Count; j++)
            {
                DataGridGames.Rows[j].Selected = false;
            }
        }

        private ZipStructure ReadFromCheckBoxes()
        {
            if (cboFileType.SelectedIndex == 0)
                return ZipStructure.None;

            else if (cboFileType.SelectedIndex == 1)
            {
                if (cboCompression.SelectedIndex == 0)
                    return ZipStructure.ZipTrrnt;
                if (cboCompression.SelectedIndex == 1)
                    return ZipStructure.ZipZSTD;
            }
            else if (cboFileType.SelectedIndex == 2)
            {
                if (cboCompression.SelectedIndex == 0)
                    return ZipStructure.SevenZipSLZMA;
                if (cboCompression.SelectedIndex == 1)
                    return ZipStructure.SevenZipNLZMA;
                if (cboCompression.SelectedIndex == 2)
                    return ZipStructure.SevenZipSZSTD;
                if (cboCompression.SelectedIndex == 3)
                    return ZipStructure.SevenZipNZSTD;
            }
            else if (cboFileType.SelectedIndex == 3)
                return ZipStructure.None;

            return ZipStructure.None;
        }

        private void BtnApplyClick(object sender, EventArgs e)
        {
            ChangesMade = true;

            _rule.Compression = cboFileType.SelectedIndex == 3 ? FileType.FileOnly : (FileType)cboFileType.SelectedIndex + 1;
            _rule.CompressionOverrideDAT = chkFileTypeOverride.Checked;
            _rule.CompressionSub = ReadFromCheckBoxes();
            _rule.ConvertWhileFixing = chkConvertWhenFixing.Checked;
            _rule.Merge = (MergeType)cboMergeType.SelectedIndex;
            _rule.MergeOverrideDAT = chkMergeTypeOverride.Checked;
            _rule.Filter = (FilterType)cboFilterType.SelectedIndex;
            _rule.HeaderType = (HeaderType)cboHeaderType.SelectedIndex;
            _rule.SingleArchive = chkSingleArchive.Checked;
            _rule.SubDirType = (RemoveSubType)cboDirType.SelectedIndex;
            _rule.MultiDATDirOverride = chkMultiDatDirOverride.Checked;
            _rule.UseDescriptionAsDirName = chkUseDescription.Checked;
            _rule.UseIdForName = chkUseIdForName.Checked;

            _rule.CompleteOnly = chkCompleteOnly.Checked;

            _rule.AddCategorySubDirs = chkAddCategorySubDirs.Checked;


            string strtxt = textBox1.Text;
            strtxt = strtxt.Replace("\r", "");
            string[] strsplit = strtxt.Split('\n');

            _rule.IgnoreFiles = new List<string>(strsplit);
            int i;
            for (i = 0; i < _rule.IgnoreFiles.Count; i++)
            {
                _rule.IgnoreFiles[i] = _rule.IgnoreFiles[i].Trim();
                if (string.IsNullOrEmpty(_rule.IgnoreFiles[i]))
                {
                    _rule.IgnoreFiles.RemoveAt(i);
                    i--;
                }
            }

            bool updatingRule = false;
            for (i = 0; i < Settings.rvSettings.DatRules.Count; i++)
            {
                if (Settings.rvSettings.DatRules[i] == _rule)
                {
                    updatingRule = true;
                    break;
                }

                if (string.Compare(Settings.rvSettings.DatRules[i].DirKey, _rule.DirKey, StringComparison.Ordinal) > 0)
                {
                    break;
                }
            }

            if (!updatingRule)
                Settings.rvSettings.DatRules.Insert(i, _rule);

            Settings.rvSettings.SetRegExRules();

            UpdateGrid();
            Settings.WriteConfig(Settings.rvSettings);
            DatUpdate.CheckAllDats(DB.DirRoot.Child(0), _rule.DirKey);

            if (_displayType)
                Close();

        }
        private void BtnDeleteClick(object sender, EventArgs e)
        {
            string datLocation = _rule.DirKey;

            if (datLocation == "RomVault")
            {
                ReportError.Show("You cannot delete the " + datLocation + " Directory Settings", "RomVault Rom Location");
                return;
            }
            else
            {
                ChangesMade = true;

                DatUpdate.CheckAllDats(DB.DirRoot.Child(0), datLocation);
                for (int i = 0; i < Settings.rvSettings.DatRules.Count; i++)
                {
                    if (Settings.rvSettings.DatRules[i].DirKey == datLocation)
                    {
                        Settings.rvSettings.DatRules.RemoveAt(i);
                        i--;
                    }
                }
            }
            Settings.WriteConfig(Settings.rvSettings);

            UpdateGrid();
            Close();
        }

        private void BtnDeleteSelectedClick(object sender, EventArgs e)
        {
            ChangesMade = true;
            for (int j = 0; j < DataGridGames.SelectedRows.Count; j++)
            {
                string datLocation = DataGridGames.SelectedRows[j].Cells["CDAT"].Value.ToString();

                if (datLocation == "RomVault")
                {
                    ReportError.Show("You cannot delete the " + datLocation + " Directory Settings", "RomVault Rom Location");
                }
                else
                {
                    DatUpdate.CheckAllDats(DB.DirRoot.Child(0), datLocation);
                    for (int i = 0; i < Settings.rvSettings.DatRules.Count; i++)
                    {
                        if (Settings.rvSettings.DatRules[i].DirKey == datLocation)
                        {
                            Settings.rvSettings.DatRules.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            Settings.WriteConfig(Settings.rvSettings);

            UpdateGrid();
        }

        private void BtnCloseClick(object sender, EventArgs e)
        {
            Close();
        }

        private void DataGridGamesDoubleClick(object sender, EventArgs e)
        {
            if (DataGridGames.SelectedRows.Count <= 0)
            {
                return;
            }
            Text = "Edit Existing DATs Rule";
            _rule = (DatRule)DataGridGames.SelectedRows[0].Tag;
            UpdateGrid();
            SetDisplay();
        }

        private void FrmSetDirActivated(object sender, EventArgs e)
        {
            for (int j = 0; j < DataGridGames.Rows.Count; j++)
            {
                DataGridGames.Rows[j].Selected = false;
            }
        }

        private void BtnResetAllClick(object sender, EventArgs e)
        {
            ChangesMade = true;
            for (int i = 0; i < Settings.rvSettings.DatRules.Count; i++)
            {
                DatRule _rule = Settings.rvSettings.DatRules[i];

                if (_rule.Compression != FileType.Zip ||
                    _rule.CompressionOverrideDAT ||
                    _rule.Merge != MergeType.Split ||
                    _rule.HeaderType != HeaderType.Optional ||
                    _rule.MergeOverrideDAT ||
                    _rule.SubDirType != RemoveSubType.KeepAllSubDirs ||
                    _rule.SingleArchive ||
                    _rule.MultiDATDirOverride ||
                    _rule.UseDescriptionAsDirName ||
                    _rule.UseIdForName ||
                    _rule.CompleteOnly)
                    DatUpdate.CheckAllDats(DB.DirRoot.Child(0), _rule.DirKey);
            }

            Settings.rvSettings.ResetDatRules();
            Settings.WriteConfig(Settings.rvSettings);
            _rule = Settings.rvSettings.DatRules[0];
            UpdateGrid();
            SetDisplay();
        }

        private void chkSingleArchive_CheckedChanged(object sender, EventArgs e)
        {
            cboDirType.Enabled = chkSingleArchive.Checked;
        }

        private void chkAddCategorySubDirs_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkAddCategorySubDirs.Checked)
            {
                dgCategories.Enabled = false;
                btnUp.Enabled = false;
                btnDown.Enabled = false;
                return;
            }

            dgCategories.Enabled = true;
            btnUp.Enabled = true;
            btnDown.Enabled = true;

            if (_rule.CategoryOrder == null || _rule.CategoryOrder.Count == 0)
            {
                _rule.CategoryOrder = new List<string>()
                {
                    "Preproduction",
                    "Educational",
                    "Guides",
                    "Manuals",
                    "Magazines",
                    "Documents",
                    "Audio",
                    "Video",
                    "Multimedia",
                    "Coverdiscs",
                    "Covermount",
                    "Bonus Discs",
                    "Bonus",
                    "Add-Ons",
                    "Source Code",
                    "Updates",
                    "Applications",
                    "Demos",
                    "Games",
                    "Miscellaneous"
                };
            }
            SetCategoryList();
        }

        private void SetCategoryList()
        {
            dgCategories.Rows.Clear();
            foreach (string s in _rule.CategoryOrder)
            {
                dgCategories.Rows.Add(s);
            }
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            if (dgCategories.SelectedRows.Count == 0)
                return;

            int idx = dgCategories.SelectedRows[0].Index;
            if (idx == 0)
                return;
            string v = _rule.CategoryOrder[idx];
            _rule.CategoryOrder[idx] = _rule.CategoryOrder[idx - 1];
            _rule.CategoryOrder[idx - 1] = v;
            dgCategories.Rows[idx].Cells[0].Value = _rule.CategoryOrder[idx];
            dgCategories.Rows[idx - 1].Cells[0].Value = _rule.CategoryOrder[idx - 1];
            dgCategories.Rows[idx - 1].Selected = true;

            int selectedIndex = idx - 1 - 4;
            selectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
            dgCategories.FirstDisplayedScrollingRowIndex = selectedIndex;
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            if (dgCategories.SelectedRows.Count == 0)
                return;

            int idx = dgCategories.SelectedRows[0].Index;
            if (idx == _rule.CategoryOrder.Count - 1)
                return;
            string v = _rule.CategoryOrder[idx];
            _rule.CategoryOrder[idx] = _rule.CategoryOrder[idx + 1];
            _rule.CategoryOrder[idx + 1] = v;
            dgCategories.Rows[idx].Cells[0].Value = _rule.CategoryOrder[idx];
            dgCategories.Rows[idx + 1].Cells[0].Value = _rule.CategoryOrder[idx + 1];
            dgCategories.Rows[idx + 1].Selected = true;

            int selectedIndex = idx + 1 - 4;
            selectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
            dgCategories.FirstDisplayedScrollingRowIndex = selectedIndex;
        }
    }
}
