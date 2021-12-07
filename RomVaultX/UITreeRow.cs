using System.Drawing;
using RVXCore;

namespace RomVaultX
{
    public class UITreeRow 
    {
        public Rectangle RTree;
        public Rectangle RExpand;
        public Rectangle RIcon;
        public Rectangle RText;

        public string TreeBranches;

        public readonly RvTreeRow TRow;

        public UITreeRow(RvTreeRow treeRow)
        {
            TRow = treeRow;
        }
    }
}
