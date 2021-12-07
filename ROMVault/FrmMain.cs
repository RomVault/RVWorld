/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2010                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.FindFix;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RVIO;


/*
 * DatVault   to enable auto DatDownload
 * RomShare   to enable rom share code
 * ZipFile    to enable zip as file code
 * 
 */

namespace ROMVault
{
    public partial class FrmMain : Form
    {
        private static readonly Color CBlue = Color.FromArgb(214, 214, 255);
        private static readonly Color CGreyBlue = Color.FromArgb(214, 224, 255);
        private static readonly Color CRed = Color.FromArgb(255, 214, 214);
        private static readonly Color CBrightRed = Color.FromArgb(255, 0, 0);
        private static readonly Color CGreen = Color.FromArgb(214, 255, 214);
        private static readonly Color CGrey = Color.FromArgb(214, 214, 214);
        private static readonly Color CCyan = Color.FromArgb(214, 255, 255);
        private static readonly Color CCyanGrey = Color.FromArgb(214, 225, 225);
        private static readonly Color CMagenta = Color.FromArgb(255, 214, 255);
        private static readonly Color CBrown = Color.FromArgb(140, 80, 80);
        private static readonly Color CPurple = Color.FromArgb(214, 140, 214);
        private static readonly Color CYellow = Color.FromArgb(255, 255, 214);
        private static readonly Color COrange = Color.FromArgb(255, 214, 140);
        private static readonly Color CWhite = Color.FromArgb(255, 255, 255);
        private static int[] _gameGridColumnXPositions;

        private readonly Color[] _displayColor;
        private readonly Color[] _fontColor;

        private readonly ContextMenuStrip _mnuContext;
        private readonly ContextMenuStrip _mnuContextToSort;

        private readonly ToolStripMenuItem _mnuOpen;

        private readonly ToolStripMenuItem _mnuToSortOpen;
        private readonly ToolStripMenuItem _mnuToSortDelete;
        private readonly ToolStripMenuItem _mnuToSortSetPrimary;
        private readonly ToolStripMenuItem _mnuToSortSetCache;

        private RvFile _clickedTree;

        private bool _updatingGameGrid;


        private FrmKey _fk;

        private float _scaleFactorX = 1;
        private float _scaleFactorY = 1;

        #region MainUISetup

