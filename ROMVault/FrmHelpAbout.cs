/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ROMVault
{
    public partial class FrmHelpAbout : Form
    {
        public FrmHelpAbout()
        {
            InitializeComponent();
            Text = "Version " + Program.strVersion + " : " + Application.StartupPath;
            lblVersion.Text = "Version " + Program.strVersion;
        }

        private void label1_Click(object sender, EventArgs e)
        {
            try { Process.Start("http://www.romvault.com/"); } catch { }
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            try { Process.Start("http://paypal.me/romvault"); } catch { }
        }

    }
}