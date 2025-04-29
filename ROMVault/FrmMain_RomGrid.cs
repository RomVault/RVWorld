using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Compress;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

namespace ROMVault
{
    public enum eRomGrid
    {
        Got = 0,
        Rom = 1,
        Merge = 2,
        Size = 3,
        CRC32 = 4,
        SHA1 = 5,
        MD5 = 6,
        AltSize = 7,
        AltCRC32 = 8,
        AltSHA1 = 9,
        AltMD5 = 10,
        Status = 11,
        DateModFile = 12,
        ZipIndex = 13,
        DupeCount = 14

    }

    public partial class FrmMain
    {
        private bool altFound = false;
        private RvFile[] romGrid;
        private int romSortIndex = -1;
        private SortOrder romSortDir = SortOrder.None;

        private bool showStatus;
        private bool showFileModDate;


        private void UpdateRomGrid(RvFile tGame, bool onTimer = false)
        {
            int scrollPosition = -1;
            try
            {
                scrollPosition = RomGrid.FirstDisplayedScrollingRowIndex;
            }
            catch { }


            if (Settings.IsMono && RomGrid.RowCount > 0)
            {
                RomGrid.CurrentCell = RomGrid[0, 0];
            }

            if (romSortIndex != -1)
                RomGrid.Columns[romSortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;

            romSortIndex = -1;
            romSortDir = SortOrder.None;

            RomGrid.Rows.Clear();

            altFound = false;
            showStatus = false;
            showFileModDate = false;

            List<RvFile> fileList = new List<RvFile>();
            AddDir(tGame, "", ref fileList);
            romGrid = fileList.ToArray();

            RomGrid.Columns[(int)eRomGrid.AltSize].Visible = altFound;
            RomGrid.Columns[(int)eRomGrid.AltCRC32].Visible = altFound;
            RomGrid.Columns[(int)eRomGrid.AltSHA1].Visible = altFound;
            RomGrid.Columns[(int)eRomGrid.AltMD5].Visible = altFound;

            RomGrid.Columns[(int)eRomGrid.Status].Visible = showStatus;
            RomGrid.Columns[(int)eRomGrid.DateModFile].Visible = showFileModDate;

            RomGrid.RowCount = romGrid.Length;

            try
            {
                if (onTimer && scrollPosition >= 0 && scrollPosition <= RomGrid.RowCount)
                    RomGrid.FirstDisplayedScrollingRowIndex = scrollPosition;
            }
            catch { }
        }

        private void AddDir(RvFile tGame, string pathAdd, ref List<RvFile> fileList)
        {
            if (tGame == null)
                return;

            try
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
                    if (!tDir.IsDirectory)
                    {
                        continue;
                    }

                    if (tDir.Game == null)
                    {
                        AddDir(tDir, pathAdd + tDir.Name + "/", ref fileList);
                    }
                }
            }
            catch { }
        }

        private void AddRom(RvFile tFile, string pathAdd, ref List<RvFile> fileList)
        {
            try
            {

                if (tFile.DatStatus != DatStatus.InDatMerged || tFile.RepStatus != RepStatus.NotCollected ||
                chkBoxShowMerged.Checked)
                {
                    tFile.UiDisplayName = pathAdd + tFile.Name;
                    fileList.Add(tFile);
                    if (!altFound)
                    {
                        altFound = (tFile.AltSize != null) || (tFile.AltCRC != null) || (tFile.AltSHA1 != null) || (tFile.AltMD5 != null);
                    }
                    showStatus |= !string.IsNullOrWhiteSpace(tFile.Status);

                    showFileModDate |=
                        (tFile.FileModTimeStamp != 0) &&
                        (tFile.FileModTimeStamp != long.MinValue) &&
                        (tFile.FileModTimeStamp != Compress.StructuredZip.StructuredZip.TrrntzipDateTime) &&
                        (tFile.FileModTimeStamp != Compress.StructuredZip.StructuredZip.TrrntzipDosDateTime);
                }
            }
            catch { }
        }

