/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using DATReader.DatStore;
using DATReader.DatWriter;
using Extensions;
using RomVaultCore;
using RomVaultCore.FindFix;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RomVaultCore.Utils;
using RVIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MethodInvoker = System.Windows.Forms.MethodInvoker;

namespace ROMVault
{
    public partial class FrmMain : Form
    {
        private RvFile _clickedTree;

        #region FrmMainOpenClose

        public FrmMain()
        {
            InitializeComponent();

            ReadDefaults();

            InitializeTitleBarText();



            ctrRvTree.Setup(ref DB.DirRoot);

            splitDatInfoTree_Panel1_Resize(new object(), new EventArgs());
            splitGameInfoLists_Panel1_Resize(new object(), new EventArgs());

            InitializeTreeMainMenu();
            InitializeTreeToSortMenu();
            InitializeGameGridMenu();

            ExtHelper.AddIns(this, isWorking, UpdateDats, updateMIACallback);

            if (Settings.rvSettings.Darkness)
            {
                Dark.dark.SetColors(this);
                SetTextBoxHeight(ucDatInfo);
                SetTextBoxHeight(ucGameInfo);
            }

            grdGame.updateGameInfo += UpdateGameGridCallback;
            grdGame.updateDatInfo += UpdateDatInfoUpdateCallback;
            grdGame.MenuClick += OpenMenuCallback;

            sidePannel.DisplaySide += SidePannelDisplaySide;
            SidePannelDisplaySide(false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_working)
            {
                e.Cancel = true;
                return;
            }
            WriteDefaults();
        }
        private void FrmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_formKey != null && !_formKey.IsDisposed)
                _formKey.Close();

            while (sendingTextIsEmpty)
                Thread.Sleep(1000);

            this.Hide();

