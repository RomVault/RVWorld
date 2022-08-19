namespace ROMVault
{
    partial class FrmSetDirSettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmSetDirSettings));
            this.DataGridGames = new System.Windows.Forms.DataGridView();
            this.CDAT = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CROM = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnDeleteSelected = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
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
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnClearROMLocation = new System.Windows.Forms.Button();
            this.btnSetROMLocation = new System.Windows.Forms.Button();
            this.btnSet = new System.Windows.Forms.Button();
            this.txtROMLocation = new System.Windows.Forms.Label();
            this.lblROMLocation = new System.Windows.Forms.Label();
            this.txtDATLocation = new System.Windows.Forms.Label();
            this.lblDATLocation = new System.Windows.Forms.Label();
            this.lblDelete = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnResetAll = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DataGridGames)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // DataGridGames
            // 
            this.DataGridGames.AllowUserToAddRows = false;
            this.DataGridGames.AllowUserToDeleteRows = false;
            this.DataGridGames.AllowUserToResizeRows = false;
            this.DataGridGames.BackgroundColor = System.Drawing.Color.White;
            this.DataGridGames.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DataGridGames.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.CDAT,
            this.CROM});
            this.DataGridGames.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.DataGridGames.Location = new System.Drawing.Point(12, 342);
            this.DataGridGames.Name = "DataGridGames";
            this.DataGridGames.ReadOnly = true;
            this.DataGridGames.RowHeadersVisible = false;
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
            this.CDAT.HeaderText = "Rule Path";
            this.CDAT.Name = "CDAT";
            this.CDAT.ReadOnly = true;
            // 
            // CROM
            // 
            this.CROM.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CROM.HeaderText = "Dir Location";
            this.CROM.Name = "CROM";
            this.CROM.ReadOnly = true;
            // 
            // btnDeleteSelected
            // 
            this.btnDeleteSelected.Location = new System.Drawing.Point(12, 562);
            this.btnDeleteSelected.Name = "btnDeleteSelected";
            this.btnDeleteSelected.Size = new System.Drawing.Size(96, 25);
            this.btnDeleteSelected.TabIndex = 11;
            this.btnDeleteSelected.Text = "Delete Selected";
            this.btnDeleteSelected.UseVisualStyleBackColor = true;
            this.btnDeleteSelected.Click += new System.EventHandler(this.BtnDeleteSelectedClick);
            // 
            // groupBox2
            // 
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
            this.groupBox2.Location = new System.Drawing.Point(19, 129);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(328, 180);
            this.groupBox2.TabIndex = 49;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "DAT Rule Setttings";
            // 
            // cboHeaderType
            // 
            this.cboHeaderType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboHeaderType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboHeaderType.FormattingEnabled = true;
            this.cboHeaderType.Location = new System.Drawing.Point(110, 96);
            this.cboHeaderType.Name = "cboHeaderType";
            this.cboHeaderType.Size = new System.Drawing.Size(102, 21);
            this.cboHeaderType.TabIndex = 46;
            // 
            // lblHeaderType
            // 
            this.lblHeaderType.AutoSize = true;
            this.lblHeaderType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHeaderType.Location = new System.Drawing.Point(6, 99);
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
            this.cboFileType.Location = new System.Drawing.Point(110, 16);
            this.cboFileType.Name = "cboFileType";
            this.cboFileType.Size = new System.Drawing.Size(102, 21);
            this.cboFileType.TabIndex = 29;
            // 
            // chkFileTypeOverride
            // 
            this.chkFileTypeOverride.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkFileTypeOverride.Location = new System.Drawing.Point(220, 19);
            this.chkFileTypeOverride.Name = "chkFileTypeOverride";
            this.chkFileTypeOverride.Size = new System.Drawing.Size(96, 17);
            this.chkFileTypeOverride.TabIndex = 31;
            this.chkFileTypeOverride.Text = "Override DAT";
            this.chkFileTypeOverride.UseVisualStyleBackColor = true;
            // 
            // cboMergeType
            // 
            this.cboMergeType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMergeType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboMergeType.FormattingEnabled = true;
            this.cboMergeType.Location = new System.Drawing.Point(110, 43);
            this.cboMergeType.Name = "cboMergeType";
            this.cboMergeType.Size = new System.Drawing.Size(102, 21);
            this.cboMergeType.TabIndex = 32;
            // 
            // cboDirType
            // 
            this.cboDirType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDirType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboDirType.FormattingEnabled = true;
            this.cboDirType.Location = new System.Drawing.Point(136, 127);
            this.cboDirType.Name = "cboDirType";
            this.cboDirType.Size = new System.Drawing.Size(183, 21);
            this.cboDirType.TabIndex = 44;
            // 
            // lblMergeType
            // 
            this.lblMergeType.AutoSize = true;
            this.lblMergeType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMergeType.Location = new System.Drawing.Point(6, 46);
            this.lblMergeType.Name = "lblMergeType";
            this.lblMergeType.Size = new System.Drawing.Size(67, 13);
            this.lblMergeType.TabIndex = 33;
            this.lblMergeType.Text = "Merge Type:";
            // 
            // chkUseDescription
            // 
            this.chkUseDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkUseDescription.Location = new System.Drawing.Point(9, 154);
            this.chkUseDescription.Name = "chkUseDescription";
            this.chkUseDescription.Size = new System.Drawing.Size(310, 17);
            this.chkUseDescription.TabIndex = 42;
            this.chkUseDescription.Text = "Use description (instead of name)  for auto added paths";
            this.chkUseDescription.UseVisualStyleBackColor = true;
            // 
            // chkMergeTypeOverride
            // 
            this.chkMergeTypeOverride.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkMergeTypeOverride.Location = new System.Drawing.Point(220, 46);
            this.chkMergeTypeOverride.Name = "chkMergeTypeOverride";
            this.chkMergeTypeOverride.Size = new System.Drawing.Size(94, 17);
            this.chkMergeTypeOverride.TabIndex = 34;
            this.chkMergeTypeOverride.Text = "Override DAT";
            this.chkMergeTypeOverride.UseVisualStyleBackColor = true;
            // 
            // cboFilterType
            // 
            this.cboFilterType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFilterType.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboFilterType.FormattingEnabled = true;
            this.cboFilterType.Location = new System.Drawing.Point(110, 70);
            this.cboFilterType.Name = "cboFilterType";
            this.cboFilterType.Size = new System.Drawing.Size(102, 21);
            this.cboFilterType.TabIndex = 41;
            // 
            // chkSingleArchive
            // 
            this.chkSingleArchive.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSingleArchive.Location = new System.Drawing.Point(9, 130);
            this.chkSingleArchive.Name = "chkSingleArchive";
            this.chkSingleArchive.Size = new System.Drawing.Size(118, 17);
            this.chkSingleArchive.TabIndex = 35;
            this.chkSingleArchive.Text = "Single Archive";
            this.chkSingleArchive.UseVisualStyleBackColor = true;
            this.chkSingleArchive.CheckedChanged += new System.EventHandler(this.chkSingleArchive_CheckedChanged);
            // 
            // lblROMCHDFilter
            // 
            this.lblROMCHDFilter.AutoSize = true;
            this.lblROMCHDFilter.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblROMCHDFilter.Location = new System.Drawing.Point(6, 73);
            this.lblROMCHDFilter.Name = "lblROMCHDFilter";
            this.lblROMCHDFilter.Size = new System.Drawing.Size(88, 13);
            this.lblROMCHDFilter.TabIndex = 40;
            this.lblROMCHDFilter.Text = "ROM/CHD Filter:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.chkMultiDatDirOverride);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(19, 78);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(328, 44);
            this.groupBox1.TabIndex = 48;
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
            // label6
            // 
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(361, 257);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(243, 42);
            this.label6.TabIndex = 47;
            this.label6.Text = "One rule per line\r\nBasic rules support * and ? wildcards\r\nRegex rules must start " +
    "with \"regex:\"";
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(359, 82);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(243, 17);
            this.label5.TabIndex = 46;
            this.label5.Text = "Filenames not to remove from RomDir\'s";
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Location = new System.Drawing.Point(360, 100);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(244, 148);
            this.textBox1.TabIndex = 45;
            // 
            // btnDelete
            // 
            this.btnDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDelete.Location = new System.Drawing.Point(614, 257);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(59, 24);
            this.btnDelete.TabIndex = 39;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.BtnDeleteClick);
            // 
            // btnClearROMLocation
            // 
            this.btnClearROMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClearROMLocation.Image = ((System.Drawing.Image)(resources.GetObject("btnClearROMLocation.Image")));
            this.btnClearROMLocation.Location = new System.Drawing.Point(641, 44);
            this.btnClearROMLocation.Margin = new System.Windows.Forms.Padding(1);
            this.btnClearROMLocation.Name = "btnClearROMLocation";
            this.btnClearROMLocation.Size = new System.Drawing.Size(33, 29);
            this.btnClearROMLocation.TabIndex = 38;
            this.btnClearROMLocation.UseVisualStyleBackColor = true;
            this.btnClearROMLocation.Click += new System.EventHandler(this.btnClearROMLocation_Click);
            // 
            // btnSetROMLocation
            // 
            this.btnSetROMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSetROMLocation.Image = ((System.Drawing.Image)(resources.GetObject("btnSetROMLocation.Image")));
            this.btnSetROMLocation.Location = new System.Drawing.Point(606, 44);
            this.btnSetROMLocation.Margin = new System.Windows.Forms.Padding(1);
            this.btnSetROMLocation.Name = "btnSetROMLocation";
            this.btnSetROMLocation.Size = new System.Drawing.Size(33, 29);
            this.btnSetROMLocation.TabIndex = 37;
            this.btnSetROMLocation.UseVisualStyleBackColor = true;
            this.btnSetROMLocation.Click += new System.EventHandler(this.BtnSetROMLocationClick);
            // 
            // btnSet
            // 
            this.btnSet.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSet.Location = new System.Drawing.Point(614, 285);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(59, 25);
            this.btnSet.TabIndex = 14;
            this.btnSet.Text = "Apply";
            this.btnSet.UseVisualStyleBackColor = true;
            this.btnSet.Click += new System.EventHandler(this.BtnApplyClick);
            // 
            // txtROMLocation
            // 
            this.txtROMLocation.BackColor = System.Drawing.Color.White;
            this.txtROMLocation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtROMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtROMLocation.Location = new System.Drawing.Point(99, 48);
            this.txtROMLocation.Name = "txtROMLocation";
            this.txtROMLocation.Size = new System.Drawing.Size(503, 22);
            this.txtROMLocation.TabIndex = 13;
            this.txtROMLocation.Text = "label2";
            this.txtROMLocation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblROMLocation
            // 
            this.lblROMLocation.AutoSize = true;
            this.lblROMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblROMLocation.Location = new System.Drawing.Point(22, 53);
            this.lblROMLocation.Name = "lblROMLocation";
            this.lblROMLocation.Size = new System.Drawing.Size(67, 13);
            this.lblROMLocation.TabIndex = 12;
            this.lblROMLocation.Text = "Dir Location:";
            // 
            // txtDATLocation
            // 
            this.txtDATLocation.BackColor = System.Drawing.Color.White;
            this.txtDATLocation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDATLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDATLocation.Location = new System.Drawing.Point(99, 18);
            this.txtDATLocation.Name = "txtDATLocation";
            this.txtDATLocation.Size = new System.Drawing.Size(503, 22);
            this.txtDATLocation.TabIndex = 11;
            this.txtDATLocation.Text = "label2";
            this.txtDATLocation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblDATLocation
            // 
            this.lblDATLocation.AutoSize = true;
            this.lblDATLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDATLocation.Location = new System.Drawing.Point(22, 23);
            this.lblDATLocation.Name = "lblDATLocation";
            this.lblDATLocation.Size = new System.Drawing.Size(57, 13);
            this.lblDATLocation.TabIndex = 10;
            this.lblDATLocation.Text = "Rule Path:";
            // 
            // lblDelete
            // 
            this.lblDelete.AutoSize = true;
            this.lblDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDelete.Location = new System.Drawing.Point(12, 321);
            this.lblDelete.Name = "lblDelete";
            this.lblDelete.Size = new System.Drawing.Size(144, 13);
            this.lblDelete.TabIndex = 15;
            this.lblDelete.Text = "Delete Existing Mapping";
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(586, 562);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(96, 25);
            this.btnClose.TabIndex = 16;
            this.btnClose.Text = "Done";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.BtnCloseClick);
            // 
            // btnResetAll
            // 
            this.btnResetAll.Location = new System.Drawing.Point(138, 562);
            this.btnResetAll.Name = "btnResetAll";
            this.btnResetAll.Size = new System.Drawing.Size(96, 25);
            this.btnResetAll.TabIndex = 17;
            this.btnResetAll.Text = "Reset All";
            this.btnResetAll.UseVisualStyleBackColor = true;
            this.btnResetAll.Click += new System.EventHandler(this.BtnResetAllClick);
            // 
            // FrmSetDirSettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(693, 352);
            this.Controls.Add(this.btnResetAll);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblDelete);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnClearROMLocation);
            this.Controls.Add(this.btnSetROMLocation);
            this.Controls.Add(this.btnSet);
            this.Controls.Add(this.txtROMLocation);
            this.Controls.Add(this.lblROMLocation);
            this.Controls.Add(this.txtDATLocation);
            this.Controls.Add(this.lblDATLocation);
            this.Controls.Add(this.btnDeleteSelected);
            this.Controls.Add(this.DataGridGames);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmSetDirSettings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set Directory / DATs Rules";
            this.Activated += new System.EventHandler(this.FrmSetDirActivated);
            ((System.ComponentModel.ISupportInitialize)(this.DataGridGames)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView DataGridGames;
        private System.Windows.Forms.Button btnDeleteSelected;
        private System.Windows.Forms.Button btnSet;
        private System.Windows.Forms.Label txtROMLocation;
        private System.Windows.Forms.Label lblROMLocation;
        private System.Windows.Forms.Label txtDATLocation;
        private System.Windows.Forms.Label lblDATLocation;
        private System.Windows.Forms.Label lblDelete;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnResetAll;
        private System.Windows.Forms.CheckBox chkMultiDatDirOverride;
        private System.Windows.Forms.CheckBox chkSingleArchive;
        private System.Windows.Forms.CheckBox chkMergeTypeOverride;
        private System.Windows.Forms.Label lblMergeType;
        private System.Windows.Forms.ComboBox cboMergeType;
        private System.Windows.Forms.CheckBox chkFileTypeOverride;
        private System.Windows.Forms.Label lblArchiveType;
        private System.Windows.Forms.ComboBox cboFileType;
        private System.Windows.Forms.Button btnSetROMLocation;
        private System.Windows.Forms.Button btnClearROMLocation;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.ComboBox cboFilterType;
        private System.Windows.Forms.Label lblROMCHDFilter;
        private System.Windows.Forms.CheckBox chkUseDescription;
        private System.Windows.Forms.ComboBox cboDirType;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.DataGridViewTextBoxColumn CDAT;
        private System.Windows.Forms.DataGridViewTextBoxColumn CROM;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox cboHeaderType;
        private System.Windows.Forms.Label lblHeaderType;
    }
}