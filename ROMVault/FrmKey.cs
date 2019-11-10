/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RVCore;

namespace ROMVault
{
    public partial class FrmKey : Form
    {
        public FrmKey()
        {
            InitializeComponent();
        }

        private void FrmKey_Load(object sender, EventArgs e)
        {
            List<RepStatus> displayList = new List<RepStatus>
            {
                RepStatus.Missing,
                RepStatus.Correct,
                RepStatus.NotCollected,
                RepStatus.UnNeeded,
                RepStatus.Unknown,
                RepStatus.InToSort,
                RepStatus.Corrupt,
                RepStatus.UnScanned,
                RepStatus.Ignore,
                RepStatus.CanBeFixed,
                RepStatus.MoveToSort,
                RepStatus.Delete,
                RepStatus.NeededForFix
            };
            Height = displayList.Count*44 + 32;
            for (int i = 0; i < displayList.Count; i++)
            {
                PictureBox pictureBox = new PictureBox();
                pictureBox.BorderStyle = BorderStyle.FixedSingle;
                pictureBox.Location = new Point(4, 4 + i*44);
                pictureBox.Name = "pictureBox" + i;
                pictureBox.Size = new Size(48, 42);
                pictureBox.TabIndex = 0;
                pictureBox.TabStop = false;

                Controls.Add(pictureBox);

                Bitmap bm = rvImages.GetBitmap("G_" + displayList[i]);
                pictureBox.Image = bm;

                Label label = new Label();
                label.BackColor = SystemColors.Control;
                label.BorderStyle = BorderStyle.FixedSingle;
                label.Location = new Point(54, 4 + i*44);
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Name = "label" + i;
                label.Size = new Size(542, 42);
                label.TabIndex = 0;

                string text;
                switch (displayList[i])
                {
                    case RepStatus.Missing:
                        text = "Red - This ROM is missing.";
                        break;
                    case RepStatus.Correct:
                        text = "Green - This ROM is Correct.";
                        break;
                    case RepStatus.NotCollected:
                        text = "Gray - This ROM is not collected. Either it is in the parent set, or it is a 'BadDump ROM'";
                        break;
                    case RepStatus.UnNeeded:
                        text = "Light Cyan - This ROM is unneeded here, as this ROM is collected in the parent set.";
                        break;
                    case RepStatus.Unknown:
                        text = "Cyan - This ROM is not needed here. (Find Fixes to see what should be done with this ROM)";
                        break;
                    case RepStatus.InToSort:
                        text = "Magenta - This ROM is in the ToSort directory, after Finding Fixes this ROM is not needed in any sets.";
                        break;
                    case RepStatus.Corrupt:
                        text = "Red - This ROM is Corrupt in the Zip File.";
                        break;
                    case RepStatus.UnScanned:
                        text = "Blue - This file could not be scanned as it is locked by another process.";
                        break;
                    case RepStatus.Ignore:
                        text = "GreyBlue - This file is found in the Ignore file list.";
                        break;
                    case RepStatus.CanBeFixed:
                        text = "Yellow - This ROM is missing here but has been found somewhere else, and so can be fixed.";
                        break;
                    case RepStatus.MoveToSort:
                        text = "Purple - This ROM is not found in any DAT set, and so will be moved out to ToSort.";
                        break;
                    case RepStatus.Delete:
                        text = "Brown - This ROM should be deleted, as a copy of it is correctly located somewhere else.";
                        break;
                    case RepStatus.NeededForFix:
                        text = "Orange - This Rom in not needed here, but is required in another set somewhere else.";
                        break;

                    default:
                        text = "";
                        break;
                }

                label.Text = text;
                Controls.Add(label);
            }
        }
    }
}