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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.lblSideBehavior = new System.Windows.Forms.Label();
            this.tabCHD = new System.Windows.Forms.TabPage();
            ((System.ComponentModel.ISupportInitialize)(this.upTime)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(114, 15);
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
            this.lblDATRoot.Location = new System.Drawing.Point(193, 11);
            this.lblDATRoot.Name = "lblDATRoot";
            this.lblDATRoot.Size = new System.Drawing.Size(189, 22);
            this.lblDATRoot.TabIndex = 3;
            this.lblDATRoot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnDAT
            // 
            this.btnDAT.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDAT.Location = new System.Drawing.Point(388, 10);
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
            this.btnOK.Location = new System.Drawing.Point(281, 609);
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
            this.btnCancel.Location = new System.Drawing.Point(376, 609);
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
            this.textBox1.Location = new System.Drawing.Point(115, 145);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(312, 110);
            this.textBox1.TabIndex = 12;
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(114, 70);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(328, 72);
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
            this.cboFixLevel.Location = new System.Drawing.Point(194, 42);
            this.cboFixLevel.Name = "cboFixLevel";
            this.cboFixLevel.Size = new System.Drawing.Size(235, 21);
            this.cboFixLevel.TabIndex = 14;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(114, 46);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(66, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Fixing Level:";
            // 
            // chkDebugLogs
            // 
            this.chkDebugLogs.AutoSize = true;
            this.chkDebugLogs.Location = new System.Drawing.Point(121, 510);
            this.chkDebugLogs.Name = "chkDebugLogs";
            this.chkDebugLogs.Size = new System.Drawing.Size(131, 17);
            this.chkDebugLogs.TabIndex = 18;
            this.chkDebugLogs.Text = "Enable Debug logging";
            this.chkDebugLogs.UseVisualStyleBackColor = true;
            // 
            // chkCacheSaveTimer
            // 
            this.chkCacheSaveTimer.AutoSize = true;
            this.chkCacheSaveTimer.Location = new System.Drawing.Point(121, 295);
            this.chkCacheSaveTimer.Name = "chkCacheSaveTimer";
            this.chkCacheSaveTimer.Size = new System.Drawing.Size(154, 17);
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
            this.upTime.Location = new System.Drawing.Point(281, 293);
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
            this.label5.Location = new System.Drawing.Point(337, 298);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(44, 13);
            this.label5.TabIndex = 21;
            this.label5.Text = "Minutes";
            // 
            // chkDoubleCheckDelete
            // 
            this.chkDoubleCheckDelete.AutoSize = true;
            this.chkDoubleCheckDelete.Location = new System.Drawing.Point(121, 270);
            this.chkDoubleCheckDelete.Name = "chkDoubleCheckDelete";
            this.chkDoubleCheckDelete.Size = new System.Drawing.Size(256, 17);
            this.chkDoubleCheckDelete.TabIndex = 22;
            this.chkDoubleCheckDelete.Text = "Double check file exists elsewhere before deleting";
            this.chkDoubleCheckDelete.UseVisualStyleBackColor = true;
            // 
            // chkDetailedReporting
            // 
            this.chkDetailedReporting.AutoSize = true;
            this.chkDetailedReporting.Location = new System.Drawing.Point(121, 485);
            this.chkDetailedReporting.Name = "chkDetailedReporting";
            this.chkDetailedReporting.Size = new System.Drawing.Size(240, 17);
            this.chkDetailedReporting.TabIndex = 25;
            this.chkDetailedReporting.Text = "Show detailed actions in Fixing Status window";
            this.chkDetailedReporting.UseVisualStyleBackColor = true;
            // 
            // chkSendFoundMIA
            // 
            this.chkSendFoundMIA.AutoSize = true;
            this.chkSendFoundMIA.Location = new System.Drawing.Point(121, 405);
            this.chkSendFoundMIA.Name = "chkSendFoundMIA";
            this.chkSendFoundMIA.Size = new System.Drawing.Size(166, 17);
            this.chkSendFoundMIA.TabIndex = 27;
            this.chkSendFoundMIA.Text = "Send Found MIA notifications";
            this.chkSendFoundMIA.UseVisualStyleBackColor = true;
            this.chkSendFoundMIA.CheckedChanged += new System.EventHandler(this.chkSendFoundMIA_CheckedChanged);
            // 
            // chkSendFoundMIAAnon
            // 
            this.chkSendFoundMIAAnon.AutoSize = true;
            this.chkSendFoundMIAAnon.Location = new System.Drawing.Point(137, 430);
            this.chkSendFoundMIAAnon.Name = "chkSendFoundMIAAnon";
            this.chkSendFoundMIAAnon.Size = new System.Drawing.Size(116, 17);
            this.chkSendFoundMIAAnon.TabIndex = 28;
            this.chkSendFoundMIAAnon.Text = "Send anonymously";
            this.chkSendFoundMIAAnon.UseVisualStyleBackColor = true;
            // 
            // chkDeleteOldCueFiles
            // 
            this.chkDeleteOldCueFiles.AutoSize = true;
            this.chkDeleteOldCueFiles.Location = new System.Drawing.Point(121, 455);
            this.chkDeleteOldCueFiles.Name = "chkDeleteOldCueFiles";
            this.chkDeleteOldCueFiles.Size = new System.Drawing.Size(206, 17);
            this.chkDeleteOldCueFiles.TabIndex = 30;
            this.chkDeleteOldCueFiles.Text = "Delete previous Cue file zips in ToSort ";
            this.chkDeleteOldCueFiles.UseVisualStyleBackColor = true;
            // 
            // lblSide1
            // 
            this.lblSide1.Location = new System.Drawing.Point(9, 15);
            this.lblSide1.Name = "lblSide1";
            this.lblSide1.Size = new System.Drawing.Size(94, 13);
            this.lblSide1.TabIndex = 31;
            this.lblSide1.Text = "Core Settings :";
            this.lblSide1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblSide3
            // 
            this.lblSide3.Location = new System.Drawing.Point(9, 406);
            this.lblSide3.Name = "lblSide3";
            this.lblSide3.Size = new System.Drawing.Size(94, 13);
            this.lblSide3.TabIndex = 33;
            this.lblSide3.Text = "DATVault :";
            this.lblSide3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblSide4
            // 
            this.lblSide4.Location = new System.Drawing.Point(9, 486);
            this.lblSide4.Name = "lblSide4";
            this.lblSide4.Size = new System.Drawing.Size(94, 13);
            this.lblSide4.TabIndex = 34;
            this.lblSide4.Text = "Logging :";
            this.lblSide4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(120, 350);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(102, 13);
            this.label2.TabIndex = 37;
            this.label2.Text = "Max ZSTD workers:";
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(9, 350);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(94, 13);
            this.label6.TabIndex = 38;
            this.label6.Text = "Compression :";
            this.label6.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(120, 375);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(83, 13);
            this.label7.TabIndex = 39;
            this.label7.Text = "Default 7Z type:";
            // 
            // cbo7zStruct
            // 
            this.cbo7zStruct.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbo7zStruct.FormattingEnabled = true;
            this.cbo7zStruct.Location = new System.Drawing.Point(230, 372);
            this.cbo7zStruct.Name = "cbo7zStruct";
            this.cbo7zStruct.Size = new System.Drawing.Size(121, 21);
            this.cbo7zStruct.TabIndex = 40;
            // 
            // cboCores
            // 
            this.cboCores.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCores.FormattingEnabled = true;
            this.cboCores.Location = new System.Drawing.Point(230, 347);
            this.cboCores.Name = "cboCores";
            this.cboCores.Size = new System.Drawing.Size(58, 21);
            this.cboCores.TabIndex = 41;
            // 
            // chkDarkMode
            // 
            this.chkDarkMode.AutoSize = true;
            this.chkDarkMode.Location = new System.Drawing.Point(121, 320);
            this.chkDarkMode.Name = "chkDarkMode";
            this.chkDarkMode.Size = new System.Drawing.Size(164, 17);
            this.chkDarkMode.TabIndex = 42;
            this.chkDarkMode.Text = "Dark Mode (Restart required.)";
            this.chkDarkMode.UseVisualStyleBackColor = true;
            // 
            // chkDoNotReportFeedback
            // 
            this.chkDoNotReportFeedback.AutoSize = true;
            this.chkDoNotReportFeedback.Location = new System.Drawing.Point(121, 535);
            this.chkDoNotReportFeedback.Name = "chkDoNotReportFeedback";
            this.chkDoNotReportFeedback.Size = new System.Drawing.Size(135, 17);
            this.chkDoNotReportFeedback.TabIndex = 43;
            this.chkDoNotReportFeedback.Text = "Do not report feedback";
            this.chkDoNotReportFeedback.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabGeneral);
            this.tabControl1.Controls.Add(this.tabCHD);
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(460, 600);
            this.tabControl1.TabIndex = 44;
            // 
            // tabGeneral
            // 
            this.tabGeneral.AutoScroll = true;
            this.tabGeneral.Controls.Add(this.lblSideBehavior);
            this.tabGeneral.Controls.Add(this.lblSide1);
            this.tabGeneral.Controls.Add(this.chkDoNotReportFeedback);
            this.tabGeneral.Controls.Add(this.label4);
            this.tabGeneral.Controls.Add(this.chkDarkMode);
            this.tabGeneral.Controls.Add(this.textBox1);
            this.tabGeneral.Controls.Add(this.cboCores);
            this.tabGeneral.Controls.Add(this.label1);
            this.tabGeneral.Controls.Add(this.cbo7zStruct);
            this.tabGeneral.Controls.Add(this.lblDATRoot);
            this.tabGeneral.Controls.Add(this.label7);
            this.tabGeneral.Controls.Add(this.btnDAT);
            this.tabGeneral.Controls.Add(this.label6);
            this.tabGeneral.Controls.Add(this.cboFixLevel);
            this.tabGeneral.Controls.Add(this.label2);
            this.tabGeneral.Controls.Add(this.label3);
            this.tabGeneral.Controls.Add(this.lblSide4);
            this.tabGeneral.Controls.Add(this.chkDebugLogs);
            this.tabGeneral.Controls.Add(this.lblSide3);
            this.tabGeneral.Controls.Add(this.chkCacheSaveTimer);
            this.tabGeneral.Controls.Add(this.upTime);
            this.tabGeneral.Controls.Add(this.chkDeleteOldCueFiles);
            this.tabGeneral.Controls.Add(this.label5);
            this.tabGeneral.Controls.Add(this.chkSendFoundMIAAnon);
            this.tabGeneral.Controls.Add(this.chkDoubleCheckDelete);
            this.tabGeneral.Controls.Add(this.chkSendFoundMIA);
            this.tabGeneral.Controls.Add(this.chkDetailedReporting);
            this.tabGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(452, 574);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // lblSideBehavior
            // 
            this.lblSideBehavior.Location = new System.Drawing.Point(9, 270);
            this.lblSideBehavior.Name = "lblSideBehavior";
            this.lblSideBehavior.Size = new System.Drawing.Size(94, 13);
            this.lblSideBehavior.TabIndex = 44;
            this.lblSideBehavior.Text = "Behavior :";
            this.lblSideBehavior.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // tabCHD
            // 
            this.tabCHD.Location = new System.Drawing.Point(4, 22);
            this.tabCHD.Name = "tabCHD";
            this.tabCHD.Padding = new System.Windows.Forms.Padding(3);
            this.tabCHD.Size = new System.Drawing.Size(445, 565);
            this.tabCHD.TabIndex = 1;
            this.tabCHD.Text = "CHD";
            this.tabCHD.UseVisualStyleBackColor = true;
            // 
            // FrmSettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(477, 641);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "RomVault Settings";
            this.Load += new System.EventHandler(this.FrmConfigLoad);
            ((System.ComponentModel.ISupportInitialize)(this.upTime)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabGeneral.PerformLayout();
            this.ResumeLayout(false);

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
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.TabPage tabCHD;
        private System.Windows.Forms.Label lblSideBehavior;
    }
}
