using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ROMVault
{
    public partial class FrmMain
    {
        private void ReadDefaults()
        {
            defaults defaults = defaults.ReadDefaults();
            if (defaults != null)
            {
                if (defaults.mainX > -30000 && defaults.mainY > -30000 && defaults.mainHeight > 50)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(defaults.mainX, defaults.mainY);
                    this.Size = new Size(defaults.mainWidth, defaults.mainHeight);
                }

                if (defaults.splitDatInfoGameInfo_pos != int.MinValue) this.splitDatInfoGameInfo.SplitterDistance = defaults.splitDatInfoGameInfo_pos;
                if (defaults.splitGameListRomList_pos != int.MinValue) this.splitGameListRomList.SplitterDistance = defaults.splitGameListRomList_pos;
                if (defaults.splitListArt_pos != int.MinValue) this.splitListArt.SplitterDistance = defaults.splitListArt_pos;

                if (defaults.gg0_width != int.MinValue) GameGrid.Columns[0].Width = defaults.gg0_width;
                if (defaults.gg1_width != int.MinValue) GameGrid.Columns[1].Width = defaults.gg1_width;
                if (defaults.gg2_width != int.MinValue) GameGrid.Columns[2].Width = defaults.gg2_width;
                if (defaults.gg3_width != int.MinValue) GameGrid.Columns[3].Width = defaults.gg3_width;

                if (defaults.rg0_width != int.MinValue) RomGrid.Columns[0].Width = defaults.rg0_width;
                if (defaults.rg1_width != int.MinValue) RomGrid.Columns[1].Width = defaults.rg1_width;
                if (defaults.rg2_width != int.MinValue) RomGrid.Columns[2].Width = defaults.rg2_width;
                if (defaults.rg3_width != int.MinValue) RomGrid.Columns[3].Width = defaults.rg3_width;
                if (defaults.rg4_width != int.MinValue) RomGrid.Columns[4].Width = defaults.rg4_width;
                if (defaults.rg5_width != int.MinValue) RomGrid.Columns[5].Width = defaults.rg5_width;
                if (defaults.rg6_width != int.MinValue) RomGrid.Columns[6].Width = defaults.rg6_width;
                if (defaults.rg7_width != int.MinValue) RomGrid.Columns[7].Width = defaults.rg7_width;
                if (defaults.rg8_width != int.MinValue) RomGrid.Columns[8].Width = defaults.rg8_width;
                if (defaults.rg9_width != int.MinValue) RomGrid.Columns[9].Width = defaults.rg9_width;
                if (defaults.rg10_width != int.MinValue) RomGrid.Columns[10].Width = defaults.rg10_width;
                if (defaults.rg11_width != int.MinValue) RomGrid.Columns[11].Width = defaults.rg11_width;
                if (defaults.rg12_width != int.MinValue) RomGrid.Columns[12].Width = defaults.rg12_width;
                if (defaults.rg13_width != int.MinValue) RomGrid.Columns[13].Width = defaults.rg13_width;
                if (defaults.rg14_width != int.MinValue) RomGrid.Columns[14].Width = defaults.rg14_width;

                if (defaults.nfo_FontSize != int.MinValue) trbFontSize.Value = defaults.nfo_FontSize;

            }
        }


        private void WriteDefaults()
        {


            defaults df = new defaults();
            if (this.WindowState == FormWindowState.Minimized)
            {
                df.mainX = this.RestoreBounds.X;
                df.mainY = this.RestoreBounds.Y;
                df.mainWidth = this.RestoreBounds.Width;
                df.mainHeight = this.RestoreBounds.Height;
            }
            else
            {
                df.mainX = this.Location.X;
                df.mainY = this.Location.Y;
                df.mainWidth = this.Size.Width;
                df.mainHeight = this.Size.Height;
            }

            df.splitDatInfoGameInfo_pos = this.splitDatInfoGameInfo.SplitterDistance;
            df.splitGameListRomList_pos = this.splitGameListRomList.SplitterDistance;
            df.splitListArt_pos = this.splitListArt.SplitterDistance;

            df.gg0_width = GameGrid.Columns[0].Width;
            df.gg1_width = GameGrid.Columns[1].Width;
            df.gg2_width = GameGrid.Columns[2].Width;
            df.gg3_width = GameGrid.Columns[3].Width;

            df.rg0_width = RomGrid.Columns[0].Width;
            df.rg1_width = RomGrid.Columns[1].Width;
            df.rg2_width = RomGrid.Columns[2].Width;
            df.rg3_width = RomGrid.Columns[3].Width;
            df.rg4_width = RomGrid.Columns[4].Width;
            df.rg5_width = RomGrid.Columns[5].Width;
            df.rg6_width = RomGrid.Columns[6].Width;
            df.rg7_width = RomGrid.Columns[7].Width;
            df.rg8_width = RomGrid.Columns[8].Width;
            df.rg9_width = RomGrid.Columns[9].Width;
            df.rg10_width = RomGrid.Columns[10].Width;
            df.rg11_width = RomGrid.Columns[11].Width;
            df.rg12_width = RomGrid.Columns[12].Width;
            df.rg13_width = RomGrid.Columns[13].Width;
            df.rg14_width = RomGrid.Columns[14].Width;

            df.nfo_FontSize = trbFontSize.Value;

            defaults.WriteDefaults(df);
        }
    }
}
