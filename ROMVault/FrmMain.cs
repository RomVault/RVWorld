/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RVIO;

using DATReader.DatStore;
using DATReader.DatWriter;
using TrrntZipUI;
using RomVaultCore.Utils;
using System.Threading;

namespace ROMVault
{
    public partial class FrmMain : Form
    {
        private static readonly Color CBlue = Color.FromArgb(214, 214, 255);
        private static readonly Color CGreyBlue = Color.FromArgb(214, 224, 255);
        private static readonly Color CRed = Color.FromArgb(255, 214, 214);
        private static readonly Color CBrightRed = Color.FromArgb(255, 0, 0);
        private static readonly Color CGreen = Color.FromArgb(214, 255, 214);
        private static readonly Color CNeonGreen = Color.FromArgb(100, 255, 100);
        private static readonly Color CLightRed = Color.FromArgb(255, 235, 235);
        private static readonly Color CSoftGreen = Color.FromArgb(150, 200, 150);
        private static readonly Color CGrey = Color.FromArgb(214, 214, 214);
        private static readonly Color CCyan = Color.FromArgb(214, 255, 255);
        private static readonly Color CCyanGrey = Color.FromArgb(214, 225, 225);
        private static readonly Color CMagenta = Color.FromArgb(255, 214, 255);
        private static readonly Color CBrown = Color.FromArgb(140, 80, 80);
        private static readonly Color CPurple = Color.FromArgb(214, 140, 214);
        private static readonly Color CYellow = Color.FromArgb(255, 255, 214);
        private static readonly Color CDarkYellow = Color.FromArgb(255, 255, 100);
        private static readonly Color COrange = Color.FromArgb(255, 214, 140);
        private static readonly Color CWhite = Color.FromArgb(255, 255, 255);
        private static int[] _gameGridColumnXPositions;

        private readonly Color[] _displayColor;
        private readonly Color[] _fontColor;

        private readonly ContextMenuStrip _mnuContext;
        private readonly ContextMenuStrip _mnuContextToSort;

        private readonly ToolStripMenuItem _mnuOpen;

        private readonly ToolStripMenuItem _mnuToSortOpen;
        private readonly ToolStripMenuItem _mnuToSortDelete;
        private readonly ToolStripMenuItem _mnuToSortSetPrimary;
        private readonly ToolStripMenuItem _mnuToSortSetCache;
        private readonly ToolStripMenuItem _mnuToSortSetFileOnly;
        private readonly ToolStripMenuItem _mnuToSortClearFileOnly;
        private readonly ToolStripMenuItem _mnuToSortUp;
        private readonly ToolStripMenuItem _mnuToSortDown;

        private RvFile _clickedTree;

        private bool _updatingGameGrid;

        private FrmKey _fk;

        private float _scaleFactorX = 1;
        private float _scaleFactorY = 1;

        private ToolStripMenuItem garbageCollectToolStripMenuItem;
        #region MainUISetup


