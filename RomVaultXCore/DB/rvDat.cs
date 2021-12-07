using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using RVXCore.Util;

namespace RVXCore.DB
{
    public class RvDat
    {
        private static SQLiteCommand _commandRvDatWrite;
        private static SQLiteCommand _commandRvDatRead;
        public uint DatId;
        public uint DirId;

        public string Filename;
        public string Name;
        public string RootDir;
        public string Description;
        public string Category;
        public string Version;
        public string Date;
        public string Author;
        public string Email;
        public string Homepage;
        public string URL;
        public string Comment;
        public string MergeType;

        public bool ExtraDir;
        public string Path;
        public long DatTimeStamp;
        
        public List<RvGame> Games;


        public static void CreateTable()
        {
             DBSqlite.db.ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [DAT] (
                    [DatId] INTEGER  PRIMARY KEY NOT NULL,
                    [DirId] INTEGER  NOT NULL,
                    [Filename] NVARCHAR(300)  NULL,

                    [name] NVARCHAR(100)  NULL,
                    [rootdir] NVARCHAR(10)  NULL,
                    [description] NVARCHAR(10)  NULL,
                    [category] NVARCHAR(10)  NULL,
                    [version] NVARCHAR(10)  NULL,
                    [date] NVARCHAR(10)  NULL,
                    [author] NVARCHAR(10)  NULL,
                    [email] NVARCHAR(10)  NULL,
                    [homepage] NVARCHAR(10)  NULL,
                    [url] NVARCHAR(10)  NULL,
                    [comment] NVARCHAR(10) NULL,
                    [mergetype] NVARCHAR(10) NULL,
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [Path] NVARCHAR(10)  NOT NULL,
                    [DatTimeStamp] NVARCHAR(20)  NOT NULL,
                    [ExtraDir] BOOLEAN DEFAULT 0,
                    [found] BOOLEAN DEFAULT 1,            
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");
        }

        public void DbRead(uint datId, bool readGames = false)
        {
            if (_commandRvDatRead == null)
            {
                _commandRvDatRead = new SQLiteCommand(@"
                SELECT DirId,Filename,name,rootdir,description,category,version,date,author,email,homepage,url,comment,mergetype
                FROM DAT WHERE DatId=@datId ORDER BY Filename",  DBSqlite.db.Connection);
                _commandRvDatRead.Parameters.Add(new SQLiteParameter("datId"));
            }


            _commandRvDatRead.Parameters["DatID"].Value = datId;

            using (DbDataReader dr = _commandRvDatRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    DatId = datId;
                    DirId = Convert.ToUInt32(dr["DirId"]);
                    Filename = dr["filename"].ToString();
                    Name = dr["name"].ToString();
                    RootDir = dr["rootdir"].ToString();
                    Description = dr["description"].ToString();
                    Category = dr["category"].ToString();
                    Version = dr["version"].ToString();
                    Date = dr["date"].ToString();
                    Author = dr["author"].ToString();
                    Email = dr["email"].ToString();
                    Homepage = dr["homepage"].ToString();
                    URL = dr["url"].ToString();
                    Comment = dr["comment"].ToString();
                    MergeType = dr["mergetype"].ToString();
                }
                dr.Close();
            }

            if (readGames)
            {
                Games = RvGame.ReadGames(DatId, true);
            }
        }

        public void DbWrite()
        {
            if (_commandRvDatWrite == null)
            {
                _commandRvDatWrite = new SQLiteCommand(@"
                INSERT INTO DAT ( DirId, Filename, name, rootdir, description, category, version, date, author, email, homepage, url, comment, MergeType, Path, DatTimeStamp,ExtraDir)
                VALUES          (@DirId,@Filename,@name,@rootdir,@description,@category,@version,@date,@author,@email,@homepage,@url,@comment,@MergeType,@Path, @DatTimeStamp,@ExtraDir);

                SELECT last_insert_rowid();",  DBSqlite.db.Connection);

                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("DirId"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("Filename"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("name"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("rootdir"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("description"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("category"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("version"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("date"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("author"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("email"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("homepage"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("url"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("comment"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("mergetype"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("Path"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("DatTimeStamp"));
                _commandRvDatWrite.Parameters.Add(new SQLiteParameter("ExtraDir"));
            }

            _commandRvDatWrite.Parameters["DirId"].Value = DirId;
            _commandRvDatWrite.Parameters["Filename"].Value = Filename;
            _commandRvDatWrite.Parameters["name"].Value = Name;
            _commandRvDatWrite.Parameters["rootdir"].Value = RootDir;
            _commandRvDatWrite.Parameters["description"].Value = Description;
            _commandRvDatWrite.Parameters["category"].Value = Category;
            _commandRvDatWrite.Parameters["version"].Value = Version;
            _commandRvDatWrite.Parameters["date"].Value = Date;
            _commandRvDatWrite.Parameters["author"].Value = Author;
            _commandRvDatWrite.Parameters["email"].Value = Email;
            _commandRvDatWrite.Parameters["homepage"].Value = Homepage;
            _commandRvDatWrite.Parameters["url"].Value = URL;
            _commandRvDatWrite.Parameters["comment"].Value = Comment;
            _commandRvDatWrite.Parameters["mergetype"].Value = MergeType;
            _commandRvDatWrite.Parameters["path"].Value = Path;
            _commandRvDatWrite.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();
            _commandRvDatWrite.Parameters["ExtraDir"].Value = ExtraDir;
            object res = _commandRvDatWrite.ExecuteScalar();

            if ((res == null) || (res == DBNull.Value))
            {
                return;
            }
            DatId = Convert.ToUInt32(res);

            if (Games == null)
            {
                return;
            }

            foreach (RvGame rvGame in Games)
            {
                rvGame.DatId = DatId;
                rvGame.DBWrite();
            }
        }

        public void AddGame(RvGame rvGame)
        {
            if (Games == null)
            {
                Games = new List<RvGame>();
            }

            int index;
            ChildNameSearch(rvGame.Name, out index);
            Games.Insert(index, rvGame);
        }

        public string GetExtraDirName()
        {
            if (!string.IsNullOrWhiteSpace(Description))
            {
                return Description;
            }
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name;
            }
            return "-unknown-";
        }


        public int ChildNameSearch(string lGameName, out int index)
        {
            int intBottom = 0;
            int intTop = Games.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop)/2;

                intRes = VarFix.CompareName(lGameName, Games[intMid].Name);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while ((index > 0) && (intRes1 == 0))
                {
                    intRes1 = VarFix.CompareName(lGameName, Games[index - 1].Name);
                    if (intRes1 == 0)
                    {
                        index--;
                    }
                }
            }
            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
            {
                index++;
            }

            return intRes;
        }
    }
}