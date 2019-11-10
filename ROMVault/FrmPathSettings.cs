using System;
using System.Drawing;
using System.Windows.Forms;
using ROMVault.Utils;
using RVCore;
using RVCore.ReadDat;
using RVCore.RvDB;
using RVCore.Utils;

namespace ROMVault
{
    public partial class FrmSetDirSettings : Form
    {
        private readonly Color _cMagenta = Color.FromArgb(255, 214, 255);
        private readonly Color _cGreen = Color.FromArgb(214, 255, 214);
        private readonly Color _cYellow = Color.FromArgb(255, 255, 214);

        public bool ChangesMade;

        private DatRule _rule;

        public FrmSetDirSettings()
        {
            InitializeComponent();
            cboFileType.Items.Clear();
            cboFileType.Items.Add("File");
            cboFileType.Items.Add("Zip");
            cboFileType.Items.Add("SevenZip");

            cboMergeType.Items.Clear();
            cboMergeType.Items.Add("Nothing");
            cboMergeType.Items.Add("Split");
            cboMergeType.Items.Add("Merge");
            cboMergeType.Items.Add("NonMerge");


            cboFilterType.Items.Clear();
            cboFilterType.Items.Add("Roms & CHDs");
            cboFilterType.Items.Add("Roms Only");
            cboFilterType.Items.Add("CHDs Only");
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
            Height = type ? 238 : 525;
            foreach (object c in Controls)
            {
                if ((c is Control ct) && (ct.Top > 200))
                {
                    ct.Visible = !type;
                }
            }
        }

        private static DatRule FindRule(string dLocation)
        {
            foreach (DatRule t in Settings.rvSettings.DatRules)
            {
                if (string.Compare(t.DirKey, dLocation, StringComparison.Ordinal) == 0)
                    return t;
            }

            return new DatRule { DirKey = dLocation };
        }


        private void SetDisplay()
        {
            txtDATLocation.Text = _rule.DirKey;
            //txtROMLocation.Text = DBHelper.GetPhysicalPath(_rule.DirKey);
            txtROMLocation.Text = _rule.DirPath;

            cboFileType.SelectedIndex = (int)_rule.Compression - 1;
            chkFileTypeOverride.Checked = _rule.CompressionOverrideDAT;

            cboMergeType.SelectedIndex = (int)_rule.Merge;
            chkMergeTypeOverride.Checked = _rule.MergeOverrideDAT;

            cboFilterType.SelectedIndex = (int) _rule.Filter;

            chkSingleArchive.Checked = _rule.SingleArchive;
            chkMultiDatDirOverride.Checked = _rule.MultiDATDirOverride;
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
                DataGridGames.Rows[row].Cells["CDAT"].Value = t.DirKey;
                DataGridGames.Rows[row].Cells["CROM"].Value = t.DirPath;

                if (t.DirPath == "ToSort")
                {
                    DataGridGames.Rows[row].Cells["CDAT"].Style.BackColor = _cMagenta;
                    DataGridGames.Rows[row].Cells["CROM"].Style.BackColor = _cMagenta;
                }
                else if (t == _rule)
                {
                    DataGridGames.Rows[row].Cells["CDAT"].Style.BackColor = _cGreen;
                    DataGridGames.Rows[row].Cells["CROM"].Style.BackColor = _cGreen;
                }
                else if (t.DirKey.Length > _rule.DirKey.Length)
                {
                    if (t.DirKey.Substring(0, _rule.DirKey.Length+1) == _rule.DirKey+"\\")
                    {
                        DataGridGames.Rows[row].Cells["CDAT"].Style.BackColor = _cYellow;
                        DataGridGames.Rows[row].Cells["CROM"].Style.BackColor = _cYellow;
                    }
                }
                /*
                DataGridGames.Rows[row].Cells["CDAT"].Value = t.DirKey;
                DataGridGames.Rows[row].Cells["CCompressionType"].Value = t.Compression;
                DataGridGames.Rows[row].Cells["CCompressionOverride"].Value = t.CompressionOverrideDAT;
                DataGridGames.Rows[row].Cells["CMergeType"].Value = t.Merge;
                DataGridGames.Rows[row].Cells["CMergeTypeOverride"].Value = t.MergeOverrideDAT;
                 */
            }

            for (int j = 0; j < DataGridGames.Rows.Count; j++)
            {
                DataGridGames.Rows[j].Selected = false;
            }
        }


        private void btnClearROMLocation_Click(object sender, EventArgs e)
        {
            if (_rule.DirKey == "RomVault")
            {
                txtROMLocation.Text = "RomRoot";
                return;
            }

            if (_rule.DirKey == "ToSort")
            {
                txtROMLocation.Text = "ToSort";
                return;
            }

            txtROMLocation.Text = null;
        }

        private void BtnSetROMLocationClick(object sender, EventArgs e)
        {
            FolderBrowserDialog browse = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                Description = "Please select a folder for This Rom Set",
                //RootFolder = Environment.SpecialFolder.MyComputer,
                SelectedPath = RvFile.GetPhysicalPath(_rule.DirKey)
            };
            if (browse.ShowDialog() == DialogResult.OK)
            {
                string relPath = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, browse.SelectedPath);
                txtROMLocation.Text = relPath;
            }
        }

        private void BtnApplyClick(object sender, EventArgs e)
        {
            ChangesMade = true;

            _rule.DirPath = txtROMLocation.Text;
            _rule.Compression = (FileType)cboFileType.SelectedIndex + 1;
            _rule.CompressionOverrideDAT = chkFileTypeOverride.Checked;
            _rule.Merge = (MergeType)cboMergeType.SelectedIndex;
            _rule.MergeOverrideDAT = chkMergeTypeOverride.Checked;
            _rule.Filter = (FilterType) cboFilterType.SelectedIndex;
            _rule.SingleArchive = chkSingleArchive.Checked;
            _rule.MultiDATDirOverride = chkMultiDatDirOverride.Checked;

            bool updatingRule = false;
            int i;
            for (i = 0; i < Settings.rvSettings.DatRules.Count; i++)
            {
                if (Settings.rvSettings.DatRules[i] == _rule)
                {
                    updatingRule = true;
                    break;
                }

                if (string.Compare(Settings.rvSettings.DatRules[i].DirKey, _rule.DirKey) > 0)
                {
                    break;
                }
            }

            if (!updatingRule)
                Settings.rvSettings.DatRules.Insert(i, _rule);

            UpdateGrid();
            Settings.WriteConfig(Settings.rvSettings);
            DatUpdate.CheckAllDats(DB.DirTree.Child(0), _rule.DirKey);

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

                DatUpdate.CheckAllDats(DB.DirTree.Child(0), datLocation);
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
                    DatUpdate.CheckAllDats(DB.DirTree.Child(0), datLocation);
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

            grpBoxAddNew.Text = "Edit Existing Directory Mapping";
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
                    _rule.MergeOverrideDAT ||
                    _rule.SingleArchive ||
                    _rule.MultiDATDirOverride)
                    DatUpdate.CheckAllDats(DB.DirTree.Child(0), _rule.DirKey);
            }

            Settings.rvSettings.ResetDatRules();
            Settings.WriteConfig(Settings.rvSettings);
            _rule = Settings.rvSettings.DatRules[0];
            UpdateGrid();
        }

    }
}