using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using DokanNet;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX
{
    public partial class frmMain : Form
    {
        private static readonly Color CRed = Color.FromArgb(255, 214, 214);
        private static readonly Color CGreen = Color.FromArgb(214, 255, 214);
        private static readonly Color CYellow = Color.FromArgb(255, 255, 214);
        private static readonly Color CGray = Color.FromArgb(214, 214, 214);

        private readonly char vDriveLetter;

        private bool _updatingGameGrid;

        private float _scaleFactorX = 1;
        private float _scaleFactorY = 1;

        private FolderBrowserDialog sortDir;

        private VDrive di;

        public frmMain()
        {
            InitializeComponent();
            Text = $@"RomVaultX {Application.StartupPath}";

            string driveLetter = AppSettings.ReadSetting("vDriveLetter");
            if (driveLetter == null)
            {
                AppSettings.AddUpdateAppSettings("vDriveLetter", "V");

                driveLetter = AppSettings.ReadSetting("vDriveLetter");
            }
            vDriveLetter = driveLetter.ToCharArray()[0];

            addGameGrid();
            string res = Program.db.ConnectToDB();

            if (!string.IsNullOrEmpty(res))
            {
                MessageBox.Show("res", "DB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }

            DatUpdate.UpdateGotTotal();
            DirTree.Setup(RvTreeRow.ReadTreeFromDB());
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            splitContainer1.SplitterDistance = (int) (splitContainer1.SplitterDistance*factor.Width);
            splitContainer2.SplitterDistance = (int) (splitContainer2.SplitterDistance*factor.Width);
            splitContainer2.Panel1MinSize = (int) (splitContainer2.Panel1MinSize*factor.Width);

            splitContainer3.SplitterDistance = (int) (splitContainer3.SplitterDistance*factor.Height);
            splitContainer4.SplitterDistance = (int) (splitContainer4.SplitterDistance*factor.Height);

            _scaleFactorX *= factor.Width;
            _scaleFactorY *= factor.Height;
        }

        private void btnUpdateDats_Click(object sender, EventArgs e)
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat);
            progress.ShowDialog(this);
            progress.Dispose();
            DirTree.Setup(RvTreeRow.ReadTreeFromDB());
            DatSetSelected(DirTree.Selected);
        }

        private void btnScanRoms_MouseUp(object sender, MouseEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                SetScanDir(false);
            }
            else
            {
                ToSortScanDir();
            }
        }


        private void ToSortScanDir()
        {
            RomScanner.RootDir = @"ToSort";
            RomScanner.DelFiles = true;
            DoScan();
        }

        private void DoScan()
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Files", RomScanner.ScanFiles);
            progress.ShowDialog(this);
            progress.Dispose();
            DirTree.Setup(RvTreeRow.ReadTreeFromDB());
            DatSetSelected(DirTree.Selected);
        }

        private void quickReScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning RomRoot Files", romRootScanner.ScanFiles);
            progress.ShowDialog(this);
            progress.Dispose();
            DirTree.Setup(RvTreeRow.ReadTreeFromDB());
            DatSetSelected(DirTree.Selected);
        }

        private void deepReScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning RomRoot Files", romRootScanner.ScanFilesDeep);
            progress.ShowDialog(this);
            progress.Dispose();
            DirTree.Setup(RvTreeRow.ReadTreeFromDB());
            DatSetSelected(DirTree.Selected);
        }


        private void DirTree_RvSelected(object sender, MouseEventArgs e)
        {
            RvTreeRow tr = (RvTreeRow) sender;
            Debug.WriteLine(tr.dirFullName);
            updateSelectedTreeRow(tr);
        }


        private void DatSetSelected(RvTreeRow cf)
        {
            DirTree.Refresh();
            GameGrid.Rows.Clear();
            RomGrid.Rows.Clear();

            if (cf == null)
            {
                return;
            }

            UpdateGameGrid(cf.DatId);
        }


        private void chkBoxShowCorrect_CheckedChanged(object sender, EventArgs e)
        {
            DatSetSelected(DirTree.Selected);
        }

        private void chkBoxShowMissing_CheckedChanged(object sender, EventArgs e)
        {
            DatSetSelected(DirTree.Selected);
        }

        private void romRootScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void updateZipDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateZipDB.UpdateDB();
        }

        private void startVDriveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Dokan.Unmount(vDriveLetter);
            di = new VDrive();
