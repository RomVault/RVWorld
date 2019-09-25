using System;
using System.Data.SQLite;

namespace RomVaultX.DB
{
    public class RvDir
    {
        private static SQLiteCommand CommandFindInDir;
        private static SQLiteCommand CommandSetDirFound;
        private static SQLiteCommand CommandInsertIntoDir;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS [DIR] (
                    [DirId] INTEGER PRIMARY KEY NOT NULL,
                    [ParentDirId] INTEGER NULL,
                    [name] NVARCHAR(300) NOT NULL,
                    [fullname] NVARCHAR(300) NOT NULL,
                    [expanded] BOOLEAN DEFAULT 1 NOT NULL,
                    [found] BOOLEAN DEFAULT 1,
                    [CreationTime] INTEGER,
                    [LastAccessTime] INTEGER,
                    [LastWriteTime] INTEGER,
                    [RomTotal] INTEGER NULL,
                    [RomGot] INTEGER NULL,
                    [RomNoDump] INTEGER NULL
                );");
        }


        public static uint FindOrInsertIntoDir(uint parentDirId, string name, string fullName)
        {
            uint? foundDatId = FindInDir(fullName);
            if (foundDatId == null)
            {
                return InsertIntoDir(parentDirId, name, fullName);
            }

            SetDirFound((uint) foundDatId);
            return (uint) foundDatId;
        }


        private static uint? FindInDir(string fullname)
        {
            if (CommandFindInDir == null)
            {
                CommandFindInDir = new SQLiteCommand(@"SELECT DirId FROM dir WHERE fullname=@fullname LIMIT 1", Program.db.Connection);
                CommandFindInDir.Parameters.Add(new SQLiteParameter("fullname"));
            }

            CommandFindInDir.Parameters["FullName"].Value = fullname;
            object resFind = CommandFindInDir.ExecuteScalar();
            if ((resFind == null) || (resFind == DBNull.Value))
            {
                return null;
            }
            return (uint?) Convert.ToInt32(resFind);
        }


        private static void SetDirFound(uint foundDatId)
        {
            if (CommandSetDirFound == null)
            {
                CommandSetDirFound = new SQLiteCommand(@"Update Dir SET Found=1 WHERE DirId=@DirId", Program.db.Connection);
                CommandSetDirFound.Parameters.Add(new SQLiteParameter("DirId"));
            }

            CommandSetDirFound.Parameters["DirId"].Value = foundDatId;
            CommandSetDirFound.ExecuteNonQuery();
        }

        private static uint InsertIntoDir(uint parentDirId, string name, string fullName)
        {
            if (CommandInsertIntoDir == null)
            {
                CommandInsertIntoDir = new SQLiteCommand(@"
                    INSERT INTO DIR (ParentDirId,Name,FullName,CreationTime,LastAccessTime,LastWriteTime)
                         VALUES (@ParentDirId,@Name,@FullName,@TimeStamp,@TimeStamp,@TimeStamp);

                         SELECT last_insert_rowid();
                    ", Program.db.Connection);

                CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("ParentDirId"));
                CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("Name"));
                CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("FullName"));
                CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("TimeStamp"));
            }

            CommandInsertIntoDir.Parameters["ParentDirId"].Value = parentDirId;
            CommandInsertIntoDir.Parameters["Name"].Value = name;
            CommandInsertIntoDir.Parameters["FullName"].Value = fullName;
            CommandInsertIntoDir.Parameters["TimeStamp"].Value = DateTime.Now.Ticks;

            object res = CommandInsertIntoDir.ExecuteScalar();

            if ((res == null) || (res == DBNull.Value))
            {
                return 0;
            }
            return Convert.ToUInt32(res);
        }
    }
}