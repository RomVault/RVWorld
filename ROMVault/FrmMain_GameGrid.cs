using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using Compress;
using RVCore;
using RVCore.RvDB;
using RVIO;

namespace ROMVault
{
    public partial class FrmMain
    {
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

            UpdateGameMetaData(tDir);
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

            UpdateGameMetaData(tGame);
            UpdateRomGrid(tGame);
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
                string path = tGame.Parent.DatTreeFullName;
                if (Settings.rvSettings?.EInfo == null)
                    return;

                foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
                {
                    if (!string.Equals(path, ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(ei.CommandLine))
                        continue;

                    string commandLineOptions = ei.CommandLine;
                    string dirname = tGame.Parent.FullName;
                    commandLineOptions = commandLineOptions.Replace("{gamename}", Path.GetFileNameWithoutExtension(tGame.Name));
                    commandLineOptions = commandLineOptions.Replace("{gamefilename}", tGame.Name);
                    commandLineOptions = commandLineOptions.Replace("{gamedirectory}", dirname);
                    using (Process exeProcess = new Process())
                    {
                        exeProcess.StartInfo.WorkingDirectory = ei.WorkingDirectory;
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

    }
}
