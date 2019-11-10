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
            this.label1 = new System.Windows.Forms.Label();
            this.lblDATRoot = new System.Windows.Forms.Label();
            this.btnDAT = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cboFixLevel = new System.Windows.Forms.ComboBox();
            this.cboScanLevel = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.chkDebugLogs = new System.Windows.Forms.CheckBox();
            this.chkCacheSaveTimer = new System.Windows.Forms.CheckBox();
            this.upTime = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.chkDoubleCheckDelete = new System.Windows.Forms.CheckBox();
            this.chkRV7z = new System.Windows.Forms.CheckBox();
            this.chk7zDeCompress = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.upTime)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(103, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "DAT Root Directory:";
            // 
            // lblDATRoot
            // 
            this.lblDATRoot.BackColor = System.Drawing.Color.White;
            this.lblDATRoot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDATRoot.Location = new System.Drawing.Point(123, 14);
            this.lblDATRoot.Name = "lblDATRoot";
            this.lblDATRoot.Size = new System.Drawing.Size(357, 22);
            this.lblDATRoot.TabIndex = 3;
            this.lblDATRoot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnDAT
            // 
            this.btnDAT.Location = new System.Drawing.Point(486, 12);
            this.btnDAT.Name = "btnDAT";
            this.btnDAT.Size = new System.Drawing.Size(47, 24);
            this.btnDAT.TabIndex = 6;
            this.btnDAT.Text = "Set";
            this.btnDAT.UseVisualStyleBackColor = true;
            this.btnDAT.Click += new System.EventHandler(this.BtnDatClick);
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(167, 314);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(89, 23);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOkClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(280, 314);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(89, 23);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelClick);
            // 
            // textBox1
            // 
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Location = new System.Drawing.Point(123, 111);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(357, 136);
            this.textBox1.TabIndex = 12;
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(8, 113);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(106, 72);
            this.label4.TabIndex = 13;
            this.label4.Text = "Filenames not to remove from RomDir\'s :                  (Put each file on its ow" +
    "n line)";
            // 
            // cboFixLevel
            // 
            this.cboFixLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFixLevel.FormattingEnabled = true;
            this.cboFixLevel.Location = new System.Drawing.Point(123, 75);
            this.cboFixLevel.Name = "cboFixLevel";
            this.cboFixLevel.Size = new System.Drawing.Size(357, 21);
            this.cboFixLevel.TabIndex = 14;
            // 
            // cboScanLevel
            // 
            this.cboScanLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboScanLevel.FormattingEnabled = true;
            this.cboScanLevel.Location = new System.Drawing.Point(123, 48);
            this.cboScanLevel.Name = "cboScanLevel";
            this.cboScanLevel.Size = new System.Drawing.Size(357, 21);
            this.cboScanLevel.TabIndex = 15;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 51);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(84, 13);
            this.label2.TabIndex = 16;
            this.label2.Text = "Scanning Level:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 79);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(66, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Fixing Level:";
            // 
            // chkDebugLogs
            // 
            this.chkDebugLogs.AutoSize = true;
            this.chkDebugLogs.Location = new System.Drawing.Point(279, 261);
            this.chkDebugLogs.Name = "chkDebugLogs";
            this.chkDebugLogs.Size = new System.Drawing.Size(126, 17);
            this.chkDebugLogs.TabIndex = 18;
            this.chkDebugLogs.Text = "Debug Logs Enabled";
            this.chkDebugLogs.UseVisualStyleBackColor = true;
            // 
            // chkCacheSaveTimer
            // 
            this.chkCacheSaveTimer.AutoSize = true;
            this.chkCacheSaveTimer.Location = new System.Drawing.Point(125, 285);
            this.chkCacheSaveTimer.Name = "chkCacheSaveTimer";
            this.chkCacheSaveTimer.Size = new System.Drawing.Size(156, 17);
            this.chkCacheSaveTimer.TabIndex = 19;
            this.chkCacheSaveTimer.Text = "Save Cache On Timer Ever";
            this.chkCacheSaveTimer.UseVisualStyleBackColor = true;
            // 
            // upTime
            // 
            this.upTime.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.upTime.Location = new System.Drawing.Point(279, 283);
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
            this.label5.Location = new System.Drawing.Point(329, 286);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(44, 13);
            this.label5.TabIndex = 21;
            this.label5.Text = "Minutes";
            // 
            // chkDoubleCheckDelete
            // 
            this.chkDoubleCheckDelete.AutoSize = true;
            this.chkDoubleCheckDelete.Location = new System.Drawing.Point(125, 261);
            this.chkDoubleCheckDelete.Name = "chkDoubleCheckDelete";
            this.chkDoubleCheckDelete.Size = new System.Drawing.Size(128, 17);
            this.chkDoubleCheckDelete.TabIndex = 22;
            this.chkDoubleCheckDelete.Text = "Double Check Delete";
            this.chkDoubleCheckDelete.UseVisualStyleBackColor = true;
            // 
            // chkRV7z
            // 
            this.chkRV7z.AutoSize = true;
            this.chkRV7z.Location = new System.Drawing.Point(411, 261);
            this.chkRV7z.Name = "chkRV7z";
            this.chkRV7z.Size = new System.Drawing.Size(131, 17);
            this.chkRV7z.TabIndex = 23;
            this.chkRV7z.Text = "Convert all 7z to RV7z";
            this.chkRV7z.UseVisualStyleBackColor = true;
            // 
            // chk7zDeCompress
            // 
            this.chk7zDeCompress.AutoSize = true;
            this.chk7zDeCompress.Location = new System.Drawing.Point(411, 286);
            this.chk7zDeCompress.Name = "chk7zDeCompress";
            this.chk7zDeCompress.Size = new System.Drawing.Size(133, 17);
            this.chk7zDeCompress.TabIndex = 24;
            this.chk7zDeCompress.Text = "Cache 7z Decompress";
            this.chk7zDeCompress.UseVisualStyleBackColor = true;
            // 
            // FrmSettings
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(554, 348);
            this.Controls.Add(this.chk7zDeCompress);
            this.Controls.Add(this.chkRV7z);
            this.Controls.Add(this.chkDoubleCheckDelete);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.upTime);
            this.Controls.Add(this.chkCacheSaveTimer);
            this.Controls.Add(this.chkDebugLogs);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cboScanLevel);
            this.Controls.Add(this.cboFixLevel);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnDAT);
            this.Controls.Add(this.lblDATRoot);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FrmSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "RomVault Settings";
            this.Load += new System.EventHandler(this.FrmConfigLoad);
            ((System.ComponentModel.ISupportInitialize)(this.upTime)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblDATRoot;
        private System.Windows.Forms.Button btnDAT;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cboFixLevel;
        private System.Windows.Forms.ComboBox cboScanLevel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox chkDebugLogs;
        private System.Windows.Forms.CheckBox chkCacheSaveTimer;
        private System.Windows.Forms.NumericUpDown upTime;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox chkDoubleCheckDelete;
        private System.Windows.Forms.CheckBox chkRV7z;
        private System.Windows.Forms.CheckBox chk7zDeCompress;
    }
}