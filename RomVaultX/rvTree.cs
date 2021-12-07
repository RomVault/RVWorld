using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RVXCore;

namespace RomVaultX
{
    public partial class RvTree : UserControl
    {
        private List<UITreeRow> _rows;

        public RvTree()
        {
            _rows = new List<UITreeRow>();
            InitializeComponent();
        }

        public event MouseEventHandler RvSelected;

        #region "Setup"

        public void Setup(List<RvTreeRow> rows)
        {
            _rows.Clear();

            int yPos = 0;
            int treeCount = rows.Count;
            for (int i = 0; i < treeCount; i++)
            {
                UITreeRow pTree=new UITreeRow(rows[i]);
                _rows.Add(pTree);

                int nodeDepth = pTree.TRow.dirFullName.Count(x => x == '\\') - 1;
                if (pTree.TRow.MultiDatDir)
                {
                    nodeDepth += 1;
                }
                pTree.RTree = new Rectangle(0, yPos - 8, nodeDepth * 18, 16);
                if (pTree.TRow.DatId == null)
                {
                    pTree.RExpand = new Rectangle(5 + nodeDepth * 18, yPos + 4, 9, 9);
                }
                pTree.RIcon = new Rectangle(20 + nodeDepth * 18, yPos, 16, 16);
                pTree.RText = new Rectangle(36 + nodeDepth * 18, yPos, 500, 16);
                yPos = yPos + 16;
            }
            AutoScrollMinSize = new Size(500, yPos);


            string lastBranch = "";
            for (int i = treeCount - 1; i >= 0; i--)
            {
                UITreeRow pTree = _rows[i];
                int nodeDepth = pTree.TRow.dirFullName.Count(x => x == '\\');
                if (pTree.TRow.MultiDatDir)
                {
                    nodeDepth += 1;
                }

                string thisBranch;
                if (nodeDepth - 1 == 0)
                {
                    thisBranch = "";
                }
                else if (nodeDepth - 1 > lastBranch.Length)
                {
                    thisBranch = lastBranch + new string(' ', nodeDepth - 1 - lastBranch.Length);
                }
                else
                {
                    thisBranch = lastBranch.Substring(0, nodeDepth - 1);
                }

                thisBranch = thisBranch.Replace("└", "│");
                thisBranch += "└";

                pTree.TreeBranches = thisBranch;
                lastBranch = thisBranch;
            }
            Refresh();
        }

        #endregion

        #region "Paint"

        private int _hScroll;
        private int _vScroll;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            _hScroll = HorizontalScroll.Value;
            _vScroll = VerticalScroll.Value;

            Rectangle t = new Rectangle(e.ClipRectangle.Left + _hScroll, e.ClipRectangle.Top + _vScroll, e.ClipRectangle.Width, e.ClipRectangle.Height);

            g.FillRectangle(Brushes.White, e.ClipRectangle);


            int treeCount = _rows.Count;
            for (int i = 0; i < treeCount; i++)
            {
                UITreeRow pTree = _rows[i];
                PaintTree(pTree, g, t);
            }
        }