        public FrmMain()
        {
            InitializeComponent();

            btnUpdateDats.BackgroundImage = rvImages.GetBitmap("btnUpdateDats_Enabled");
            btnScanRoms.BackgroundImage = rvImages.GetBitmap("btnScanRoms_Enabled");
            btnFindFixes.BackgroundImage = rvImages.GetBitmap("btnFindFixes_Enabled");
            btnFixFiles.BackgroundImage = rvImages.GetBitmap("btnFixFiles_Enabled");
            btnReport.BackgroundImage = rvImages.GetBitmap("btnReport_Enabled");

            btnDefault1.BackgroundImage = rvImages.GetBitmap("default1");
            btnDefault2.BackgroundImage = rvImages.GetBitmap("default2");
            btnDefault3.BackgroundImage = rvImages.GetBitmap("default3");
            btnDefault4.BackgroundImage = rvImages.GetBitmap("default4");

            AddGameMetaData();
            Text = $@"RomVault ({Program.strVersion}) {Application.StartupPath}";

            Type dgvType = GameGrid.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(GameGrid, true, null);

            dgvType = RomGrid.GetType();
            pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(RomGrid, true, null);


            _displayColor = new Color[(int)RepStatus.EndValue];
            _fontColor = new Color[(int)RepStatus.EndValue];

            // RepStatus.UnSet

            _displayColor[(int)RepStatus.UnScanned] = CBlue;

            _displayColor[(int)RepStatus.DirCorrect] = CGreen;
            _displayColor[(int)RepStatus.DirMissing] = CRed;
            _displayColor[(int)RepStatus.DirCorrupt] = CBrightRed; //BrightRed

            _displayColor[(int)RepStatus.Missing] = CRed;
            _displayColor[(int)RepStatus.Correct] = CGreen;
            _displayColor[(int)RepStatus.CorrectMIA] = CNeonGreen;
            _displayColor[(int)RepStatus.NotCollected] = CGrey;
            _displayColor[(int)RepStatus.UnNeeded] = CCyanGrey;
            _displayColor[(int)RepStatus.Unknown] = CCyan;
            _displayColor[(int)RepStatus.InToSort] = CMagenta;

            _displayColor[(int)RepStatus.MissingMIA] = CSoftGreen;

            _displayColor[(int)RepStatus.Corrupt] = CBrightRed; //BrightRed
            _displayColor[(int)RepStatus.Ignore] = CGreyBlue;

            _displayColor[(int)RepStatus.CanBeFixed] = CYellow;
            _displayColor[(int)RepStatus.CanBeFixedMIA] = CDarkYellow;
            _displayColor[(int)RepStatus.MoveToSort] = CPurple;
            _displayColor[(int)RepStatus.Delete] = CBrown;
            _displayColor[(int)RepStatus.NeededForFix] = COrange;
            _displayColor[(int)RepStatus.Rename] = COrange;

            _displayColor[(int)RepStatus.CorruptCanBeFixed] = CYellow;
            _displayColor[(int)RepStatus.MoveToCorrupt] = CPurple; //Missing

            _displayColor[(int)RepStatus.Incomplete] = CLightRed;

            _displayColor[(int)RepStatus.Deleted] = CWhite;

            for (int i = 0; i < (int)RepStatus.EndValue; i++)
            {
                _fontColor[i] = Contrasty(_displayColor[i]);
            }

            _gameGridColumnXPositions = new int[(int)RepStatus.EndValue];

            ctrRvTree.Setup(ref DB.DirRoot);

            splitContainer3_Panel1_Resize(new object(), new EventArgs());
            splitContainer4_Panel1_Resize(new object(), new EventArgs());


            _mnuContext = new ContextMenuStrip();

            ToolStripMenuItem mnuScan1 = new ToolStripMenuItem
            {
                Text = @"Scan Quick (Headers Only)",
                Tag = EScanLevel.Level1
            };
            ToolStripMenuItem mnuScan2 = new ToolStripMenuItem
            {
                Text = @"Scan",
                Tag = EScanLevel.Level2
            };
            ToolStripMenuItem mnuScan3 = new ToolStripMenuItem
            {
                Text = @"Scan Full (Complete Re-Scan)",
                Tag = EScanLevel.Level3
            };

            ToolStripMenuItem mnuDirDatSettings = new ToolStripMenuItem
            {
                Text = @"Set Dir Dat Settings",
                Tag = null
            };

            ToolStripMenuItem mnuDirMappings = new ToolStripMenuItem
            {
                Text = @"Set Dir Mappings",
                Tag = null
            };

            _mnuOpen = new ToolStripMenuItem
            {
                Text = @"Open Directory",
                Tag = null
            };

            ToolStripMenuItem mnuFixDat = new ToolStripMenuItem
            {
                Text = @"Save fix DATs",
                Tag = null
            };

            ToolStripMenuItem mnuMakeDat = new ToolStripMenuItem
            {
                Text = @"Save full DAT",
                Tag = null
            };



            _mnuContext.Items.Add(mnuScan2);
            _mnuContext.Items.Add(mnuScan1);
            _mnuContext.Items.Add(mnuScan3);
            _mnuContext.Items.Add(mnuDirDatSettings);
            _mnuContext.Items.Add(mnuDirMappings);
            _mnuContext.Items.Add(new ToolStripSeparator());
            _mnuContext.Items.Add(_mnuOpen);
            _mnuContext.Items.Add(mnuFixDat);
            _mnuContext.Items.Add(mnuMakeDat);

          
            mnuScan1.Click += MnuScan;
            mnuScan2.Click += MnuScan;
            mnuScan3.Click += MnuScan;
            mnuDirDatSettings.Click += MnuDirSettings;
            mnuDirMappings.Click += MnuDirMappings;
            _mnuOpen.Click += MnuOpenClick;
            mnuFixDat.Click += MnuMakeFixDatClick;
            mnuMakeDat.Click += MnuMakeDatClick;

            _mnuContextToSort = new ContextMenuStrip();

            ToolStripMenuItem mnuToSortScan1 = new ToolStripMenuItem
            {
                Text = @"Scan Quick (Headers Only)",
                Tag = EScanLevel.Level1
            };
            ToolStripMenuItem mnuToSortScan2 = new ToolStripMenuItem
            {
                Text = @"Scan",
                Tag = EScanLevel.Level2
            };
            ToolStripMenuItem mnuToSortScan3 = new ToolStripMenuItem
            {
                Text = @"Scan Full (Complete Re-Scan)",
                Tag = EScanLevel.Level3
            };


            _mnuToSortOpen = new ToolStripMenuItem
            {
                Text = @"Open ToSort Directory",
                Tag = null
            };

            _mnuToSortDelete = new ToolStripMenuItem
            {
                Text = @"Remove",
                Tag = null
            };

            _mnuToSortSetPrimary = new ToolStripMenuItem
            {
                Text = @"Set To Primary ToSort",
                Tag = null
            };

            _mnuToSortSetCache = new ToolStripMenuItem
            {
                Text = @"Set To Cache ToSort",
                Tag = null
            };

            _mnuToSortSetFileOnly = new ToolStripMenuItem
            {
                Text = @"Set To File Only ToSort",
                Tag = null
            };
            _mnuToSortClearFileOnly = new ToolStripMenuItem
            {
                Text = @"Clear File Only ToSort",
                Tag = null
            };

            _mnuToSortUp = new ToolStripMenuItem
            {
                Text = @"Move Up",
                Tag = null
            };

            _mnuToSortDown = new ToolStripMenuItem
            {
                Text = @"Move Down",
                Tag = null
            };

            _mnuContextToSort.Items.Add(mnuToSortScan2);
            _mnuContextToSort.Items.Add(mnuToSortScan1);
            _mnuContextToSort.Items.Add(mnuToSortScan3);
            _mnuContextToSort.Items.Add(_mnuToSortOpen);
            _mnuContextToSort.Items.Add(new ToolStripSeparator());
            _mnuContextToSort.Items.Add(_mnuToSortSetPrimary);
            _mnuContextToSort.Items.Add(_mnuToSortSetCache);
            _mnuContextToSort.Items.Add(_mnuToSortSetFileOnly);
            _mnuContextToSort.Items.Add(_mnuToSortClearFileOnly);
            _mnuContextToSort.Items.Add(_mnuToSortDelete);
            _mnuContextToSort.Items.Add(new ToolStripSeparator());
            _mnuContextToSort.Items.Add(_mnuToSortUp);
            _mnuContextToSort.Items.Add(_mnuToSortDown);

            mnuToSortScan1.Click += MnuScan;
            mnuToSortScan2.Click += MnuScan;
            mnuToSortScan3.Click += MnuScan;
            _mnuToSortOpen.Click += MnuToSortOpen;
            _mnuToSortDelete.Click += MnuToSortDelete;
            _mnuToSortSetPrimary.Click += MnuToSortSetPrimary;
            _mnuToSortSetCache.Click += MnuToSortSetCache;
            _mnuToSortSetFileOnly.Click += MnuToSortSetFileOnly;
            _mnuToSortClearFileOnly.Click += MnuToSortClearFileOnly;
            _mnuToSortUp.Click += MnuToSortUp;
            _mnuToSortDown.Click += MnuToSortDown;
                       
            chkBoxShowComplete.Checked = Settings.rvSettings.chkBoxShowComplete;
            chkBoxShowPartial.Checked = Settings.rvSettings.chkBoxShowPartial;
            chkBoxShowFixes.Checked = Settings.rvSettings.chkBoxShowFixes;
            chkBoxShowMIA.Checked = Settings.rvSettings.chkBoxShowMIA;
            chkBoxShowMerged.Checked = Settings.rvSettings.chkBoxShowMerged;

            TabArtworkInitialize();

            SetButtonPosLeft();

            tooltip.SetToolTip(btnDefault1, "Right Click: Save Tree Settings\nLeft Click: Load Tree Settings");
            tooltip.SetToolTip(btnDefault2, "Right Click: Save Tree Settings\nLeft Click: Load Tree Settings");
            tooltip.SetToolTip(btnDefault3, "Right Click: Save Tree Settings\nLeft Click: Load Tree Settings");
            tooltip.SetToolTip(btnDefault4, "Right Click: Save Tree Settings\nLeft Click: Load Tree Settings");

            tooltip.SetToolTip(btnUpdateDats, "Left Click: Dat Update\nShift Left Click: Full Dat Rescan\n\nRight Click: Open DatVault");
            tooltip.SetToolTip(btnFixFiles, "Left Click: Fix Files\nRight Click: Scan / Find Fix / Fix");

#if DEBUG
            garbageCollectToolStripMenuItem.Name = "garbageCollectToolStripMenuItem";
            garbageCollectToolStripMenuItem.Size = new Size(186, 22);
            garbageCollectToolStripMenuItem.Text = "Garbage Collect";
            garbageCollectToolStripMenuItem.Click += new EventHandler(this.garbageCollectToolStripMenuItem_Click_1);
            helpToolStripMenuItem.DropDownItems.Add(garbageCollectToolStripMenuItem);
#endif
            InitGameGridMenu();

            if (Settings.rvSettings.Darkness)
            {
                Dark.dark.SetColors(this);
                SetTextBoxHeight(gbDatInfo);
                SetTextBoxHeight(gbSetInfo);
            }
        }


