﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Compress;
using FileHeaderReader;
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
        DateModDat = 12,
        DateModFile = 13,
        ZipIndex = 14,
        DupeCount = 15

    }

    public partial class FrmMain
    {
        private bool altFound = false;
        private RvFile[] romGrid;
        private int romSortIndex = -1;
        private SortOrder romSortDir = SortOrder.None;

        private bool showStatus;
        private bool showDatModDate;
        private bool showFileModDate;


        private void UpdateRomGrid(RvFile tGame)
        {
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
            showDatModDate = false;
            showFileModDate = false;

            List<RvFile> fileList = new List<RvFile>();
            AddDir(tGame, "", ref fileList);
            romGrid = fileList.ToArray();

            RomGrid.Columns[(int)eRomGrid.AltSize].Visible = altFound;
            RomGrid.Columns[(int)eRomGrid.AltCRC32].Visible = altFound;
            RomGrid.Columns[(int)eRomGrid.AltSHA1].Visible = altFound;
            RomGrid.Columns[(int)eRomGrid.AltMD5].Visible = altFound;

            RomGrid.Columns[(int)eRomGrid.Status].Visible = showStatus;
            RomGrid.Columns[(int)eRomGrid.DateModDat].Visible = showDatModDate;
            RomGrid.Columns[(int)eRomGrid.DateModFile].Visible = showFileModDate;

            RomGrid.RowCount = romGrid.Length;
        }

        private void AddDir(RvFile tGame, string pathAdd, ref List<RvFile> fileList)
        {
            if (tGame == null)
                return;

            for (int l = 0; l < tGame.ChildCount; l++)
            {
                RvFile tBase = tGame.Child(l);

                if (tBase.IsFile)
                {
                    AddRom(tBase, pathAdd, ref fileList);
                }

                if (tBase.IsDir)
                {
                    AddDir(tBase, pathAdd + tBase.Name + "/", ref fileList);
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
                if (!altFound)
                {
                    altFound = (tFile.AltSize != null) || (tFile.AltCRC != null) || (tFile.AltSHA1 != null) || (tFile.AltMD5 != null);
                }
                showStatus |= !string.IsNullOrWhiteSpace(tFile.Status);
#if dt
                showDatModDate |= (tFile.DatModTimeStamp != null);
                showFileModDate |= (tFile.FileModTimeStamp != CompressUtils.TrrntzipDateTime) &
                                   ((tFile.DatModTimeStamp == null) | (tFile.DatModTimeStamp != null & tFile.DatModTimeStamp != tFile.FileModTimeStamp));

#endif     
            }
        }

        private void RomGridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            RvFile tFile = romGrid[e.RowIndex];
            switch ((eRomGrid)e.ColumnIndex)
            {
                case eRomGrid.Got:
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

                    if (tFile.HeaderFileType != HeaderFileType.Nothing)
                    {
                        fname += " (" + tFile.HeaderFileType + ")";
                    }

                    e.Value = fname;

                    break;
                case eRomGrid.Merge:
                    e.Value = tFile.Merge;
                    break;
                case eRomGrid.Size:
                    e.Value = SetCell(tFile.Size.ToString(), tFile, FileStatus.SizeFromDAT, FileStatus.SizeFromHeader, FileStatus.SizeVerified);
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
                    e.Value = SetCell(tFile.AltSize.ToString(), tFile, FileStatus.AltSizeFromDAT, FileStatus.AltSizeFromHeader, FileStatus.AltSizeVerified);
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
                        if (tFile.FileModTimeStamp == 0)
                            break;
                        DateTime tmp = new DateTime(tFile.FileModTimeStamp);
                        e.Value = tmp.ToString("yyyy/MM/dd HH:mm:ss");
                        break;
                    }
#if dt
                case eRomGrid.DateModDat:
                {
                    if (tFile.DatModTimeStamp == null)
                        break;
                    DateTime tmp = new DateTime((long)tFile.DatModTimeStamp);
                    e.Value = tmp.ToString("yyyy/MM/dd HH:mm:ss");
                    break;
                }
#endif
                case eRomGrid.ZipIndex:
                    if (tFile.FileType == FileType.ZipFile)
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
            RvFile tFile = romGrid[e.RowIndex];
            e.CellStyle.BackColor = _displayColor[(int)tFile.RepStatus];
            e.CellStyle.ForeColor = _fontColor[(int)tFile.RepStatus];
        }


        private void RomGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
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
            if (e == null || e.Button == MouseButtons.Left)
            {
                var hitTest = RomGrid.HitTest(e.X, e.Y);
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
