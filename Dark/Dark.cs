using System.Diagnostics;
using System.Runtime.InteropServices;
using System;
using System.Windows.Forms;
using System.Drawing;

namespace Dark
{
    public static class dark
    {
        public static Color bg0 = Color.FromArgb(37, 39, 44);

        public static Color bg = Color.FromArgb(47, 49, 54);
        public static Color bg1 = Color.FromArgb(54, 57, 63);
        public static Color fg = Color.FromArgb(210, 210, 210);
        public static Brush sb_bg = new SolidBrush(bg);
        public static Brush sb_bg1 = new SolidBrush(bg1);
        public static Brush sb_fg = new SolidBrush(fg);

        public static bool darkEnabled;


        [DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

        private static string ToBgr(Color c) => $"{c.B:X2}{c.G:X2}{c.R:X2}";
        const int DWWMA_CAPTION_COLOR = 35;
        private static void SetTitleBarColor(Color color, IntPtr handle)
        {
            if (IsUnix)
                return;

            IntPtr hWnd = handle;
            int[] colorstr = new int[] { int.Parse(ToBgr(color), System.Globalization.NumberStyles.HexNumber) };
            DwmSetWindowAttribute(hWnd, DWWMA_CAPTION_COLOR, colorstr, 4);
        }

        public static void SetColors(Form frm)
        {
            SetTitleBarColor(Color.FromArgb(32, 34, 37), frm.Handle);
            foreach (Control c in frm.Controls)
            {
                SetColors(c);
            }
            frm.BackColor = bg;
            frm.ForeColor = fg;
        }

        public static void SetColors(Control c)
        {
            c.BackColor = bg;
            c.ForeColor = fg;

            foreach (Control c1 in c.Controls)
                SetColors(c1);

            //Debug.WriteLine(c.GetType().ToString(), c.Name);

            switch (c)
            {
                case TextBox tb:
                    tb.BorderStyle = BorderStyle.None;
                    tb.BackColor = bg0;
                    break;
                case DataGridView dgv:
                    dgv.BackgroundColor = bg1;
                    dgv.ForeColor = fg;

                    dgv.DefaultCellStyle.BackColor = bg1;
                    dgv.DefaultCellStyle.ForeColor = fg;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = bg;
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = fg;
                    dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

                    dgv.GridColor = bg;
                    break;

                case Label _:
                case Button _:
                case SplitContainer _:
                case TrackBar _:
                case CheckBox _:
                case ComboBox _:
                case Panel _:
                case PictureBox _:
                case RichTextBox _:
                case MenuStrip _:
                case HScrollBar _:
                case VScrollBar _:
                case TabControl _:
                case GroupBox _:
                case ProgressBar _:
                    break;
                default:
                    Debug.WriteLine($"Control Unknown {c}");
                    break;
            }
        }

        public static Color bgColor(Color c)
        {
            return darkEnabled ? bg : c;
        }
        public static Color bgColor1(Color c)
        {
            return darkEnabled ? bg1 : c;
        }

        public static Brush bgBrush(Brush b)
        {
            return darkEnabled ? sb_bg : b;
        }
        public static Brush bgBrush1(Brush b)
        {
            return darkEnabled ? sb_bg1 : b;
        }
        public static Brush fgBrush(Brush b)
        {
            return darkEnabled ? sb_fg : b;
        }
        public static Color Down(Color c)
        {
            if (!darkEnabled)
                return c;

            return Color.FromArgb(255, (int)(c.R * 0.8), (int)(c.G * 0.8), (int)(c.B * 0.8));
        }

        public static bool IsUnix
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return ((p == 4) || (p == 6) || (p == 128));
            }
        }
    }
}
