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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmTrrntzip));
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.StatusPanel = new System.Windows.Forms.Panel();
            this.tbProccessors = new System.Windows.Forms.TrackBar();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.chkFix = new System.Windows.Forms.CheckBox();
            this.cboOutType = new System.Windows.Forms.ComboBox();
            this.cboInType = new System.Windows.Forms.ComboBox();
            this.chkForce = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.lblTotalStatus = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.DropBox = new System.Windows.Forms.PictureBox();
            this.dataGrid = new System.Windows.Forms.DataGridView();
            this.FileName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Status = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.StatusPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbProccessors)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
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
            this.splitContainer.MinimumSize = new System.Drawing.Size(0, 300);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.StatusPanel);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.dataGrid);
            this.splitContainer.Size = new System.Drawing.Size(761, 446);
            this.splitContainer.SplitterDistance = 250;
            this.splitContainer.TabIndex = 0;
            // 
            // StatusPanel
            // 
            this.StatusPanel.Controls.Add(this.tbProccessors);
            this.StatusPanel.Controls.Add(this.pictureBox2);
            this.StatusPanel.Controls.Add(this.label3);
            this.StatusPanel.Controls.Add(this.label2);
            this.StatusPanel.Controls.Add(this.chkFix);
            this.StatusPanel.Controls.Add(this.cboOutType);
            this.StatusPanel.Controls.Add(this.cboInType);
            this.StatusPanel.Controls.Add(this.chkForce);
            this.StatusPanel.Controls.Add(this.label1);
            this.StatusPanel.Controls.Add(this.lblTotalStatus);
            this.StatusPanel.Controls.Add(this.pictureBox1);
            this.StatusPanel.Controls.Add(this.DropBox);
            this.StatusPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StatusPanel.Location = new System.Drawing.Point(0, 0);
            this.StatusPanel.Name = "StatusPanel";
            this.StatusPanel.Size = new System.Drawing.Size(250, 446);
            this.StatusPanel.TabIndex = 0;
            // 
            // tbProccessors
            // 
            this.tbProccessors.Location = new System.Drawing.Point(12, 189);
            this.tbProccessors.Name = "tbProccessors";
            this.tbProccessors.Size = new System.Drawing.Size(221, 45);
            this.tbProccessors.TabIndex = 15;
            this.tbProccessors.ValueChanged += new System.EventHandler(this.tbProccessors_ValueChanged);
            // 
            // pictureBox2
            // 
            this.pictureBox2.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.pictureBox2.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox2.Image")));
            this.pictureBox2.Location = new System.Drawing.Point(51, 405);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(144, 29);
            this.pictureBox2.TabIndex = 13;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Click += new System.EventHandler(this.pictureBox2_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(84, 146);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(42, 13);
            this.label3.TabIndex = 12;
            this.label3.Text = "Output:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(84, 125);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(34, 13);
            this.label2.TabIndex = 11;
            this.label2.Text = "Input:";
            // 
            // chkFix
            // 
            this.chkFix.AutoSize = true;
            this.chkFix.Checked = true;
            this.chkFix.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFix.Location = new System.Drawing.Point(100, 169);
            this.chkFix.Name = "chkFix";
            this.chkFix.Size = new System.Drawing.Size(39, 17);
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
            this.cboOutType.Location = new System.Drawing.Point(132, 143);
            this.cboOutType.Name = "cboOutType";
            this.cboOutType.Size = new System.Drawing.Size(101, 21);
            this.cboOutType.TabIndex = 9;
            this.cboOutType.TextChanged += new System.EventHandler(this.cboOutType_TextChanged);
            // 
            // cboInType
            // 
            this.cboInType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboInType.FormattingEnabled = true;
            this.cboInType.Items.AddRange(new object[] {
            "ZIP",
            "7z",
            "ZIP & 7z"});
            this.cboInType.Location = new System.Drawing.Point(132, 122);
            this.cboInType.Name = "cboInType";
            this.cboInType.Size = new System.Drawing.Size(101, 21);
            this.cboInType.TabIndex = 8;
            this.cboInType.TextChanged += new System.EventHandler(this.cboInType_TextChanged);
            // 
            // chkForce
            // 
            this.chkForce.AutoSize = true;
            this.chkForce.Location = new System.Drawing.Point(180, 169);
            this.chkForce.Name = "chkForce";
            this.chkForce.Size = new System.Drawing.Size(53, 17);
            this.chkForce.TabIndex = 7;
            this.chkForce.Text = "Force";
            this.chkForce.UseVisualStyleBackColor = true;
            this.chkForce.CheckedChanged += new System.EventHandler(this.chkForce_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(79, 100);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "<-- drop Zip files here";
            // 
            // lblTotalStatus
            // 
            this.lblTotalStatus.Location = new System.Drawing.Point(9, 169);
            this.lblTotalStatus.Name = "lblTotalStatus";
            this.lblTotalStatus.Size = new System.Drawing.Size(89, 14);
            this.lblTotalStatus.TabIndex = 3;
            this.lblTotalStatus.Text = "(0/0)";
            this.lblTotalStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(250, 90);
            this.pictureBox1.TabIndex = 2;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // DropBox
            // 
            this.DropBox.BackColor = System.Drawing.SystemColors.Control;
            this.DropBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.DropBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.DropBox.InitialImage = null;
            this.DropBox.Location = new System.Drawing.Point(10, 99);
            this.DropBox.Name = "DropBox";
            this.DropBox.Size = new System.Drawing.Size(63, 61);
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
            this.dataGrid.MultiSelect = false;
            this.dataGrid.Name = "dataGrid";
            this.dataGrid.ReadOnly = true;
            this.dataGrid.RowHeadersVisible = false;
            this.dataGrid.ShowCellErrors = false;
            this.dataGrid.ShowEditingIcon = false;
            this.dataGrid.ShowRowErrors = false;
            this.dataGrid.Size = new System.Drawing.Size(507, 446);
            this.dataGrid.TabIndex = 0;
            // 
            // FileName
            // 
            this.FileName.HeaderText = "FileName";
            this.FileName.Name = "FileName";
            this.FileName.ReadOnly = true;
            this.FileName.Width = 300;
            // 
            // Status
            // 
            this.Status.HeaderText = "Status";
            this.Status.Name = "Status";
            this.Status.ReadOnly = true;
            this.Status.Width = 300;
            // 
            // FrmTrrntzip
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(761, 446);
            this.Controls.Add(this.splitContainer);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmTrrntzip";
            this.Text = "Trrntzip .Net (V2.6.0)";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.StatusPanel.ResumeLayout(false);
            this.StatusPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbProccessors)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DropBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGrid)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Panel StatusPanel;
        private System.Windows.Forms.PictureBox DropBox;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label lblTotalStatus;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView dataGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn FileName;
        private System.Windows.Forms.DataGridViewTextBoxColumn Status;
        private System.Windows.Forms.CheckBox chkForce;
        private System.Windows.Forms.ComboBox cboOutType;
        private System.Windows.Forms.ComboBox cboInType;
        private System.Windows.Forms.CheckBox chkFix;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.TrackBar tbProccessors;
    }
}

