/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using RomVaultCore.RvDB;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace RomVaultCore.ReadDat
{
    public class Entry
    {
        public string Path;
        public RvTreeRow.TreeSelect Selected;
        public bool Expanded;

        public Entry() { }
        public Entry(string path, RvTreeRow.TreeSelect selected, bool expanded)
        {
            Path = path;
            Selected = selected;
            Expanded = expanded;
        }
    }

    public class DatTreeStatusStore
    {
        private Dictionary<string, RvTreeRow> treeRows = new Dictionary<string, RvTreeRow>();

        public void write(int ind)
        {
            PreStoreTreeValue(DB.DirRoot);

            List<Entry> entries = new List<Entry>(treeRows.Count);
            foreach (var row in treeRows)
            {
                RvTreeRow rvTreeRow = row.Value;
                if (rvTreeRow != null)
                    entries.Add(new Entry(row.Key, rvTreeRow.Checked, rvTreeRow.TreeExpanded));
            }
            using (FileStream writer = File.Create($"treeDefault{ind}.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Entry>));
                serializer.Serialize(writer, entries);
            }
        }

        public void read(int ind)
        {
            string filename = $"treeDefault{ind}.xml";
            if (!File.Exists(filename))
                return;
            using (FileStream reader = new FileStream(filename, FileMode.Open))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Entry>));
                List<Entry> myList = (List<Entry>)serializer.Deserialize(reader);
                foreach (var v in myList)
                {
                    var t = new RvTreeRow();
                    t.SetChecked(v.Selected, true);
                    t.SetTreeExpanded(v.Expanded, true);
                    treeRows.Add(v.Path, t);
                }
                RvTreeRow.OpenStream();
                SetBackTreeValues(DB.DirRoot, false);
                RvTreeRow.CloseStream();
            }
        }

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
        public void SetBackTreeValues(RvFile lDir, bool isCore)
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
                            dbChild.Tree.SetChecked(rVal.Checked, isCore);
                            dbChild.Tree.SetTreeExpanded(rVal.TreeExpanded, isCore);
                        }
                    }
                }

                if (dbChild?.FileType == FileType.Dir)
                    SetBackTreeValues(dbChild, isCore);

                dbIndex++;
            }
        }
    }
}
