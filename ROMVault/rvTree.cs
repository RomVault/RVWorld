/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RVCore;
using RVCore.RvDB;

namespace ROMVault
{

    public partial class RvTree : UserControl
    {
        private class UiTree
        {
            public string TreeBranches;
            public Rectangle RTree;
            public Rectangle RExpand;
            public Rectangle RChecked;
            public Rectangle RIcon;
            public Rectangle RText;
        }

        public event MouseEventHandler RvSelected;
        public event MouseEventHandler RvChecked;

        private RvFile _lTree;

        private RvFile _lSelected;

        public RvTree()
        {
            InitializeComponent();
        }


        public RvFile Selected => _lSelected;


        #region "Setup"

        private int _yPos;

        public void Setup(ref RvFile dirTree)
        {
            _lSelected = null;
            _lTree = dirTree;
            SetupInt();
        }

        private void SetupInt()
        {
            _yPos = 0;

            int treeCount = _lTree.ChildCount;

            if (treeCount >= 1)
            {
                for (int i = 0; i < treeCount - 1; i++)
                {
                    SetupTree(_lTree.Child(i), "├");
                }

                SetupTree(_lTree.Child(treeCount - 1), "└");
            }
            AutoScrollMinSize = new Size(500, _yPos);
            Refresh();
        }

        private void SetupTree(RvFile pTree, string pTreeBranches)
        {
            int nodeDepth = pTreeBranches.Length - 1;

            pTree.Tree.UiObject = new UiTree();

            ((UiTree)pTree.Tree.UiObject).TreeBranches = pTreeBranches;

            ((UiTree)pTree.Tree.UiObject).RTree = new Rectangle(0, _yPos - 8, 1 + nodeDepth * 18, 16);
            ((UiTree)pTree.Tree.UiObject).RExpand = new Rectangle(5 + nodeDepth * 18, _yPos + 4, 9, 9);
            ((UiTree)pTree.Tree.UiObject).RChecked = new Rectangle(20 + nodeDepth * 18, _yPos + 2, 13, 13);
            ((UiTree)pTree.Tree.UiObject).RIcon = new Rectangle(35 + nodeDepth * 18, _yPos, 16, 16);
            ((UiTree)pTree.Tree.UiObject).RText = new Rectangle(51 + nodeDepth * 18, _yPos, 500, 16);

            pTreeBranches = pTreeBranches.Replace("├", "│");
            pTreeBranches = pTreeBranches.Replace("└", " ");

            _yPos = _yPos + 16;

            bool found = false;
            int last = 0;
            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile dir = pTree.Child(i);
                if (!dir.IsDir)
                    continue;

                if (dir.Tree == null)
                    continue;

                found = true;
                if (pTree.Tree.TreeExpanded)
                    last = i;

            }
            if (!found)
            {
                ((UiTree)pTree.Tree.UiObject).RExpand = new Rectangle(0, 0, 0, 0);
            }


            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile dir = pTree.Child(i);
                if (!dir.IsDir)
                    continue;

                if (dir.Tree == null)
                    continue;

                if (!pTree.Tree.TreeExpanded)
                    continue;

                if (i != last)
                    SetupTree(pTree.Child(i), pTreeBranches + "├");
                else
                    SetupTree(pTree.Child(i), pTreeBranches + "└");
            }
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

            if (_lTree == null)
                return;

