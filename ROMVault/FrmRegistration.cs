using System;
using System.Windows.Forms;
using RVCore;

namespace ROMVault
{
    public partial class FrmRegistration : Form
    {
        public FrmRegistration()
        {
            InitializeComponent();
            txtName.Text = Settings.Username;
            txtEmail.Text = Settings.EMail;
            chkBoxOptOut.Checked = Settings.OptOut;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Settings.Username = txtName.Text;
            Settings.EMail = txtEmail.Text;
            Settings.OptOut = chkBoxOptOut.Checked;
            Close();
        }
    }
}
