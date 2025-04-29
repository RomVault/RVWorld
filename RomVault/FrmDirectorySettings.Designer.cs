namespace ROMVault
{
    partial class FrmDirectorySettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmDirectorySettings));
            this.DataGridGames = new System.Windows.Forms.DataGridView();
            this.CDAT = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CArchiveType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CMergeType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CSingleArchive = new System.Windows.Forms.DataGridViewImageColumn();
            this.btnDeleteSelected = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnSet = new System.Windows.Forms.Button();
            this.txtDATLocation = new System.Windows.Forms.Label();
            this.lblDATLocation = new System.Windows.Forms.Label();
            this.lblDelete = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnResetAll = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDirMerge = new System.Windows.Forms.TabPage();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lblArchiveCompression = new System.Windows.Forms.Label();
            this.cboCompression = new System.Windows.Forms.ComboBox();
            this.chkConvertWhenFixing = new System.Windows.Forms.CheckBox();
            this.cboHeaderType = new System.Windows.Forms.ComboBox();
            this.lblHeaderType = new System.Windows.Forms.Label();
            this.lblArchiveType = new System.Windows.Forms.Label();
            this.cboFileType = new System.Windows.Forms.ComboBox();
            this.chkFileTypeOverride = new System.Windows.Forms.CheckBox();
            this.cboMergeType = new System.Windows.Forms.ComboBox();
            this.cboDirType = new System.Windows.Forms.ComboBox();
            this.lblMergeType = new System.Windows.Forms.Label();
            this.chkUseDescription = new System.Windows.Forms.CheckBox();
            this.chkMergeTypeOverride = new System.Windows.Forms.CheckBox();
            this.cboFilterType = new System.Windows.Forms.ComboBox();
            this.chkSingleArchive = new System.Windows.Forms.CheckBox();
            this.lblROMCHDFilter = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkMultiDatDirOverride = new System.Windows.Forms.CheckBox();
            this.tabAdvanced = new System.Windows.Forms.TabPage();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.dgCategories = new System.Windows.Forms.DataGridView();
            this.Category = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chkAddCategorySubDirs = new System.Windows.Forms.CheckBox();
            this.chkCompleteOnly = new System.Windows.Forms.CheckBox();
            this.chkUseIdForName = new System.Windows.Forms.CheckBox();
            this.tabExclude = new System.Windows.Forms.TabPage();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.DataGridGames)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabDirMerge.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabAdvanced.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgCategories)).BeginInit();
            this.tabExclude.SuspendLayout();
            this.SuspendLayout();
            // 
            // DataGridGames
            // 
            this.DataGridGames.AllowUserToAddRows = false;
            this.DataGridGames.AllowUserToDeleteRows = false;
            this.DataGridGames.AllowUserToResizeRows = false;
            this.DataGridGames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DataGridGames.BackgroundColor = System.Drawing.Color.White;
            this.DataGridGames.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DataGridGames.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.CDAT,
            this.CArchiveType,
            this.CMergeType,
            this.CSingleArchive});
            this.DataGridGames.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.DataGridGames.Location = new System.Drawing.Point(12, 327);
            this.DataGridGames.Name = "DataGridGames";
            this.DataGridGames.ReadOnly = true;
            this.DataGridGames.RowHeadersVisible = false;
            this.DataGridGames.RowHeadersWidth = 62;
            this.DataGridGames.RowTemplate.Height = 17;
            this.DataGridGames.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DataGridGames.ShowCellErrors = false;
            this.DataGridGames.ShowCellToolTips = false;
            this.DataGridGames.ShowEditingIcon = false;
            this.DataGridGames.ShowRowErrors = false;
            this.DataGridGames.Size = new System.Drawing.Size(670, 214);
            this.DataGridGames.TabIndex = 10;
            this.DataGridGames.DoubleClick += new System.EventHandler(this.DataGridGamesDoubleClick);
            // 
            // CDAT
            // 
            this.CDAT.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CDAT.FillWeight = 50F;
            this.CDAT.HeaderText = "Rule Path";
            this.CDAT.MinimumWidth = 8;
            this.CDAT.Name = "CDAT";
            this.CDAT.ReadOnly = true;
            // 
            // CArchiveType
            // 
            this.CArchiveType.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CArchiveType.FillWeight = 20F;
            this.CArchiveType.HeaderText = "Archive Type";
            this.CArchiveType.MinimumWidth = 8;
            this.CArchiveType.Name = "CArchiveType";
            this.CArchiveType.ReadOnly = true;
            // 
            // CMergeType
            // 
            this.CMergeType.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CMergeType.FillWeight = 20F;
            this.CMergeType.HeaderText = "Merge Type";
            this.CMergeType.MinimumWidth = 8;
            this.CMergeType.Name = "CMergeType";
            this.CMergeType.ReadOnly = true;
            // 
            // CSingleArchive
            // 
            this.CSingleArchive.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CSingleArchive.FillWeight = 10F;
            this.CSingleArchive.HeaderText = "Single";
            this.CSingleArchive.MinimumWidth = 8;
            this.CSingleArchive.Name = "CSingleArchive";
            this.CSingleArchive.ReadOnly = true;
            this.CSingleArchive.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.CSingleArchive.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // btnDeleteSelected
            // 
            this.btnDeleteSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDeleteSelected.Location = new System.Drawing.Point(12, 547);
            this.btnDeleteSelected.Name = "btnDeleteSelected";
            this.btnDeleteSelected.Size = new System.Drawing.Size(96, 25);
            this.btnDeleteSelected.TabIndex = 11;
            this.btnDeleteSelected.Text = "Delete Selected";
            this.btnDeleteSelected.UseVisualStyleBackColor = true;
            this.btnDeleteSelected.Click += new System.EventHandler(this.BtnDeleteSelectedClick);
            // 
            // btnDelete
            // 
            this.btnDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDelete.Location = new System.Drawing.Point(625, 230);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(59, 24);
            this.btnDelete.TabIndex = 39;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.BtnDeleteClick);
            // 
            // btnSet
            // 
            this.btnSet.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSet.Location = new System.Drawing.Point(625, 260);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(59, 25);
            this.btnSet.TabIndex = 14;
            this.btnSet.Text = "Apply";
            this.btnSet.UseVisualStyleBackColor = true;
            this.btnSet.Click += new System.EventHandler(this.BtnApplyClick);
            // 
            // txtDATLocation
            // 
            this.txtDATLocation.BackColor = System.Drawing.Color.White;
            this.txtDATLocation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDATLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDATLocation.Location = new System.Drawing.Point(76, 18);
            this.txtDATLocation.Name = "txtDATLocation";
            this.txtDATLocation.Size = new System.Drawing.Size(527, 22);
            this.txtDATLocation.TabIndex = 11;
            this.txtDATLocation.Text = "label2";
            this.txtDATLocation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtDATLocation.UseMnemonic = false;
            // 
            // lblDATLocation
            // 
            this.lblDATLocation.AutoSize = true;
            this.lblDATLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDATLocation.Location = new System.Drawing.Point(12, 23);
            this.lblDATLocation.Name = "lblDATLocation";
            this.lblDATLocation.Size = new System.Drawing.Size(57, 13);
            this.lblDATLocation.TabIndex = 10;
            this.lblDATLocation.Text = "Rule Path:";
            // 
            // lblDelete
            // 
            this.lblDelete.AutoSize = true;
            this.lblDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDelete.Location = new System.Drawing.Point(12, 306);
            this.lblDelete.Name = "lblDelete";
            this.lblDelete.Size = new System.Drawing.Size(111, 13);
            this.lblDelete.TabIndex = 15;
            this.lblDelete.Text = "Existing Dat Rules";
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(586, 547);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(96, 25);
            this.btnClose.TabIndex = 16;
            this.btnClose.Text = "Done";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.BtnCloseClick);
            // 
            // btnResetAll
            // 
            this.btnResetAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnResetAll.Location = new System.Drawing.Point(138, 547);
            this.btnResetAll.Name = "btnResetAll";
            this.btnResetAll.Size = new System.Drawing.Size(96, 25);
            this.btnResetAll.TabIndex = 17;
            this.btnResetAll.Text = "Reset All";
            this.btnResetAll.UseVisualStyleBackColor = true;
            this.btnResetAll.Click += new System.EventHandler(this.BtnResetAllClick);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabDirMerge);
            this.tabControl1.Controls.Add(this.tabAdvanced);
            this.tabControl1.Controls.Add(this.tabExclude);
            this.tabControl1.Location = new System.Drawing.Point(12, 49);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(608, 236);
            this.tabControl1.TabIndex = 50;
            // 
            // tabDirMerge
            // 
            this.tabDirMerge.Controls.Add(this.groupBox2);
            this.tabDirMerge.Controls.Add(this.groupBox1);
            this.tabDirMerge.Location = new System.Drawing.Point(4, 22);
            this.tabDirMerge.Name = "tabDirMerge";
            this.tabDirMerge.Padding = new System.Windows.Forms.Padding(3);
            this.tabDirMerge.Size = new System.Drawing.Size(600, 210);
            this.tabDirMerge.TabIndex = 0;
            this.tabDirMerge.Text = "Directory/Merge Rules";
            this.tabDirMerge.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lblArchiveCompression);
            this.groupBox2.Controls.Add(this.cboCompression);
            this.groupBox2.Controls.Add(this.chkConvertWhenFixing);
            this.groupBox2.Controls.Add(this.cboHeaderType);
            this.groupBox2.Controls.Add(this.lblHeaderType);
            this.groupBox2.Controls.Add(this.lblArchiveType);
            this.groupBox2.Controls.Add(this.cboFileType);
            this.groupBox2.Controls.Add(this.chkFileTypeOverride);
            this.groupBox2.Controls.Add(this.cboMergeType);
            this.groupBox2.Controls.Add(this.cboDirType);
            this.groupBox2.Controls.Add(this.lblMergeType);
            this.groupBox2.Controls.Add(this.chkUseDescription);
            this.groupBox2.Controls.Add(this.chkMergeTypeOverride);
            this.groupBox2.Controls.Add(this.cboFilterType);
            this.groupBox2.Controls.Add(this.chkSingleArchive);
            this.groupBox2.Controls.Add(this.lblROMCHDFilter);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(14, 60);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(573, 142);
            this.groupBox2.TabIndex = 50;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "DAT Rule Settings";
            // 
            // lblArchiveCompression
            // 
            this.lblArchiveCompression.AutoSize = true;
            this.lblArchiveCompression.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblArchiveCompression.Location = new System.Drawing.Point(6, 46);
            this.lblArchiveCompression.Name = "lblArchiveCompression";
            this.lblArchiveCompression.Size = new System.Drawing.Size(97, 13);
            this.lblArchiveCompression.TabIndex = 48;
            this.lblArchiveCompression.Text = "Compression Type:";
            // 
            // cboCompression
            // 
            this.cboCompression.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCompression.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboCompression.FormattingEnabled = true;
            this.cboCompression.Location = new System.Drawing.Point(105, 43);
            this.cboCompression.Name = "cboCompression";
            this.cboCompression.Size = new System.Drawing.Size(132, 21);
            this.cboCompression.TabIndex = 47;
            // 
            // chkConvertWhenFixing
            // 
            this.chkConvertWhenFixing.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkConvertWhenFixing.Location = new System.Drawing.Point(247, 46);
            this.chkConvertWhenFixing.Name = "chkConvertWhenFixing";
            this.chkConvertWhenFixing.Size = new System.Drawing.Size(120, 17);
            this.chkConvertWhenFixing.TabIndex = 49;
            this.chkConvertWhenFixing.Text = "Convert when fixing";
            this.chkConvertWhenFixing.UseVisualStyleBackColor = true;
            // 
            // cboHeaderType
            // 
            this.cboHeaderType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboHeaderType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboHeaderType.FormattingEnabled = true;
            this.cboHeaderType.Location = new System.Drawing.Point(465, 44);
            this.cboHeaderType.Name = "cboHeaderType";
            this.cboHeaderType.Size = new System.Drawing.Size(102, 21);
            this.cboHeaderType.TabIndex = 46;
            // 
            // lblHeaderType
            // 
            this.lblHeaderType.AutoSize = true;
            this.lblHeaderType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHeaderType.Location = new System.Drawing.Point(375, 47);
            this.lblHeaderType.Name = "lblHeaderType";
            this.lblHeaderType.Size = new System.Drawing.Size(72, 13);
            this.lblHeaderType.TabIndex = 45;
            this.lblHeaderType.Text = "Header Type:";
            // 
            // lblArchiveType
            // 
            this.lblArchiveType.AutoSize = true;
            this.lblArchiveType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblArchiveType.Location = new System.Drawing.Point(6, 19);
            this.lblArchiveType.Name = "lblArchiveType";
            this.lblArchiveType.Size = new System.Drawing.Size(73, 13);
            this.lblArchiveType.TabIndex = 30;
            this.lblArchiveType.Text = "Archive Type:";
            // 
            // cboFileType
            // 
            this.cboFileType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFileType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboFileType.FormattingEnabled = true;
            this.cboFileType.Location = new System.Drawing.Point(105, 16);
            this.cboFileType.Name = "cboFileType";
            this.cboFileType.Size = new System.Drawing.Size(132, 21);
            this.cboFileType.TabIndex = 29;
            this.cboFileType.SelectedIndexChanged += new System.EventHandler(this.cboFileType_SelectedIndexChanged);
            // 
            // chkFileTypeOverride
            // 
            this.chkFileTypeOverride.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkFileTypeOverride.Location = new System.Drawing.Point(247, 19);
            this.chkFileTypeOverride.Name = "chkFileTypeOverride";
            this.chkFileTypeOverride.Size = new System.Drawing.Size(120, 17);
            this.chkFileTypeOverride.TabIndex = 31;
            this.chkFileTypeOverride.Text = "Override DAT";
            this.chkFileTypeOverride.UseVisualStyleBackColor = true;
            // 
            // cboMergeType
            // 
            this.cboMergeType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMergeType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboMergeType.FormattingEnabled = true;
            this.cboMergeType.Location = new System.Drawing.Point(105, 70);
            this.cboMergeType.Name = "cboMergeType";
            this.cboMergeType.Size = new System.Drawing.Size(132, 21);
            this.cboMergeType.TabIndex = 32;
            // 
            // cboDirType
            // 
            this.cboDirType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDirType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboDirType.FormattingEnabled = true;
            this.cboDirType.Location = new System.Drawing.Point(105, 95);
            this.cboDirType.Name = "cboDirType";
            this.cboDirType.Size = new System.Drawing.Size(331, 21);
            this.cboDirType.TabIndex = 44;
            // 
            // lblMergeType
            // 
            this.lblMergeType.AutoSize = true;
            this.lblMergeType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMergeType.Location = new System.Drawing.Point(6, 73);
            this.lblMergeType.Name = "lblMergeType";
            this.lblMergeType.Size = new System.Drawing.Size(67, 13);
            this.lblMergeType.TabIndex = 33;
            this.lblMergeType.Text = "Merge Type:";
            // 
            // chkUseDescription
            // 
            this.chkUseDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkUseDescription.Location = new System.Drawing.Point(9, 119);
            this.chkUseDescription.Name = "chkUseDescription";
            this.chkUseDescription.Size = new System.Drawing.Size(297, 19);
            this.chkUseDescription.TabIndex = 42;
            this.chkUseDescription.Text = "Use description (instead of name)  for auto added paths";
            this.chkUseDescription.UseVisualStyleBackColor = true;
            // 
            // chkMergeTypeOverride
            // 
            this.chkMergeTypeOverride.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkMergeTypeOverride.Location = new System.Drawing.Point(247, 73);
            this.chkMergeTypeOverride.Name = "chkMergeTypeOverride";
            this.chkMergeTypeOverride.Size = new System.Drawing.Size(118, 17);
            this.chkMergeTypeOverride.TabIndex = 34;
            this.chkMergeTypeOverride.Text = "Override DAT";
            this.chkMergeTypeOverride.UseVisualStyleBackColor = true;
            // 
            // cboFilterType
            // 
            this.cboFilterType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFilterType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboFilterType.FormattingEnabled = true;
            this.cboFilterType.Location = new System.Drawing.Point(465, 16);
            this.cboFilterType.Name = "cboFilterType";
            this.cboFilterType.Size = new System.Drawing.Size(102, 21);
            this.cboFilterType.TabIndex = 41;
            // 
            // chkSingleArchive
            // 
            this.chkSingleArchive.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSingleArchive.Location = new System.Drawing.Point(9, 98);
            this.chkSingleArchive.Name = "chkSingleArchive";
            this.chkSingleArchive.Size = new System.Drawing.Size(95, 19);
            this.chkSingleArchive.TabIndex = 35;
            this.chkSingleArchive.Text = "Single Archive";
            this.chkSingleArchive.UseVisualStyleBackColor = true;
            this.chkSingleArchive.CheckedChanged += new System.EventHandler(this.chkSingleArchive_CheckedChanged);
            // 
            // lblROMCHDFilter
            // 
            this.lblROMCHDFilter.AutoSize = true;
            this.lblROMCHDFilter.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblROMCHDFilter.Location = new System.Drawing.Point(375, 19);
            this.lblROMCHDFilter.Name = "lblROMCHDFilter";
            this.lblROMCHDFilter.Size = new System.Drawing.Size(88, 13);
            this.lblROMCHDFilter.TabIndex = 40;
            this.lblROMCHDFilter.Text = "ROM/CHD Filter:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.chkMultiDatDirOverride);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(14, 10);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(573, 44);
            this.groupBox1.TabIndex = 49;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Multi DAT Directory Setting";
            // 
            // chkMultiDatDirOverride
            // 
            this.chkMultiDatDirOverride.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkMultiDatDirOverride.Location = new System.Drawing.Point(11, 20);
            this.chkMultiDatDirOverride.Name = "chkMultiDatDirOverride";
            this.chkMultiDatDirOverride.Size = new System.Drawing.Size(220, 16);
            this.chkMultiDatDirOverride.TabIndex = 36;
            this.chkMultiDatDirOverride.Text = "Don\'t auto add DAT directories";
            this.chkMultiDatDirOverride.UseVisualStyleBackColor = true;
            // 
            // tabAdvanced
            // 
            this.tabAdvanced.Controls.Add(this.groupBox3);
            this.tabAdvanced.Location = new System.Drawing.Point(4, 22);
            this.tabAdvanced.Name = "tabAdvanced";
            this.tabAdvanced.Padding = new System.Windows.Forms.Padding(3);
            this.tabAdvanced.Size = new System.Drawing.Size(600, 210);
            this.tabAdvanced.TabIndex = 2;
            this.tabAdvanced.Text = "Advanced Options";
            this.tabAdvanced.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.btnDown);
            this.groupBox3.Controls.Add(this.btnUp);
            this.groupBox3.Controls.Add(this.dgCategories);
            this.groupBox3.Controls.Add(this.chkAddCategorySubDirs);
            this.groupBox3.Controls.Add(this.chkCompleteOnly);
            this.groupBox3.Controls.Add(this.chkUseIdForName);
            this.groupBox3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox3.Location = new System.Drawing.Point(12, 13);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(564, 188);
            this.groupBox3.TabIndex = 50;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Advanced Options";
            // 
            // btnDown
            // 
            this.btnDown.Image = global::ROMVault.Properties.Resources.pngDown;
            this.btnDown.Location = new System.Drawing.Point(356, 94);
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(32, 35);
            this.btnDown.TabIndex = 54;
            this.btnDown.UseVisualStyleBackColor = true;
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
            // 
            // btnUp
            // 
            this.btnUp.Image = global::ROMVault.Properties.Resources.pngUp;
            this.btnUp.Location = new System.Drawing.Point(356, 58);
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(32, 35);
            this.btnUp.TabIndex = 53;
            this.btnUp.UseVisualStyleBackColor = true;
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            // 
            // dgCategories
            // 
            this.dgCategories.AllowUserToAddRows = false;
            this.dgCategories.AllowUserToDeleteRows = false;
            this.dgCategories.AllowUserToResizeColumns = false;
            this.dgCategories.AllowUserToResizeRows = false;
            this.dgCategories.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgCategories.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Category});
            this.dgCategories.EnableHeadersVisualStyles = false;
            this.dgCategories.Location = new System.Drawing.Point(394, 10);
            this.dgCategories.MultiSelect = false;
            this.dgCategories.Name = "dgCategories";
            this.dgCategories.ReadOnly = true;
            this.dgCategories.RowHeadersVisible = false;
            this.dgCategories.RowHeadersWidth = 62;
            this.dgCategories.RowTemplate.Height = 18;
            this.dgCategories.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dgCategories.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgCategories.ShowCellErrors = false;
            this.dgCategories.ShowCellToolTips = false;
            this.dgCategories.ShowEditingIcon = false;
            this.dgCategories.ShowRowErrors = false;
            this.dgCategories.Size = new System.Drawing.Size(151, 171);
            this.dgCategories.TabIndex = 52;
            // 
            // Category
            // 
            this.Category.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Category.HeaderText = "Category Priority";
            this.Category.MinimumWidth = 8;
            this.Category.Name = "Category";
            this.Category.ReadOnly = true;
            this.Category.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // chkAddCategorySubDirs
            // 
            this.chkAddCategorySubDirs.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkAddCategorySubDirs.Location = new System.Drawing.Point(6, 28);
            this.chkAddCategorySubDirs.Name = "chkAddCategorySubDirs";
            this.chkAddCategorySubDirs.Size = new System.Drawing.Size(265, 17);
            this.chkAddCategorySubDirs.TabIndex = 51;
            this.chkAddCategorySubDirs.Text = "Add Category Sub Directories";
            this.chkAddCategorySubDirs.UseVisualStyleBackColor = true;
            this.chkAddCategorySubDirs.CheckedChanged += new System.EventHandler(this.chkAddCategorySubDirs_CheckedChanged);
            // 
            // chkCompleteOnly
            // 
            this.chkCompleteOnly.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkCompleteOnly.Location = new System.Drawing.Point(6, 88);
            this.chkCompleteOnly.Name = "chkCompleteOnly";
            this.chkCompleteOnly.Size = new System.Drawing.Size(265, 17);
            this.chkCompleteOnly.TabIndex = 50;
            this.chkCompleteOnly.Text = "Only Keep Complete Sets";
            this.chkCompleteOnly.UseVisualStyleBackColor = true;
            // 
            // chkUseIdForName
            // 
            this.chkUseIdForName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkUseIdForName.Location = new System.Drawing.Point(6, 65);
            this.chkUseIdForName.Name = "chkUseIdForName";
            this.chkUseIdForName.Size = new System.Drawing.Size(265, 17);
            this.chkUseIdForName.TabIndex = 49;
            this.chkUseIdForName.Text = "Use ID for Numbered DAT Names (No-Intro DATs)";
            this.chkUseIdForName.UseVisualStyleBackColor = true;
            // 
            // tabExclude
            // 
            this.tabExclude.Controls.Add(this.label6);
            this.tabExclude.Controls.Add(this.label5);
            this.tabExclude.Controls.Add(this.textBox1);
            this.tabExclude.Location = new System.Drawing.Point(4, 22);
            this.tabExclude.Name = "tabExclude";
            this.tabExclude.Padding = new System.Windows.Forms.Padding(3);
            this.tabExclude.Size = new System.Drawing.Size(600, 210);
            this.tabExclude.TabIndex = 1;
            this.tabExclude.Text = "Filename Exclude";
            this.tabExclude.UseVisualStyleBackColor = true;
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(391, 37);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(196, 49);
            this.label6.TabIndex = 50;
            this.label6.Text = "One rule per line\r\nBasic rules support * and ? wildcards\r\nRegex rules must start " +
    "with \"regex:\"";
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(17, 19);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(169, 17);
            this.label5.TabIndex = 49;
            this.label5.Text = "Filenames not to remove from RomDir\'s";
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Location = new System.Drawing.Point(18, 37);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(361, 148);
            this.textBox1.TabIndex = 48;
            // 
            // FrmDirectorySettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(693, 580);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.btnResetAll);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblDelete);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnSet);
            this.Controls.Add(this.txtDATLocation);
            this.Controls.Add(this.lblDATLocation);
            this.Controls.Add(this.btnDeleteSelected);
            this.Controls.Add(this.DataGridGames);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(709, 500);
            this.Name = "FrmDirectorySettings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set DAT Rules";
            this.Activated += new System.EventHandler(this.FrmSetDirActivated);
            ((System.ComponentModel.ISupportInitialize)(this.DataGridGames)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabDirMerge.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.tabAdvanced.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgCategories)).EndInit();
            this.tabExclude.ResumeLayout(false);
            this.tabExclude.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView DataGridGames;
        private System.Windows.Forms.Button btnDeleteSelected;
        private System.Windows.Forms.Button btnSet;
        private System.Windows.Forms.Label txtDATLocation;
        private System.Windows.Forms.Label lblDATLocation;
        private System.Windows.Forms.Label lblDelete;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnResetAll;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDirMerge;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox cboHeaderType;
        private System.Windows.Forms.Label lblHeaderType;
        private System.Windows.Forms.Label lblArchiveType;
        private System.Windows.Forms.ComboBox cboFileType;
        private System.Windows.Forms.CheckBox chkFileTypeOverride;
        private System.Windows.Forms.ComboBox cboMergeType;
        private System.Windows.Forms.ComboBox cboDirType;
        private System.Windows.Forms.Label lblMergeType;
        private System.Windows.Forms.CheckBox chkUseDescription;
        private System.Windows.Forms.CheckBox chkMergeTypeOverride;
        private System.Windows.Forms.ComboBox cboFilterType;
        private System.Windows.Forms.CheckBox chkSingleArchive;
        private System.Windows.Forms.Label lblROMCHDFilter;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox chkMultiDatDirOverride;
        private System.Windows.Forms.TabPage tabExclude;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TabPage tabAdvanced;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox chkCompleteOnly;
        private System.Windows.Forms.CheckBox chkUseIdForName;
        private System.Windows.Forms.CheckBox chkAddCategorySubDirs;
        private System.Windows.Forms.DataGridView dgCategories;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.DataGridViewTextBoxColumn Category;
        private System.Windows.Forms.DataGridViewTextBoxColumn CDAT;
        private System.Windows.Forms.DataGridViewTextBoxColumn CArchiveType;
        private System.Windows.Forms.DataGridViewTextBoxColumn CMergeType;
        private System.Windows.Forms.DataGridViewImageColumn CSingleArchive;
        private System.Windows.Forms.Label lblArchiveCompression;
        private System.Windows.Forms.ComboBox cboCompression;
        private System.Windows.Forms.CheckBox chkConvertWhenFixing;
    }
}