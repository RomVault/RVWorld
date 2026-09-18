namespace ROMVault.UIElements
{
    partial class UIFilterOptions
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            chkBoxShowEmpty = new System.Windows.Forms.CheckBox();
            chkBoxShowMIA = new System.Windows.Forms.CheckBox();
            btnClear = new System.Windows.Forms.Button();
            txtFilter = new System.Windows.Forms.TextBox();
            chkBoxShowMerged = new System.Windows.Forms.CheckBox();
            chkBoxShowFixes = new System.Windows.Forms.CheckBox();
            chkBoxShowPartial = new System.Windows.Forms.CheckBox();
            chkBoxShowComplete = new System.Windows.Forms.CheckBox();
            SuspendLayout();
            // 
            // chkBoxShowEmpty
            // 
            chkBoxShowEmpty.AutoSize = true;
            chkBoxShowEmpty.Checked = true;
            chkBoxShowEmpty.CheckState = System.Windows.Forms.CheckState.Checked;
            chkBoxShowEmpty.Location = new System.Drawing.Point(0, 34);
            chkBoxShowEmpty.Name = "chkBoxShowEmpty";
            chkBoxShowEmpty.Size = new System.Drawing.Size(92, 19);
            chkBoxShowEmpty.TabIndex = 28;
            chkBoxShowEmpty.Text = "Show Empty";
            chkBoxShowEmpty.UseVisualStyleBackColor = true;
            chkBoxShowEmpty.CheckedChanged += chkBoxShowEmpty_CheckedChanged;
            // 
            // chkBoxShowMIA
            // 
            chkBoxShowMIA.AutoSize = true;
            chkBoxShowMIA.Checked = true;
            chkBoxShowMIA.CheckState = System.Windows.Forms.CheckState.Checked;
            chkBoxShowMIA.Location = new System.Drawing.Point(0, 66);
            chkBoxShowMIA.Name = "chkBoxShowMIA";
            chkBoxShowMIA.Size = new System.Drawing.Size(80, 19);
            chkBoxShowMIA.TabIndex = 27;
            chkBoxShowMIA.Text = "Show MIA";
            chkBoxShowMIA.UseVisualStyleBackColor = true;
            chkBoxShowMIA.CheckedChanged += chkBoxShowMIA_CheckedChanged;
            // 
            // btnClear
            // 
            btnClear.Location = new System.Drawing.Point(117, 101);
            btnClear.Name = "btnClear";
            btnClear.Size = new System.Drawing.Size(28, 22);
            btnClear.TabIndex = 26;
            btnClear.Text = "X";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            // 
            // txtFilter
            // 
            txtFilter.Location = new System.Drawing.Point(2, 102);
            txtFilter.Name = "txtFilter";
            txtFilter.Size = new System.Drawing.Size(109, 23);
            txtFilter.TabIndex = 25;
            txtFilter.TextChanged += txtFilter_TextChanged;
            // 
            // chkBoxShowMerged
            // 
            chkBoxShowMerged.AutoSize = true;
            chkBoxShowMerged.Location = new System.Drawing.Point(0, 82);
            chkBoxShowMerged.Name = "chkBoxShowMerged";
            chkBoxShowMerged.Size = new System.Drawing.Size(158, 19);
            chkBoxShowMerged.TabIndex = 24;
            chkBoxShowMerged.Text = "Show Merged / Deduped";
            chkBoxShowMerged.UseVisualStyleBackColor = true;
            chkBoxShowMerged.CheckedChanged += chkBoxShowMerged_CheckedChanged;
            // 
            // chkBoxShowFixes
            // 
            chkBoxShowFixes.AutoSize = true;
            chkBoxShowFixes.Checked = true;
            chkBoxShowFixes.CheckState = System.Windows.Forms.CheckState.Checked;
            chkBoxShowFixes.Location = new System.Drawing.Point(0, 50);
            chkBoxShowFixes.Name = "chkBoxShowFixes";
            chkBoxShowFixes.Size = new System.Drawing.Size(83, 19);
            chkBoxShowFixes.TabIndex = 23;
            chkBoxShowFixes.Text = "Show Fixes";
            chkBoxShowFixes.UseVisualStyleBackColor = true;
            chkBoxShowFixes.CheckedChanged += chkBoxShowFixes_CheckedChanged;
            // 
            // chkBoxShowPartial
            // 
            chkBoxShowPartial.AutoSize = true;
            chkBoxShowPartial.Checked = true;
            chkBoxShowPartial.CheckState = System.Windows.Forms.CheckState.Checked;
            chkBoxShowPartial.Location = new System.Drawing.Point(0, 18);
            chkBoxShowPartial.Name = "chkBoxShowPartial";
            chkBoxShowPartial.Size = new System.Drawing.Size(91, 19);
            chkBoxShowPartial.TabIndex = 22;
            chkBoxShowPartial.Text = "Show Partial";
            chkBoxShowPartial.UseVisualStyleBackColor = true;
            chkBoxShowPartial.CheckedChanged += chkBoxShowPartial_CheckedChanged;
            // 
            // chkBoxShowComplete
            // 
            chkBoxShowComplete.AutoSize = true;
            chkBoxShowComplete.Checked = true;
            chkBoxShowComplete.CheckState = System.Windows.Forms.CheckState.Checked;
            chkBoxShowComplete.Location = new System.Drawing.Point(0, 2);
            chkBoxShowComplete.Name = "chkBoxShowComplete";
            chkBoxShowComplete.Size = new System.Drawing.Size(110, 19);
            chkBoxShowComplete.TabIndex = 21;
            chkBoxShowComplete.Text = "Show Complete";
            chkBoxShowComplete.UseVisualStyleBackColor = true;
            chkBoxShowComplete.CheckedChanged += chkBoxShowComplete_CheckedChanged;
            // 
            // UIFilterOptions
            // 
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            Controls.Add(chkBoxShowEmpty);
            Controls.Add(chkBoxShowMIA);
            Controls.Add(btnClear);
            Controls.Add(txtFilter);
            Controls.Add(chkBoxShowMerged);
            Controls.Add(chkBoxShowFixes);
            Controls.Add(chkBoxShowPartial);
            Controls.Add(chkBoxShowComplete);
            Name = "UIFilterOptions";
            Size = new System.Drawing.Size(173, 132);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.CheckBox chkBoxShowEmpty;
        private System.Windows.Forms.CheckBox chkBoxShowMIA;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.CheckBox chkBoxShowMerged;
        private System.Windows.Forms.CheckBox chkBoxShowFixes;
        private System.Windows.Forms.CheckBox chkBoxShowPartial;
        private System.Windows.Forms.CheckBox chkBoxShowComplete;
    }
}
