using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ROMVault.Utils;
using RVCore;
using RVCore.RvDB;
using RVCore.Utils;

namespace ROMVault
{
    internal static class Report
    {
        private static StreamWriter _ts;
        private static RvDat _tDat;
        private static string _outdir;
        private static string _dfn;
        private static int _rpy; // count of red plus yellow items per fixdat file
        private static int _ro; // count of red items only per fixdat file

        private static int _fileNameLength;
        private static int _fileSizeLength;
        private static int _repStatusLength;

        private static readonly RepStatus[] Partial =
        {
            RepStatus.UnScanned,
            RepStatus.Missing,
            RepStatus.Corrupt,
            RepStatus.CanBeFixed,
            RepStatus.CorruptCanBeFixed
        };

        private static readonly RepStatus[] Fixing =
        {
            RepStatus.CanBeFixed,
            RepStatus.MoveToSort,
            RepStatus.Delete,
            RepStatus.NeededForFix,
            RepStatus.Rename,
            RepStatus.CorruptCanBeFixed,
            RepStatus.MoveToCorrupt
        };

        private static string Etxt(string e)
        {
            string ret = e;
            ret = ret.Replace("&", "&amp;");
            ret = ret.Replace("\"", "&quot;");
            ret = ret.Replace("'", "&apos;");
            ret = ret.Replace("<", "&lt;");
            ret = ret.Replace(">", "&gt;");

            return ret;
        }

        // check report, remove games without either rom or disk content, delete reports without any game content
        public static void scrub()
        {
            if (_ro < _rpy) // don't bother unless there is a difference
            {
                List<string> res = new List<string>();
                List<string> game = new List<string>();
                int gameCount = 0, itemCount = 0;
                bool inHeader = true;
                foreach (string line in File.ReadAllLines(_dfn))
                {
                    if (inHeader)
                    {
                        res.Add(line);
                        inHeader = !line.Contains("</header>");
                        if (!inHeader)
                        {
                            int rc = _rpy - _ro;
                            res.Insert(res.Count - 1, "\t\t<comment>Excludes " + rc + " item" + (rc == 0 ? "" : "s") + " that could be fixed</comment>");
                        }
                    }
                    else
                    {
                        if (line.Contains("</datafile>"))
                        {
                            res.Add(line);
                            File.Delete(_dfn);
                            if (gameCount > 0)
                            {
                                File.WriteAllLines(_dfn, res.ToArray());
                            }
                        }
                        else
                        {
                            if (line.Contains("<game "))
                            {
                                game = new List<string> { line };
                            }
                            else if (line.Contains("</game>"))
                            {
                                if (itemCount > 0)
                                {
                                    game.Add(line);
                                    res.AddRange(game);
                                    gameCount++;
                                }
                                itemCount = 0;
                            }
                            else if (line.Contains("<description>"))
                            {
                                game.Add(line);
                            }
                            else if (line.Contains("<rom ") || line.Contains("<disk "))
                            {
                                game.Add(line);
                                itemCount++;
                            }
                        }
                    }
                }
            }
        }

        public static void MakeFixFiles(RvFile root=null, bool scrubIt = true)
        {
            _tDat = null;
            _ts = null;

            FolderBrowserDialog browse = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                Description = @"Please select fixdat files destination. NOTE: " + (scrubIt ? @"reports will include Red items only (omitting any Yellow that may be present)" : @"reports will include both Red and Yellow items"),
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Reports")
            };

            if (browse.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _outdir = browse.SelectedPath;
            _tDat = null;

            if (root == null)
                root = DB.DirTree.Child(0);

            MakeFixFilesRecurse(root, true, scrubIt);

            if (_ts == null)
            {
                return;
            }

            _ts.WriteLine("</datafile>");
            _ts.Close();
            if (scrubIt)
            {
                scrub();
            }
        }

