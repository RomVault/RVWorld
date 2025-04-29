/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using RomVaultCore;
using System;
using System.Windows.Forms;

namespace ROMVault
{
    public partial class FrmShowError : Form
    {
        public FrmShowError()
        {
            InitializeComponent();
            if (Settings.rvSettings.DoNotReportFeedback)
                label1.Text = "You have opted out of sending this Crash Report";
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