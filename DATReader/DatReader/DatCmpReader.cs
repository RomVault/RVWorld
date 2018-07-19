using System.ComponentModel;
using DATReader.DatStore;
using DATReader.Utils;
using RVIO;

namespace DATReader.DatReader
{
    public class DatCmpReader
    {
        private readonly ReportError _errorReport;
        private string _filename;

        public DatCmpReader(ReportError errorReport)
        {
            _errorReport = errorReport;
        }
        public bool ReadDat(string strFilename, out DatHeader datHeader)
        {
            _filename = strFilename;

            using (DatFileLoader dfl = new DatFileLoader())
            {

                datHeader = new DatHeader { BaseDir = new DatDir(DatFileType.UnSet) };
                int errorCode = dfl.LoadDat(strFilename);
                if (errorCode != 0)
                {
                    _errorReport?.Invoke(strFilename, new Win32Exception(errorCode).Message);
                    return false;
                }

                dfl.Gn();
                if (dfl.EndOfStream())
                {
                    return false;
                }
                if (dfl.Next.ToLower() == "clrmamepro" || dfl.Next.ToLower() == "clrmame")
                {
                    dfl.Gn();
                    if (!LoadHeaderFromDat(dfl, datHeader))
                    {
                        return false;
                    }
                    dfl.Gn();
                }
                if (dfl.Next.ToLower() == "romvault")
                {
                    dfl.Gn();
                    if (!LoadHeaderFromDat(dfl, datHeader))
                    {
                        return false;
                    }
                    dfl.Gn();
                }

                while (!dfl.EndOfStream())
                {
                    bool res = ReadNextBlock(dfl, datHeader.BaseDir);
                    if (!res)
                        return false;
                }

            }

            return true;
        }

        private bool ReadNextBlock(DatFileLoader dfl, DatDir parentDir)
        {
            switch (dfl.Next.ToLower())
            {
                case "dir":
                    dfl.Gn();
                    if (!LoadDirFromDat(dfl, parentDir))
                    {
                        return false;
                    }
                    dfl.Gn();
                    break;
                case "game":
                case "machine":
                    dfl.Gn();
                    if (!LoadGameFromDat(dfl, parentDir))
                    {
                        return false;
                    }
                    dfl.Gn();
                    break;
                case "resource":
                    dfl.Gn();
                    if (!LoadGameFromDat(dfl, parentDir))
                    {
                        return false;
                    }
                    dfl.Gn();
                    break;
                case "emulator":
                    dfl.Gn();
                    if (!LoadEmulator(dfl))
                    {
                        return false;
                    }
                    dfl.Gn();
                    break;
                default:
                    _errorReport?.Invoke(dfl.Filename, "Error Keyword " + dfl.Next + " not know in dir, on line " + dfl.LineNumber);
                    dfl.Gn();
                    break;
            }
            return true;
        }

        private bool LoadHeaderFromDat(DatFileLoader dfl, DatHeader datHeader)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after clrmamepro, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            datHeader.Filename = _filename;

            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "name":
                        datHeader.Name = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "description":
                        datHeader.Description = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "category":
                        datHeader.Category = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "version":
                        datHeader.Version = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "date":
                        datHeader.Date = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "author":
                        datHeader.Author = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "email":
                        datHeader.Email = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "homepage":
                        datHeader.Homepage = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "url":
                        datHeader.URL = dfl.GnRest();
                        dfl.Gn();
                        break;

