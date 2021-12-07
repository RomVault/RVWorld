using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using RVXCore.DB;
using RVXCore.Util;

namespace RVXCore
{
    public static class FixDatList
    {

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


        public static void extract(string dirName,string datFilename)
        {
            Debug.WriteLine(dirName);

            SQLiteCommand getfiles = new SQLiteCommand(
                $@"
                    select dir.FullName as FullName,Game.Name as GameName, Rom.Name as RomName,Size,crc,sha1,md5 from DIR
                        inner join DAT on DIR.dirId=DAT.dirId
                        inner join GAME on DAT.DatId=GAME.DatId
                        inner join ROM on GAME.GameId=ROM.GameId
                    where DIR.FullName like '{dirName}%' and FileId is null
                    order by dir.FullName,GAME.Name,ROM.RomId", DBSqlite.db.Connection);

            DbDataReader reader = getfiles.ExecuteReader();
            
            StreamWriter _ts = new StreamWriter(datFilename);

            _ts.WriteLine("<?xml version=\"1.0\"?>");
            _ts.WriteLine("<!DOCTYPE datafile PUBLIC \"-//Logiqx//DTD ROM Management Datafile//EN\" \"http://www.logiqx.com/Dats/datafile.dtd\">");
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

            List<string> matchingcrc = new List<string>();

            string lastname = "";
            while (reader.Read())
            {
                string filename = reader["FullName"].ToString();
                filename = filename.Substring(dirName.Length);
                string GameName = reader["GameName"].ToString();
                
                string RomName = reader["RomName"].ToString();
                ulong? size = reader["size"].Equals(DBNull.Value) ? (ulong?)null : Convert.ToUInt64(reader["size"]);
                byte[] CRC = VarFix.CleanMD5SHA1(reader["crc"].ToString(), 8);
                byte[] sha1 = VarFix.CleanMD5SHA1(reader["sha1"].ToString(), 40);
                byte[] md5 = VarFix.CleanMD5SHA1(reader["md5"].ToString(), 32);

                string strSize = size != null ? $" size=\"{size}\"" : "";
                string strCRC = CRC != null ? $" crc=\"{VarFix.ToString(CRC)}\"" : "";
                string strSHA1 = sha1 != null ? $" sha1=\"{VarFix.ToString(sha1)}\"" : "";
                string strMD5 = md5 != null ? $" md5=\"{VarFix.ToString(md5)}\"" : "";

                
                if (matchingcrc.Contains(strCRC))
                    continue;

                matchingcrc.Add(strCRC);
                

                string thisFilename = filename + GameName;
                if (thisFilename != lastname)
                {
                    if (!string.IsNullOrEmpty(lastname))
                        _ts.WriteLine($"\t</game>");

                    _ts.WriteLine($"\t<game name=\"{Etxt(thisFilename.Replace('\\','-').Replace('/','-'))}\">");
                    _ts.WriteLine($"\t\t<description>{Etxt(thisFilename.Replace('\\', '-').Replace('/', '-'))}</description>");
                    lastname = thisFilename;
                }
                _ts.WriteLine($"\t\t<rom name=\"{Etxt(RomName)}\"{strSize}{strCRC}{strSHA1}{strMD5} />");

            }
            if (!string.IsNullOrEmpty(lastname))
                _ts.WriteLine($"\t</game>");



            _ts.WriteLine($"</datafile>");
            _ts.Flush();
            _ts.Close();
            _ts.Dispose();
        }
    }
}