        public FrmMain()
        {
            InitializeComponent();
            AddGameMetaData();
            Text = $@"RomVault ({Program.StrVersion} WIP 6)";

            if (Settings.rvSettings.zstd)
            {
                Text += " -using ZSTD";
            }

            Text += $@" {Application.StartupPath}";

            Type dgvType = GameGrid.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(GameGrid, true, null);

            dgvType = RomGrid.GetType();
            pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(RomGrid, true, null);


            _displayColor = new Color[(int)RepStatus.EndValue];
            _fontColor = new Color[(int)RepStatus.EndValue];

            // RepStatus.UnSet

            _displayColor[(int)RepStatus.UnScanned] = CBlue;

            _displayColor[(int)RepStatus.DirCorrect] = CGreen;
            _displayColor[(int)RepStatus.DirMissing] = CRed;
            _displayColor[(int)RepStatus.DirCorrupt] = CBrightRed; //BrightRed

            _displayColor[(int)RepStatus.Missing] = CRed;
            _displayColor[(int)RepStatus.Correct] = CGreen;
            _displayColor[(int)RepStatus.NotCollected] = CGrey;
            _displayColor[(int)RepStatus.UnNeeded] = CCyanGrey;
            _displayColor[(int)RepStatus.Unknown] = CCyan;
            _displayColor[(int)RepStatus.InToSort] = CMagenta;

            _displayColor[(int)RepStatus.Corrupt] = CBrightRed; //BrightRed
            _displayColor[(int)RepStatus.Ignore] = CGreyBlue;

            _displayColor[(int)RepStatus.CanBeFixed] = CYellow;
            _displayColor[(int)RepStatus.MoveToSort] = CPurple;
            _displayColor[(int)RepStatus.Delete] = CBrown;
            _displayColor[(int)RepStatus.NeededForFix] = COrange;
            _displayColor[(int)RepStatus.Rename] = COrange;

            _displayColor[(int)RepStatus.CorruptCanBeFixed] = CYellow;
            _displayColor[(int)RepStatus.MoveToCorrupt] = CPurple; //Missing


            _displayColor[(int)RepStatus.Deleted] = CWhite;

            for (int i = 0; i < (int)RepStatus.EndValue; i++)
            {
                _fontColor[i] = Contrasty(_displayColor[i]);
            }

            _gameGridColumnXPositions = new int[(int)RepStatus.EndValue];

            ctrRvTree.Setup(ref DB.DirRoot);

            splitContainer3_Panel1_Resize(new object(), new EventArgs());
            splitContainer4_Panel1_Resize(new object(), new EventArgs());


            _mnuContext = new ContextMenuStrip();

            ToolStripMenuItem mnuScan1 = new ToolStripMenuItem
            {
                Text = @"Scan Quick (Headers Only)",
                Tag = EScanLevel.Level1
            };
            ToolStripMenuItem mnuScan2 = new ToolStripMenuItem
            {
                Text = @"Scan",
                Tag = EScanLevel.Level2
            };
            ToolStripMenuItem mnuScan3 = new ToolStripMenuItem
            {
                Text = @"Scan Full (Complete Re-Scan)",
                Tag = EScanLevel.Level3
            };

            ToolStripMenuItem mnuFile = new ToolStripMenuItem
            {
                Text = @"Set Dir Settings",
                Tag = null
            };

            _mnuOpen = new ToolStripMenuItem
            {
                Text = @"Open Directory",
                Tag = null
            };

            ToolStripMenuItem mnuFixDat = new ToolStripMenuItem
            {
                Text = @"Create Fix DATs",
                Tag = null
            };

            ToolStripMenuItem mnuMakeDat = new ToolStripMenuItem
            {
                Text = @"Make Dat with CHDs as disk",
                Tag = null
            };

            ToolStripMenuItem mnuMakeDat2 = new ToolStripMenuItem
            {
                Text = @"Make Dat with CHDs as rom",
                Tag = null
            };

            _mnuContext.Items.Add(mnuScan2);
            _mnuContext.Items.Add(mnuScan1);
            _mnuContext.Items.Add(mnuScan3);
            _mnuContext.Items.Add(mnuFile);
            _mnuContext.Items.Add("-");
            _mnuContext.Items.Add(_mnuOpen);
            _mnuContext.Items.Add(mnuFixDat);
            _mnuContext.Items.Add(mnuMakeDat);
            _mnuContext.Items.Add(mnuMakeDat2);

            mnuScan1.Click += MnuScan;
            mnuScan2.Click += MnuScan;
            mnuScan3.Click += MnuScan;
            mnuFile.Click += MnuFileClick;
            _mnuOpen.Click += MnuOpenClick;
            mnuFixDat.Click += MnuMakeFixDatClick;
            mnuMakeDat.Click += MnuMakeDatClick;
            mnuMakeDat2.Click += MnuMakeDat2Click;


            _mnuContextToSort = new ContextMenuStrip();

            ToolStripMenuItem mnuToSortScan1 = new ToolStripMenuItem
            {
                Text = @"Scan Quick (Headers Only)",
                Tag = EScanLevel.Level1
            };
            ToolStripMenuItem mnuToSortScan2 = new ToolStripMenuItem
            {
                Text = @"Scan",
                Tag = EScanLevel.Level2
            };
            ToolStripMenuItem mnuToSortScan3 = new ToolStripMenuItem
            {
                Text = @"Scan Full (Complete Re-Scan)",
                Tag = EScanLevel.Level3
            };
            _mnuToSortOpen = new ToolStripMenuItem
            {
                Text = @"Open ToSort Directory",
                Tag = null
            };

            _mnuToSortDelete = new ToolStripMenuItem
            {
                Text = @"Remove",
                Tag = null
            };

            _mnuToSortSetPrimary = new ToolStripMenuItem
            {
                Text = @"Set To Primary ToSort",
                Tag = null
            };

            _mnuToSortSetCache = new ToolStripMenuItem
            {
                Text = @"Set To Cache ToSort",
                Tag = null
            };

            _mnuContextToSort.Items.Add(mnuToSortScan2);
            _mnuContextToSort.Items.Add(mnuToSortScan1);
            _mnuContextToSort.Items.Add(mnuToSortScan3);
            _mnuContextToSort.Items.Add(_mnuToSortOpen);
            _mnuContextToSort.Items.Add(_mnuToSortDelete);
            _mnuContextToSort.Items.Add(_mnuToSortSetPrimary);
            _mnuContextToSort.Items.Add(_mnuToSortSetCache);

            mnuToSortScan1.Click += MnuScan;
            mnuToSortScan2.Click += MnuScan;
            mnuToSortScan3.Click += MnuScan;
            _mnuToSortOpen.Click += MnuToSortOpen;
            _mnuToSortDelete.Click += MnuToSortDelete;
            _mnuToSortSetPrimary.Click += MnuToSortSetPrimary;
            _mnuToSortSetCache.Click += MnuToSortSetCache;


            chkBoxShowCorrect.Checked = Settings.rvSettings.chkBoxShowCorrect;
            chkBoxShowMissing.Checked = Settings.rvSettings.chkBoxShowMissing;
            chkBoxShowFixed.Checked = Settings.rvSettings.chkBoxShowFixed;
            chkBoxShowMerged.Checked = Settings.rvSettings.chkBoxShowMerged;

            TabArtworkInitialize();
        }


