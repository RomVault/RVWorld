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
            this.grpBoxAddNew = new System.Windows.Forms.GroupBox();
            this.chkRemoveSubDir = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.chkUseDescription = new System.Windows.Forms.CheckBox();
            this.cboFilterType = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnClearROMLocation = new System.Windows.Forms.Button();
            this.btnSetROMLocation = new System.Windows.Forms.Button();
            this.chkMultiDatDirOverride = new System.Windows.Forms.CheckBox();
            this.chkSingleArchive = new System.Windows.Forms.CheckBox();
            this.chkMergeTypeOverride = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cboMergeType = new System.Windows.Forms.ComboBox();
            this.chkFileTypeOverride = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cboFileType = new System.Windows.Forms.ComboBox();
            this.btnSet = new System.Windows.Forms.Button();
            this.txtROMLocation = new System.Windows.Forms.Label();
            this.lblROMLocation = new System.Windows.Forms.Label();
            this.txtDATLocation = new System.Windows.Forms.Label();
            this.lblDATLocation = new System.Windows.Forms.Label();
            this.lblDelete = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnResetAll = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DataGridGames)).BeginInit();
            this.grpBoxAddNew.SuspendLayout();
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
            this.DataGridGames.Location = new System.Drawing.Point(12, 230);
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
            this.CDAT.HeaderText = "DAT Location";
            this.CDAT.Name = "CDAT";
            this.CDAT.ReadOnly = true;
            // 
            // CROM
            // 
            this.CROM.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CROM.HeaderText = "ROM Location";
            this.CROM.Name = "CROM";
            this.CROM.ReadOnly = true;
            // 
            // btnDeleteSelected
            // 
            this.btnDeleteSelected.Location = new System.Drawing.Point(12, 450);
            this.btnDeleteSelected.Name = "btnDeleteSelected";
            this.btnDeleteSelected.Size = new System.Drawing.Size(96, 25);
            this.btnDeleteSelected.TabIndex = 11;
            this.btnDeleteSelected.Text = "Delete Selected";
            this.btnDeleteSelected.UseVisualStyleBackColor = true;
            this.btnDeleteSelected.Click += new System.EventHandler(this.BtnDeleteSelectedClick);
            // 
            // grpBoxAddNew
            // 
            this.grpBoxAddNew.Controls.Add(this.chkRemoveSubDir);
            this.grpBoxAddNew.Controls.Add(this.label4);
            this.grpBoxAddNew.Controls.Add(this.chkUseDescription);
            this.grpBoxAddNew.Controls.Add(this.cboFilterType);
            this.grpBoxAddNew.Controls.Add(this.label3);
            this.grpBoxAddNew.Controls.Add(this.btnDelete);
            this.grpBoxAddNew.Controls.Add(this.btnClearROMLocation);
            this.grpBoxAddNew.Controls.Add(this.btnSetROMLocation);
            this.grpBoxAddNew.Controls.Add(this.chkMultiDatDirOverride);
            this.grpBoxAddNew.Controls.Add(this.chkSingleArchive);
            this.grpBoxAddNew.Controls.Add(this.chkMergeTypeOverride);
            this.grpBoxAddNew.Controls.Add(this.label1);
            this.grpBoxAddNew.Controls.Add(this.cboMergeType);
            this.grpBoxAddNew.Controls.Add(this.chkFileTypeOverride);
            this.grpBoxAddNew.Controls.Add(this.label2);
            this.grpBoxAddNew.Controls.Add(this.cboFileType);
            this.grpBoxAddNew.Controls.Add(this.btnSet);
            this.grpBoxAddNew.Controls.Add(this.txtROMLocation);
            this.grpBoxAddNew.Controls.Add(this.lblROMLocation);
            this.grpBoxAddNew.Controls.Add(this.txtDATLocation);
            this.grpBoxAddNew.Controls.Add(this.lblDATLocation);
            this.grpBoxAddNew.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpBoxAddNew.Location = new System.Drawing.Point(12, 12);
            this.grpBoxAddNew.Name = "grpBoxAddNew";
            this.grpBoxAddNew.Size = new System.Drawing.Size(670, 180);
            this.grpBoxAddNew.TabIndex = 14;
            this.grpBoxAddNew.TabStop = false;
            this.grpBoxAddNew.Text = "Add New Directory Mapping";
            // 
            // chkRemoveSubDir
            // 
            this.chkRemoveSubDir.Location = new System.Drawing.Point(464, 130);
            this.chkRemoveSubDir.Name = "chkRemoveSubDir";
            this.chkRemoveSubDir.Size = new System.Drawing.Size(126, 17);
            this.chkRemoveSubDir.TabIndex = 44;
            this.chkRemoveSubDir.Text = "Remove Sub Dir";
            this.chkRemoveSubDir.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(351, 88);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(112, 13);
            this.label4.TabIndex = 43;
            this.label4.Text = "Directory Settings:";
            // 
            // chkUseDescription
            // 
            this.chkUseDescription.Location = new System.Drawing.Point(354, 153);
            this.chkUseDescription.Name = "chkUseDescription";
            this.chkUseDescription.Size = new System.Drawing.Size(187, 17);
            this.chkUseDescription.TabIndex = 42;
            this.chkUseDescription.Text = "Use Description for Auto Dir";
            this.chkUseDescription.UseVisualStyleBackColor = true;
            // 
            // cboFilterType
            // 
            this.cboFilterType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFilterType.FormattingEnabled = true;
            this.cboFilterType.Location = new System.Drawing.Point(122, 147);
            this.cboFilterType.Name = "cboFilterType";
            this.cboFilterType.Size = new System.Drawing.Size(102, 21);
            this.cboFilterType.TabIndex = 41;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 151);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(103, 13);
            this.label3.TabIndex = 40;
            this.label3.Text = "ROM/CHD Filter:";
            // 
            // btnDelete
            // 
            this.btnDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDelete.Location = new System.Drawing.Point(596, 109);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(68, 24);
            this.btnDelete.TabIndex = 39;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.BtnDeleteClick);
            // 
            // btnClearROMLocation
            // 
            this.btnClearROMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClearROMLocation.Image = ((System.Drawing.Image)(resources.GetObject("btnClearROMLocation.Image")));
            this.btnClearROMLocation.Location = new System.Drawing.Point(631, 51);
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
            this.btnSetROMLocation.Location = new System.Drawing.Point(596, 51);
            this.btnSetROMLocation.Margin = new System.Windows.Forms.Padding(1);
            this.btnSetROMLocation.Name = "btnSetROMLocation";
            this.btnSetROMLocation.Size = new System.Drawing.Size(33, 29);
            this.btnSetROMLocation.TabIndex = 37;
            this.btnSetROMLocation.UseVisualStyleBackColor = true;
            this.btnSetROMLocation.Click += new System.EventHandler(this.BtnSetROMLocationClick);
            // 
            // chkMultiDatDirOverride
            // 
            this.chkMultiDatDirOverride.Location = new System.Drawing.Point(354, 108);
            this.chkMultiDatDirOverride.Name = "chkMultiDatDirOverride";
            this.chkMultiDatDirOverride.Size = new System.Drawing.Size(220, 16);
            this.chkMultiDatDirOverride.TabIndex = 36;
            this.chkMultiDatDirOverride.Text = "Don\'t Auto Add DAT Directories";
            this.chkMultiDatDirOverride.UseVisualStyleBackColor = true;
            // 
            // chkSingleArchive
            // 
            this.chkSingleArchive.Location = new System.Drawing.Point(354, 130);
            this.chkSingleArchive.Name = "chkSingleArchive";
            this.chkSingleArchive.Size = new System.Drawing.Size(109, 17);
            this.chkSingleArchive.TabIndex = 35;
            this.chkSingleArchive.Text = "Single Archive";
            this.chkSingleArchive.UseVisualStyleBackColor = true;
            this.chkSingleArchive.CheckedChanged += new System.EventHandler(this.chkSingleArchive_CheckedChanged);
            // 
            // chkMergeTypeOverride
            // 
            this.chkMergeTypeOverride.Location = new System.Drawing.Point(233, 116);
            this.chkMergeTypeOverride.Name = "chkMergeTypeOverride";
            this.chkMergeTypeOverride.Size = new System.Drawing.Size(110, 17);
            this.chkMergeTypeOverride.TabIndex = 34;
            this.chkMergeTypeOverride.Text = "Override DAT";
            this.chkMergeTypeOverride.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 117);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 13);
            this.label1.TabIndex = 33;
            this.label1.Text = "Merge Type:";
            // 
            // cboMergeType
            // 
            this.cboMergeType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMergeType.FormattingEnabled = true;
            this.cboMergeType.Location = new System.Drawing.Point(122, 114);
            this.cboMergeType.Name = "cboMergeType";
            this.cboMergeType.Size = new System.Drawing.Size(102, 21);
            this.cboMergeType.TabIndex = 32;
            // 
            // chkFileTypeOverride
            // 
            this.chkFileTypeOverride.Location = new System.Drawing.Point(233, 87);
            this.chkFileTypeOverride.Name = "chkFileTypeOverride";
            this.chkFileTypeOverride.Size = new System.Drawing.Size(110, 17);
            this.chkFileTypeOverride.TabIndex = 31;
            this.chkFileTypeOverride.Text = "Override DAT";
            this.chkFileTypeOverride.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 88);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(86, 13);
            this.label2.TabIndex = 30;
            this.label2.Text = "Archive Type:";
            // 
            // cboFileType
            // 
            this.cboFileType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFileType.FormattingEnabled = true;
            this.cboFileType.Location = new System.Drawing.Point(122, 85);
            this.cboFileType.Name = "cboFileType";
            this.cboFileType.Size = new System.Drawing.Size(102, 21);
            this.cboFileType.TabIndex = 29;
            // 
            // btnSet
            // 
            this.btnSet.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSet.Location = new System.Drawing.Point(596, 140);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(68, 24);
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
            this.txtROMLocation.Location = new System.Drawing.Point(89, 55);
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
            this.lblROMLocation.Location = new System.Drawing.Point(12, 60);
            this.lblROMLocation.Name = "lblROMLocation";
            this.lblROMLocation.Size = new System.Drawing.Size(79, 13);
            this.lblROMLocation.TabIndex = 12;
            this.lblROMLocation.Text = "ROM Location:";
            // 
            // txtDATLocation
            // 
            this.txtDATLocation.BackColor = System.Drawing.Color.White;
            this.txtDATLocation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDATLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDATLocation.Location = new System.Drawing.Point(89, 25);
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
            this.lblDATLocation.Location = new System.Drawing.Point(12, 30);
            this.lblDATLocation.Name = "lblDATLocation";
            this.lblDATLocation.Size = new System.Drawing.Size(76, 13);
            this.lblDATLocation.TabIndex = 10;
            this.lblDATLocation.Text = "DAT Location:";
            // 
            // lblDelete
            // 
            this.lblDelete.AutoSize = true;
            this.lblDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDelete.Location = new System.Drawing.Point(12, 205);
            this.lblDelete.Name = "lblDelete";
            this.lblDelete.Size = new System.Drawing.Size(144, 13);
            this.lblDelete.TabIndex = 15;
            this.lblDelete.Text = "Delete Existing Mapping";
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(586, 450);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(96, 25);
            this.btnClose.TabIndex = 16;
            this.btnClose.Text = "Done";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.BtnCloseClick);
            // 
            // btnResetAll
            // 
            this.btnResetAll.Location = new System.Drawing.Point(138, 450);
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
            this.ClientSize = new System.Drawing.Size(694, 201);
            this.Controls.Add(this.btnResetAll);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblDelete);
            this.Controls.Add(this.grpBoxAddNew);
            this.Controls.Add(this.btnDeleteSelected);
            this.Controls.Add(this.DataGridGames);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmSetDirSettings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set ROM Directories";
            this.Activated += new System.EventHandler(this.FrmSetDirActivated);
            ((System.ComponentModel.ISupportInitialize)(this.DataGridGames)).EndInit();
            this.grpBoxAddNew.ResumeLayout(false);
            this.grpBoxAddNew.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView DataGridGames;
        private System.Windows.Forms.DataGridViewTextBoxColumn CDAT;
        private System.Windows.Forms.DataGridViewTextBoxColumn CROM;
        private System.Windows.Forms.Button btnDeleteSelected;
        private System.Windows.Forms.GroupBox grpBoxAddNew;
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
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cboMergeType;
        private System.Windows.Forms.CheckBox chkFileTypeOverride;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cboFileType;
        private System.Windows.Forms.Button btnSetROMLocation;
        private System.Windows.Forms.Button btnClearROMLocation;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.ComboBox cboFilterType;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox chkUseDescription;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox chkRemoveSubDir;
    }
}