using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using Compress;
using Compress.gZip;
using Compress.Support.Utils;
using Compress.ZipFile;
using RVXCore.DB;
using RVXCore.Util;
using FileStream = RVIO.FileStream;

namespace RVXCore
{
    public static class ExtractFiles
    {
        private static SQLiteCommand CommandFindRomsInGame;

        public static void extract(string dirName, string outPath)
        {
            if (CommandFindRomsInGame == null)
                CommandFindRomsInGame = new SQLiteCommand(
                    @"SELECT
                    ROM.RomId, ROM.name, FILES.size, FILES.compressedsize, FILES.crc, FILES.sha1
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId AND ROM.PutInZip ORDER BY ROM.RomId", DBSqlite.db.Connection);
            CommandFindRomsInGame.Parameters.Add(new SQLiteParameter("GameId"));
            
            Debug.WriteLine(dirName);

            SQLiteCommand getfiles = new SQLiteCommand(@"SELECT dir.FullName,GameId,game.Name FROM dir,dat,game where dat.dirid=dir.dirid and game.datid=dat.datid and dir.fullname like '" + dirName + "%'", DBSqlite.db.Connection);

            DbDataReader reader = getfiles.ExecuteReader();

            while (reader.Read())
            {
                string outputFile = reader["fullname"].ToString() + reader["Name"].ToString() + ".zip";
                outputFile = outputFile.Substring(dirName.Length);

                outputFile = Path.Combine(outPath, outputFile).Replace(@"/", @"\");

                Debug.WriteLine(outputFile);

                int GameId = Convert.ToInt32(reader["GameId"]);
                string GameName = reader["name"].ToString();
                Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                Zip memZip = new Zip();
                memZip.ZipCreateFake();

                ulong fileOffset = 0;

                Stream zipFs = null;

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
                        byte[] sha1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 32);

                        Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  Compressed: " + compressedSize + "  CRC: " + VarFix.ToString(CRC));

                        byte[] localHeader;
                        memZip.ZipFileAddFake(RomName, fileOffset, size, compressedSize, CRC, out localHeader);

                        //ZipSetLocalFileHeader(RomId, localHeader, fileOffset);
                        if (romCount == 0)
                        {
                            CompressUtils.CreateDirForFile(outputFile);
                            int errorCode = FileStream.OpenFileWrite(outputFile, out zipFs);
                        }
                        zipFs.Write(localHeader, 0, localHeader.Length);

                        gZip GZip = new gZip();
                        string strFilename = RomRootDir.Getfilename(sha1);
                        GZip.ZipFileOpen(strFilename, -1, true);
                        GZip.ZipFileOpenReadStream(0, true, out Stream oStr, out ulong _);

                        StreamCopier.StreamCopy(oStr,zipFs,compressedSize);

                        GZip.ZipFileCloseReadStream();
                        GZip.ZipFileClose();

                        fileOffset += (ulong)localHeader.Length + compressedSize;
                        zipFs.Position = (long)fileOffset;

                        romCount += 1;
                    }
                }

                memZip.ZipFileCloseFake(fileOffset, out byte[] centeralDir);

                if (romCount > 0)
                {
                    zipFs.Write(centeralDir, 0, centeralDir.Length);
                    zipFs.Flush();
                    zipFs.Close();
                    zipFs.Dispose();
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
