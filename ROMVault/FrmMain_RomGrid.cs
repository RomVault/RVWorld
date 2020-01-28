using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FileHeaderReader;
using RVCore;
using RVCore.RvDB;
using RVCore.Utils;

namespace ROMVault
{
    public partial class FrmMain
    {
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

            if (Settings.IsMono && RomGrid.RowCount > 0)
            {
                RomGrid.CurrentCell = RomGrid[0, 0];
            }

            RomGrid.Rows.Clear();
            List<RvFile> fileList = new List<RvFile>();
            AddDir(tGame, "", ref fileList);
            //AddDir(tGame, "");

            gridFiles = fileList.ToArray();
            GC.Collect();

        }

        private RvFile[] gridFiles;

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
                    bool found = false;
                    string path = tGame.Parent.DatTreeFullName;
                    foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
                    {
                        if (!string.Equals(path, ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                            continue;

                        if (string.IsNullOrWhiteSpace(ei.ExtraPath))
                            continue;

                        found = true;
                        LoadMamePannels(tGame, ei.ExtraPath);
                    }

                    if (!found)
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
        }


        private void AddDir(RvFile tGame, string pathAdd, ref List<RvFile> fileList)
        {
            for (int l = 0; l < tGame.ChildCount; l++)
            {
                RvFile tBase = tGame.Child(l);

                RvFile tFile = tBase;
                if (tFile.IsFile)
                {
                    AddRom(tFile, pathAdd, ref fileList);
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
                    AddDir(tDir, pathAdd + tGame.Name + "/", ref fileList);
                }
            }
        }

        private void AddRom(RvFile tFile, string pathAdd, ref List<RvFile> fileList)
        {
            if (tFile.DatStatus != DatStatus.InDatMerged || tFile.RepStatus != RepStatus.NotCollected ||
                chkBoxShowMerged.Checked)
            {
                fileList.Add(tFile);
            }
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
                    AddDir(tDir, pathAdd + tBase.Name + "\\");
                }
            }
        }


        private void AddRom(RvFile tFile, string pathAdd)
        {
            if (tFile.DatStatus != DatStatus.InDatMerged || tFile.RepStatus != RepStatus.NotCollected || chkBoxShowMerged.Checked)
            {
                RomGrid.Rows.Add();
                int row = RomGrid.Rows.Count - 1;
                RomGrid.Rows[row].Tag = tFile;

                for (int i = 0; i < RomGrid.Rows[row].Cells.Count; i++)
                {
                    DataGridViewCellStyle cs = RomGrid.Rows[row].Cells[i].Style;
                    cs.BackColor = _displayColor[(int)tFile.RepStatus];
                    cs.ForeColor = _fontColor[(int)tFile.RepStatus];
                }

                string fname = pathAdd + tFile.Name;
                if (!string.IsNullOrEmpty(tFile.FileName))
                {
                    fname += " (Found: " + tFile.FileName + ")";
                }

                if (tFile.CHDVersion != null)
                {
                    fname += " (V" + tFile.CHDVersion + ")";
                }

                if (tFile.HeaderFileType != HeaderFileType.Nothing)
                {
                    fname += " (" + tFile.HeaderFileType + ")";
                }


                RomGrid.Rows[row].Cells["CRom"].Value = fname;

                RomGrid.Rows[row].Cells["CMerge"].Value = tFile.Merge;
                RomGrid.Rows[row].Cells["CStatus"].Value = tFile.Status;

                SetCell(RomGrid.Rows[row].Cells["CSize"], tFile.Size.ToString(), tFile, FileStatus.SizeFromDAT, FileStatus.SizeFromHeader, FileStatus.SizeVerified);
                SetCell(RomGrid.Rows[row].Cells["CCRC32"], tFile.CRC.ToHexString(), tFile, FileStatus.CRCFromDAT, FileStatus.CRCFromHeader, FileStatus.CRCVerified);
                SetCell(RomGrid.Rows[row].Cells["CSHA1"], tFile.SHA1.ToHexString(), tFile, FileStatus.SHA1FromDAT, FileStatus.SHA1FromHeader, FileStatus.SHA1Verified);
                SetCell(RomGrid.Rows[row].Cells["CMD5"], tFile.MD5.ToHexString(), tFile, FileStatus.MD5FromDAT, FileStatus.MD5FromHeader, FileStatus.MD5Verified);

                SetCell(RomGrid.Rows[row].Cells["CAltSize"], tFile.AltSize.ToString(), tFile, FileStatus.AltSizeFromDAT, FileStatus.AltSizeFromHeader, FileStatus.AltSizeVerified);
                SetCell(RomGrid.Rows[row].Cells["CAltCRC32"], tFile.AltCRC.ToHexString(), tFile, FileStatus.AltCRCFromDAT, FileStatus.AltCRCFromHeader, FileStatus.AltCRCVerified);
                SetCell(RomGrid.Rows[row].Cells["CAltSHA1"], tFile.AltSHA1.ToHexString(), tFile, FileStatus.AltSHA1FromDAT, FileStatus.AltSHA1FromHeader, FileStatus.AltSHA1Verified);
                SetCell(RomGrid.Rows[row].Cells["CAltMD5"], tFile.AltMD5.ToHexString(), tFile, FileStatus.AltMD5FromDAT, FileStatus.AltMD5FromHeader, FileStatus.AltMD5Verified);

                if (tFile.FileType == FileType.ZipFile)
                {
                    RomGrid.Rows[row].Cells["ZipIndex"].Value = tFile.ZipFileIndex == -1
                        ? ""
                        : tFile.ZipFileIndex.ToString(CultureInfo.InvariantCulture);
                    RomGrid.Rows[row].Cells["ZipHeader"].Value = tFile.ZipFileHeaderPosition == null
                        ? ""
                        : tFile.ZipFileHeaderPosition.ToString();
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

        private void RomGridSelectionChanged(object sender, EventArgs e)
        {
            RomGrid.ClearSelection();
        }

    }
}
