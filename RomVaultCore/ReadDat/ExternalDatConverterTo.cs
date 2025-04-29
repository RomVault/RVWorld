/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using DATReader.DatStore;
using DATReader.Utils;
using RomVaultCore.RvDB;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RomVaultCore.ReadDat
{
    public class ExternalDatConverterTo
    {
        public bool useHeader = true;

        public bool filterGot = true;
        public bool filterMissing = true;
        public bool filterFixable = true;
        public bool filterMIA = true;
        public bool filterMerged = true;

        public bool filterFiles = true;
        public bool filterZIPs = true;



        public DatHeader ConvertToExternalDat(RvFile rvFile)
        {
            if (rvFile.IsFile)
                return null;

            RvDat dat = null;

            if (rvFile.DirDatCount == 1)
                dat = rvFile.DirDat(0);
            if (rvFile.Dat != null)
                dat = rvFile.Dat;

            DatHeader datHeader = new DatHeader();
            if (dat != null && useHeader)
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
                datHeader.Compression = dat.GetData(RvDat.DatData.Compression);
            }
            else
            {
                datHeader.Name = rvFile.Name;
            }


            datHeader.BaseDir = new DatDir("", FileType.Dir);

            for (int i = 0; i < rvFile.ChildCount; i++)
            {
                ChildAdd(datHeader.BaseDir, rvFile.Child(i));
            }

            return datHeader;
        }

        private void ChildAdd(DatDir extDir, RvFile rvfile)
        {
            if (rvfile.IsFile)
            {
                switch (rvfile.RepStatus)
                {
                    case RepStatus.Correct:
                    case RepStatus.CorrectMIA:
                    case RepStatus.UnNeeded:
                    case RepStatus.Unknown:
                    case RepStatus.MoveToSort:
                    case RepStatus.Delete:
                    case RepStatus.NeededForFix:
                    case RepStatus.Rename:
                        if (!filterGot) return; break;
                    case RepStatus.Missing:
                    case RepStatus.Incomplete:
                        if (!filterMissing) return; break;
                    case RepStatus.MissingMIA:
                        if (!filterMIA) return; break;
                    case RepStatus.NotCollected:
                        if (!filterMerged) return; break;
                    case RepStatus.CanBeFixed:
                        if (!filterFixable) return; break;

                    case RepStatus.InToSort: break;
                    default:
                        Debug.WriteLine("FilterType unknown");
                        break;
                }

                DatFile extFile = new DatFile(rvfile.Name, FileType.UnSet)
                {
                    Size = rvfile.Size,
                    CRC = rvfile.CRC,
                    SHA1 = rvfile.SHA1,
                    MD5 = rvfile.MD5,
                    Merge = rvfile.Merge,
                    Status = rvfile.Status
                };

                if (rvfile.DatStatus == DatStatus.InDatMIA)
                {
                    extFile.MIA = "yes";
                }

                bool isDisk = (rvfile.HeaderFileType == HeaderFileType.CHD);
                if (isDisk)
                {
                    extFile.isDisk = true;
                    extFile.Name = VarFix.CleanCHD(extFile.Name);
                    extFile.Merge = VarFix.CleanCHD(extFile.Merge);
                    if (rvfile.AltMD5 != null || rvfile.AltSHA1 != null)
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
            if (rvfile.FileType == FileType.Zip)
                gameName = gameName.Substring(0, gameName.Length - 4);
            else if (rvfile.FileType == FileType.SevenZip)
                gameName = gameName.Substring(0, gameName.Length - 3);

            DatDir extDir1 = new DatDir(gameName, FileType.UnSet);

            if (rvfile.Game != null)
            {
                extDir1.DGame = new DatGame
                {
                    Description = rvfile.Game.GetData(RvGame.GameData.Description),
                    Category = CategoryList(rvfile.Game.GetData(RvGame.GameData.Category)),
                    RomOf = rvfile.Game.GetData(RvGame.GameData.RomOf),
                    IsBios = rvfile.Game.GetData(RvGame.GameData.IsBios),
                    SourceFile = rvfile.Game.GetData(RvGame.GameData.Sourcefile),
                    CloneOf = rvfile.Game.GetData(RvGame.GameData.CloneOf),
                    SampleOf = rvfile.Game.GetData(RvGame.GameData.SampleOf),
                    Board = rvfile.Game.GetData(RvGame.GameData.Board),
                    Year = rvfile.Game.GetData(RvGame.GameData.Year),
                    Manufacturer = rvfile.Game.GetData(RvGame.GameData.Manufacturer)
                };
                if (extDir1.DGame.Description != null && extDir1.DGame.Description == "¤")
                    extDir1.DGame.Description = Path.GetFileNameWithoutExtension(rvfile.Name);
            }
            else if (rvfile.FileType == FileType.Zip)
            {
                extDir1.DGame = new DatGame();
            }
            extDir.ChildAdd(extDir1);

            for (int i = 0; i < rvfile.ChildCount; i++)
            {
                ChildAdd(extDir1, rvfile.Child(i));
            }
        }

        private static List<string> CategoryList(string instr)
        {
            if (string.IsNullOrWhiteSpace(instr))
                return null;

            string[] splitList = instr.Split('|');
            for (int i = 0; i < splitList.Length; i++)
                splitList[i] = splitList[i].Trim();

            return splitList.ToList();
        }
    }
}
