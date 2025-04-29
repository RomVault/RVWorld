namespace ROMVault
{
    partial class FrmDirectoryMappings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmDirectoryMappings));
            this.DGDirectoryMappingRules = new System.Windows.Forms.DataGridView();
            this.CPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CLocation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnDeleteSelected = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnSet = new System.Windows.Forms.Button();
            this.txtROMLocation = new System.Windows.Forms.Label();
            this.lblROMLocation = new System.Windows.Forms.Label();
            this.txtDATLocation = new System.Windows.Forms.Label();
            this.lblDATLocation = new System.Windows.Forms.Label();
            this.lblDelete = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnResetAll = new System.Windows.Forms.Button();
            this.btnClearROMLocation = new System.Windows.Forms.Button();
            this.btnSetROMLocation = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DGDirectoryMappingRules)).BeginInit();
            this.SuspendLayout();
            // 
            // DGDirectoryMappingRules
            // 
            this.DGDirectoryMappingRules.AllowUserToAddRows = false;
            this.DGDirectoryMappingRules.AllowUserToDeleteRows = false;
            this.DGDirectoryMappingRules.AllowUserToResizeRows = false;
            this.DGDirectoryMappingRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DGDirectoryMappingRules.BackgroundColor = System.Drawing.Color.White;
            this.DGDirectoryMappingRules.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DGDirectoryMappingRules.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.CPath,
            this.CLocation});
            this.DGDirectoryMappingRules.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.DGDirectoryMappingRules.Location = new System.Drawing.Point(12, 140);
            this.DGDirectoryMappingRules.Name = "DGDirectoryMappingRules";
            this.DGDirectoryMappingRules.ReadOnly = true;
            this.DGDirectoryMappingRules.RowHeadersVisible = false;
            this.DGDirectoryMappingRules.RowHeadersWidth = 62;
            this.DGDirectoryMappingRules.RowTemplate.Height = 17;
            this.DGDirectoryMappingRules.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DGDirectoryMappingRules.ShowCellErrors = false;
            this.DGDirectoryMappingRules.ShowCellToolTips = false;
            this.DGDirectoryMappingRules.ShowEditingIcon = false;
            this.DGDirectoryMappingRules.ShowRowErrors = false;
            this.DGDirectoryMappingRules.Size = new System.Drawing.Size(670, 214);
            this.DGDirectoryMappingRules.TabIndex = 10;
            this.DGDirectoryMappingRules.DoubleClick += new System.EventHandler(this.DataGridGamesDoubleClick);
            // 
            // CPath
            // 
            this.CPath.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CPath.HeaderText = "Rule Path";
            this.CPath.MinimumWidth = 8;
            this.CPath.Name = "CPath";
            this.CPath.ReadOnly = true;
            // 
            // CLocation
            // 
            this.CLocation.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CLocation.HeaderText = "Dir Location";
            this.CLocation.MinimumWidth = 8;
            this.CLocation.Name = "CLocation";
            this.CLocation.ReadOnly = true;
            // 
            // btnDeleteSelected
            // 
            this.btnDeleteSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDeleteSelected.Location = new System.Drawing.Point(12, 360);
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
            this.btnDelete.Location = new System.Drawing.Point(543, 83);
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
            this.btnSet.Location = new System.Drawing.Point(606, 83);
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
            this.txtROMLocation.UseMnemonic = false;
            // 
            // lblROMLocation
            // 
            this.lblROMLocation.AutoSize = true;
            this.lblROMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblROMLocation.Location = new System.Drawing.Point(22, 53);
            this.lblROMLocation.Name = "lblROMLocation";
            this.lblROMLocation.Size = new System.Drawing.Size(106, 20);
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
            this.txtDATLocation.UseMnemonic = false;
            // 
            // lblDATLocation
            // 
            this.lblDATLocation.AutoSize = true;
            this.lblDATLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDATLocation.Location = new System.Drawing.Point(22, 23);
            this.lblDATLocation.Name = "lblDATLocation";
            this.lblDATLocation.Size = new System.Drawing.Size(87, 20);
            this.lblDATLocation.TabIndex = 10;
            this.lblDATLocation.Text = "Rule Path:";
            // 
            // lblDelete
            // 
            this.lblDelete.AutoSize = true;
            this.lblDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDelete.Location = new System.Drawing.Point(12, 114);
            this.lblDelete.Name = "lblDelete";
            this.lblDelete.Size = new System.Drawing.Size(152, 20);
            this.lblDelete.TabIndex = 15;
            this.lblDelete.Text = "Existing Mapping";
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(586, 360);
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
            this.btnResetAll.Location = new System.Drawing.Point(138, 360);
            this.btnResetAll.Name = "btnResetAll";
            this.btnResetAll.Size = new System.Drawing.Size(96, 25);
            this.btnResetAll.TabIndex = 17;
            this.btnResetAll.Text = "Reset All";
            this.btnResetAll.UseVisualStyleBackColor = true;
            this.btnResetAll.Click += new System.EventHandler(this.BtnResetAllClick);
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
            // FrmDirectoryMappings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(693, 389);
            this.Controls.Add(this.btnResetAll);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblDelete);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnClearROMLocation);
            this.Controls.Add(this.btnSetROMLocation);
            this.Controls.Add(this.btnSet);
            this.Controls.Add(this.txtROMLocation);
            this.Controls.Add(this.lblROMLocation);
            this.Controls.Add(this.txtDATLocation);
            this.Controls.Add(this.lblDATLocation);
            this.Controls.Add(this.btnDeleteSelected);
            this.Controls.Add(this.DGDirectoryMappingRules);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmDirectoryMappings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set Directory Mapping";
            this.Activated += new System.EventHandler(this.FrmSetDirActivated);
            ((System.ComponentModel.ISupportInitialize)(this.DGDirectoryMappingRules)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView DGDirectoryMappingRules;
        private System.Windows.Forms.Button btnDeleteSelected;
        private System.Windows.Forms.Button btnSet;
        private System.Windows.Forms.Label txtROMLocation;
        private System.Windows.Forms.Label lblROMLocation;
        private System.Windows.Forms.Label txtDATLocation;
        private System.Windows.Forms.Label lblDATLocation;
        private System.Windows.Forms.Label lblDelete;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnResetAll;
        private System.Windows.Forms.Button btnSetROMLocation;
        private System.Windows.Forms.Button btnClearROMLocation;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.DataGridViewTextBoxColumn CPath;
        private System.Windows.Forms.DataGridViewTextBoxColumn CLocation;
    }
}