                    case "comment":
                        datHeader.Comment = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "header":
                        datHeader.Header = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "forcezipping":
                        datHeader.Compression = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "forcepacking":
                        datHeader.Compression = dfl.GnRest();
                        dfl.Gn();
                        break; // incorrect usage
                    case "forcemerging":
                        datHeader.MergeType = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "forcenodump":
                        datHeader.NoDump = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "dir":
                        datHeader.Dir = dfl.GnRest();
                        dfl.Gn();
                        break;
                    default:
                        _errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in clrmamepro, on line " + dfl.LineNumber);
                        dfl.Gn();
                        break;
                }
            }

            return true;
        }

        private bool LoadEmulator(DatFileLoader dfl)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after emulator, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();
            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "name":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "version":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "debug":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    default:
                        _errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in emulator, on line " + dfl.LineNumber);
                        dfl.Gn();
                        break;
                }
            }
            return true;
        }


        private bool LoadDirFromDat(DatFileLoader dfl, DatDir parentDir)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after game, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            if (dfl.Next.ToLower() != "name")
            {
                _errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
                return false;
            }
            DatDir dir = new DatDir(DatFileType.UnSet)
            {
                Name = dfl.GnRest()
            };

            dfl.Gn();

            parentDir.ChildAdd(dir);

            while (dfl.Next != ")")
            {
                bool res = ReadNextBlock(dfl, dir);
                if (!res)
                    return false;
            }
            return true;
        }

        private bool LoadGameFromDat(DatFileLoader dfl, DatDir parentDir)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after game, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            string snext = dfl.Next.ToLower();

            string pathextra = "";
            if (snext == "rebuildto")
            {
                pathextra = dfl.Gn();
                dfl.Gn();
                snext = dfl.Next.ToLower();
            }

            if (snext != "name")
            {
                _errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
                return false;
            }

            string name = dfl.GnRest();

            name = Path.Combine(pathextra, name);

            dfl.Gn();

            DatDir dDir = new DatDir(DatFileType.UnSet) { Name = name, DGame = new DatGame() };
            DatGame dGame = dDir.DGame;
            while (dfl.Next != ")" && !dfl.EndOfStream())
            {
                switch (dfl.Next.ToLower())
                {
                    case "romof":
                        dGame.RomOf = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "description":
                        dGame.Description = dfl.GnRest();
                        dfl.Gn();
                        break;

                    case "sourcefile":
                        dGame.SourceFile = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "cloneof":
                        dGame.CloneOf = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "sampleof":
                        dGame.SampleOf = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "board":
                        dGame.Board = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "year":
                        dGame.Year = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "manufacturer":
                        dGame.Manufacturer = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "history":
                        dGame.History = dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "serial":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "rebuildto":
                        dfl.GnRest();
                        dfl.Gn();
                        break;

                    case "sample":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "biosset":
                        dfl.GnRest();
                        dfl.Gn();
                        break;

                    case "chip":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "video":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "sound":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "input":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "dipswitch":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "driver":
                        dfl.GnRest();
                        dfl.Gn();
                        break;
                    case "display":
                        dfl.GnRest();
                        dfl.Gn();
                        break;


                    case "rom":
                        dfl.Gn();
                        if (!LoadRomFromDat(dfl, dDir))
                        {
                            return false;
                        }
                        dfl.Gn();
                        break;
                    case "disk":
                        dfl.Gn();
                        if (!LoadDiskFromDat(dfl, dDir))
                        {
                            return false;
                        }
                        dfl.Gn();
                        break;

                    case "archive":
                        dfl.Gn();
                        if (!LoadArchiveFromDat(dfl))
                        {
                            return false;
                        }
                        dfl.Gn();
                        break;

                    default:
                        _errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in game, on line " + dfl.LineNumber);
                        dfl.Gn();
                        break;
                }
            }
            parentDir.ChildAdd(dDir);
            return true;
        }

        private bool LoadRomFromDat(DatFileLoader dfl, DatDir parentDir)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after rom, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            if (dfl.Next.ToLower() != "name")
            {
                _errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
                return false;
            }


            DatFile dRom = new DatFile(DatFileType.UnSet)
            {
                Name = dfl.Gn()
            };
            dfl.Gn();


            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "size":
                        dRom.Size = VarFix.ULong(dfl.Gn());
                        dfl.Gn();
                        break;
                    case "crc":
                        dRom.CRC = VarFix.CleanMD5SHA1(dfl.Gn(), 8);
                        dfl.Gn();
                        break;
                    case "sha1":
                        dRom.SHA1 = VarFix.CleanMD5SHA1(dfl.Gn(), 40);
                        dfl.Gn();
                        break;
                    case "md5":
                        dRom.MD5 = VarFix.CleanMD5SHA1(dfl.Gn(), 32);
                        dfl.Gn();
                        break;
                    case "merge":
                        dRom.Merge = VarFix.String(dfl.Gn());
                        dfl.Gn();
                        break;
                    case "flags":
                        dRom.Status = VarFix.ToLower(dfl.Gn());
                        dfl.Gn();
                        break;
                    case "date":
                        dfl.Gn();
                        dfl.Gn();
                        break;
                    case "bios":
                        dfl.Gn();
                        dfl.Gn();
                        break;
                    case "region":
                        dfl.Gn();
                        dfl.Gn();
                        break;
                    case "offs":
                        dfl.Gn();
                        dfl.Gn();
                        break;
                    case "nodump":
                        dRom.Status = "nodump";
                        dfl.Gn();
                        break;
                    case "baddump":
                        dRom.Status = "baddump";
                        dfl.Gn();
                        break;

                    default:
                        _errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in rom, on line " + dfl.LineNumber);
                        dfl.Gn();
                        break;
                }
            }

            parentDir.ChildAdd(dRom);

            return true;
        }

        private bool LoadDiskFromDat(DatFileLoader dfl, DatDir parentDir)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after rom, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            if (dfl.Next.ToLower() != "name")
            {
                _errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
                return false;
            }


            DatFile dRom = new DatFile(DatFileType.UnSet)
            {
                Name = VarFix.String(dfl.Gn()) + ".chd",
                isDisk = true
            };

            dfl.Gn();

            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "sha1":
                        dRom.SHA1 = VarFix.CleanMD5SHA1(dfl.Gn(), 40);
                        dfl.Gn();
                        break;
                    case "md5":
                        dRom.MD5 = VarFix.CleanMD5SHA1(dfl.Gn(), 32);
                        dfl.Gn();
                        break;
                    case "region":
                        dRom.Region = VarFix.String(dfl.Gn());
                        dfl.Gn();
                        break;
                    case "merge":
                        dRom.Merge = VarFix.String(dfl.Gn());
                        dfl.Gn();
                        break;
                    case "index":
                        dfl.Gn();
                        dfl.Gn();
                        break;
                    case "flags":
                        dRom.Status = VarFix.ToLower(dfl.Gn());
                        dfl.Gn();
                        break;
                    case "nodump":
                        dRom.Status = "nodump";
                        dfl.Gn();
                        break;
                    default:
                        _errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in rom, on line " + dfl.LineNumber);
                        dfl.Gn();
                        break;
                }
            }
            parentDir.ChildAdd(dRom);

            return true;
        }

        private bool LoadArchiveFromDat(DatFileLoader dfl)
        {
            if (dfl.Next != "(")
            {
                _errorReport?.Invoke(dfl.Filename, "( not found after Archive, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "name":
                        dfl.Gn();
                        dfl.Gn();
                        break;
                    default:
                        _errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not know in Archive, on line " + dfl.LineNumber);
                        dfl.Gn();
                        break;
                }
            }
            return true;
        }


    }
}