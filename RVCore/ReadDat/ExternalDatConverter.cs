using System.Collections.Generic;
using DATReader.DatStore;
using FileHeaderReader;
using RVCore.RvDB;

namespace RVCore.ReadDat
{
    public static class ExternalDatConverter
    {
        public static RvFile ConvertFromExternalDat(DatHeader datHeaderExternal,RvDat datFile)
        {
            RvFile newDirFromExternal = new RvFile(FileType.Dir);
            RvDat newDatFromExternal = new RvDat();
            newDatFromExternal.AddData(RvDat.DatData.DatRootFullName, datFile.GetData(RvDat.DatData.DatRootFullName));
            newDatFromExternal.TimeStamp = datFile.TimeStamp;
            newDatFromExternal.Status = DatUpdateStatus.Correct;
            newDatFromExternal.AddData(RvDat.DatData.DatName, datHeaderExternal.Name);
            newDatFromExternal.AddData(RvDat.DatData.RootDir, datHeaderExternal.RootDir);
            newDatFromExternal.AddData(RvDat.DatData.Description,datHeaderExternal.Description);
            newDatFromExternal.AddData(RvDat.DatData.Category, datHeaderExternal.Category);
            newDatFromExternal.AddData(RvDat.DatData.Version, datHeaderExternal.Version);
            newDatFromExternal.AddData(RvDat.DatData.Date, datHeaderExternal.Date);
            newDatFromExternal.AddData(RvDat.DatData.Author, datHeaderExternal.Author);
            newDatFromExternal.AddData(RvDat.DatData.Email, datHeaderExternal.Email);
            newDatFromExternal.AddData(RvDat.DatData.HomePage, datHeaderExternal.Homepage);
            newDatFromExternal.AddData(RvDat.DatData.URL, datHeaderExternal.URL);
            newDatFromExternal.AddData(RvDat.DatData.DirSetup, datHeaderExternal.Dir);
            newDatFromExternal.AddData(RvDat.DatData.Header, datHeaderExternal.Header);
            newDatFromExternal.MultiDatsInDirectory = datFile.MultiDatsInDirectory;
            newDatFromExternal.MultiDatOverride = datFile.MultiDatOverride;

            newDirFromExternal.Dat = newDatFromExternal;



            HeaderFileType headerFileType = FileHeaderReader.FileHeaderReader.GetFileTypeFromHeader(datHeaderExternal.Header);

            CopyDir(datHeaderExternal.BaseDir, newDirFromExternal, newDatFromExternal, headerFileType, false);

            return newDirFromExternal;
        }

        private static void CheckAttribute(RvGame cGame, string source, RvGame.GameData gParam)
        {
            if (string.IsNullOrWhiteSpace(source))
                return;
            cGame.AddData(gParam, source);
        }

