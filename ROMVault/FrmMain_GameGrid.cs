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

namespace ROMVault
{
    public partial class FrmMain
    {
        private RvFile gameGridSource;

        private RvFile[] gameGrid;

        private int gameSortIndex = -1;
        private SortOrder gameSortDir = SortOrder.None;


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
            gameSortIndex = 0;
            gameSortDir = SortOrder.Descending;

        }

        private void UpdateGameGrid(RvFile tDir)
        {
            gameGridSource = tDir;
            _updatingGameGrid = true;

            ClearGameGrid();
            UpdateGameGrid();

        }

        private void UpdateGameGrid()
        {
            if (gameGridSource == null)
                return;

            _updatingGameGrid = true;
            
            List<RvFile> gameList = new List<RvFile>();

            _gameGridColumnXPositions = new int[(int)RepStatus.EndValue];

            string searchLowerCase = txtFilter.Text.ToLower();
            for (int j = 0; j < gameGridSource.ChildCount; j++)
            {
                RvFile tChildDir = gameGridSource.Child(j);
                if (!tChildDir.IsDir)
                {
                    continue;
                }

                if (txtFilter.Text.Length > 0 && !tChildDir.Name.ToLower().Contains(searchLowerCase))
                {
                    continue;
                }

                ReportStatus tDirStat = tChildDir.DirStatus;

                bool gCorrect = tDirStat.HasCorrect();
                bool gMissing = tDirStat.HasMissing();
                bool gUnknown = tDirStat.HasUnknown();
                bool gInToSort = tDirStat.HasInToSort();
                bool gFixes = tDirStat.HasFixesNeeded();
                bool gAllMerged = tDirStat.HasAllMerged();

                bool show = chkBoxShowCorrect.Checked && gCorrect && !gMissing && !gFixes;
                show = show || chkBoxShowMissing.Checked && gMissing;
                show = show || chkBoxShowFixed.Checked && gFixes;
                show = show || chkBoxShowMerged.Checked && gAllMerged;
                show = show || gUnknown;
                show = show || gInToSort;
                show = show || tChildDir.GotStatus == GotStatus.Corrupt;
                show = show || !(gCorrect || gMissing || gUnknown || gInToSort || gFixes || gAllMerged);

                if (!show)
                {
                    continue;
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

            _updatingGameGrid = false;
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

        private void UpdateSelectedGame()
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
            UpdateRomGrid(tGame);
        }


        private void GameGridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= gameGrid.Length)
                return;

            RvFile tRvDir = gameGrid[e.RowIndex];
            
            switch (e.ColumnIndex)
            {
                case 0: // CType
                    {
                        string bitmapName;
                        switch (tRvDir.FileType)
                        {
                            case FileType.Zip:
                                bitmapName = "Zip";
                                if (tRvDir.ZipStatus == ZipStatus.TrrntZip) { bitmapName += "TZ"; }
                                if (tRvDir.GotStatus == GotStatus.Corrupt) { bitmapName += "Corrupt"; }
                                if (tRvDir.RepStatus == RepStatus.DirMissing) { bitmapName += "Missing"; }

                                break;
                            case FileType.SevenZip:
                                bitmapName = "SevenZip";
                                if (tRvDir.ZipStatus == ZipStatus.TrrntZip) { bitmapName += "TZ"; }
                                if (tRvDir.ZipStatus == ZipStatus.Trrnt7Zip) { bitmapName += "T7Z"; }
                                if (tRvDir.GotStatus == GotStatus.Corrupt) { bitmapName += "Corrupt"; }
                                if (tRvDir.RepStatus == RepStatus.DirMissing) { bitmapName += "Missing"; }

                                break;
                            default:
                                bitmapName = "Dir";
                                // hack because DirDirInToSort image doesnt exist.
                                if (tRvDir.RepStatus == RepStatus.DirMissing) { bitmapName += "Missing"; }

                                break;
                        }

                        Bitmap bmp = new Bitmap(GameGrid.Columns[0].Width, 18);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            Bitmap bm = rvImages.GetBitmap(bitmapName);
                            if (bm != null)
                            {
                                float xSize = (float)bm.Width / bm.Height * (18 - 1);

                                g.DrawImage(bm, (GameGrid.Columns[0].Width - xSize) / 2, 0, xSize, 18 - 1);
                                bm.Dispose();
                            }
                            else
                            {
                                Debug.WriteLine("Missing Graphic for " + bitmapName);
                            }
                        }
                        e.Value = bmp;

                        break;
                    }
                case 1: // CName
                    if (string.IsNullOrEmpty(tRvDir.FileName))
                    {
                        e.Value = tRvDir.Name;
                    }
                    else
                    {
                        e.Value = tRvDir.Name + " (Found: " + tRvDir.FileName + ")";
                    }

                    break;

                case 2: // CDescription
                    if (tRvDir.Game != null)
                    {
                        e.Value = tRvDir.Game.GetData(RvGame.GameData.Description);
					} else if (tRvDir.Dat != null) {
						e.Value = tRvDir.Dat.GetData(RvDat.DatData.Description);
					}

					break;
                case 3: // CCorrect
                    {
                        Bitmap bmp = new Bitmap(GameGrid.Columns[3].Width, 18);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
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
                                Bitmap bmg = rvImages.GetBitmap(@"G_" + RepairStatus.DisplayOrder[l]);
                                if (bmg != null)
                                {
                                    g.DrawImage(bmg, gOff, 0, 21, 18);
                                    bmg.Dispose();
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
                                    g.DrawString(tRvDir.DirStatus.Get(RepairStatus.DisplayOrder[l]).ToString(CultureInfo.InvariantCulture), drawFont, drawBrushBlack, new PointF(gOff + 20, 3));
                                    columnIndex++;
                                }
                            }

                            drawBrushBlack.Dispose();
                            drawFont.Dispose();
                        }

