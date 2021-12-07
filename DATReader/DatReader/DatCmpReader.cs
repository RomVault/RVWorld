using System.ComponentModel;
using System.Text;
using DATReader.DatStore;
using DATReader.Utils;
using RVIO;

namespace DATReader.DatReader
{
    public static class DatCmpReader
    {
        public static bool ReadDat(string strFilename, ReportError errorReport, out DatHeader datHeader)
        {
            using (DatFileLoader dfl = new DatFileLoader())
            {
                datHeader = new DatHeader { BaseDir = new DatDir(DatFileType.UnSet) };
                int errorCode = dfl.LoadDat(strFilename, DatRead.Enc);
                if (errorCode != 0)
                {
                    errorReport?.Invoke(strFilename, new Win32Exception(errorCode).Message);
                    return false;
                }

                dfl.Gn();
                if (dfl.EndOfStream())
                {
                    return false;
                }
                if (dfl.Next.ToLower() == "clrmamepro" || dfl.Next.ToLower() == "clrmame")
                {
                    if (!LoadHeaderFromDat(dfl, strFilename, datHeader, errorReport))
                    {
                        return false;
                    }
                    dfl.Gn();
                }
                if (dfl.Next.ToLower() == "raine")
                {
                    while (dfl.Next.ToLower() != "emulator")
                        dfl.Gn();
                    if (!LoadHeaderFromDat(dfl, strFilename, datHeader, errorReport))
                    {
                        return false;
                    }
                    dfl.Gn();
                }
                if (dfl.Next.ToLower() == "romvault")
                {
                    if (!LoadHeaderFromDat(dfl, strFilename, datHeader, errorReport))
                    {
                        return false;
                    }
                    dfl.Gn();
                }

                while (!dfl.EndOfStream())
                {
                    bool res = ReadNextBlock(dfl, datHeader.BaseDir, errorReport);
                    if (!res)
                        return false;
                }

            }

            return true;
        }

        private static bool ReadNextBlock(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            switch (dfl.Next.ToLower())
            {
                case "dir":
                    if (!LoadDirFromDat(dfl, parentDir, errorReport))
                    {
                        return false;
                    }
                    break;
                case "game":
                case "machine":
                    if (!LoadGameFromDat(dfl, parentDir, errorReport))
                    {
                        return false;
                    }
                    break;
                case "resource":
                    if (!LoadGameFromDat(dfl, parentDir, errorReport))
                    {
                        return false;
                    }
                    break;
                case "emulator":
                    if (!LoadEmulator(dfl, errorReport))
                    {
                        return false;
                    }
                    break;
                case "#": // comments

                    dfl.GnRest();
                    break;
                default:
                    errorReport?.Invoke(dfl.Filename, "Error Keyword " + dfl.Next + " not know in dir, on line " + dfl.LineNumber);
                    break;
            }
            dfl.Gn();
            return true;
        }

        private static bool LoadHeaderFromDat(DatFileLoader dfl, string filename, DatHeader datHeader, ReportError errorReport)
        {
            dfl.Gn();
            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after clrmamepro, on line " + dfl.LineNumber);
                return false;
            }
            dfl.Gn();

            datHeader.Filename = filename;

            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "name":
                        datHeader.Name = dfl.GnRest();
                        break;
                    case "description":
                        datHeader.Description = dfl.GnRest();
                        break;
                    case "category":
                        datHeader.Category = dfl.GnRest();
                        break;
                    case "version":
                        datHeader.Version = dfl.GnRest();
                        break;
                    case "date":
                        datHeader.Date = dfl.GnRest();
                        break;
                    case "author":
                        datHeader.Author = dfl.GnRest();
                        break;
                    case "email":
                        datHeader.Email = dfl.GnRest();
                        break;
                    case "homepage":
                        datHeader.Homepage = dfl.GnRest();
                        break;
                    case "url":
                        datHeader.URL = dfl.GnRest();
                        break;

