/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2022                                 *
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
            Text = "Version " + Program.StrVersion + " : " + Application.StartupPath;
            lblVersion.Text = "Version " + Program.StrVersion;
        }

        private void label1_Click(object sender, EventArgs e)
        {
            string url = "http://www.romvault.com/";
            Process.Start(url);
        }
        
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            Process.Start("http://paypal.me/romvault");
        }

    }
}