        private static void MakeFixFilesRecurse(RvFile b, bool selected, bool scrubIt)
        {
            if (selected)
            {
                if (b.Dat != null)
                {
                    RvFile tDir = b;
                    if (tDir.IsDir && tDir.Game != null && tDir.DirStatus.HasMissing())
                    {
                        if (_tDat != b.Dat)
                        {
                            if (_tDat != null)
                            {
                                _ts.WriteLine("</datafile>");
                                _ts.WriteLine();
                            }

                            if (_ts != null)
                            {
                                _ts.Close();
                                if (scrubIt)
                                {
                                    scrub();
                                }
                            }

                            _tDat = b.Dat;
                            int test = 0;
                            string datFilename = Path.Combine(_outdir, "fixDat_" + Path.GetFileNameWithoutExtension(_tDat.GetData(RvDat.DatData.DatRootFullName)) + ".dat");
                            while (File.Exists(datFilename))
                            {
                                test++;
                                datFilename = Path.Combine(_outdir, "fixDat_" + Path.GetFileNameWithoutExtension(_tDat.GetData(RvDat.DatData.DatRootFullName)) + "(" + test + ").dat");
                            }
                            _ts = new StreamWriter(datFilename);
                            _dfn = datFilename;
                            _rpy = 0;
                            _ro = 0;

                            _ts.WriteLine("<?xml version=\"1.0\"?>");
                            _ts.WriteLine(
                                "<!DOCTYPE datafile PUBLIC \"-//Logiqx//DTD ROM Management Datafile//EN\" \"http://www.logiqx.com/Dats/datafile.dtd\">");
                            _ts.WriteLine("");
                            _ts.WriteLine("<datafile>");
                            _ts.WriteLine("\t<header>");
                            _ts.WriteLine("\t\t<name>fix_" + Etxt(_tDat.GetData(RvDat.DatData.DatName)) + "</name>");
                            if (_tDat.GetData(RvDat.DatData.SuperDat) == "superdat")
                            {
                                _ts.WriteLine("\t\t<type>SuperDAT</type>");
                            }

                            string description = _tDat.GetData(RvDat.DatData.Description);
                            if (!string.IsNullOrWhiteSpace(description))
                            {
                                _ts.WriteLine("\t\t<description>fix_" + Etxt(description) + "</description>");
                            }
                            
                            _ts.WriteLine("\t\t<category>FIXDATFILE</category>");
                            _ts.WriteLine("\t\t<version>" + DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "</version>");
                            _ts.WriteLine("\t\t<date>" + DateTime.Now.ToString("MM/dd/yyyy") + "</date>");
                            _ts.WriteLine("\t\t<author>RomVault</author>");
                            _ts.WriteLine("\t</header>");
                        }


                        string gamename = tDir.SuperDatFileName();
                        if (tDir.FileType == FileType.Zip || tDir.FileType == FileType.SevenZip)
                            gamename = Path.GetFileNameWithoutExtension(gamename);

                        _ts.WriteLine("\t<game name=\"" + Etxt(gamename) + "\">");
                        if (!string.IsNullOrEmpty(tDir.Game.GetData(RvGame.GameData.Description)))
                        {
                            _ts.WriteLine("\t\t<description>" + Etxt(tDir.Game.GetData(RvGame.GameData.Description)) + "</description>");
                        }
                    }

                    RvFile tRom = b;
                    if (tRom.IsFile)
                    {
                        if (tRom.DatStatus == DatStatus.InDatCollect && tRom.GotStatus != GotStatus.Got)
                        {
                            _rpy++;
                        }
                        if (tRom.DatStatus == DatStatus.InDatCollect && tRom.GotStatus != GotStatus.Got && !(tRom.RepStatus == RepStatus.CanBeFixed || tRom.RepStatus == RepStatus.CorruptCanBeFixed))
                        {
                            _ro++;
                            string strRom;
                            if (tRom.FileStatusIs(FileStatus.AltSHA1FromDAT) || tRom.FileStatusIs(FileStatus.AltMD5FromDAT))
                            {
                                strRom = "\t\t<disk name=\"" + Etxt(Path.GetFileNameWithoutExtension(tRom.Name)) + "\"";
                            }
                            else
                            {
                                strRom = "\t\t<rom name=\"" + Etxt(tRom.Name) + "\"";
                            }

                            if (tRom.FileStatusIs(FileStatus.SizeFromDAT) && tRom.Size != null)
                            {
                                strRom += " size=\"" + tRom.Size + "\"";
                            }

                            string strCRC = tRom.CRC.ToHexString();
                            if (tRom.FileStatusIs(FileStatus.CRCFromDAT) && !string.IsNullOrEmpty(strCRC))
                            {
                                strRom += " crc=\"" + strCRC + "\"";
                            }

                            string strSHA1 = tRom.SHA1.ToHexString();
                            if (tRom.FileStatusIs(FileStatus.SHA1FromDAT) && !string.IsNullOrEmpty(strSHA1))
                            {
                                strRom += " sha1=\"" + strSHA1 + "\"";
                            }

                            string strMD5 = tRom.MD5.ToHexString();
                            if (tRom.FileStatusIs(FileStatus.MD5FromDAT) && !string.IsNullOrEmpty(strMD5))
                            {
                                strRom += " md5=\"" + strMD5 + "\"";
                            }

                            string strSHA1CHD = tRom.AltSHA1.ToHexString();
                            if (tRom.FileStatusIs(FileStatus.AltSHA1FromDAT) && !string.IsNullOrEmpty(strSHA1CHD))
                            {
                                strRom += " sha1=\"" + strSHA1CHD + "\"";
                            }

                            string strMD5CHD = tRom.AltMD5.ToHexString();
                            if (tRom.FileStatusIs(FileStatus.AltMD5FromDAT) && !string.IsNullOrEmpty(strMD5CHD))
                            {
                                strRom += " md5=\"" + strMD5CHD + "\"";
                            }

                            strRom += "/>";

                            _ts.WriteLine(strRom);
                        }
                    }
                }
            }

            RvFile d = b;
            if (d.IsDir)
            {
                for (int i = 0; i < d.ChildCount; i++)
                {
                    bool nextSelected = selected;
                    if (d.Tree != null)
                    {
                        nextSelected = d.Tree.Checked == RvTreeRow.TreeSelect.Selected;
                    }
                    MakeFixFilesRecurse(d.Child(i), nextSelected, scrubIt);
                }
            }

            if (selected)
            {
                if (b.Dat != null)
                {
                    RvFile tDir = b;
                    if (tDir.IsDir && tDir.Game != null && tDir.DirStatus.HasMissing())
                    {
                        _ts.WriteLine("\t</game>");
                    }
                }
            }
        }

