using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Drawing;
using RVXCore.DB;

namespace RVXCore
{

    public class RvTreeRow
    {
        private static SQLiteCommand CommandReadTree;


        private static SQLiteCommand _commandGetFirstExpanded;
        public uint DirId;
        public string dirName;
        public string dirFullName;
        public bool Expanded;

        public uint? DatId;
        public string datName;
        public string description;

        public int RomTotal;
        public int RomGot;
        public int RomNoDump;

        public bool MultiDatDir;


        public static List<RvTreeRow> ReadTreeFromDB()
        {
            if (CommandReadTree == null)
            {
                CommandReadTree = new SQLiteCommand(@"
                    SELECT 
                        dir.DirId as DirId,
                        dir.name as dirname,
                        dir.fullname,
                        dir.expanded,
                        dir.RomTotal as dirRomTotal,
                        dir.RomGot as dirRomGot,
                        dir.RomNoDump as dirNoDump,
                        dat.DatId,
                        dat.name as datname,
                        dat.description,
                        dat.RomTotal,
                        dat.RomGot,
                        dat.RomNoDump
                    FROM dir LEFT JOIN dat ON dir.DirId=dat.DirId
                    ORDER BY dir.Fullname,dat.Filename",  DBSqlite.db.Connection);
            }

            List<RvTreeRow> rows = new List<RvTreeRow>();

            using (DbDataReader dr = CommandReadTree.ExecuteReader())
            {
                bool multiDatDirFound = false;

                string skipUntil = "";

                RvTreeRow lastTree = null;
                while (dr.Read())
                {
                    // a single DAT in a directory is just displayed in the tree at the same level as the directory
                    RvTreeRow pTree = new RvTreeRow
                    {
                        DirId = Convert.ToUInt32(dr["DirId"]),
                        dirName = dr["dirname"].ToString(),
                        dirFullName = dr["fullname"].ToString(),
                        Expanded = Convert.ToBoolean(dr["expanded"]),
                        DatId = dr["DatId"] == DBNull.Value ? null : (uint?) Convert.ToUInt32(dr["DatId"]),
                        datName = dr["datname"] == DBNull.Value ? null : dr["datname"].ToString(),
                        description = dr["description"] == DBNull.Value ? null : dr["description"].ToString(),
                        RomTotal = dr["RomTotal"] == DBNull.Value ? Convert.ToInt32(dr["dirRomTotal"]) : Convert.ToInt32(dr["RomTotal"]),
                        RomGot = dr["RomGot"] == DBNull.Value ? Convert.ToInt32(dr["dirRomGot"]) : Convert.ToInt32(dr["RomGot"]),
                        RomNoDump = dr["RomNoDump"] == DBNull.Value ? Convert.ToInt32(dr["dirNoDump"]) : Convert.ToInt32(dr["RomNoDump"])
                    };

                    if (!string.IsNullOrEmpty(skipUntil))
                    {
                        if (pTree.dirFullName.Length >= skipUntil.Length)
                        {
                            if (pTree.dirFullName.Substring(0, skipUntil.Length) == skipUntil)
                            {
                                continue;
                            }
                        }
                    }
                    if (!pTree.Expanded)
                    {
                        skipUntil = pTree.dirFullName;
                        pTree.DatId = null;
                        pTree.datName = null;
                        pTree.description = null;
                        pTree.RomTotal = Convert.ToInt32(dr["dirRomTotal"]);
                        pTree.RomGot = Convert.ToInt32(dr["dirRomGot"]);
                        pTree.RomNoDump = Convert.ToInt32(dr["dirNoDump"]);
                    }
                    rows.Add(pTree);

                    if (lastTree != null)
                    {
                        // if multiple DAT's are in the same directory then we should add another level in the tree to display the directory
                        bool thisMultiDatDirFound = lastTree.DirId == pTree.DirId;
                        if (thisMultiDatDirFound && !multiDatDirFound)
                        {
                            // found a new multidat
                            RvTreeRow dirTree = new RvTreeRow
                            {
                                DirId = lastTree.DirId,
                                dirName = lastTree.dirName,
                                dirFullName = lastTree.dirFullName,
                                Expanded = lastTree.Expanded,
                                DatId = null,
                                datName = null,
                                RomTotal = Convert.ToInt32(dr["dirRomTotal"]),
                                RomGot = Convert.ToInt32(dr["dirRomGot"]),
                                RomNoDump = Convert.ToInt32(dr["dirNoDump"])
                            };
                            rows.Insert(rows.Count - 2, dirTree);
                            lastTree.MultiDatDir = true;
                        }
                        if (thisMultiDatDirFound)
                        {
                            pTree.MultiDatDir = true;
                        }

                        multiDatDirFound = thisMultiDatDirFound;
                    }


                    lastTree = pTree;
                }
            }

            return rows;
        }

        public static void SetTreeExpandedChildren(uint DirId)
        {
            int? value = GetFirstExpanded(DirId);
            if (value == null)
            {
                return;
            }
            value = 1 - value;

            List<uint> todo = new List<uint>();
            todo.Add(DirId);

            while (todo.Count > 0)
            {
                UpdateSelectedFromList(todo, (int) value);
                todo = UpdateSelectedGetChildList(todo);
            }
        }

        private static int? GetFirstExpanded(uint DirId)
        {
            if (_commandGetFirstExpanded == null)
            {
                _commandGetFirstExpanded = new SQLiteCommand(@"
                SELECT expanded FROM dir WHERE ParentDirId=@DirId ORDER BY fullname LIMIT 1",  DBSqlite.db.Connection);
                _commandGetFirstExpanded.Parameters.Add(new SQLiteParameter("DirId"));
            }

            _commandGetFirstExpanded.Parameters["DirId"].Value = DirId;
            object res = _commandGetFirstExpanded.ExecuteScalar();
            if ((res == null) || (res == DBNull.Value))
            {
                return null;
            }
            return Convert.ToInt32(res);
        }


        private static void UpdateSelectedFromList(List<uint> todo, int value)
        {
            string todoList = string.Join(",", todo);
            using (DbCommand SetStatus = new SQLiteCommand(@"UPDATE dir SET expanded=" + value + " WHERE ParentDirId in (" + todoList + ")",  DBSqlite.db.Connection))
            {
                SetStatus.ExecuteNonQuery();
            }
        }

        private static List<uint> UpdateSelectedGetChildList(List<uint> todo)
        {
            string todoList = string.Join(",", todo);
            List<uint> retList = new List<uint>();
            using (DbCommand GetChild = new SQLiteCommand(@"select DirId from dir where ParentDirId in (" + todoList + ")",  DBSqlite.db.Connection))
            {
                using (DbDataReader dr = GetChild.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        uint id = Convert.ToUInt32(dr["DirId"]);
                        retList.Add(id);
                    }
                    dr.Close();
                }
            }
            return retList;
        }

        private static SQLiteCommand _commandSetTreeExpanded;

        public static void SetTreeExpanded(uint DirId, bool expanded)
        {
            if (_commandSetTreeExpanded == null)
            {
                _commandSetTreeExpanded = new SQLiteCommand(@"
                    UPDATE dir SET expanded=@expanded WHERE DirId=@dirId", DBSqlite.db.Connection);
                _commandSetTreeExpanded.Parameters.Add(new SQLiteParameter("expanded"));
                _commandSetTreeExpanded.Parameters.Add(new SQLiteParameter("dirId"));
            }
            _commandSetTreeExpanded.Parameters["dirId"].Value = DirId;
            _commandSetTreeExpanded.Parameters["expanded"].Value = expanded;
            _commandSetTreeExpanded.ExecuteNonQuery();
        }

    }
}