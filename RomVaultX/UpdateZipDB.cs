using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows.Forms;
using Compress.ZipFile;
using RomVaultX.Util;

namespace RomVaultX
{
    public static class UpdateZipDB
    {
        private static SQLiteCommand CommandWriteLocalHeaderToRom;
        private static SQLiteCommand CommandWriteCentralDirToGame;
        private static SQLiteCommand CommandGetAllGamesWithRoms;
        private static SQLiteCommand CommandFindRomsInGame;

        public static void UpdateDB()
        {
            SetupSQLCommands();

            Program.db.ExecuteNonQuery(@"update game set dirid=(select dirId from DAT where game.Datid=dat.datid) where dirid is null;");

            using (DbDataReader drGame = ZipSetGetAllGames())
            {
                int commitCount = 0;
                Program.db.Begin();

                while (drGame.Read())
                {
                    int GameId = Convert.ToInt32(drGame["GameId"]);
                    string GameName = drGame["name"].ToString();
                    Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                    ZipFile memZip = new ZipFile();
                    memZip.ZipCreateFake();

                    ulong fileOffset = 0;

                    int romCount = 0;
                    using (DbDataReader drRom = ZipSetGetRomsInGame(GameId))
                    {
                        while (drRom.Read())
                        {
                            int RomId = Convert.ToInt32(drRom["RomId"]);
                            string RomName = drRom["name"].ToString();
                            ulong size = Convert.ToUInt64(drRom["size"]);
                            ulong compressedSize = Convert.ToUInt64(drRom["compressedsize"]);
                            byte[] CRC = VarFix.CleanMD5SHA1(drRom["crc"].ToString(), 8);
                            byte[] SHA1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 40);
                            Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  Compressed: " + compressedSize + "  CRC: " + VarFix.ToString(CRC));

                            byte[] localHeader;
                            memZip.ZipFileAddFake(RomName, fileOffset, size, compressedSize, CRC, out localHeader);

                            ZipSetLocalFileHeader(RomId, localHeader, fileOffset, compressedSize, SHA1);

                            fileOffset += (ulong)localHeader.Length + compressedSize;
                            commitCount += 1;
                            romCount += 1;
                        }
                    }

                    byte[] centeralDir;
                    memZip.ZipFileCloseFake(fileOffset, out centeralDir);

                    if (romCount > 0)
                    {
                        ZipSetCentralFileHeader(GameId, fileOffset + (ulong)centeralDir.Length, DateTime.UtcNow.Ticks, centeralDir, fileOffset);
                    }

                    if (commitCount >= 100)
                    {
                        Program.db.Commit();
                        Program.db.Begin();
                        commitCount = 0;
                    }
                }
            }
            Program.db.Commit();

            MessageBox.Show("Zip Header Database Update Complete");
        }

        private static void SetupSQLCommands()
        {
            // just check one as the rest should be the same
            if (CommandGetAllGamesWithRoms != null)
            {
                return;
            }

            CommandGetAllGamesWithRoms = new SQLiteCommand(@"SELECT GameId,name FROM game WHERE RomGot>0 AND ZipFileLength is null", Program.db.Connection);


            CommandFindRomsInGame = new SQLiteCommand(
                @"SELECT
                    ROM.RomId, ROM.name, FILES.size, FILES.compressedsize, FILES.crc,FILES.sha1
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId AND ROM.PutInZip ORDER BY ROM.RomId", Program.db.Connection);
            CommandFindRomsInGame.Parameters.Add(new SQLiteParameter("GameId"));

            CommandWriteLocalHeaderToRom = new SQLiteCommand(
                @"UPDATE ROM SET 
                    LocalFileHeader=@localFileHeader,
                    LocalFileHeaderOffset=@localFileHeaderOffset,
                    LocalFileHeaderLength=@localFileHeaderLength,
                    LocalFileSha1=@localFileSha1,
                    LocalFileCompressedSize=@localFileCompressedSize
                WHERE
                    RomId=@romID", Program.db.Connection);
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeader"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeaderOffset"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeaderLength"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileSha1"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileCompressedSize"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("RomId"));


            CommandWriteCentralDirToGame = new SQLiteCommand(
                @"UPDATE GAME SET 
                    ZipFileLength=@zipFileLength,
                    LastWriteTime=@zipFileTimeStamp,
                    CreationTime=@zipFileTimeStamp,
                    LastAccessTime=@zipFileTimeStamp,
                    CentralDirectory=@centralDirectory,
                    CentralDirectoryOffset=@centralDirectoryOffset,
                    CentralDirectoryLength=@centralDirectoryLength
                WHERE
                    GameId=@gameID", Program.db.Connection);
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("zipFileLength"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("zipFileTimeStamp"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectory"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectoryOffset"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectoryLength"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("GameId"));
        }

        private static DbDataReader ZipSetGetAllGames()
        {
            return CommandGetAllGamesWithRoms.ExecuteReader();
        }

        private static DbDataReader ZipSetGetRomsInGame(int GameId)
        {
            CommandFindRomsInGame.Parameters["GameId"].Value = GameId;
            return CommandFindRomsInGame.ExecuteReader();
        }

        private static void ZipSetLocalFileHeader(int RomId, byte[] localHeader, ulong fileOffset, ulong compressedSize, byte[] sha1)
        {
            CommandWriteLocalHeaderToRom.Parameters["localFileHeader"].Value = localHeader;
            CommandWriteLocalHeaderToRom.Parameters["localFileHeaderOffset"].Value = fileOffset;
            CommandWriteLocalHeaderToRom.Parameters["localFileHeaderLength"].Value = localHeader.Length;
            CommandWriteLocalHeaderToRom.Parameters["localFileSha1"].Value = VarFix.ToString(sha1);
            CommandWriteLocalHeaderToRom.Parameters["localFileCompressedSize"].Value = compressedSize;

            CommandWriteLocalHeaderToRom.Parameters["RomId"].Value = RomId;
            CommandWriteLocalHeaderToRom.ExecuteNonQuery();
        }

        private static void ZipSetCentralFileHeader(int GameId, ulong zipFileLength, long timestamp, byte[] centeralDir, ulong fileOffset)
        {
            CommandWriteCentralDirToGame.Parameters["zipFileLength"].Value = zipFileLength;
            CommandWriteCentralDirToGame.Parameters["zipFileTimeStamp"].Value = timestamp;
            CommandWriteCentralDirToGame.Parameters["centralDirectory"].Value = centeralDir;
            CommandWriteCentralDirToGame.Parameters["centralDirectoryOffset"].Value = fileOffset;
            CommandWriteCentralDirToGame.Parameters["centralDirectoryLength"].Value = centeralDir.Length;
            CommandWriteCentralDirToGame.Parameters["GameId"].Value = GameId;
            CommandWriteCentralDirToGame.ExecuteNonQuery();
        }
    }
}