        private static string CleanTime()
        {
            return " (" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ")";
        }

        public static void GenerateReport()
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                Title = @"Generate Full Report",
                FileName = @"RVFullReport" + CleanTime() + ".txt",
                Filter = @"Rom Vault Report (*.txt)|*.txt|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                _ts = new StreamWriter(saveFileDialog1.FileName);

                _ts.WriteLine("Complete DAT Sets");
                _ts.WriteLine("-----------------------------------------");
                FindAllDats(DB.DirTree.Child(0), ReportType.Complete);
                _ts.WriteLine("");
                _ts.WriteLine("");
                _ts.WriteLine("Empty DAT Sets");
                _ts.WriteLine("-----------------------------------------");
                FindAllDats(DB.DirTree.Child(0), ReportType.CompletelyMissing);
                _ts.WriteLine("");
                _ts.WriteLine("");
                _ts.WriteLine("Partial DAT Sets - (Listing Missing ROMs)");
                _ts.WriteLine("-----------------------------------------");
                FindAllDats(DB.DirTree.Child(0), ReportType.PartialMissing);
                _ts.Close();
            }
        }

        public static void GenerateFixReport()
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                Title = @"Generate Fix Report",
                FileName = @"RVFixReport" + CleanTime() + ".txt",
                Filter = @"Rom Vault Fixing Report (*.txt)|*.txt|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                _ts = new StreamWriter(saveFileDialog1.FileName);

                _ts.WriteLine("Listing Fixes");
                _ts.WriteLine("-----------------------------------------");
                FindAllDats(DB.DirTree.Child(0), ReportType.Fixing);
                _ts.Close();
            }
        }

        private static void FindAllDats(RvFile b, ReportType rt)
        {
            RvFile d = b;
            if (!d.IsDir)
            {
                return;
            }
            if (d.DirDatCount > 0)
            {
                for (int i = 0; i < d.DirDatCount; i++)
                {
                    RvDat dat = d.DirDat(i);

                    int correct = 0;
                    int missing = 0;
                    int fixesNeeded = 0;

                    if (d.Dat == dat)
                    {
                        correct += d.DirStatus.CountCorrect();
                        missing += d.DirStatus.CountMissing();
                        fixesNeeded += d.DirStatus.CountFixesNeeded();
                    }
                    else
                    {
                        for (int j = 0; j < d.ChildCount; j++)
                        {
                            RvFile c = d.Child(j);

                            if (!c.IsDir || c.Dat != dat)
                            {
                                continue;
                            }

                            correct += c.DirStatus.CountCorrect();
                            missing += c.DirStatus.CountMissing();
                            fixesNeeded += c.DirStatus.CountFixesNeeded();
                        }
                    }

                    switch (rt)
                    {
                        case ReportType.Complete:
                            if (correct > 0 && missing == 0 && fixesNeeded == 0)
                            {
                                _ts.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                            }
                            break;
                        case ReportType.CompletelyMissing:
                            if (correct == 0 && missing > 0 && fixesNeeded == 0)
                            {
                                _ts.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                            }
                            break;
                        case ReportType.PartialMissing:
                            if (correct > 0 && missing > 0 || fixesNeeded > 0)
                            {
                                _ts.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                                _fileNameLength = 0;
                                _fileSizeLength = 0;
                                _repStatusLength = 0;
                                ReportMissingFindSizes(d, dat, rt);
                                ReportDrawBars();
                                ReportMissing(d, dat, rt);
                                ReportDrawBars();
                                _ts.WriteLine();
                            }
                            break;
                        case ReportType.Fixing:
                            if (fixesNeeded > 0)
                            {
                                _ts.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                                _fileNameLength = 0;
                                _fileSizeLength = 0;
                                _repStatusLength = 0;
                                ReportMissingFindSizes(d, dat, rt);
                                ReportDrawBars();
                                ReportMissing(d, dat, rt);
                                ReportDrawBars();
                                _ts.WriteLine();
                            }
                            break;
                    }
                }
            }

            if (b.Dat != null)
            {
                return;
            }

            for (int i = 0; i < d.ChildCount; i++)
            {
                FindAllDats(d.Child(i), rt);
            }
        }

        private static string RemoveBase(string name)
        {
            int p = name.IndexOf("\\", StringComparison.Ordinal);
            return p > 0 ? name.Substring(p + 1) : name;
        }


        private static void ReportMissingFindSizes(RvFile dir, RvDat dat, ReportType rt)
        {
            for (int i = 0; i < dir.ChildCount; i++)
            {
                RvFile b = dir.Child(i);
                if (b.Dat != null && b.Dat != dat)
                {
                    continue;
                }

                RvFile f = b;

                if (f.IsFile)
                {
                    if (
                        rt == ReportType.PartialMissing && Partial.Contains(f.RepStatus) ||
                        rt == ReportType.Fixing && Fixing.Contains(f.RepStatus)
                    )
                    {
                        int fileNameLength = f.FileNameInsideGame().Length;
                        int fileSizeLength = f.Size.ToString().Length;
                        int repStatusLength = f.RepStatus.ToString().Length;

                        if (fileNameLength > _fileNameLength)
                        {
                            _fileNameLength = fileNameLength;
                        }
                        if (fileSizeLength > _fileSizeLength)
                        {
                            _fileSizeLength = fileSizeLength;
                        }
                        if (repStatusLength > _repStatusLength)
                        {
                            _repStatusLength = repStatusLength;
                        }
                    }
                }
                RvFile d = b;
                if (d.IsDir)
                {
                    ReportMissingFindSizes(d, dat, rt);
                }
            }
        }

        private static void ReportDrawBars()
        {
            _ts.WriteLine("+" + new string('-', _fileNameLength + 2) + "+" + new string('-', _fileSizeLength + 2) + "+----------+" + new string('-', _repStatusLength + 2) + "+");
        }

        private static void ReportMissing(RvFile dir, RvDat dat, ReportType rt)
        {
            for (int i = 0; i < dir.ChildCount; i++)
            {
                RvFile b = dir.Child(i);
                if (b.Dat != null && b.Dat != dat)
                {
                    continue;
                }

                RvFile f = b;

                if (f.IsFile)
                {
                    if (
                        rt == ReportType.PartialMissing && Partial.Contains(f.RepStatus) ||
                        rt == ReportType.Fixing && Fixing.Contains(f.RepStatus)
                    )
                    {
                        string filename = f.FileNameInsideGame();
                        string crc = f.CRC.ToHexString();
                        _ts.WriteLine("| " + filename + new string(' ', _fileNameLength + 1 - filename.Length) + "| "
                                      + f.Size + new string(' ', _fileSizeLength + 1 - f.Size.ToString().Length) + "| "
                                      + crc + new string(' ', 9 - crc.Length) + "| "
                                      + f.RepStatus + new string(' ', _repStatusLength + 1 - f.RepStatus.ToString().Length) + "|");
                    }
                }
                RvFile d = b;
                if (d.IsDir)
                {
                    ReportMissing(d, dat, rt);
                }
            }
        }

        private enum ReportType
        {
            Complete,
            CompletelyMissing,
            PartialMissing,
            Fixing
        }
    }
}