using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Compress.gZip;
using DokanNet;
using RVXCore.DB;
using RVXCore.Util;

namespace RVXCore
{
    public class VFile
    {
        public string FileName;
        public long Length;
        private int _fileId;
        public bool IsDirectory;
        private int _fileSplitIndex = -1;
        private DateTime _creationTime;
        private DateTime _lastAccessTime;
        private DateTime _lastWriteTime;

        public List<VZipFile> Files;

        public static explicit operator FileInformation(VFile v)
        {
            FileInformation fi = new FileInformation
            {
                FileName = v.FileName,
                Length = v.Length,
                Attributes = v.IsDirectory ? FileAttributes.Directory | FileAttributes.ReadOnly : FileAttributes.Normal | FileAttributes.ReadOnly,
                CreationTime = v._creationTime,
                LastAccessTime = v._lastAccessTime,
                LastWriteTime = v._lastWriteTime
            };
            return fi;
        }

        /*
        private static string path = @"D:\tmp";
        private static string GetPath(string searchFilename)
        {
            return path + searchFilename;
        }
        */

        /*
        Dokan Filename format:
        \DatRoot
        \DatRoot\Rom
        \DatRoot\Rom\file.zip

        DB DIR -> fullname
        DatRoot\
        DatRoot\Rom\

        */

        // using the supplied filename, try and find and return the information (vFile) about this testFilename
        // this may be a file or a directory, so we need to also figure that out.
        public static VFile FindFilename(string filename)
        {
            Debug.WriteLine("Trying to find information about  " + filename);

            // 1) test for the root direction
            VFile retVal = FindRoot(filename);
            if (retVal != null)
            {
                return retVal;
            }

            // 2) test for a regular DB Directory
            retVal = FindInDBDir(filename);
            if (retVal != null)
            {
                return retVal;
            }

            // 3) test for a Dat Entry
            retVal = FindInDBDat(filename);
            if (retVal != null)
            {
                return retVal;
            }

            // Failed to file this filename
            return null;
        }