        private void RomGridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                RvFile tFile = romGrid[e.RowIndex];
                switch ((eRomGrid)e.ColumnIndex)
                {
                    case eRomGrid.Got:
                        Bitmap bmp = new Bitmap(54, 18);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                            string bitmapName = "R_" + tFile.DatStatus + "_" + tFile.RepStatus;
                            Bitmap romIcon = rvImages.GetBitmap(bitmapName, false);
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
                        break;
                    case eRomGrid.Rom:
                        string fname = tFile.UiDisplayName;
                        if (!string.IsNullOrEmpty(tFile.FileName))
                        {
                            fname += " (Found: " + tFile.FileName + ")";
                        }

                        if (tFile.CHDVersion != null)
                        {
                            fname += " (V" + tFile.CHDVersion + ")";
                        }

                        string D = tFile.FileStatusIs(FileStatus.HeaderFileTypeFromDAT) ? "D" : "";
                        string F = tFile.FileStatusIs(FileStatus.HeaderFileTypeFromHeader) ? "F" : "";
                        if (tFile.HeaderFileType != HeaderFileType.Nothing || !string.IsNullOrWhiteSpace(D) || !string.IsNullOrWhiteSpace(F))
                        {
                            string req = tFile.HeaderFileTypeRequired ? ",Required" : "";
                            fname += $" ({tFile.HeaderFileType}{req} {D}{F})";
                        }

                        e.Value = fname;

                        break;
                    case eRomGrid.Merge:
                        e.Value = tFile.Merge;
                        break;
                    case eRomGrid.Size:
                        e.Value = SetCell(tFile.Size == null ? "" : ((ulong)tFile.Size).ToString("N0"), tFile, FileStatus.SizeFromDAT, FileStatus.SizeFromHeader, FileStatus.SizeVerified);
                        break;
                    case eRomGrid.CRC32:
                        e.Value = SetCell(tFile.CRC.ToHexString(), tFile, FileStatus.CRCFromDAT, FileStatus.CRCFromHeader, FileStatus.CRCVerified);
                        break;
                    case eRomGrid.SHA1:
                        e.Value = SetCell(tFile.SHA1.ToHexString(), tFile, FileStatus.SHA1FromDAT, FileStatus.SHA1FromHeader, FileStatus.SHA1Verified);
                        break;
                    case eRomGrid.MD5:
                        e.Value = SetCell(tFile.MD5.ToHexString(), tFile, FileStatus.MD5FromDAT, FileStatus.MD5FromHeader, FileStatus.MD5Verified);
                        break;
                    case eRomGrid.AltSize:
                        e.Value = SetCell(tFile.AltSize == null ? "" : ((ulong)tFile.AltSize).ToString("N0"), tFile, FileStatus.AltSizeFromDAT, FileStatus.AltSizeFromHeader, FileStatus.AltSizeVerified);
                        break;
                    case eRomGrid.AltCRC32:
                        e.Value = SetCell(tFile.AltCRC.ToHexString(), tFile, FileStatus.AltCRCFromDAT, FileStatus.AltCRCFromHeader, FileStatus.AltCRCVerified);
                        break;
                    case eRomGrid.AltSHA1:
                        e.Value = SetCell(tFile.AltSHA1.ToHexString(), tFile, FileStatus.AltSHA1FromDAT, FileStatus.AltSHA1FromHeader, FileStatus.AltSHA1Verified);
                        break;
                    case eRomGrid.AltMD5:
                        e.Value = SetCell(tFile.AltMD5.ToHexString(), tFile, FileStatus.AltMD5FromDAT, FileStatus.AltMD5FromHeader, FileStatus.AltMD5Verified);
                        break;
                    case eRomGrid.Status:
                        e.Value = tFile.Status;
                        break;
                    case eRomGrid.DateModFile:
                        {
                            if (tFile.FileModTimeStamp == 0 || tFile.FileModTimeStamp == long.MinValue)
                                break;
                            if (tFile.FileModTimeStamp == Compress.StructuredZip.StructuredZip.TrrntzipDateTime || tFile.FileModTimeStamp == Compress.StructuredZip.StructuredZip.TrrntzipDosDateTime)
                            {
                                e.Value = SetCell("Trrntziped", tFile, FileStatus.DateFromDAT, 0, 0);
                                break;
                            }
                            e.Value = SetCell(CompressUtils.zipDateTimeToString(tFile.FileModTimeStamp), tFile, FileStatus.DateFromDAT, 0, 0);
                            break;
                        }

                    case eRomGrid.ZipIndex:
                        if (tFile.FileType == FileType.FileZip)
                            e.Value = tFile.ZipFileIndex == -1 ? "" : tFile.ZipFileIndex.ToString();
                        break;
                    case eRomGrid.DupeCount:
                        if (tFile.FileGroup != null)
                        {
                            e.Value = tFile.FileGroup.Files.Count.ToString();
                        }
                        break;

                }
            }
            catch
            {
                e.Value = "";
            }
        }

        private static string SetCell(string txt, RvFile tRomTable, FileStatus dat, FileStatus file, FileStatus verified)
        {
            string flags = "";
            if (dat != 0 && tRomTable.FileStatusIs(dat))
            {
                flags += "D";
            }

            if (file != 0 && tRomTable.FileStatusIs(file))
            {
                flags += "F";
            }

            if (verified != 0 && tRomTable.FileStatusIs(verified))
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
            try
            {
                RvFile tFile = romGrid[e.RowIndex];
                e.CellStyle.BackColor = Dark.dark.Down(_displayColor[(int)tFile.RepStatus]);
                e.CellStyle.ForeColor = _fontColor[(int)tFile.RepStatus];
            }
            catch { }
        }



        private void RomGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (romGrid == null)
                    return;

                if (RomGrid.Columns[e.ColumnIndex].SortMode == DataGridViewColumnSortMode.NotSortable)
                    return;

                if (romSortIndex != e.ColumnIndex)
                {
                    if (romSortIndex >= 0)
                        RomGrid.Columns[romSortIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
                    romSortIndex = e.ColumnIndex;
                    romSortDir = SortOrder.Ascending;
                }
                else
                {
                    romSortDir = romSortDir == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
                }

                RomGrid.Columns[romSortIndex].HeaderCell.SortGlyphDirection = romSortDir;

                Debug.WriteLine($"Sort in {romSortIndex}  dir {romSortDir}");

                IComparer<RvFile> t = new RomUiCompare(e.ColumnIndex, romSortDir);

                Array.Sort(romGrid, t);
                RomGrid.Refresh();
            }
            catch { }
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
                try
                {


                    int retVal = 0;
                    switch ((eRomGrid)_colIndex)
                    {
                        case eRomGrid.Got:   // then by name
                            retVal = x.GotStatus - y.GotStatus;
                            if (retVal != 0)
                                break;
                            retVal = x.RepStatus - y.RepStatus;
                            if (retVal != 0)
                                break;
                            retVal = string.Compare(x.UiDisplayName ?? "", y.UiDisplayName ?? "", StringComparison.Ordinal);
                            break;
                        case eRomGrid.Rom:
                            retVal = string.Compare(x.UiDisplayName ?? "", y.UiDisplayName ?? "", StringComparison.Ordinal);
                            break;
                        case eRomGrid.Merge:
                            retVal = string.Compare(x.Merge ?? "", y.Merge ?? "", StringComparison.Ordinal);
                            break;
                        case eRomGrid.Size:
                            retVal = ULong.iCompareNull(x.Size, y.Size);
                            break;
                        case eRomGrid.CRC32:
                            retVal = ArrByte.ICompare(x.CRC, y.CRC);
                            break;
                        case eRomGrid.SHA1:
                            retVal = ArrByte.ICompare(x.SHA1, y.SHA1);
                            break;
                        case eRomGrid.MD5:
                            retVal = ArrByte.ICompare(x.MD5, y.MD5);
                            break;
                        case eRomGrid.AltSize:
                            retVal = ULong.iCompareNull(x.AltSize, y.AltSize);
                            break;
                        case eRomGrid.AltCRC32:
                            retVal = ArrByte.ICompare(x.AltCRC, y.AltCRC);
                            break;
                        case eRomGrid.AltSHA1:
                            retVal = ArrByte.ICompare(x.AltSHA1, y.AltSHA1);
                            break;
                        case eRomGrid.AltMD5:
                            retVal = ArrByte.ICompare(x.AltMD5, y.AltMD5);
                            break;
                        case eRomGrid.Status:
                            retVal = string.Compare(x.Status ?? "", y.Status ?? "", StringComparison.Ordinal);
                            break;
                        case eRomGrid.DateModFile:
                            string time1 = CompressUtils.zipDateTimeToString(x.FileModTimeStamp);
                            string time2 = CompressUtils.zipDateTimeToString(y.FileModTimeStamp);
                            retVal = string.Compare(time1 ?? "", time2 ?? "", StringComparison.Ordinal);
                            break;

                        case eRomGrid.ZipIndex:
                            retVal = x.ZipFileIndex - y.ZipFileIndex;
                            break;

                        case eRomGrid.DupeCount:
                            if (x.FileGroup != null && y.FileGroup != null)
                                retVal = x.FileGroup.Files.Count - y.FileGroup.Files.Count;
                            else
                                retVal = 0;
                            break;
                    }

                    if (_sortDir == SortOrder.Descending)
                        retVal = -retVal;

                    if (retVal == 0 && _colIndex != 1)
                        retVal = string.Compare(x.UiDisplayName ?? "", y.UiDisplayName ?? "", StringComparison.Ordinal);

                    return retVal;
                }
                catch
                {
                    return 0;
                }
            }


        }

        private void RomGridMouseUp(object sender, MouseEventArgs e)
        {
            if (e == null)
                return;

            DataGridView.HitTestInfo hitTest = RomGrid.HitTest(e.X, e.Y);

            if (e.Button == MouseButtons.Left)
            {
                if (hitTest.ColumnIndex != (int)eRomGrid.DupeCount)
                    return;
                if (hitTest.RowIndex < 0)
                    return;

                RvFile tFile = romGrid[hitTest.RowIndex];
                FrmRomInfo fri = new FrmRomInfo();
                fri.SetRom(tFile);
                fri.ShowDialog();
                return;
            }

            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int mouseRow = hitTest.RowIndex;
            if (mouseRow < 0)
                return;

            int mouseColumn = hitTest.ColumnIndex;


            string name = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.Rom].Value ?? "").ToString();
            string size = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.Size].Value ?? "").ToString();
            if (size.Contains(" "))
                size = size.Substring(0, size.IndexOf(" "));

            string crc = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.CRC32].Value ?? "").ToString();
            if (crc.Length > 8)
                crc = crc.Substring(0, 8);

            string sha1 = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.SHA1].Value ?? "").ToString();
            if (sha1.Length > 40)
                sha1 = sha1.Substring(0, 40);

            string md5 = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.MD5].Value ?? "").ToString();
            if (md5.Length > 32)
                md5 = md5.Substring(0, 32);


            string altsize = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.AltSize].Value ?? "").ToString();
            if (altsize.Contains(" "))
                altsize = altsize.Substring(0, altsize.IndexOf(" "));


            string altcrc = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.AltCRC32].Value ?? "").ToString();
            if (altcrc.Length > 8)
                altcrc = altcrc.Substring(0, 8);

            string altsha1 = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.AltSHA1].Value ?? "").ToString();
            if (altsha1.Length > 40)
                altsha1 = altsha1.Substring(0, 40);

            string altmd5 = (RomGrid.Rows[mouseRow].Cells[(int)eRomGrid.AltMD5].Value ?? "").ToString();
            if (altmd5.Length > 32)
                altmd5 = altmd5.Substring(0, 32);


            string clipText = null;
            switch ((eRomGrid)mouseColumn)
            {
                case eRomGrid.Got:
                    {
                        clipText = $"Name : {name}\n";
                        clipText += $"Size : {size}\n";
                        clipText += $"CRC32: {crc}\n";
                        if (!string.IsNullOrWhiteSpace(sha1))
                            clipText += $"SHA1 : {sha1}\n";

                        if (!string.IsNullOrWhiteSpace(md5))
                            clipText += $"MD5  : {md5}\n";
                        break;
                    }
                case eRomGrid.Rom: clipText = name; break;
                case eRomGrid.Size: clipText = size; break;
                case eRomGrid.CRC32: clipText = crc; break;
                case eRomGrid.SHA1: clipText = sha1; break;
                case eRomGrid.MD5: clipText = md5; break;
                case eRomGrid.AltSize: clipText = altsize; break;
                case eRomGrid.AltCRC32: clipText = altcrc; break;
                case eRomGrid.AltSHA1: clipText = altsha1; break;
                case eRomGrid.AltMD5: clipText = altmd5; break;
            }

            if (string.IsNullOrEmpty(clipText))
                return;

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
            /*
            if (RomGrid.SelectedRows.Count == 1)
            {
                RvFile rom = romGrid[RomGrid.SelectedRows[0].Index];
                LoadPannelFromRom(rom);
            }
            */
            RomGrid.ClearSelection();
        }

    }
}
