using System;
using System.Drawing;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVIO;

namespace ROMVault
{
    public partial class FrmDirectoryMappings : Form
    {
        private Color _cMagenta = Color.FromArgb(255, 214, 255);
        private Color _cGreen = Color.FromArgb(214, 255, 214);
        private Color _cYellow = Color.FromArgb(255, 255, 214);
        private Color _cRed = Color.FromArgb(255, 214, 214);

        private DirMapping _rule;
        private readonly ToolTip tooltip;

        public FrmDirectoryMappings()
        {
            InitializeComponent();
            tooltip = new ToolTip
            {
                InitialDelay = 1000,
                ReshowDelay = 500
            };
            tooltip.AutoPopDelay = 32767;

            tooltip.SetToolTip(btnSetROMLocation, "Select a new Directory mapping location for this path.");
            tooltip.SetToolTip(btnClearROMLocation, "Use this to clear the directory mapping.\nThis rule will still apply the archive options and checked options below to the selected directory.");
            if (Settings.rvSettings.Darkness)
            {
                Dark.dark.SetColors(this);
                _cMagenta = Color.FromArgb((int)(255 * 0.8), (int)(214 * 0.8), (int)(255 * 0.8));
                _cGreen = Color.FromArgb((int)(214 * 0.8), (int)(255 * 0.8), (int)(214 * 0.8));
                _cYellow = Color.FromArgb((int)(255 * 0.8), (int)(255 * 0.8), (int)(214 * 0.8));

                DGDirectoryMappingRules.DefaultCellStyle.ForeColor = Color.Black;
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
                if ((c is Control ct) && (ct.Top > 110))
                {
                    ct.Visible = !type;
                }
            }
            MinimumSize = new Size(709, type ? 150 : 300);
            Height = type ? 155 : 428;
            FormBorderStyle = type ? FormBorderStyle.FixedSingle : FormBorderStyle.Sizable;

        }

        private static DirMapping FindRule(string dLocation)
        {
            foreach (DirMapping t in Settings.rvSettings.DirMappings)
            {
                if (string.Compare(t.DirKey, dLocation, StringComparison.Ordinal) == 0)
                    return t;
            }

            return new DirMapping { DirKey = dLocation };
        }


        private void SetDisplay()
        {
            txtDATLocation.Text = _rule.DirKey;
            txtROMLocation.Text = _rule.DirPath;
        }


        private void UpdateGrid()
        {
            if (Settings.IsMono && DGDirectoryMappingRules.RowCount > 0)
            {
                DGDirectoryMappingRules.CurrentCell = DGDirectoryMappingRules[0, 0];
            }


            DGDirectoryMappingRules.Rows.Clear();
            foreach (DirMapping t in Settings.rvSettings.DirMappings)
            {
                DGDirectoryMappingRules.Rows.Add();
                int row = DGDirectoryMappingRules.Rows.Count - 1;
                DGDirectoryMappingRules.Rows[row].Tag = t;
                DGDirectoryMappingRules.Rows[row].Cells["CPath"].Value = t.DirKey;
                DGDirectoryMappingRules.Rows[row].Cells["CLocation"].Value = t.DirPath;

                if (t.DirPath == "ToSort")
                {
                    DGDirectoryMappingRules.Rows[row].Cells["CPath"].Style.BackColor = _cMagenta;
                    DGDirectoryMappingRules.Rows[row].Cells["CLocation"].Style.BackColor = _cMagenta;
                }
                else if (t == _rule)
                {
                    DGDirectoryMappingRules.Rows[row].Cells["CPath"].Style.BackColor = _cGreen;
                    DGDirectoryMappingRules.Rows[row].Cells["CLocation"].Style.BackColor = _cGreen;
                }
                else if (t.DirKey.Length > _rule.DirKey.Length)
                {
                    if (t.DirKey.Substring(0, _rule.DirKey.Length + 1) == _rule.DirKey + "\\")
                    {
                        DGDirectoryMappingRules.Rows[row].Cells["CPath"].Style.BackColor = _cYellow;
                        DGDirectoryMappingRules.Rows[row].Cells["CLocation"].Style.BackColor = _cYellow;
                    }
                }

                if (!Directory.Exists(t.DirPath))
                {
                    DGDirectoryMappingRules.Rows[row].Cells["CLocation"].Style.BackColor = _cRed;
                }
            }

            for (int j = 0; j < DGDirectoryMappingRules.Rows.Count; j++)
            {
                DGDirectoryMappingRules.Rows[j].Selected = false;
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
            FolderBrowser browse = new FolderBrowser
            {
                ShowNewFolderButton = true,
                Description = "Please select a folder for This Rom Set",
                //RootFolder = Environment.SpecialFolder.MyComputer,
                SelectedPath = txtROMLocation.Text
            };
            if (browse.ShowDialog() == DialogResult.OK)
            {
                string relPath = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, browse.SelectedPath);
                txtROMLocation.Text = relPath;
            }
        }