        private static VFile FindRoot(string filename)
        {
            if (filename != @"\")
            {
                return null;
            }

            VFile vfile = new VFile
            {
                FileName = filename,
                IsDirectory = true,
                _fileId = 0,
                _creationTime = DateTime.Today,
                _lastWriteTime = DateTime.Today,
                _lastAccessTime = DateTime.Today
            };
            return vfile;
        }


        private static VFile FindInDBDir(string filename)
        {
            // try and find this directory in the DIR table
            string testName = filename.Substring(1) + @"\"; // takes the slash of the front of the string and add one on the end
            Debug.WriteLine("Looking in DIR from  " + testName);
            using (DbCommand getDirectoryId =  DBSqlite.db.Command(@"
                                    SELECT 
                                        DirId,
                                        CreationTime,
                                        LastAccessTime,
                                        LastWriteTime
                                    FROM
                                        Dir 
                                    WHERE 
                                        fullname=@FName"))
            {
                DbParameter pFName =  DBSqlite.db.Parameter("FName", testName);
                getDirectoryId.Parameters.Add(pFName);

                using (DbDataReader reader = getDirectoryId.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }
                    VFile vDir = new VFile
                    {
                        FileName = filename,
                        IsDirectory = true,
                        _fileId = Convert.ToInt32(reader["DirId"]),
                        _creationTime = new DateTime(Convert.ToInt64(reader["CreationTime"])),
                        _lastAccessTime = new DateTime(Convert.ToInt64(reader["LastAccessTime"])),
                        _lastWriteTime = new DateTime(Convert.ToInt64(reader["LastWriteTime"]))
                    };
                    return vDir;
                }
            }
        }

        private static VFile FindInDBDat(string filename)
        {
            int filenameLength = filename.Length;
            // we only search in the DB for .zip files so test for that extension
            bool isFile = (filenameLength > 4) && (filename.Substring(filenameLength - 4).ToLower() == ".zip");

            string testFilename = filename;
            if (isFile)
            {
                // if is File remove the .zip file extension
                testFilename = testFilename.Substring(0, filenameLength - 4);
            }

            string dirName = testFilename;
            while (true)
            {
                int slashPos = dirName.LastIndexOf(@"\", StringComparison.Ordinal);
                if (slashPos <= 0)
                {
                    return null;
                }
                dirName = testFilename.Substring(0, slashPos);
                int? dirId = DirFind(dirName);
                if (dirId == null)
                {
                    continue; // loop to next slash
                }

                string filePart = testFilename.Substring(slashPos + 1);
                if (isFile)
                {
                    VFile vFile = DBGameFindFile((int)dirId, filePart, filename);
                    if (vFile != null)
                    {
                        vFile._fileSplitIndex = slashPos;
                        return vFile;
                    }
                }
                else
                {
                    VFile vFile = DBGameFindDir((int)dirId, filePart, filename);
                    if (vFile != null)
                    {
                        vFile._fileSplitIndex = slashPos;
                        return vFile;
                    }
                }
                return null;
            }
        }


        private static int? DirFind(string dirName)
        {
            if (string.IsNullOrEmpty(dirName))
            {
                return null;
            }

            string testName = dirName.Substring(1) + @"\";
            using (DbCommand getDirectoryId =  DBSqlite.db.Command(@"
                    SELECT 
                        DirId 
                    FROM
                        Dir 
                    WHERE 
                        Fullname=@FName"))
            {
                DbParameter pFName =  DBSqlite.db.Parameter("FName", testName);
                getDirectoryId.Parameters.Add(pFName);

                object ret = getDirectoryId.ExecuteScalar();
                if ((ret == null) || (ret == DBNull.Value))
                {
                    return null;
                }
                return Convert.ToInt32(ret);
            }
        }

        private static VFile DBGameFindFile(int dirId, string searchFilename, string realFilename)
        {
            using (DbCommand getFileInDirectory =  DBSqlite.db.Command(@"
                            SELECT 
                                GameId, 
                                ZipFileLength,
                                LastWriteTime,
                                CreationTime,
                                LastAccessTime 
                            FROM
                                Game 
                            WHERE 
                                Dirid = @dirId AND
                                ZipFileLength > 0 AND
                                name = @name
                                "))
            {
                DbParameter pDirId =  DBSqlite.db.Parameter("DirId", dirId);
                getFileInDirectory.Parameters.Add(pDirId);
                DbParameter pName =  DBSqlite.db.Parameter("Name", searchFilename.Replace(@"\", @"/"));
                getFileInDirectory.Parameters.Add(pName);
                using (DbDataReader dr = getFileInDirectory.ExecuteReader())
                {
                    if (!dr.Read())
                    {
                        return null;
                    }
                    VFile vFile = new VFile
                    {
                        IsDirectory = false,
                        _fileId = Convert.ToInt32(dr["GameId"]),
                        FileName = realFilename,
                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"])),
                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"]))
                    };
                    return vFile;
                }
            }
        }

        private static VFile DBGameFindDir(int dirId, string searchDirectoryName, string realFilename)
        {
            using (DbCommand getFileInDirectory =  DBSqlite.db.Command(@"
                            SELECT 
                                GameId, 
                                ZipFileLength,
                                LastWriteTime,
                                CreationTime,
                                LastAccessTime 
                            FROM
                                Game 
                            WHERE 
                                Dirid = @dirId AND
                                ZipFileLength > 0 AND 
                                name Like @name
                            LIMIT 1"))
            {
                DbParameter pDirId =  DBSqlite.db.Parameter("DirId", dirId);
                getFileInDirectory.Parameters.Add(pDirId);
                DbParameter pName =  DBSqlite.db.Parameter("Name", searchDirectoryName.Replace(@"\", @"/") + @"/%");
                getFileInDirectory.Parameters.Add(pName);
                using (DbDataReader dr = getFileInDirectory.ExecuteReader())
                {
                    if (!dr.Read())
                    {
                        return null;
                    }
                    VFile vFile = new VFile
                    {
                        IsDirectory = true,
                        _fileId = dirId, // we are storing the id of the DIR not the GameId (So we can use the dirId later)
                        FileName = realFilename,
                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"])),
                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"]))
                    };
                    return vFile;
                }
            }
        }

        public static List<VFile> DirGetSubItems(VFile vDir)
        {
            int dirId = vDir._fileId;
            List<VFile> dirs = new List<VFile>();

            if (!vDir.IsDirectory)
            {
                return dirs;
            }

            if (vDir._fileSplitIndex == -1)
            {
                // we are not inside a DAT directory structure

                // find any child DIR's from this DIR level
                using (DbCommand getDirectory =  DBSqlite.db.Command(@"
                    SELECT 
                        DirId,
                        Name,
                        CreationTime,
                        LastAccessTime,
                        LastWriteTime 
                    FROM
                        Dir
                    WHERE 
                        ParentDirId=@DirId"))
                {
                    DbParameter pParentDirId =  DBSqlite.db.Parameter("DirId", dirId);
                    getDirectory.Parameters.Add(pParentDirId);
                    using (DbDataReader dr = getDirectory.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string filename = (string)dr["name"];
                            bool found = dirs.Any(t => t.FileName == filename);
                            if (!found)
                            {
                                dirs.Add(
                                    new VFile
                                    {
                                        IsDirectory = true,
                                        _fileId = Convert.ToInt32(dr["DirId"]),
                                        FileName = filename,
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    }
                                );
                            }
                        }
                    }
                }

                // find any DB items from top filename level
                using (DbCommand getFilesInDirectory =  DBSqlite.db.Command(@"
                        SELECT 
                            GameId, 
                            Name,
                            ZipFileLength,
                            LastWriteTime,
                            CreationTime,
                            LastAccessTime
                        FROM 
                            Game 
                        WHERE 
                            DirId=@dirId AND
                            ZipFileLength>0 
                            "))
                {
                    DbParameter pDirId =  DBSqlite.db.Parameter("DirId", vDir._fileId);
                    getFilesInDirectory.Parameters.Add(pDirId);
                    using (DbDataReader dr = getFilesInDirectory.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string filename = (string)dr["name"];
                            // test if filename is a directory
                            int filenameSplit = filename.IndexOf(@"/", StringComparison.Ordinal);
                            if (filenameSplit >= 0)
                            {
                                string dirFilename = filename.Substring(0, filenameSplit);
                                bool found = dirs.Any(t => t.FileName == dirFilename);
                                if (!found)
                                {
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = true,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = dirFilename,
                                        _fileSplitIndex = filenameSplit,
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                                }
                            }
                            else
                            {
                                string zipFilename = filename + ".zip";
                                bool found = dirs.Any(t => t.FileName == zipFilename);
                                if (!found)
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = false,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = zipFilename,
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                            }
                        }
                    }
                }
            }
            else
            {
                // we are in a DAT with sub directories

                string datfilePart = vDir.FileName.Substring(1 + vDir._fileSplitIndex).Replace(@"\", @"/") + @"/";
                int datfilePartLength = datfilePart.Length;
                // find any DB items from top filename level
                using (DbCommand getFilesInDirectory =  DBSqlite.db.Command(@"
                        SELECT 
                            GameId, 
                            Name,
                            ZipFileLength,
                            LastWriteTime,
                            CreationTime,
                            LastAccessTime
                        FROM 
                            Game 
                        WHERE 
                            DirId=@dirId AND
                            ZipFileLength>0 AND 
                            Name LIKE @dirName
                            "))
                {
                    DbParameter pDirName =  DBSqlite.db.Parameter("DirName", datfilePart + "%");
                    getFilesInDirectory.Parameters.Add(pDirName);

                    DbParameter pDirId =  DBSqlite.db.Parameter("DirId", vDir._fileId);
                    getFilesInDirectory.Parameters.Add(pDirId);
                    using (DbDataReader dr = getFilesInDirectory.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string filename = (string)dr["name"];
                            filename = filename.Substring(datfilePartLength);
                            int filenameSplit = filename.IndexOf(@"/", StringComparison.Ordinal);
                            if (filenameSplit >= 0)
                            {
                                string dirFilename = filename.Substring(0, filenameSplit);
                                bool found = dirs.Any(t => t.FileName == dirFilename);
                                if (!found)
                                {
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = true,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = dirFilename,
                                        _fileSplitIndex = vDir._fileSplitIndex + filenameSplit, // check this is correct
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                                }
                            }
                            else
                            {
                                string zipFilename = filename + ".zip";
                                bool found = dirs.Any(t => t.FileName == zipFilename);
                                if (!found)
                                {
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = false,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = zipFilename,
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return dirs;
        }



        /*
        private static VFile FindInRealRoot(string filename)
        {
            string fullPath = "RealRoot" + filename;
            DirectoryInfo di = new DirectoryInfo(fullPath);
            if (di.Exists)
            {
                VFile vfile = new VFile
                {
                    FileName = filename,
                    IsDirectory = true,
                    _creationTime = di.CreationTime,
                    _lastWriteTime = di.LastWriteTime,
                    _lastAccessTime = di.LastAccessTime,
                    _IsRealFile = true
                };
                return vfile;
            }

            FileInfo fi = new FileInfo(fullPath);
            if (fi.Exists)
            {
                VFile vfile = new VFile
                {
                    FileName = filename,
                    Length = fi.Length,
                    _creationTime = fi.CreationTime,
                    _lastWriteTime = fi.LastWriteTime,
                    _lastAccessTime = fi.LastAccessTime,
                    _IsRealFile = true
                };
                return vfile;
            }

            return null;
        }
        */


        public bool LoadVFileZipData() // used to get ready to load an actual ZIP file
        {
            Files = new List<VZipFile>();

            using (DbCommand getRoms =  DBSqlite.db.Command(
                @"SELECT
                    LocalFileSha1,
                    LocalFileCompressedSize,
                    LocalFileHeader,
                    LocalFileHeaderOffset,
                    LocalFileHeaderLength
                 FROM 
                    ROM
                 WHERE 
                    ROM.GameId=@GameId AND
                    LocalFileHeaderLength > 0
                 ORDER BY 
                    Rom.RomId"))
            {
                DbParameter pGameId =  DBSqlite.db.Parameter("GameId", _fileId);
                getRoms.Parameters.Add(pGameId);
                using (DbDataReader dr = getRoms.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        VZipFile gf = new VZipFile
                        {
                            LocalHeaderOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]),
                            LocalHeaderLength = Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            LocalHeader = (byte[])dr["LocalFileHeader"],
                            GZipSha1 = VarFix.CleanMD5SHA1(dr["LocalFileSha1"].ToString(), 20),
                            CompressedDataOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]) + Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            CompressedDataLength = Convert.ToInt64(dr["LocalFileCompressedsize"]),
                            GZip = null // opened as needed
                        };
                        Files.Add(gf);
                    }
                }
            }


            // the central directory is now added on to the end of the file list, like is another file with zero bytes of compressed data.
            using (DbCommand getCentralDir =  DBSqlite.db.Command(
                @"select 
                    CentralDirectory, 
                    CentralDirectoryOffset, 
                    CentralDirectoryLength 
                 from game where GameId=@gameId"))
            {
                DbParameter pGameId =  DBSqlite.db.Parameter("GameId", _fileId);
                getCentralDir.Parameters.Add(pGameId);
                using (DbDataReader dr = getCentralDir.ExecuteReader())
                {
                    if (!dr.Read())
                    {
                        return false;
                    }

                    VZipFile gf = new VZipFile
                    {
                        LocalHeaderOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]),
                        LocalHeaderLength = Convert.ToInt64(dr["CentralDirectoryLength"]),
                        LocalHeader = (byte[])dr["CentralDirectory"],
                        GZipSha1 = null,
                        CompressedDataOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]) + Convert.ToInt64(dr["CentralDirectoryLength"]),
                        CompressedDataLength = 0,
                        GZip = null // not used
                    };
                    Files.Add(gf);
                }
            }


            return true;
        }


        public class VZipFile
        {
            public long LocalHeaderOffset;
            public long LocalHeaderLength;
            public byte[] LocalHeader;

            public byte[] GZipSha1;
            public long CompressedDataOffset;
            public long CompressedDataLength;

            public gZip GZip;
        }
    }
}