        private static void CopyDir(DatDir datD, RvFile rvD, RvDat rvDat, HeaderFileType headerFileType, bool gameFile)
        {
            DatBase[] datB = datD.ToArray();
            if (datB == null)
                return;
            foreach (DatBase b in datB)
            {
                switch (b)
                {
                    case DatDir nDir:
                        RvFile nd = new RvFile(ConvE(nDir.DatFileType))
                        {
                            Name = nDir.Name + GetExt(nDir.DatFileType),
                            Dat = rvDat,
                            DatStatus = ConvE(nDir.DatStatus)
                        };
                        if (nDir.DGame == null && !gameFile)
                            nd.Tree = new RvTreeRow();

                        rvD.ChildAdd(nd);

                        if (nDir.DGame != null)
                        {
                            DatGame dGame = nDir.DGame;
                            RvGame cGame = new RvGame();
                            CheckAttribute(cGame, dGame.Description, RvGame.GameData.Description);
                            CheckAttribute(cGame, dGame.RomOf, RvGame.GameData.RomOf);
                            CheckAttribute(cGame, dGame.IsBios, RvGame.GameData.IsBios);
                            CheckAttribute(cGame, dGame.SourceFile, RvGame.GameData.Sourcefile);
                            CheckAttribute(cGame, dGame.CloneOf, RvGame.GameData.CloneOf);
                            CheckAttribute(cGame, dGame.SampleOf, RvGame.GameData.SampleOf);
                            CheckAttribute(cGame, dGame.Board, RvGame.GameData.Board);
                            CheckAttribute(cGame, dGame.Year, RvGame.GameData.Year);
                            CheckAttribute(cGame, dGame.Manufacturer, RvGame.GameData.Manufacturer);

                            if (nDir.DGame.IsEmuArc)
                            {
                                cGame.AddData(RvGame.GameData.EmuArc, "yes");
                                CheckAttribute(cGame, dGame.TitleId, RvGame.GameData.TitleId);
                                CheckAttribute(cGame, dGame.Publisher, RvGame.GameData.Publisher);
                                CheckAttribute(cGame, dGame.Developer, RvGame.GameData.Developer);
                                CheckAttribute(cGame, dGame.Genre, RvGame.GameData.Genre);
                                CheckAttribute(cGame, dGame.SubGenre, RvGame.GameData.SubGenre);
                                CheckAttribute(cGame, dGame.Ratings, RvGame.GameData.Ratings);
                                CheckAttribute(cGame, dGame.Score, RvGame.GameData.Score);
                                CheckAttribute(cGame, dGame.Players, RvGame.GameData.Players);
                                CheckAttribute(cGame, dGame.Enabled, RvGame.GameData.Enabled);
                                CheckAttribute(cGame, dGame.CRC, RvGame.GameData.CRC);
                                CheckAttribute(cGame, dGame.RelatedTo, RvGame.GameData.RelatedTo);
                                CheckAttribute(cGame, dGame.Source, RvGame.GameData.Source);
                            }
                            nd.Game = cGame;
                        }

                        CopyDir(nDir, nd, rvDat, headerFileType, gameFile || nDir.DGame != null);
                        break;

                    case DatFile nFile:
                        RvFile nf = new RvFile(ConvE(nFile.DatFileType))
                        {
                            Name = nFile.Name,
                            Size = nFile.Size,
                            CRC = nFile.CRC,
                            SHA1 = nFile.SHA1,
                            MD5 = nFile.MD5,
                            Merge = nFile.Merge,
                            Status = nFile.Status,
                            Dat = rvDat,
                            DatStatus = ConvE(nFile.DatStatus),
                            HeaderFileType = headerFileType
                        };
                        if (nFile.isDisk)
                        {
                            nf.HeaderFileType = HeaderFileType.CHD;
                        }

                        if (nf.HeaderFileType != HeaderFileType.Nothing) nf.FileStatusSet(FileStatus.HeaderFileTypeFromDAT);
                        if (nf.Size != null) nf.FileStatusSet(FileStatus.SizeFromDAT);
                        if (nf.CRC != null) nf.FileStatusSet(FileStatus.CRCFromDAT);
                        if (nf.SHA1 != null) nf.FileStatusSet(FileStatus.SHA1FromDAT);
                        if (nf.MD5 != null) nf.FileStatusSet(FileStatus.MD5FromDAT);

                        rvD.ChildAdd(nf);
                        break;
                }
            }
        }


        private static readonly List<FileType> ConvList = new List<FileType>
        {
            FileType.Unknown,
            FileType.Dir,
            FileType.Zip,
            FileType.Zip,
            FileType.SevenZip,
            FileType.File,
            FileType.ZipFile,
            FileType.SevenZipFile
        };
        private static FileType ConvE(DatFileType inft)
        {
            return ConvList[(int)inft];
        }

        private static readonly List<DatStatus> ConvDat = new List<DatStatus>
        {
            DatStatus.InDatCollect,
            DatStatus.InDatMerged,
            DatStatus.InDatBad
        };

        private static DatStatus ConvE(DatFileStatus infs)
        {
            return ConvDat[(int)infs];
        }

        private static string GetExt(DatFileType intf)
        {
            switch (intf)
            {
                case DatFileType.DirTorrentZip:
                    return ".zip";
                case DatFileType.Dir7Zip:
                    return ".7z";
                default:
                    return "";
            }
        }
    }
}
