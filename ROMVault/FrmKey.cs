/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RomVaultCore;

namespace ROMVault
{
    public partial class FrmKey : Form
    {
        public FrmKey()
        {
            InitializeComponent();
        }

        private void AddLabel(Point location, Size size, string name, string text)
        {
            Label label = new Label
            {
                Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point),
                Location = location,
                TextAlign = ContentAlignment.MiddleLeft,
                Name = name,
                Size = size,
                TabIndex = 0,
                Text = text
            };
            Controls.Add(label);
        }

        private void FrmKey_Load(object sender, EventArgs e)
        {
            List<RepStatus> displayList = new List<RepStatus>
            {
                RepStatus.Correct,
                RepStatus.Missing,
                RepStatus.Unknown,
                RepStatus.UnNeeded,
                RepStatus.NotCollected,
                RepStatus.InToSort,
                RepStatus.Ignore,

                RepStatus.CanBeFixed,
                RepStatus.NeededForFix,
                RepStatus.MoveToSort,
                RepStatus.Delete,


                RepStatus.Corrupt,
                RepStatus.UnScanned,
            };
            Height = displayList.Count * 46 + 110;
            AddLabel(new Point(6,6),new Size(538,20),"LabelBasic","Basic Statuses");
            int eOffset = 28;

            for (int i = 0; i < displayList.Count; i++)
            {
                if (i == 7)
                {
                    AddLabel(new Point(6, i * 46 + eOffset), new Size(538, 20), "LabelFix", "Fix Statuses");
                    eOffset += 20;
                }

                if (i == 11)
                {
                    AddLabel(new Point(6, i * 46 + eOffset), new Size(538, 20), "LabelProblem", "Problem Statuses");
                    eOffset += 20;
                }
                PictureBox pictureBox = new PictureBox
                {
                    BorderStyle = BorderStyle.FixedSingle,
                    Location = new Point(6, i * 46 + eOffset),
                    Name = "pictureBox" + i,
                    Size = new Size(48, 42),
                    TabIndex = 0,
                    TabStop = false
                };

                Controls.Add(pictureBox);

                Bitmap bm = rvImages.GetBitmap("G_" + displayList[i]);
                pictureBox.Image = bm;

                Label label = new Label
                {
                    BackColor = SystemColors.Control,
                    BorderStyle = BorderStyle.FixedSingle,
                    Location = new Point(56, i * 46 + eOffset),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Name = "label" + i,
                    Size = new Size(538, 42),
                    TabIndex = 0
                };

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