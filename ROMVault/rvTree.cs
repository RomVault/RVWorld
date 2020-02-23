/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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

        private readonly Font tFont = new Font("Microsoft Sans Serif", 8);
        private readonly Font tFont1 = new Font("Microsoft Sans Serif", 7);

        public RvTree()
        {
            InitializeComponent();
        }

        public RvFile Selected { get; private set; }

        #region "Setup"

        private int _yPos;

        public void Setup(ref RvFile dirTree)
        {
            Selected = null;
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

            int nodeHeight = 16;
            if (pTree.Tree.TreeExpanded && pTree.DirDatCount > 1)
            {
                for (int i = 0; i < pTree.DirDatCount; i++)
                {
                    if (!pTree.DirDat(i).AutoAddedDirectory)
                        nodeHeight += 12;
                }
            }

            UiTree uTree = new UiTree();
            pTree.Tree.UiObject = uTree;

            uTree.TreeBranches = pTreeBranches;

            uTree.RTree = new Rectangle(0, _yPos, 1 + nodeDepth * 18, nodeHeight);
            uTree.RExpand = new Rectangle(5 + nodeDepth * 18, _yPos + 4, 9, 9);
            uTree.RChecked = new Rectangle(20 + nodeDepth * 18, _yPos + 2, 13, 13);
            uTree.RIcon = new Rectangle(35 + nodeDepth * 18, _yPos, 16, 16);
            uTree.RText = new Rectangle(51 + nodeDepth * 18, _yPos, 500, nodeHeight);

            pTreeBranches = pTreeBranches.Replace("├", "│");
            pTreeBranches = pTreeBranches.Replace("└", " ");

            _yPos = _yPos + nodeHeight;

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


            if (!found && pTree.DirDatCount<=1)
            {
                uTree.RExpand = new Rectangle(0, 0, 0, 0);
            }

            if (pTree.Tree.TreeExpanded && found)
            {
                uTree.TreeBranches += "┐";
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
            UiTree uTree = (UiTree)pTree.Tree.UiObject;

            int y = uTree.RTree.Top - _vScroll;

            if (uTree.RTree.IntersectsWith(t))
            {
                Pen p = new Pen(Brushes.Gray, 1) { DashStyle = DashStyle.Dot };

                string lTree = uTree.TreeBranches;
                for (int j = 0; j < lTree.Length; j++)
                {
                    int x = j * 18 - _hScroll;
                    string cTree = lTree.Substring(j, 1);
                    switch (cTree)
                    {
                        case "│":
                            g.DrawLine(p, x + 9, y, x + 9, y + uTree.RTree.Height);
                            break;

                        case "├":
                            g.DrawLine(p, x + 9, y, x + 9, y + uTree.RTree.Height);
                            g.DrawLine(p, x + 9, y + 8, x + 27, y + 8);
                            break;
                        case "└":
                            g.DrawLine(p, x + 9, y, x + 9, y + 8);
                            g.DrawLine(p, x + 9, y + 8, x + 27, y + 8);
                            break;
                        case "┐":
                            g.DrawLine(p, x + 9, y + 8, x + 9, y + uTree.RTree.Height);
                            break;
                    }
                }
            }

            if (!uTree.RExpand.IsEmpty)
            {
                if (uTree.RExpand.IntersectsWith(t))
                {
                    g.DrawImage(pTree.Tree.TreeExpanded ? rvImages.ExpandBoxMinus : rvImages.ExpandBoxPlus, RSub(uTree.RExpand, _hScroll, _vScroll));
                }
            }


            if (uTree.RChecked.IntersectsWith(t))
            {
                switch (pTree.Tree.Checked)
                {
                    case RvTreeRow.TreeSelect.Locked:
                        g.DrawImage(rvImages.TickBoxLocked, RSub(uTree.RChecked, _hScroll, _vScroll));
                        break;
                    case RvTreeRow.TreeSelect.UnSelected:
                        g.DrawImage(rvImages.TickBoxUnTicked, RSub(uTree.RChecked, _hScroll, _vScroll));
                        break;
                    case RvTreeRow.TreeSelect.Selected:
                        g.DrawImage(rvImages.TickBoxTicked, RSub(uTree.RChecked, _hScroll, _vScroll));
                        break;
                }
            }

            if (uTree.RIcon.IntersectsWith(t))
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
                if (pTree.Dat == null && pTree.DirDatCount == 0) // Directory above DAT's in Tree
                {
                    bm = rvImages.GetBitmap("DirectoryTree" + icon);
                }
                else if (pTree.Dat == null && pTree.DirDatCount >= 1) // Directory that contains DAT's
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
                    g.DrawImage(bm, RSub(uTree.RIcon, _hScroll, _vScroll));
                }
            }


            Rectangle recBackGround = new Rectangle(uTree.RText.X, uTree.RText.Y, Width - uTree.RText.X + _hScroll, uTree.RText.Height);

            if (recBackGround.IntersectsWith(t))
            {
                string thistxt;
                List<string> datList = null;
                string subtxt = "( Have:" + pTree.DirStatus.CountCorrect() + " \\ Missing: " + pTree.DirStatus.CountMissing() + " )";

                if (pTree.Dat == null && pTree.DirDatCount == 0) // Directory above DAT's in Tree
                {
                    thistxt = pTree.Name;
                }
                else if (pTree.Dat == null && pTree.DirDatCount == 1) // Directory that contains DAT's
                {
                    thistxt = pTree.Name + ": " + pTree.DirDat(0).GetData(RvDat.DatData.Description);
                }
                else if (pTree.Dat == null && pTree.DirDatCount > 1) // Directory above DAT's in Tree
                {
                    thistxt = pTree.Name;
                    if (pTree.Tree.TreeExpanded)
                    {
                        datList = new List<string>();
                        for (int i = 0; i < pTree.DirDatCount; i++)
                        {
                            if (!pTree.DirDat(i).AutoAddedDirectory)
                            {
                                string title = pTree.DirDat(i).GetData(RvDat.DatData.Description);
                                if (string.IsNullOrWhiteSpace(title))
                                    title = pTree.DirDat(i).GetData(RvDat.DatData.DatName);
                                datList.Add(title);
                            }
                        }
                    }
                }

                // pTree.Parent.DirDatCount>1: This should probably be a test like parent contains Dat 
                else if (pTree.Dat != null && pTree.Dat.AutoAddedDirectory && pTree.Parent.DirDatCount > 1)
                {
                    thistxt = pTree.Name + ": ";
                }
                else if (pTree.Dat != null && pTree.DirDatCount == 0) // Directories made by a DAT
                {
                    thistxt = pTree.Name;
                }
                else
                {
                    ReportError.SendAndShow("Unknown Tree settings in DisplayTree.");
                    thistxt = "";
                }

                if (pTree.IsInToSort)
                {
                    subtxt = "";
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

                Brush textBrush;
                if (Selected != null && pTree.TreeFullName == Selected.TreeFullName)
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(51, 153, 255)), RSub(recBackGround, _hScroll, _vScroll));
                    textBrush = Brushes.Wheat;
                }
                else
                {
                    textBrush = Brushes.Black;
                }

                thistxt += " " + subtxt;
                g.DrawString(thistxt, tFont, textBrush, uTree.RText.Left - _hScroll, uTree.RText.Top + 1 - _vScroll);
                
                if (datList != null)
                {
                    for (int i = 0; i < datList.Count; i++)
                    {
                        g.DrawString(datList[i], tFont1, textBrush,
                            ((UiTree)pTree.Tree.UiObject).RText.Left + 20 - _hScroll,
                            ((UiTree)pTree.Tree.UiObject).RText.Top + 14 + i * 12 - _vScroll);
                    }
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
                        Selected = t;
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
            return Selected;
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

                Selected = pTree;
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
            RvTreeRow.OpenStream();
            SetCheckedRecurse(pTree,nSelection);
            RvTreeRow.CloseStream();
        }

        private static void SetCheckedRecurse(RvFile pTree, RvTreeRow.TreeSelect nSelection)
        {
            pTree.Tree.Checked = nSelection;
            for (int i = 0; i < pTree.ChildCount; i++)
            {
                RvFile d = pTree.Child(i);
                if (d.IsDir && d.Tree != null)
                {
                    SetCheckedRecurse(d, nSelection);
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
            RvTreeRow.OpenStream();
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
            RvTreeRow.CloseStream();
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