using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX
{
    internal static class FixDatList
    {
        private static SQLiteCommand CommandFindRomsInGame;

        private static string Etxt(string e)
        {
            string ret = e;
            ret = ret.Replace("&", "&amp;");
            ret = ret.Replace("\"", "&quot;");
            ret = ret.Replace("'", "&apos;");
            ret = ret.Replace("<", "&lt;");
            ret = ret.Replace(">", "&gt;");

            return ret;
        }


        public static void extract(string dirName)
        {

            if (CommandFindRomsInGame == null)
                CommandFindRomsInGame = new SQLiteCommand(
                    @"SELECT
                    ROM.RomId, ROM.name, ROM.size, ROM.crc, ROM.SHA1, ROM.MD5
                 FROM ROM WHERE ROM.GameId=@GameId and ROM.FileId is null ORDER BY ROM.RomId", Program.db.Connection);
            CommandFindRomsInGame.Parameters.Add(new SQLiteParameter("GameId"));

            Debug.WriteLine(dirName);

            SQLiteCommand getfiles = new SQLiteCommand(@"SELECT dat.datid,dir.FullName,GameId,game.Name FROM dir,dat,game where dat.dirid=dir.dirid and game.datid=dat.datid and dir.fullname like '" + dirName + @"%' 
and (select count(1) from ROM WHERE ROM.FileId is null AND ROM.GameId=Game.GameId)>0 order by dat.datid,GameId", Program.db.Connection);

            DbDataReader reader = getfiles.ExecuteReader();

            int DatId = -1;

            string datFilename = @"D:\fixOut.dat";

            StreamWriter _ts = new StreamWriter(datFilename);

            _ts.WriteLine("<?xml version=\"1.0\"?>");
            _ts.WriteLine(
                "<!DOCTYPE datafile PUBLIC \"-//Logiqx//DTD ROM Management Datafile//EN\" \"http://www.logiqx.com/Dats/datafile.dtd\">");
            _ts.WriteLine("");
            _ts.WriteLine("<datafile>");
            _ts.WriteLine("\t<header>");
            _ts.WriteLine("\t\t<name>fix_" + Etxt(dirName) + "</name>");
            _ts.WriteLine("\t\t<description>fix_" + Etxt(dirName) + "</description>");
            _ts.WriteLine("\t\t<category>FIXDATFILE</category>");
            _ts.WriteLine("\t\t<version>" + DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "</version>");
            _ts.WriteLine("\t\t<date>" + DateTime.Now.ToString("MM/dd/yyyy") + "</date>");
            _ts.WriteLine("\t\t<author>RomVault</author>");
            _ts.WriteLine("\t</header>");

            List<string> matchingcrc=new List<string>();

            while (reader.Read())
            {
                int thisDatId = Convert.ToInt32(reader["datId"]);
                if (thisDatId == DatId)
                {
                    string dirFullName = reader["FullName"].ToString();
                    Debug.WriteLine(dirFullName);
                    DatId = thisDatId;
                }
                string filename = reader["FullName"].ToString();
                filename = filename.Substring(dirName.Length);
                int GameId = Convert.ToInt32(reader["GameId"]);
                string GameName = reader["name"].ToString();
                Debug.WriteLine("Fullname: " + filename + " Game: " + GameId + " Name: " + GameName);

                bool found = false;
                int romCount = 0;
                using (DbDataReader drRom = ZipSetGetRomsInGame(GameId))
                {
                    while (drRom.Read())
                    {
                        int RomId = Convert.ToInt32(drRom["RomId"]);
                        string RomName = drRom["name"].ToString();
                        ulong? size = drRom["size"].Equals(DBNull.Value) ? (ulong?)null : Convert.ToUInt64(drRom["size"]);
                        byte[] CRC = VarFix.CleanMD5SHA1(drRom["crc"].ToString(), 8);
                        byte[] sha1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 40);
                        byte[] md5 = VarFix.CleanMD5SHA1(drRom["md5"].ToString(), 32);

                        string strSize = size != null ? $" size=\"{size}\"" : "";
                        string strCRC = CRC != null ? $" crc=\"{VarFix.ToString(CRC)}\"" : "";
                        string strSHA1 = sha1 != null ? $" sha1=\"{VarFix.ToString(sha1)}\"" : "";
                        string strMD5 = md5 != null ? $" md5=\"{VarFix.ToString(md5)}\"" : "";

                        if (matchingcrc.Contains(strCRC))
                            continue;

                        matchingcrc.Add(strCRC);

                        if (!found)
                        {
                            _ts.WriteLine($"\t<game name=\"{Etxt(filename + GameName)}\">");
                            _ts.WriteLine($"\t\t<description>{Etxt(filename + GameName)}</description>");
                            found = true;
                        }
                        Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  CRC: " + VarFix.ToString(CRC));
                        _ts.WriteLine($"\t\t<rom name=\"{Etxt(RomName)}\"{strSize}{strCRC}{strSHA1}{strMD5} />");

                        romCount += 1;
                    }
                }
                if (found)
                    _ts.WriteLine($"\t</game>");

            }

            _ts.WriteLine($"</datafile>");
            _ts.Flush();
            _ts.Close();
            _ts.Dispose();
        }

        private static DbDataReader ZipSetGetRomsInGame(int GameId)
        {
            CommandFindRomsInGame.Parameters["GameId"].Value = GameId;
            return CommandFindRomsInGame.ExecuteReader();
        }

    }
}
