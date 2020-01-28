namespace ROMVault
{
    partial class FrmMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMain));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitToolBarMain = new System.Windows.Forms.SplitContainer();
            this.btnReport = new System.Windows.Forms.Button();
            this.btnFixFiles = new System.Windows.Forms.Button();
            this.btnFindFixes = new System.Windows.Forms.Button();
            this.btnScanRoms = new System.Windows.Forms.Button();
            this.btnUpdateDats = new System.Windows.Forms.Button();
            this.splitDatInfoGameInfo = new System.Windows.Forms.SplitContainer();
            this.splitDatInfoTree = new System.Windows.Forms.SplitContainer();
            this.gbDatInfo = new System.Windows.Forms.GroupBox();
            this.lblDIRomsUnknown = new System.Windows.Forms.Label();
            this.lblDIROMsGot = new System.Windows.Forms.Label();
            this.lblDITRomsUnknown = new System.Windows.Forms.TextBox();
            this.lblDITRomsFixable = new System.Windows.Forms.TextBox();
            this.lblDITRomsMissing = new System.Windows.Forms.TextBox();
            this.lblDITRomsGot = new System.Windows.Forms.TextBox();
            this.lblDIRomPath = new System.Windows.Forms.Label();
            this.lblDITPath = new System.Windows.Forms.TextBox();
            this.lblDIDate = new System.Windows.Forms.Label();
            this.lblDIAuthor = new System.Windows.Forms.Label();
            this.lblDITDate = new System.Windows.Forms.TextBox();
            this.lblDITAuthor = new System.Windows.Forms.TextBox();
            this.lblDIVersion = new System.Windows.Forms.Label();
            this.lblDICategory = new System.Windows.Forms.Label();
            this.lblDITVersion = new System.Windows.Forms.TextBox();
            this.lblDITCategory = new System.Windows.Forms.TextBox();
            this.lblDIDescription = new System.Windows.Forms.Label();
            this.lblDIName = new System.Windows.Forms.Label();
            this.lblDITDescription = new System.Windows.Forms.TextBox();
            this.lblDITName = new System.Windows.Forms.TextBox();
            this.lblDIRomsFixable = new System.Windows.Forms.Label();
            this.lblDIROMsMissing = new System.Windows.Forms.Label();
            this.DirTree = new ROMVault.RvTree();
            this.splitGameInfoLists = new System.Windows.Forms.SplitContainer();
            this.btnClear = new System.Windows.Forms.Button();
            this.txtFilter = new System.Windows.Forms.TextBox();
            this.picPatreon = new System.Windows.Forms.PictureBox();
            this.picPayPal = new System.Windows.Forms.PictureBox();
            this.chkBoxShowMerged = new System.Windows.Forms.CheckBox();
            this.chkBoxShowFixed = new System.Windows.Forms.CheckBox();
            this.chkBoxShowMissing = new System.Windows.Forms.CheckBox();
            this.chkBoxShowCorrect = new System.Windows.Forms.CheckBox();
            this.gbSetInfo = new System.Windows.Forms.GroupBox();
            this.splitListArt = new System.Windows.Forms.SplitContainer();
            this.splitGameListRomList = new System.Windows.Forms.SplitContainer();
            this.GameGrid = new System.Windows.Forms.DataGridView();
            this.Type = new System.Windows.Forms.DataGridViewImageColumn();
            this.CGame = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CCorrect = new System.Windows.Forms.DataGridViewImageColumn();
            this.RomGrid = new System.Windows.Forms.DataGridView();
            this.CGot = new System.Windows.Forms.DataGridViewImageColumn();
            this.CRom = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CMerge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CCRC32 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CSHA1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CMD5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CAltSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CAltCRC32 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CAltSHA1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CAltMD5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ZipIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ZipHeader = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TabEmuArc = new System.Windows.Forms.TabControl();
            this.tabArtWork = new System.Windows.Forms.TabPage();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.picArtwork = new System.Windows.Forms.PictureBox();
            this.tabScreens = new System.Windows.Forms.TabPage();
            this.picScreenShot = new System.Windows.Forms.PictureBox();
            this.picScreenTitle = new System.Windows.Forms.PictureBox();
            this.tabInfo = new System.Windows.Forms.TabPage();
            this.txtInfo = new System.Windows.Forms.RichTextBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.tsmUpdateDATs = new System.Windows.Forms.ToolStripMenuItem();
            this.addToSortToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmScanROMs = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmScanLevel1 = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmScanLevel2 = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmScanLevel3 = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmFindFixes = new System.Windows.Forms.ToolStripMenuItem();
            this.FixROMsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.romVaultSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.directorySettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.registrationSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reportsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fixDatReportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fullReportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fixReportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.colorKeyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutRomVaultToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dataGridViewImageColumn1 = new System.Windows.Forms.DataGridViewImageColumn();
            this.dataGridViewImageColumn2 = new System.Windows.Forms.DataGridViewImageColumn();
            this.dataGridViewImageColumn3 = new System.Windows.Forms.DataGridViewImageColumn();
            ((System.ComponentModel.ISupportInitialize)(this.splitToolBarMain)).BeginInit();
            this.splitToolBarMain.Panel1.SuspendLayout();
            this.splitToolBarMain.Panel2.SuspendLayout();
            this.splitToolBarMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitDatInfoGameInfo)).BeginInit();
            this.splitDatInfoGameInfo.Panel1.SuspendLayout();
            this.splitDatInfoGameInfo.Panel2.SuspendLayout();
            this.splitDatInfoGameInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitDatInfoTree)).BeginInit();
            this.splitDatInfoTree.Panel1.SuspendLayout();
            this.splitDatInfoTree.Panel2.SuspendLayout();
            this.splitDatInfoTree.SuspendLayout();
            this.gbDatInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitGameInfoLists)).BeginInit();
            this.splitGameInfoLists.Panel1.SuspendLayout();
            this.splitGameInfoLists.Panel2.SuspendLayout();
            this.splitGameInfoLists.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPatreon)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPayPal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitListArt)).BeginInit();
            this.splitListArt.Panel1.SuspendLayout();
            this.splitListArt.Panel2.SuspendLayout();
            this.splitListArt.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitGameListRomList)).BeginInit();
            this.splitGameListRomList.Panel1.SuspendLayout();
            this.splitGameListRomList.Panel2.SuspendLayout();
            this.splitGameListRomList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GameGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.RomGrid)).BeginInit();
            this.TabEmuArc.SuspendLayout();
            this.tabArtWork.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picArtwork)).BeginInit();
            this.tabScreens.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picScreenShot)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picScreenTitle)).BeginInit();
            this.tabInfo.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitToolBarMain
            // 
            this.splitToolBarMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitToolBarMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitToolBarMain.IsSplitterFixed = true;
            this.splitToolBarMain.Location = new System.Drawing.Point(0, 24);
            this.splitToolBarMain.Name = "splitToolBarMain";
            // 
            // splitToolBarMain.Panel1
            // 
            this.splitToolBarMain.Panel1.BackColor = System.Drawing.Color.White;
            this.splitToolBarMain.Panel1.Controls.Add(this.btnReport);
            this.splitToolBarMain.Panel1.Controls.Add(this.btnFixFiles);
            this.splitToolBarMain.Panel1.Controls.Add(this.btnFindFixes);
            this.splitToolBarMain.Panel1.Controls.Add(this.btnScanRoms);
            this.splitToolBarMain.Panel1.Controls.Add(this.btnUpdateDats);
            // 
            // splitToolBarMain.Panel2
            // 
            this.splitToolBarMain.Panel2.Controls.Add(this.splitDatInfoGameInfo);
            this.splitToolBarMain.Size = new System.Drawing.Size(1264, 738);
            this.splitToolBarMain.SplitterDistance = 80;
            this.splitToolBarMain.TabIndex = 5;
            // 
            // btnReport
            // 
            this.btnReport.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnReport.BackgroundImage")));
            this.btnReport.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnReport.Location = new System.Drawing.Point(0, 320);
            this.btnReport.Name = "btnReport";
            this.btnReport.Size = new System.Drawing.Size(80, 80);
            this.btnReport.TabIndex = 13;
            this.btnReport.Text = "Generate Reports";
            this.btnReport.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnReport.UseVisualStyleBackColor = true;
            this.btnReport.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnReport_MouseUp);
            // 
            // btnFixFiles
            // 
            this.btnFixFiles.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnFixFiles.BackgroundImage")));
            this.btnFixFiles.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnFixFiles.Location = new System.Drawing.Point(0, 240);
            this.btnFixFiles.Name = "btnFixFiles";
            this.btnFixFiles.Size = new System.Drawing.Size(80, 80);
            this.btnFixFiles.TabIndex = 10;
            this.btnFixFiles.Text = "Fix ROMs";
            this.btnFixFiles.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnFixFiles.UseVisualStyleBackColor = true;
            this.btnFixFiles.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnFixFiles_MouseUp);
            // 
            // btnFindFixes
            // 
            this.btnFindFixes.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnFindFixes.BackgroundImage")));
            this.btnFindFixes.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnFindFixes.Location = new System.Drawing.Point(0, 160);
            this.btnFindFixes.Name = "btnFindFixes";
            this.btnFindFixes.Size = new System.Drawing.Size(80, 80);
            this.btnFindFixes.TabIndex = 9;
            this.btnFindFixes.Text = "Find Fixes";
            this.btnFindFixes.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnFindFixes.UseVisualStyleBackColor = true;
            this.btnFindFixes.Click += new System.EventHandler(this.BtnFindFixesClick);
            // 
            // btnScanRoms
            // 
            this.btnScanRoms.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnScanRoms.BackgroundImage")));
            this.btnScanRoms.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnScanRoms.Location = new System.Drawing.Point(0, 80);
            this.btnScanRoms.Name = "btnScanRoms";
            this.btnScanRoms.Size = new System.Drawing.Size(80, 80);
            this.btnScanRoms.TabIndex = 8;
            this.btnScanRoms.Text = "Scan ROMs";
            this.btnScanRoms.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnScanRoms.UseVisualStyleBackColor = true;
            this.btnScanRoms.Click += new System.EventHandler(this.BtnScanRomsClick);
            // 
            // btnUpdateDats
            // 
            this.btnUpdateDats.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnUpdateDats.BackgroundImage")));
            this.btnUpdateDats.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnUpdateDats.Location = new System.Drawing.Point(0, 0);
            this.btnUpdateDats.Name = "btnUpdateDats";
            this.btnUpdateDats.Size = new System.Drawing.Size(80, 80);
            this.btnUpdateDats.TabIndex = 0;
            this.btnUpdateDats.Text = "Update DATs";
            this.btnUpdateDats.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnUpdateDats.UseVisualStyleBackColor = true;
            this.btnUpdateDats.Click += new System.EventHandler(this.BtnUpdateDatsClick);
            // 
            // splitDatInfoGameInfo
            // 
            this.splitDatInfoGameInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitDatInfoGameInfo.Location = new System.Drawing.Point(0, 0);
            this.splitDatInfoGameInfo.Name = "splitDatInfoGameInfo";
            // 
            // splitDatInfoGameInfo.Panel1
            // 
            this.splitDatInfoGameInfo.Panel1.Controls.Add(this.splitDatInfoTree);
            this.splitDatInfoGameInfo.Panel1MinSize = 450;
            // 
            // splitDatInfoGameInfo.Panel2
            // 
            this.splitDatInfoGameInfo.Panel2.BackColor = System.Drawing.SystemColors.Control;
            this.splitDatInfoGameInfo.Panel2.Controls.Add(this.splitGameInfoLists);
            this.splitDatInfoGameInfo.Size = new System.Drawing.Size(1180, 738);
            this.splitDatInfoGameInfo.SplitterDistance = 479;
            this.splitDatInfoGameInfo.TabIndex = 0;
            // 
            // splitDatInfoTree
            // 
            this.splitDatInfoTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitDatInfoTree.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitDatInfoTree.IsSplitterFixed = true;
            this.splitDatInfoTree.Location = new System.Drawing.Point(0, 0);
            this.splitDatInfoTree.Name = "splitDatInfoTree";
            this.splitDatInfoTree.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitDatInfoTree.Panel1
            // 
            this.splitDatInfoTree.Panel1.Controls.Add(this.gbDatInfo);
            this.splitDatInfoTree.Panel1.Resize += new System.EventHandler(this.splitContainer3_Panel1_Resize);
            // 
            // splitDatInfoTree.Panel2
            // 
            this.splitDatInfoTree.Panel2.Controls.Add(this.DirTree);
            this.splitDatInfoTree.Size = new System.Drawing.Size(479, 738);
            this.splitDatInfoTree.SplitterDistance = 148;
            this.splitDatInfoTree.TabIndex = 0;
            // 
            // gbDatInfo
            // 
            this.gbDatInfo.Controls.Add(this.lblDIRomsUnknown);
            this.gbDatInfo.Controls.Add(this.lblDIROMsGot);
            this.gbDatInfo.Controls.Add(this.lblDITRomsUnknown);
            this.gbDatInfo.Controls.Add(this.lblDITRomsFixable);
            this.gbDatInfo.Controls.Add(this.lblDITRomsMissing);
            this.gbDatInfo.Controls.Add(this.lblDITRomsGot);
            this.gbDatInfo.Controls.Add(this.lblDIRomPath);
            this.gbDatInfo.Controls.Add(this.lblDITPath);
            this.gbDatInfo.Controls.Add(this.lblDIDate);
            this.gbDatInfo.Controls.Add(this.lblDIAuthor);
            this.gbDatInfo.Controls.Add(this.lblDITDate);
            this.gbDatInfo.Controls.Add(this.lblDITAuthor);
            this.gbDatInfo.Controls.Add(this.lblDIVersion);
            this.gbDatInfo.Controls.Add(this.lblDICategory);
            this.gbDatInfo.Controls.Add(this.lblDITVersion);
            this.gbDatInfo.Controls.Add(this.lblDITCategory);
            this.gbDatInfo.Controls.Add(this.lblDIDescription);
            this.gbDatInfo.Controls.Add(this.lblDIName);
            this.gbDatInfo.Controls.Add(this.lblDITDescription);
            this.gbDatInfo.Controls.Add(this.lblDITName);
            this.gbDatInfo.Controls.Add(this.lblDIRomsFixable);
            this.gbDatInfo.Controls.Add(this.lblDIROMsMissing);
            this.gbDatInfo.Location = new System.Drawing.Point(5, 0);
            this.gbDatInfo.Name = "gbDatInfo";
            this.gbDatInfo.Size = new System.Drawing.Size(468, 147);
            this.gbDatInfo.TabIndex = 3;
            this.gbDatInfo.TabStop = false;
            this.gbDatInfo.Text = "Dat Info :";
            this.gbDatInfo.Resize += new System.EventHandler(this.gbDatInfo_Resize);
            // 
            // lblDIRomsUnknown
            // 
            this.lblDIRomsUnknown.Location = new System.Drawing.Point(214, 121);
            this.lblDIRomsUnknown.Name = "lblDIRomsUnknown";
            this.lblDIRomsUnknown.Size = new System.Drawing.Size(92, 13);
            this.lblDIRomsUnknown.TabIndex = 26;
            this.lblDIRomsUnknown.Text = "ROMs Unknown :";
            this.lblDIRomsUnknown.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIROMsGot
            // 
            this.lblDIROMsGot.Location = new System.Drawing.Point(10, 105);
            this.lblDIROMsGot.Name = "lblDIROMsGot";
            this.lblDIROMsGot.Size = new System.Drawing.Size(75, 13);
            this.lblDIROMsGot.TabIndex = 23;
            this.lblDIROMsGot.Text = "ROMs Got :";
            this.lblDIROMsGot.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITRomsUnknown
            // 
            this.lblDITRomsUnknown.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsUnknown.Location = new System.Drawing.Point(311, 120);
            this.lblDITRomsUnknown.Name = "lblDITRomsUnknown";
            this.lblDITRomsUnknown.ReadOnly = true;
            this.lblDITRomsUnknown.Size = new System.Drawing.Size(120, 20);
            this.lblDITRomsUnknown.TabIndex = 27;
            this.lblDITRomsUnknown.TabStop = false;
            // 
            // lblDITRomsFixable
            // 
            this.lblDITRomsFixable.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsFixable.Location = new System.Drawing.Point(311, 104);
            this.lblDITRomsFixable.Name = "lblDITRomsFixable";
            this.lblDITRomsFixable.ReadOnly = true;
            this.lblDITRomsFixable.Size = new System.Drawing.Size(120, 20);
            this.lblDITRomsFixable.TabIndex = 28;
            this.lblDITRomsFixable.TabStop = false;
            // 
            // lblDITRomsMissing
            // 
            this.lblDITRomsMissing.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsMissing.Location = new System.Drawing.Point(89, 120);
            this.lblDITRomsMissing.Name = "lblDITRomsMissing";
            this.lblDITRomsMissing.ReadOnly = true;
            this.lblDITRomsMissing.Size = new System.Drawing.Size(120, 20);
            this.lblDITRomsMissing.TabIndex = 29;
            this.lblDITRomsMissing.TabStop = false;
            // 
            // lblDITRomsGot
            // 
            this.lblDITRomsGot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsGot.Location = new System.Drawing.Point(89, 104);
            this.lblDITRomsGot.Name = "lblDITRomsGot";
            this.lblDITRomsGot.ReadOnly = true;
            this.lblDITRomsGot.Size = new System.Drawing.Size(120, 20);
            this.lblDITRomsGot.TabIndex = 30;
            this.lblDITRomsGot.TabStop = false;
            // 
            // lblDIRomPath
            // 
            this.lblDIRomPath.Location = new System.Drawing.Point(10, 79);
            this.lblDIRomPath.Name = "lblDIRomPath";
            this.lblDIRomPath.Size = new System.Drawing.Size(75, 13);
            this.lblDIRomPath.TabIndex = 15;
            this.lblDIRomPath.Text = "ROM Path :";
            this.lblDIRomPath.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITPath
            // 
            this.lblDITPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITPath.Location = new System.Drawing.Point(89, 78);
            this.lblDITPath.Name = "lblDITPath";
            this.lblDITPath.ReadOnly = true;
            this.lblDITPath.Size = new System.Drawing.Size(342, 20);
            this.lblDITPath.TabIndex = 31;
            this.lblDITPath.TabStop = false;
            // 
            // lblDIDate
            // 
            this.lblDIDate.Location = new System.Drawing.Point(214, 63);
            this.lblDIDate.Name = "lblDIDate";
            this.lblDIDate.Size = new System.Drawing.Size(92, 13);
            this.lblDIDate.TabIndex = 12;
            this.lblDIDate.Text = "Date :";
            this.lblDIDate.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIAuthor
            // 
            this.lblDIAuthor.Location = new System.Drawing.Point(10, 63);
            this.lblDIAuthor.Name = "lblDIAuthor";
            this.lblDIAuthor.Size = new System.Drawing.Size(75, 13);
            this.lblDIAuthor.TabIndex = 11;
            this.lblDIAuthor.Text = "Author :";
            this.lblDIAuthor.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITDate
            // 
            this.lblDITDate.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITDate.Location = new System.Drawing.Point(311, 62);
            this.lblDITDate.Name = "lblDITDate";
            this.lblDITDate.ReadOnly = true;
            this.lblDITDate.Size = new System.Drawing.Size(120, 20);
            this.lblDITDate.TabIndex = 32;
            this.lblDITDate.TabStop = false;
            // 
            // lblDITAuthor
            // 
            this.lblDITAuthor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITAuthor.Location = new System.Drawing.Point(89, 62);
            this.lblDITAuthor.Name = "lblDITAuthor";
            this.lblDITAuthor.ReadOnly = true;
            this.lblDITAuthor.Size = new System.Drawing.Size(120, 20);
            this.lblDITAuthor.TabIndex = 33;
            this.lblDITAuthor.TabStop = false;
            // 
            // lblDIVersion
            // 
            this.lblDIVersion.Location = new System.Drawing.Point(214, 47);
            this.lblDIVersion.Name = "lblDIVersion";
            this.lblDIVersion.Size = new System.Drawing.Size(92, 13);
            this.lblDIVersion.TabIndex = 8;
            this.lblDIVersion.Text = "Version :";
            this.lblDIVersion.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDICategory
            // 
            this.lblDICategory.Location = new System.Drawing.Point(10, 47);
            this.lblDICategory.Name = "lblDICategory";
            this.lblDICategory.Size = new System.Drawing.Size(75, 13);
            this.lblDICategory.TabIndex = 7;
            this.lblDICategory.Text = "Category :";
            this.lblDICategory.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITVersion
            // 
            this.lblDITVersion.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITVersion.Location = new System.Drawing.Point(311, 46);
            this.lblDITVersion.Name = "lblDITVersion";
            this.lblDITVersion.ReadOnly = true;
            this.lblDITVersion.Size = new System.Drawing.Size(120, 20);
            this.lblDITVersion.TabIndex = 34;
            this.lblDITVersion.TabStop = false;
            // 
            // lblDITCategory
            // 
            this.lblDITCategory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITCategory.Location = new System.Drawing.Point(89, 46);
            this.lblDITCategory.Name = "lblDITCategory";
            this.lblDITCategory.ReadOnly = true;
            this.lblDITCategory.Size = new System.Drawing.Size(120, 20);
            this.lblDITCategory.TabIndex = 35;
            this.lblDITCategory.TabStop = false;
            // 
            // lblDIDescription
            // 
            this.lblDIDescription.Location = new System.Drawing.Point(10, 31);
            this.lblDIDescription.Name = "lblDIDescription";
            this.lblDIDescription.Size = new System.Drawing.Size(75, 13);
            this.lblDIDescription.TabIndex = 4;
            this.lblDIDescription.Text = "Description :";
            this.lblDIDescription.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIName
            // 
            this.lblDIName.Location = new System.Drawing.Point(10, 15);
            this.lblDIName.Name = "lblDIName";
            this.lblDIName.Size = new System.Drawing.Size(75, 13);
            this.lblDIName.TabIndex = 3;
            this.lblDIName.Text = "Name :";
            this.lblDIName.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITDescription
            // 
            this.lblDITDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITDescription.Location = new System.Drawing.Point(89, 30);
            this.lblDITDescription.Multiline = true;
            this.lblDITDescription.Name = "lblDITDescription";
            this.lblDITDescription.ReadOnly = true;
            this.lblDITDescription.Size = new System.Drawing.Size(342, 17);
            this.lblDITDescription.TabIndex = 36;
            this.lblDITDescription.TabStop = false;
            // 
            // lblDITName
            // 
            this.lblDITName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITName.Location = new System.Drawing.Point(89, 14);
            this.lblDITName.Name = "lblDITName";
            this.lblDITName.ReadOnly = true;
            this.lblDITName.Size = new System.Drawing.Size(342, 20);
            this.lblDITName.TabIndex = 37;
            this.lblDITName.TabStop = false;
            // 
            // lblDIRomsFixable
            // 
            this.lblDIRomsFixable.Location = new System.Drawing.Point(214, 105);
            this.lblDIRomsFixable.Name = "lblDIRomsFixable";
            this.lblDIRomsFixable.Size = new System.Drawing.Size(92, 13);
            this.lblDIRomsFixable.TabIndex = 25;
            this.lblDIRomsFixable.Text = "ROMs Fixable :";
            this.lblDIRomsFixable.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIROMsMissing
            // 
            this.lblDIROMsMissing.Location = new System.Drawing.Point(2, 121);
            this.lblDIROMsMissing.Name = "lblDIROMsMissing";
            this.lblDIROMsMissing.Size = new System.Drawing.Size(83, 13);
            this.lblDIROMsMissing.TabIndex = 24;
            this.lblDIROMsMissing.Text = "ROMs Missing :";
            this.lblDIROMsMissing.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // DirTree
            // 
            this.DirTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.DirTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DirTree.Location = new System.Drawing.Point(0, 0);
            this.DirTree.Name = "DirTree";
            this.DirTree.Size = new System.Drawing.Size(479, 586);
            this.DirTree.TabIndex = 2;
            this.DirTree.RvSelected += new System.Windows.Forms.MouseEventHandler(this.DirTreeRvSelected);
            this.DirTree.RvChecked += new System.Windows.Forms.MouseEventHandler(this.DirTreeRvChecked);
            // 
            // splitGameInfoLists
            // 
            this.splitGameInfoLists.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitGameInfoLists.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitGameInfoLists.IsSplitterFixed = true;
            this.splitGameInfoLists.Location = new System.Drawing.Point(0, 0);
            this.splitGameInfoLists.Name = "splitGameInfoLists";
            this.splitGameInfoLists.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitGameInfoLists.Panel1
            // 
            this.splitGameInfoLists.Panel1.Controls.Add(this.btnClear);
            this.splitGameInfoLists.Panel1.Controls.Add(this.txtFilter);
            this.splitGameInfoLists.Panel1.Controls.Add(this.picPatreon);
            this.splitGameInfoLists.Panel1.Controls.Add(this.picPayPal);
            this.splitGameInfoLists.Panel1.Controls.Add(this.chkBoxShowMerged);
            this.splitGameInfoLists.Panel1.Controls.Add(this.chkBoxShowFixed);
            this.splitGameInfoLists.Panel1.Controls.Add(this.chkBoxShowMissing);
            this.splitGameInfoLists.Panel1.Controls.Add(this.chkBoxShowCorrect);
            this.splitGameInfoLists.Panel1.Controls.Add(this.gbSetInfo);
            this.splitGameInfoLists.Panel1.Resize += new System.EventHandler(this.splitContainer4_Panel1_Resize);
            // 
            // splitGameInfoLists.Panel2
            // 
            this.splitGameInfoLists.Panel2.Controls.Add(this.splitListArt);
            this.splitGameInfoLists.Size = new System.Drawing.Size(697, 738);
            this.splitGameInfoLists.SplitterDistance = 148;
            this.splitGameInfoLists.TabIndex = 0;
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(664, 86);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(28, 22);
            this.btnClear.TabIndex = 18;
            this.btnClear.Text = "X";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.BtnClear_Click);
            // 
            // txtFilter
            // 
            this.txtFilter.Location = new System.Drawing.Point(549, 87);
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new System.Drawing.Size(109, 20);
            this.txtFilter.TabIndex = 17;
            this.txtFilter.TextChanged += new System.EventHandler(this.TxtFilter_TextChanged);
            // 
            // picPatreon
            // 
            this.picPatreon.Image = ((System.Drawing.Image)(resources.GetObject("picPatreon.Image")));
            this.picPatreon.Location = new System.Drawing.Point(620, 114);
            this.picPatreon.Name = "picPatreon";
            this.picPatreon.Size = new System.Drawing.Size(73, 29);
            this.picPatreon.TabIndex = 16;
            this.picPatreon.TabStop = false;
            this.picPatreon.Click += new System.EventHandler(this.picPatreon_Click);
            // 
            // picPayPal
            // 
            this.picPayPal.Image = ((System.Drawing.Image)(resources.GetObject("picPayPal.Image")));
            this.picPayPal.Location = new System.Drawing.Point(547, 114);
            this.picPayPal.Name = "picPayPal";
            this.picPayPal.Size = new System.Drawing.Size(73, 29);
            this.picPayPal.TabIndex = 15;
            this.picPayPal.TabStop = false;
            this.picPayPal.Click += new System.EventHandler(this.picPayPal_Click);
            // 
            // chkBoxShowMerged
            // 
            this.chkBoxShowMerged.AutoSize = true;
            this.chkBoxShowMerged.Location = new System.Drawing.Point(547, 64);
            this.chkBoxShowMerged.Name = "chkBoxShowMerged";
            this.chkBoxShowMerged.Size = new System.Drawing.Size(125, 17);
            this.chkBoxShowMerged.TabIndex = 8;
            this.chkBoxShowMerged.Text = "Show Merged ROMs";
            this.chkBoxShowMerged.UseVisualStyleBackColor = true;
            this.chkBoxShowMerged.CheckedChanged += new System.EventHandler(this.ChkBoxShowMergedCheckedChanged);
            // 
            // chkBoxShowFixed
            // 
            this.chkBoxShowFixed.AutoSize = true;
            this.chkBoxShowFixed.Checked = true;
            this.chkBoxShowFixed.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBoxShowFixed.Location = new System.Drawing.Point(547, 48);
            this.chkBoxShowFixed.Name = "chkBoxShowFixed";
            this.chkBoxShowFixed.Size = new System.Drawing.Size(102, 17);
            this.chkBoxShowFixed.TabIndex = 7;
            this.chkBoxShowFixed.Text = "Show Fix ROMs";
            this.chkBoxShowFixed.UseVisualStyleBackColor = true;
            this.chkBoxShowFixed.CheckedChanged += new System.EventHandler(this.ChkBoxShowFixedCheckedChanged);
            // 
            // chkBoxShowMissing
            // 
            this.chkBoxShowMissing.AutoSize = true;
            this.chkBoxShowMissing.Checked = true;
            this.chkBoxShowMissing.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBoxShowMissing.Location = new System.Drawing.Point(547, 32);
            this.chkBoxShowMissing.Name = "chkBoxShowMissing";
            this.chkBoxShowMissing.Size = new System.Drawing.Size(124, 17);
            this.chkBoxShowMissing.TabIndex = 6;
            this.chkBoxShowMissing.Text = "Show Missing ROMs";
            this.chkBoxShowMissing.UseVisualStyleBackColor = true;
            this.chkBoxShowMissing.CheckedChanged += new System.EventHandler(this.ChkBoxShowMissingCheckedChanged);
            // 
            // chkBoxShowCorrect
            // 
            this.chkBoxShowCorrect.AutoSize = true;
            this.chkBoxShowCorrect.Checked = true;
            this.chkBoxShowCorrect.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBoxShowCorrect.Location = new System.Drawing.Point(547, 16);
            this.chkBoxShowCorrect.Name = "chkBoxShowCorrect";
            this.chkBoxShowCorrect.Size = new System.Drawing.Size(123, 17);
            this.chkBoxShowCorrect.TabIndex = 5;
            this.chkBoxShowCorrect.Text = "Show Correct ROMs";
            this.chkBoxShowCorrect.UseVisualStyleBackColor = true;
            this.chkBoxShowCorrect.CheckedChanged += new System.EventHandler(this.ChkBoxShowCorrectCheckedChanged);
            // 
            // gbSetInfo
            // 
            this.gbSetInfo.Location = new System.Drawing.Point(5, 0);
            this.gbSetInfo.Name = "gbSetInfo";
            this.gbSetInfo.Size = new System.Drawing.Size(532, 147);
            this.gbSetInfo.TabIndex = 4;
            this.gbSetInfo.TabStop = false;
            this.gbSetInfo.Text = "Game Info :";
            this.gbSetInfo.Resize += new System.EventHandler(this.gbSetInfo_Resize);
            // 
            // splitListArt
            // 
            this.splitListArt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitListArt.Location = new System.Drawing.Point(0, 0);
            this.splitListArt.Name = "splitListArt";
            // 
            // splitListArt.Panel1
            // 
            this.splitListArt.Panel1.Controls.Add(this.splitGameListRomList);
            // 
            // splitListArt.Panel2
            // 
            this.splitListArt.Panel2.Controls.Add(this.TabEmuArc);
            this.splitListArt.Size = new System.Drawing.Size(697, 586);
            this.splitListArt.SplitterDistance = 544;
            this.splitListArt.TabIndex = 1;
            // 
            // splitGameListRomList
            // 
            this.splitGameListRomList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitGameListRomList.Location = new System.Drawing.Point(0, 0);
            this.splitGameListRomList.Name = "splitGameListRomList";
            this.splitGameListRomList.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitGameListRomList.Panel1
            // 
            this.splitGameListRomList.Panel1.Controls.Add(this.GameGrid);
            // 
            // splitGameListRomList.Panel2
            // 
            this.splitGameListRomList.Panel2.Controls.Add(this.RomGrid);
            this.splitGameListRomList.Size = new System.Drawing.Size(544, 586);
            this.splitGameListRomList.SplitterDistance = 267;
            this.splitGameListRomList.TabIndex = 0;
            // 
            // GameGrid
            // 
            this.GameGrid.AllowUserToAddRows = false;
            this.GameGrid.AllowUserToDeleteRows = false;
            this.GameGrid.AllowUserToResizeRows = false;
            this.GameGrid.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.GameGrid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.GameGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GameGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Type,
            this.CGame,
            this.CDescription,
            this.CCorrect});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.GameGrid.DefaultCellStyle = dataGridViewCellStyle2;
            this.GameGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GameGrid.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.GameGrid.Location = new System.Drawing.Point(0, 0);
            this.GameGrid.MultiSelect = false;
            this.GameGrid.Name = "GameGrid";
            this.GameGrid.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.GameGrid.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.GameGrid.RowHeadersVisible = false;
            this.GameGrid.RowTemplate.Height = 17;
            this.GameGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.GameGrid.ShowCellErrors = false;
            this.GameGrid.ShowCellToolTips = false;
            this.GameGrid.ShowEditingIcon = false;
            this.GameGrid.ShowRowErrors = false;
            this.GameGrid.Size = new System.Drawing.Size(544, 267);
            this.GameGrid.TabIndex = 4;
            this.GameGrid.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.GameGrid_CellFormatting);
            this.GameGrid.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.GameGridColumnHeaderMouseClick);
            this.GameGrid.SelectionChanged += new System.EventHandler(this.GameGridSelectionChanged);
            this.GameGrid.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.GameGridMouseDoubleClick);
            this.GameGrid.MouseUp += new System.Windows.Forms.MouseEventHandler(this.GameGrid_MouseUp);
            // 
            // Type
            // 
            this.Type.FillWeight = 40F;
            this.Type.HeaderText = "Type";
            this.Type.Name = "Type";
            this.Type.ReadOnly = true;
            this.Type.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Type.Width = 40;
            // 
            // CGame
            // 
            this.CGame.HeaderText = "Game (Directory / Zip)";
            this.CGame.Name = "CGame";
            this.CGame.ReadOnly = true;
            this.CGame.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            this.CGame.Width = 220;
            // 
            // CDescription
            // 
            this.CDescription.HeaderText = "Description";
            this.CDescription.Name = "CDescription";
            this.CDescription.ReadOnly = true;
            this.CDescription.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            this.CDescription.Width = 220;
            // 
            // CCorrect
            // 
            this.CCorrect.HeaderText = "ROM Status";
            this.CCorrect.Name = "CCorrect";
            this.CCorrect.ReadOnly = true;
            this.CCorrect.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.CCorrect.Width = 500;
            // 
            // RomGrid
            // 
            this.RomGrid.AllowUserToAddRows = false;
            this.RomGrid.AllowUserToDeleteRows = false;
            this.RomGrid.AllowUserToResizeRows = false;
            this.RomGrid.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.RomGrid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.RomGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.RomGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.CGot,
            this.CRom,
            this.CMerge,
            this.CSize,
            this.CCRC32,
            this.CSHA1,
            this.CMD5,
            this.CAltSize,
            this.CAltCRC32,
            this.CAltSHA1,
            this.CAltMD5,
            this.CStatus,
            this.ZipIndex,
            this.ZipHeader});
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.RomGrid.DefaultCellStyle = dataGridViewCellStyle5;
            this.RomGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RomGrid.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.RomGrid.Location = new System.Drawing.Point(0, 0);
            this.RomGrid.MultiSelect = false;
            this.RomGrid.Name = "RomGrid";
            this.RomGrid.ReadOnly = true;
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.RomGrid.RowHeadersDefaultCellStyle = dataGridViewCellStyle6;
            this.RomGrid.RowHeadersVisible = false;
            this.RomGrid.RowTemplate.Height = 19;
            this.RomGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.RomGrid.ShowCellErrors = false;
            this.RomGrid.ShowEditingIcon = false;
            this.RomGrid.ShowRowErrors = false;
            this.RomGrid.Size = new System.Drawing.Size(544, 315);
            this.RomGrid.TabIndex = 21;
            this.RomGrid.VirtualMode = true;
            this.RomGrid.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.RomGrid_CellFormatting);
            this.RomGrid.SelectionChanged += new System.EventHandler(this.RomGridSelectionChanged);
            this.RomGrid.SortCompare += new System.Windows.Forms.DataGridViewSortCompareEventHandler(this.RomGrid_SortCompare);
            this.RomGrid.MouseUp += new System.Windows.Forms.MouseEventHandler(this.RomGridMouseUp);
            // 
            // CGot
            // 
            this.CGot.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.CGot.HeaderText = "Got";
            this.CGot.Name = "CGot";
            this.CGot.ReadOnly = true;
            this.CGot.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.CGot.Width = 65;
            // 
            // CRom
            // 
            this.CRom.HeaderText = "ROM (File)";
            this.CRom.Name = "CRom";
            this.CRom.ReadOnly = true;
            this.CRom.Width = 150;
            // 
            // CMerge
            // 
            this.CMerge.HeaderText = "Merge";
            this.CMerge.Name = "CMerge";
            this.CMerge.ReadOnly = true;
            this.CMerge.Width = 60;
            // 
            // CSize
            // 
            this.CSize.HeaderText = "Size";
            this.CSize.Name = "CSize";
            this.CSize.ReadOnly = true;
            this.CSize.Width = 60;
            // 
            // CCRC32
            // 
            this.CCRC32.HeaderText = "CRC32";
            this.CCRC32.Name = "CCRC32";
            this.CCRC32.ReadOnly = true;
            // 
            // CSHA1
            // 
            this.CSHA1.HeaderText = "SHA1";
            this.CSHA1.Name = "CSHA1";
            this.CSHA1.ReadOnly = true;
            this.CSHA1.Width = 150;
            // 
            // CMD5
            // 
            this.CMD5.HeaderText = "MD5";
            this.CMD5.Name = "CMD5";
            this.CMD5.ReadOnly = true;
            this.CMD5.Width = 150;
            // 
            // CAltSize
            // 
            this.CAltSize.HeaderText = "AltSize";
            this.CAltSize.Name = "CAltSize";
            this.CAltSize.ReadOnly = true;
            this.CAltSize.Width = 60;
            // 
            // CAltCRC32
            // 
            this.CAltCRC32.HeaderText = "AltCRC32";
            this.CAltCRC32.Name = "CAltCRC32";
            this.CAltCRC32.ReadOnly = true;
            // 
            // CAltSHA1
            // 
            this.CAltSHA1.HeaderText = "AltSHA1";
            this.CAltSHA1.Name = "CAltSHA1";
            this.CAltSHA1.ReadOnly = true;
            this.CAltSHA1.Width = 150;
            // 
            // CAltMD5
            // 
            this.CAltMD5.HeaderText = "AltMD5";
            this.CAltMD5.Name = "CAltMD5";
            this.CAltMD5.ReadOnly = true;
            this.CAltMD5.Width = 150;
            // 
            // CStatus
            // 
            this.CStatus.HeaderText = "Status";
            this.CStatus.Name = "CStatus";
            this.CStatus.ReadOnly = true;
            // 
            // ZipIndex
            // 
            this.ZipIndex.HeaderText = "ZipIndex";
            this.ZipIndex.Name = "ZipIndex";
            this.ZipIndex.ReadOnly = true;
            // 
            // ZipHeader
            // 
            this.ZipHeader.HeaderText = "ZipHeader";
            this.ZipHeader.Name = "ZipHeader";
            this.ZipHeader.ReadOnly = true;
            // 
            // TabEmuArc
            // 
            this.TabEmuArc.Controls.Add(this.tabArtWork);
            this.TabEmuArc.Controls.Add(this.tabScreens);
            this.TabEmuArc.Controls.Add(this.tabInfo);
            this.TabEmuArc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TabEmuArc.Location = new System.Drawing.Point(0, 0);
            this.TabEmuArc.Name = "TabEmuArc";
            this.TabEmuArc.SelectedIndex = 0;
            this.TabEmuArc.Size = new System.Drawing.Size(149, 586);
            this.TabEmuArc.TabIndex = 0;
            // 
            // tabArtWork
            // 
            this.tabArtWork.BackColor = System.Drawing.SystemColors.Control;
            this.tabArtWork.Controls.Add(this.picLogo);
            this.tabArtWork.Controls.Add(this.picArtwork);
            this.tabArtWork.Location = new System.Drawing.Point(4, 22);
            this.tabArtWork.Name = "tabArtWork";
            this.tabArtWork.Padding = new System.Windows.Forms.Padding(3);
            this.tabArtWork.Size = new System.Drawing.Size(141, 560);
            this.tabArtWork.TabIndex = 0;
            this.tabArtWork.Text = "ArtWork";
            this.tabArtWork.Resize += new System.EventHandler(this.tabArtWork_Resize);
            // 
            // picLogo
            // 
            this.picLogo.BackColor = System.Drawing.Color.White;
            this.picLogo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picLogo.Location = new System.Drawing.Point(14, 197);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(114, 117);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 1;
            this.picLogo.TabStop = false;
            // 
            // picArtwork
            // 
            this.picArtwork.BackColor = System.Drawing.Color.White;
            this.picArtwork.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picArtwork.Location = new System.Drawing.Point(13, 60);
            this.picArtwork.Name = "picArtwork";
            this.picArtwork.Size = new System.Drawing.Size(116, 104);
            this.picArtwork.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picArtwork.TabIndex = 0;
            this.picArtwork.TabStop = false;
            // 
            // tabScreens
            // 
            this.tabScreens.BackColor = System.Drawing.SystemColors.Control;
            this.tabScreens.Controls.Add(this.picScreenShot);
            this.tabScreens.Controls.Add(this.picScreenTitle);
            this.tabScreens.Location = new System.Drawing.Point(4, 22);
            this.tabScreens.Name = "tabScreens";
            this.tabScreens.Padding = new System.Windows.Forms.Padding(3);
            this.tabScreens.Size = new System.Drawing.Size(141, 560);
            this.tabScreens.TabIndex = 1;
            this.tabScreens.Text = "Screens";
            this.tabScreens.Resize += new System.EventHandler(this.tabScreens_Resize);
            // 
            // picScreenShot
            // 
            this.picScreenShot.BackColor = System.Drawing.Color.White;
            this.picScreenShot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picScreenShot.Location = new System.Drawing.Point(15, 218);
            this.picScreenShot.Name = "picScreenShot";
            this.picScreenShot.Size = new System.Drawing.Size(104, 113);
            this.picScreenShot.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picScreenShot.TabIndex = 1;
            this.picScreenShot.TabStop = false;
            // 
            // picScreenTitle
            // 
            this.picScreenTitle.BackColor = System.Drawing.Color.White;
            this.picScreenTitle.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picScreenTitle.Location = new System.Drawing.Point(15, 69);
            this.picScreenTitle.Name = "picScreenTitle";
            this.picScreenTitle.Size = new System.Drawing.Size(104, 117);
            this.picScreenTitle.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picScreenTitle.TabIndex = 0;
            this.picScreenTitle.TabStop = false;
            // 
            // tabInfo
            // 
            this.tabInfo.BackColor = System.Drawing.SystemColors.Control;
            this.tabInfo.Controls.Add(this.txtInfo);
            this.tabInfo.Location = new System.Drawing.Point(4, 22);
            this.tabInfo.Name = "tabInfo";
            this.tabInfo.Size = new System.Drawing.Size(141, 560);
            this.tabInfo.TabIndex = 2;
            this.tabInfo.Text = "Info";
            this.tabInfo.Resize += new System.EventHandler(this.tabInfo_Resize);
            // 
            // txtInfo
            // 
            this.txtInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtInfo.Location = new System.Drawing.Point(0, 0);
            this.txtInfo.Name = "txtInfo";
            this.txtInfo.ReadOnly = true;
            this.txtInfo.Size = new System.Drawing.Size(141, 560);
            this.txtInfo.TabIndex = 0;
            this.txtInfo.Text = "";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmUpdateDATs,
            this.addToSortToolStripMenuItem,
            this.tsmScanROMs,
            this.tsmFindFixes,
            this.FixROMsToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.reportsToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1264, 24);
            this.menuStrip1.TabIndex = 6;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // tsmUpdateDATs
            // 
            this.tsmUpdateDATs.Name = "tsmUpdateDATs";
            this.tsmUpdateDATs.Size = new System.Drawing.Size(85, 20);
            this.tsmUpdateDATs.Text = "Update DATs";
            this.tsmUpdateDATs.Click += new System.EventHandler(this.TsmUpdateDaTsClick);
            // 
            // addToSortToolStripMenuItem
            // 
            this.addToSortToolStripMenuItem.Name = "addToSortToolStripMenuItem";
            this.addToSortToolStripMenuItem.Size = new System.Drawing.Size(74, 20);
            this.addToSortToolStripMenuItem.Text = "AddToSort";
            this.addToSortToolStripMenuItem.Click += new System.EventHandler(this.AddToSortToolStripMenuItem_Click);
            // 
            // tsmScanROMs
            // 
            this.tsmScanROMs.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmScanLevel1,
            this.tsmScanLevel2,
            this.tsmScanLevel3});
            this.tsmScanROMs.Name = "tsmScanROMs";
            this.tsmScanROMs.Size = new System.Drawing.Size(79, 20);
            this.tsmScanROMs.Text = "Scan ROMs";
            // 
            // tsmScanLevel1
            // 
            this.tsmScanLevel1.Name = "tsmScanLevel1";
            this.tsmScanLevel1.Size = new System.Drawing.Size(272, 22);
            this.tsmScanLevel1.Text = "Scan new ROMs headers only";
            this.tsmScanLevel1.Click += new System.EventHandler(this.TsmScanLevel1Click);
            // 
            // tsmScanLevel2
            // 
            this.tsmScanLevel2.Name = "tsmScanLevel2";
            this.tsmScanLevel2.Size = new System.Drawing.Size(272, 22);
            this.tsmScanLevel2.Text = "Scan new ROMs with full hash check";
            this.tsmScanLevel2.Click += new System.EventHandler(this.TsmScanLevel2Click);
            // 
            // tsmScanLevel3
            // 
            this.tsmScanLevel3.Name = "tsmScanLevel3";
            this.tsmScanLevel3.Size = new System.Drawing.Size(272, 22);
            this.tsmScanLevel3.Text = "ReScan All ROMs with full hash check";
            this.tsmScanLevel3.Click += new System.EventHandler(this.TsmScanLevel3Click);
            // 
            // tsmFindFixes
            // 
            this.tsmFindFixes.Name = "tsmFindFixes";
            this.tsmFindFixes.Size = new System.Drawing.Size(71, 20);
            this.tsmFindFixes.Text = "Find Fixes";
            this.tsmFindFixes.Click += new System.EventHandler(this.TsmFindFixesClick);
            // 
            // FixROMsToolStripMenuItem
            // 
            this.FixROMsToolStripMenuItem.Name = "FixROMsToolStripMenuItem";
            this.FixROMsToolStripMenuItem.Size = new System.Drawing.Size(69, 20);
            this.FixROMsToolStripMenuItem.Text = "Fix ROMs";
            this.FixROMsToolStripMenuItem.Click += new System.EventHandler(this.FixFilesToolStripMenuItemClick);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.romVaultSettingsToolStripMenuItem,
            this.directorySettingsToolStripMenuItem,
            this.registrationSettingsToolStripMenuItem});
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.settingsToolStripMenuItem.Text = "Settings";
            // 
            // romVaultSettingsToolStripMenuItem
            // 
            this.romVaultSettingsToolStripMenuItem.Name = "romVaultSettingsToolStripMenuItem";
            this.romVaultSettingsToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.romVaultSettingsToolStripMenuItem.Text = "RomVault Settings";
            this.romVaultSettingsToolStripMenuItem.Click += new System.EventHandler(this.RomVaultSettingsToolStripMenuItem_Click);
            // 
            // directorySettingsToolStripMenuItem
            // 
            this.directorySettingsToolStripMenuItem.Name = "directorySettingsToolStripMenuItem";
            this.directorySettingsToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.directorySettingsToolStripMenuItem.Text = "Directory Settings";
            this.directorySettingsToolStripMenuItem.Click += new System.EventHandler(this.DirectorySettingsToolStripMenuItem_Click);
            // 
            // registrationSettingsToolStripMenuItem
            // 
            this.registrationSettingsToolStripMenuItem.Name = "registrationSettingsToolStripMenuItem";
            this.registrationSettingsToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.registrationSettingsToolStripMenuItem.Text = "Registration Settings";
            this.registrationSettingsToolStripMenuItem.Click += new System.EventHandler(this.RegistrationSettingsToolStripMenuItem_Click);
            // 
            // reportsToolStripMenuItem
            // 
            this.reportsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fixDatReportToolStripMenuItem,
            this.fullReportToolStripMenuItem,
            this.fixReportToolStripMenuItem});
            this.reportsToolStripMenuItem.Name = "reportsToolStripMenuItem";
            this.reportsToolStripMenuItem.Size = new System.Drawing.Size(59, 20);
            this.reportsToolStripMenuItem.Text = "Reports";
            // 
            // fixDatReportToolStripMenuItem
            // 
            this.fixDatReportToolStripMenuItem.Name = "fixDatReportToolStripMenuItem";
            this.fixDatReportToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
            this.fixDatReportToolStripMenuItem.Text = "Fix Dat Report";
            this.fixDatReportToolStripMenuItem.Click += new System.EventHandler(this.fixDatReportToolStripMenuItem_Click);
            // 
            // fullReportToolStripMenuItem
            // 
            this.fullReportToolStripMenuItem.Name = "fullReportToolStripMenuItem";
            this.fullReportToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
            this.fullReportToolStripMenuItem.Text = "Full Report";
            this.fullReportToolStripMenuItem.Click += new System.EventHandler(this.fullReportToolStripMenuItem_Click);
            // 
            // fixReportToolStripMenuItem
            // 
            this.fixReportToolStripMenuItem.Name = "fixReportToolStripMenuItem";
            this.fixReportToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
            this.fixReportToolStripMenuItem.Text = "Fix Report";
            this.fixReportToolStripMenuItem.Click += new System.EventHandler(this.fixReportToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.colorKeyToolStripMenuItem,
            this.aboutRomVaultToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // colorKeyToolStripMenuItem
            // 
            this.colorKeyToolStripMenuItem.Name = "colorKeyToolStripMenuItem";
            this.colorKeyToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.colorKeyToolStripMenuItem.Text = "Color Key";
            this.colorKeyToolStripMenuItem.Click += new System.EventHandler(this.colorKeyToolStripMenuItem_Click);
            // 
            // aboutRomVaultToolStripMenuItem
            // 
            this.aboutRomVaultToolStripMenuItem.Name = "aboutRomVaultToolStripMenuItem";
            this.aboutRomVaultToolStripMenuItem.Size = new System.Drawing.Size(161, 22);
            this.aboutRomVaultToolStripMenuItem.Text = "About RomVault";
            this.aboutRomVaultToolStripMenuItem.Click += new System.EventHandler(this.AboutRomVaultToolStripMenuItemClick);
            // 
            // dataGridViewImageColumn1
            // 
            this.dataGridViewImageColumn1.FillWeight = 40F;
            this.dataGridViewImageColumn1.HeaderText = "Type";
            this.dataGridViewImageColumn1.Name = "dataGridViewImageColumn1";
            this.dataGridViewImageColumn1.ReadOnly = true;
            this.dataGridViewImageColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewImageColumn1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.dataGridViewImageColumn1.Width = 40;
            // 
            // dataGridViewImageColumn2
            // 
            this.dataGridViewImageColumn2.HeaderText = "ROM Status";
            this.dataGridViewImageColumn2.Name = "dataGridViewImageColumn2";
            this.dataGridViewImageColumn2.ReadOnly = true;
            this.dataGridViewImageColumn2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewImageColumn2.Width = 300;
            // 
            // dataGridViewImageColumn3
            // 
            this.dataGridViewImageColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.dataGridViewImageColumn3.HeaderText = "Got";
            this.dataGridViewImageColumn3.Name = "dataGridViewImageColumn3";
            this.dataGridViewImageColumn3.ReadOnly = true;
            this.dataGridViewImageColumn3.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewImageColumn3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.dataGridViewImageColumn3.Width = 65;
            // 
            // FrmMain
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1264, 762);
            this.Controls.Add(this.splitToolBarMain);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmMain";
            this.Text = "RomVault (V2.0)";
            this.splitToolBarMain.Panel1.ResumeLayout(false);
            this.splitToolBarMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitToolBarMain)).EndInit();
            this.splitToolBarMain.ResumeLayout(false);
            this.splitDatInfoGameInfo.Panel1.ResumeLayout(false);
            this.splitDatInfoGameInfo.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitDatInfoGameInfo)).EndInit();
            this.splitDatInfoGameInfo.ResumeLayout(false);
            this.splitDatInfoTree.Panel1.ResumeLayout(false);
            this.splitDatInfoTree.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitDatInfoTree)).EndInit();
            this.splitDatInfoTree.ResumeLayout(false);
            this.gbDatInfo.ResumeLayout(false);
            this.gbDatInfo.PerformLayout();
            this.splitGameInfoLists.Panel1.ResumeLayout(false);
            this.splitGameInfoLists.Panel1.PerformLayout();
            this.splitGameInfoLists.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitGameInfoLists)).EndInit();
            this.splitGameInfoLists.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picPatreon)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPayPal)).EndInit();
            this.splitListArt.Panel1.ResumeLayout(false);
            this.splitListArt.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitListArt)).EndInit();
            this.splitListArt.ResumeLayout(false);
            this.splitGameListRomList.Panel1.ResumeLayout(false);
            this.splitGameListRomList.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitGameListRomList)).EndInit();
            this.splitGameListRomList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GameGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.RomGrid)).EndInit();
            this.TabEmuArc.ResumeLayout(false);
            this.tabArtWork.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picArtwork)).EndInit();
            this.tabScreens.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picScreenShot)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picScreenTitle)).EndInit();
            this.tabInfo.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitToolBarMain;
        private System.Windows.Forms.Button btnReport;
        private System.Windows.Forms.Button btnFixFiles;
        private System.Windows.Forms.Button btnFindFixes;
        private System.Windows.Forms.Button btnScanRoms;
        private System.Windows.Forms.Button btnUpdateDats;
        private System.Windows.Forms.SplitContainer splitDatInfoGameInfo;
        private System.Windows.Forms.SplitContainer splitDatInfoTree;
        private System.Windows.Forms.GroupBox gbDatInfo;
        private System.Windows.Forms.Label lblDIRomsUnknown;
        private System.Windows.Forms.Label lblDIROMsMissing;
        private System.Windows.Forms.Label lblDIROMsGot;
        private System.Windows.Forms.TextBox lblDITRomsUnknown;
        private System.Windows.Forms.TextBox lblDITRomsMissing;
        private System.Windows.Forms.TextBox lblDITRomsGot;
        private System.Windows.Forms.Label lblDIRomPath;
        private System.Windows.Forms.TextBox lblDITPath;
        private System.Windows.Forms.Label lblDIDate;
        private System.Windows.Forms.Label lblDIAuthor;
        private System.Windows.Forms.TextBox lblDITDate;
        private System.Windows.Forms.TextBox lblDITAuthor;
        private System.Windows.Forms.Label lblDIVersion;
        private System.Windows.Forms.Label lblDICategory;
        private System.Windows.Forms.TextBox lblDITVersion;
        private System.Windows.Forms.TextBox lblDITCategory;
        private System.Windows.Forms.Label lblDIDescription;
        private System.Windows.Forms.Label lblDIName;
        private System.Windows.Forms.TextBox lblDITDescription;
        private System.Windows.Forms.TextBox lblDITName;
        private System.Windows.Forms.SplitContainer splitGameInfoLists;
        private System.Windows.Forms.CheckBox chkBoxShowMerged;
        private System.Windows.Forms.CheckBox chkBoxShowFixed;
        private System.Windows.Forms.CheckBox chkBoxShowMissing;
        private System.Windows.Forms.CheckBox chkBoxShowCorrect;
        private System.Windows.Forms.GroupBox gbSetInfo;

        private System.Windows.Forms.SplitContainer splitGameListRomList;
        private System.Windows.Forms.DataGridView GameGrid;
        private System.Windows.Forms.DataGridView RomGrid;
        private RvTree DirTree;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn1;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn2;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn3;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem tsmUpdateDATs;
        private System.Windows.Forms.ToolStripMenuItem tsmScanROMs;
        private System.Windows.Forms.ToolStripMenuItem tsmScanLevel1;
        private System.Windows.Forms.ToolStripMenuItem tsmScanLevel3;
        private System.Windows.Forms.ToolStripMenuItem tsmFindFixes;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutRomVaultToolStripMenuItem;
        private System.Windows.Forms.DataGridViewImageColumn Type;
        private System.Windows.Forms.DataGridViewTextBoxColumn CGame;
        private System.Windows.Forms.DataGridViewTextBoxColumn CDescription;
        private System.Windows.Forms.DataGridViewImageColumn CCorrect;
        private System.Windows.Forms.ToolStripMenuItem FixROMsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tsmScanLevel2;
        private System.Windows.Forms.TextBox lblDITRomsFixable;
        private System.Windows.Forms.Label lblDIRomsFixable;
        private System.Windows.Forms.ToolStripMenuItem reportsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fixDatReportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fullReportToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fixReportToolStripMenuItem;
        private System.Windows.Forms.PictureBox picPayPal;
        private System.Windows.Forms.DataGridViewImageColumn CGot;
        private System.Windows.Forms.DataGridViewTextBoxColumn CRom;
        private System.Windows.Forms.DataGridViewTextBoxColumn CMerge;
        private System.Windows.Forms.DataGridViewTextBoxColumn CSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn CCRC32;
        private System.Windows.Forms.DataGridViewTextBoxColumn CSHA1;
        private System.Windows.Forms.DataGridViewTextBoxColumn CMD5;
        private System.Windows.Forms.DataGridViewTextBoxColumn CAltSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn CAltCRC32;
        private System.Windows.Forms.DataGridViewTextBoxColumn CAltSHA1;
        private System.Windows.Forms.DataGridViewTextBoxColumn CAltMD5;
        private System.Windows.Forms.DataGridViewTextBoxColumn CStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn ZipIndex;
        private System.Windows.Forms.DataGridViewTextBoxColumn ZipHeader;
        private System.Windows.Forms.SplitContainer splitListArt;
        private System.Windows.Forms.TabControl TabEmuArc;
        private System.Windows.Forms.TabPage tabArtWork;
        private System.Windows.Forms.TabPage tabScreens;
        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.PictureBox picArtwork;
        private System.Windows.Forms.PictureBox picScreenShot;
        private System.Windows.Forms.PictureBox picScreenTitle;
        private System.Windows.Forms.TabPage tabInfo;
        private System.Windows.Forms.ToolStripMenuItem colorKeyToolStripMenuItem;
        private System.Windows.Forms.PictureBox picPatreon;
        private System.Windows.Forms.ToolStripMenuItem addToSortToolStripMenuItem;
        private System.Windows.Forms.RichTextBox txtInfo;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.ToolStripMenuItem romVaultSettingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem directorySettingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem registrationSettingsToolStripMenuItem;
    }
}