#if DEBUG
            Thread t2 = new Thread(() => { di.Mount(vDriveLetter + ":\\", DokanOptions.DebugMode, 1); });
#else
            Thread t2 = new Thread(() => { di.Mount(vDriveLetter + ":\\", DokanOptions.FixedDrive, 10); });
#endif
            t2.Start();
        }

        private void closeVDriveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Dokan.Unmount(vDriveLetter);
        }
        private void extractFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DirTree.Selected == null)
                return;

            ExtractFiles.extract(DirTree.Selected.dirFullName);
        }

        private void fixDatsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DirTree.Selected == null)
                return;
            
            FixDatList.extract(DirTree.Selected.dirFullName);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Dokan.Unmount(vDriveLetter);
        }

        private void RomGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        #region DAT dsiplay code

        private void splitContainer3_Panel1_Resize(object sender, EventArgs e)
        {
            gbDatInfo.Width = splitContainer3.Panel1.Width - gbDatInfo.Left*2;
        }


        private void gbDatInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 89;
            int rightPos = (int) (gbDatInfo.Width/_scaleFactorX) - 15;
            if (rightPos > 600)
            {
                rightPos = 600;
            }
            int width = rightPos - leftPos;
            int widthB1 = (int) ((double) width*120/340);
            int leftB2 = rightPos - widthB1;


            int backD = 97;

            width = (int) (width*_scaleFactorX);
            widthB1 = (int) (widthB1*_scaleFactorX);
            leftB2 = (int) (leftB2*_scaleFactorX);
            backD = (int) (backD*_scaleFactorX);


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

            lblDIRomsTotal.Left = leftB2 - backD;

            lblDITRomsTotal.Left = leftB2;
            lblDITRomsTotal.Width = widthB1;

            lblDIRomsNoDump.Left = leftB2 - backD;
            lblDITRomsNoDump.Left = leftB2;
            lblDITRomsNoDump.Width = widthB1;
        }


        private void updateSelectedTreeRow(RvTreeRow tr)
        {
            lblDITName.Text = tr.datName;
            lblDITPath.Text = tr.dirFullName;

            if (tr.DatId != null)
            {
                RvDat tDat = new RvDat();
                tDat.DbRead((uint) tr.DatId);
                lblDITDescription.Text = tDat.Description;
                lblDITCategory.Text = tDat.Category;
                lblDITVersion.Text = tDat.Version;
                lblDITAuthor.Text = tDat.Author;
                lblDITDate.Text = tDat.Date;
            }
            else
            {
                lblDITDescription.Text = "";
                lblDITCategory.Text = "";
                lblDITVersion.Text = "";
                lblDITAuthor.Text = "";
                lblDITDate.Text = "";
            }
            lblDITRomsGot.Text = tr.RomGot.ToString("#,0");
            lblDITRomsMissing.Text = (tr.RomTotal - tr.RomGot - tr.RomNoDump).ToString("#,0");
            lblDITRomsTotal.Text = tr.RomTotal.ToString("#,0");
            lblDITRomsNoDump.Text = tr.RomNoDump.ToString("#,0");

            UpdateGameGrid(tr.DatId);
        }

        #endregion

        #region Game display code

        private Label lblSIName;
        private Label lblSITName;

        private Label lblSIDescription;
        private Label lblSITDescription;

        private Label lblSIManufacturer;
        private Label lblSITManufacturer;

        private Label lblSICloneOf;
        private Label lblSITCloneOf;

        private Label lblSIRomOf;
        private Label lblSITRomOf;

        private Label lblSIYear;
        private Label lblSITYear;

        private Label lblSITotalRoms;
        private Label lblSITTotalRoms;

        //Trurip Extra Data
        private Label lblSIPublisher;
        private Label lblSITPublisher;

        private Label lblSIDeveloper;
        private Label lblSITDeveloper;

        private Label lblSIEdition;
        private Label lblSITEdition;

        private Label lblSIVersion;
        private Label lblSITVersion;

        private Label lblSIType;
        private Label lblSITType;

        private Label lblSIMedia;
        private Label lblSITMedia;

        private Label lblSILanguage;
        private Label lblSITLanguage;

        private Label lblSIPlayers;
        private Label lblSITPlayers;

        private Label lblSIRatings;
        private Label lblSITRatings;

        private Label lblSIGenre;
        private Label lblSITGenre;

        private Label lblSIPeripheral;
        private Label lblSITPeripheral;

        private Label lblSIBarCode;
        private Label lblSITBarCode;

        private Label lblSIMediaCatalogNumber;
        private Label lblSITMediaCatalogNumber;

        private void addGameGrid()
        {
            lblSIName = new Label {Location = SPoint(6, 15), Size = SSize(76, 13), Text = "Name :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITName = new Label {Location = SPoint(84, 14), Size = SSize(320, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIName);
            gbSetInfo.Controls.Add(lblSITName);

            lblSIDescription = new Label {Location = SPoint(6, 31), Size = SSize(76, 13), Text = "Description :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITDescription = new Label {Location = SPoint(84, 30), Size = SSize(320, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIDescription);
            gbSetInfo.Controls.Add(lblSITDescription);

            lblSIManufacturer = new Label {Location = SPoint(6, 47), Size = SSize(76, 13), Text = "Manufacturer :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITManufacturer = new Label {Location = SPoint(84, 46), Size = SSize(320, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIManufacturer);
            gbSetInfo.Controls.Add(lblSITManufacturer);

            lblSICloneOf = new Label {Location = SPoint(6, 63), Size = SSize(76, 13), Text = "Clone of :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITCloneOf = new Label {Location = SPoint(84, 62), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSICloneOf);
            gbSetInfo.Controls.Add(lblSITCloneOf);

            lblSIYear = new Label {Location = SPoint(206, 63), Size = SSize(76, 13), Text = "Year :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITYear = new Label {Location = SPoint(284, 62), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIYear);
            gbSetInfo.Controls.Add(lblSITYear);


            lblSIRomOf = new Label {Location = SPoint(6, 79), Size = SSize(76, 13), Text = "ROM of :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITRomOf = new Label {Location = SPoint(84, 78), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIRomOf);
            gbSetInfo.Controls.Add(lblSITRomOf);

            lblSITotalRoms = new Label {Location = SPoint(206, 79), Size = SSize(76, 13), Text = "Total ROMs :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITTotalRoms = new Label {Location = SPoint(284, 78), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSITotalRoms);
            gbSetInfo.Controls.Add(lblSITTotalRoms);

            //Trurip

            lblSIPublisher = new Label {Location = SPoint(6, 47), Size = SSize(76, 13), Text = "Publisher :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITPublisher = new Label {Location = SPoint(84, 46), Size = SSize(320, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIPublisher);
            gbSetInfo.Controls.Add(lblSITPublisher);

            lblSIDeveloper = new Label {Location = SPoint(6, 63), Size = SSize(76, 13), Text = "Developer :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITDeveloper = new Label {Location = SPoint(84, 62), Size = SSize(320, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIDeveloper);
            gbSetInfo.Controls.Add(lblSITDeveloper);


            lblSIEdition = new Label {Location = SPoint(6, 79), Size = SSize(76, 13), Text = "Edition :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITEdition = new Label {Location = SPoint(84, 78), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIEdition);
            gbSetInfo.Controls.Add(lblSITEdition);

            lblSIVersion = new Label {Location = SPoint(206, 79), Size = SSize(76, 13), Text = "Version :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITVersion = new Label {Location = SPoint(284, 78), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIVersion);
            gbSetInfo.Controls.Add(lblSITVersion);

            lblSIType = new Label {Location = SPoint(406, 79), Size = SSize(76, 13), Text = "Type :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITType = new Label {Location = SPoint(484, 78), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIType);
            gbSetInfo.Controls.Add(lblSITType);


            lblSIMedia = new Label {Location = SPoint(6, 95), Size = SSize(76, 13), Text = "Media :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITMedia = new Label {Location = SPoint(84, 94), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIMedia);
            gbSetInfo.Controls.Add(lblSITMedia);

            lblSILanguage = new Label {Location = SPoint(206, 95), Size = SSize(76, 13), Text = "Language :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITLanguage = new Label {Location = SPoint(284, 94), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSILanguage);
            gbSetInfo.Controls.Add(lblSITLanguage);

            lblSIPlayers = new Label {Location = SPoint(406, 95), Size = SSize(76, 13), Text = "Players :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITPlayers = new Label {Location = SPoint(484, 94), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIPlayers);
            gbSetInfo.Controls.Add(lblSITPlayers);


            lblSIRatings = new Label {Location = SPoint(6, 111), Size = SSize(76, 13), Text = "Ratings :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITRatings = new Label {Location = SPoint(84, 110), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIRatings);
            gbSetInfo.Controls.Add(lblSITRatings);

            lblSIGenre = new Label {Location = SPoint(206, 111), Size = SSize(76, 13), Text = "Genre :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITGenre = new Label {Location = SPoint(284, 110), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIGenre);
            gbSetInfo.Controls.Add(lblSITGenre);

            lblSIPeripheral = new Label {Location = SPoint(406, 111), Size = SSize(76, 13), Text = "Peripheral :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITPeripheral = new Label {Location = SPoint(484, 110), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIPeripheral);
            gbSetInfo.Controls.Add(lblSITPeripheral);


            lblSIBarCode = new Label {Location = SPoint(6, 127), Size = SSize(76, 13), Text = "Barcode :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITBarCode = new Label {Location = SPoint(84, 126), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIBarCode);
            gbSetInfo.Controls.Add(lblSITBarCode);

            lblSIMediaCatalogNumber = new Label {Location = SPoint(406, 127), Size = SSize(76, 13), Text = "Cat. No. :", TextAlign = ContentAlignment.TopRight, Visible = false};
            lblSITMediaCatalogNumber = new Label {Location = SPoint(484, 126), Size = SSize(120, 17), BorderStyle = BorderStyle.FixedSingle, Visible = false};
            gbSetInfo.Controls.Add(lblSIMediaCatalogNumber);
            gbSetInfo.Controls.Add(lblSITMediaCatalogNumber);
        }

        private Point SPoint(int x, int y)
        {
            return new Point((int) (x*_scaleFactorX), (int) (y*_scaleFactorY));
        }

        private Size SSize(int x, int y)
        {
            return new Size((int) (x*_scaleFactorX), (int) (y*_scaleFactorY));
        }

        private void splitContainer4_Panel1_Resize(object sender, EventArgs e)
        {
            int chkLeft = splitContainer4.Panel1.Width - 150;
            if (chkLeft < 430)
            {
                chkLeft = 430;
            }

            chkBoxShowCorrect.Left = chkLeft;
            chkBoxShowMissing.Left = chkLeft;

            gbSetInfo.Width = chkLeft - gbSetInfo.Left - 10;
        }

        private void gbSetInfo_Resize(object sender, EventArgs e)
        {
            int leftPos = 84;
            int rightPos = gbSetInfo.Width - 15;
            if (rightPos > 750)
            {
                rightPos = 750;
            }
            int width = rightPos - leftPos;

            int widthB1 = (int) ((double) width*120/340);
            int leftB2 = leftPos + width - widthB1;

            if (lblSITName == null)
            {
                return;
            }

            lblSITName.Width = width;
            lblSITDescription.Width = width;
            lblSITManufacturer.Width = width;

            lblSITCloneOf.Width = widthB1;

            lblSIYear.Left = leftB2 - 78;
            lblSITYear.Left = leftB2;
            lblSITYear.Width = widthB1;

            lblSITRomOf.Width = widthB1;

            lblSITotalRoms.Left = leftB2 - 78;
            lblSITTotalRoms.Left = leftB2;
            lblSITTotalRoms.Width = widthB1;

            lblSITPublisher.Width = width;
            lblSITDeveloper.Width = width;

            int width3 = (int) (width*0.24);
            int P2 = (int) (width*0.38);

            int width4 = (int) (width*0.24);

            lblSITEdition.Width = width3;

            lblSIVersion.Left = leftPos + P2 - 78;
            lblSITVersion.Left = leftPos + P2;
            lblSITVersion.Width = width3;

            lblSIType.Left = leftPos + width - width3 - 78;
            lblSITType.Left = leftPos + width - width3;
            lblSITType.Width = width3;


            lblSITMedia.Width = width3;

            lblSILanguage.Left = leftPos + P2 - 78;
            lblSITLanguage.Left = leftPos + P2;
            lblSITLanguage.Width = width3;

            lblSIPlayers.Left = leftPos + width - width3 - 78;
            lblSITPlayers.Left = leftPos + width - width3;
            lblSITPlayers.Width = width3;

            lblSITRatings.Width = width3;

            lblSIGenre.Left = leftPos + P2 - 78;
            lblSITGenre.Left = leftPos + P2;
            lblSITGenre.Width = width3;

            lblSIPeripheral.Left = leftPos + width - width3 - 78;
            lblSITPeripheral.Left = leftPos + width - width3;
            lblSITPeripheral.Width = width3;


            lblSITBarCode.Width = width4;

            lblSIMediaCatalogNumber.Left = leftPos + width - width4 - 78;
            lblSITMediaCatalogNumber.Left = leftPos + width - width4;
            lblSITMediaCatalogNumber.Width = width4;
        }


        private void UpdateGameGrid(uint? DatId)
        {
            _updatingGameGrid = true;
            GameGrid.Rows.Clear();
            RomGrid.Rows.Clear();

            if (DatId == null)
            {
                return;
            }

            List<rvGameGridRow> rows = rvGameGridRow.ReadGames((int) DatId);

            foreach (rvGameGridRow row in rows)
            {
                bool gCorrect = row.HasCorrect();
                bool gMissing = row.HasMissing();

                bool show = chkBoxShowCorrect.Checked && gCorrect && !gMissing;
                show = show || (chkBoxShowMissing.Checked && gMissing);
                show = show || !(gCorrect || gMissing);

                if (!show)
                {
                    continue;
                }

                GameGrid.Rows.Add();
                int iRow = GameGrid.Rows.Count - 1;

                Color cellColor;
                if (row.RomGot >= row.RomTotal - row.RomNoDump)
                {
                    cellColor = CGreen;
                }
                else if ((row.RomGot > 0) && (row.RomTotal - row.RomNoDump > 0))
                {
                    cellColor = CYellow;
                }
                else if ((row.RomGot == 0) && (row.RomTotal - row.RomNoDump > 0))
                {
                    cellColor = CRed;
                }
                else
                {
                    cellColor = CGray;
                }

                GameGrid.Rows[iRow].Selected = false;
                GameGrid.Rows[iRow].Tag = row.GameId;
                GameGrid.Rows[iRow].Cells[1].Value = row.Name;
                GameGrid.Rows[iRow].Cells[2].Value = row.Description;
                GameGrid.Rows[iRow].Cells[3].Value = row.RomGot;
                if (row.RomNoDump > 0)
                {
                    GameGrid.Rows[iRow].Cells[4].Value = row.RomTotal - row.RomGot - row.RomNoDump + " , (" + row.RomNoDump + ")";
                }
                else
                {
                    GameGrid.Rows[iRow].Cells[4].Value = row.RomTotal - row.RomGot;
                }

                for (int i = 0; i < 5; i++)
                {
                    GameGrid.Rows[iRow].DefaultCellStyle.BackColor = cellColor;
                }
            }
            _updatingGameGrid = false;
            UpdateSelectedGame();
        }

        private void GameGrid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectedGame();
        }

        private void UpdateSelectedGame()
        {
            RvGame tGame = null;

            if (_updatingGameGrid)
            {
                return;
            }

            if (GameGrid.SelectedRows.Count == 1)
            {
                int GameId = (int) GameGrid.SelectedRows[0].Tag;
                tGame = new RvGame();
                tGame.DBRead(GameId);
            }
            if (tGame == null)
            {
                lblSIName.Visible = false;
                lblSITName.Visible = false;

                lblSIDescription.Visible = false;
                lblSITDescription.Visible = false;

                lblSIManufacturer.Visible = false;
                lblSITManufacturer.Visible = false;

                lblSICloneOf.Visible = false;
                lblSITCloneOf.Visible = false;

                lblSIRomOf.Visible = false;
                lblSITRomOf.Visible = false;

                lblSIYear.Visible = false;
                lblSITYear.Visible = false;

                lblSITotalRoms.Visible = false;
                lblSITTotalRoms.Visible = false;

                // Trurip

                lblSIPublisher.Visible = false;
                lblSITPublisher.Visible = false;

                lblSIDeveloper.Visible = false;
                lblSITDeveloper.Visible = false;

                lblSIEdition.Visible = false;
                lblSITEdition.Visible = false;

                lblSIVersion.Visible = false;
                lblSITVersion.Visible = false;

                lblSIType.Visible = false;
                lblSITType.Visible = false;

                lblSIMedia.Visible = false;
                lblSITMedia.Visible = false;

                lblSILanguage.Visible = false;
                lblSITLanguage.Visible = false;

                lblSIPlayers.Visible = false;
                lblSITPlayers.Visible = false;

                lblSIRatings.Visible = false;
                lblSITRatings.Visible = false;

                lblSIGenre.Visible = false;
                lblSITGenre.Visible = false;

                lblSIPeripheral.Visible = false;
                lblSITPeripheral.Visible = false;

                lblSIBarCode.Visible = false;
                lblSITBarCode.Visible = false;

                lblSIMediaCatalogNumber.Visible = false;
                lblSITMediaCatalogNumber.Visible = false;

                return;
            }

            lblSIName.Visible = true;
            lblSITName.Visible = true;
            lblSITName.Text = GameGrid.SelectedRows[0].Cells[1].Value.ToString();

            if (tGame.IsTrurip)
            {
                lblSIDescription.Visible = true;
                lblSITDescription.Visible = true;
                lblSITDescription.Text = tGame.Description;

                lblSIManufacturer.Visible = false;
                lblSITManufacturer.Visible = false;

                lblSICloneOf.Visible = false;
                lblSITCloneOf.Visible = false;

                lblSIRomOf.Visible = false;
                lblSITRomOf.Visible = false;

                lblSIYear.Visible = false;
                lblSITYear.Visible = false;

                lblSITotalRoms.Visible = false;
                lblSITTotalRoms.Visible = false;


                lblSIPublisher.Visible = true;
                lblSITPublisher.Visible = true;
                lblSITPublisher.Text = tGame.Publisher;

                lblSIDeveloper.Visible = true;
                lblSITDeveloper.Visible = true;
                lblSITDeveloper.Text = tGame.Developer;

                lblSIEdition.Visible = true;
                lblSITEdition.Visible = true;
                lblSITEdition.Text = tGame.Edition;

                lblSIVersion.Visible = true;
                lblSITVersion.Visible = true;
                lblSITVersion.Text = tGame.Version;

                lblSIType.Visible = true;
                lblSITType.Visible = true;
                lblSITType.Text = tGame.Type;

                lblSIMedia.Visible = true;
                lblSITMedia.Visible = true;
                lblSITMedia.Text = tGame.Media;

                lblSILanguage.Visible = true;
                lblSITLanguage.Visible = true;
                lblSITLanguage.Text = tGame.Language;

                lblSIPlayers.Visible = true;
                lblSITPlayers.Visible = true;
                lblSITPlayers.Text = tGame.Players;

                lblSIRatings.Visible = true;
                lblSITRatings.Visible = true;
                lblSITRatings.Text = tGame.Ratings;

                lblSIGenre.Visible = true;
                lblSITGenre.Visible = true;
                lblSITGenre.Text = tGame.Genre;

                lblSIPeripheral.Visible = true;
                lblSITPeripheral.Visible = true;
                lblSITPeripheral.Text = tGame.Peripheral;

                lblSIBarCode.Visible = true;
                lblSITBarCode.Visible = true;
                lblSITBarCode.Text = tGame.BarCode;

                lblSIMediaCatalogNumber.Visible = true;
                lblSITMediaCatalogNumber.Visible = true;
                lblSITMediaCatalogNumber.Text = tGame.MediaCatalogNumber;
            }
            else
            {
                lblSIDescription.Visible = true;
                lblSITDescription.Visible = true;
                lblSITDescription.Text = tGame.Description;

                lblSIManufacturer.Visible = true;
                lblSITManufacturer.Visible = true;
                lblSITManufacturer.Text = tGame.Manufacturer;

                lblSICloneOf.Visible = true;
                lblSITCloneOf.Visible = true;
                lblSITCloneOf.Text = tGame.CloneOf;

                lblSIRomOf.Visible = true;
                lblSITRomOf.Visible = true;
                lblSITRomOf.Text = tGame.RomOf;

                lblSIYear.Visible = true;
                lblSITYear.Visible = true;
                lblSITYear.Text = tGame.Year;

                lblSITotalRoms.Visible = true;
                lblSITTotalRoms.Visible = true;


                lblSIPublisher.Visible = false;
                lblSITPublisher.Visible = false;

                lblSIDeveloper.Visible = false;
                lblSITDeveloper.Visible = false;

                lblSIEdition.Visible = false;
                lblSITEdition.Visible = false;

                lblSIVersion.Visible = false;
                lblSITVersion.Visible = false;

                lblSIType.Visible = false;
                lblSITType.Visible = false;

                lblSIMedia.Visible = false;
                lblSITMedia.Visible = false;

                lblSILanguage.Visible = false;
                lblSITLanguage.Visible = false;

                lblSIPlayers.Visible = false;
                lblSITPlayers.Visible = false;

                lblSIRatings.Visible = false;
                lblSITRatings.Visible = false;

                lblSIGenre.Visible = false;
                lblSITGenre.Visible = false;

                lblSIPeripheral.Visible = false;
                lblSITPeripheral.Visible = false;

                lblSIBarCode.Visible = false;
                lblSITBarCode.Visible = false;

                lblSIMediaCatalogNumber.Visible = false;
                lblSITMediaCatalogNumber.Visible = false;
            }


            UpdateRomGrid(tGame.GameId);
        }

        #endregion

        #region Rom display code

        private void UpdateRomGrid(uint GameId)
        {
            RomGrid.Rows.Clear();

            IEnumerable<RvRom> roms = RvRom.ReadRoms(GameId);

            foreach (RvRom rom in roms)
            {
                RomGrid.Rows.Add();
                int iRow = RomGrid.Rows.Count - 1;

                RomGrid.Rows[iRow].Selected = false;
                RomGrid.Rows[iRow].Tag = rom.RomId;
                RomGrid.Rows[iRow].Cells[1].Value = rom.Name;
                if (rom.Size != null)
                {
                    RomGrid.Rows[iRow].Cells[2].Value = rom.Size;
                }
                else if (rom.FileSize != null)
                {
                    RomGrid.Rows[iRow].Cells[2].Style.ForeColor = Color.FromArgb(0, 0, 255);
                    RomGrid.Rows[iRow].Cells[2].Value = rom.Size;
                }

                if (rom.FileCompressedSize != null)
                {
                    RomGrid.Rows[iRow].Cells[3].Style.ForeColor = Color.FromArgb(0, 0, 255);
                    RomGrid.Rows[iRow].Cells[3].Value = rom.FileCompressedSize;
                }

                RomGrid.Rows[iRow].Cells[4].Value = rom.Merge;

                if (rom.CRC != null)
                {
                    RomGrid.Rows[iRow].Cells[5].Value = VarFix.ToString(rom.CRC);
                }
                else if (rom.FileCRC != null)
                {
                    RomGrid.Rows[iRow].Cells[5].Style.ForeColor = Color.FromArgb(0, 0, 255);
                    RomGrid.Rows[iRow].Cells[5].Value = VarFix.ToString(rom.FileCRC);
                }

                if (rom.SHA1 != null)
                {
                    RomGrid.Rows[iRow].Cells[6].Value = VarFix.ToString(rom.SHA1);
                }
                else if (rom.FileSHA1 != null)
                {
                    RomGrid.Rows[iRow].Cells[6].Style.ForeColor = Color.FromArgb(0, 0, 255);
                    RomGrid.Rows[iRow].Cells[6].Value = VarFix.ToString(rom.FileSHA1);
                }
                if (rom.MD5 != null)
                {
                    RomGrid.Rows[iRow].Cells[7].Value = VarFix.ToString(rom.MD5);
                }
                else if (rom.FileMD5 != null)
                {
                    RomGrid.Rows[iRow].Cells[7].Style.ForeColor = Color.FromArgb(0, 0, 255);
                    RomGrid.Rows[iRow].Cells[7].Value = VarFix.ToString(rom.FileMD5);
                }
                RomGrid.Rows[iRow].Cells[8].Value = rom.Status;
                RomGrid.Rows[iRow].Cells[9].Value = rom.PutInZip;

                if (((rom.Status == "nodump") && (rom.CRC == null) && (rom.SHA1 == null) && (rom.MD5 == null) && (rom.FileId == null)) || !rom.PutInZip)
                {
                    RomGrid.Rows[iRow].DefaultCellStyle.BackColor = CGray;
                }
                else
                {
                    RomGrid.Rows[iRow].DefaultCellStyle.BackColor = rom.FileId != null ? CGreen : CRed;
                }
            }
        }

        private void RomGrid_SelectionChanged(object sender, EventArgs e)
        {
            RomGrid.ClearSelection();
        }


        #endregion

        private void scanADirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetScanDir(false);
        }

        private void scanWithDeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetScanDir(true);
        }

        private void SetScanDir(bool withDelete)
        {
            if (sortDir == null)
            {
                sortDir = new FolderBrowserDialog { ShowNewFolderButton = false };
            }

            DialogResult result = sortDir.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            RomScanner.RootDir = sortDir.SelectedPath;
            RomScanner.DelFiles = withDelete;
            DoScan();
        }

    }
}