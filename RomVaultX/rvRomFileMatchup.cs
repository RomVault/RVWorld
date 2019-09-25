using System;
using System.Data.SQLite;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX
{
    public enum FindStatus
    {
        FileUnknown,
        FoundFileInArchive,
        FileNeededInArchive
    }

    public static class RvRomFileMatchup
    {
        #region FileNeededTest

        private static SQLiteCommand _commandFindInFiles;
        private static SQLiteCommand _commandFindInRoMsZero;
        private static SQLiteCommand _commandFindInRoMs;
        private static SQLiteCommand _commandFindInRoMsAlt;

        public static FindStatus FileneededTest(RvFile tFile)
        {
            // first check to see if we already have it in the file table
            bool inFileDB = FindInFiles(tFile); // returns true if found in File table
            if (inFileDB)
            {
                return FindStatus.FoundFileInArchive;
            }

            // now check if needed in any ROMs
            if (FindInROMs(tFile))
            {
                return FindStatus.FileNeededInArchive;
            }

            if (FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType) && FindInROMsAlt(tFile))
            {
                return FindStatus.FileNeededInArchive;
            }

            return FindStatus.FileUnknown;
        }

        public static bool FindInFiles(RvFile tFile)
        {
            if (_commandFindInFiles == null)
            {
                _commandFindInFiles = new SQLiteCommand(@"
                    SELECT COUNT(1) FROM FILES WHERE
                        size=@size AND crc=@CRC and sha1=@SHA1 and md5=@MD5", Program.db.Connection);
                _commandFindInFiles.Parameters.Add(new SQLiteParameter("size"));
                _commandFindInFiles.Parameters.Add(new SQLiteParameter("crc"));
                _commandFindInFiles.Parameters.Add(new SQLiteParameter("sha1"));
                _commandFindInFiles.Parameters.Add(new SQLiteParameter("md5"));
            }

            _commandFindInFiles.Parameters["size"].Value = tFile.Size;
            _commandFindInFiles.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            _commandFindInFiles.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            _commandFindInFiles.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = _commandFindInFiles.ExecuteScalar();
            if ((res == null) || (res == DBNull.Value))
            {
                return false;
            }
            int count = Convert.ToInt32(res);

            return count > 0;
        }

        private static bool FindInROMs(RvFile tFile)
        {
            if ((_commandFindInRoMsZero == null) || (_commandFindInRoMs == null))
            {
                _commandFindInRoMsZero = new SQLiteCommand(@"
                    SELECT count(1) AS TotalFound FROM ROM WHERE
                        ( sha1=@SHA1 OR sha1 is NULL ) AND 
                        ( md5=@MD5 OR md5 is NULL) AND
                        ( crc=@CRC OR crc is NULL ) AND
                        ( size=0 and (status !='nodump' or status is null)) ", Program.db.Connection);
                _commandFindInRoMsZero.Parameters.Add(new SQLiteParameter("crc"));
                _commandFindInRoMsZero.Parameters.Add(new SQLiteParameter("sha1"));
                _commandFindInRoMsZero.Parameters.Add(new SQLiteParameter("md5"));


                _commandFindInRoMs = new SQLiteCommand(@"
                        SELECT
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( sha1=@SHA1 ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL )
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( md5=@MD5 ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL )
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( crc=@CRC ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( size=@size OR size is NULL )
                        ) 
                        AS TotalFound", Program.db.Connection);
                _commandFindInRoMs.Parameters.Add(new SQLiteParameter("size"));
                _commandFindInRoMs.Parameters.Add(new SQLiteParameter("crc"));
                _commandFindInRoMs.Parameters.Add(new SQLiteParameter("sha1"));
                _commandFindInRoMs.Parameters.Add(new SQLiteParameter("md5"));
            }

            if (tFile.Size == 0)
            {
                _commandFindInRoMsZero.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandFindInRoMsZero.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                _commandFindInRoMsZero.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

                object resZero = _commandFindInRoMsZero.ExecuteScalar();

                if ((resZero == null) || (resZero == DBNull.Value))
                {
                    return false;
                }
                int countZero = Convert.ToInt32(resZero);

                return countZero > 0;
            }

            _commandFindInRoMs.Parameters["size"].Value = tFile.Size;
            _commandFindInRoMs.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            _commandFindInRoMs.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            _commandFindInRoMs.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = _commandFindInRoMs.ExecuteScalar();

            if ((res == null) || (res == DBNull.Value))
            {
                return false;
            }
            int count = Convert.ToInt32(res);

            return count > 0;
        }

        private static bool FindInROMsAlt(RvFile tFile)
        {
            if (_commandFindInRoMsAlt == null)
            {
                _commandFindInRoMsAlt = new SQLiteCommand(@"
                        SELECT
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( sha1=@SHA1 ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL )
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( md5=@MD5 ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL )
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( crc=@CRC ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( size=@size OR size is NULL )
                        ) 
                        AS TotalFound", Program.db.Connection);
                _commandFindInRoMsAlt.Parameters.Add(new SQLiteParameter("type"));
                _commandFindInRoMsAlt.Parameters.Add(new SQLiteParameter("size"));
                _commandFindInRoMsAlt.Parameters.Add(new SQLiteParameter("crc"));
                _commandFindInRoMsAlt.Parameters.Add(new SQLiteParameter("sha1"));
                _commandFindInRoMsAlt.Parameters.Add(new SQLiteParameter("md5"));
            }

            _commandFindInRoMsAlt.Parameters["type"].Value = (int)tFile.AltType;
            _commandFindInRoMsAlt.Parameters["size"].Value = tFile.AltSize;
            _commandFindInRoMsAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.AltCRC);
            _commandFindInRoMsAlt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.AltSHA1);
            _commandFindInRoMsAlt.Parameters["md5"].Value = VarFix.ToDBString(tFile.AltMD5);

            object res = _commandFindInRoMsAlt.ExecuteScalar();

            if ((res == null) || (res == DBNull.Value))
            {
                return false;
            }
            int count = Convert.ToInt32(res);

            return count > 0;
        }

        #endregion

        #region MatchFileToRoms

        private static SQLiteCommand _commandRvFileUpdateRom;
        private static SQLiteCommand _commandRvFileUpdateRomAlt;
        private static SQLiteCommand _commandRvFileUpdateZeroRom;

        public static void MatchFiletoRoms(RvFile file)
        {
            if (file.Size != 0)
            {
                FileUpdateRom(file);

                if (FileHeaderReader.FileHeaderReader.AltHeaderFile(file.AltType))
                {
                    FileUpdateRomAlt(file);
                }
            }
            else
            {
                FileUpdateZeroRom(file);
            }
        }

        private static void FileUpdateRom(RvFile file)
        {
            if (_commandRvFileUpdateRom == null)
            {
                _commandRvFileUpdateRom = new SQLiteCommand(
                    @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
                        FileId IS NULL;
                ", Program.db.Connection);
                _commandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("FileId"));
                _commandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("size"));
                _commandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("crc"));
                _commandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("sha1"));
                _commandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("md5"));
            }

            _commandRvFileUpdateRom.Parameters["FileId"].Value = file.FileId;
            _commandRvFileUpdateRom.Parameters["size"].Value = file.Size;
            _commandRvFileUpdateRom.Parameters["crc"].Value = VarFix.ToDBString(file.CRC);
            _commandRvFileUpdateRom.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1);
            _commandRvFileUpdateRom.Parameters["md5"].Value = VarFix.ToDBString(file.MD5);
            _commandRvFileUpdateRom.ExecuteNonQuery();
        }

        private static void FileUpdateRomAlt(RvFile file)
        {
            if (_commandRvFileUpdateRomAlt == null)
            {
                _commandRvFileUpdateRomAlt = new SQLiteCommand(
                    @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
                        FileId IS NULL;
                ", Program.db.Connection);
                _commandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("FileId"));
                _commandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("type"));
                _commandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("size"));
                _commandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("crc"));
                _commandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("sha1"));
                _commandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("md5"));
            }

            _commandRvFileUpdateRomAlt.Parameters["FileId"].Value = file.FileId;
            _commandRvFileUpdateRomAlt.Parameters["type"].Value = file.AltType;
            _commandRvFileUpdateRomAlt.Parameters["size"].Value = file.AltSize;
            _commandRvFileUpdateRomAlt.Parameters["crc"].Value = VarFix.ToDBString(file.AltCRC);
            _commandRvFileUpdateRomAlt.Parameters["sha1"].Value = VarFix.ToDBString(file.AltSHA1);
            _commandRvFileUpdateRomAlt.Parameters["md5"].Value = VarFix.ToDBString(file.AltMD5);
            _commandRvFileUpdateRomAlt.ExecuteNonQuery();
        }

        private static void FileUpdateZeroRom(RvFile file)
        {
            if (_commandRvFileUpdateZeroRom == null)
            {
                _commandRvFileUpdateZeroRom = new SQLiteCommand(
                    @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    ( Size=0 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
                        FileId IS NULL;
                ", Program.db.Connection);
                _commandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("FileId"));
                _commandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("crc"));
                _commandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("sha1"));
                _commandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("md5"));
            }

            _commandRvFileUpdateZeroRom.Parameters["FileId"].Value = file.FileId;
            _commandRvFileUpdateZeroRom.Parameters["crc"].Value = VarFix.ToDBString(file.CRC);
            _commandRvFileUpdateZeroRom.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1);
            _commandRvFileUpdateZeroRom.Parameters["md5"].Value = VarFix.ToDBString(file.MD5);
            _commandRvFileUpdateZeroRom.ExecuteNonQuery();
        }

        #endregion

        #region MatchRomToaFile

        private static SQLiteCommand _commandSHA1;
        private static SQLiteCommand _commandMD5;
        private static SQLiteCommand _commandCRC;
        private static SQLiteCommand _commandSize;

        private static SQLiteCommand _commandSHA1Alt;
        private static SQLiteCommand _commandMD5Alt;
        private static SQLiteCommand _commandCRCAlt;

        public static uint? FindAFile(RvRom tFile)
        {
            if (_commandSHA1 == null)
            {
                _commandSHA1 = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
	                                    (                  @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", Program.db.Connection);

                _commandSHA1.Parameters.Add(new SQLiteParameter("sha1"));
                _commandSHA1.Parameters.Add(new SQLiteParameter("md5"));
                _commandSHA1.Parameters.Add(new SQLiteParameter("crc"));
                _commandSHA1.Parameters.Add(new SQLiteParameter("size"));


                _commandSHA1Alt = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @sha1 = altsha1 ) AND
	                                    ( @md5  is NULL OR @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ", Program.db.Connection);

                _commandSHA1Alt.Parameters.Add(new SQLiteParameter("alttype"));
                _commandSHA1Alt.Parameters.Add(new SQLiteParameter("sha1"));
                _commandSHA1Alt.Parameters.Add(new SQLiteParameter("md5"));
                _commandSHA1Alt.Parameters.Add(new SQLiteParameter("crc"));
                _commandSHA1Alt.Parameters.Add(new SQLiteParameter("size"));


                _commandMD5 = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
	                                    (                  @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", Program.db.Connection);

                _commandMD5.Parameters.Add(new SQLiteParameter("md5"));
                _commandMD5.Parameters.Add(new SQLiteParameter("crc"));
                _commandMD5.Parameters.Add(new SQLiteParameter("size"));

                _commandMD5Alt = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ", Program.db.Connection);

                _commandMD5Alt.Parameters.Add(new SQLiteParameter("alttype"));
                _commandMD5Alt.Parameters.Add(new SQLiteParameter("md5"));
                _commandMD5Alt.Parameters.Add(new SQLiteParameter("crc"));
                _commandMD5Alt.Parameters.Add(new SQLiteParameter("size"));

                _commandCRC = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
	                                    (                  @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", Program.db.Connection);

                _commandCRC.Parameters.Add(new SQLiteParameter("crc"));
                _commandCRC.Parameters.Add(new SQLiteParameter("size"));

                _commandCRCAlt = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ", Program.db.Connection);

                _commandCRCAlt.Parameters.Add(new SQLiteParameter("alttype"));
                _commandCRCAlt.Parameters.Add(new SQLiteParameter("crc"));
                _commandCRCAlt.Parameters.Add(new SQLiteParameter("size"));


                _commandSize = new SQLiteCommand(@"
                       select FileId from FILES
                            WHERE
	                                    ( @size = Size )
                            limit 1
                ", Program.db.Connection);

                _commandSize.Parameters.Add(new SQLiteParameter("size"));
            }


            if (tFile.SHA1 != null)
            {
                _commandSHA1.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                _commandSHA1.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                _commandSHA1.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandSHA1.Parameters["size"].Value = tFile.Size;

                object res = _commandSHA1.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }

                if (!FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType))
                {
                    return null;
                }

                _commandSHA1Alt.Parameters["alttype"].Value = (int)tFile.AltType;
                _commandSHA1Alt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                _commandSHA1Alt.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                _commandSHA1Alt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandSHA1Alt.Parameters["size"].Value = tFile.Size;

                res = _commandSHA1Alt.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }

                return null;
            }

            if (tFile.MD5 != null)
            {
                _commandMD5.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                _commandMD5.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandMD5.Parameters["size"].Value = tFile.Size;

                object res = _commandMD5.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }

                if (!FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType))
                {
                    return null;
                }

                _commandMD5Alt.Parameters["alttype"].Value = (int)tFile.AltType;
                _commandMD5Alt.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                _commandMD5Alt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandMD5Alt.Parameters["size"].Value = tFile.Size;

                res = _commandMD5Alt.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }

                return null;
            }

            if (tFile.CRC != null)
            {
                _commandCRC.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandCRC.Parameters["size"].Value = tFile.Size;

                object res = _commandCRC.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }

                if (!FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType))
                {
                    return null;
                }

                _commandCRCAlt.Parameters["alttype"].Value = (int)tFile.AltType;
                _commandCRCAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                _commandCRCAlt.Parameters["size"].Value = tFile.Size;

                res = _commandCRCAlt.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }

                return null;
            }


            if (tFile.Size != null && tFile.Size == 0)
            {
                _commandSize.Parameters["size"].Value = tFile.Size;

                object res = _commandSize.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    return (uint?)Convert.ToInt32(res);
                }
            }

            return null;
        }

        #endregion
    }
}