        private void BtnApplyClick(object sender, EventArgs e)
        {
            string newDir = txtROMLocation.Text;
            if (string.IsNullOrWhiteSpace(newDir))
            {
                MessageBox.Show("You must select a directory.", "No Directory Selected", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (!Directory.Exists(newDir))
            {
                MessageBox.Show("The directory you have selected does not exist.", "Directory does not exist", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            _rule.DirPath = newDir;

            bool updatingRule = false;
            int i;
            for (i = 0; i < Settings.rvSettings.DirMappings.Count; i++)
            {
                if (Settings.rvSettings.DirMappings[i] == _rule)
                {
                    updatingRule = true;
                    break;
                }

                if (string.Compare(Settings.rvSettings.DirMappings[i].DirKey, _rule.DirKey, StringComparison.Ordinal) > 0)
                {
                    break;
                }
            }

            if (!updatingRule)
                Settings.rvSettings.DirMappings.Insert(i, _rule);

            UpdateGrid();
            Settings.WriteConfig(Settings.rvSettings);

            if (_displayType)
                Close();
        }
        private void BtnDeleteClick(object sender, EventArgs e)
        {
            string datLocation = _rule.DirKey;

            if (datLocation == "RomVault")
            {
                ReportError.Show("You cannot delete the base RomVault directory mapping", "RomVault Rom Location");
                return;
            }
            else
            {
                DatUpdate.CheckAllDats(DB.DirRoot.Child(0), datLocation);
                for (int i = 0; i < Settings.rvSettings.DirMappings.Count; i++)
                {
                    if (Settings.rvSettings.DirMappings[i].DirKey == datLocation)
                    {
                        Settings.rvSettings.DirMappings.RemoveAt(i);
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
            for (int j = 0; j < DGDirectoryMappingRules.SelectedRows.Count; j++)
            {
                string datLocation = DGDirectoryMappingRules.SelectedRows[j].Cells["CPATH"].Value.ToString();

                if (datLocation == "RomVault")
                {
                    ReportError.Show("You cannot delete the " + datLocation + " Directory Settings", "RomVault Rom Location");
                }
                else
                {
                    for (int i = 0; i < Settings.rvSettings.DirMappings.Count; i++)
                    {
                        if (Settings.rvSettings.DirMappings[i].DirKey == datLocation)
                        {
                            Settings.rvSettings.DirMappings.RemoveAt(i);
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
            if (DGDirectoryMappingRules.SelectedRows.Count <= 0)
            {
                return;
            }
            Text = "Edit Existing Directory / DATs Mapping";
            _rule = (DirMapping)DGDirectoryMappingRules.SelectedRows[0].Tag;
            UpdateGrid();
            SetDisplay();
        }

        private void FrmSetDirActivated(object sender, EventArgs e)
        {
            for (int j = 0; j < DGDirectoryMappingRules.Rows.Count; j++)
            {
                DGDirectoryMappingRules.Rows[j].Selected = false;
            }
        }

        private void BtnResetAllClick(object sender, EventArgs e)
        {
            Settings.rvSettings.ResetDirMappings();
            Settings.WriteConfig(Settings.rvSettings);
            _rule = Settings.rvSettings.DirMappings[0];
            UpdateGrid();
            SetDisplay();
        }
    }
}