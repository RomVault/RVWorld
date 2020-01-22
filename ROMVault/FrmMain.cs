/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2010                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using System.Drawing;
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

        private readonly MenuItem _mnuOpen;

        private readonly MenuItem _mnuToSortScan;
        private readonly MenuItem _mnuToSortOpen;
        private readonly MenuItem _mnuToSortDelete;
        private readonly MenuItem _mnuToSortSetPrimary;
        private readonly MenuItem _mnuToSortSetCache;

        private RvFile _clickedTree;

        private bool _updatingGameGrid;

        private int _gameGridSortColumnIndex;
        private SortOrder _gameGridSortOrder = SortOrder.Descending;

        private FrmKey _fk;

        private float _scaleFactorX = 1;
        private float _scaleFactorY = 1;

        private Label _labelGameName;
        private TextBox _textGameName;

        private Label _labelGameDescription;
        private TextBox _textGameDescription;

        private Label _labelGameManufacturer;
        private TextBox _textGameManufacturer;

        private Label _labelGameCloneOf;
        private TextBox _textGameCloneOf;

        private Label _labelGameRomOf;
        private TextBox _textGameRomOf;

        private Label _labelGameYear;
        private TextBox _textGameYear;

        private Label _labelGameTotalRoms;
        private TextBox _textGameTotalRoms;

        //Trurip Extra Data
        private Label _labelTruripPublisher;
        private TextBox _textTruripPublisher;

        private Label _labelTruripDeveloper;
        private TextBox _textTruripDeveloper;

        private Label _labelTruripTitleId;
        private TextBox _textTruripTitleId;

        private Label _labelTruripSource;
        private TextBox _textTruripSource;

        private Label _labelTruripCloneOf;
        private TextBox _textTruripCloneOf;

        private Label _labelTruripRelatedTo;
        private TextBox _textTruripRelatedTo;


        private Label _labelTruripYear;
        private TextBox _textTruripYear;

        private Label _labelTruripPlayers;
        private TextBox _textTruripPlayers;


        private Label _labelTruripGenre;
        private TextBox _textTruripGenre;

        private Label _labelTruripSubGenre;
        private TextBox _textTruripSubGenre;


        private Label _labelTruripRatings;
        private TextBox _textTruripRatings;

        private Label _labelTruripScore;
        private TextBox _textTruripScore;


        public FrmMain()
        {
            InitializeComponent();
            AddGameGrid();
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

            MenuItem mnuFile = new MenuItem
            {
                Text = @"Set Dir Settings",
                Tag = null
            };

            _mnuOpen = new MenuItem
            {
                Text = @"Open",
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
            _mnuContext.MenuItems.Add(mnuFile);
            _mnuContext.MenuItems.Add(mnuMakeDat);
            _mnuContext.MenuItems.Add(mnuMakeDat2);

            mnuScan.Click += MnuToSortScan;
            _mnuOpen.Click += MnuOpenClick;
            mnuFile.Click += MnuFileClick;
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
            if (Settings.rvSettings.UseFileSelection)
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
                AutoSize=false,
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
            Report.MakeFixFiles(e.Button == MouseButtons.Left);
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
            GameGrid.Columns[_gameGridSortColumnIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
            _gameGridSortColumnIndex = 0;
            _gameGridSortOrder = SortOrder.Descending;

            if (cf == null)
            {
                return;
            }

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


        private void gbSetInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 84;
            int rightPos = gbSetInfo.Width - 15;
            if (rightPos > 750)
            {
                rightPos = 750;
            }

            int width = rightPos - leftPos;


            if (_textGameName == null)
            {
                return;
            }

            {
                int textWidth = (int)((double)width * 120 / 340);
                int text2Left = leftPos + width - textWidth;
                int label2Left = text2Left - 78;

                _textGameName.Width = width;
                _textGameDescription.Width = width;
                _textGameManufacturer.Width = width;

                _textGameCloneOf.Width = textWidth;

                _labelGameYear.Left = label2Left;
                _textGameYear.Left = text2Left;
                _textGameYear.Width = textWidth;

                _textGameRomOf.Width = textWidth;

                _labelGameTotalRoms.Left = label2Left;
                _textGameTotalRoms.Left = text2Left;
                _textGameTotalRoms.Width = textWidth;
            }




            {
                int textWidth = (int)(width * 0.20);
                int text2Left = (int)(width * 0.4 + leftPos);
                int label2Left = text2Left - 78;
                int text3Left = leftPos + width - textWidth;
                int label3Left = text3Left - 78;

                _textTruripPublisher.Width = (int)(width * 0.6);
                _textTruripDeveloper.Width = (int)(width * 0.6);
                _textTruripCloneOf.Width = width;
                _textTruripRelatedTo.Width = width;

                _textTruripYear.Width = textWidth;
                _textTruripPlayers.Width = textWidth;

                _labelTruripGenre.Left = label2Left;
                _textTruripGenre.Left = text2Left;
                _textTruripGenre.Width = textWidth;

                _labelTruripSubGenre.Left = label2Left;
                _textTruripSubGenre.Left = text2Left;
                _textTruripSubGenre.Width = textWidth;


                _labelTruripTitleId.Left = label3Left;
                _textTruripTitleId.Left = text3Left;
                _textTruripTitleId.Width = textWidth;

                _labelTruripSource.Left = label3Left;
                _textTruripSource.Left = text3Left;
                _textTruripSource.Width = textWidth;

                _labelTruripRatings.Left = label3Left;
                _textTruripRatings.Left = text3Left;
                _textTruripRatings.Width = textWidth;

                _labelTruripScore.Left = label3Left;
                _textTruripScore.Left = text3Left;
                _textTruripScore.Width = textWidth;


            }
        }


        private void gbDatInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 89;
            int rightPos = (int)(gbDatInfo.Width / _scaleFactorX) - 15;
            if (rightPos > 600)
            {
                rightPos = 600;
            }

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
            if (GameGridDir != null)
                UpdateGameGrid(GameGridDir);
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