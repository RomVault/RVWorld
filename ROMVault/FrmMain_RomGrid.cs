using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FileHeaderReader;
using RVCore;
using RVCore.RvDB;
using RVCore.Utils;

namespace ROMVault
{
    public partial class FrmMain
    {
        private RvFile[] gridFiles;
        private int sortIndex = -1;
        private SortOrder sortDir = SortOrder.None;
     
        private void UpdateRomGrid(RvFile tGame)
        {
            if (Settings.IsMono && RomGrid.RowCount > 0)
            {
                RomGrid.CurrentCell = RomGrid[0, 0];
            }

            if (sortIndex != -1)
                RomGrid.Columns[sortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;

            sortIndex = -1;
            sortDir = SortOrder.None;

            RomGrid.Rows.Clear();
            List<RvFile> fileList = new List<RvFile>();
            AddDir(tGame, "", ref fileList);

            gridFiles = fileList.ToArray();
            RomGrid.RowCount = gridFiles.Length;
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
                tFile.UiDisplayName = pathAdd + tFile.Name;
                fileList.Add(tFile);
            }
        }

        private void RomGridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            RvFile tFile = gridFiles[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: //CGot
                    Bitmap bmp = new Bitmap(54, 18);
                    Graphics g = Graphics.FromImage(bmp);
                    string bitmapName = "R_" + tFile.DatStatus + "_" + tFile.RepStatus;
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

                    g.Dispose();
                    break;
                case 1: //CRom
                    string fname = tFile.UiDisplayName;
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

                    e.Value = fname;

                    break;
                case 2: //CMerge
                    e.Value = tFile.Merge;
                    break;
                case 3: //CSize
                    e.Value = SetCell(tFile.Size.ToString(), tFile, FileStatus.SizeFromDAT, FileStatus.SizeFromHeader, FileStatus.SizeVerified);
                    break;
                case 4: //CCRC32
                    e.Value = SetCell(tFile.CRC.ToHexString(), tFile, FileStatus.CRCFromDAT, FileStatus.CRCFromHeader, FileStatus.CRCVerified);
                    break;
                case 5: //CSHA1
                    e.Value = SetCell(tFile.SHA1.ToHexString(), tFile, FileStatus.SHA1FromDAT, FileStatus.SHA1FromHeader, FileStatus.SHA1Verified);
                    break;
                case 6: //CMD5
                    e.Value = SetCell(tFile.MD5.ToHexString(), tFile, FileStatus.MD5FromDAT, FileStatus.MD5FromHeader, FileStatus.MD5Verified);
                    break;
                case 7: //CAltSize
                    e.Value = SetCell(tFile.AltSize.ToString(), tFile, FileStatus.AltSizeFromDAT, FileStatus.AltSizeFromHeader, FileStatus.AltSizeVerified);
                    break;
                case 8: //CAltCRC32
                    e.Value = SetCell(tFile.AltCRC.ToHexString(), tFile, FileStatus.AltCRCFromDAT, FileStatus.AltCRCFromHeader, FileStatus.AltCRCVerified);
                    break;
                case 9: //CAltSHA1
                    e.Value = SetCell(tFile.AltSHA1.ToHexString(), tFile, FileStatus.AltSHA1FromDAT, FileStatus.AltSHA1FromHeader, FileStatus.AltSHA1Verified);
                    break;
                case 10: //CAltMD5
                    e.Value = SetCell(tFile.AltMD5.ToHexString(), tFile, FileStatus.AltMD5FromDAT, FileStatus.AltMD5FromHeader, FileStatus.AltMD5Verified);
                    break;
                case 11: //CStatus
                    e.Value = tFile.Status;
                    break;
                case 12: // ZipIndex
                    if (tFile.FileType == FileType.ZipFile)
                        e.Value = tFile.ZipFileIndex == -1 ? "" : tFile.ZipFileIndex.ToString();
                    break;
                case 13: // ZipHeader
                    if (tFile.FileType == FileType.ZipFile)
                        e.Value = tFile.ZipFileHeaderPosition == null ? "" : tFile.ZipFileHeaderPosition.ToString();
                    break;
            }
        }

        private static string SetCell(string txt, RvFile tRomTable, FileStatus dat, FileStatus file, FileStatus verified)
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

            return txt + flags;
        }

        private void RomGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            RvFile tFile = gridFiles[e.RowIndex];
            e.CellStyle.BackColor = _displayColor[(int)tFile.RepStatus];
            e.CellStyle.ForeColor = _fontColor[(int)tFile.RepStatus];
        }


        private void RomGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (gridFiles==null)
                return;
            
            if (RomGrid.Columns[e.ColumnIndex].SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

            if (sortIndex != e.ColumnIndex)
            {
                if (sortIndex >= 0)
                    RomGrid.Columns[sortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
                sortIndex = e.ColumnIndex;
                sortDir = SortOrder.Ascending;
            }
            else
            {
                sortDir = sortDir == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }

            RomGrid.Columns[sortIndex].HeaderCell.SortGlyphDirection = sortDir;

            Debug.WriteLine($"Sort in {sortIndex}  dir {sortDir}");

            IComparer<RvFile> t = new RomUiCompare(e.ColumnIndex, sortDir);

            Array.Sort(gridFiles, t);
            RomGrid.Refresh();
        }

        private class RomUiCompare : IComparer<RvFile>
        {
            private readonly int _colIndex;
            private readonly SortOrder _sortDir;

            public RomUiCompare(int colIndex, SortOrder sortDir)
            {
                _colIndex = colIndex;
                _sortDir = sortDir;
            }

            public int Compare(RvFile x, RvFile y)
            {
                int retVal = 0;
                switch (_colIndex)
                {
                    case 1:
                        retVal = string.Compare(x.UiDisplayName ?? "", y.UiDisplayName ?? "", StringComparison.Ordinal);
                        break;
                    case 2:
                        retVal = string.Compare(x.Merge ?? "", y.Merge ?? "", StringComparison.Ordinal);
                        break;
                    case 3:
                        retVal = ULong.iCompareNull(x.Size, y.Size);
                        break;
                    case 4:
                        retVal = ArrByte.ICompare(x.CRC, y.CRC);
                        break;
                    case 5:
                        retVal = ArrByte.ICompare(x.SHA1, y.SHA1);
                        break;
                    case 6:
                        retVal = ArrByte.ICompare(x.MD5, y.MD5);
                        break;
                    case 7:
                        retVal = ULong.iCompareNull(x.AltSize, y.AltSize);
                        break;
                    case 8:
                        retVal = ArrByte.ICompare(x.AltCRC, y.AltCRC);
                        break;
                    case 9:
                        retVal = ArrByte.ICompare(x.AltSHA1, y.AltSHA1);
                        break;
                    case 10:
                        retVal = ArrByte.ICompare(x.AltMD5, y.AltMD5);
                        break;
                    case 11:
                        retVal = string.Compare(x.Status ?? "", y.Status ?? "", StringComparison.Ordinal);
                        break;
                }
                
                if (_sortDir == SortOrder.Descending)
                    retVal = -retVal;

                if (retVal == 0 && _colIndex != 1)
                    retVal = string.Compare(x.UiDisplayName ?? "", y.UiDisplayName ?? "", StringComparison.Ordinal);

                return retVal;
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
