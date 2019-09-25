using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using RomVaultX.Util;

namespace RomVaultX
{
    internal static class FixDatList
    {
        private static SQLiteCommand CommandFindRomsInGame;

        public static void extract(string dirName)
        {

            if (CommandFindRomsInGame == null)
                CommandFindRomsInGame = new SQLiteCommand(
                    @"SELECT
                    ROM.RomId, ROM.name, ROM.size, ROM.crc, ROM.SHA1
                 FROM ROM WHERE ROM.FileId is null AND ROM.GameId=@GameId ORDER BY ROM.RomId", Program.db.Connection);
            CommandFindRomsInGame.Parameters.Add(new SQLiteParameter("GameId"));
            
            Debug.WriteLine(dirName);
            
            SQLiteCommand getfiles = new SQLiteCommand(@"SELECT dat.datid,dir.FullName,GameId,game.Name FROM dir,dat,game where dat.dirid=dir.dirid and game.datid=dat.datid and dir.fullname like '" + dirName + @"%' 
and (select count(1) from ROM WHERE ROM.FileId is null AND ROM.GameId=Game.GameId)>0 order by dat.datid,GameId", Program.db.Connection);

            DbDataReader reader = getfiles.ExecuteReader();

            int DatId = -1;

            while (reader.Read())
            {
                int thisDatId = Convert.ToInt32(reader["datId"]);
                if (thisDatId == DatId)
                {
                    string dirFullName = reader["FullName"].ToString();
                    Debug.WriteLine(dirFullName);
                    DatId = thisDatId;
                }
                int GameId = Convert.ToInt32(reader["GameId"]);
                string GameName = reader["name"].ToString();
                Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                int romCount = 0;
                using (DbDataReader drRom = ZipSetGetRomsInGame(GameId))
                {
                    while (drRom.Read())
                    {
                        int RomId = Convert.ToInt32(drRom["RomId"]);
                        string RomName = drRom["name"].ToString();
                        ulong size = Convert.ToUInt64(drRom["size"]);
                        byte[] CRC = VarFix.CleanMD5SHA1(drRom["crc"].ToString(), 8);
                        byte[] sha1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 32);

                        Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  CRC: " + VarFix.ToString(CRC));


                        romCount += 1;
                    }
                }

            }


        }

        private static DbDataReader ZipSetGetRomsInGame(int GameId)
        {
            CommandFindRomsInGame.Parameters["GameId"].Value = GameId;
            return CommandFindRomsInGame.ExecuteReader();
        }

    }
}
