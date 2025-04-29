/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
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
                RepStatus.CorrectMIA,
                RepStatus.Missing,
                RepStatus.MissingMIA,
                RepStatus.Unknown,
                RepStatus.UnNeeded,
                RepStatus.NotCollected,
                RepStatus.InToSort,
                RepStatus.Ignore,

                RepStatus.CanBeFixed,
                RepStatus.CanBeFixedMIA,
                RepStatus.NeededForFix,
                RepStatus.Rename,
                RepStatus.MoveToSort,
                RepStatus.Incomplete,
                RepStatus.Delete,


                RepStatus.Corrupt,
                RepStatus.UnScanned,
            };
            Height = displayList.Count * 46 + 110;
            AddLabel(new Point(6,6),new Size(538,20),"LabelBasic","Basic Statuses");
            int eOffset = 28;

            for (int i = 0; i < displayList.Count; i++)
            {
                if (i == 9)
                {
                    AddLabel(new Point(6, i * 46 + eOffset), new Size(538, 20), "LabelFix", "Fix Statuses");
                    eOffset += 20;
                }

                if (i == 16)
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
                    TabStop = false,
                    BackColor = Color.White
                };

                Controls.Add(pictureBox);

                pictureBox.Image = rvImages.GetBitmap("G_" + displayList[i]);

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
                    case RepStatus.MissingMIA:
                        text = "Salmon - This ROM is known to be private or missing in action (MIA).";
                        break;
                    case RepStatus.Correct:
                        text = "Green - This ROM is Correct.";
                        break;
                    case RepStatus.CorrectMIA:
                        text = "SuperGreen - The ROM was known to be MIA (Missing In Action), but you found it. (Good Job!)";
                        break;
                    case RepStatus.NotCollected:
                        text = "Gray - The ROM is not collected here because it belongs in the parent or primary deduped set.";
                        break;
                    case RepStatus.UnNeeded:
                        text = "Light Cyan - The ROM is not needed here because it belongs in the parent or primary deduped set.";
                        break;
                    case RepStatus.Unknown:
                        text = "Cyan - The ROM is not needed here. Use 'Find Fixes' to see what should be done with the ROM.";
                        break;
                    case RepStatus.InToSort:
                        text = "Magenta - The ROM is in a ToSort directory.";
                        break;
                    case RepStatus.Corrupt:
                        text = "Red - This file is corrupt.";
                        break;
                    case RepStatus.UnScanned:
                        text = "Blue - The file could not be scanned. The file could be locked or have incompatible permissions.";
                        break;
                    case RepStatus.Ignore:
                        text = "GreyBlue - The file matches an ignore rule.";
                        break;
                    case RepStatus.CanBeFixed:
                        text = "Yellow - The ROM is missing here, but it's available elsewhere. The ROM will be fixed.";
                        break;
                    case RepStatus.CanBeFixedMIA:
                        text = "SuperYellow - The MIA ROM is missing here, but it's available elsewhere. The ROM will be fixed.";
                        break;
                    case RepStatus.MoveToSort:
                        text = "Purple - The ROM is not needed here, but a copy isn't located elsewhere. The ROM will be moved to the Primary ToSort.";
                        break;
                    case RepStatus.Delete:
                        text = "Brown - The ROM is not needed here, but a copy is located elsewhere. The ROM will be deleted.";
                        break;
                    case RepStatus.NeededForFix:
                        text = "Orange - The ROM is not needed here, but it's needed elsewhere. The ROM will be moved.";
                        break;
                    case RepStatus.Rename:
                        text = "Light Orange - The ROM is needed here, but has the incorrect name. The ROM will be renamed.";
                        break;
                    case RepStatus.Incomplete:
                        text = "Pink - This is a ROM that could be fixed, but will not be because it is part of an incomplete set.";
                        break;

                    default:
                        text = "";
                        break;
                }

                label.Text = text;
                Controls.Add(label);
            }



            if (Settings.rvSettings.Darkness)
                Dark.dark.SetColors(this);
        }
    }
}