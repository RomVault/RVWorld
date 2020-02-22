/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2010                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using RVCore;
using RVCore.FindFix;
using RVCore.ReadDat;
using RVCore.RvDB;
using RVCore.Scanner;
using RVIO;

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

        private readonly ContextMenu _mnuContext;
        private readonly ContextMenu _mnuContextToSort;

        private readonly MenuItem _mnuFile;
        private readonly MenuItem _mnuOpen;

        private readonly MenuItem _mnuToSortScan;
        private readonly MenuItem _mnuToSortOpen;
        private readonly MenuItem _mnuToSortDelete;
        private readonly MenuItem _mnuToSortSetPrimary;
        private readonly MenuItem _mnuToSortSetCache;

        private RvFile _clickedTree;

        private bool _updatingGameGrid;


        private FrmKey _fk;

        private float _scaleFactorX = 1;
        private float _scaleFactorY = 1;


        public FrmMain()
        {
            InitializeComponent();
            AddGameMetaData();
            Text = $@"RomVault ({Program.StrVersion})  {Application.StartupPath}";

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

            DirTree.Setup(ref DB.DirTree);

            splitContainer3_Panel1_Resize(new object(), new EventArgs());
            splitContainer4_Panel1_Resize(new object(), new EventArgs());

            _mnuContext = new ContextMenu();

            MenuItem mnuScan = new MenuItem
            {
                Text = @"Scan",
                Tag = null
            };

            _mnuFile = new MenuItem
            {
                Text = @"Set Dir Settings",
                Tag = null
            };

            _mnuOpen = new MenuItem
            {
                Text = @"Open",
                Tag = null
            };

            MenuItem mnuFixDat = new MenuItem
            {
                Text = @"Create Fix DATs",
                Tag = null
            };

            MenuItem mnuMakeDat = new MenuItem
            {
                Text = @"Make Dat with CHDs as disk",
                Tag = null
            };

            MenuItem mnuMakeDat2 = new MenuItem
            {
                Text = @"Make Dat with CHDs as rom",
                Tag = null
            };

            _mnuContext.MenuItems.Add(mnuScan);
            _mnuContext.MenuItems.Add(_mnuOpen);
            _mnuContext.MenuItems.Add(_mnuFile);
            _mnuContext.MenuItems.Add(mnuFixDat);
            _mnuContext.MenuItems.Add(mnuMakeDat);
            _mnuContext.MenuItems.Add(mnuMakeDat2);

            mnuScan.Click += MnuToSortScan;
            _mnuOpen.Click += MnuOpenClick;
            _mnuFile.Click += MnuFileClick;
            mnuFixDat.Click += MnuMakeFixDatClick;
            mnuMakeDat.Click += MnuMakeDatClick;
            mnuMakeDat2.Click += MnuMakeDat2Click;


            _mnuContextToSort = new ContextMenu();

            _mnuToSortScan = new MenuItem
            {
                Text = @"Scan",
                Tag = null
            };

            _mnuToSortOpen = new MenuItem
            {
                Text = @"Open",
                Tag = null
            };

            _mnuToSortDelete = new MenuItem
            {
                Text = @"Remove",
                Tag = null
            };

            _mnuToSortSetPrimary = new MenuItem
            {
                Text = @"Set To Primary ToSort",
                Tag = null
            };

            _mnuToSortSetCache = new MenuItem
            {
                Text = @"Set To Cache ToSort",
                Tag = null
            };

            _mnuContextToSort.MenuItems.Add(_mnuToSortScan);
            _mnuContextToSort.MenuItems.Add(_mnuToSortOpen);
            _mnuContextToSort.MenuItems.Add(_mnuToSortDelete);
            _mnuContextToSort.MenuItems.Add(_mnuToSortSetPrimary);
            _mnuContextToSort.MenuItems.Add(_mnuToSortSetCache);

            _mnuToSortScan.Click += MnuToSortScan;
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

        private void AddTextBox(int line, string name, int x, int x1, out Label lBox, out TextBox tBox)
        {
            int y = 14 + line * 16;

            lBox = new Label
            {
                Location = SPoint(x, y + 1),
                Size = SSize(x1 - x - 2, 13),
                Text = name + @" :",
                TextAlign = ContentAlignment.TopRight
            };
            tBox = new TextBox
            {
                AutoSize = false,
                Location = SPoint(x1, y),
                Size = SSize(20, 17),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                TabStop = false
            };
            gbSetInfo.Controls.Add(lBox);
            gbSetInfo.Controls.Add(tBox);

        }


        private Point SPoint(int x, int y)
        {
            return new Point((int)(x * _scaleFactorX), (int)(y * _scaleFactorY));
        }

        private Size SSize(int x, int y)
        {
            return new Size((int)(x * _scaleFactorX), (int)(y * _scaleFactorY));
        }

        private void DirTreeRvChecked(object sender, MouseEventArgs e)
        {
            RepairStatus.ReportStatusReset(DB.DirTree);
            DatSetSelected(DirTree.Selected);
        }

        private void DirTreeRvSelected(object sender, MouseEventArgs e)
        {
            RvFile cf = (RvFile)sender;
            if (cf != DirTree.GetSelected())
            {
                DatSetSelected(cf);
            }

            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            _clickedTree = (RvFile)sender;

            Point controLocation = ControlLoc(DirTree);

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
                _mnuFile.Enabled = _clickedTree.Dat == null;
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


        private void AddToSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result != DialogResult.OK) return;

            RvFile ts = new RvFile(FileType.Dir)
            {
                Name = folderBrowserDialog1.SelectedPath,
                Tree = new RvTreeRow { Checked = RvTreeRow.TreeSelect.Locked },
                DatStatus = DatStatus.InDatCollect
            };

            DB.DirTree.ChildAdd(ts, DB.DirTree.ChildCount);

            RepairStatus.ReportStatusReset(DB.DirTree);
            DirTree.Setup(ref DB.DirTree);
            DatSetSelected(ts);

            DB.Write();
        }

        private void MnuToSortScan(object sender, EventArgs e)
        {
            ScanRoms(Settings.rvSettings.ScanLevel, _clickedTree);
        }

        private void MnuToSortOpen(object sender, EventArgs e)
        {
            string tDir = _clickedTree.FullName;
            if (Directory.Exists(tDir))
                Process.Start(tDir);
        }

        private void MnuToSortDelete(object sender, EventArgs e)
        {
            for (int i = 0; i < DB.DirTree.ChildCount; i++)
            {
                if (DB.DirTree.Child(i) == _clickedTree)
                {
                    DB.DirTree.ChildRemove(i);
                    RepairStatus.ReportStatusReset(DB.DirTree);

                    DirTree.Setup(ref DB.DirTree);
                    DatSetSelected(DB.DirTree.Child(i - 1));
                    DB.Write();
                    DirTree.Refresh();
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
            DirTree.Refresh();
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
            DirTree.Refresh();
        }




        private void ChkBoxShowCorrectCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowCorrect != this.chkBoxShowCorrect.Checked)
            {
                Settings.rvSettings.chkBoxShowCorrect = this.chkBoxShowCorrect.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(DirTree.Selected);
            }
        }

        private void ChkBoxShowMissingCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMissing != this.chkBoxShowMissing.Checked)
            {
                Settings.rvSettings.chkBoxShowMissing = this.chkBoxShowMissing.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(DirTree.Selected);
            }
        }

        private void ChkBoxShowFixedCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowFixed != this.chkBoxShowFixed.Checked)
            {
                Settings.rvSettings.chkBoxShowFixed = this.chkBoxShowFixed.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(DirTree.Selected);
            }
        }

        private void ChkBoxShowMergedCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMerged != this.chkBoxShowMerged.Checked)
            {
                Settings.rvSettings.chkBoxShowMerged = this.chkBoxShowMerged.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                UpdateSelectedGame();
            }
        }


        private void btnReport_MouseUp(object sender, MouseEventArgs e)
        {
            Report.MakeFixFiles(null, e.Button == MouseButtons.Left);
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


        private void AboutRomVaultToolStripMenuItemClick(object sender, EventArgs e)
        {
            FrmHelpAbout fha = new FrmHelpAbout();
            fha.ShowDialog(this);
            fha.Dispose();
        }


        #region "Main Buttons"

        private void TsmUpdateDaTsClick(object sender, EventArgs e)
        {
            UpdateDats();
        }

        private void BtnUpdateDatsClick(object sender, EventArgs e)
        {
            UpdateDats();
        }

        private void UpdateDats()
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat);
            progress.ShowDialog(this);
            progress.Dispose();

            DirTree.Setup(ref DB.DirTree);
            DatSetSelected(DirTree.Selected);
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

        private void BtnScanRomsClick(object sender, EventArgs e)
        {
            ScanRoms(Settings.rvSettings.ScanLevel);
        }

        private void ScanRoms(EScanLevel sd, RvFile StartAt = null)
        {
            FileScanning.StartAt = StartAt;
            FileScanning.EScanLevel = sd;
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dirs", FileScanning.ScanFiles);
            progress.ShowDialog(this);
            progress.Dispose();

            DatSetSelected(DirTree.Selected);
        }


        private void TsmFindFixesClick(object sender, EventArgs e)
        {
            FindFix();
        }

        private void BtnFindFixesClick(object sender, EventArgs e)
        {
            FindFix();
        }

        private void FindFix()
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Finding Fixes", FindFixes.ScanFiles);
            progress.ShowDialog(this);
            progress.Dispose();

            DatSetSelected(DirTree.Selected);
        }

        private void btnFixFiles_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ScanRoms(Settings.rvSettings.ScanLevel);
                FindFix();
            }

            FixFiles();
        }

        private void FixFilesToolStripMenuItemClick(object sender, EventArgs e)
        {
            FixFiles();
        }


        private void FixFiles()
        {
            FrmProgressWindowFix progress = new FrmProgressWindowFix(this);
            progress.ShowDialog(this);
            progress.Dispose();

            DatSetSelected(DirTree.Selected);
        }

        #endregion

        #region "DAT display code"

        private void DatSetSelected(RvFile cf)
        {
            DirTree.Refresh();

            if (Settings.IsMono)
            {
                if (GameGrid.RowCount > 0)
                {
                    GameGrid.CurrentCell = GameGrid[0, 0];
                }

                if (RomGrid.RowCount > 0)
                {
                    RomGrid.CurrentCell = RomGrid[0, 0];
                }
            }

            GameGrid.Rows.Clear();
            RomGrid.Rows.Clear();

            // clear sorting
            if (gameSortIndex>=0)
                GameGrid.Columns[gameSortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
            gameSortIndex = 0;
            gameSortDir = SortOrder.Descending;

            if (cf == null)
            {
                return;
            }

            UpdateDatMetaData(cf);
            UpdateGameGrid(cf);
        }



        private void splitContainer3_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitDatInfoTree.Panel1.Width == 0)
            {
                return;
            }

            gbDatInfo.Width = splitDatInfoTree.Panel1.Width - gbDatInfo.Left * 2;
        }


        private void splitContainer4_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitGameInfoLists.Panel1.Width == 0)
            {
                return;
            }

            int chkLeft = splitGameInfoLists.Panel1.Width - 150;
            if (chkLeft < 430)
            {
                chkLeft = 430;
            }

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

        #endregion


        private void picPayPal_Click(object sender, EventArgs e)
        {
            Process.Start("http://paypal.me/romvault");
        }

        private void picPatreon_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.patreon.com/romvault");
        }

        /*
        private void jsonDataDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DB.WriteJson();
        }
        */


        private void colorKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_fk == null || _fk.IsDisposed)
            {
                _fk = new FrmKey();
            }

            _fk.Show();
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

        private void RomVaultSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FrmSettings fcfg = new FrmSettings())
            {
                fcfg.ShowDialog(this);
            }
        }
        private void RegistrationSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FrmRegistration fReg = new FrmRegistration())
            {
                fReg.ShowDialog();
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

    }
}