        // returns either white or black, depending of quick luminance of the Color " a "
        // called when the _displayColor is finished, in order to populate the _fontColor table.
        private static Color Contrasty(Color a)
        {
            return (a.R << 1) + a.B + a.G + (a.G << 2) < 1024 ? Color.White : Color.Black;
        }

        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        private void splitContainer3_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitDatInfoTree.Panel1.Width == 0)
                return;

            gbDatInfo.Width = splitDatInfoTree.Panel1.Width - gbDatInfo.Left * 2;
        }

        private void splitContainer4_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitGameInfoLists.Panel1.Width == 0)
                return;

            int chkLeft = splitGameInfoLists.Panel1.Width - 150;
            if (chkLeft < 430)
                chkLeft = 430;

            chkBoxShowCorrect.Left = chkLeft;
            chkBoxShowMissing.Left = chkLeft;
            chkBoxShowFixed.Left = chkLeft;
            chkBoxShowMerged.Left = chkLeft;
            txtFilter.Left = chkLeft;
            btnClear.Left = chkLeft + txtFilter.Width + 2;
            picPayPal.Left = chkLeft;
            picPatreon.Left = chkLeft + picPayPal.Width;

            gbSetInfo.Width = chkLeft - gbSetInfo.Left - 10;
        }
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            splitToolBarMain.SplitterDistance = (int)(splitToolBarMain.SplitterDistance * factor.Width);
            splitDatInfoGameInfo.SplitterDistance = (int)(splitDatInfoGameInfo.SplitterDistance * factor.Width);
            splitDatInfoGameInfo.Panel1MinSize = (int)(splitDatInfoGameInfo.Panel1MinSize * factor.Width);

            splitDatInfoTree.SplitterDistance = (int)(splitDatInfoTree.SplitterDistance * factor.Height);
            splitGameInfoLists.SplitterDistance = (int)(splitGameInfoLists.SplitterDistance * factor.Height);

            _scaleFactorX *= factor.Width;
            _scaleFactorY *= factor.Height;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_working)
            {
                e.Cancel = true;
            }
        }
        #endregion


        #region Tree
        private void DirTreeRvChecked(object sender, MouseEventArgs e)
        {
            RepairStatus.ReportStatusReset(DB.DirRoot);
            DatSetSelected(ctrRvTree.Selected);
        }

        private void DirTreeRvSelected(object sender, MouseEventArgs e)
        {
            RvFile cf = (RvFile)sender;

            if (e.Button != MouseButtons.Right)
            {
                if (cf != gameGridSource)
                {
                    DatSetSelected(cf);
                }
                return;
            }

            if (cf != ctrRvTree.Selected)
            {
                DatSetSelected(cf);
            }

            _clickedTree = (RvFile)sender;

            if (_working)
                return;

            Point controLocation = ControlLoc(ctrRvTree);

            if (cf.IsInToSort)
            {
                bool selected = (_clickedTree.Tree.Checked != RvTreeRow.TreeSelect.Locked);

                _mnuToSortOpen.Enabled = Directory.Exists(_clickedTree.FullName);
                _mnuToSortDelete.Enabled = !(_clickedTree.FileStatusIs(FileStatus.PrimaryToSort) |
                                             _clickedTree.FileStatusIs(FileStatus.CacheToSort));
                _mnuToSortSetCache.Enabled = selected;
                _mnuToSortSetPrimary.Enabled = selected;

                _mnuContextToSort.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));
            }
            else
            {
                _mnuOpen.Enabled = Directory.Exists(_clickedTree.FullName);
                //_mnuFile.Enabled = _clickedTree.Dat == null;
                _mnuContext.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));
            }
        }

        private Point ControlLoc(Control c)
        {
            Point ret = new Point(c.Left, c.Top);

            if (c.Parent == this)
                return ret;

            Point pNext = ControlLoc(c.Parent);
            ret.X += pNext.X;
            ret.Y += pNext.Y;

            return ret;
        }


        #endregion


        #region popupMenus

        private void MnuScan(object sender, EventArgs e)
        {
            ScanRoms((EScanLevel)((ToolStripMenuItem)sender).Tag, _clickedTree);
        }
        private void MnuFileClick(object sender, EventArgs e)
        {
            using (FrmSetDirSettings sd = new FrmSetDirSettings())
            {
                string tDir = _clickedTree.TreeFullName;
                sd.SetLocation(tDir);
                sd.SetDisplayType(true);
                sd.ShowDialog(this);

                if (sd.ChangesMade)
                    UpdateDats();
            }
        }
        private void MnuOpenClick(object sender, EventArgs e)
        {
            string tDir = _clickedTree.FullName;
            if (Directory.Exists(tDir))
                Process.Start(tDir);
        }
        private void MnuMakeFixDatClick(object sender, EventArgs e)
        {
            Report.MakeFixFiles(_clickedTree);
        }

        private void MnuMakeDatClick(object sender, EventArgs e)
        {
            SaveFileDialog browse = new SaveFileDialog
            {
                Filter = "DAT file|*.dat",
                Title = "Save an Dat File",
                FileName = _clickedTree.Name
            };

            if (browse.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            if (browse.FileName == "")
            {
                return;
            }


            DatMaker.MakeDatFromDir(_clickedTree, browse.FileName);
        }

        private void MnuMakeDat2Click(object sender, EventArgs e)
        {
            SaveFileDialog browse = new SaveFileDialog
            {
                Filter = "DAT file|*.dat",
                Title = "Save an Dat File",
                FileName = _clickedTree.Name
            };

            if (browse.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            if (browse.FileName == "")
            {
                return;
            }

            DatMaker.MakeDatFromDir(_clickedTree, browse.FileName, false);
        }


        private void MnuToSortOpen(object sender, EventArgs e)
        {
            string tDir = _clickedTree.FullName;
            if (Directory.Exists(tDir))
                Process.Start(tDir);
        }

        private void MnuToSortDelete(object sender, EventArgs e)
        {
            for (int i = 0; i < DB.DirRoot.ChildCount; i++)
            {
                if (DB.DirRoot.Child(i) == _clickedTree)
                {
                    DB.DirRoot.ChildRemove(i);
                    RepairStatus.ReportStatusReset(DB.DirRoot);

                    ctrRvTree.Setup(ref DB.DirRoot);
                    DatSetSelected(DB.DirRoot.Child(i - 1));
                    DB.Write();
                    ctrRvTree.Refresh();
                    return;
                }
            }
        }

        private void MnuToSortSetPrimary(object sender, EventArgs e)
        {
            if (_clickedTree.Tree.Checked == RvTreeRow.TreeSelect.Locked)
            {
                MessageBox.Show("Directory Must be ticked.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RvFile t = DB.RvFileToSort();
            bool wasCache = t.FileStatusIs(FileStatus.CacheToSort);
            t.FileStatusClear(FileStatus.PrimaryToSort | FileStatus.CacheToSort);

            _clickedTree.FileStatusSet(FileStatus.PrimaryToSort);
            if (wasCache)
                _clickedTree.FileStatusSet(FileStatus.CacheToSort);

            DB.Write();
            ctrRvTree.Refresh();
        }

        private void MnuToSortSetCache(object sender, EventArgs e)
        {
            if (_clickedTree.Tree.Checked == RvTreeRow.TreeSelect.Locked)
            {
                MessageBox.Show("Directory Must be ticked.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RvFile t = DB.RvFileCache();
            t.FileStatusClear(FileStatus.CacheToSort);

            _clickedTree.FileStatusSet(FileStatus.CacheToSort);

            DB.Write();
            ctrRvTree.Refresh();
        }

        #endregion


        #region TopMenu

        private void updateNewDATsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateDats();
        }
        private void updateAllDATsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DatUpdate.CheckAllDats(DB.DirRoot.Child(0), @"DatRoot\");
            UpdateDats();
        }
              

        private void AddToSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result != DialogResult.OK) return;

            RvFile ts = new RvFile(FileType.Dir)
            {
                Name = folderBrowserDialog1.SelectedPath,
                DatStatus = DatStatus.InDatCollect,
                Tree = new RvTreeRow()
            };
            ts.Tree.SetChecked(RvTreeRow.TreeSelect.Locked, false);

            DB.DirRoot.ChildAdd(ts, DB.DirRoot.ChildCount);

            RepairStatus.ReportStatusReset(DB.DirRoot);
            ctrRvTree.Setup(ref DB.DirRoot);
            DatSetSelected(ts);

            DB.Write();
        }



        private void TsmScanLevel1Click(object sender, EventArgs e)
        {
            ScanRoms(EScanLevel.Level1);
        }
        private void TsmScanLevel2Click(object sender, EventArgs e)
        {
            ScanRoms(EScanLevel.Level2);
        }
        private void TsmScanLevel3Click(object sender, EventArgs e)
        {
            ScanRoms(EScanLevel.Level3);
        }





        private void TsmFindFixesClick(object sender, EventArgs e)
        {
            FindFixs();
        }

        private void FixFilesToolStripMenuItemClick(object sender, EventArgs e)
        {
            FixFiles();
        }





        private void RomVaultSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FrmSettings fcfg = new FrmSettings())
            {
                fcfg.ShowDialog(this);
            }
        }
        private void DirectorySettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FrmSetDirSettings sd = new FrmSetDirSettings())
            {
                string tDir = "RomVault";
                sd.SetLocation(tDir);
                sd.SetDisplayType(false);
                sd.ShowDialog(this);

                if (sd.ChangesMade)
                    UpdateDats();
            }
        }

        private void RegistrationSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FrmRegistration fReg = new FrmRegistration())
            {
                fReg.ShowDialog();
            }
        }







        private void fixDatReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Report.MakeFixFiles();
        }

        private void fullReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Report.GenerateReport();
        }

        private void fixReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Report.GenerateFixReport();
        }




        private void colorKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_fk == null || _fk.IsDisposed)
            {
                _fk = new FrmKey();
            }

            _fk.Show();
        }
        private void AboutRomVaultToolStripMenuItemClick(object sender, EventArgs e)
        {
            FrmHelpAbout fha = new FrmHelpAbout();
            fha.ShowDialog(this);
            fha.Dispose();
        }




        #endregion


        #region sideButtons
        private void BtnUpdateDatsMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && Control.ModifierKeys == Keys.Shift)
            {
                DatUpdate.CheckAllDats(DB.DirRoot.Child(0), @"DatRoot\");
            }
           
            UpdateDats();
        }
        private void BtnScanRomsClick(object sender, EventArgs e)
        {
            ScanRoms(EScanLevel.Level2);
        }
        private void BtnFindFixesClick(object sender, EventArgs e)
        {
            FindFixs();
        }
        private void BtnFixFilesMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _multiFixing = true;
                ScanRoms(EScanLevel.Level2);
                return;
            }

            FixFiles();
        }
        private void BtnReportMouseUp(object sender, MouseEventArgs e)
        {
            Report.MakeFixFiles(null, e.Button == MouseButtons.Left);
        }
        #endregion


        #region TopRight

        private void ChkBoxShowCorrectCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowCorrect != this.chkBoxShowCorrect.Checked)
            {
                Settings.rvSettings.chkBoxShowCorrect = this.chkBoxShowCorrect.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }

        private void ChkBoxShowMissingCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMissing != this.chkBoxShowMissing.Checked)
            {
                Settings.rvSettings.chkBoxShowMissing = this.chkBoxShowMissing.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }

        private void ChkBoxShowFixedCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowFixed != this.chkBoxShowFixed.Checked)
            {
                Settings.rvSettings.chkBoxShowFixed = this.chkBoxShowFixed.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }

        private void ChkBoxShowMergedCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMerged != this.chkBoxShowMerged.Checked)
            {
                Settings.rvSettings.chkBoxShowMerged = this.chkBoxShowMerged.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }



        private void BtnClear_Click(object sender, EventArgs e)
        {
            txtFilter.Text = "";
        }

        private void TxtFilter_TextChanged(object sender, EventArgs e)
        {
            if (gameGridSource != null)
                UpdateGameGrid(gameGridSource);
            txtFilter.Focus();
        }


        private void picPayPal_Click(object sender, EventArgs e)
        {
            Process.Start("http://paypal.me/romvault");
        }

        private void picPatreon_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.patreon.com/romvault");
        }

        #endregion


        #region coreFunctions

      
        private void UpdateDats()
        {
            // incase the selected tree item(DAT) is removed from the tree in the updated we need to build a parent list and traverse up it until we find a parent item still in the tree.

            // build a list of the selected item in the Tree view and all the items up the parent list from there back to the root.
            RvFile selected = ctrRvTree.Selected;
            List<RvFile> parents = new List<RvFile>();
            while (selected != null)
            {
                parents.Add(selected);
                selected = selected.Parent;
            }

            // update the dats
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat, null);
            progress.HideCancelButton();
            progress.ShowDialog(this);
            progress.Dispose();

            // rebuild the tree
            ctrRvTree.Setup(ref DB.DirRoot);

            // if the rvFile.Parent is null it have been removed from the tree so remove it from the list.
            // set up until we find a rvFile with a parent.
            while (parents.Count > 1 && parents[0].Parent == null)
                parents.RemoveAt(0);

            // did we find a parent
            if (parents.Count > 0)
                selected = parents[0];
            else
                selected = null;

            // update the selected tree item, and the game grid view.
            ctrRvTree.SetSelected(selected);
            DatSetSelected(selected);
        }

        private void setPos(Form childForm)
        {
            childForm.Owner = this;
            childForm.StartPosition = FormStartPosition.Manual;
            childForm.Location = new Point(
              Location.X + (Width - childForm.Width) / 2,
              Location.Y + (Height - childForm.Height) / 2
            );
        }

        private bool _multiFixing = false;

        private FrmProgressWindow frmScanRoms;
        private void ScanRoms(EScanLevel sd, RvFile StartAt = null)
        {
            if (frmScanRoms != null)
            {
                frmScanRoms.FormClosed -= ScanRomsClosed;
                frmScanRoms.Dispose();
            }
            FileScanning.StartAt = StartAt;
            FileScanning.EScanLevel = sd;
            frmScanRoms = new FrmProgressWindow(this, "Scanning Dirs", FileScanning.ScanFiles, Finish);
            Start();
            setPos(frmScanRoms);
            frmScanRoms.FormClosed += ScanRomsClosed;
            frmScanRoms.Show();
        }
        private void ScanRomsClosed(object sender, FormClosedEventArgs e)
        {
            if (!_multiFixing)
                return;

            if (frmScanRoms.Cancelled)
            {
                _multiFixing = false;
                return;
            }
            FindFixs();
        }

        private FrmProgressWindow frmFindFixs;
        private void FindFixs()
        {
            if (frmFindFixs != null)
            {
                frmFindFixs.FormClosed -= FindFixsClosed;
                frmFindFixs.Dispose();
            }
            frmFindFixs = new FrmProgressWindow(this, "Finding Fixes", FindFixes.ScanFiles, Finish);
            Start();
            setPos(frmFindFixs);
            frmFindFixs.FormClosed += FindFixsClosed;
            frmFindFixs.Show();
        }
        private void FindFixsClosed(object sender, FormClosedEventArgs e)
        {
            if (!_multiFixing)
                return;

            if (frmFindFixs.Cancelled)
            {
                _multiFixing = false;
                return;
            }
            FixFiles();
        }

        FrmProgressWindowFix frmFixFiles;
        private void FixFiles()
        {
            if (frmFixFiles != null)
                frmFixFiles.Dispose();

            frmFixFiles = new FrmProgressWindowFix(this, Finish);
            Start();
            setPos(frmFixFiles);
            frmFixFiles.Show();

            _multiFixing = false;
        }

        private bool _working = false;
        private void Start()
        {
            _working = true;
            timer1.Enabled = true;
            ctrRvTree.Working = true;
            menuStrip1.Enabled = false;
            btnUpdateDats.Enabled = false;
            btnScanRoms.Enabled = false;
            btnFindFixes.Enabled = false;
            btnFixFiles.Enabled = false;
            btnReport.Enabled = false;

            btnUpdateDats.BackgroundImage = rvImages1.btnUpdateDats_Disabled;
            btnScanRoms.BackgroundImage = rvImages1.btnScanRoms_Disabled;
            btnFindFixes.BackgroundImage = rvImages1.btnFindFixes_Disabled;
            btnFixFiles.BackgroundImage = rvImages1.btnFixFiles_Disabled;
            btnReport.BackgroundImage = rvImages1.btnReport_Disabled;
        }
        private void Finish()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMain));


            _working = false;
            ctrRvTree.Working = false;
            menuStrip1.Enabled = true;

            btnUpdateDats.BackgroundImage = rvImages1.btnUpdateDats_Enabled;
            btnScanRoms.BackgroundImage = rvImages1.btnScanRoms_Enabled;
            btnFindFixes.BackgroundImage = rvImages1.btnFindFixes_Enabled;
            btnFixFiles.BackgroundImage = rvImages1.btnFixFiles_Enabled;
            btnReport.BackgroundImage = rvImages1.btnReport_Enabled;

            btnUpdateDats.Enabled = true;
            btnScanRoms.Enabled = true;
            btnFindFixes.Enabled = true;
            btnFixFiles.Enabled = true;
            btnReport.Enabled = true;

            timer1.Enabled = false;
            DatSetSelected(ctrRvTree.Selected);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            ctrRvTree.Refresh();
            UpdateGameGrid();
            if (ctrRvTree.Selected != null)
                UpdateDatMetaData(ctrRvTree.Selected);
            GameGrid.Refresh();
        }


        #endregion


        #region DatDisplay

        private void DatSetSelected(RvFile cf)
        {
            ctrRvTree.Refresh();

            ClearGameGrid();

            if (cf == null)
            {
                return;
            }

            UpdateDatMetaData(cf);
            UpdateGameGrid(cf);
        }
        private void UpdateDatMetaData(RvFile tDir)
        {
            lblDITName.Text = tDir.Name;
            if (tDir.Dat != null)
            {
                RvDat tDat = tDir.Dat;
                lblDITDescription.Text = tDat.GetData(RvDat.DatData.Description);
                lblDITCategory.Text = tDat.GetData(RvDat.DatData.Category);
                lblDITVersion.Text = tDat.GetData(RvDat.DatData.Version);
                lblDITAuthor.Text = tDat.GetData(RvDat.DatData.Author);
                lblDITDate.Text = tDat.GetData(RvDat.DatData.Date);
                string header = tDat.GetData(RvDat.DatData.Header);
                if (!string.IsNullOrWhiteSpace(header))
                    lblDITName.Text += " (" + header + ")";
            }
            else if (tDir.DirDatCount == 1)
            {
                RvDat tDat = tDir.DirDat(0);
                lblDITDescription.Text = tDat.GetData(RvDat.DatData.Description);
                lblDITCategory.Text = tDat.GetData(RvDat.DatData.Category);
                lblDITVersion.Text = tDat.GetData(RvDat.DatData.Version);
                lblDITAuthor.Text = tDat.GetData(RvDat.DatData.Author);
                lblDITDate.Text = tDat.GetData(RvDat.DatData.Date);
                string header = tDat.GetData(RvDat.DatData.Header);
                if (!string.IsNullOrWhiteSpace(header))
                    lblDITName.Text += " (" + header + ")";
            }
            else
            {
                lblDITDescription.Text = "";
                lblDITCategory.Text = "";
                lblDITVersion.Text = "";
                lblDITAuthor.Text = "";
                lblDITDate.Text = "";
            }

            lblDITPath.Text = tDir.FullName;

            lblDITRomsGot.Text = tDir.DirStatus.CountCorrect().ToString(CultureInfo.InvariantCulture);
            lblDITRomsMissing.Text = tDir.DirStatus.CountMissing().ToString(CultureInfo.InvariantCulture);
            lblDITRomsFixable.Text = tDir.DirStatus.CountFixesNeeded().ToString(CultureInfo.InvariantCulture);
            lblDITRomsUnknown.Text = (tDir.DirStatus.CountUnknown() + tDir.DirStatus.CountInToSort()).ToString(CultureInfo.InvariantCulture);
        }


        private void gbDatInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 89;
            int rightPos = (int)(gbDatInfo.Width / _scaleFactorX) - 15;


            int width = rightPos - leftPos;
            int widthB1 = (int)((double)width * 120 / 340);
            int leftB2 = rightPos - widthB1;


            int backD = 97;

            width = (int)(width * _scaleFactorX);
            widthB1 = (int)(widthB1 * _scaleFactorX);
            leftB2 = (int)(leftB2 * _scaleFactorX);
            backD = (int)(backD * _scaleFactorX);


            lblDITName.Width = width;
            lblDITDescription.Width = width;

            lblDITCategory.Width = widthB1;
            lblDITAuthor.Width = widthB1;

            lblDIVersion.Left = leftB2 - backD;
            lblDIDate.Left = leftB2 - backD;

            lblDITVersion.Left = leftB2;
            lblDITVersion.Width = widthB1;
            lblDITDate.Left = leftB2;
            lblDITDate.Width = widthB1;

            lblDITPath.Width = width;

            lblDITRomsGot.Width = widthB1;
            lblDITRomsMissing.Width = widthB1;

            lblDIRomsFixable.Left = leftB2 - backD;
            lblDIRomsUnknown.Left = leftB2 - backD;

            lblDITRomsFixable.Left = leftB2;
            lblDITRomsFixable.Width = widthB1;
            lblDITRomsUnknown.Left = leftB2;
            lblDITRomsUnknown.Width = widthB1;
        }


        #endregion



    }
}