            Environment.Exit(0);
        }


        #endregion



        #region Defaults


        private void ReadDefaults()
        {
            defaults defaults = defaults.ReadDefaults();
            if (defaults != null)
            {
                if (defaults.mainX > -30000 && defaults.mainY > -30000 && defaults.mainHeight > 50)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(defaults.mainX, defaults.mainY);
                    this.Size = new Size(defaults.mainWidth, defaults.mainHeight);
                }

                if (defaults.splitDatInfoGameInfo_pos != int.MinValue) this.splitDatInfoGameInfo.SplitterDistance = defaults.splitDatInfoGameInfo_pos;
                if (defaults.splitGameListRomList_pos != int.MinValue) this.splitGameListRomList.SplitterDistance = defaults.splitGameListRomList_pos;
                if (defaults.splitListArt_pos != int.MinValue) this.splitListArt.SplitterDistance = defaults.splitListArt_pos;


                grdGame.SetDefaults(defaults);
                grdRom.SetDefaults(defaults);
                sidePannel.SetDefaults(defaults);
            }
        }


        private void WriteDefaults()
        {
            defaults df = new defaults();
            if (this.WindowState == FormWindowState.Minimized)
            {
                df.mainX = this.RestoreBounds.X;
                df.mainY = this.RestoreBounds.Y;
                df.mainWidth = this.RestoreBounds.Width;
                df.mainHeight = this.RestoreBounds.Height;
            }
            else
            {
                df.mainX = this.Location.X;
                df.mainY = this.Location.Y;
                df.mainWidth = this.Size.Width;
                df.mainHeight = this.Size.Height;
            }

            df.splitDatInfoGameInfo_pos = this.splitDatInfoGameInfo.SplitterDistance;
            df.splitGameListRomList_pos = this.splitGameListRomList.SplitterDistance;
            df.splitListArt_pos = this.splitListArt.SplitterDistance;


            grdGame.PutDefaults(df);
            grdRom.PutDefaults(df);
            sidePannel.PutDefaults(df);

            defaults.WriteDefaults(df);
        }

        #endregion

        #region TreeEvents
        private void DirTreeRvChecked(object sender, MouseEventArgs e)
        {
            RepairStatus.ReportStatusReset(DB.DirRoot);
            DatSetSelected(ctrRvTree.Selected);
        }

        private void DirTreeRvSelected(object sender, MouseEventArgs e)
        {
            RvFile cf = (RvFile)sender;

            if (e.Button != MouseButtons.Right)
            {
                if (cf != grdGame.gameGridSource)
                {
                    DatSetSelected(cf);
                }
                return;
            }

            if (cf != ctrRvTree.Selected)
            {
                DatSetSelected(cf);
            }

            _clickedTree = (RvFile)sender;

            if (_working)
                return;

            Point controLocation = ControlLoc(ctrRvTree);

            if (cf.IsInToSort)
            {
                _mnuToSortOpen.Enabled = Directory.Exists(_clickedTree.FullName);
                _mnuToSortDelete.Enabled = !(_clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortPrimary) || _clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortCache));
                _mnuToSortSetCache.Visible = !(_clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortCache) || _clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly));
                _mnuToSortSetPrimary.Visible = !(_clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortPrimary) || _clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly));

                _mnuToSortSetFileOnly.Visible = !(_clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly) || _clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortPrimary) || _clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortCache));
                _mnuToSortClearFileOnly.Visible = _clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly);

                int thisToSort = 0;
                for (int i = 0; i < DB.DirRoot.ChildCount; i++)
                {
                    if (DB.DirRoot.Child(i) == cf)
                    {
                        thisToSort = i;
                        break;
                    }
                }
                _mnuToSortUp.Enabled = thisToSort >= 2;
                _mnuToSortDown.Enabled = thisToSort <= DB.DirRoot.ChildCount - 2;

                _mnuTreeToSort.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));
            }
            else
            {
                _mnuOpen.Enabled = Directory.Exists(_clickedTree.FullName);
                //_mnuFile.Enabled = _clickedTree.Dat == null;
                _mnuTreeMain.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));
            }
        }


        #endregion

        #region MainTreeMenu
        private ContextMenuStrip _mnuTreeMain;
        private ToolStripMenuItem _mnuOpen;

        private void InitializeTreeMainMenu()
        {
            _mnuTreeMain = new ContextMenuStrip();
            addMenuItem(_mnuTreeMain, "Scan", MnuScan, EScanLevel.Level2);
            addMenuItem(_mnuTreeMain, "Scan Quick (Headers Only)", MnuScan, EScanLevel.Level1);
            addMenuItem(_mnuTreeMain, "Scan Full (Complete Re-Scan)", MnuScan, EScanLevel.Level3);
            addMenuItem(_mnuTreeMain, "Set Dir Dat Settings", MnuDirSettings);
            addMenuItem(_mnuTreeMain, "Set Dir Mappings", MnuDirMappings);
            _mnuTreeMain.Items.Add(new ToolStripSeparator());
            _mnuOpen = addMenuItem(_mnuTreeMain, "Open Directory", MnuOpenClick);
            addMenuItem(_mnuTreeMain, "Save fix DATs", MnuMakeFixDatClick);
            addMenuItem(_mnuTreeMain, "Save full DAT", MnuMakeDatClick);

            if ((Settings.rvSettings.Permissions & 8) == 8)
                addMenuItem(_mnuTreeMain, "Reset Corrupt for scanning", MnuResetCorruptClick);
        }

        private void MnuDirSettings(object sender, EventArgs e)
        {
            using (FrmDirectorySettings fDirSettings = new FrmDirectorySettings())
            {
                string tDir = _clickedTree.TreeFullName;
                fDirSettings.SetLocation(tDir);
                fDirSettings.SetDisplayType(true);
                fDirSettings.ShowDialog(this);

                if (fDirSettings.ChangesMade)
                    UpdateDats();
            }
        }

        private void MnuDirMappings(object sender, EventArgs e)
        {
            using (FrmDirectoryMappings fDirMappings = new FrmDirectoryMappings())
            {
                string tDir = _clickedTree.TreeFullName;
                fDirMappings.SetLocation(tDir);
                fDirMappings.SetDisplayType(true);
                fDirMappings.ShowDialog(this);
            }
        }

        private void MnuOpenClick(object sender, EventArgs e)
        {
            RVProcess.StartDIR(_clickedTree.FullName);
        }

        private void MnuMakeFixDatClick(object sender, EventArgs e)
        {
            MakeFixDat(_clickedTree, true);
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
                return;

            if (browse.FileName == "")
                return;

            DatHeader dh = (new ExternalDatConverterTo()).ConvertToExternalDat(_clickedTree);
            DatXMLWriter.WriteDat(browse.FileName, dh);
        }

        private void MnuResetCorruptClick(object sender, EventArgs e)
        {
            ClearPartial.ResetCorrupt(_clickedTree);
            DatSetSelected(ctrRvTree.Selected);
        }
        #endregion

        #region ToSortTreeMenu
        private ContextMenuStrip _mnuTreeToSort;
        private ToolStripMenuItem _mnuToSortOpen;
        private ToolStripMenuItem _mnuToSortSetPrimary;
        private ToolStripMenuItem _mnuToSortSetCache;
        private ToolStripMenuItem _mnuToSortSetFileOnly;
        private ToolStripMenuItem _mnuToSortClearFileOnly;
        private ToolStripMenuItem _mnuToSortDelete;
        private ToolStripMenuItem _mnuToSortUp;
        private ToolStripMenuItem _mnuToSortDown;

        private void InitializeTreeToSortMenu()
        {
            _mnuTreeToSort = new ContextMenuStrip();
            addMenuItem(_mnuTreeToSort, "Scan", MnuScan, EScanLevel.Level2);
            addMenuItem(_mnuTreeToSort, "Scan Quick (Headers Only)", MnuScan, EScanLevel.Level1);
            addMenuItem(_mnuTreeToSort, "Scan Full (Complete Re-Scan)", MnuScan, EScanLevel.Level3);
            _mnuToSortOpen = addMenuItem(_mnuTreeToSort, "Open ToSort Directory", MnuToSortOpen);
            _mnuTreeToSort.Items.Add(new ToolStripSeparator());
            _mnuToSortSetPrimary = addMenuItem(_mnuTreeToSort, "Set To Primary ToSort", MnuToSortSetPrimary);
            _mnuToSortSetCache = addMenuItem(_mnuTreeToSort, "Set To Cache ToSort", MnuToSortSetCache);
            _mnuToSortSetFileOnly = addMenuItem(_mnuTreeToSort, "Set To File Only ToSort", MnuToSortSetFileOnly);
            _mnuToSortClearFileOnly = addMenuItem(_mnuTreeToSort, "Clear File Only ToSort", MnuToSortClearFileOnly);
            _mnuToSortDelete = addMenuItem(_mnuTreeToSort, "Remove", MnuToSortDelete);
            _mnuTreeToSort.Items.Add(new ToolStripSeparator());
            _mnuToSortUp = addMenuItem(_mnuTreeToSort, "Move Up", MnuToSortUp);
            _mnuToSortDown = addMenuItem(_mnuTreeToSort, "Move Down", MnuToSortDown);
        }

        private void MnuToSortOpen(object sender, EventArgs e)
        {
            RVProcess.StartDIR(_clickedTree.FullName);
        }

        private void MnuToSortSetPrimary(object sender, EventArgs e)
        {
            if (_clickedTree.Tree.Checked == RvTreeRow.TreeSelect.Locked)
            {
                _clickedTree.Tree.SetChecked(RvTreeRow.TreeSelect.Selected, true);
                //MessageBox.Show("Directory Must be ticked.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //return;
            }

            RvFile t = DB.GetToSortPrimary();
            bool wasCache = t.ToSortStatusIs(RvFile.ToSortDirType.ToSortCache);
            t.ToSortStatusClear(RvFile.ToSortDirType.ToSortPrimary | RvFile.ToSortDirType.ToSortCache);

            _clickedTree.ToSortStatusSet(RvFile.ToSortDirType.ToSortPrimary);
            if (wasCache)
                _clickedTree.ToSortStatusSet(RvFile.ToSortDirType.ToSortCache);

            DB.Write();
            ctrRvTree.Refresh();
        }

        private void MnuToSortSetCache(object sender, EventArgs e)
        {
            if (_clickedTree.Tree.Checked == RvTreeRow.TreeSelect.Locked)
            {
                _clickedTree.Tree.SetChecked(RvTreeRow.TreeSelect.Selected, true);
                //MessageBox.Show("Directory Must be ticked.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //return;
            }

            RvFile t = DB.GetToSortCache();
            t.ToSortStatusClear(RvFile.ToSortDirType.ToSortCache);

            _clickedTree.ToSortStatusSet(RvFile.ToSortDirType.ToSortCache);

            DB.Write();
            ctrRvTree.Refresh();
        }

        private void MnuToSortSetFileOnly(object sender, EventArgs e)
        {
            if (_clickedTree.Tree.Checked == RvTreeRow.TreeSelect.Locked)
            {
                _clickedTree.Tree.SetChecked(RvTreeRow.TreeSelect.Selected, true);
                //MessageBox.Show("Directory Must be ticked.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //return;
            }
            if (_clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortPrimary))
            {
                MessageBox.Show("Primary Directory Cannot be File Only.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_clickedTree.ToSortStatusIs(RvFile.ToSortDirType.ToSortCache))
            {
                MessageBox.Show("Cache Directory Cannot be File Only.", "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _clickedTree.ToSortStatusSet(RvFile.ToSortDirType.ToSortFileOnly);

            DB.Write();
            ctrRvTree.Refresh();

        }

        private void MnuToSortClearFileOnly(object sender, EventArgs e)
        {
            _clickedTree.ToSortStatusClear(RvFile.ToSortDirType.ToSortFileOnly);
            ctrRvTree.Setup(ref DB.DirRoot);
            DB.Write();
        }

        private void MnuToSortDelete(object sender, EventArgs e)
        {
            for (int i = 0; i < DB.DirRoot.ChildCount; i++)
            {
                if (DB.DirRoot.Child(i) == _clickedTree)
                {
                    DB.DirRoot.ChildRemove(i);
                    RepairStatus.ReportStatusReset(DB.DirRoot);

                    ctrRvTree.Setup(ref DB.DirRoot);
                    DatSetSelected(DB.DirRoot.Child(i - 1));
                    DB.Write();
                    ctrRvTree.Refresh();
                    return;
                }
            }
        }

        private void MnuToSortUp(object sender, EventArgs e)
        {
            DB.MoveToSortUp(_clickedTree);
            ctrRvTree.Setup(ref DB.DirRoot);
            DB.Write();
        }

        private void MnuToSortDown(object sender, EventArgs e)
        {
            DB.MoveToSortDown(_clickedTree);
            ctrRvTree.Setup(ref DB.DirRoot);
            DB.Write();
        }

        #endregion

        #region GameGridMenu
        private ContextMenuStrip _mnuGameGrid;

        private ToolStripMenuItem _mnuGameScan1;
        private ToolStripMenuItem _mnuGameScan2;
        private ToolStripMenuItem _mnuGameScan3;
        private ToolStripMenuItem _mnuOpenDir;
        private ToolStripMenuItem _mnuOpenParentDir;
        private ToolStripMenuItem _mnuDir2Dat;
        private ToolStripMenuItem _mnuLaunchEmulator;


        private void InitializeGameGridMenu()
        {
            _mnuGameGrid = new ContextMenuStrip();
            _mnuGameScan1 = addMenuItem(null, "Scan Quick (Headers Only)", MnuGameScan, EScanLevel.Level1);
            _mnuGameScan2 = addMenuItem(null, "Scan", MnuGameScan, EScanLevel.Level2);
            _mnuGameScan3 = addMenuItem(null, "Scan Full (Complete Re-Scan)", MnuGameScan, EScanLevel.Level3);
            _mnuOpenDir = addMenuItem(null, "Open Directory", MnuOpenDir);
            _mnuOpenParentDir = addMenuItem(null, "Open Parent", MnuOpenParentDir);
            _mnuDir2Dat = addMenuItem(null, "Dir2Dat", MnuDir2Dat);
            _mnuLaunchEmulator = addMenuItem(null, "Launch Emulator", LaunchEmulator);
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

        public void OpenMenuCallback(RvFile thisGame, MouseEventArgs e)
        {
            Point controLocation = ControlLoc(grdGame);
            _mnuGameGrid.Items.Clear();

            ToolStripSeparator item = new ToolStripSeparator();
            if (thisGame.FileType == FileType.Dir && !_working)
            {
                _mnuGameGrid.Items.Add(_mnuGameScan2);
                _mnuGameGrid.Items.Add(_mnuGameScan1);
                _mnuGameGrid.Items.Add(_mnuGameScan3);
                _mnuGameGrid.Items.Insert(3, item);
            }

            bool found = false;
            if (thisGame.FileType == FileType.Dir)
            {
                if ((Settings.rvSettings.Permissions & 4) == 4)
                    _mnuGameGrid.Items.Add(_mnuDir2Dat);

                string folderPath = thisGame.FullNameCase;
                if (Directory.Exists(folderPath))
                {
                    found = true;
                    _mnuOpenDir.Text = "Open Dir";
                    _mnuGameGrid.Items.Add(_mnuOpenDir);
                }
            }

            if (thisGame.FileType == FileType.Zip || thisGame.FileType == FileType.SevenZip)
            {
                string zipPath = thisGame.FullNameCase;
                if (File.Exists(zipPath))
                {
                    found = true;
                    if (thisGame.FileType == FileType.Zip)
                        _mnuOpenDir.Text = "Open Zip";

                    if (thisGame.FileType == FileType.SevenZip)
                        _mnuOpenDir.Text = "Open 7Zip";
                    _mnuGameGrid.Items.Add(_mnuOpenDir);
                }
            }

            {
                string parentPath = thisGame.Parent.FullName;
                if (Directory.Exists(parentPath))
                {
                    found = true;
                    _mnuOpenParentDir.Text = "Open Parent";
                    _mnuGameGrid.Items.Add(_mnuOpenParentDir);
                }
            }

            if (EmulatorLaunch.FindEmulatorInfo(thisGame) != null && found)
                _mnuGameGrid.Items.Add(_mnuLaunchEmulator);

            if (_mnuGameGrid.Items.Count == 0)
                return;

            if (_mnuGameGrid.Items[_mnuGameGrid.Items.Count - 1] == item)
                _mnuGameGrid.Items.RemoveAt(_mnuGameGrid.Items.Count - 1);

            _mnuGameGrid.Tag = thisGame;
            _mnuGameGrid.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));

        }

        private void MnuOpenDir(object sender, EventArgs e)
        {
            RvFile thisFile = (RvFile)_mnuGameGrid.Tag;
            if (thisFile.FileType == FileType.Dir)
            {
                RVProcess.StartDIR(thisFile.FullNameCase);
                return;
            }
            if (thisFile.FileType == FileType.Zip || thisFile.FileType == FileType.SevenZip)
            {
                string zipPath = thisFile.FullNameCase;
                if (File.Exists(zipPath))
                {
                    RVProcess.StartURL(zipPath);
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
                RVProcess.StartDIR(thisFile.FullNameCase);
                return;
            }
        }

        frmDir2Dat d2d = null;
        private void MnuDir2Dat(object sender, EventArgs e)
        {
            if (d2d == null)
                d2d = new frmDir2Dat();

            d2d.PopulateFrom((RvFile)_mnuGameGrid.Tag);
            d2d.ShowDialog();
        }

        private void LaunchEmulator(object sender, EventArgs e)
        {
            RvFile tGame = _mnuGameGrid.Tag as RvFile;
            if (tGame != null)
                EmulatorLaunch.LaunchEmulator(tGame);
        }
        #endregion



        #region SideButtons

        private void sideButtons_BtnUpdateDats_MouseUp(object sender, MouseEventArgs e)
        {
            RootDirsCreate.CheckDatRoot();
            if (e.Button == MouseButtons.Right)
            {
                ExtHelper.DatVaultRightClick();
                return;
            }
            else if (Control.ModifierKeys == Keys.Shift)
            {
                DatUpdate.InvalidateAllDATs(DB.DirRoot.Child(0), @"DatRoot\");
            }
            Start();
            UpdateDats();
            Finish();
        }

        private void sideButtons_BtnScanRoms_MouseUp(object sender, MouseEventArgs e)
        {
            ScanRoms(EScanLevel.Level2);
        }

        private void sideButtons_BtnFindFixes_MouseUp(object sender, MouseEventArgs e)
        {
            FindFixes(Control.ModifierKeys == (Keys.Shift | Keys.Control));
        }

        private void sideButtons_BtnFixFiles_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Automate.AutoScanFix();
                return;
            }

            FixFiles();
        }

        private void sideButtons_BtnReport_MouseUp(object sender, MouseEventArgs e)
        {
            MakeFixDat(DB.DirRoot.Child(0), e.Button == MouseButtons.Left);
        }

        private void sideButtons_BtnDefault1_MouseUp(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 1);
        }

        private void sideButtons_BtnDefault2_MouseUp(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 2);
        }

        private void sideButtons_BtnDefault3_MouseUp(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 3);
        }

        private void sideButtons_BtnDefault4_MouseUp(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 4);
        }

        public void treeDefault(bool set, int index)
        {
            DatTreeStatusStore dtss = new DatTreeStatusStore();
            if (set)
            {
                dtss.write(index);
                return;
            }
            dtss.read(index);
            ctrRvTree.Setup(ref DB.DirRoot, true);
        }

        #endregion

        #region TopMenu

        // Update DATs
        private void updateNewDATsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            UpdateDats();
        }
        private void updateAllDATsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            DatUpdate.InvalidateAllDATs(DB.DirRoot.Child(0), @"DatRoot\");
            UpdateDats();
        }

        //Scan ROMs
        private void TsmScanLevel1Click(object sender, EventArgs e)
        {
            if (_working) return;
            ScanRoms(EScanLevel.Level1);
        }
        private void TsmScanLevel2Click(object sender, EventArgs e)
        {
            if (_working) return;
            ScanRoms(EScanLevel.Level2);
        }
        private void TsmScanLevel3Click(object sender, EventArgs e)
        {
            if (_working) return;
            ScanRoms(EScanLevel.Level3);
        }

        //Find Fixes
        private void TsmFindFixesClick(object sender, EventArgs e)
        {
            if (_working) return;
            FindFixes();
        }

        //Fix ROMs
        private void FixFilesToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_working) return;
            FixFiles();
        }

        //Reports
        private void fixDatReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            MakeFixDat(DB.DirRoot.Child(0), true);
        }
        private void fullReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            Report.GenerateReport();
        }
        private void fixReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            Report.GenerateFixReport();
        }

        //Settings
        private void RomVaultSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            using (FrmSettings fcfg = new FrmSettings())
            {
                fcfg.ShowDialog(this);

                if (!fcfg.MIADaysChanged && Settings.rvSettings.ShowNewMIA == fcfg.previousShowNewMIA)
                    return;

                bool changeShowNewMIA = Settings.rvSettings.ShowNewMIA != fcfg.previousShowNewMIA;

                if (changeShowNewMIA)
                {
                    // flip the value back to correctly remove all the old values.
                    if (changeShowNewMIA)
                        Settings.rvSettings.ShowNewMIA = !Settings.rvSettings.ShowNewMIA;

                    MIA.ClearOut();

                    // now flip the value again to put it back to its new value.
                    if (changeShowNewMIA)
                        Settings.rvSettings.ShowNewMIA = !Settings.rvSettings.ShowNewMIA;

                    // continue on with the new settings, and the code below will put everything back in as now required.
                }

                MIA.updateType = MIA.MIAUpdateType.doUpdate;
                using (FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", MIA.UpdateMIA, null))
                {
                    progress.HideCancelButton();
                    progress.ShowDialog(this);
                }
                DatSetSelected(ctrRvTree.Selected);
            }
        }
        private void DirectorySettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            using (FrmDirectorySettings sd = new FrmDirectorySettings())
            {
                string tDir = "RomVault";
                sd.SetLocation(tDir);
                sd.SetDisplayType(false);
                sd.ShowDialog(this);

                if (sd.ChangesMade)
                    UpdateDats();
            }
        }
        private void directoryMappingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            using (FrmDirectoryMappings sd = new FrmDirectoryMappings())
            {
                string tDir = "RomVault";
                sd.SetLocation(tDir);
                sd.SetDisplayType(false);
                sd.ShowDialog(this);
            }
        }

        //Add ToSort
        private void AddToSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            FolderBrowser cfbd = new FolderBrowser
            {
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.MyComputer,
                Description = "Select new ToSort Folder"
            };

            DialogResult result = cfbd.ShowDialog(this);
            if (result != DialogResult.OK) return;

            string relPath = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, cfbd.SelectedPath);

            RvFile ts = new RvFile(FileType.Dir)
            {
                Name = relPath,
                DatStatus = DatStatus.InToSort,
                Tree = new RvTreeRow()
            };
            ts.Tree.SetChecked(RvTreeRow.TreeSelect.Locked, false);

            DB.DirRoot.ChildAdd(ts, DB.DirRoot.ChildCount);

            RepairStatus.ReportStatusReset(DB.DirRoot);
            ctrRvTree.Setup(ref DB.DirRoot);
            DatSetSelected(ts);

            DB.Write();
        }

        //Help
        private void torrentZipToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if NET10_0
            string appName = Environment.ProcessPath;
