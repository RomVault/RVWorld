using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvGame
    {
        private static SQLiteCommand _commandRvGameWrite;
        private static SQLiteCommand _commandRvGameRead;
        private static SQLiteCommand _commandRvGameReadDatGames;
        public uint GameId;
        public uint DatId;
        public string Name;
        public string Description;

        public string Manufacturer;
        public string CloneOf;
        public string RomOf;
        public string SampleOf;
        public string SourceFile;
        public string IsBios;
        public string Board;
        public string Year;

        public bool IsTrurip;
        public string Publisher;
        public string Developer;
        public string Edition;
        public string Version;
        public string Type;
        public string Media;
        public string Language;
        public string Players;
        public string Ratings;
        public string Genre;
        public string Peripheral;
        public string BarCode;
        public string MediaCatalogNumber;

        public List<RvRom> Roms;

        public int RomCount => Roms?.Count ?? 0;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [GAME] (
                    [GameId] INTEGER  PRIMARY KEY NOT NULL,
                    [DatId] INTEGER NOT NULL,
                    [DirId] INTEGER NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [description] NVARCHAR(220) NULL,
                    [manufacturer] NVARCHAR(200) NULL,
                    [cloneof] NVARCHAR(200) NULL,
                    [romof] NVARCHAR(200) NULL,
                    [sampleof] NVARCHAR(200) NULL,
                    [sourcefile] NVARCHAR(200) NULL,
                    [isbios] NVARCHAR(200) NULL,
                    [board] NVARCHAR(200) NULL,
                    [year] NVARCHAR(200) NULL,
                    [istrurip] BOOLEAN DEFAULT 0 NOT NULL,
                    [publisher] NVARCHAR(200) NULL,
                    [developer] NVARCHAR(200) NULL,
                    [edition] NVARCHAR(200) NULL,
                    [version] NVARCHAR(200) NULL,
                    [type] NVARCHAR(200) NULL,
                    [media] NVARCHAR(200) NULL,
                    [language] NVARCHAR(200) NULL,
                    [players] NVARCHAR(200) NULL,
                    [ratings] NVARCHAR(200) NULL,
                    [genre] NVARCHAR(200) NULL,
                    [peripheral] NVARCHAR(200) NULL,
                    [barcode] NVARCHAR(200) NULL,
                    [mediacatalognumber] NVARCHAR(200),
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [ZipFileLength] INTEGER NULL, 
                    [LastWriteTime] INTEGER NULL,
                    [CreationTime] INTEGER NULL,
                    [LastAccessTime] INTEGER NULL,
                    [CentralDirectory] BLOB NULL,
                    [CentralDirectoryOffset] INTEGER NULL,
                    [CentralDirectoryLength] INTEGER NULL,
                    FOREIGN KEY(DatId) REFERENCES DAT(DatId)
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");
        }

        public void DBRead(int gameId, bool readRoms = false)
        {
            if (_commandRvGameRead == null)
            {
                _commandRvGameRead = new SQLiteCommand(@"
                SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE GameId=@GameId ORDER BY name", Program.db.Connection);
                _commandRvGameRead.Parameters.Add(new SQLiteParameter("GameId"));
            }

            _commandRvGameRead.Parameters["GameId"].Value = gameId;

            using (DbDataReader dr = _commandRvGameRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    RvGameReadFromReader(dr, this);
                }

                dr.Close();
            }

            if (readRoms)
            {
                Roms = RvRom.ReadRoms(GameId);
            }
        }

        public static List<RvGame> ReadGames(uint datId, bool readRoms = false)
        {
            if (_commandRvGameReadDatGames == null)
            {
                _commandRvGameReadDatGames = new SQLiteCommand(@"
                SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE DatId=@DatId ORDER BY name", Program.db.Connection);
                _commandRvGameReadDatGames.Parameters.Add(new SQLiteParameter("DatId"));
            }

            List<RvGame> games = new List<RvGame>();
            _commandRvGameReadDatGames.Parameters["DatId"].Value = datId;

            using (DbDataReader dr = _commandRvGameReadDatGames.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGame rvGame = new RvGame();
                    RvGameReadFromReader(dr, rvGame);
                    games.Add(rvGame);
                }
                dr.Close();
            }

            if (readRoms)
            {
                foreach (RvGame game in games)
                {
                    game.Roms = RvRom.ReadRoms(game.GameId);
                }
            }

            return games;
        }

        private static void RvGameReadFromReader(DbDataReader dr, RvGame game)
        {
            game.GameId = Convert.ToUInt32(dr["GameId"]);
            game.DatId = Convert.ToUInt32(dr["DatId"]);
            game.Name = dr["name"].ToString();
            game.Description = dr["description"].ToString();
            game.Manufacturer = dr["manufacturer"].ToString();
            game.CloneOf = dr["cloneOf"].ToString();
            game.RomOf = dr["romof"].ToString();
            game.SourceFile = dr["sourcefile"].ToString();
            game.IsBios = dr["isbios"].ToString();
            game.Board = dr["board"].ToString();
            game.Year = dr["year"].ToString();
            game.IsTrurip = Convert.ToBoolean(dr["istrurip"]);
            game.Publisher = dr["publisher"].ToString();
            game.Developer = dr["developer"].ToString();
            game.Edition = dr["edition"].ToString();
            game.Version = dr["version"].ToString();
            game.Type = dr["type"].ToString();
            game.Media = dr["media"].ToString();
            game.Language = dr["language"].ToString();
            game.Players = dr["players"].ToString();
            game.Ratings = dr["ratings"].ToString();
            game.Genre = dr["genre"].ToString();
            game.Peripheral = dr["peripheral"].ToString();
            game.BarCode = dr["barcode"].ToString();
            game.MediaCatalogNumber = dr["mediacatalognumber"].ToString();
        }

        public void DBWrite()
        {
            if (_commandRvGameWrite == null)
            {
                _commandRvGameWrite = new SQLiteCommand(@"
                INSERT INTO GAME ( DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber)
                          VALUES (@DatId,@Name,@Description,@Manufacturer,@CloneOf,@RomOf,@SourceFile,@IsBios,@Board,@Year,@IsTrurip,@Publisher,@Developer,@Edition,@Version,@Type,@Media,@Language,@Players,@Ratings,@Genre,@Peripheral,@BarCode,@MediaCatalogNumber);

                SELECT last_insert_rowid();", Program.db.Connection);

                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("DatId")); //DatId;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Name")); //Name;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Description")); //Description;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Manufacturer")); //Manufacturer;

                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("CloneOf")); //CloneOf;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("RomOf")); //RomOf;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("SampleOf")); //SampleOf;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Sourcefile")); //SourceFile;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("IsBios")); //IsBios;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Board")); //Board;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Year")); //Year;

                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("IsTrurip")); //IsTrurip;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Publisher")); //Publisher;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Developer")); //Developer;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Edition")); //Edition;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Version")); //Version;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Type")); //Type;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Media")); //Media;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Language")); //Language;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Players")); //Players;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Ratings")); //Ratings;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Genre")); //Genre;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("Peripheral")); //Peripheral;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("BarCode")); //BarCode;
                _commandRvGameWrite.Parameters.Add(new SQLiteParameter("MediaCatalogNumber")); //MediaCatalogNumber;        
            }


            _commandRvGameWrite.Parameters["DatId"].Value = DatId;
            _commandRvGameWrite.Parameters["Name"].Value = Name;
            _commandRvGameWrite.Parameters["Description"].Value = Description;
            _commandRvGameWrite.Parameters["Manufacturer"].Value = Manufacturer;

            _commandRvGameWrite.Parameters["CloneOf"].Value = CloneOf;
            _commandRvGameWrite.Parameters["RomOf"].Value = RomOf;
            _commandRvGameWrite.Parameters["SampleOf"].Value = SampleOf;
            _commandRvGameWrite.Parameters["sourcefile"].Value = SourceFile;
            _commandRvGameWrite.Parameters["IsBios"].Value = IsBios;
            _commandRvGameWrite.Parameters["Board"].Value = Board;
            _commandRvGameWrite.Parameters["Year"].Value = Year;

            _commandRvGameWrite.Parameters["IsTrurip"].Value = IsTrurip;
            _commandRvGameWrite.Parameters["Publisher"].Value = Publisher;
            _commandRvGameWrite.Parameters["Developer"].Value = Developer;
            _commandRvGameWrite.Parameters["Edition"].Value = Edition;
            _commandRvGameWrite.Parameters["Version"].Value = Version;
            _commandRvGameWrite.Parameters["Type"].Value = Type;
            _commandRvGameWrite.Parameters["Media"].Value = Media;
            _commandRvGameWrite.Parameters["Language"].Value = Language;
            _commandRvGameWrite.Parameters["Players"].Value = Players;
            _commandRvGameWrite.Parameters["Ratings"].Value = Ratings;
            _commandRvGameWrite.Parameters["Genre"].Value = Genre;
            _commandRvGameWrite.Parameters["Peripheral"].Value = Peripheral;
            _commandRvGameWrite.Parameters["BarCode"].Value = BarCode;
            _commandRvGameWrite.Parameters["MediaCatalogNumber"].Value = MediaCatalogNumber;

            object res = _commandRvGameWrite.ExecuteScalar();

            if ((res == null) || (res == DBNull.Value))
            {
                return;
            }
            GameId = Convert.ToUInt32(res);

            if (Roms != null)
            {
                foreach (RvRom rvRom in Roms)
                {
                    rvRom.GameId = GameId;
                    rvRom.DBWrite();
                }
            }
        }

        public int AddRom(RvRom rvRom)
        {
            if (Roms == null)
            {
                Roms = new List<RvRom>();
            }

            int index;
            ChildNameSearch(rvRom.Name, out index);
            Roms.Insert(index, rvRom);
            return index;
        }

        private int ChildNameSearch(string lRomName, out int index)
        {
            int intBottom = 0;
            int intTop = Roms.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop)/2;

                intRes = VarFix.CompareName(lRomName, Roms[intMid].Name);
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
                    intRes1 = VarFix.CompareName(lRomName, Roms[index - 1].Name);
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