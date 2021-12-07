namespace TrrntZipUI
{
    partial class FrmTrrntzip
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmTrrntzip));
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.StatusPanel = new System.Windows.Forms.Panel();
            this.picRomVault = new System.Windows.Forms.PictureBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnPause = new System.Windows.Forms.Button();
            this.tbProccessors = new System.Windows.Forms.TrackBar();
            this.picDonate = new System.Windows.Forms.PictureBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.chkFix = new System.Windows.Forms.CheckBox();
            this.cboOutType = new System.Windows.Forms.ComboBox();
            this.cboInType = new System.Windows.Forms.ComboBox();
            this.chkForce = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.lblTotalStatus = new System.Windows.Forms.Label();
            this.picTitle = new System.Windows.Forms.PictureBox();
            this.DropBox = new System.Windows.Forms.PictureBox();
            this.dataGrid = new System.Windows.Forms.DataGridView();
            this.FileName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Status = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.StatusPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picRomVault)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbProccessors)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picDonate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picTitle)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DropBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.IsSplitterFixed = true;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer.MinimumSize = new System.Drawing.Size(0, 462);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.StatusPanel);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.dataGrid);
            this.splitContainer.Size = new System.Drawing.Size(1176, 555);
            this.splitContainer.SplitterDistance = 250;
            this.splitContainer.SplitterWidth = 6;
            this.splitContainer.TabIndex = 0;
            // 
            // StatusPanel
            // 
            this.StatusPanel.Controls.Add(this.picRomVault);
            this.StatusPanel.Controls.Add(this.btnCancel);
            this.StatusPanel.Controls.Add(this.btnPause);
            this.StatusPanel.Controls.Add(this.tbProccessors);
            this.StatusPanel.Controls.Add(this.picDonate);
            this.StatusPanel.Controls.Add(this.label3);
            this.StatusPanel.Controls.Add(this.label2);
            this.StatusPanel.Controls.Add(this.chkFix);
            this.StatusPanel.Controls.Add(this.cboOutType);
            this.StatusPanel.Controls.Add(this.cboInType);
            this.StatusPanel.Controls.Add(this.chkForce);
            this.StatusPanel.Controls.Add(this.label1);
            this.StatusPanel.Controls.Add(this.lblTotalStatus);
            this.StatusPanel.Controls.Add(this.picTitle);
            this.StatusPanel.Controls.Add(this.DropBox);
            this.StatusPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StatusPanel.Location = new System.Drawing.Point(0, 0);
            this.StatusPanel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.StatusPanel.Name = "StatusPanel";
            this.StatusPanel.Size = new System.Drawing.Size(250, 555);
            this.StatusPanel.TabIndex = 0;
            // 
            // picRomVault
            // 
            this.picRomVault.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.picRomVault.Image = ((System.Drawing.Image)(resources.GetObject("picRomVault.Image")));
            this.picRomVault.Location = new System.Drawing.Point(163, 492);
            this.picRomVault.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.picRomVault.Name = "picRomVault";
            this.picRomVault.Size = new System.Drawing.Size(135, 45);
            this.picRomVault.TabIndex = 18;
            this.picRomVault.TabStop = false;
            this.picRomVault.Click += new System.EventHandler(this.picRomVault_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Enabled = false;
            this.btnCancel.Image = ((System.Drawing.Image)(resources.GetObject("btnCancel.Image")));
            this.btnCancel.Location = new System.Drawing.Point(318, 149);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(32, 31);
            this.btnCancel.TabIndex = 17;
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnPause
            // 
            this.btnPause.Enabled = false;
            this.btnPause.Image = ((System.Drawing.Image)(resources.GetObject("btnPause.Image")));
            this.btnPause.Location = new System.Drawing.Point(282, 149);
            this.btnPause.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(32, 31);
            this.btnPause.TabIndex = 16;
            this.btnPause.UseVisualStyleBackColor = true;
            this.btnPause.Click += new System.EventHandler(this.btnPause_Click);
            // 
            // tbProccessors
            // 
            this.tbProccessors.Location = new System.Drawing.Point(18, 291);
            this.tbProccessors.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tbProccessors.Name = "tbProccessors";
            this.tbProccessors.Size = new System.Drawing.Size(332, 69);
            this.tbProccessors.TabIndex = 15;
            this.tbProccessors.ValueChanged += new System.EventHandler(this.tbProccessors_ValueChanged);
            // 
            // picDonate
            // 
            this.picDonate.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.picDonate.Image = ((System.Drawing.Image)(resources.GetObject("picDonate.Image")));
            this.picDonate.Location = new System.Drawing.Point(-44, 492);
            this.picDonate.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.picDonate.Name = "picDonate";
            this.picDonate.Size = new System.Drawing.Size(201, 45);
            this.picDonate.TabIndex = 13;
            this.picDonate.TabStop = false;
            this.picDonate.Click += new System.EventHandler(this.picDonate_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(126, 225);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 20);
            this.label3.TabIndex = 12;
            this.label3.Text = "Output:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(126, 192);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 20);
            this.label2.TabIndex = 11;
            this.label2.Text = "Input:";
            // 
            // chkFix
            // 
            this.chkFix.AutoSize = true;
            this.chkFix.Checked = true;
            this.chkFix.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFix.Location = new System.Drawing.Point(198, 260);
            this.chkFix.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.chkFix.Name = "chkFix";
            this.chkFix.Size = new System.Drawing.Size(55, 24);
            this.chkFix.TabIndex = 10;
            this.chkFix.Text = "Fix";
            this.chkFix.UseVisualStyleBackColor = true;
            this.chkFix.CheckedChanged += new System.EventHandler(this.chkFix_CheckedChanged);
            // 
            // cboOutType
            // 
            this.cboOutType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboOutType.FormattingEnabled = true;
            this.cboOutType.Items.AddRange(new object[] {
            "ZIP",
            "7z",
            "Original"});
            this.cboOutType.Location = new System.Drawing.Point(198, 220);
            this.cboOutType.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cboOutType.Name = "cboOutType";
            this.cboOutType.Size = new System.Drawing.Size(150, 28);
            this.cboOutType.TabIndex = 9;
            this.cboOutType.TextChanged += new System.EventHandler(this.cboOutType_TextChanged);
            // 
            // cboInType
            // 
            this.cboInType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboInType.FormattingEnabled = true;
            this.cboInType.Items.AddRange(new object[] {
            "ZIP",
            "7Z",
            "ZIP & 7Z",
            "Files",
            "All"});
            this.cboInType.Location = new System.Drawing.Point(198, 188);
            this.cboInType.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cboInType.Name = "cboInType";
            this.cboInType.Size = new System.Drawing.Size(150, 28);
            this.cboInType.TabIndex = 8;
            this.cboInType.TextChanged += new System.EventHandler(this.cboInType_TextChanged);
            // 
            // chkForce
            // 
            this.chkForce.AutoSize = true;
            this.chkForce.Location = new System.Drawing.Point(280, 260);
            this.chkForce.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.chkForce.Name = "chkForce";
            this.chkForce.Size = new System.Drawing.Size(76, 24);
            this.chkForce.TabIndex = 7;
            this.chkForce.Text = "Force";
            this.chkForce.UseVisualStyleBackColor = true;
            this.chkForce.CheckedChanged += new System.EventHandler(this.chkForce_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(110, 154);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(169, 20);
            this.label1.TabIndex = 4;
            this.label1.Text = "<-- drop Files/Dirs here";
            // 
            // lblTotalStatus
            // 
            this.lblTotalStatus.Location = new System.Drawing.Point(14, 260);
            this.lblTotalStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTotalStatus.Name = "lblTotalStatus";
            this.lblTotalStatus.Size = new System.Drawing.Size(176, 26);
            this.lblTotalStatus.TabIndex = 3;
            this.lblTotalStatus.Text = "(0/0)";
            this.lblTotalStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // picTitle
            // 
            this.picTitle.Image = ((System.Drawing.Image)(resources.GetObject("picTitle.Image")));
            this.picTitle.Location = new System.Drawing.Point(18, 9);
            this.picTitle.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.picTitle.Name = "picTitle";
            this.picTitle.Size = new System.Drawing.Size(336, 129);
            this.picTitle.TabIndex = 2;
            this.picTitle.TabStop = false;
            this.picTitle.Click += new System.EventHandler(this.picTitle_Click);
            // 
            // DropBox
            // 
            this.DropBox.BackColor = System.Drawing.SystemColors.Control;
            this.DropBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.DropBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.DropBox.InitialImage = null;
            this.DropBox.Location = new System.Drawing.Point(15, 152);
            this.DropBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.DropBox.Name = "DropBox";
            this.DropBox.Size = new System.Drawing.Size(94, 93);
            this.DropBox.TabIndex = 0;
            this.DropBox.TabStop = false;
            // 
            // dataGrid
            // 
            this.dataGrid.AllowUserToAddRows = false;
            this.dataGrid.AllowUserToDeleteRows = false;
            this.dataGrid.AllowUserToResizeRows = false;
            this.dataGrid.BackgroundColor = System.Drawing.Color.White;
            this.dataGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.FileName,
            this.Status});
            this.dataGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGrid.Location = new System.Drawing.Point(0, 0);
            this.dataGrid.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.dataGrid.MultiSelect = false;
            this.dataGrid.Name = "dataGrid";
            this.dataGrid.ReadOnly = true;
            this.dataGrid.RowHeadersVisible = false;
            this.dataGrid.RowHeadersWidth = 62;
            this.dataGrid.ShowCellErrors = false;
            this.dataGrid.ShowEditingIcon = false;
            this.dataGrid.ShowRowErrors = false;
            this.dataGrid.Size = new System.Drawing.Size(920, 555);
            this.dataGrid.TabIndex = 0;
            // 
            // FileName
            // 
            this.FileName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.FileName.HeaderText = "FileName";
            this.FileName.MinimumWidth = 200;
            this.FileName.Name = "FileName";
            this.FileName.ReadOnly = true;
            // 
            // Status
            // 
            this.Status.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Status.HeaderText = "Status";
            this.Status.MinimumWidth = 8;
            this.Status.Name = "Status";
            this.Status.ReadOnly = true;
            this.Status.Width = 160;
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // FrmTrrntzip
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1176, 555);
            this.Controls.Add(this.splitContainer);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MinimumSize = new System.Drawing.Size(634, 482);
            this.Name = "FrmTrrntzip";
            this.Text = "Trrntzip-ui";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.StatusPanel.ResumeLayout(false);
            this.StatusPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picRomVault)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbProccessors)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picDonate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picTitle)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DropBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGrid)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Panel StatusPanel;
        private System.Windows.Forms.PictureBox DropBox;
        private System.Windows.Forms.PictureBox picTitle;
        private System.Windows.Forms.Label lblTotalStatus;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView dataGrid;
        private System.Windows.Forms.CheckBox chkForce;
        private System.Windows.Forms.ComboBox cboOutType;
        private System.Windows.Forms.ComboBox cboInType;
        private System.Windows.Forms.CheckBox chkFix;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PictureBox picDonate;
        private System.Windows.Forms.TrackBar tbProccessors;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.PictureBox picRomVault;
        private System.Windows.Forms.DataGridViewTextBoxColumn FileName;
        private System.Windows.Forms.DataGridViewTextBoxColumn Status;
        private System.Windows.Forms.Timer timer1;
    }
}

