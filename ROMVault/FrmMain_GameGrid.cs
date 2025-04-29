using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using Compress;
using RomVaultCore;
using RomVaultCore.RvDB;
using RVIO;
using StorageList;

namespace ROMVault
{

    public partial class FrmMain
    {
        private enum GameGridColumns
        {
            CType = 0,
            CGame = 1,
            CDescription = 2,
            CDateTime = 3,
            CRomStatus = 4
        }

        private RvFile gameGridSource;

        private RvFile[] gameGrid;

        private int gameSortIndex = -1;
        private SortOrder gameSortDir = SortOrder.None;

        private bool showDescription;

        private ContextMenuStrip _mnuGameGrid;

        ToolStripMenuItem mnuGameScan1;
        ToolStripMenuItem mnuGameScan2;
        ToolStripMenuItem mnuGameScan3;
        ToolStripMenuItem mnuOpenDir;
        ToolStripMenuItem mnuOpenParentDir;
        ToolStripMenuItem mnuLaunchEmulator;
        ToolStripMenuItem mnuOpenPage;

        private void InitGameGridMenu()
        {
            _mnuGameGrid = new ContextMenuStrip();


            mnuGameScan1 = new ToolStripMenuItem
            {
                Text = @"Scan Quick (Headers Only)",
                Tag = EScanLevel.Level1
            };
            mnuGameScan2 = new ToolStripMenuItem
            {
                Text = @"Scan",
                Tag = EScanLevel.Level2
            };
            mnuGameScan3 = new ToolStripMenuItem
            {
                Text = @"Scan Full (Complete Re-Scan)",
                Tag = EScanLevel.Level3
            };

            mnuGameScan1.Click += MnuGameScan;
            mnuGameScan2.Click += MnuGameScan;
            mnuGameScan3.Click += MnuGameScan;


            mnuOpenDir = new ToolStripMenuItem
            {
                Text = @"Open Dir",
                Tag = null
            };
            mnuOpenDir.Click += MnuOpenDir;

            mnuOpenParentDir = new ToolStripMenuItem
            {
                Text = @"Open Parent",
                Tag = null
            };
            mnuOpenParentDir.Click += MnuOpenParentDir;

            mnuLaunchEmulator = new ToolStripMenuItem
            {
                Text = @"Launch emulator",
                Tag = null
            };
            mnuLaunchEmulator.Click += LaunchEmulator;

            mnuOpenPage = new ToolStripMenuItem
            {
                Text = @"Open Web Page",
                Tag = null
            };
            mnuOpenPage.Click += OpenWebPage;

        }