        private void SetTextBoxHeight(Control c)
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


        // returns either white or black, depending of quick luminance of the Color " a "
        // called when the _displayColor is finished, in order to populate the _fontColor table.
        private static Color Contrasty(Color a)
        {
            return (a.R << 1) + a.B + a.G + (a.G << 2) < 1024 ? Color.White : Color.Black;
        }

        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        private void splitContainer3_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitDatInfoTree.Panel1.Width == 0)
                return;

            gbDatInfo.Width = splitDatInfoTree.Panel1.Width - gbDatInfo.Left * 2;
        }

        private void splitContainer4_Panel1_Resize(object sender, EventArgs e)
        {
            // fixes a rendering issue in mono
            if (splitGameInfoLists.Panel1.Width == 0)
                return;

            int chkLeft = splitGameInfoLists.Panel1.Width - 150;
            if (chkLeft < 430)
                chkLeft = 430;

            chkBoxShowComplete.Left = chkLeft;
            chkBoxShowPartial.Left = chkLeft;
            chkBoxShowEmpty.Left = chkLeft;
            chkBoxShowFixes.Left = chkLeft;
            chkBoxShowMIA.Left = chkLeft;
            chkBoxShowMerged.Left = chkLeft;
            txtFilter.Left = chkLeft;
            btnClear.Left = chkLeft + txtFilter.Width + 2;

            gbSetInfo.Width = chkLeft - gbSetInfo.Left - 10;
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_working)
            {
                e.Cancel = true;
                return;
            }
        }
        #endregion


        #region Tree
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
                if (cf != gameGridSource)
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

                _mnuContextToSort.Show(this, new Point(controLocation.X + e.X - 32, controLocation.Y + e.Y - 10));
            }
            else
            {
                _mnuOpen.Enabled = Directory.Exists(_clickedTree.FullName);
                //_mnuFile.Enabled = _clickedTree.Dat == null;
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


        #endregion


        #region popupMenus

        private void MnuScan(object sender, EventArgs e)
        {
            ScanRoms((EScanLevel)((ToolStripMenuItem)sender).Tag, _clickedTree);
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
            string tDir = _clickedTree.FullName;
            if (Directory.Exists(tDir))
                try { Process.Start(tDir); } catch { }
        }
        private void MnuMakeFixDatClick(object sender, EventArgs e)
        {
            MakeFixDat(_clickedTree, true);
        }

        private void MakeFixDat(RvFile baseDir, bool redOnly)
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
                Settings.WriteConfig(Settings.rvSettings);
            }

            FixDatReport.RecursiveDatTree(Settings.rvSettings.FixDatOutPath, baseDir, redOnly);
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

            DatHeader dh = (new ExternalDatConverterTo()).ConvertToExternalDat(_clickedTree);
            DatXMLWriter.WriteDat(browse.FileName, dh);
        }


        private void MnuToSortOpen(object sender, EventArgs e)
        {
            string tDir = _clickedTree.FullName;
            if (Directory.Exists(tDir))
                try { Process.Start(tDir); } catch { }
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


        #region TopMenu

        private void updateNewDATsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            UpdateDats();
        }
        private void updateAllDATsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            DatUpdate.CheckAllDats(DB.DirRoot.Child(0), @"DatRoot\");
            UpdateDats();
        }

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





        private void TsmFindFixesClick(object sender, EventArgs e)
        {
            if (_working) return;
            FindFixes();
        }

        private void FixFilesToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_working) return;
            FixFiles();
        }





        private void RomVaultSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_working) return;
            using (FrmSettings fcfg = new FrmSettings())
            {
                fcfg.ShowDialog(this);
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




        private void colorKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_fk == null || _fk.IsDisposed)
            {
                _fk = new FrmKey();
            }

            _fk.Show();
        }
        private void AboutRomVaultToolStripMenuItemClick(object sender, EventArgs e)
        {
            FrmHelpAbout fha = new FrmHelpAbout();
            fha.ShowDialog(this);
            fha.Dispose();
        }


        #endregion


        #region sideButtons
        private void BtnUpdateDatsMouseUp(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Shift)
            {
                DatUpdate.CheckAllDats(DB.DirRoot.Child(0), @"DatRoot\");
            }
            RootDirsCreate.CheckDatRoot();
            Start();
            UpdateDats();
            Finish();
        }
        private void BtnScanRomsClick(object sender, EventArgs e)
        {
            ScanRoms(EScanLevel.Level2);
        }

        private void btnFindFixes_MouseUp(object sender, MouseEventArgs e)
        {
            FindFixes(Control.ModifierKeys == (Keys.Shift | Keys.Control));
        }

        private void BtnFixFilesMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Automate.AutoScanFix();
                return;
            }

            FixFiles();
        }
        private void BtnReportMouseUp(object sender, MouseEventArgs e)
        {
            MakeFixDat(DB.DirRoot.Child(0), e.Button == MouseButtons.Left);
        }
        #endregion


        #region TopRight

        private void ChkBoxShowCompleteCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowComplete != this.chkBoxShowComplete.Checked)
            {
                Settings.rvSettings.chkBoxShowComplete = this.chkBoxShowComplete.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }

        private void ChkBoxShowPartialCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowPartial != this.chkBoxShowPartial.Checked)
            {
                Settings.rvSettings.chkBoxShowPartial = this.chkBoxShowPartial.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }
        private void chkBoxShowEmptyCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowEmpty != this.chkBoxShowEmpty.Checked)
            {
                Settings.rvSettings.chkBoxShowEmpty = this.chkBoxShowEmpty.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }

        private void ChkBoxShowFixesCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowFixes != this.chkBoxShowFixes.Checked)
            {
                Settings.rvSettings.chkBoxShowFixes = this.chkBoxShowFixes.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }


        private void chkBoxShowMIA_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMIA != this.chkBoxShowMIA.Checked)
            {
                Settings.rvSettings.chkBoxShowMIA = this.chkBoxShowMIA.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }

        private void ChkBoxShowMergedCheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMerged != this.chkBoxShowMerged.Checked)
            {
                Settings.rvSettings.chkBoxShowMerged = this.chkBoxShowMerged.Checked;
                Settings.WriteConfig(Settings.rvSettings);
                DatSetSelected(ctrRvTree.Selected);
            }
        }



        private void BtnClear_Click(object sender, EventArgs e)
        {
            txtFilter.Text = "";
        }


        private void TxtFilter_TextChanged(object sender, EventArgs e)
        {
            if (gameGridSource != null)
                UpdateGameGrid(gameGridSource);
            txtFilter.Focus();
        }


        private void picPayPal_Click(object sender, EventArgs e)
        {
            try { Process.Start("http://paypal.me/romvault"); } catch { }
        }

        private void picPatreon_Click(object sender, EventArgs e)
        {
            try { Process.Start("https://www.patreon.com/romvault"); } catch { }
        }

        #endregion


        #region coreFunctions

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
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat, null);
            progress.HideCancelButton();
            progress.ShowDialog(this);
            progress.Dispose();

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

        private void setPos(Form childForm)
        {
            childForm.Owner = this;
            childForm.StartPosition = FormStartPosition.Manual;
            childForm.Location = new Point(
              Location.X + (Width - childForm.Width) / 2,
              Location.Y + (Height - childForm.Height) / 2
            );
        }

        public FrmProgressWindow frmScanRoms;
        public void ScanRoms(EScanLevel sd, RvFile StartAt = null, FormClosedEventHandler fceh = null)
        {
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
            frmFixFiles = new FrmProgressWindowFix(this, closeOnExit, Finish);
            Start();
            setPos(frmFixFiles);
            if (fceh != null)
                frmFixFiles.FormClosed += fceh;
            frmFixFiles.Show();
        }

        private bool _working = false;
        private void Start()
        {
            _working = true;
            timer1.Enabled = true;
            ctrRvTree.Working = true;
            //menuStrip1.Enabled = false;
            foreach (var item in menuStrip1.Items)
            {
                if (!(item is ToolStripMenuItem menuItem))
                    continue;
                if (menuItem.Text == "Help")
                    continue;
                menuItem.Enabled = false;
            }
            btnUpdateDats.Enabled = false;
            btnScanRoms.Enabled = false;
            btnFindFixes.Enabled = false;
            btnFixFiles.Enabled = false;
            btnReport.Enabled = false;

            btnDefault1.Enabled = false;
            btnDefault2.Enabled = false;
            btnDefault3.Enabled = false;
            btnDefault4.Enabled = false;

            btnUpdateDats.BackgroundImage = rvImages.GetBitmap("btnUpdateDats_Disabled");
            btnScanRoms.BackgroundImage = rvImages.GetBitmap("btnScanRoms_Disabled");
            btnFindFixes.BackgroundImage = rvImages.GetBitmap("btnFindFixes_Disabled");
            btnFixFiles.BackgroundImage = rvImages.GetBitmap("btnFixFiles_Disabled");
            btnReport.BackgroundImage = rvImages.GetBitmap("btnReport_Disabled");
        }
        private void Finish()
        {
            _working = false;
            ctrRvTree.Working = false;
            //menuStrip1.Enabled = true;
            foreach (var item in menuStrip1.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                    menuItem.Enabled = true;
            }

            btnUpdateDats.BackgroundImage = rvImages.GetBitmap("btnUpdateDats_Enabled");
            btnScanRoms.BackgroundImage = rvImages.GetBitmap("btnScanRoms_Enabled");
            btnFindFixes.BackgroundImage = rvImages.GetBitmap("btnFindFixes_Enabled");
            btnFixFiles.BackgroundImage = rvImages.GetBitmap("btnFixFiles_Enabled");
            btnReport.BackgroundImage = rvImages.GetBitmap("btnReport_Enabled");

            btnDefault1.Enabled = true;
            btnDefault2.Enabled = true;
            btnDefault3.Enabled = true;
            btnDefault4.Enabled = true;

            btnUpdateDats.Enabled = true;
            btnScanRoms.Enabled = true;
            btnFindFixes.Enabled = true;
            btnFixFiles.Enabled = true;
            btnReport.Enabled = true;

            timer1.Enabled = false;
            DatSetSelected(ctrRvTree.Selected);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            ctrRvTree.Refresh();
            UpdateGameGrid(true);
            if (ctrRvTree.Selected != null)
                UpdateDatMetaData(ctrRvTree.Selected);
            GameGrid.Refresh();
        }


        #endregion


        #region DatDisplay

        private void DatSetSelected(RvFile cf)
        {
            ctrRvTree.Refresh();

            ClearGameGrid();

            if (cf == null)
            {
                return;
            }

            UpdateDatMetaData(cf);
            UpdateGameGrid(cf);
        }
        private void UpdateDatMetaData(RvFile tDir)
        {
            lblDITName.Text = tDir.Name;


            RvDat tDat = null;
            if (tDir.Dat != null)
                tDat = tDir.Dat;
            else if (tDir.DirDatCount == 1)
                tDat = tDir.DirDat(0);

            if (tDat != null)
            {
                if (lblDITName.Text != tDat.GetData(RvDat.DatData.DatName))
                    lblDITName.Text += $":  {tDat.GetData(RvDat.DatData.DatName)}";

                string DatId = tDat.GetData(RvDat.DatData.Id);
                if (!string.IsNullOrWhiteSpace(DatId))
                    lblDITName.Text += $" (ID:{DatId})";


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
            if (tDir.DirStatus.CountFoundMIA() > 0) { lblDITRomsGot.Text += $"  -  {tDir.DirStatus.CountFoundMIA()} Found MIA"; }
            lblDITRomsMissing.Text = tDir.DirStatus.CountMissing().ToString(CultureInfo.InvariantCulture);
            if (tDir.DirStatus.CountMIA() > 0) { lblDITRomsMissing.Text += $"  -  {tDir.DirStatus.CountMIA()} MIA"; }
            lblDITRomsFixable.Text = tDir.DirStatus.CountFixesNeeded().ToString(CultureInfo.InvariantCulture);
            lblDITRomsUnknown.Text = (tDir.DirStatus.CountUnknown() + tDir.DirStatus.CountInToSort()).ToString(CultureInfo.InvariantCulture);
        }


        private void gbDatInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 89;
            int rightPos = (int)(gbDatInfo.Width / _scaleFactorX) - 15;


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



        private void btnDefault1_MouseDown(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 1);
        }

        private void btnDefault2_MouseDown(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 2);
        }

        private void btnDefault3_MouseDown(object sender, MouseEventArgs e)
        {
            treeDefault(e.Button == MouseButtons.Right, 3);
        }

        private void btnDefault4_MouseDown(object sender, MouseEventArgs e)
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

        private void splitToolBarMain_Panel1_Resize(object sender, EventArgs e)
        {
            SetButtonPosLeft();
        }
        private void SetButtonPosLeft()
        {
            int pH = splitToolBarMain.Panel1.Height;
            if (pH < 550)
                pH = 550;

            lblTreePreSets.Top = pH - 98;
            btnDefault1.Top = pH - 82;
            btnDefault2.Top = pH - 82;
            btnDefault3.Top = pH - 42;
            btnDefault4.Top = pH - 42;
        }

        private void visitHelpWikiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try { Process.Start("https://wiki.romvault.com/doku.php?id=help"); } catch { }
        }

        private void whatsNewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try { Process.Start("https://wiki.romvault.com/doku.php?id=whats_new"); } catch { }
        }

        private void FrmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_fk != null && !_fk.IsDisposed)
                _fk.Close();

            this.Hide();
            foreach (Thread frmTrrntzip in frmTrrntzips)
                frmTrrntzip.Join();

            Environment.Exit(0);
        }

        private List<Thread> frmTrrntzips = new List<Thread>();
        private void torrentZipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread tStart = new Thread(() =>
            {
                FrmTrrntzip frmTrrntzip = new FrmTrrntzip();
                if (Settings.rvSettings.Darkness)
                    Dark.dark.SetColors(frmTrrntzip);
                Application.Run(frmTrrntzip);
            });
            frmTrrntzips.Add(tStart);
            tStart.SetApartmentState(ApartmentState.STA);
            tStart.Start();
        }

        private void garbageCollectToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            GC.Collect();
        }

    }
}