        private void PaintTree(UITreeRow pTree, Graphics g, Rectangle t)
        {
            int y = pTree.RTree.Top - _vScroll;

            if (pTree.RTree.IntersectsWith(t))
            {
                Pen p = new Pen(Brushes.Gray, 1) { DashStyle = DashStyle.Dot };

                string lTree = pTree.TreeBranches;
                for (int j = 0; j < lTree.Length; j++)
                {
                    int x = j * 18 - _hScroll;
                    string cTree = lTree.Substring(j, 1);
                    switch (cTree)
                    {
                        case "│":
                            g.DrawLine(p, x + 9, y, x + 9, y + 16);
                            break;

                        case "└":
                            g.DrawLine(p, x + 9, y, x + 9, y + 16);
                            g.DrawLine(p, x + 9, y + 16, x + 27, y + 16);
                            break;
                    }
                }
            }

            if (!pTree.RExpand.IsEmpty)
            {
                if (pTree.RExpand.IntersectsWith(t))
                {
                    g.DrawImage(pTree.TRow.Expanded ? RvImages.ExpandBoxMinus : RvImages.ExpandBoxPlus, RSub(pTree.RExpand, _hScroll, _vScroll));
                }
            }

            if (pTree.RIcon.IntersectsWith(t))
            {
                int icon;

                if (pTree.TRow.RomGot == pTree.TRow.RomTotal - pTree.TRow.RomNoDump)
                {
                    icon = 3;
                }
                else if (pTree.TRow.RomGot > 0)
                {
                    icon = 2;
                }
                else
                {
                    icon = 1;
                }


                Bitmap bm;
                //if (pTree.Dat == null && pTree.DirDatCount != 1) // Directory above DAT's in Tree
                bm = string.IsNullOrEmpty(pTree.TRow.datName) ?
                    RvImages.GetBitmap("DirectoryTree" + icon) :
                    RvImages.GetBitmap("Tree" + icon);

                if (bm != null)
                {
                    g.DrawImage(bm, RSub(pTree.RIcon, _hScroll, _vScroll));
                }
            }


            Rectangle recBackGround = new Rectangle(pTree.RText.X, pTree.RText.Y, Width - pTree.RText.X + _hScroll, pTree.RText.Height);

            if (recBackGround.IntersectsWith(t))
            {
                string thistxt = pTree.TRow.dirName;
                if (!string.IsNullOrEmpty(pTree.TRow.datName) || !string.IsNullOrEmpty(pTree.TRow.description))
                {
                    if (!string.IsNullOrEmpty(pTree.TRow.description))
                    {
                        thistxt += ": " + pTree.TRow.description;
                    }
                    else
                    {
                        thistxt += ": " + pTree.TRow.datName;
                    }
                }
                if ((pTree.TRow.RomTotal > 0) || (pTree.TRow.RomGot > 0) || (pTree.TRow.RomNoDump > 0))
                {
                    thistxt += " ( Have: " + pTree.TRow.RomGot.ToString("#,0") + " / Missing: " + (pTree.TRow.RomTotal - pTree.TRow.RomGot - pTree.TRow.RomNoDump).ToString("#,0") + " )";
                }
                if (Selected == pTree)
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(51, 153, 255)), RSub(recBackGround, _hScroll, _vScroll));
                    g.DrawString(thistxt, new Font("Microsoft Sans Serif", 8), Brushes.White, pTree.RText.Left - _hScroll, pTree.RText.Top + 1 - _vScroll);
                }
                else
                {
                    g.DrawString(thistxt, new Font("Microsoft Sans Serif", 8), Brushes.Black, pTree.RText.Left - _hScroll, pTree.RText.Top + 1 - _vScroll);
                }
            }
        }


        private static Rectangle RSub(Rectangle r, int h, int v)
        {
            Rectangle ret = new Rectangle(r.Left - h, r.Top - v, r.Width, r.Height);
            return ret;
        }

        #endregion

        #region"Mouse Events"

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            bool mousehit = false;

            int x = mevent.X + HorizontalScroll.Value;
            int y = mevent.Y + VerticalScroll.Value;

            if (_rows != null)
            {
                foreach (UITreeRow tDir in _rows)
                {
                    if (!CheckMouseDown(tDir, x, y, mevent))
                    {
                        continue;
                    }

                    mousehit = true;
                    break;
                }
            }

            if (mousehit)
            {
                return;
            }

            base.OnMouseDown(mevent);
        }

        private bool CheckMouseDown(UITreeRow pTree, int x, int y, MouseEventArgs mevent)
        {
            if (pTree.RExpand.Contains(x, y))
            {
                SetExpanded(pTree, mevent.Button);
                return true;
            }

            if (pTree.RText.Contains(x, y))
            {
                RvSelected?.Invoke(pTree, mevent);

                Selected = pTree;
                Refresh();
                return true;
            }

            return false;
        }

        public UITreeRow Selected { get; private set; }

        private void SetExpanded(UITreeRow pTree, MouseButtons mouseB)
        {
            if (mouseB == MouseButtons.Left)
            {
                RvTreeRow.SetTreeExpanded(pTree.TRow.DirId, !pTree.TRow.Expanded);
                Setup(RvTreeRow.ReadTreeFromDB());
            }
            else
            {
                RvTreeRow.SetTreeExpandedChildren(pTree.TRow.DirId);
                Setup(RvTreeRow.ReadTreeFromDB());
            }
        }



        #endregion
    }
}