            for (int i = 0; i < _lTree.ChildCount; i++)
            {
                RvFile tDir = _lTree.Child(i);
                if (!tDir.IsDir)
                    continue;

                if (tDir.Tree?.UiObject != null)
                {
                    PaintTree(tDir, g, t);
                }
            }
        }

        private void PaintTree(RvFile pTree, Graphics g, Rectangle t)
        {
            int y = ((UiTree)pTree.Tree.UiObject).RTree.Top - _vScroll;

            if (((UiTree)pTree.Tree.UiObject).RTree.IntersectsWith(t))
            {
                Pen p = new Pen(Brushes.Gray, 1) { DashStyle = DashStyle.Dot };

                string lTree = ((UiTree)pTree.Tree.UiObject).TreeBranches;
                for (int j = 0; j < lTree.Length; j++)
                {
                    int x = j * 18 - _hScroll;
                    string cTree = lTree.Substring(j, 1);
                    switch (cTree)
                    {
                        case "│":
                            g.DrawLine(p, x + 9, y, x + 9, y + 16);
                            break;

                        case "├":
                        case "└":
                            g.DrawLine(p, x + 9, y, x + 9, y + 16);
                            g.DrawLine(p, x + 9, y + 16, x + 27, y + 16);
                            break;
                    }
                }
            }

            if (!((UiTree)pTree.Tree.UiObject).RExpand.IsEmpty)
            {
                if (((UiTree)pTree.Tree.UiObject).RExpand.IntersectsWith(t))
                {
                    g.DrawImage(pTree.Tree.TreeExpanded ? rvImages.ExpandBoxMinus : rvImages.ExpandBoxPlus, RSub(((UiTree)pTree.Tree.UiObject).RExpand, _hScroll, _vScroll));
                }
            }


            if (((UiTree)pTree.Tree.UiObject).RChecked.IntersectsWith(t))
            {
                switch (pTree.Tree.Checked)
                {
                    case RvTreeRow.TreeSelect.Locked:
                        g.DrawImage(rvImages.TickBoxLocked, RSub(((UiTree)pTree.Tree.UiObject).RChecked, _hScroll, _vScroll));
                        break;
                    case RvTreeRow.TreeSelect.UnSelected:
                        g.DrawImage(rvImages.TickBoxUnTicked, RSub(((UiTree)pTree.Tree.UiObject).RChecked, _hScroll, _vScroll));
                        break;
                    case RvTreeRow.TreeSelect.Selected:
                        g.DrawImage(rvImages.TickBoxTicked, RSub(((UiTree)pTree.Tree.UiObject).RChecked, _hScroll, _vScroll));
                        break;
                }
            }

            if (((UiTree)pTree.Tree.UiObject).RIcon.IntersectsWith(t))
            {
                int icon = 2;
                if (pTree.DirStatus.HasInToSort())
                {
                    icon = 4;
                }
                else if (!pTree.DirStatus.HasCorrect())
                {
                    icon = 1;
                }
                else if (!pTree.DirStatus.HasMissing())
                {
                    icon = 3;
                }


                Bitmap bm;
                if (pTree.Dat == null && pTree.DirDatCount != 1) // Directory above DAT's in Tree
                {
                    bm = rvImages.GetBitmap("DirectoryTree" + icon);
                }
                else if (pTree.Dat == null && pTree.DirDatCount == 1) // Directory that contains DAT's
                {
                    bm = rvImages.GetBitmap("Tree" + icon);
                }
                else if (pTree.Dat != null && pTree.DirDatCount == 0) // Directories made by a DAT
                {
                    bm = rvImages.GetBitmap("Tree" + icon);
                }
                else
                {
                    ReportError.SendAndShow("Unknown Tree settings in DisplayTree.");
                    bm = null;
                }

                if (bm != null)
                {
                    g.DrawImage(bm, RSub(((UiTree)pTree.Tree.UiObject).RIcon, _hScroll, _vScroll));
                }
            }


            Rectangle recBackGround = new Rectangle(((UiTree)pTree.Tree.UiObject).RText.X, ((UiTree)pTree.Tree.UiObject).RText.Y, Width - ((UiTree)pTree.Tree.UiObject).RText.X + _hScroll, ((UiTree)pTree.Tree.UiObject).RText.Height);

            if (recBackGround.IntersectsWith(t))
            {
                string thistxt;

                if (pTree.Dat == null && pTree.DirDatCount != 1) // Directory above DAT's in Tree
                {
                    thistxt = pTree.Name;
                }
                else if (pTree.Dat == null && pTree.DirDatCount == 1) // Directory that contains DAT's
                {
                    thistxt = pTree.Name + ": " + pTree.DirDat(0).GetData(RvDat.DatData.Description) + " ( Have:" + pTree.DirStatus.CountCorrect() + " \\ Missing: " + pTree.DirStatus.CountMissing() + " )";
                }

                // pTree.Parent.DirDatCount>1: This should probably be a test like parent contains Dat 
                else if (pTree.Dat != null && pTree.Dat.AutoAddDirectory && pTree.Parent.DirDatCount > 1)
                {
                    thistxt = pTree.Name + ": " + pTree.Dat.GetData(RvDat.DatData.Description) + " ( Have:" + pTree.DirStatus.CountCorrect() + " \\ Missing: " + pTree.DirStatus.CountMissing() + " )";
                }
                else if (pTree.Dat != null && pTree.DirDatCount == 0) // Directories made by a DAT
                {
                    thistxt = pTree.Name + " ( Have:" + pTree.DirStatus.CountCorrect() + " \\ Missing: " + pTree.DirStatus.CountMissing() + " )";
                }
                else
                {
                    ReportError.SendAndShow("Unknown Tree settings in DisplayTree.");
                    thistxt = "";
                }

                if (pTree.FileStatusIs(FileStatus.PrimaryToSort | FileStatus.CacheToSort))
                {
                    thistxt += " (Primary)";
                }
                else if (pTree.FileStatusIs(FileStatus.PrimaryToSort))
                {
                    thistxt += " (Primary)";
                }
                else if (pTree.FileStatusIs(FileStatus.CacheToSort))
                {
                    thistxt += " (Cache)";
                }

                if (_lSelected != null && pTree.TreeFullName == _lSelected.TreeFullName)
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(51, 153, 255)), RSub(recBackGround, _hScroll, _vScroll));
                    g.DrawString(thistxt, new Font("Microsoft Sans Serif", 8), Brushes.White, ((UiTree)pTree.Tree.UiObject).RText.Left - _hScroll, ((UiTree)pTree.Tree.UiObject).RText.Top + 1 - _vScroll);
                }
                else
                {
                    g.DrawString(thistxt, new Font("Microsoft Sans Serif", 8), Brushes.Black, ((UiTree)pTree.Tree.UiObject).RText.Left - _hScroll, ((UiTree)pTree.Tree.UiObject).RText.Top + 1 - _vScroll);
                }
            }

            if (!pTree.Tree.TreeExpanded)
                return;

            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile tDir = pTree.Child(i);
                if (tDir.IsDir && tDir.Tree?.UiObject != null)
                {
                    PaintTree(tDir, g, t);
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

        private bool _mousehit;

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            int x = mevent.X + HorizontalScroll.Value;
            int y = mevent.Y + VerticalScroll.Value;

            if (_lTree != null)
            {
                for (int i = 0; i < _lTree.ChildCount; i++)
                {
                    RvFile tDir = _lTree.Child(i);
                    if (tDir.Tree == null)
                        continue;
                    if (CheckMouseUp(tDir, x, y, mevent))
                        break;
                }
            }

            if (!_mousehit)
            {
                return;
            }

            SetupInt();
            base.OnMouseUp(mevent);
        }

        public void SetSelected(RvFile selected)
        {
            bool found = false;

            RvFile t = selected;
            while (t != null)
            {
                if (t.Tree != null)
                {
                    if (!found)
                    {
                        _lSelected = t;
                        found = true;
                    }
                    else
                    {
                        t.Tree.TreeExpanded = true;
                    }
                }
                t = t.Parent;
            }
            SetupInt();
        }

        public RvFile GetSelected()
        {
            return _lSelected;
        }

        private bool CheckMouseUp(RvFile pTree, int x, int y, MouseEventArgs mevent)
        {
            if (((UiTree)pTree.Tree.UiObject).RChecked.Contains(x, y))
            {
                RvChecked?.Invoke(pTree, mevent);

                if (mevent.Button == MouseButtons.Right)
                {
                    _mousehit = true;
                    if (pTree.FileStatusIs(FileStatus.PrimaryToSort) || pTree.FileStatusIs(FileStatus.CacheToSort))
                        return true;

                    SetChecked(pTree, RvTreeRow.TreeSelect.Locked);
                    return true;
                }

                _mousehit = true;
                SetChecked(pTree, pTree.Tree.Checked == RvTreeRow.TreeSelect.Selected ? RvTreeRow.TreeSelect.UnSelected : RvTreeRow.TreeSelect.Selected);
                return true;
            }

            if (((UiTree)pTree.Tree.UiObject).RExpand.Contains(x, y))
            {
                _mousehit = true;
                SetExpanded(pTree, mevent.Button == MouseButtons.Right);
                return true;
            }

            if (((UiTree)pTree.Tree.UiObject).RText.Contains(x, y))
            {
                _mousehit = true;

                RvSelected?.Invoke(pTree, mevent);

                _lSelected = pTree;
                return true;
            }

            if (!pTree.Tree.TreeExpanded)
                return false;

            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile rDir = pTree.Child(i);
                if (!rDir.IsDir || rDir.Tree == null)
                    continue;

                if (CheckMouseUp(rDir, x, y, mevent))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetChecked(RvFile pTree, RvTreeRow.TreeSelect nSelection)
        {
            pTree.Tree.Checked = nSelection;
            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile d = pTree.Child(i);
                if (d.IsDir && d.Tree != null)
                {
                    SetChecked(d, nSelection);
                }
            }
        }

        private static void SetExpanded(RvFile pTree, bool rightClick)
        {
            if (!rightClick)
            {
                pTree.Tree.TreeExpanded = !pTree.Tree.TreeExpanded;
                return;
            }

            // Find the value of the first child node.
            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile d = pTree.Child(i);
                if (!d.IsDir || d.Tree == null)
                    continue;

                //Recusivly Set All Child Nodes to this value
                SetExpandedRecurse(pTree, !d.Tree.TreeExpanded);
                break;
            }
        }

        private static void SetExpandedRecurse(RvFile pTree, bool expanded)
        {
            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile d = pTree.Child(i);
                if (!d.IsDir || d.Tree == null)
                    continue;

                d.Tree.TreeExpanded = expanded;
                SetExpandedRecurse(d, expanded);
            }
        }

        #endregion
    }
}