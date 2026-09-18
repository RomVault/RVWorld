using RomVaultCore;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ROMVault.UIElements
{
    public partial class UIFilterOptions : UserControl
    {
        public sealed class FilterTextChangedEventArgs : EventArgs
        {
            public FilterTextChangedEventArgs(string filterText)
            {
                FilterText = filterText;
            }

            public string FilterText { get; }
        }

        [Category("Action")]
        [Description("Occurs when any filter checkbox checked state changes.")]
        public event EventHandler CheckedChanged;

        [Category("Action")]
        [Description("Occurs when the filter text changes.")]
        public event EventHandler<FilterTextChangedEventArgs> FilterTextChanged;


        public UIFilterOptions()
        {
            InitializeComponent();

            if (Settings.rvSettings == null)
                return;

            chkBoxShowComplete.Checked = Settings.rvSettings.chkBoxShowComplete;
            chkBoxShowPartial.Checked = Settings.rvSettings.chkBoxShowPartial;
            chkBoxShowEmpty.Checked = Settings.rvSettings.chkBoxShowEmpty;
            chkBoxShowFixes.Checked = Settings.rvSettings.chkBoxShowFixes;
            chkBoxShowMIA.Checked = Settings.rvSettings.chkBoxShowMIA;
            chkBoxShowMerged.Checked = Settings.rvSettings.chkBoxShowMerged;
        }

        private void chkBoxShowComplete_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowComplete != this.chkBoxShowComplete.Checked)
            {
                Settings.rvSettings.chkBoxShowComplete = this.chkBoxShowComplete.Checked;
                Settings.WriteConfig();
                CheckedChanged?.Invoke(this, e);
            }
        }

        private void chkBoxShowPartial_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowPartial != this.chkBoxShowPartial.Checked)
            {
                Settings.rvSettings.chkBoxShowPartial = this.chkBoxShowPartial.Checked;
                Settings.WriteConfig();
                CheckedChanged?.Invoke(this, e);
            }
        }
        private void chkBoxShowEmpty_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowEmpty != this.chkBoxShowEmpty.Checked)
            {
                Settings.rvSettings.chkBoxShowEmpty = this.chkBoxShowEmpty.Checked;
                Settings.WriteConfig();
                CheckedChanged?.Invoke(this, e);
            }
        }

        private void chkBoxShowFixes_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowFixes != this.chkBoxShowFixes.Checked)
            {
                Settings.rvSettings.chkBoxShowFixes = this.chkBoxShowFixes.Checked;
                Settings.WriteConfig();
                CheckedChanged?.Invoke(this, e);
            }
        }


        private void chkBoxShowMIA_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMIA != this.chkBoxShowMIA.Checked)
            {
                Settings.rvSettings.chkBoxShowMIA = this.chkBoxShowMIA.Checked;
                Settings.WriteConfig();
                CheckedChanged?.Invoke(this, e);
            }
        }

        private void chkBoxShowMerged_CheckedChanged(object sender, EventArgs e)
        {
            if (Settings.rvSettings.chkBoxShowMerged != this.chkBoxShowMerged.Checked)
            {
                Settings.rvSettings.chkBoxShowMerged = this.chkBoxShowMerged.Checked;
                Settings.WriteConfig();
                CheckedChanged?.Invoke(this, e);
            }
        }



        private void btnClear_Click(object sender, EventArgs e)
        {
            txtFilter.Text = "";
        }


        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            FilterTextChanged?.Invoke(this, new FilterTextChangedEventArgs(txtFilter.Text));
            txtFilter.Focus();
        }
    }
}
