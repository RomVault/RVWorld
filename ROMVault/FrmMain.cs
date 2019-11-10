/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2010                                 *
 ******************************************************/

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using Compress;
using FileHeaderReader;
using ROMVault.Utils;
using RVCore;
using RVCore.FindFix;
using RVCore.ReadDat;
using RVCore.RvDB;
using RVCore.Scanner;
using RVCore.Utils;
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
        private Label _textGameName;

        private Label _labelGameDescription;
        private Label _textGameDescription;

        private Label _labelGameManufacturer;
        private Label _textGameManufacturer;

        private Label _labelGameCloneOf;
        private Label _textGameCloneOf;

        private Label _labelGameRomOf;
        private Label _textGameRomOf;

        private Label _labelGameYear;
        private Label _textGameYear;

        private Label _labelGameTotalRoms;
        private Label _textGameTotalRoms;

        //Trurip Extra Data
        private Label _labelTruripPublisher;
        private Label _textTruripPublisher;

        private Label _labelTruripDeveloper;
        private Label _textTruripDeveloper;

        private Label _labelTruripTitleId;
        private Label _textTruripTitleId;

        private Label _labelTruripSource;
        private Label _textTruripSource;

        private Label _labelTruripCloneOf;
        private Label _textTruripCloneOf;

        private Label _labelTruripRelatedTo;
        private Label _textTruripRelatedTo;


        private Label _labelTruripYear;
        private Label _textTruripYear;

        private Label _labelTruripPlayers;
        private Label _textTruripPlayers;


        private Label _labelTruripGenre;
        private Label _textTruripGenre;

        private Label _labelTruripSubGenre;
        private Label _textTruripSubGenre;


        private Label _labelTruripRatings;
        private Label _textTruripRatings;

        private Label _labelTruripScore;
        private Label _textTruripScore;


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

        private void AddTextBox(int line, string name, int x, int x1, out Label lBox, out Label tBox)
        {
            int y = 14 + line * 16;

            lBox = new Label
            {
                Location = SPoint(x, y + 1),
                Size = SSize(x1 - x - 2, 13),
                Text = name + @" :",
                TextAlign = ContentAlignment.TopRight
            };
            tBox = new Label { Location = SPoint(x1, y), Size = SSize(20, 17), BorderStyle = BorderStyle.FixedSingle };
            gbSetInfo.Controls.Add(lBox);
            gbSetInfo.Controls.Add(tBox);

        }

        private void AddGameGrid()
        {
            AddTextBox(0, "Name", 6, 84, out _labelGameName, out _textGameName);
            AddTextBox(1, "Description", 6, 84, out _labelGameDescription, out _textGameDescription);
            AddTextBox(2, "Manufacturer", 6, 84, out _labelGameManufacturer, out _textGameManufacturer);

            AddTextBox(3, "Clone of", 6, 84, out _labelGameCloneOf, out _textGameCloneOf);
            AddTextBox(3, "Year", 206, 284, out _labelGameYear, out _textGameYear);

            AddTextBox(4, "Rom of", 6, 84, out _labelGameRomOf, out _textGameRomOf);
            AddTextBox(4, "Total ROMs", 206, 284, out _labelGameTotalRoms, out _textGameTotalRoms);

            //Trurip

            AddTextBox(2, "Publisher", 6, 84, out _labelTruripPublisher, out _textTruripPublisher);
            AddTextBox(2, "Title Id", 406, 484, out _labelTruripTitleId, out _textTruripTitleId);

            AddTextBox(3, "Developer", 6, 84, out _labelTruripDeveloper, out _textTruripDeveloper);
            AddTextBox(3, "Source", 406, 484, out _labelTruripSource, out _textTruripSource);

            AddTextBox(4, "Clone of", 6, 84, out _labelTruripCloneOf, out _textTruripCloneOf);
            AddTextBox(5, "Related to", 6, 84, out _labelTruripRelatedTo, out _textTruripRelatedTo);

            AddTextBox(6, "Year", 6, 84, out _labelTruripYear, out _textTruripYear);
            AddTextBox(6, "Genre", 206, 284, out _labelTruripGenre, out _textTruripGenre);
            AddTextBox(6, "Ratings", 406, 484, out _labelTruripRatings, out _textTruripRatings);

            AddTextBox(7, "Players", 6, 84, out _labelTruripPlayers, out _textTruripPlayers);
            AddTextBox(7, "SubGenre", 206, 284, out _labelTruripSubGenre, out _textTruripSubGenre);
            AddTextBox(7, "Score", 406, 484, out _labelTruripScore, out _textTruripScore);


            gbSetInfo_Resize(null, new EventArgs());
            UpdateRomGrid(new RvFile(FileType.Dir));
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



        private void RomGridSelectionChanged(object sender, EventArgs e)
        {
            RomGrid.ClearSelection();
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

        private void RomGridMouseUp(object sender, MouseEventArgs e)
        {
            if (e == null || e.Button != MouseButtons.Right)
            {
                return;
            }

            int currentMouseOverRow = RomGrid.HitTest(e.X, e.Y).RowIndex;
            if (currentMouseOverRow < 0)
            {
                return;
            }

            string name = (RomGrid.Rows[currentMouseOverRow].Cells[1].Value ?? "").ToString();
            string size = (RomGrid.Rows[currentMouseOverRow].Cells[3].Value ?? "").ToString();
            if (size.Contains(" "))
            {
                size = size.Substring(0, size.IndexOf(" "));
            }

            string crc = (RomGrid.Rows[currentMouseOverRow].Cells[4].Value ?? "").ToString();
            if (crc.Length > 8)
            {
                crc = crc.Substring(0, 8);
            }

            string sha1 = (RomGrid.Rows[currentMouseOverRow].Cells[5].Value ?? "").ToString();
            if (sha1.Length > 40)
            {
                sha1 = sha1.Substring(0, 40);
            }

            string md5 = (RomGrid.Rows[currentMouseOverRow].Cells[6].Value ?? "").ToString();
            if (md5.Length > 32)
            {
                md5 = md5.Substring(0, 32);
            }

            string clipText = "Name : " + name + Environment.NewLine;
            clipText += "Size : " + size + Environment.NewLine;
            clipText += "CRC32: " + crc + Environment.NewLine;
            if (sha1.Length > 0)
            {
                clipText += "SHA1 : " + sha1 + Environment.NewLine;
            }

            if (md5.Length > 0)
            {
                clipText += "MD5  : " + md5 + Environment.NewLine;
            }

            try
            {
                Clipboard.Clear();
                Clipboard.SetText(clipText);
            }
            catch
            {
            }
        }

        private void GameGrid_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int currentMouseOverRow = GameGrid.HitTest(e.X, e.Y).RowIndex;
            if (currentMouseOverRow < 0)
            {
                return;
            }

            object r1 = GameGrid.Rows[currentMouseOverRow].Cells[1].FormattedValue;
            string filename = r1?.ToString() ?? "";
            object r2 = GameGrid.Rows[currentMouseOverRow].Cells[2].FormattedValue;
            string description = r2?.ToString() ?? "";

            try
            {
                Clipboard.Clear();
                Clipboard.SetText("Name : " + filename + Environment.NewLine + "Desc : " + description +
                                  Environment.NewLine);
            }
            catch
            {
            }
        }

        // Override the default "string" sort of the values in the 'Size' column of the RomGrid
        private void RomGrid_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            try // to sort by 'Size', then by 'Name'.
            {
                if (e.Column.Index != 2)
                {
                    return;
                }

                // compare only the value found before the first space character in each CellValue (excludes " (DHV)", etc..)
                e.SortResult = int.Parse(e.CellValue1.ToString().Split(' ')[0])
                    .CompareTo(int.Parse(e.CellValue2.ToString().Split(' ')[0]));
                if (e.SortResult == 0) // when sizes are the same, sort by the name in column 1
                {
                    e.SortResult = string.CompareOrdinal(
                        RomGrid.Rows[e.RowIndex1].Cells[1].Value.ToString(),
                        RomGrid.Rows[e.RowIndex2].Cells[1].Value.ToString());
                }

                e.Handled = true; // bypass the default string sort
            }
            catch
            {
            }
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


        private void BtnFixFilesClick(object sender, EventArgs e)
        {
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

        #region "Game Grid Code"

        private RvFile GameGridDir;
        private void UpdateGameGrid(RvFile tDir)
        {
            GameGridDir = tDir;

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
            lblDITRomsUnknown.Text =
                (tDir.DirStatus.CountUnknown() + tDir.DirStatus.CountInToSort()).ToString(CultureInfo.InvariantCulture);

            _updatingGameGrid = true;

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


            ReportStatus tDirStat;

            _gameGridColumnXPositions = new int[(int)RepStatus.EndValue];

            int rowCount = 0;
            for (int j = 0; j < tDir.ChildCount; j++)
            {
                RvFile tChildDir = tDir.Child(j);
                if (!tChildDir.IsDir)
                {
                    continue;
                }

                tDirStat = tChildDir.DirStatus;

                bool gCorrect = tDirStat.HasCorrect();
                bool gMissing = tDirStat.HasMissing();
                bool gUnknown = tDirStat.HasUnknown();
                bool gInToSort = tDirStat.HasInToSort();
                bool gFixes = tDirStat.HasFixesNeeded();

                bool show = chkBoxShowCorrect.Checked && gCorrect && !gMissing && !gFixes;
                show = show || chkBoxShowMissing.Checked && gMissing;
                show = show || chkBoxShowFixed.Checked && gFixes;
                show = show || gUnknown;
                show = show || gInToSort;
                show = show || tChildDir.GotStatus == GotStatus.Corrupt;
                show = show || !(gCorrect || gMissing || gUnknown || gInToSort || gFixes);

                if (txtFilter.Text.Length > 0)
                {
                    show = tChildDir.Name.Contains(txtFilter.Text);
                }

                if (!show)
                {
                    continue;
                }

                rowCount++;

                int columnIndex = 0;
                for (int l = 0; l < RepairStatus.DisplayOrder.Length; l++)
                {
                    if (l >= 13)
                    {
                        columnIndex = l;
                    }

                    if (tDirStat.Get(RepairStatus.DisplayOrder[l]) <= 0)
                    {
                        continue;
                    }

                    int len = DigitLength(tDirStat.Get(RepairStatus.DisplayOrder[l])) * 7 + 26;
                    if (len > _gameGridColumnXPositions[columnIndex])
                    {
                        _gameGridColumnXPositions[columnIndex] = len;
                    }

                    columnIndex++;
                }
            }

            GameGrid.RowCount = rowCount;

            int t = 0;
            for (int l = 0; l < (int)RepStatus.EndValue; l++)
            {
                int colWidth = _gameGridColumnXPositions[l];
                _gameGridColumnXPositions[l] = t;
                t += colWidth;
            }

            int row = 0;
            for (int j = 0; j < tDir.ChildCount; j++)
            {
                RvFile tChildDir = tDir.Child(j);
                if (!tChildDir.IsDir)
                {
                    continue;
                }

                tDirStat = tChildDir.DirStatus;

                bool gCorrect = tDirStat.HasCorrect();
                bool gMissing = tDirStat.HasMissing();
                bool gUnknown = tDirStat.HasUnknown();
                bool gFixes = tDirStat.HasFixesNeeded();
                bool gInToSort = tDirStat.HasInToSort();

                bool show = chkBoxShowCorrect.Checked && gCorrect && !gMissing && !gFixes;
                show = show || chkBoxShowMissing.Checked && gMissing;
                show = show || chkBoxShowFixed.Checked && gFixes;
                show = show || gUnknown;
                show = show || gInToSort;
                show = show || tChildDir.GotStatus == GotStatus.Corrupt;
                show = show || !(gCorrect || gMissing || gUnknown || gInToSort || gFixes);

                if (txtFilter.Text.Length > 0)
                {
                    show = tChildDir.Name.Contains(txtFilter.Text);
                }

                if (!show)
                {
                    continue;
                }

                GameGrid.Rows[row].Selected = false;
                GameGrid.Rows[row].Tag = tChildDir;
                row++;
            }

            _updatingGameGrid = false;

            UpdateRomGrid(tDir);
        }

        private static int DigitLength(int number)
        {
            int textNumber = number;
            int len = 0;

            while (textNumber > 0)
            {
                textNumber = textNumber / 10;
                len++;
            }

            return len;
        }

        private void GameGridSelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectedGame();
        }

        private void GameGridMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_updatingGameGrid)
            {
                return;
            }

            if (GameGrid.SelectedRows.Count != 1)
            {
                return;
            }

            RvFile tGame = (RvFile)GameGrid.SelectedRows[0].Tag;
            if (tGame.Game == null)
            {
                UpdateGameGrid(tGame);
                DirTree.SetSelected(tGame);
            }
            else
            {
                string path = tGame.Dat.GetData(RvDat.DatData.DatRootFullName);
                path = Path.GetDirectoryName(path);
                if (Settings.rvSettings?.EInfo == null)
                    return;

                foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
                {
                    if (!string.Equals(path, ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    string commandLineOptions = ei.CommandLine;
                    string dirname = tGame.Parent.FullName;
                    commandLineOptions = commandLineOptions.Replace("{gamename}", tGame.Name);
                    commandLineOptions = commandLineOptions.Replace("{gamedirectory}", dirname);
                    using (Process exeProcess = new Process())
                    {
                        exeProcess.StartInfo.FileName = ei.ExeName;
                        exeProcess.StartInfo.Arguments = commandLineOptions;
                        exeProcess.StartInfo.UseShellExecute = false;
                        exeProcess.StartInfo.CreateNoWindow = true;
                        exeProcess.Start();
                    }
                }
            }
        }

        private void GameGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_updatingGameGrid)
            {
                return;
            }

            Rectangle cellBounds = GameGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            RvFile tRvDir = (RvFile)GameGrid.Rows[e.RowIndex].Tag;
            ReportStatus tDirStat = tRvDir.DirStatus;
            Color bgCol = Color.FromArgb(255, 255, 255);
            Color fgCol = Color.FromArgb(0, 0, 0);

            if (cellBounds.Width == 0 || cellBounds.Height == 0)
            {
                return;
            }

            foreach (RepStatus t1 in RepairStatus.DisplayOrder)
            {
                if (tDirStat.Get(t1) <= 0)
                {
                    continue;
                }

                bgCol = _displayColor[(int)t1];
                fgCol = _fontColor[(int)t1];
                break;
            }

            switch (GameGrid.Columns[e.ColumnIndex].Name)
            {
                case "Type":
                    {
                        e.CellStyle.BackColor = bgCol;
                        e.CellStyle.SelectionBackColor = bgCol;
                        e.CellStyle.ForeColor = fgCol;

                        Bitmap bmp = new Bitmap(cellBounds.Width, cellBounds.Height);
                        Graphics g = Graphics.FromImage(bmp);

                        string bitmapName;
                        switch (tRvDir.FileType)
                        {
                            case FileType.Zip:
                                if (tRvDir.RepStatus == RepStatus.DirCorrect && tRvDir.ZipStatus == ZipStatus.TrrntZip)
                                {
                                    bitmapName = "ZipTZ";
                                }
                                else
                                {
                                    bitmapName = "Zip" + tRvDir.RepStatus;
                                }

                                break;
                            case FileType.SevenZip:
                                if (tRvDir.RepStatus == RepStatus.DirCorrect && tRvDir.ZipStatus == ZipStatus.TrrntZip)
                                {
                                    bitmapName = "SevenZipTZ";
                                }
                                else if (tRvDir.RepStatus == RepStatus.DirCorrect && tRvDir.ZipStatus == ZipStatus.Trrnt7Zip)
                                {
                                    bitmapName = "SevenZipT7Z";
                                }
                                else
                                {
                                    bitmapName = "SevenZip" + tRvDir.RepStatus;
                                }

                                break;
                            default:
                                // hack because DirDirInToSort image doesnt exist.
                                if (tRvDir.RepStatus == RepStatus.DirInToSort)
                                {
                                    bitmapName = "Dir" + RepStatus.DirUnknown;
                                }
                                else
                                {
                                    bitmapName = "Dir" + tRvDir.RepStatus;
                                }

                                break;
                        }

                        Bitmap bm = rvImages.GetBitmap(bitmapName);
                        if (bm != null)
                        {
                            float xSize = (float)bm.Width / bm.Height * (cellBounds.Height - 1);

                            g.DrawImage(bm, (cellBounds.Width - xSize) / 2, 0, xSize, cellBounds.Height - 1);
                            bm.Dispose();
                        }
                        else
                        {
                            Debug.WriteLine("Missing Graphic for " + bitmapName);
                        }

                        e.Value = bmp;
                        break;
                    }

                case "CGame":
                    {
                        e.CellStyle.BackColor = bgCol;
                        e.CellStyle.ForeColor = fgCol;

                        if (string.IsNullOrEmpty(tRvDir.FileName))
                        {
                            e.Value = tRvDir.Name;
                        }
                        else
                        {
                            e.Value = tRvDir.Name + " (Found: " + tRvDir.FileName + ")";
                        }

                        break;
                    }

                case "CDescription":
                    {
                        e.CellStyle.BackColor = bgCol;
                        e.CellStyle.ForeColor = fgCol;

                        if (tRvDir.Game != null)
                        {
                            e.Value = tRvDir.Game.GetData(RvGame.GameData.Description);
                        }

                        break;
                    }

                case "CCorrect":
                    {
                        e.CellStyle.SelectionBackColor = Color.White;

                        Bitmap bmp = new Bitmap(cellBounds.Width, cellBounds.Height);
                        Graphics g = Graphics.FromImage(bmp);
                        g.Clear(Color.White);
                        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                        Font drawFont = new Font("Arial", 9);
                        SolidBrush drawBrushBlack = new SolidBrush(Color.Black);

                        int gOff;
                        int columnIndex = 0;
                        for (int l = 0; l < RepairStatus.DisplayOrder.Length; l++)
                        {
                            if (l >= 13)
                            {
                                columnIndex = l;
                            }

                            if (tRvDir.DirStatus.Get(RepairStatus.DisplayOrder[l]) <= 0)
                            {
                                continue;
                            }

                            gOff = _gameGridColumnXPositions[columnIndex];
                            Bitmap bm = rvImages.GetBitmap(@"G_" + RepairStatus.DisplayOrder[l]);
                            if (bm != null)
                            {
                                g.DrawImage(bm, gOff, 0, 21, 18);
                                bm.Dispose();
                            }
                            else
                            {
                                Debug.WriteLine("Missing Graphics for " + "G_" + RepairStatus.DisplayOrder[l]);
                            }

                            columnIndex++;
                        }

                        columnIndex = 0;
                        for (int l = 0; l < RepairStatus.DisplayOrder.Length; l++)
                        {
                            if (l >= 13)
                            {
                                columnIndex = l;
                            }

                            if (tRvDir.DirStatus.Get(RepairStatus.DisplayOrder[l]) > 0)
                            {
                                gOff = _gameGridColumnXPositions[columnIndex];
                                g.DrawString(
                                    tRvDir.DirStatus.Get(RepairStatus.DisplayOrder[l]).ToString(CultureInfo.InvariantCulture),
                                    drawFont, drawBrushBlack, new PointF(gOff + 20, 3));
                                columnIndex++;
                            }
                        }

                        drawBrushBlack.Dispose();
                        drawFont.Dispose();
                        e.Value = bmp;
                        break;
                    }

                default:
                    Console.WriteLine(
                        $@"WARN: GameGrid_CellFormatting() unknown column: {GameGrid.Columns[e.ColumnIndex].Name}");
                    break;
            }
        }

        private void GameGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            // only allow sort on CGame/CDescription
            if (e.ColumnIndex != 1 && e.ColumnIndex != 2)
            {
                return;
            }

            DataGridViewColumn newColumn = GameGrid.Columns[e.ColumnIndex];
            DataGridViewColumn oldColumn = GameGrid.Columns[_gameGridSortColumnIndex];

            if (newColumn == oldColumn)
            {
                _gameGridSortOrder = _gameGridSortOrder == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                oldColumn.HeaderCell.SortGlyphDirection = SortOrder.None;
                _gameGridSortOrder = SortOrder.Ascending;
            }

            GameGrid.Sort(new GameGridRowComparer(_gameGridSortOrder, e.ColumnIndex));
            newColumn.HeaderCell.SortGlyphDirection = _gameGridSortOrder;
            _gameGridSortColumnIndex = e.ColumnIndex;
        }

        private class GameGridRowComparer : IComparer
        {
            private readonly int _sortMod = 1;
            private readonly int _columnIndex;

            public GameGridRowComparer(SortOrder sortOrder, int index)
            {
                _columnIndex = index;

                if (sortOrder == SortOrder.Descending)
                {
                    _sortMod = -1;
                }
            }

            public int Compare(object a, object b)
            {
                DataGridViewRow aRow = (DataGridViewRow)a;
                DataGridViewRow bRow = (DataGridViewRow)b;

                RvFile aRvDir = (RvFile)aRow?.Tag;
                RvFile bRvDir = (RvFile)bRow?.Tag;

                if (aRvDir == null || bRvDir == null)
                    return 0;

                int result = 0;
                switch (_columnIndex)
                {
                    case 1: // CGame
                        result = string.CompareOrdinal(aRvDir.Name, bRvDir.Name);
                        break;
                    case 2: // CDescription
                        string aDes = "";
                        string bDes = "";
                        if (aRvDir.Game != null)
                        {
                            aDes = aRvDir.Game.GetData(RvGame.GameData.Description);
                        }

                        if (bRvDir.Game != null)
                        {
                            bDes = bRvDir.Game.GetData(RvGame.GameData.Description);
                        }

                        result = string.CompareOrdinal(aDes, bDes);

                        // if desciptions match, fall through to sorting by name
                        if (result == 0)
                        {
                            result = string.CompareOrdinal(aRvDir.Name, bRvDir.Name);
                        }

                        break;
                    default:
                        Console.WriteLine($@"WARN: GameGridRowComparer::Compare() Invalid columnIndex: {_columnIndex}");
                        break;
                }

                return _sortMod * result;
            }
        }

        #endregion

        #region "Rom Grid Code"

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


        private void UpdateSelectedGame()
        {
            if (_updatingGameGrid)
            {
                return;
            }

            if (GameGrid.SelectedRows.Count != 1)
            {
                return;
            }

            RvFile tGame = (RvFile)GameGrid.SelectedRows[0].Tag;
            UpdateRomGrid(tGame);
        }

        private void UpdateRomGrid(RvFile tGame)
        {
            _labelGameName.Visible = true;
            _textGameName.Text = tGame.Name;
            if (tGame.Game == null)
            {
                _labelGameDescription.Visible = false;
                _textGameDescription.Visible = false;
            }

            if (tGame.Game == null || tGame.Game.GetData(RvGame.GameData.EmuArc) != "yes")
            {
                _labelTruripPublisher.Visible = false;
                _textTruripPublisher.Visible = false;

                _labelTruripDeveloper.Visible = false;
                _textTruripDeveloper.Visible = false;

                _labelTruripTitleId.Visible = false;
                _textTruripTitleId.Visible = false;

                _labelTruripSource.Visible = false;
                _textTruripSource.Visible = false;

                _labelTruripCloneOf.Visible = false;
                _textTruripCloneOf.Visible = false;

                _labelTruripRelatedTo.Visible = false;
                _textTruripRelatedTo.Visible = false;

                _labelTruripYear.Visible = false;
                _textTruripYear.Visible = false;

                _labelTruripPlayers.Visible = false;
                _textTruripPlayers.Visible = false;

                _labelTruripGenre.Visible = false;
                _textTruripGenre.Visible = false;

                _labelTruripSubGenre.Visible = false;
                _textTruripSubGenre.Visible = false;

                _labelTruripRatings.Visible = false;
                _textTruripRatings.Visible = false;

                _labelTruripScore.Visible = false;
                _textTruripScore.Visible = false;
            }

            if (tGame.Game == null || tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes")
            {
                _labelGameManufacturer.Visible = false;
                _textGameManufacturer.Visible = false;

                _labelGameCloneOf.Visible = false;
                _textGameCloneOf.Visible = false;

                _labelGameRomOf.Visible = false;
                _textGameRomOf.Visible = false;

                _labelGameYear.Visible = false;
                _textGameYear.Visible = false;

                _labelGameTotalRoms.Visible = false;
                _textGameTotalRoms.Visible = false;
            }


            if (tGame.Game != null)
            {
                if (tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes")
                {
                    _labelGameDescription.Visible = true;
                    _textGameDescription.Visible = true;
                    _textGameDescription.Text = tGame.Game.GetData(RvGame.GameData.Description);

                    _labelTruripPublisher.Visible = true;
                    _textTruripPublisher.Visible = true;
                    _textTruripPublisher.Text = tGame.Game.GetData(RvGame.GameData.Publisher);

                    _labelTruripDeveloper.Visible = true;
                    _textTruripDeveloper.Visible = true;
                    _textTruripDeveloper.Text = tGame.Game.GetData(RvGame.GameData.Developer);


                    _labelTruripTitleId.Visible = true;
                    _textTruripTitleId.Visible = true;
                    _textTruripTitleId.Text = tGame.Game.GetData(RvGame.GameData.TitleId);

                    _labelTruripSource.Visible = true;
                    _textTruripSource.Visible = true;
                    _textTruripSource.Text = tGame.Game.GetData(RvGame.GameData.Source);

                    _labelTruripCloneOf.Visible = true;
                    _textTruripCloneOf.Visible = true;
                    _textTruripCloneOf.Text = tGame.Game.GetData(RvGame.GameData.CloneOf);

                    _labelTruripRelatedTo.Visible = true;
                    _textTruripRelatedTo.Visible = true;
                    _textTruripRelatedTo.Text = tGame.Game.GetData(RvGame.GameData.RelatedTo);

                    _labelTruripYear.Visible = true;
                    _textTruripYear.Visible = true;
                    _textTruripYear.Text = tGame.Game.GetData(RvGame.GameData.Year);

                    _labelTruripPlayers.Visible = true;
                    _textTruripPlayers.Visible = true;
                    _textTruripPlayers.Text = tGame.Game.GetData(RvGame.GameData.Players);

                    _labelTruripGenre.Visible = true;
                    _textTruripGenre.Visible = true;
                    _textTruripGenre.Text = tGame.Game.GetData(RvGame.GameData.Genre);

                    _labelTruripSubGenre.Visible = true;
                    _textTruripSubGenre.Visible = true;
                    _textTruripSubGenre.Text = tGame.Game.GetData(RvGame.GameData.SubGenre);

                    _labelTruripRatings.Visible = true;
                    _textTruripRatings.Visible = true;
                    _textTruripRatings.Text = tGame.Game.GetData(RvGame.GameData.Ratings);

                    _labelTruripScore.Visible = true;
                    _textTruripScore.Visible = true;
                    _textTruripScore.Text = tGame.Game.GetData(RvGame.GameData.Score);

                    LoadPannels(tGame);
                }
                else
                {
                    HidePannel();

                    _labelGameDescription.Visible = true;
                    _textGameDescription.Visible = true;
                    _textGameDescription.Text = tGame.Game.GetData(RvGame.GameData.Description);

                    _labelGameManufacturer.Visible = true;
                    _textGameManufacturer.Visible = true;
                    _textGameManufacturer.Text = tGame.Game.GetData(RvGame.GameData.Manufacturer);

                    _labelGameCloneOf.Visible = true;
                    _textGameCloneOf.Visible = true;
                    _textGameCloneOf.Text = tGame.Game.GetData(RvGame.GameData.CloneOf);

                    _labelGameRomOf.Visible = true;
                    _textGameRomOf.Visible = true;
                    _textGameRomOf.Text = tGame.Game.GetData(RvGame.GameData.RomOf);

                    _labelGameYear.Visible = true;
                    _textGameYear.Visible = true;
                    _textGameYear.Text = tGame.Game.GetData(RvGame.GameData.Year);

                    _labelGameTotalRoms.Visible = true;
                    _textGameTotalRoms.Visible = true;

                }
            }
            else
            {
                HidePannel();
            }


            if (Settings.IsMono && RomGrid.RowCount > 0)
            {
                RomGrid.CurrentCell = RomGrid[0, 0];
            }

            RomGrid.Rows.Clear();
            AddDir(tGame, "");
            GC.Collect();
        }



        private void AddDir(RvFile tGame, string pathAdd)
        {
            for (int l = 0; l < tGame.ChildCount; l++)
            {
                RvFile tBase = tGame.Child(l);

                RvFile tFile = tBase;
                if (tFile.IsFile)
                {
                    AddRom(tFile, pathAdd);
                }

                if (tGame.Dat == null)
                {
                    continue;
                }

                RvFile tDir = tBase;
                if (!tDir.IsDir)
                {
                    continue;
                }

                if (tDir.Game == null)
                {
                    AddDir(tDir, pathAdd + tGame.Name + "/");
                }
            }
        }

        // returns either white or black, depending of quick luminance of the Color " a "
        // called when the _displayColor is finished, in order to populate the _fontColor table.
        private static Color Contrasty(Color a)
        {
            return (a.R << 1) + a.B + a.G + (a.G << 2) < 1024 ? Color.White : Color.Black;
        }

        private void AddRom(RvFile tRomTable, string pathAdd)
        {
            if (tRomTable.DatStatus != DatStatus.InDatMerged || tRomTable.RepStatus != RepStatus.NotCollected ||
                chkBoxShowMerged.Checked)
            {
                RomGrid.Rows.Add();
                int row = RomGrid.Rows.Count - 1;
                RomGrid.Rows[row].Tag = tRomTable;

                for (int i = 0; i < RomGrid.Rows[row].Cells.Count; i++)
                {
                    DataGridViewCellStyle cs = RomGrid.Rows[row].Cells[i].Style;
                    cs.BackColor = _displayColor[(int)tRomTable.RepStatus];
                    cs.ForeColor = _fontColor[(int)tRomTable.RepStatus];
                }

                string fname = pathAdd + tRomTable.Name;
                if (!string.IsNullOrEmpty(tRomTable.FileName))
                {
                    fname += " (Found: " + tRomTable.FileName + ")";
                }

                if (tRomTable.CHDVersion != null)
                {
                    fname += " (V" + tRomTable.CHDVersion + ")";
                }

                if (tRomTable.HeaderFileType != HeaderFileType.Nothing)
                {
                    fname += " (" + tRomTable.HeaderFileType + ")";
                }


                RomGrid.Rows[row].Cells["CRom"].Value = fname;

                RomGrid.Rows[row].Cells["CMerge"].Value = tRomTable.Merge;
                RomGrid.Rows[row].Cells["CStatus"].Value = tRomTable.Status;

                SetCell(RomGrid.Rows[row].Cells["CSize"], tRomTable.Size.ToString(), tRomTable, FileStatus.SizeFromDAT, FileStatus.SizeFromHeader, FileStatus.SizeVerified);
                SetCell(RomGrid.Rows[row].Cells["CCRC32"], tRomTable.CRC.ToHexString(), tRomTable, FileStatus.CRCFromDAT, FileStatus.CRCFromHeader, FileStatus.CRCVerified);
                SetCell(RomGrid.Rows[row].Cells["CSHA1"], tRomTable.SHA1.ToHexString(), tRomTable, FileStatus.SHA1FromDAT, FileStatus.SHA1FromHeader, FileStatus.SHA1Verified);
                SetCell(RomGrid.Rows[row].Cells["CMD5"], tRomTable.MD5.ToHexString(), tRomTable, FileStatus.MD5FromDAT, FileStatus.MD5FromHeader, FileStatus.MD5Verified);

                SetCell(RomGrid.Rows[row].Cells["CAltSize"], tRomTable.AltSize.ToString(), tRomTable, FileStatus.AltSizeFromDAT, FileStatus.AltSizeFromHeader, FileStatus.AltSizeVerified);
                SetCell(RomGrid.Rows[row].Cells["CAltCRC32"], tRomTable.AltCRC.ToHexString(), tRomTable, FileStatus.AltCRCFromDAT, FileStatus.AltCRCFromHeader, FileStatus.AltCRCVerified);
                SetCell(RomGrid.Rows[row].Cells["CAltSHA1"], tRomTable.AltSHA1.ToHexString(), tRomTable, FileStatus.AltSHA1FromDAT, FileStatus.AltSHA1FromHeader, FileStatus.AltSHA1Verified);
                SetCell(RomGrid.Rows[row].Cells["CAltMD5"], tRomTable.AltMD5.ToHexString(), tRomTable, FileStatus.AltMD5FromDAT, FileStatus.AltMD5FromHeader, FileStatus.AltMD5Verified);

                if (tRomTable.FileType == FileType.ZipFile)
                {
                    RomGrid.Rows[row].Cells["ZipIndex"].Value = tRomTable.ZipFileIndex == -1
                        ? ""
                        : tRomTable.ZipFileIndex.ToString(CultureInfo.InvariantCulture);
                    RomGrid.Rows[row].Cells["ZipHeader"].Value = tRomTable.ZipFileHeaderPosition == null
                        ? ""
                        : tRomTable.ZipFileHeaderPosition.ToString();
                }
            }
        }

        private static void SetCell(DataGridViewCell cell, string txt, RvFile tRomTable, FileStatus dat, FileStatus file, FileStatus verified)
        {
            cell.Value = txt + ShowFlags(tRomTable, dat, file, verified);
            if (!string.IsNullOrWhiteSpace(txt) && !tRomTable.FileStatusIs(dat))
                cell.Style.ForeColor = Color.FromArgb(0, 0, 255);
        }

        private static string ShowFlags(RvFile tRomTable, FileStatus dat, FileStatus file, FileStatus verified)
        {
            string flags = "";
            if (tRomTable.FileStatusIs(dat))
            {
                flags += "D";
            }

            if (tRomTable.FileStatusIs(file))
            {
                flags += "F";
            }

            if (tRomTable.FileStatusIs(verified))
            {
                flags += "V";
            }

            if (!string.IsNullOrEmpty(flags))
            {
                flags = " (" + flags + ")";
            }

            return flags;
        }

        private void RomGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_updatingGameGrid)
            {
                return;
            }

            Rectangle cellBounds = RomGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            RvFile tRvFile = (RvFile)RomGrid.Rows[e.RowIndex].Tag;

            if (cellBounds.Width == 0 || cellBounds.Height == 0)
            {
                return;
            }

            if (RomGrid.Columns[e.ColumnIndex].Name == "CGot")
            {
                Bitmap bmp = new Bitmap(cellBounds.Width, cellBounds.Height);
                Graphics g = Graphics.FromImage(bmp);
                string bitmapName = "R_" + tRvFile.DatStatus + "_" + tRvFile.RepStatus;
                Bitmap romIcon = rvImages.GetBitmap(bitmapName);
                if (romIcon != null)
                {
                    g.DrawImage(romIcon, 0, 0, 54, 18);
                    e.Value = bmp;
                }
                else
                {
                    Debug.WriteLine($"Missing image for {bitmapName}");
                }
            }
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

        private void TabArtworkInitialize()
        {
            splitListArt.Panel2Collapsed = true;
            splitListArt.Panel2.Hide();

            tabArtWork_Resize(null, new EventArgs());
            tabScreens_Resize(null, new EventArgs());
            tabInfo_Resize(null, new EventArgs());
        }

        private void tabArtWork_Resize(object sender, EventArgs e)
        {
            int imageWidth = tabArtWork.Width - 20;
            if (imageWidth < 2)
                imageWidth = 2;

            picArtwork.Left = 10;
            picArtwork.Width = imageWidth;
            picArtwork.Top = (int)(tabArtWork.Height * 0.05);
            picArtwork.Height = (int)(tabArtWork.Height * 0.4);

            picLogo.Left = 10;
            picLogo.Width = imageWidth;
            picLogo.Top = (int)(tabArtWork.Height * 0.55);
            picLogo.Height = (int)(tabArtWork.Height * 0.4);
        }

        private void tabScreens_Resize(object sender, EventArgs e)
        {
            int imageWidth = tabScreens.Width - 20;
            if (imageWidth < 2)
                imageWidth = 2;

            picScreenTitle.Left = 10;
            picScreenTitle.Width = imageWidth;
            picScreenTitle.Top = (int)(tabScreens.Height * 0.05);
            picScreenTitle.Height = (int)(tabScreens.Height * 0.4);

            picScreenShot.Left = 10;
            picScreenShot.Width = imageWidth;
            picScreenShot.Top = (int)(tabScreens.Height * 0.55);
            picScreenShot.Height = (int)(tabScreens.Height * 0.4);
        }
        private void tabInfo_Resize(object sender, EventArgs e)
        {

        }

        private void LoadPannels(RvFile tGame)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);

            /*
             * artwork_front.png
             * artowrk_back.png
             * logo.png
             * medium_front.png
             * screentitle.png
             * screenshot.png
             * story.txt
             *
             * System.Diagnostics.Process.Start(@"D:\stage\RomVault\RomRoot\SNK\Neo Geo CD (World) - SuperDAT\Games\Double Dragon (19950603)\video.mp4");
             *
             */


            bool artLoaded = picArtwork.TryLoadImage(tGame, "artwork_front");
            bool logoLoaded = picLogo.TryLoadImage(tGame, "logo");
            bool titleLoaded = picScreenTitle.TryLoadImage(tGame, "screentitle");
            bool screenLoaded = picScreenShot.TryLoadImage(tGame, "screenshot");
            bool storyLoaded = txtInfo.LoadText(tGame, "story.txt");

            if (artLoaded || logoLoaded) TabEmuArc.TabPages.Add(tabArtWork);
            if (titleLoaded || screenLoaded) TabEmuArc.TabPages.Add(tabScreens);
            if (storyLoaded) TabEmuArc.TabPages.Add(tabInfo);

            if (artLoaded || logoLoaded || titleLoaded || screenLoaded || storyLoaded)
            {
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
            }

        }

        private void HidePannel()
        {
            splitListArt.Panel2Collapsed = true;
            splitListArt.Panel2.Hide();

            picArtwork.ClearImage();
            picLogo.ClearImage();
            picScreenTitle.ClearImage();
            picScreenShot.ClearImage();
            txtInfo.ClearText();
        }

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