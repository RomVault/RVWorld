namespace RomVaultX
{
    partial class frmMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.btnUpdateDats = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnScanRoms = new System.Windows.Forms.Button();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.gbDatInfo = new System.Windows.Forms.GroupBox();
            this.lblDITRomsNoDump = new System.Windows.Forms.Label();
            this.lblDIRomsNoDump = new System.Windows.Forms.Label();
            this.lblDIRomsTotal = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.lblDITRomsTotal = new System.Windows.Forms.Label();
            this.lblDITRomsMissing = new System.Windows.Forms.Label();
            this.lblDITRomsGot = new System.Windows.Forms.Label();
            this.lblDITRomPath = new System.Windows.Forms.Label();
            this.lblDITPath = new System.Windows.Forms.Label();
            this.lblDIDate = new System.Windows.Forms.Label();
            this.lblDIAuthor = new System.Windows.Forms.Label();
            this.lblDITDate = new System.Windows.Forms.Label();
            this.lblDITAuthor = new System.Windows.Forms.Label();
            this.lblDIVersion = new System.Windows.Forms.Label();
            this.lblDICategory = new System.Windows.Forms.Label();
            this.lblDITVersion = new System.Windows.Forms.Label();
            this.lblDITCategory = new System.Windows.Forms.Label();
            this.lblDIDescription = new System.Windows.Forms.Label();
            this.lblDIName = new System.Windows.Forms.Label();
            this.lblDITDescription = new System.Windows.Forms.Label();
            this.lblDITName = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.chkBoxShowMissing = new System.Windows.Forms.CheckBox();
            this.chkBoxShowCorrect = new System.Windows.Forms.CheckBox();
            this.gbSetInfo = new System.Windows.Forms.GroupBox();
            this.splitContainer5 = new System.Windows.Forms.SplitContainer();
            this.GameGrid = new System.Windows.Forms.DataGridView();
            this.Type = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CGame = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CCorrect = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CMissing = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RomGrid = new System.Windows.Forms.DataGridView();
            this.CGot = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CRom = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CCompressSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CMerge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CCRC32 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CSHA1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CMD5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CInZip = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.romRootScanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quickReScanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deepReScanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scanADirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSubScanADir = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSubScanADirWithDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.updateZipDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startVDriveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeVDriveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fixDatsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.DirTree = new RomVaultX.RvTree();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.gbDatInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).BeginInit();
            this.splitContainer4.Panel1.SuspendLayout();
            this.splitContainer4.Panel2.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer5)).BeginInit();
            this.splitContainer5.Panel1.SuspendLayout();
            this.splitContainer5.Panel2.SuspendLayout();
            this.splitContainer5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GameGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.RomGrid)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
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
            this.btnUpdateDats.Click += new System.EventHandler(this.btnUpdateDats_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.BackColor = System.Drawing.Color.White;
            this.splitContainer1.Panel1.Controls.Add(this.btnScanRoms);
            this.splitContainer1.Panel1.Controls.Add(this.btnUpdateDats);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1264, 737);
            this.splitContainer1.SplitterDistance = 80;
            this.splitContainer1.TabIndex = 4;
            // 
            // btnScanRoms
            // 
            this.btnScanRoms.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnScanRoms.BackgroundImage")));
            this.btnScanRoms.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnScanRoms.Location = new System.Drawing.Point(0, 79);
            this.btnScanRoms.Name = "btnScanRoms";
            this.btnScanRoms.Size = new System.Drawing.Size(80, 80);
            this.btnScanRoms.TabIndex = 9;
            this.btnScanRoms.Text = "Scan ROMs";
            this.btnScanRoms.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnScanRoms.UseVisualStyleBackColor = true;
            this.btnScanRoms.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnScanRoms_MouseUp);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer3);
            this.splitContainer2.Panel1MinSize = 450;
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer4);
            this.splitContainer2.Size = new System.Drawing.Size(1180, 737);
            this.splitContainer2.SplitterDistance = 533;
            this.splitContainer2.TabIndex = 0;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer3.IsSplitterFixed = true;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.gbDatInfo);
            this.splitContainer3.Panel1.Resize += new System.EventHandler(this.splitContainer3_Panel1_Resize);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.DirTree);
            this.splitContainer3.Size = new System.Drawing.Size(533, 737);
            this.splitContainer3.SplitterDistance = 148;
            this.splitContainer3.TabIndex = 0;
            // 
            // gbDatInfo
            // 
            this.gbDatInfo.Controls.Add(this.lblDITRomsNoDump);
            this.gbDatInfo.Controls.Add(this.lblDIRomsNoDump);
            this.gbDatInfo.Controls.Add(this.lblDIRomsTotal);
            this.gbDatInfo.Controls.Add(this.label9);
            this.gbDatInfo.Controls.Add(this.lblDITRomsTotal);
            this.gbDatInfo.Controls.Add(this.lblDITRomsMissing);
            this.gbDatInfo.Controls.Add(this.lblDITRomsGot);
            this.gbDatInfo.Controls.Add(this.lblDITRomPath);
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
            this.gbDatInfo.Controls.Add(this.label8);
            this.gbDatInfo.Location = new System.Drawing.Point(5, 0);
            this.gbDatInfo.Name = "gbDatInfo";
            this.gbDatInfo.Size = new System.Drawing.Size(440, 147);
            this.gbDatInfo.TabIndex = 4;
            this.gbDatInfo.TabStop = false;
            this.gbDatInfo.Text = "Dat Info :";
            this.gbDatInfo.Resize += new System.EventHandler(this.gbDatInfo_Resize);
            // 
            // lblDITRomsNoDump
            // 
            this.lblDITRomsNoDump.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsNoDump.Location = new System.Drawing.Point(311, 120);
            this.lblDITRomsNoDump.Name = "lblDITRomsNoDump";
            this.lblDITRomsNoDump.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsNoDump.TabIndex = 28;
            // 
            // lblDIRomsNoDump
            // 
            this.lblDIRomsNoDump.Location = new System.Drawing.Point(214, 121);
            this.lblDIRomsNoDump.Name = "lblDIRomsNoDump";
            this.lblDIRomsNoDump.Size = new System.Drawing.Size(92, 13);
            this.lblDIRomsNoDump.TabIndex = 27;
            this.lblDIRomsNoDump.Text = "ROMs NoDump :";
            this.lblDIRomsNoDump.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIRomsTotal
            // 
            this.lblDIRomsTotal.Location = new System.Drawing.Point(214, 105);
            this.lblDIRomsTotal.Name = "lblDIRomsTotal";
            this.lblDIRomsTotal.Size = new System.Drawing.Size(92, 13);
            this.lblDIRomsTotal.TabIndex = 26;
            this.lblDIRomsTotal.Text = "ROMs Total :";
            this.lblDIRomsTotal.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(10, 105);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(75, 13);
            this.label9.TabIndex = 23;
            this.label9.Text = "ROMs Got :";
            this.label9.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITRomsTotal
            // 
            this.lblDITRomsTotal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsTotal.Location = new System.Drawing.Point(311, 104);
            this.lblDITRomsTotal.Name = "lblDITRomsTotal";
            this.lblDITRomsTotal.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsTotal.TabIndex = 21;
            // 
            // lblDITRomsMissing
            // 
            this.lblDITRomsMissing.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsMissing.Location = new System.Drawing.Point(89, 120);
            this.lblDITRomsMissing.Name = "lblDITRomsMissing";
            this.lblDITRomsMissing.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsMissing.TabIndex = 19;
            // 
            // lblDITRomsGot
            // 
            this.lblDITRomsGot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsGot.Location = new System.Drawing.Point(89, 104);
            this.lblDITRomsGot.Name = "lblDITRomsGot";
            this.lblDITRomsGot.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsGot.TabIndex = 18;
            // 
            // lblDITRomPath
            // 
            this.lblDITRomPath.Location = new System.Drawing.Point(10, 79);
            this.lblDITRomPath.Name = "lblDITRomPath";
            this.lblDITRomPath.Size = new System.Drawing.Size(75, 13);
            this.lblDITRomPath.TabIndex = 15;
            this.lblDITRomPath.Text = "ROM Path:";
            this.lblDITRomPath.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITPath
            // 
            this.lblDITPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITPath.Location = new System.Drawing.Point(89, 78);
            this.lblDITPath.Name = "lblDITPath";
            this.lblDITPath.Size = new System.Drawing.Size(342, 17);
            this.lblDITPath.TabIndex = 13;
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
            this.lblDITDate.Size = new System.Drawing.Size(120, 17);
            this.lblDITDate.TabIndex = 10;
            // 
            // lblDITAuthor
            // 
            this.lblDITAuthor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITAuthor.Location = new System.Drawing.Point(89, 62);
            this.lblDITAuthor.Name = "lblDITAuthor";
            this.lblDITAuthor.Size = new System.Drawing.Size(120, 17);
            this.lblDITAuthor.TabIndex = 9;
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
            this.lblDITVersion.Size = new System.Drawing.Size(120, 17);
            this.lblDITVersion.TabIndex = 6;
            // 
            // lblDITCategory
            // 
            this.lblDITCategory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITCategory.Location = new System.Drawing.Point(89, 46);
            this.lblDITCategory.Name = "lblDITCategory";
            this.lblDITCategory.Size = new System.Drawing.Size(120, 17);
            this.lblDITCategory.TabIndex = 5;
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
            this.lblDITDescription.Name = "lblDITDescription";
            this.lblDITDescription.Size = new System.Drawing.Size(342, 17);
            this.lblDITDescription.TabIndex = 2;
            // 
            // lblDITName
            // 
            this.lblDITName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITName.Location = new System.Drawing.Point(89, 14);
            this.lblDITName.Name = "lblDITName";
            this.lblDITName.Size = new System.Drawing.Size(342, 17);
            this.lblDITName.TabIndex = 1;
            // 
            // label8
            // 
            this.label8.Location = new System.Drawing.Point(2, 121);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(83, 13);
            this.label8.TabIndex = 24;
            this.label8.Text = "ROMs Missing :";
            this.label8.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            this.splitContainer4.Panel1.Controls.Add(this.chkBoxShowMissing);
            this.splitContainer4.Panel1.Controls.Add(this.chkBoxShowCorrect);
            this.splitContainer4.Panel1.Controls.Add(this.gbSetInfo);
            this.splitContainer4.Panel1.Resize += new System.EventHandler(this.splitContainer4_Panel1_Resize);
            // 
            // splitContainer4.Panel2
            // 
            this.splitContainer4.Panel2.Controls.Add(this.splitContainer5);
            this.splitContainer4.Size = new System.Drawing.Size(643, 737);
            this.splitContainer4.SplitterDistance = 148;
            this.splitContainer4.TabIndex = 0;
            // 
            // chkBoxShowMissing
            // 
            this.chkBoxShowMissing.AutoSize = true;
            this.chkBoxShowMissing.Checked = true;
            this.chkBoxShowMissing.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBoxShowMissing.Location = new System.Drawing.Point(508, 27);
            this.chkBoxShowMissing.Name = "chkBoxShowMissing";
            this.chkBoxShowMissing.Size = new System.Drawing.Size(124, 17);
            this.chkBoxShowMissing.TabIndex = 8;
            this.chkBoxShowMissing.Text = "Show Missing ROMs";
            this.chkBoxShowMissing.UseVisualStyleBackColor = true;
            this.chkBoxShowMissing.CheckedChanged += new System.EventHandler(this.chkBoxShowMissing_CheckedChanged);
            // 
            // chkBoxShowCorrect
            // 
            this.chkBoxShowCorrect.AutoSize = true;
            this.chkBoxShowCorrect.Checked = true;
            this.chkBoxShowCorrect.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBoxShowCorrect.Location = new System.Drawing.Point(508, 11);
            this.chkBoxShowCorrect.Name = "chkBoxShowCorrect";
            this.chkBoxShowCorrect.Size = new System.Drawing.Size(123, 17);
            this.chkBoxShowCorrect.TabIndex = 7;
            this.chkBoxShowCorrect.Text = "Show Correct ROMs";
            this.chkBoxShowCorrect.UseVisualStyleBackColor = true;
            this.chkBoxShowCorrect.CheckedChanged += new System.EventHandler(this.chkBoxShowCorrect_CheckedChanged);
            // 
            // gbSetInfo
            // 
            this.gbSetInfo.Location = new System.Drawing.Point(5, 0);
            this.gbSetInfo.Name = "gbSetInfo";
            this.gbSetInfo.Size = new System.Drawing.Size(416, 147);
            this.gbSetInfo.TabIndex = 5;
            this.gbSetInfo.TabStop = false;
            this.gbSetInfo.Text = "Game Info :";
            this.gbSetInfo.Resize += new System.EventHandler(this.gbSetInfo_Resize);
            // 
            // splitContainer5
            // 
            this.splitContainer5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer5.Location = new System.Drawing.Point(0, 0);
            this.splitContainer5.Name = "splitContainer5";
            this.splitContainer5.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer5.Panel1
            // 
            this.splitContainer5.Panel1.Controls.Add(this.GameGrid);
            // 
            // splitContainer5.Panel2
            // 
            this.splitContainer5.Panel2.Controls.Add(this.RomGrid);
            this.splitContainer5.Size = new System.Drawing.Size(643, 585);
            this.splitContainer5.SplitterDistance = 276;
            this.splitContainer5.TabIndex = 0;
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
            this.CCorrect,
            this.CMissing});
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
            this.GameGrid.Size = new System.Drawing.Size(643, 276);
            this.GameGrid.TabIndex = 5;
            this.GameGrid.SelectionChanged += new System.EventHandler(this.GameGrid_SelectionChanged);
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
            this.CGame.Width = 220;
            // 
            // CDescription
            // 
            this.CDescription.HeaderText = "Description";
            this.CDescription.Name = "CDescription";
            this.CDescription.ReadOnly = true;
            this.CDescription.Width = 220;
            // 
            // CCorrect
            // 
            this.CCorrect.HeaderText = "ROMs Got";
            this.CCorrect.Name = "CCorrect";
            this.CCorrect.ReadOnly = true;
            this.CCorrect.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.CCorrect.Width = 80;
            // 
            // CMissing
            // 
            this.CMissing.HeaderText = "ROMs Missing";
            this.CMissing.Name = "CMissing";
            this.CMissing.ReadOnly = true;
            this.CMissing.Resizable = System.Windows.Forms.DataGridViewTriState.False;
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
            this.CSize,
            this.CCompressSize,
            this.CMerge,
            this.CCRC32,
            this.CSHA1,
            this.CMD5,
            this.CStatus,
            this.CInZip});
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
            this.RomGrid.Size = new System.Drawing.Size(643, 305);
            this.RomGrid.TabIndex = 22;
            this.RomGrid.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.RomGrid_CellContentClick);
            this.RomGrid.SelectionChanged += new System.EventHandler(this.RomGrid_SelectionChanged);
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
            // CSize
            // 
            this.CSize.HeaderText = "Size";
            this.CSize.Name = "CSize";
            this.CSize.ReadOnly = true;
            this.CSize.Width = 60;
            // 
            // CCompressSize
            // 
            this.CCompressSize.HeaderText = "ZipSize";
            this.CCompressSize.Name = "CCompressSize";
            this.CCompressSize.ReadOnly = true;
            this.CCompressSize.Width = 60;
            // 
            // CMerge
            // 
            this.CMerge.HeaderText = "Merge";
            this.CMerge.Name = "CMerge";
            this.CMerge.ReadOnly = true;
            this.CMerge.Width = 60;
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
            this.CMD5.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.CMD5.HeaderText = "MD5";
            this.CMD5.Name = "CMD5";
            this.CMD5.ReadOnly = true;
            this.CMD5.Width = 150;
            // 
            // CStatus
            // 
            this.CStatus.HeaderText = "Status";
            this.CStatus.Name = "CStatus";
            this.CStatus.ReadOnly = true;
            // 
            // CInZip
            // 
            this.CInZip.HeaderText = "In Zip";
            this.CInZip.Name = "CInZip";
            this.CInZip.ReadOnly = true;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.romRootScanToolStripMenuItem,
            this.scanADirToolStripMenuItem,
            this.updateZipDBToolStripMenuItem,
            this.startVDriveToolStripMenuItem,
            this.closeVDriveToolStripMenuItem,
            this.extractFilesToolStripMenuItem,
            this.fixDatsToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1264, 24);
            this.menuStrip1.TabIndex = 5;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // romRootScanToolStripMenuItem
            // 
            this.romRootScanToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.quickReScanToolStripMenuItem,
            this.deepReScanToolStripMenuItem});
            this.romRootScanToolStripMenuItem.Name = "romRootScanToolStripMenuItem";
            this.romRootScanToolStripMenuItem.Size = new System.Drawing.Size(69, 20);
            this.romRootScanToolStripMenuItem.Text = "RomRoot";
            this.romRootScanToolStripMenuItem.Click += new System.EventHandler(this.romRootScanToolStripMenuItem_Click);
            // 
            // quickReScanToolStripMenuItem
            // 
            this.quickReScanToolStripMenuItem.Name = "quickReScanToolStripMenuItem";
            this.quickReScanToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.quickReScanToolStripMenuItem.Text = "Quick ReScan";
            this.quickReScanToolStripMenuItem.Click += new System.EventHandler(this.quickReScanToolStripMenuItem_Click);
            // 
            // deepReScanToolStripMenuItem
            // 
            this.deepReScanToolStripMenuItem.Name = "deepReScanToolStripMenuItem";
            this.deepReScanToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deepReScanToolStripMenuItem.Text = "Deep ReScan";
            this.deepReScanToolStripMenuItem.Click += new System.EventHandler(this.deepReScanToolStripMenuItem_Click);
            // 
            // scanADirToolStripMenuItem
            // 
            this.scanADirToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSubScanADir,
            this.toolStripSubScanADirWithDelete});
            this.scanADirToolStripMenuItem.Name = "scanADirToolStripMenuItem";
            this.scanADirToolStripMenuItem.Size = new System.Drawing.Size(73, 20);
            this.scanADirToolStripMenuItem.Text = "Scan A Dir";
            // 
            // toolStripSubScanADir
            // 
            this.toolStripSubScanADir.Name = "toolStripSubScanADir";
            this.toolStripSubScanADir.Size = new System.Drawing.Size(180, 22);
            this.toolStripSubScanADir.Text = "Scan A Dir";
            this.toolStripSubScanADir.Click += new System.EventHandler(this.scanADirToolStripMenuItem_Click);
            // 
            // toolStripSubScanADirWithDelete
            // 
            this.toolStripSubScanADirWithDelete.Name = "toolStripSubScanADirWithDelete";
            this.toolStripSubScanADirWithDelete.Size = new System.Drawing.Size(180, 22);
            this.toolStripSubScanADirWithDelete.Text = "Scan With Delete";
            this.toolStripSubScanADirWithDelete.Click += new System.EventHandler(this.scanWithDeleteToolStripMenuItem_Click);
            // 
            // updateZipDBToolStripMenuItem
            // 
            this.updateZipDBToolStripMenuItem.Name = "updateZipDBToolStripMenuItem";
            this.updateZipDBToolStripMenuItem.Size = new System.Drawing.Size(89, 20);
            this.updateZipDBToolStripMenuItem.Text = "UpdateZipDB";
            this.updateZipDBToolStripMenuItem.Click += new System.EventHandler(this.updateZipDBToolStripMenuItem_Click);
            // 
            // startVDriveToolStripMenuItem
            // 
            this.startVDriveToolStripMenuItem.Name = "startVDriveToolStripMenuItem";
            this.startVDriveToolStripMenuItem.Size = new System.Drawing.Size(83, 20);
            this.startVDriveToolStripMenuItem.Text = "Start V Drive";
            this.startVDriveToolStripMenuItem.Click += new System.EventHandler(this.startVDriveToolStripMenuItem_Click);
            // 
            // closeVDriveToolStripMenuItem
            // 
            this.closeVDriveToolStripMenuItem.Name = "closeVDriveToolStripMenuItem";
            this.closeVDriveToolStripMenuItem.Size = new System.Drawing.Size(88, 20);
            this.closeVDriveToolStripMenuItem.Text = "Close V Drive";
            this.closeVDriveToolStripMenuItem.Click += new System.EventHandler(this.closeVDriveToolStripMenuItem_Click);
            // 
            // extractFilesToolStripMenuItem
            // 
            this.extractFilesToolStripMenuItem.Name = "extractFilesToolStripMenuItem";
            this.extractFilesToolStripMenuItem.Size = new System.Drawing.Size(78, 20);
            this.extractFilesToolStripMenuItem.Text = "ExtractFiles";
            this.extractFilesToolStripMenuItem.Click += new System.EventHandler(this.extractFilesToolStripMenuItem_Click);
            // 
            // fixDatsToolStripMenuItem
            // 
            this.fixDatsToolStripMenuItem.Name = "fixDatsToolStripMenuItem";
            this.fixDatsToolStripMenuItem.Size = new System.Drawing.Size(57, 20);
            this.fixDatsToolStripMenuItem.Text = "FixDats";
            this.fixDatsToolStripMenuItem.Click += new System.EventHandler(this.fixDatsToolStripMenuItem_Click);
            // 
            // DirTree
            // 
            this.DirTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.DirTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DirTree.Location = new System.Drawing.Point(0, 0);
            this.DirTree.Name = "DirTree";
            this.DirTree.Size = new System.Drawing.Size(533, 585);
            this.DirTree.TabIndex = 0;
            this.DirTree.RvSelected += new System.Windows.Forms.MouseEventHandler(this.DirTree_RvSelected);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 761);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "frmMain";
            this.Text = "ROM Vault X";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.gbDatInfo.ResumeLayout(false);
            this.splitContainer4.Panel1.ResumeLayout(false);
            this.splitContainer4.Panel1.PerformLayout();
            this.splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).EndInit();
            this.splitContainer4.ResumeLayout(false);
            this.splitContainer5.Panel1.ResumeLayout(false);
            this.splitContainer5.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer5)).EndInit();
            this.splitContainer5.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GameGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.RomGrid)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnUpdateDats;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.SplitContainer splitContainer5;
        private RvTree DirTree;
        private System.Windows.Forms.GroupBox gbDatInfo;
        private System.Windows.Forms.Label lblDIRomsTotal;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label lblDITRomsTotal;
        private System.Windows.Forms.Label lblDITRomsMissing;
        private System.Windows.Forms.Label lblDITRomsGot;
        private System.Windows.Forms.Label lblDITRomPath;
        private System.Windows.Forms.Label lblDITPath;
        private System.Windows.Forms.Label lblDIDate;
        private System.Windows.Forms.Label lblDIAuthor;
        private System.Windows.Forms.Label lblDITDate;
        private System.Windows.Forms.Label lblDITAuthor;
        private System.Windows.Forms.Label lblDIVersion;
        private System.Windows.Forms.Label lblDICategory;
        private System.Windows.Forms.Label lblDITVersion;
        private System.Windows.Forms.Label lblDITCategory;
        private System.Windows.Forms.Label lblDIDescription;
        private System.Windows.Forms.Label lblDIName;
        private System.Windows.Forms.Label lblDITDescription;
        private System.Windows.Forms.Label lblDITName;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox gbSetInfo;
        private System.Windows.Forms.DataGridView GameGrid;
        private System.Windows.Forms.DataGridView RomGrid;
        private System.Windows.Forms.Button btnScanRoms;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem romRootScanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem quickReScanToolStripMenuItem;
        private System.Windows.Forms.DataGridViewTextBoxColumn Type;
        private System.Windows.Forms.DataGridViewTextBoxColumn CGame;
        private System.Windows.Forms.DataGridViewTextBoxColumn CDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn CCorrect;
        private System.Windows.Forms.DataGridViewTextBoxColumn CMissing;
        private System.Windows.Forms.Label lblDITRomsNoDump;
        private System.Windows.Forms.Label lblDIRomsNoDump;
        private System.Windows.Forms.CheckBox chkBoxShowMissing;
        private System.Windows.Forms.CheckBox chkBoxShowCorrect;
        private System.Windows.Forms.ToolStripMenuItem scanADirToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deepReScanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateZipDBToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startVDriveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeVDriveToolStripMenuItem;
        private System.Windows.Forms.DataGridViewTextBoxColumn CGot;
        private System.Windows.Forms.DataGridViewTextBoxColumn CRom;
        private System.Windows.Forms.DataGridViewTextBoxColumn CSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn CCompressSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn CMerge;
        private System.Windows.Forms.DataGridViewTextBoxColumn CCRC32;
        private System.Windows.Forms.DataGridViewTextBoxColumn CSHA1;
        private System.Windows.Forms.DataGridViewTextBoxColumn CMD5;
        private System.Windows.Forms.DataGridViewTextBoxColumn CStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn CInZip;
        private System.Windows.Forms.ToolStripMenuItem extractFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fixDatsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripSubScanADir;
        private System.Windows.Forms.ToolStripMenuItem toolStripSubScanADirWithDelete;
    }
}

