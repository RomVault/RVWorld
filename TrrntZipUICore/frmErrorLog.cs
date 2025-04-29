using System;
using System.Windows.Forms;

namespace TrrntZipUI
{
    public partial class frmErrorLog : Form
    {
        public bool closing = false;
        public frmErrorLog()
        {
            InitializeComponent();
        }

        private void frmErrorLog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (closing)
                return;

            this.Hide();
            e.Cancel = true; // this cancels the close event.
        }


        public void AddError(string message)
        {
            Show();
            txtLog.Text = txtLog.Text + $"----{DateTime.Now}----\n{message}\n\n".Replace($"\n", $"\r\n");

            if (txtLog.Visible)
            {
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
        }
    }
}
