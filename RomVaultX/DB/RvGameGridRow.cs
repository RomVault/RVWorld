using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;

namespace RomVaultX.DB
{
    public class rvGameGridRow
    {
        private static SQLiteCommand _commandRvGameGridRowRead;
        public int GameId;
        public string Name;
        public string Description;
        public int RomGot;
        public int RomTotal;
        public int RomNoDump;

        public static List<rvGameGridRow> ReadGames(int datId)
        {
            if (_commandRvGameGridRowRead == null)
            {
                _commandRvGameGridRowRead = new SQLiteCommand(@"
                    SELECT GameId,Name,Description,RomTotal,RomGot,RomNoDump FROM game WHERE DatId=@datId ORDER BY Name"
                    , Program.db.Connection);
                _commandRvGameGridRowRead.Parameters.Add(new SQLiteParameter("datId"));
            }

            List<rvGameGridRow> rows = new List<rvGameGridRow>();
            _commandRvGameGridRowRead.Parameters["DatId"].Value = datId;

            using (DbDataReader dr = _commandRvGameGridRowRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    rvGameGridRow gridRow = new rvGameGridRow
                    {
                        GameId = Convert.ToInt32(dr["GameID"]),
                        Name = dr["name"].ToString(),
                        Description = dr["description"].ToString(),
                        RomGot = Convert.ToInt32(dr["RomGot"]),
                        RomTotal = Convert.ToInt32(dr["RomTotal"]),
                        RomNoDump = Convert.ToInt32(dr["RomNoDump"])
                    };
                    rows.Add(gridRow);
                }
                dr.Close();
            }
            return rows;
        }

        public bool HasCorrect()
        {
            return RomGot > 0;
        }

        public bool HasMissing()
        {
            return RomTotal - RomNoDump - RomGot > 0;
        }
    }
}