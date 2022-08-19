using DATReader.DatStore;
using RomVaultCore.RvDB;

namespace RomVaultCore.ReadDat
{
    public static class ExternalDatConverterTo
    {
        public static DatHeader ConvertToExternalDat(RvFile rvFile)
        {
            if (rvFile.IsFile)
                return null;

            RvDat dat = null;

            if (rvFile.DirDatCount == 1)
                dat = rvFile.DirDat(0);
            if (rvFile.Dat != null)
                dat = rvFile.Dat;

            DatHeader datHeader = new DatHeader();
            if (dat != null)
            {
                datHeader.Name = dat.GetData(RvDat.DatData.DatName);
                datHeader.RootDir = dat.GetData(RvDat.DatData.RootDir);
                datHeader.Description = dat.GetData(RvDat.DatData.Description);
                datHeader.Category = dat.GetData(RvDat.DatData.Category);
                datHeader.Version = dat.GetData(RvDat.DatData.Version);
                datHeader.Date = dat.GetData(RvDat.DatData.Date);
                datHeader.Author = dat.GetData(RvDat.DatData.Author);
                datHeader.Email = dat.GetData(RvDat.DatData.Email);
                datHeader.Homepage = dat.GetData(RvDat.DatData.HomePage);
                datHeader.URL = dat.GetData(RvDat.DatData.URL);
                datHeader.Dir = dat.GetData(RvDat.DatData.DirSetup);
                datHeader.Header = dat.GetData(RvDat.DatData.Header);
            }
            else
            {
                datHeader.Name = rvFile.Name;
            }


            datHeader.BaseDir = new DatDir("",DatFileType.Dir);

            for (int i = 0; i < rvFile.ChildCount; i++)
            {
                ChildAdd(datHeader.BaseDir, rvFile.Child(i));
            }

            return datHeader;
        }

        private static void ChildAdd(DatDir extDir, RvFile rvfile)
        {
            if (rvfile.IsFile)
            {
                DatFile extFile = new DatFile(rvfile.Name, DatFileType.UnSet)
                {
                    Size = rvfile.Size,
                    CRC = rvfile.CRC,
                    SHA1 = rvfile.SHA1,
                    MD5 = rvfile.MD5,
                    Merge = rvfile.Merge,
                    Status = rvfile.Status
                };

                bool isDisk = (rvfile.HeaderFileType == FileHeaderReader.HeaderFileType.CHD);
                if (isDisk)
                {
                    extFile.isDisk = true;
                    if (rvfile.AltMD5!=null || rvfile.AltSHA1!=null)
                    {
                        extFile.Size = rvfile.AltSize;
                        extFile.CRC = rvfile.AltCRC;
                        extFile.SHA1 = rvfile.AltSHA1;
                        extFile.MD5 = rvfile.AltMD5;
                    }
                }

                extDir.ChildAdd(extFile);
                return;
            }

            string gameName = rvfile.Name;
            if (rvfile.FileType==FileType.Zip)
                gameName = gameName.Substring(0, gameName.Length - 4);
            else if (rvfile.FileType==FileType.SevenZip)
                gameName = gameName.Substring(0, gameName.Length - 3);

            DatDir extDir1 = new DatDir(gameName,DatFileType.UnSet);
            
            if (rvfile.Game!=null)
            {
                extDir1.DGame = new DatGame();
                extDir1.DGame.Description = rvfile.Game.GetData(RvGame.GameData.Description);
                extDir1.DGame.Category = rvfile.Game.GetData(RvGame.GameData.Category);
                extDir1.DGame.RomOf = rvfile.Game.GetData(RvGame.GameData.RomOf);
                extDir1.DGame.IsBios = rvfile.Game.GetData(RvGame.GameData.IsBios);
                extDir1.DGame.SourceFile = rvfile.Game.GetData(RvGame.GameData.Sourcefile);
                extDir1.DGame.CloneOf = rvfile.Game.GetData(RvGame.GameData.CloneOf);
                extDir1.DGame.SampleOf = rvfile.Game.GetData(RvGame.GameData.SampleOf);
                extDir1.DGame.Board = rvfile.Game.GetData(RvGame.GameData.Board);
                extDir1.DGame.Year = rvfile.Game.GetData(RvGame.GameData.Year);
                extDir1.DGame.Manufacturer = rvfile.Game.GetData(RvGame.GameData.Manufacturer);
            }
            extDir.ChildAdd(extDir1);

            for(int i=0;i<rvfile.ChildCount;i++)
            {
                ChildAdd(extDir1, rvfile.Child(i));
            }
        }
    }
}
