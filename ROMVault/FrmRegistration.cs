using System;
using System.Windows.Forms;
using RomVaultCore;

namespace ROMVault
{
    public partial class FrmRegistration : Form
    {
        public FrmRegistration()
        {
            InitializeComponent();
            txtName.Text = UISettings.Username;
            txtEmail.Text = UISettings.EMail;
            chkBoxOptOut.Checked = UISettings.OptOut;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            UISettings.Username = txtName.Text;
            UISettings.EMail = txtEmail.Text;
            UISettings.OptOut = chkBoxOptOut.Checked;
            
            ReportError.Username = UISettings.Username;
            ReportError.EMail = UISettings.EMail;
            ReportError.OptOut = UISettings.OptOut;
            Close();
        }
    }
}
