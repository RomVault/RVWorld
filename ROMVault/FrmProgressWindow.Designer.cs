namespace ROMVault
{
    partial class FrmProgressWindow
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmProgressWindow));
            this.progressBar2 = new System.Windows.Forms.ProgressBar();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label = new System.Windows.Forms.Label();
            this.cancelButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.lbl2Prog = new System.Windows.Forms.Label();
            this.ErrorGrid = new System.Windows.Forms.DataGridView();
            this.CError = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CErrorFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.ErrorGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // progressBar2
            // 
            this.progressBar2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar2.Location = new System.Drawing.Point(12, 71);
            this.progressBar2.Name = "progressBar2";
            this.progressBar2.Size = new System.Drawing.Size(400, 22);
            this.progressBar2.TabIndex = 3;
            this.progressBar2.Visible = false;
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 99);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(400, 23);
            this.progressBar.TabIndex = 1;
            // 
            // label
            // 
            this.label.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label.AutoEllipsis = true;
            this.label.Location = new System.Drawing.Point(12, 9);
            this.label.Name = "label";
            this.label.Size = new System.Drawing.Size(368, 15);
            this.label.TabIndex = 0;
            this.label.Text = "Starting operation...";
            this.label.UseMnemonic = false;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(424, 99);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 2;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.CancelButtonClick);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoEllipsis = true;
            this.label2.Location = new System.Drawing.Point(12, 34);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(480, 16);
            this.label2.TabIndex = 4;
            this.label2.UseMnemonic = false;
            this.label2.Visible = false;
            // 
            // lbl2Prog
            // 
            this.lbl2Prog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lbl2Prog.Location = new System.Drawing.Point(386, 9);
            this.lbl2Prog.Name = "lbl2Prog";
            this.lbl2Prog.Size = new System.Drawing.Size(109, 25);
            this.lbl2Prog.TabIndex = 5;
            this.lbl2Prog.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lbl2Prog.Visible = false;
            // 
            // ErrorGrid
            // 
            this.ErrorGrid.AllowUserToAddRows = false;
            this.ErrorGrid.AllowUserToDeleteRows = false;
            this.ErrorGrid.AllowUserToResizeRows = false;
            this.ErrorGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.ErrorGrid.BackgroundColor = System.Drawing.Color.White;
            this.ErrorGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.ErrorGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.CError,
            this.CErrorFile});
            this.ErrorGrid.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.ErrorGrid.Location = new System.Drawing.Point(0, 0);
            this.ErrorGrid.MultiSelect = false;
            this.ErrorGrid.Name = "ErrorGrid";
            this.ErrorGrid.ReadOnly = true;
            this.ErrorGrid.RowHeadersVisible = false;
            this.ErrorGrid.RowTemplate.Height = 17;
            this.ErrorGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.ErrorGrid.ShowCellErrors = false;
            this.ErrorGrid.ShowCellToolTips = false;
            this.ErrorGrid.ShowEditingIcon = false;
            this.ErrorGrid.ShowRowErrors = false;
            this.ErrorGrid.Size = new System.Drawing.Size(511, 186);
            this.ErrorGrid.TabIndex = 6;
            this.ErrorGrid.SelectionChanged += new System.EventHandler(this.ErrorGridSelectionChanged);
            // 
            // CError
            // 
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.CError.DefaultCellStyle = dataGridViewCellStyle1;
            this.CError.HeaderText = "Error";
            this.CError.Name = "CError";
            this.CError.ReadOnly = true;
            this.CError.Width = 200;
            // 
            // CErrorFile
            // 
            this.CErrorFile.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.CErrorFile.HeaderText = "Error Filename";
            this.CErrorFile.Name = "CErrorFile";
            this.CErrorFile.ReadOnly = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.label3);
            this.splitContainer1.Panel1.Controls.Add(this.progressBar2);
            this.splitContainer1.Panel1.Controls.Add(this.label);
            this.splitContainer1.Panel1.Controls.Add(this.progressBar);
            this.splitContainer1.Panel1.Controls.Add(this.lbl2Prog);
            this.splitContainer1.Panel1.Controls.Add(this.label2);
            this.splitContainer1.Panel1.Controls.Add(this.cancelButton);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.ErrorGrid);
            this.splitContainer1.Panel2.Resize += new System.EventHandler(this.splitContainer1_Panel2_Resize);
            this.splitContainer1.Size = new System.Drawing.Size(511, 320);
            this.splitContainer1.SplitterDistance = 130;
            this.splitContainer1.TabIndex = 8;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoEllipsis = true;
            this.label3.Location = new System.Drawing.Point(12, 50);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(480, 16);
            this.label3.TabIndex = 6;
            this.label3.UseMnemonic = false;
            // 
            // FrmProgressWindow
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(511, 320);
            this.Controls.Add(this.splitContainer1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmProgressWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "RomVault Progress";
            this.Shown += new System.EventHandler(this.FrmProgressWindowNewShown);
            this.Resize += new System.EventHandler(this.FrmProgressWindow_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.ErrorGrid)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar2;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label label;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lbl2Prog;
        private System.Windows.Forms.DataGridView ErrorGrid;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridViewTextBoxColumn CError;
        private System.Windows.Forms.DataGridViewTextBoxColumn CErrorFile;
        private System.Windows.Forms.Label label3;
    }
}