        private void ClearGameGrid()
        {

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
            if (gameSortIndex >= 0)
                GameGrid.Columns[gameSortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
            gameSortIndex = -1;
            gameSortDir = SortOrder.None;

        }

        private void UpdateGameGrid(RvFile tDir)
        {
            gameGridSource = tDir;
            _updatingGameGrid = true;

            ClearGameGrid();
            UpdateGameGrid();

        }

        private void UpdateGameGrid(bool onTimer = false)
        {
            if (gameGridSource == null)
                return;

            try
            {

                _updatingGameGrid = true;
                showDescription = false;

                List<RvFile> gameList = new List<RvFile>();

                _gameGridColumnXPositions = new int[(int)RepStatus.EndValue];

                bool wideTypeColumn = false;

                string searchLowerCase = txtFilter.Text.ToLower();
                for (int j = 0; j < gameGridSource.ChildCount; j++)
                {
                    RvFile tChildDir = gameGridSource.Child(j);
                    if (!tChildDir.IsDirectory)
                    {
                        continue;
                    }

                    if (txtFilter.Text.Length > 0 && !tChildDir.Name.ToLower().Contains(searchLowerCase))
                    {
                        continue;
                    }

                    if (showDescription == false && tChildDir.Game != null)
                    {
                        string desc = tChildDir.Game.GetData(RvGame.GameData.Description);
                        if (!string.IsNullOrWhiteSpace(desc) && desc != "¤")
                            showDescription = true;
                    }

                    ReportStatus tDirStat = tChildDir.DirStatus;

                    bool gCorrect = tDirStat.HasCorrect();
                    bool gMissing = tDirStat.HasMissing(false);
                    bool gUnknown = tDirStat.HasUnknown();
                    bool gInToSort = tDirStat.HasInToSort();
                    bool gFixes = tDirStat.HasFixesNeeded();
                    bool gMIA = tDirStat.HasMIA();
                    bool gAllMerged = tDirStat.HasAllMerged();

                    bool show = chkBoxShowComplete.Checked && gCorrect && !gMissing && !gFixes;
                    show = show || chkBoxShowPartial.Checked && gMissing && gCorrect;
                    show = show || chkBoxShowEmpty.Checked && gMissing && !gCorrect;
                    show = show || chkBoxShowFixes.Checked && gFixes;
                    show = show || chkBoxShowMIA.Checked && gMIA;
                    show = show || chkBoxShowMerged.Checked && gAllMerged;
                    show = show || gUnknown;
                    show = show || gInToSort;
                    show = show || tChildDir.GotStatus == GotStatus.Corrupt;
                    show = show || !(gCorrect || gMissing || gUnknown || gInToSort || gFixes || gMIA || gAllMerged);

                    if (!show)
                    {
                        continue;
                    }


                    if (!wideTypeColumn)
                    {
                        string bitmapNameDat = null;
                        if (tChildDir.DatStatus != DatStatus.NotInDat && tChildDir.DatStatus != DatStatus.InToSort)
                            bitmapNameDat = GetBitmapFromType(tChildDir.FileType, tChildDir.ZipDatStruct);

                        string bitmapName = null;
                        if (tChildDir.GotStatus != GotStatus.NotGot)
                            bitmapName = GetBitmapFromType(tChildDir.FileType, tChildDir.ZipStruct);

                        if (bitmapNameDat != null && bitmapName != null && bitmapNameDat != bitmapName)
                        {
                            wideTypeColumn = true;
                        }
                    }


                    gameList.Add(tChildDir);

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

                int t = 0;
                for (int l = 0; l < (int)RepStatus.EndValue; l++)
                {
                    int colWidth = _gameGridColumnXPositions[l];
                    _gameGridColumnXPositions[l] = t;
                    t += colWidth;
                }

                gameGrid = gameList.ToArray();
                if (GameGrid.RowCount != gameGrid.Length)
                {
                    GameGrid.RowCount = gameGrid.Length;
                    if (GameGrid.RowCount > 0)
                        GameGrid.Rows[0].Selected = false;
                }

                GameGrid.Columns[(int)GameGridColumns.CDescription].Visible = showDescription;

                if (onTimer)
                {
                    if (gameSortDir != SortOrder.None && gameSortIndex >= 0)
                    {
                        IComparer<RvFile> tSort = new GameUiCompare((GameGridColumns)gameSortIndex, gameSortDir);
                        gameGrid = FastArraySort.SortArray(gameGrid, tSort.Compare);
                    }
                }


                _updatingGameGrid = false;

                GameGrid.Columns[(int)GameGridColumns.CType].Width = wideTypeColumn ? 90 : 44;

                UpdateSelectedGame(onTimer);
            }
            catch { }
        }

        private static int DigitLength(int number)
        {
            int textNumber = number;
            int len = 0;

            while (textNumber > 0)
            {
                textNumber /= 10;
                len++;
            }

            return len;
        }

        private void GameGridSelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectedGame();
        }

        private void UpdateSelectedGame(bool onTimer = false)
        {
            if (_updatingGameGrid)
            {
                return;
            }

            if (GameGrid.SelectedRows.Count != 1)
            {
                UpdateGameMetaData(new RvFile(FileType.Dir));
                UpdateRomGrid(gameGridSource);
                return;
            }

            RvFile tGame = gameGrid[GameGrid.SelectedRows[0].Index];

            UpdateGameMetaData(tGame);
            UpdateRomGrid(tGame, onTimer);
        }


        private static string GetBitmapFromType(FileType ft, ZipStructure zs)
        {
            switch (ft)
            {
                case FileType.Zip:
                    if (zs == ZipStructure.None) { return "Zip"; }
                    if (zs == ZipStructure.ZipTrrnt) { return "ZipTrrnt"; }
                    if (zs == ZipStructure.ZipTDC) { return "ZipTDC"; }
                    if (zs == ZipStructure.ZipZSTD) { return "ZipZSTD"; }
                    return null;
                case FileType.SevenZip:
                    if (zs == ZipStructure.None) { return "SevenZip"; }
                    if (zs == ZipStructure.SevenZipTrrnt) { return "SevenZipTrrnt"; }
                    if (zs == ZipStructure.SevenZipSLZMA) { return "SevenZipSLZMA"; }
                    if (zs == ZipStructure.SevenZipNLZMA) { return "SevenZipNLZMA"; }
                    if (zs == ZipStructure.SevenZipSZSTD) { return "SevenZipSZSTD"; }
                    if (zs == ZipStructure.SevenZipNZSTD) { return "SevenZipNZSTD"; }
                    return null;
                case FileType.Dir:
                    return "Dir";
            }
            return null;
        }

        private void GameGridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            try
            {

                if (e.RowIndex >= gameGrid.Length)
                    return;

                RvFile tRvDir = gameGrid[e.RowIndex];

                switch ((GameGridColumns)e.ColumnIndex)
                {
                    case GameGridColumns.CType:
                        {
                            string bitmapNameDat = null;
                            if (tRvDir.DatStatus != DatStatus.NotInDat && tRvDir.DatStatus != DatStatus.InToSort)
                                bitmapNameDat = GetBitmapFromType(tRvDir.FileType, tRvDir.ZipDatStruct);

                            string bitmapName = null;
                            if (tRvDir.GotStatus != GotStatus.NotGot)
                                bitmapName = GetBitmapFromType(tRvDir.FileType, tRvDir.ZipStruct);

                            Bitmap bmp = new Bitmap(GameGrid.Columns[(int)GameGridColumns.CType].Width, 18);

                            int bm0 = -1;
                            int bm1 = -1;
                            int bm2 = -1;
                            if (bitmapNameDat != null && bitmapName != null)
                            {
                                if (bitmapNameDat == bitmapName)
                                {
                                    bm0 = (GameGrid.Columns[(int)GameGridColumns.CType].Width / 2);
                                }
                                else
                                {
                                    bitmapNameDat += "Missing";
                                    bm0 = (GameGrid.Columns[(int)GameGridColumns.CType].Width / 4) * 3;
                                    bm1 = (GameGrid.Columns[(int)GameGridColumns.CType].Width / 4);
                                    bm2 = (GameGrid.Columns[(int)GameGridColumns.CType].Width / 2);
                                }
                            }
                            else if (bitmapNameDat != null)
                            {
                                bm0 = (GameGrid.Columns[(int)GameGridColumns.CType].Width / 2);
                                bitmapNameDat += "Missing";
                            }
                            else if (bitmapName != null)
                            {
                                bm1 = (GameGrid.Columns[(int)GameGridColumns.CType].Width / 2);
                            }

                            if (tRvDir.GotStatus == GotStatus.Corrupt) { bitmapName += "Corrupt"; }

                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                                if (bm0 != -1)
                                {
                                    Bitmap bm = rvImages.GetBitmap(bitmapNameDat, false);
                                    if (bm != null)
                                    {
                                        float xSize = (float)bm.Width / bm.Height * 18;
                                        g.DrawImage(bm, (bm0 - (xSize / 2)), 0, xSize, 18);
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Missing Graphic for " + bitmapNameDat);
                                    }
                                }

                                if (bm1 != -1)
                                {
                                    Bitmap bm = rvImages.GetBitmap(bitmapName, false);
                                    if (bm != null)
                                    {
                                        float xSize = (float)bm.Width / bm.Height * 18;
                                        g.DrawImage(bm, (bm1 - (xSize / 2)), 0, xSize, 18);
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Missing Graphic for " + bitmapName);
                                    }
                                }

                                if (bm2 != -1)
                                {
                                    Bitmap bm = rvImages.GetBitmap(tRvDir.ZipDatStructFix ? "ZipConvert" : "ZipConvert1", false);
                                    float xSize = (float)bm.Width / bm.Height * 18;
                                    g.DrawImage(bm, (bm2 - (xSize / 2)), 0, xSize, 18);
                                }
                            }

                            e.Value = bmp;

                            break;
                        }

                    case GameGridColumns.CGame:
                        if (string.IsNullOrEmpty(tRvDir.FileName))
                        {
                            e.Value = tRvDir.Name;
                        }
                        else
                        {
                            e.Value = tRvDir.Name + " (Found: " + tRvDir.FileName + ")";
                        }

                        break;

                    case GameGridColumns.CDescription:
                        if (tRvDir.Game != null)
                        {
                            string desc = tRvDir.Game.GetData(RvGame.GameData.Description);
                            if (desc == "¤") desc = Path.GetFileNameWithoutExtension(tRvDir.Name);

                            e.Value = desc;
                        }

                        break;

                    case GameGridColumns.CDateTime:
                        e.Value = SetCell(CompressUtils.zipDateTimeToString(tRvDir.FileModTimeStamp), tRvDir, FileStatus.DateFromDAT, 0, 0);
                        break;

                    case GameGridColumns.CRomStatus:
                        {
                            Bitmap bmp = new Bitmap(GameGrid.Columns[(int)GameGridColumns.CRomStatus].Width, 18);
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                //g.Clear((e.RowIndex%2==1)?Dark.dark.bgColor1(Color.White): Dark.dark.bgColor(Color.White));
                                g.Clear(Dark.dark.bgColor1(Color.White));
                                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                                Font drawFont = new Font("Arial", 9);

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
                                    Bitmap bmg = rvImages.GetBitmap(@"G_" + RepairStatus.DisplayOrder[l], false);
                                    if (bmg != null)
                                    {
                                        g.DrawImage(bmg, gOff, 0, 21, 18);
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
                                        g.DrawString(tRvDir.DirStatus.Get(RepairStatus.DisplayOrder[l]).ToString(CultureInfo.InvariantCulture), drawFont, Dark.dark.fgBrush(Brushes.Black), new PointF(gOff + 20, 3));
                                        columnIndex++;
                                    }
                                }

                                drawFont.Dispose();
                            }

                            e.Value = bmp;
                            break;
                        }
                    default:
                        break;
                }
            }
            catch { e.Value = ""; }

        }
        private void GameGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {

                if (_updatingGameGrid)
                {
                    return;
                }

                if (e.ColumnIndex == (int)GameGridColumns.CRomStatus)
                {
                    e.CellStyle.SelectionBackColor = Color.White;
                    return;
                }

                RvFile tRvDir = gameGrid[e.RowIndex];
                ReportStatus tDirStat = tRvDir.DirStatus;

                if (tRvDir.GotStatus == GotStatus.FileLocked)
                {
                    e.CellStyle.BackColor = Dark.dark.Down(_displayColor[(int)RepStatus.UnScanned]);
                    e.CellStyle.ForeColor = _fontColor[(int)RepStatus.UnScanned];
                    return;
                }

                foreach (RepStatus t1 in RepairStatus.DisplayOrder)
                {
                    if (tDirStat.Get(t1) <= 0)
                    {
                        continue;
                    }

                    e.CellStyle.BackColor = Dark.dark.Down(_displayColor[(int)t1]);
                    e.CellStyle.ForeColor = _fontColor[(int)t1];


                    if (e.ColumnIndex <= (int)GameGridColumns.CType)
                    {
                        e.CellStyle.SelectionBackColor = _displayColor[(int)t1];
                    }
                    return;
                }
            }
            catch { }
        }

        private void GameGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (gameGrid == null)
                    return;

                if (GameGrid.Columns[e.ColumnIndex].SortMode == DataGridViewColumnSortMode.NotSortable)
                    return;

                if (gameSortIndex != e.ColumnIndex)
                {
                    if (gameSortIndex >= 0)
                        GameGrid.Columns[gameSortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
                    gameSortIndex = e.ColumnIndex;
                    gameSortDir = SortOrder.Ascending;
                }
                else
                {
                    gameSortDir = gameSortDir == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
                }

                GameGrid.Columns[gameSortIndex].HeaderCell.SortGlyphDirection = gameSortDir;

                Debug.WriteLine($"Sort in {gameSortIndex}  dir {gameSortDir}");

                IComparer<RvFile> tSort = new GameUiCompare((GameGridColumns)gameSortIndex, gameSortDir);
                gameGrid = FastArraySort.SortArray(gameGrid, tSort.Compare);
                GameGrid.Refresh();
                UpdateSelectedGame();
            }
            catch { }
        }

        private class GameUiCompare : IComparer<RvFile>
        {
            private readonly GameGridColumns _colIndex;
            private readonly SortOrder _sortDir;

            public GameUiCompare(GameGridColumns colIndex, SortOrder sortDir)
            {
                _colIndex = colIndex;
                _sortDir = sortDir;
            }

            public int Compare(RvFile x, RvFile y)
            {
                try
                {
                    int retVal = 0;
                    switch (_colIndex)
                    {

                        case GameGridColumns.CGame:
                            retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                            break;

                        case GameGridColumns.CDescription:
                            string descX = x.Game?.GetData(RvGame.GameData.Description) ?? "";
                            string descY = y.Game?.GetData(RvGame.GameData.Description) ?? "";
                            if (descX == "¤") descX = Path.GetFileNameWithoutExtension(x.Name);
                            if (descY == "¤") descY = Path.GetFileNameWithoutExtension(y.Name);

                            retVal = string.Compare(descX, descY, StringComparison.Ordinal);
                            if (retVal != 0)
                                break;
                            retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                            break;

                        case GameGridColumns.CType:
                            retVal = x.FileType - y.FileType;
                            if (retVal != 0)
                                break;
                            retVal = y.ZipStruct - x.ZipStruct;
                            if (retVal != 0)
                                break;
                            retVal = x.RepStatus - y.RepStatus;
                            if (retVal != 0)
                                break;
                            retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                            break;

                        case GameGridColumns.CDateTime:
                            string time1 = CompressUtils.zipDateTimeToString(x.FileModTimeStamp);
                            string time2 = CompressUtils.zipDateTimeToString(y.FileModTimeStamp);
                            retVal = string.Compare(time1 ?? "", time2 ?? "", StringComparison.Ordinal);
                            if (retVal != 0)
                                break;
                            retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                            break;
                    }

                    if (_sortDir == SortOrder.Descending)
                        retVal = -retVal;

                    return retVal;
                }
                catch { return 0; }
            }

        }

        private void GameGridMouseUp(object sender, MouseEventArgs e)
        {
            try
            {

                if (e.Button != MouseButtons.Right)
                    return;

                DataGridView.HitTestInfo hitTest = GameGrid.HitTest(e.X, e.Y);

                int mouseRow = hitTest.RowIndex;
                if (mouseRow < 0)
                    return;


                Point controLocation = ControlLoc(GameGrid);

                if (Control.ModifierKeys == Keys.Shift)
                {
                    _mnuGameGrid.Items.Clear();

                    RvFile thisGame = gameGrid[mouseRow];

                    var item = new ToolStripSeparator();
                    if (thisGame.FileType == FileType.Dir && !_working)
                    {
                        _mnuGameGrid.Items.Add(mnuGameScan2);
                        _mnuGameGrid.Items.Add(mnuGameScan1);
                        _mnuGameGrid.Items.Add(mnuGameScan3);
                        _mnuGameGrid.Items.Insert(3, item);
                    }

                    if (thisGame.Game != null && thisGame.Dat?.GetData(RvDat.DatData.HomePage) == "No-Intro")
                    {
                        string gameId = thisGame.Game.GetData(RvGame.GameData.Id);
                        string datId = thisGame.Dat.GetData(RvDat.DatData.Id);
                        if (!string.IsNullOrWhiteSpace(gameId) && !string.IsNullOrWhiteSpace(datId))
                            _mnuGameGrid.Items.Add(mnuOpenPage);
                    }
                    if (thisGame.Game != null && thisGame.Dat?.GetData(RvDat.DatData.HomePage) == "redump.org")
                    {
                        string gameId = thisGame.Game.GetData(RvGame.GameData.Id);
                        if (!string.IsNullOrWhiteSpace(gameId))
                            _mnuGameGrid.Items.Add(mnuOpenPage);
                    }



                    bool found = false;
                    if (thisGame.FileType == FileType.Dir)
                    {
                        string folderPath = thisGame.FullNameCase;
                        if (Directory.Exists(folderPath))
                        {
                            found = true;
                            mnuOpenDir.Text = "Open Dir";
                            _mnuGameGrid.Items.Add(mnuOpenDir);
                        }
                    }

                    if (thisGame.FileType == FileType.Zip || thisGame.FileType == FileType.SevenZip)
                    {
                        string zipPath = thisGame.FullNameCase;
                        if (File.Exists(zipPath))
                        {
                            found = true;
                            if (thisGame.FileType == FileType.Zip)
                                mnuOpenDir.Text = "Open Zip";

                            if (thisGame.FileType == FileType.SevenZip)
                                mnuOpenDir.Text = "Open 7Zip";
                            _mnuGameGrid.Items.Add(mnuOpenDir);
                        }
                    }

                    {
                        string parentPath = thisGame.Parent.FullName;
                        if (Directory.Exists(parentPath))
                        {
                            found = true;
                            mnuOpenParentDir.Text = "Open Parent";
                            _mnuGameGrid.Items.Add(mnuOpenParentDir);
                        }
                    }

                    if (FindEmulatorInfo(thisGame) != null && found)
                        _mnuGameGrid.Items.Add(mnuLaunchEmulator);

                    if (_mnuGameGrid.Items.Count == 0)
                        return;

                    if (_mnuGameGrid.Items[_mnuGameGrid.Items.Count - 1] == item)
                        _mnuGameGrid.Items.RemoveAt(_mnuGameGrid.Items.Count - 1);

                    _mnuGameGrid.Tag = thisGame;
                    _mnuGameGrid.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));
                    return;
                }

                int mouseColumn = hitTest.ColumnIndex;

                string clipText = null;
                string filename = GameGrid.Rows[mouseRow].Cells[(int)GameGridColumns.CGame].FormattedValue?.ToString() ?? "";
                string description = GameGrid.Rows[mouseRow].Cells[(int)GameGridColumns.CDescription].FormattedValue?.ToString() ?? "";

                RvFile thisGame1 = gameGrid[mouseRow];
                RvDat thisDat = thisGame1.Dat;
                string datDir = "";
                if (thisDat != null)
                    datDir = thisDat.GetData(RvDat.DatData.DatRootFullName);

                switch ((GameGridColumns)mouseColumn)
                {
                    case GameGridColumns.CType:
                        clipText = $"{thisGame1.Name}\n{thisGame1.FullName}\n{datDir}\n"; break;
                    case GameGridColumns.CDateTime:
                    case GameGridColumns.CRomStatus:
                        clipText = $"Name : {filename}\nDesc : {description}\n"; break;
                    case GameGridColumns.CGame: clipText = filename; break;
                    case GameGridColumns.CDescription: clipText = description; break;
                }

                if (string.IsNullOrEmpty(clipText))
                    return;


                Clipboard.Clear();
                Clipboard.SetText(clipText);
            }
            catch { }
            return;
        }

        private void MnuGameScan(object sender, EventArgs e)
        {
            if (_working)
                return;
            RvFile thisFile = (RvFile)_mnuGameGrid.Tag;
            ScanRoms((EScanLevel)((ToolStripMenuItem)sender).Tag, thisFile);
        }

        private void MnuOpenDir(object sender, EventArgs e)
        {
            RvFile thisFile = (RvFile)_mnuGameGrid.Tag;
            if (thisFile.FileType == FileType.Dir)
            {
                string folderPath = thisFile.FullNameCase;
                if (Directory.Exists(folderPath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        Arguments = folderPath,
                        FileName = "explorer.exe"
                    };
                    try { Process.Start(startInfo); } catch { }
                }
                return;
            }
            if (thisFile.FileType == FileType.Zip || thisFile.FileType == FileType.SevenZip)
            {
                string zipPath = thisFile.FullNameCase;
                if (File.Exists(zipPath))
                {
                    try { Process.Start(zipPath); } catch { }
                }
                return;
            }
        }

        private void MnuOpenParentDir(object sender, EventArgs e)
        {
            RvFile thisFile = (RvFile)_mnuGameGrid.Tag;
            thisFile = thisFile.Parent;
            if (thisFile == null)
                return;
            if (thisFile.FileType == FileType.Dir)
            {
                string folderPath = thisFile.FullNameCase;
                if (Directory.Exists(folderPath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        Arguments = folderPath,
                        FileName = "explorer.exe"
                    };
                    try { Process.Start(startInfo); } catch { }
                }
                return;
            }
        }





        private void LaunchEmulator(object sender, EventArgs e)
        {
            RvFile tGame = _mnuGameGrid.Tag as RvFile;
            if (tGame != null)
                LaunchEmulator(tGame);
        }
        private EmulatorInfo FindEmulatorInfo(RvFile tGame)
        {
            string path = tGame.Parent.DatTreeFullName;
            if (Settings.rvSettings?.EInfo == null)
                return null;
            if (path == "Error")
                return null;
            if (path.Length <= 8)
                return null;

            foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
            {
                if (!string.Equals(path.Substring(8), ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(ei.CommandLine))
                    continue;

                if (!File.Exists(ei.ExeName))
                    continue;
                return ei;
            }
            return null;
        }


        private void OpenWebPage(object sender, EventArgs e)
        {
            RvFile thisGame = ((RvFile)_mnuGameGrid.Tag);

            if (thisGame.Game != null && thisGame.Dat?.GetData(RvDat.DatData.HomePage) == "No-Intro")
            {
                string gameId = thisGame.Game.GetData(RvGame.GameData.Id);
                string datId = thisGame.Dat.GetData(RvDat.DatData.Id);
                if (!string.IsNullOrWhiteSpace(gameId) && !string.IsNullOrWhiteSpace(datId))
                    try { Process.Start($"https://datomatic.no-intro.org/index.php?page=show_record&s={datId}&n={gameId}"); } catch { }
            }
            if (thisGame.Game != null && thisGame.Dat?.GetData(RvDat.DatData.HomePage) == "redump.org")
            {
                string gameId = thisGame.Game.GetData(RvGame.GameData.Id);
                if (!string.IsNullOrWhiteSpace(gameId))
                    try { Process.Start($"http://redump.org/disc/{gameId}/"); } catch { }
            }
        }



        private void LaunchEmulator(RvFile tGame)
        {
            EmulatorInfo ei = FindEmulatorInfo(tGame);
            if (ei == null)
                return;

            string commandLineOptions = ei.CommandLine;
            string dirname = tGame.Parent.FullName;
            if (dirname.StartsWith("RomRoot\\"))
                dirname = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), dirname);

            commandLineOptions = commandLineOptions.Replace("{gamename}", Path.GetFileNameWithoutExtension(tGame.Name));
            commandLineOptions = commandLineOptions.Replace("{gamefilename}", tGame.Name);
            commandLineOptions = commandLineOptions.Replace("{gamedirectory}", dirname);

            string workingDir = ei.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(workingDir))
                workingDir = Path.GetDirectoryName(ei.ExeName);

            using (Process exeProcess = new Process())
            {
                exeProcess.StartInfo.WorkingDirectory = workingDir;
                exeProcess.StartInfo.FileName = ei.ExeName;
                exeProcess.StartInfo.Arguments = commandLineOptions;
                exeProcess.StartInfo.UseShellExecute = false;
                exeProcess.StartInfo.CreateNoWindow = true;
                exeProcess.Start();
            }
        }

        private void GameGridMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_updatingGameGrid)
                return;


            if (e.Button == MouseButtons.Right)
            {
                RvFile tParent = gameGridSource?.Parent;
                if (tParent == null)
                    return;
                UpdateGameGrid(tParent);
                ctrRvTree.SetSelected(tParent);

                UpdateDatMetaData(tParent);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;


            if (GameGrid.SelectedRows.Count != 1)
                return;

            RvFile tGame = gameGrid[GameGrid.SelectedRows[0].Index];
            if (tGame.Game == null && tGame.FileType == FileType.Dir)
            {
                UpdateGameGrid(tGame);
                ctrRvTree.SetSelected(tGame);

                UpdateDatMetaData(tGame);
            }
            else
            {
                LaunchEmulator(tGame);
            }
        }

    }
}