#else
            string appName = Assembly.GetEntryAssembly().Location;
#endif
            RVProcess.StartURL(appName, "sam");
        }

        private FrmKey _formKey;
        private void colorKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_formKey == null || _formKey.IsDisposed)
            {
                _formKey = new FrmKey();
            }

            _formKey.Show();
        }
        private void visitHelpWikiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RVProcess.StartURL("https://wiki.romvault.com/doku.php?id=help");
        }
        private void whatsNewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RVProcess.StartURL("https://wiki.romvault.com/doku.php?id=whats_new");
        }
        private void AboutRomVaultToolStripMenuItemClick(object sender, EventArgs e)
        {
            FrmHelpAbout fha = new FrmHelpAbout();
            fha.ShowDialog(this);
            fha.Dispose();
        }

        #endregion

        #region MenuCommonWorkerFunctions

        private static ToolStripMenuItem addMenuItem(ContextMenuStrip menu, string text, EventHandler click, object tag = null)
        {
            ToolStripMenuItem mnu = new ToolStripMenuItem
            {
                Text = text,
                Tag = tag
            };
            mnu.Click += click;
            if (menu != null)
                menu.Items.Add(mnu);

            return mnu;
        }

        private void MnuScan(object sender, EventArgs e)
        {
            if (_working)
                return;
            ScanRoms((EScanLevel)((ToolStripMenuItem)sender).Tag, _clickedTree);
        }

        private void MnuGameScan(object sender, EventArgs e)
        {
            if (_working)
                return;
            RvFile thisFile = (RvFile)_mnuGameGrid.Tag;
            ScanRoms((EScanLevel)((ToolStripMenuItem)sender).Tag, thisFile);
        }

        public FrmProgressWindow frmScanRoms = null;
        public void ScanRoms(EScanLevel sd, RvFile StartAt = null, FormClosedEventHandler fceh = null)
        {
            if (frmScanRoms != null && !frmScanRoms.IsDisposed)
                frmScanRoms.Dispose();

            FileScanning.StartAt = StartAt;
            FileScanning.EScanLevel = sd;
            frmScanRoms = new FrmProgressWindow(this, "Scanning Dirs", FileScanning.ScanFiles, Finish);
            Start();
            setPos(frmScanRoms);
            if (fceh != null)
                frmScanRoms.FormClosed += fceh;
            frmScanRoms.Show();
        }

        public FrmProgressWindow frmFindFixes;
        public void FindFixes(bool showLog = false, FormClosedEventHandler fceh = null)
        {
            if (frmFindFixes != null && !frmFindFixes.IsDisposed)
                frmFindFixes.Dispose();

            frmFindFixes = new FrmProgressWindow(this, "Finding Fixes", RomVaultCore.FindFix.FindFixes.ScanFiles, Finish);
            frmFindFixes.ShowTimeLog = showLog;
            Start();
            setPos(frmFindFixes);
            if (fceh != null)
                frmFindFixes.FormClosed += fceh;
            frmFindFixes.Show();
        }

        FrmProgressWindowFix frmFixFiles;
        public void FixFiles(bool closeOnExit = false, FormClosedEventHandler fceh = null)
        {
            if (frmFixFiles != null && !frmFixFiles.IsDisposed)
                frmFixFiles.Dispose();

            frmFixFiles = new FrmProgressWindowFix(this, closeOnExit, Finish);
            Start();
            setPos(frmFixFiles);
            if (fceh != null)
                frmFixFiles.FormClosed += fceh;
            frmFixFiles.Show();
        }

        private void setPos(Form childForm)
        {
            childForm.Owner = this;
            childForm.StartPosition = FormStartPosition.Manual;
            childForm.Location = new Point(
              Location.X + (Width - childForm.Width) / 2,
              Location.Y + (Height - childForm.Height) / 2
            );
        }


        public static void MakeFixDat(RvFile baseDir, bool redOnly)
        {
            FolderBrowser browse = new FolderBrowser
            {
                ShowNewFolderButton = true,
                Description = @"Please select fixdat files destination. NOTE: " + (redOnly ? @"reports will include Missing && MIA items only (omitting any Fixable items that may be present)" : @"reports will include both Missing, MIA and Fixable items"),
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = Settings.rvSettings.FixDatOutPath
            };

            if (browse.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            if (!Directory.Exists(browse.SelectedPath))
            {
                MessageBox.Show("Output Directory Not Found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (browse.SelectedPath != Settings.rvSettings.FixDatOutPath)
            {
                Settings.rvSettings.FixDatOutPath = browse.SelectedPath;
                Settings.WriteConfig();
            }

            FixDatReport.RecursiveDatTree(Settings.rvSettings.FixDatOutPath, baseDir, redOnly);
        }

        #endregion



        #region TopRightFilters
        private void ctrFilter_CheckedChanged(object sender, EventArgs e)
        {
            DatSetSelected(ctrRvTree.Selected);
        }
        private void ctrFilter_FilterTextChanged(object sender, UIElements.UIFilterOptions.FilterTextChangedEventArgs e)
        {
            grdGame.FilterText = e.FilterText;
            if (grdGame.gameGridSource != null)
                grdGame.UpdateGameGrid(grdGame.gameGridSource);
        }
        #endregion



        #region TitleBarText

        private string txtDefault;
        private string txtNow = "";
        private bool sendingTextIsEmpty = false;

        public void settext(string txt)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => settext(txt)));
                return;
            }

            txtNow = txt;

            string res = txtDefault;
            sendingTextIsEmpty = !string.IsNullOrWhiteSpace(txtNow);
            if (sendingTextIsEmpty)
                res += " " + txtNow;

            if (Text != res)
                Text = res;
        }

        /*
        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }
        */

        private void InitializeTitleBarText()
        {
            txtDefault = $@"RomVault ({Program.strVersion}) {Application.StartupPath}";
            settext("");
            MIA.stme = settext;
        }

        #endregion

        #region SidePannel
        private void SidePannelDisplaySide(bool visible)
        {
            splitListArt.Panel2Collapsed = !visible;
            if (visible)
                splitListArt.Panel2.Show();
            else
                splitListArt.Panel2.Hide();
        }

        #endregion

        #region UIWorking

        private bool _working = false;


        public bool isWorking()
        {
            return _working;
        }

        private void Start()
        {
            _working = true;
            timer1.Enabled = true;
            ctrRvTree.CoreActive = true;
            //menuStrip1.Enabled = false;
            foreach (ToolStripMenuItem item in menuStrip1.Items)
            {
                if (!(item is ToolStripMenuItem menuItem))
                    continue;
                if (menuItem.Text == "Help")
                    continue;
                menuItem.Enabled = false;
            }
            sideButtons.Disable();
        }
        private void Finish()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(Finish));
                return;
            }

            _working = false;
            ctrRvTree.CoreActive = false;
            //menuStrip1.Enabled = true;
            foreach (ToolStripMenuItem item in menuStrip1.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                    menuItem.Enabled = true;
            }

            sideButtons.Enable();


            timer1.Enabled = false;
            DatSetSelected(ctrRvTree.Selected);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            ctrRvTree.Refresh();
            grdGame.UpdateGameGrid(true);
            if (ctrRvTree.Selected != null)
                ucDatInfo.UpdateDatMetaData(ctrRvTree.Selected);
            grdGame.Refresh();
        }

        #endregion


        #region UIResize

        private float _scaleFactorX = 1;
        private float _scaleFactorY = 1;

        private static void SetTextBoxHeight(Control c)
        {
            foreach (Control c1 in c.Controls)
                SetTextBoxHeight(c1);

            switch (c)
            {
                case TextBox tb:
                    tb.Height = 14;
                    break;
            }
        }


        private void splitDatInfoTree_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitDatInfoTree.Panel1.Width == 0)
                return;

            ucDatInfo.Width = splitDatInfoTree.Panel1.Width - ucDatInfo.Left * 2;
        }

        private void splitGameInfoLists_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitGameInfoLists.Panel1.Width == 0)
                return;

            int chkLeft = splitGameInfoLists.Panel1.Width - 150;
            if (chkLeft < 430)
                chkLeft = 430;

            ctrFilter.Left = chkLeft;
            ucGameInfo.Width = chkLeft - ucGameInfo.Left - 10;
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

            ucDatInfo.SetScaleFactor(factor);
            ucGameInfo.SetScaleFactor(factor);
        }


        #endregion



        #region coreWorkerFunctions
        public void UpdateDats()
        {
            // incase the selected tree item(DAT) is removed from the tree in the updated we need to build a parent list and traverse up it until we find a parent item still in the tree.

            // build a list of the selected item in the Tree view and all the items up the parent list from there back to the root.
            RvFile selected = ctrRvTree.Selected;
            List<RvFile> parents = new List<RvFile>();
            while (selected != null)
            {
                parents.Add(selected);
                selected = selected.Parent;
            }

            // update the dats
            using (FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat, null))
            {
                progress.HideCancelButton();
                progress.ShowDialog(this);
            }
            // rebuild the tree
            ctrRvTree.Setup(ref DB.DirRoot);

            // if the rvFile.Parent is null it have been removed from the tree so remove it from the list.
            // set up until we find a rvFile with a parent.
            while (parents.Count > 1 && parents[0].Parent == null)
                parents.RemoveAt(0);

            // did we find a parent
            if (parents.Count > 0)
                selected = parents[0];
            else
                selected = null;

            // update the selected tree item, and the game grid view.
            ctrRvTree.SetSelected(selected);
            DatSetSelected(selected);
        }

        private void DatSetSelected(RvFile cf)
        {
            ctrRvTree.Refresh();

            grdGame.ClearGameGrid();

            if (cf == null)
            {
                return;
            }

            ucDatInfo.UpdateDatMetaData(cf);
            grdGame.UpdateGameGrid(cf);
        }

        private void updateMIACallback()
        {
            MIA.updateType = (Control.ModifierKeys == Keys.Shift) ? MIA.MIAUpdateType.forceUpdate : MIA.MIAUpdateType.Regular;

            // update the dats
            using (FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", MIA.UpdateMIA, null))
            {
                progress.HideCancelButton();
                progress.ShowDialog(this);
            }
            DatSetSelected(ctrRvTree.Selected);
        }

        #endregion

        #region gamegridMenu

        public void UpdateGameGridCallback(RvFile tGame, bool onTimer)
        {
            ucGameInfo.UpdateGameMetaData(tGame);
            sidePannel.UpdateSidePannel(tGame);
            grdRom.UpdateRomGrid(tGame, onTimer);
        }

        public void UpdateDatInfoUpdateCallback(RvFile tGame)
        {
            ctrRvTree.SetSelected(tGame);
            ucDatInfo.UpdateDatMetaData(tGame);
        }


        #endregion
    }
}