                    case "comment":
                        datHeader.Comment = dfl.GnRest();
                        break;
                    case "header":
                        datHeader.Header = dfl.GnRest();
                        break;
                    case "forcezipping":
                        datHeader.Compression = dfl.GnRest();
                        break;
                    case "forcepacking":
                        datHeader.Compression = dfl.GnRest();
                        break; // incorrect usage
                    case "forcemerging":
                        datHeader.MergeType = dfl.GnRest();
                        break;
                    case "forcenodump":
                        datHeader.NoDump = dfl.GnRest();
                        break;
                    case "dir":
                        datHeader.Dir = dfl.GnRest();
                        break;

                    case "games":
                        dfl.GnRest();
                        break;
                    default:
                        errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in clrmamepro, on line " + dfl.LineNumber);
                        break;
                }
                dfl.Gn();
            }

            return true;
        }

        private static bool LoadEmulator(DatFileLoader dfl, ReportError errorReport)
        {
            dfl.Gn();
            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after emulator, on line " + dfl.LineNumber);
                return false;
            }

            dfl.Gn();
            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "name":
                        dfl.GnRest();
                        break;
                    case "version":
                        dfl.GnRest();
                        break;
                    case "debug":
                        dfl.GnRest();
                        break;
                    default:
                        errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in emulator, on line " + dfl.LineNumber);
                        break;
                }
                dfl.Gn();
            }
            return true;
        }


        private static bool LoadDirFromDat(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            dfl.Gn();
            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after game, on line " + dfl.LineNumber);
                return false;
            }

            dfl.Gn();
            if (dfl.Next.ToLower() != "name")
            {
                errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
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
                bool res = ReadNextBlock(dfl, dir, errorReport);
                if (!res)
                    return false;
            }
            return true;
        }

        private static bool LoadGameFromDat(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            dfl.Gn();
            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after game, on line " + dfl.LineNumber);
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
                errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
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
                        break;
                    case "description":
                        string description = dfl.GnRestQ();
                        int idx1 = description.IndexOf("\"");
                        if (idx1 != -1)
                        {
                            int idx2 = description.IndexOf("\"", idx1 + 1);
                            if (idx2 != -1)
                            {
                                description = description.Substring(idx1 + 1, idx2 - idx1 - 1);
                            }
                        }
                        dGame.Description = description;
                        break;

                    case "sourcefile":
                        dGame.SourceFile = dfl.GnRest();
                        break;
                    case "cloneof":
                        dGame.CloneOf = dfl.GnRest();
                        break;
                    case "sampleof":
                        dGame.SampleOf = dfl.GnRest();
                        break;
                    case "board":
                        dGame.Board = dfl.GnRest();
                        break;
                    case "year":
                        dGame.Year = dfl.GnRest();
                        break;
                    case "manufacturer":
                        dGame.Manufacturer = dfl.GnRest();
                        break;
                    case "history":
                        dGame.History = dfl.GnRest();
                        break;
                    case "isdevice":
                        dGame.IsDevice = dfl.GnRest();
                        break;
                    case "serial":
                    case "rebuildto":
                    case "sample":
                    case "biosset":
                    case "chip":
                    case "video":
                    case "sound":
                    case "input":
                    case "dipswitch":
                    case "driver":
                    case "display":
                    case "comment":
                    case "releaseyear":
                    case "releasemonth":
                    case "releaseday":
                    case "genre":
                    case "developer":
                    case "publisher":
                    case "homepage":
                    case "users":
                    case "version":
                    case "license":
                    case "device_ref":
                    case "driverstatus":
                    case "ismechanical":
                    case "#": // comments

                        dfl.GnRest();
                        break;

                    case "name":
                        string tmpName = dfl.GnRest();
                        errorReport?.Invoke(dfl.Filename, "Error: multiple names found in one game '" + tmpName + "' will be ignored, on line " + dfl.LineNumber);
                        break;

                    case "rom":
                        if (!LoadRomFromDat(dfl, dDir, errorReport))
                        {
                            return false;
                        }
                        break;
                    case "disk":
                        if (!LoadDiskFromDat(dfl, dDir, errorReport))
                        {
                            return false;
                        }
                        break;

                    case "archive":
                        if (!LoadArchiveFromDat(dfl, errorReport))
                        {
                            return false;
                        }
                        break;

                    default:
                        errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in game, on line " + dfl.LineNumber);
                        break;
                }
                dfl.Gn();
            }
            parentDir.ChildAdd(dDir);
            return true;
        }

        private static bool LoadRomFromDat(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            dfl.Gn();
            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after rom, on line " + dfl.LineNumber);
                return false;
            }

            dfl.Gn();
            if (dfl.Next.ToLower() != "name")
            {
                errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
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
                        break;
                    case "hash":
                        dfl.Gn();
                        break;
                    case "crc":
                    case "crc32":
                        dRom.CRC = VarFix.CleanMD5SHA1(dfl.Gn(), 8);
                        break;
                    case "sha1":
                        dRom.SHA1 = VarFix.CleanMD5SHA1(dfl.Gn(), 40);
                        break;
                    case "md5":
                        dRom.MD5 = VarFix.CleanMD5SHA1(dfl.Gn(), 32);
                        break;
                    case "merge":
                        dRom.Merge = VarFix.String(dfl.Gn());
                        break;
                    case "flags":
                        string flags = VarFix.ToLower(dfl.Gn());
                        if (string.IsNullOrWhiteSpace(dRom.Status))
                            dRom.Status = flags;
                        break;
                    case "date":
                        dfl.Gn();
                        break;
                    case "bios":
                        dfl.Gn();
                        break;
                    case "region":
                        dfl.Gn();
                        break;
                    case "regiona":
                    case "regionb":
                        while (dfl.Next != ")")
                            dfl.Gn();
                        continue;
                    case "offs":
                        dfl.Gn();
                        break;
                    case "nodump":
                        dRom.Status = "nodump";
                        break;
                    case "baddump":
                        dRom.Status = "baddump";
                        break;


                    default:
                        errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in rom, on line " + dfl.LineNumber);
                        break;
                }
                dfl.Gn();

            }

            parentDir.ChildAdd(dRom);

            return true;
        }

        private static bool LoadDiskFromDat(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            dfl.Gn();
            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after rom, on line " + dfl.LineNumber);
                return false;
            }

            dfl.Gn();
            if (dfl.Next.ToLower() != "name")
            {
                errorReport?.Invoke(dfl.Filename, "Name not found as first object in ( ), on line " + dfl.LineNumber);
                return false;
            }

            DatFile dRom = new DatFile(DatFileType.UnSet)
            {
                Name = VarFix.CleanCHD(dfl.Gn()),
                isDisk = true
            };

            dfl.Gn();
            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "sha1":
                        dRom.SHA1 = VarFix.CleanMD5SHA1(dfl.Gn(), 40);
                        break;
                    case "md5":
                        dRom.MD5 = VarFix.CleanMD5SHA1(dfl.Gn(), 32);
                        break;
                    case "region":
                        dRom.Region = VarFix.String(dfl.Gn());
                        break;
                    case "merge":
                        dRom.Merge = VarFix.String(dfl.Gn());
                        break;
                    case "index":
                        dfl.Gn();
                        break;
                    case "flags":
                        dRom.Status = VarFix.ToLower(dfl.Gn());
                        break;
                    case "nodump":
                        dRom.Status = "nodump";
                        break;
                    default:
                        errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not known in rom, on line " + dfl.LineNumber);
                        break;
                }
                dfl.Gn();
            }
            parentDir.ChildAdd(dRom);

            return true;
        }

        private static bool LoadArchiveFromDat(DatFileLoader dfl, ReportError errorReport)
        {
            dfl.Gn();

            if (dfl.Next != "(")
            {
                errorReport?.Invoke(dfl.Filename, "( not found after Archive, on line " + dfl.LineNumber);
                return false;
            }

            dfl.Gn();
            while (dfl.Next != ")")
            {
                switch (dfl.Next.ToLower())
                {
                    case "name":
                        dfl.Gn();
                        break;
                    default:
                        errorReport?.Invoke(dfl.Filename, "Error: key word '" + dfl.Next + "' not know in Archive, on line " + dfl.LineNumber);
                        break;
                }
                dfl.Gn();
            }
            return true;
        }
    }
}