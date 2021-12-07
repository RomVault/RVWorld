using RomVaultCore.RvDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ROMVault
{
    public partial class FrmRomInfo : Form
    {
        public FrmRomInfo()
        {
            InitializeComponent();
        }

        public bool SetRom(RvFile tFile)
        {
            if (tFile.FileGroup == null)
                return false;

            StringBuilder sb = new StringBuilder();

            foreach(var v in tFile.FileGroup.Files)
            {
                sb.AppendLine(v.GotStatus+" | "+   v.FullName);
            }
            textBox1.Text = sb.ToString();
            return true;
        }
    }
}
