using RomVaultCore.RvDB;
using System.Collections.Generic;

namespace RomVaultCore.ReadDat
{
    public class DatTreeStatusStore
    {
        private Dictionary<string, RvTreeRow> treeRows = new Dictionary<string, RvTreeRow>();

        public void PreStoreTreeValue(RvFile lDir)
        {
            int dbIndex = 0;
            while (dbIndex < lDir.ChildCount)
            {
                RvFile dbChild = lDir.Child(dbIndex);

                if (dbChild.Tree != null)
                {
                    string path = dbChild.TreeFullName;
                    if (treeRows.ContainsKey(path))
                    {
                        treeRows.Remove(path);
                        treeRows.Add(path, null);
                    }
                    else
                        treeRows.Add(dbChild.TreeFullName, dbChild.Tree);
                }
                if (dbChild?.FileType == FileType.Dir)
                    PreStoreTreeValue(dbChild);

                dbIndex++;
            }
        }
        public void SetBackTreeValues(RvFile lDir)
        {
            int dbIndex = 0;
            while (dbIndex < lDir.ChildCount)
            {
                RvFile dbChild = lDir.Child(dbIndex);

                if (dbChild.Tree != null)
                {
                    if (treeRows.TryGetValue(dbChild.TreeFullName, out RvTreeRow rVal))
                    {
                        if (rVal != null && rVal != dbChild.Tree)
                        {
                            dbChild.Tree.SetChecked(rVal.Checked, true);
                            dbChild.Tree.SetTreeExpanded(rVal.TreeExpanded, true);
                        }
                    }
                }

                if (dbChild?.FileType == FileType.Dir)
                    SetBackTreeValues(dbChild);

                dbIndex++;
            }
        }
    }
}
