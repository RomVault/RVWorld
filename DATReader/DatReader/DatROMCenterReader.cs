using System;
using DATReader.DatStore;
using DATReader.Utils;
using RVIO;

namespace DATReader.DatReader
{
    public static class DatROMCenterReader
    {
        public static bool ReadDat(string strFilename, ReportError errorReport, out DatHeader datHeader)
        {
            datHeader = new DatHeader
            {
                BaseDir = new DatDir("", DatFileType.UnSet),
                Filename = strFilename
            };

            using (DatFileLoader dfl = new DatFileLoader())
            {
                dfl.LoadDat(strFilename);
                dfl.Gn();

                while (!dfl.EndOfStream())
                {
                    switch (dfl.Next.ToLower())
                    {
                        case "[credits]":
                            //getcredits
                            if (!LoadCredits(dfl, datHeader, errorReport))
                                return false;
                            break;
                        case "[dat]":
                            //getdat
                            if (!LoadDat(dfl, datHeader, errorReport))
                                return false;
                            break;
                        case "[emulator]":
                            //emulator
                            if (!LoadEmulator(dfl, datHeader, errorReport))
                                return false;
                            break;
                        case "[games]":
                            //games
                            if (!LoadGame(dfl, datHeader.BaseDir, errorReport))
                                return false;
                            break;
                        case "[resources]":
                            //resources 
                            if (!LoadGame(dfl, datHeader.BaseDir, errorReport))
                                return false;
                            break;
                        case "[disks]":
                            //games
                            if (!LoadDisks(dfl, datHeader.BaseDir, errorReport))
                                return false;
                            break;
                        default:
                            errorReport?.Invoke(dfl.Filename, "Unknown Line " + dfl.Next + " , " + dfl.LineNumber);
                            return false;
                    }
                }

            }

            return true;
        }


        private static bool LoadCredits(DatFileLoader dfl, DatHeader datHeader, ReportError errorReport)
        {
            if (dfl.Next.ToLower() != "[credits]")
            {
                errorReport?.Invoke(dfl.Filename, "Looking for [CREDITS] but found " + dfl.Next + " , " + dfl.LineNumber);
                return false;
            }

            while (!dfl.EndOfStream())
            {
                string line = dfl.Gn();
                if (line.Substring(0, 1) == "[")
                    return true;

                string element;
                string value;
                if (!splitLine(line, out element, out value))
                    return false;

                switch (element.ToLower())
                {
                    case "author":
                        datHeader.Author = value;
                        break;
                    case "email":
                        datHeader.Email = value;
                        break;
                    case "homepage":
                        datHeader.URL = value;
                        break;
                    case "url":
                        datHeader.URL = value;
                        break;
                    case "version":
                        datHeader.Version = value;
                        break;
                    case "date":
                        datHeader.Date = value;
                        break;
                    case "comment":
                        datHeader.Comment = value;
                        break;
                    default:
                        errorReport?.Invoke(dfl.Filename, "Unknown Line " + dfl.Next + " found in [CREDITS], " + dfl.LineNumber);
                        return false;
                }
            }
            return true;
        }

        private static bool LoadDat(DatFileLoader dfl, DatHeader datHeader, ReportError errorReport)
        {
            if (dfl.Next.ToLower() != "[dat]")
            {
                errorReport?.Invoke(dfl.Filename, "Looking for [DAT] but found " + dfl.Next + " , " + dfl.LineNumber);
                return false;
            }

            while (!dfl.EndOfStream())
            {
                string line = dfl.Gn();
                if (line.Substring(0, 1) == "[")
                    return true;

                string element;
                string value;
                if (!splitLine(line, out element, out value))
                    return false;

                switch (element.ToLower())
                {
                    case "version":
                        //datVersion = value;
                        break;
                    case "plugin":
                        //plugin = value;
                        break;
                    case "split":
                        datHeader.Split = value;
                        break;
                    case "merge":
                        datHeader.MergeType = value;
                        break;

                    default:
                        errorReport?.Invoke(dfl.Filename, "Unknown Line " + dfl.Next + " found in [DAT], " + dfl.LineNumber);
                        return false;
                }
            }
            return true;
        }

        private static bool LoadEmulator(DatFileLoader dfl, DatHeader datHeader, ReportError errorReport)
        {
            if (dfl.Next.ToLower() != "[emulator]")
            {
                errorReport?.Invoke(dfl.Filename, "Looking for [EMULATOR] but found " + dfl.Next + " , " + dfl.LineNumber);
                return false;
            }

            while (!dfl.EndOfStream())
            {
                string line = dfl.Gn();
                if (line.Substring(0, 1) == "[")
                    return true;

                string element;
                string value;
                if (!splitLine(line, out element, out value))
                    return false;

                switch (element.ToLower())
                {
                    case "refname":
                        datHeader.Name = value;
                        break;
                    case "version":
                        datHeader.Description = value;
                        break;
                    case "category":
                        datHeader.Category = value;
                        break;
                    case "exe":
                        break;
                    case "runcmd":
                        break;
                    case "romspaths":
                        break;
                    default:
                        errorReport?.Invoke(dfl.Filename, "Unknown Line " + dfl.Next + " found in [EMULATOR], " + dfl.LineNumber);
                        return false;
                }
            }
            return true;
        }

