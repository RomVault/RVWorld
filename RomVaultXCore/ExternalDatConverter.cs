using DATReader.DatStore;
using FileHeaderReader;
using RVIO;
using RVXCore.DB;

namespace RVXCore
{
    public static class ExternalDatConverter
    {
        //        private static CultureInfo enUS = new CultureInfo("en-US");
        public static RvDat ConvertFromExternalDat(string strFilename, DatHeader datHeaderExternal)
        {
            RvDat retDat = new RvDat
            {
                Filename = Path.GetFileName(strFilename),
                Name = datHeaderExternal.Name,
                RootDir = datHeaderExternal.RootDir,
                Description = datHeaderExternal.Description,
                Category = datHeaderExternal.Category,
                Version = datHeaderExternal.Version,
                Date = datHeaderExternal.Date,
                Author = datHeaderExternal.Author,
                Email = datHeaderExternal.Email,
                Homepage = datHeaderExternal.Homepage,
                URL = datHeaderExternal.URL,
                Comment = datHeaderExternal.Comment,
                MergeType = datHeaderExternal.MergeType
            };


            HeaderFileType headerFileType = FileHeaderReader.FileHeaderReader.GetFileTypeFromHeader(datHeaderExternal.Header);

            CopyDir(datHeaderExternal.BaseDir, headerFileType, retDat);

            return retDat;
        }

        private static void CopyDir(DatDir datD, HeaderFileType headerFileType, RvDat retDat = null, RvGame retGame = null)
        {
            DatBase[] datB = datD.ToArray();
            if (datB == null)
                return;
            foreach (DatBase b in datB)
            {
                switch (b)
                {
                    case DatDir nDir:
                        if (nDir.DGame == null)
                            break;

                        DatGame dGame = nDir.DGame;
                        RvGame cGame = new RvGame();

                        cGame.Name = b.Name;
                        cGame.Description = dGame.Description;
                        cGame.Manufacturer = dGame.Manufacturer;
                        cGame.CloneOf = dGame.CloneOf;
                        cGame.RomOf = dGame.RomOf;
                        cGame.SampleOf = dGame.SampleOf;
                        cGame.SourceFile = dGame.SourceFile;
                        cGame.IsBios = dGame.IsBios;
                        cGame.Board = dGame.Board;
                        cGame.Year = dGame.Year;

                        if (dGame.IsEmuArc)
                        {
                            cGame.IsTrurip = true;
                            cGame.Publisher = dGame.Publisher;
                            cGame.Developer = dGame.Developer;
                            //cGame.Edition
                            //cGame.Version
                            //cGame.Type
                            //cGame.Media
                            //cGame.Language
                            cGame.Players = dGame.Players;
                            cGame.Ratings = dGame.Ratings;
                            cGame.Genre = dGame.Genre;
                            //cGame.Peripheral
                            //cGame.BarCode
                            //cGame.MediaCatalogNumber
                        }
                        retDat?.AddGame(cGame);
                        CopyDir(nDir, headerFileType, null, cGame);

                        break;

                    case DatFile nFile:
                        if (nFile.isDisk)
                            break;

                        RvRom nf = new RvRom()
                        {
                            Name = nFile.Name,
                            Size = nFile.Size,
                            CRC = nFile.CRC,
                            SHA1 = nFile.SHA1,
                            MD5 = nFile.MD5,
                            Merge = nFile.Merge,
                            Status = nFile.Status,
                            AltType = headerFileType
                        };

                        retGame?.AddRom(nf);
                        break;
                }
            }
        }

    }
}