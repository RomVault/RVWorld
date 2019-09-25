using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using FileHeaderReader;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvRom
    {
        private static SQLiteCommand _commandRvRomReader;
        private static SQLiteCommand _commandRvRomWrite;
        public uint RomId;
        public uint GameId;
        public string Name;
        public ulong? Size;
        public HeaderFileType AltType;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public string Merge;
        public string Status;
        public bool PutInZip;
        public ulong? FileId;

        public ulong? FileSize;
        public ulong? FileCompressedSize;
        public byte[] FileCRC;
        public byte[] FileSHA1;
        public byte[] FileMD5;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
               CREATE TABLE IF NOT EXISTS [ROM] (
                    [RomId] INTEGER PRIMARY KEY NOT NULL,
                    [GameId] INTEGER NOT NULL,
                    [name] NVARCHAR(320) NOT NULL,
                    [type] INTEGER NULL,
                    [size] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [merge] VARCHAR(20) NULL,
                    [status] VARCHAR(20) NULL,
                    [putinzip] BOOLEAN,
                    [FileId] INTEGER NULL,
                    [LocalFileHeader] BLOB NULL,
                    [LocalFileHeaderOffset] INTEGER NULL,
                    [LocalFileHeaderLength] INTEGER NULL,
                    [LocalFileSha1] VARCHAR(40) NULL,
                    [LocalFileCompressedSize] INTEGER NULL,
                    FOREIGN KEY(GameId) REFERENCES Game(GameId),
                    FOREIGN KEY(FileId) REFERENCES Files(FileId)
                );");
        }


        public static List<RvRom> ReadRoms(uint gameId)
        {
            if (_commandRvRomReader == null)
            {
                _commandRvRomReader = new SQLiteCommand(
                    @"SELECT RomId,name,
                    type,
                    rom.size,
                    rom.crc,
                    rom.sha1,
                    rom.md5,
                    merge,status,putinzip,
                    rom.FileId,
                    files.size as fileSize,
                    files.compressedsize as fileCompressedSize,
                    files.crc as filecrc,
                    files.sha1 as filesha1,
                    files.md5 as filemd5
                FROM rom LEFT OUTER JOIN files ON files.FileId=rom.FileId WHERE GameId=@GameId ORDER BY RomId", Program.db.Connection);
                _commandRvRomReader.Parameters.Add(new SQLiteParameter("GameId"));
            }


            List<RvRom> roms = new List<RvRom>();
            _commandRvRomReader.Parameters["GameId"].Value = gameId;

            using (DbDataReader dr = _commandRvRomReader.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvRom row = new RvRom
                    {
                        RomId = Convert.ToUInt32(dr["RomId"]),
                        GameId = gameId,
                        Name = dr["name"].ToString(),
                        AltType = VarFix.FixFileType(dr["type"]),
                        Size = VarFix.FixLong(dr["size"]),
                        CRC = VarFix.CleanMD5SHA1(dr["CRC"].ToString(), 8),
                        SHA1 = VarFix.CleanMD5SHA1(dr["SHA1"].ToString(), 40),
                        MD5 = VarFix.CleanMD5SHA1(dr["MD5"].ToString(), 32),
                        Merge = dr["merge"].ToString(),
                        Status = dr["status"].ToString(),
                        PutInZip = (bool) dr["putinzip"],
                        FileId = VarFix.FixLong(dr["FileId"]),
                        FileSize = VarFix.FixLong(dr["fileSize"]),
                        FileCompressedSize = VarFix.FixLong(dr["fileCompressedSize"]),
                        FileCRC = VarFix.CleanMD5SHA1(dr["fileCRC"].ToString(), 8),
                        FileSHA1 = VarFix.CleanMD5SHA1(dr["fileSHA1"].ToString(), 40),
                        FileMD5 = VarFix.CleanMD5SHA1(dr["fileMD5"].ToString(), 32)
                    };

                    roms.Add(row);
                }
                dr.Close();
            }
            return roms;
        }

        public void DBWrite()
        {
            if (_commandRvRomWrite == null)
            {
                _commandRvRomWrite = new SQLiteCommand(@"
                INSERT INTO ROM  ( GameId, name, type, size, crc, sha1, md5, merge, status, putinzip, FileId)
                          VALUES (@GameId,@Name,@Type,@Size,@CRC,@SHA1,@MD5,@Merge,@Status,@PutInZip,@FileId);

                SELECT last_insert_rowid();", Program.db.Connection);

                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("GameId"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("Name"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("Type"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("Size"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("CRC"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("SHA1"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("MD5"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("Merge"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("Status"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("PutInZip"));
                _commandRvRomWrite.Parameters.Add(new SQLiteParameter("FileId"));
            }


            FileId = DatUpdate.NoFilesInDb ? null : RvRomFileMatchup.FindAFile(this);
            _commandRvRomWrite.Parameters["GameId"].Value = GameId;
            _commandRvRomWrite.Parameters["name"].Value = Name;
            _commandRvRomWrite.Parameters["type"].Value = (int) AltType;
            _commandRvRomWrite.Parameters["size"].Value = Size;
            _commandRvRomWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC);
            _commandRvRomWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
            _commandRvRomWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5);
            _commandRvRomWrite.Parameters["merge"].Value = Merge;
            _commandRvRomWrite.Parameters["status"].Value = Status;
            _commandRvRomWrite.Parameters["putinzip"].Value = PutInZip;
            _commandRvRomWrite.Parameters["FileID"].Value = FileId;
            _commandRvRomWrite.ExecuteNonQuery();
        }
    }
}