                        e.Value = bmp;
                        break;
                    }
                default:
                    Console.WriteLine(
                        $@"WARN: GameGridCellFormatting() unknown column: {GameGrid.Columns[e.ColumnIndex].Name}");
                    break;
            }


        }

        private void GameGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_updatingGameGrid)
            {
                return;
            }

            if (e.ColumnIndex == 3)
            {
                e.CellStyle.SelectionBackColor = Color.White;
                return;
            }

            RvFile tRvDir = gameGrid[e.RowIndex];
            ReportStatus tDirStat = tRvDir.DirStatus;

            foreach (RepStatus t1 in RepairStatus.DisplayOrder)
            {
                if (tDirStat.Get(t1) <= 0)
                {
                    continue;
                }

                e.CellStyle.BackColor = _displayColor[(int)t1];
                e.CellStyle.ForeColor = _fontColor[(int)t1];


                if (e.ColumnIndex == 0)
                {
                    e.CellStyle.SelectionBackColor = _displayColor[(int)t1];
                }
                return;
            }
        }

        private void GameGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
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

            IComparer<RvFile> t = new GameUiCompare(e.ColumnIndex, gameSortDir);

            Array.Sort(gameGrid, t);
            GameGrid.Refresh();
            UpdateSelectedGame();
        }

        private class GameUiCompare : IComparer<RvFile>
        {
            private readonly int _colIndex;
            private readonly SortOrder _sortDir;

            public GameUiCompare(int colIndex, SortOrder sortDir)
            {
                _colIndex = colIndex;
                _sortDir = sortDir;
            }

            public int Compare(RvFile x, RvFile y)
            {
                int retVal = 0;
                switch (_colIndex)
                {
                    case 0:
                        retVal = x.FileType - y.FileType;
                        if (retVal != 0)
                            break;
                        retVal = y.ZipStatus - x.ZipStatus;
                        if (retVal != 0)
                            break;
                        retVal = x.RepStatus - y.RepStatus;
                        if (retVal != 0)
                            break;
                        retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                        break;

                    case 1:
                        retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                        break;
                    case 2:
                        retVal = string.Compare(x.Game?.GetData(RvGame.GameData.Description) ?? "", y.Game?.GetData(RvGame.GameData.Description) ?? "", StringComparison.Ordinal);
                        if (retVal == 0)
                            retVal = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                        break;
                }

                if (_sortDir == SortOrder.Descending)
                    retVal = -retVal;

                return retVal;
            }

        }

        private void GameGridMouseUp(object sender, MouseEventArgs e)
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



        private void GameGridMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_updatingGameGrid)
            {
                return;
            }


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
            if (GameGrid.SelectedRows.Count != 1)
            {
                return;
            }

            RvFile tGame = gameGrid[GameGrid.SelectedRows[0].Index];
            if (tGame.Game == null && tGame.FileType == FileType.Dir)
            {
                UpdateGameGrid(tGame);
                ctrRvTree.SetSelected(tGame);

                UpdateDatMetaData(tGame);
            }
            else
            {
                string path = tGame.Parent.DatTreeFullName;
                if (Settings.rvSettings?.EInfo == null)
                    return;

                foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
                {
                    if (!string.Equals(path.Substring(8), ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(ei.CommandLine))
                        continue;

                    string commandLineOptions = ei.CommandLine;
                    string dirname = tGame.Parent.FullName;
                    if (dirname.StartsWith("RomRoot\\"))
                        dirname = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), dirname);
                   
                    commandLineOptions = commandLineOptions.Replace("{gamename}", Path.GetFileNameWithoutExtension(tGame.Name));
                    commandLineOptions = commandLineOptions.Replace("{gamefilename}", tGame.Name);
                    commandLineOptions = commandLineOptions.Replace("{gamedirectory}", dirname);

                    if (!File.Exists(ei.ExeName))
                        continue;

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

                    return;
                }
            }
        }

    }
}
