using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace ROMVault
{
    public class DVTreeView : TreeView
    {
        public enum CheckedState : int { UnChecked = 0, Checked = 1, Mixed = 2 };

        public DVTreeView() : base()
        {
            StateImageList = new ImageList();
            StateImageList.Images.Add(GetCheckBoxImage(CheckBoxState.UncheckedNormal));
            StateImageList.Images.Add(GetCheckBoxImage(CheckBoxState.CheckedNormal));
            StateImageList.Images.Add(GetCheckBoxImage(CheckBoxState.MixedNormal));
        }

        private Bitmap GetCheckBoxImage(CheckBoxState cbState)
        {
            Point location = new Point(0, 1);
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                CheckBoxRenderer.DrawCheckBox(g, location, cbState);
            }
            return bitmap;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            CheckBoxes = false;
        }

        public void UpdateBase()
        {
            SetAllNodeImages(Nodes[0]);
        }

        private CheckedState SetAllNodeImages(TreeNode tNode)
        {
            if (tNode.Nodes.Count == 0)
            {
                if (tNode.Checked)
                {
                    tNode.StateImageIndex = (int)CheckedState.Checked;
                    return CheckedState.Checked;
                }
                else
                {
                    tNode.StateImageIndex = (int)CheckedState.UnChecked;
                    return CheckedState.UnChecked;
                }
            }
            bool isChecked = false;
            bool isUnchecked = false;
            foreach (TreeNode node in tNode.Nodes)
            {
                CheckedState tState = SetAllNodeImages(node);
                if (tState == CheckedState.Checked)
                    isChecked = true;
                if (tState == CheckedState.UnChecked)
                    isUnchecked = true;
                if (tState == CheckedState.Mixed)
                {
                    isChecked = true;
                    isUnchecked = true;
                }
            }
            CheckedState rState;
            if (isChecked && isUnchecked)
                rState = CheckedState.Mixed;
            else if (isChecked)
                rState = CheckedState.Checked;
            else
                rState = CheckedState.UnChecked;

            tNode.StateImageIndex = (int)rState;

            return rState;
        }

        protected override void OnNodeMouseClick(TreeNodeMouseClickEventArgs e)
        {
            base.OnNodeMouseClick(e);

            TreeViewHitTestInfo info = HitTest(e.X, e.Y);
            if (info == null || info.Location != TreeViewHitTestLocations.StateImage)
                return;

            CheckedState rState = (CheckedState)e.Node.StateImageIndex;
            SetChildNodes(e.Node, rState == CheckedState.UnChecked);
            UpdateBase();
        }

        private void SetChildNodes(TreeNode tn, bool nChecked)
        {
            if (tn.Nodes.Count == 0)
            {
                tn.Checked = nChecked;
                return;
            }

            foreach (TreeNode treeNode in tn.Nodes)
                SetChildNodes(treeNode, nChecked);
        }
    }

}