        private static bool LoadGame(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            if (dfl.Next.ToLower() != "[games]" && dfl.Next.ToLower() != "[resources]")
            {
                errorReport?.Invoke(dfl.Filename, "Looking for [GAMES] but found " + dfl.Next + " , " + dfl.LineNumber);
                return false;
            }

            while (!dfl.EndOfStream())
            {
                string line = dfl.Gn();

                if (line.Substring(0, 1) == "[")
                    return true;

                string[] parts = line.Split('¬');

                // 1 parent name         = clone of
                // 2 parent description  = description (from parent)
                // 3 game name           = name (game)
                // 4 game description    = description
                // 5 rom name            = name (rom)
                // 6 rom crc             = crc
                // 7 rom size            = size
                // 8 romof name          = romof
                // 9 merge name          = merge

                string ParentName = parts[1];
                string ParentDescription = parts[2];
                string GameName = parts[3];
                string GameDescription = parts[4];
                string romName = parts[5];
                string romCRC = parts[6];
                string romSize = parts[7];
                string romOf = parts[8];
                string merge = parts[9];

                int index;
                DatDir dDir;
                DatDir searchDir = new DatDir(GameName, DatFileType.Dir);
                if (parentDir.ChildNameSearch(searchDir, out index) != 0)
                {
                    dDir = new DatDir(GameName, DatFileType.UnSet) { DGame = new DatGame() };
                    DatGame dGame = dDir.DGame;
                    dGame.Description = GameDescription;
                    if (ParentName != GameName)
                        dGame.CloneOf = ParentName;
                    parentDir.ChildAdd(dDir);
                }
                else
                {
                    dDir = (DatDir)parentDir[index];
                    // need to check everything matches
                }

                DatFile dRom = new DatFile(romName, DatFileType.UnSet);
                dRom.CRC = VarFix.CleanMD5SHA1(romCRC, 8);
                dRom.Size = VarFix.ULong(romSize);
                dRom.Merge = merge;
                // check romof=ParentName

                dDir.ChildAdd(dRom);
            }
            return true;
        }

        private static bool LoadDisks(DatFileLoader dfl, DatDir parentDir, ReportError errorReport)
        {
            if (dfl.Next.ToLower() != "[disks]")
            {
                errorReport?.Invoke(dfl.Filename, "Looking for [DISKS] but found " + dfl.Next + " , " + dfl.LineNumber);
                return false;
            }

            while (!dfl.EndOfStream())
            {
                string line = dfl.Gn();

                if (line.Substring(0, 1) == "[")
                    return true;

                string[] parts = line.Split('¬');

                // 1 parent name         = clone of
                // 2 parent description  = description (from parent)
                // 3 game name           = name (game)
                // 4 game description    = description
                // 5 rom name            = name (rom)
                // 6 rom crc             = crc
                // 7 rom size            = size
                // 8 romof name          = romof
                // 9 merge name          = merge

                string ParentName = parts[1];
                string ParentDescription = parts[2];
                string GameName = parts[3];
                string GameDescription = parts[4];
                string romName = parts[5];
                string romCRC = parts[6];
                string romSize = parts[7];
                string romOf = parts[8];
                string merge = parts[9];

                int index;
                DatDir dDir;
                DatDir searchDir = new DatDir(GameName, DatFileType.Dir);
                if (parentDir.ChildNameSearch(searchDir, out index) != 0)
                {
                    dDir = new DatDir(GameName, DatFileType.UnSet) { DGame = new DatGame() };
                    DatGame dGame = dDir.DGame;
                    dGame.Description = GameDescription;
                    if (ParentName != GameName)
                        dGame.CloneOf = ParentName;
                    parentDir.ChildAdd(dDir);
                }
                else
                {
                    dDir = (DatDir)parentDir[index];
                    // need to check everything matches
                }

                DatFile dRom = new DatFile(VarFix.CleanCHD(romName), DatFileType.UnSet)
                {
                    isDisk = true,
                    SHA1 = VarFix.CleanMD5SHA1(romCRC, 40),
                    Merge = merge
                };
                // dRom.Size = VarFix.ULong(romSize);
                // check romof=ParentName

                dDir.ChildAdd(dRom);
            }
            return true;
        }


        private static bool splitLine(string s, out string Element, out string Value)
        {
            int i = s.IndexOf("=");
            if (i == -1)
            {
                Element = null;
                Value = null;
                return false;
            }
            Element = s.Substring(0, i);
            Value = s.Substring(i + 1);
            return true;
        }

        internal class DatFileLoader : IDisposable
        {
            private System.IO.StreamReader _streamReader;
            public string Next;
            public int LineNumber = 0;

            public string Filename { get; private set; }

            public int LoadDat(string strFilename)
            {
                Filename = strFilename;
                _streamReader = File.OpenText(strFilename, DatRead.Enc);
                return 0;
            }

            public void Dispose()
            {
                _streamReader.Close();
                _streamReader.Dispose();
            }

            public bool EndOfStream()
            {
                return _streamReader.EndOfStream;
            }

            public string Gn()
            {
                string line = _streamReader.ReadLine();
                LineNumber++;
                while ((line.Trim().Length == 0) && !_streamReader.EndOfStream)
                {
                    line = _streamReader.ReadLine();
                    LineNumber++;
                }

                Next = line;
                return line;
            }

        }

    }
}
