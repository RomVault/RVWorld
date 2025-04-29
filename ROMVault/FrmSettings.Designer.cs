namespace ROMVault
{
    partial class FrmSettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmSettings));
            this.label1 = new System.Windows.Forms.Label();
            this.lblDATRoot = new System.Windows.Forms.Label();
            this.btnDAT = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cboFixLevel = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.chkDebugLogs = new System.Windows.Forms.CheckBox();
            this.chkCacheSaveTimer = new System.Windows.Forms.CheckBox();
            this.upTime = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.chkDoubleCheckDelete = new System.Windows.Forms.CheckBox();
            this.chkDetailedReporting = new System.Windows.Forms.CheckBox();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.chkSendFoundMIA = new System.Windows.Forms.CheckBox();
            this.chkSendFoundMIAAnon = new System.Windows.Forms.CheckBox();
            this.chkDeleteOldCueFiles = new System.Windows.Forms.CheckBox();
            this.lblSide1 = new System.Windows.Forms.Label();
            this.lblSide3 = new System.Windows.Forms.Label();
            this.lblSide4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.cbo7zStruct = new System.Windows.Forms.ComboBox();
            this.cboCores = new System.Windows.Forms.ComboBox();
            this.chkDarkMode = new System.Windows.Forms.CheckBox();
            this.chkDoNotReportFeedback = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.upTime)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(124, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "DATRoot:";
            // 
            // lblDATRoot
            // 
            this.lblDATRoot.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDATRoot.BackColor = System.Drawing.Color.White;
            this.lblDATRoot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDATRoot.Location = new System.Drawing.Point(203, 6);
            this.lblDATRoot.Name = "lblDATRoot";
            this.lblDATRoot.Size = new System.Drawing.Size(209, 22);
            this.lblDATRoot.TabIndex = 3;
            this.lblDATRoot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnDAT
            // 
            this.btnDAT.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDAT.Location = new System.Drawing.Point(421, 5);
            this.btnDAT.Name = "btnDAT";
            this.btnDAT.Size = new System.Drawing.Size(44, 24);
            this.btnDAT.TabIndex = 6;
            this.btnDAT.Text = "Set";
            this.btnDAT.UseVisualStyleBackColor = true;
            this.btnDAT.Click += new System.EventHandler(this.BtnDatClick);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(281, 499);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(89, 23);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOkClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(376, 499);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(89, 23);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelClick);
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Location = new System.Drawing.Point(125, 134);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(332, 121);
            this.textBox1.TabIndex = 12;
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(124, 59);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(348, 67);
            this.label4.TabIndex = 13;
            this.label4.Text = "Filenames not to remove:\r\n- One rule per line\r\n- Basic rules support * and ? wild" +
    "cards\r\n- Regex rules must start with regex:\'\r\n- Scanning Ignore rules must start" +
    " with \'ignore:\'";
            // 
            // cboFixLevel
            // 
            this.cboFixLevel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboFixLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFixLevel.FormattingEnabled = true;
            this.cboFixLevel.Location = new System.Drawing.Point(204, 34);
            this.cboFixLevel.Name = "cboFixLevel";
            this.cboFixLevel.Size = new System.Drawing.Size(255, 21);
            this.cboFixLevel.TabIndex = 14;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(124, 38);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(66, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Fixing Level:";
            // 
            // chkDebugLogs
            // 
            this.chkDebugLogs.AutoSize = true;
            this.chkDebugLogs.Location = new System.Drawing.Point(131, 485);
            this.chkDebugLogs.Name = "chkDebugLogs";
            this.chkDebugLogs.Size = new System.Drawing.Size(138, 21);
            this.chkDebugLogs.TabIndex = 18;
            this.chkDebugLogs.Text = "Enable Debug logging";
            this.chkDebugLogs.UseVisualStyleBackColor = true;
            // 
            // chkCacheSaveTimer
            // 
            this.chkCacheSaveTimer.AutoSize = true;
            this.chkCacheSaveTimer.Location = new System.Drawing.Point(131, 282);
            this.chkCacheSaveTimer.Name = "chkCacheSaveTimer";
            this.chkCacheSaveTimer.Size = new System.Drawing.Size(161, 21);
            this.chkCacheSaveTimer.TabIndex = 19;
            this.chkCacheSaveTimer.Text = "Save Cache on timer every";
            this.chkCacheSaveTimer.UseVisualStyleBackColor = true;
            // 
            // upTime
            // 
            this.upTime.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.upTime.Location = new System.Drawing.Point(291, 280);
            this.upTime.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.upTime.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.upTime.Name = "upTime";
            this.upTime.Size = new System.Drawing.Size(47, 20);
            this.upTime.TabIndex = 20;
            this.upTime.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(347, 285);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(44, 13);
            this.label5.TabIndex = 21;
            this.label5.Text = "Minutes";
            // 
            // chkDoubleCheckDelete
            // 
            this.chkDoubleCheckDelete.AutoSize = true;
            this.chkDoubleCheckDelete.Location = new System.Drawing.Point(131, 263);
            this.chkDoubleCheckDelete.Name = "chkDoubleCheckDelete";
            this.chkDoubleCheckDelete.Size = new System.Drawing.Size(269, 21);
            this.chkDoubleCheckDelete.TabIndex = 22;
            this.chkDoubleCheckDelete.Text = "Double check file exists elsewhere before deleting";
            this.chkDoubleCheckDelete.UseVisualStyleBackColor = true;
            // 
            // chkDetailedReporting
            // 
            this.chkDetailedReporting.AutoSize = true;
            this.chkDetailedReporting.Location = new System.Drawing.Point(131, 465);
            this.chkDetailedReporting.Name = "chkDetailedReporting";
            this.chkDetailedReporting.Size = new System.Drawing.Size(250, 21);
            this.chkDetailedReporting.TabIndex = 25;
            this.chkDetailedReporting.Text = "Show detailed actions in Fixing Status window";
            this.chkDetailedReporting.UseVisualStyleBackColor = true;
            // 
            // chkSendFoundMIA
            // 
            this.chkSendFoundMIA.AutoSize = true;
            this.chkSendFoundMIA.Location = new System.Drawing.Point(131, 403);
            this.chkSendFoundMIA.Name = "chkSendFoundMIA";
            this.chkSendFoundMIA.Size = new System.Drawing.Size(172, 21);
            this.chkSendFoundMIA.TabIndex = 27;
            this.chkSendFoundMIA.Text = "Send Found MIA notifications";
            this.chkSendFoundMIA.UseVisualStyleBackColor = true;
            this.chkSendFoundMIA.CheckedChanged += new System.EventHandler(this.chkSendFoundMIA_CheckedChanged);
            // 
            // chkSendFoundMIAAnon
            // 
            this.chkSendFoundMIAAnon.AutoSize = true;
            this.chkSendFoundMIAAnon.Location = new System.Drawing.Point(147, 421);
            this.chkSendFoundMIAAnon.Name = "chkSendFoundMIAAnon";
            this.chkSendFoundMIAAnon.Size = new System.Drawing.Size(122, 21);
            this.chkSendFoundMIAAnon.TabIndex = 28;
            this.chkSendFoundMIAAnon.Text = "Send anonymously";
            this.chkSendFoundMIAAnon.UseVisualStyleBackColor = true;
            // 
            // chkDeleteOldCueFiles
            // 
            this.chkDeleteOldCueFiles.AutoSize = true;
            this.chkDeleteOldCueFiles.Location = new System.Drawing.Point(131, 439);
            this.chkDeleteOldCueFiles.Name = "chkDeleteOldCueFiles";
            this.chkDeleteOldCueFiles.Size = new System.Drawing.Size(215, 21);
            this.chkDeleteOldCueFiles.TabIndex = 30;
            this.chkDeleteOldCueFiles.Text = "Delete previous Cue file zips in ToSort ";
            this.chkDeleteOldCueFiles.UseVisualStyleBackColor = true;
            // 
            // lblSide1
            // 
            this.lblSide1.Location = new System.Drawing.Point(19, 11);
            this.lblSide1.Name = "lblSide1";
            this.lblSide1.Size = new System.Drawing.Size(94, 13);
            this.lblSide1.TabIndex = 31;
            this.lblSide1.Text = "Core Settings :";
            this.lblSide1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblSide3
            // 
            this.lblSide3.Location = new System.Drawing.Point(19, 404);
            this.lblSide3.Name = "lblSide3";
            this.lblSide3.Size = new System.Drawing.Size(94, 13);
            this.lblSide3.TabIndex = 33;
            this.lblSide3.Text = "DATVault :";
            this.lblSide3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblSide4
            // 
            this.lblSide4.Location = new System.Drawing.Point(19, 466);
            this.lblSide4.Name = "lblSide4";
            this.lblSide4.Size = new System.Drawing.Size(94, 13);
            this.lblSide4.TabIndex = 34;
            this.lblSide4.Text = "Logging :";
            this.lblSide4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(148, 328);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(102, 13);
            this.label2.TabIndex = 37;
            this.label2.Text = "Max ZSTD workers:";
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(19, 328);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(94, 13);
            this.label6.TabIndex = 38;
            this.label6.Text = "Compression :";
            this.label6.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(148, 350);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(83, 13);
            this.label7.TabIndex = 39;
            this.label7.Text = "Default 7Z type:";
            // 
            // cbo7zStruct
            // 
            this.cbo7zStruct.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbo7zStruct.FormattingEnabled = true;
            this.cbo7zStruct.Location = new System.Drawing.Point(264, 349);
            this.cbo7zStruct.Name = "cbo7zStruct";
            this.cbo7zStruct.Size = new System.Drawing.Size(121, 21);
            this.cbo7zStruct.TabIndex = 40;
            // 
            // cboCores
            // 
            this.cboCores.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCores.FormattingEnabled = true;
            this.cboCores.Location = new System.Drawing.Point(264, 325);
            this.cboCores.Name = "cboCores";
            this.cboCores.Size = new System.Drawing.Size(78, 21);
            this.cboCores.TabIndex = 41;
            // 
            // chkDarkMode
            // 
            this.chkDarkMode.AutoSize = true;
            this.chkDarkMode.Location = new System.Drawing.Point(131, 303);
            this.chkDarkMode.Name = "chkDarkMode";
            this.chkDarkMode.Size = new System.Drawing.Size(173, 21);
            this.chkDarkMode.TabIndex = 42;
            this.chkDarkMode.Text = "Dark Mode (Restart required.)";
            this.chkDarkMode.UseVisualStyleBackColor = true;
            // 
            // chkDoNotReportFeedback
            // 
            this.chkDoNotReportFeedback.AutoSize = true;
            this.chkDoNotReportFeedback.Location = new System.Drawing.Point(131, 505);
            this.chkDoNotReportFeedback.Name = "chkDoNotReportFeedback";
            this.chkDoNotReportFeedback.Size = new System.Drawing.Size(143, 21);
            this.chkDoNotReportFeedback.TabIndex = 43;
            this.chkDoNotReportFeedback.Text = "Do not report feedback";
            this.chkDoNotReportFeedback.UseVisualStyleBackColor = true;
            // 
            // FrmSettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(477, 531);
            this.Controls.Add(this.chkDoNotReportFeedback);
            this.Controls.Add(this.chkDarkMode);
            this.Controls.Add(this.cboCores);
            this.Controls.Add(this.cbo7zStruct);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lblSide4);
            this.Controls.Add(this.lblSide3);
            this.Controls.Add(this.lblSide1);
            this.Controls.Add(this.chkDeleteOldCueFiles);
            this.Controls.Add(this.chkSendFoundMIAAnon);
            this.Controls.Add(this.chkSendFoundMIA);
            this.Controls.Add(this.chkDetailedReporting);
            this.Controls.Add(this.chkDoubleCheckDelete);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.upTime);
            this.Controls.Add(this.chkCacheSaveTimer);
            this.Controls.Add(this.chkDebugLogs);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cboFixLevel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnDAT);
            this.Controls.Add(this.lblDATRoot);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.label4);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "RomVault Settings";
            this.Load += new System.EventHandler(this.FrmConfigLoad);
            ((System.ComponentModel.ISupportInitialize)(this.upTime)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblDATRoot;
        private System.Windows.Forms.Button btnDAT;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ComboBox cboFixLevel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox chkDebugLogs;
        private System.Windows.Forms.CheckBox chkCacheSaveTimer;
        private System.Windows.Forms.NumericUpDown upTime;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox chkDoubleCheckDelete;
        private System.Windows.Forms.CheckBox chkDetailedReporting;
        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox chkSendFoundMIA;
        private System.Windows.Forms.CheckBox chkSendFoundMIAAnon;
        private System.Windows.Forms.CheckBox chkDeleteOldCueFiles;
        private System.Windows.Forms.Label lblSide1;
        private System.Windows.Forms.Label lblSide3;
        private System.Windows.Forms.Label lblSide4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox cbo7zStruct;
        private System.Windows.Forms.ComboBox cboCores;
        private System.Windows.Forms.CheckBox chkDarkMode;
        private System.Windows.Forms.CheckBox chkDoNotReportFeedback;
    }
}