/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System;
using System.Windows.Forms;

namespace RomVaultX
{
    public partial class frmShowError : Form
    {
        public frmShowError()
        {
            InitializeComponent();
        }

        public void settype(string s)
        {
            textBox1.Text = s;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}