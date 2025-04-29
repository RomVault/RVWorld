using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

namespace ROMVault
{
    internal static class Report
    {
        private static StreamWriter _ts;

        private static int _fileNameLength;
        private static int _fileSizeLength;
        private static int _repStatusLength;

        private static readonly RepStatus[] Partial =
        {
            RepStatus.UnScanned,
            RepStatus.Missing,
            RepStatus.Corrupt,
            RepStatus.CanBeFixed,
            RepStatus.CanBeFixedMIA,
            RepStatus.CorruptCanBeFixed
        };

        private static readonly RepStatus[] Fixing =
        {
            RepStatus.CanBeFixed,
            RepStatus.CanBeFixedMIA,
            RepStatus.MoveToSort,
            RepStatus.Delete,
            RepStatus.NeededForFix,
            RepStatus.Rename,
            RepStatus.CorruptCanBeFixed,
            RepStatus.MoveToCorrupt
        };

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
                FindAllDats(DB.DirRoot.Child(0), ReportType.Complete);
                _ts.WriteLine("");
                _ts.WriteLine("");
                _ts.WriteLine("Empty DAT Sets");
                _ts.WriteLine("-----------------------------------------");
                FindAllDats(DB.DirRoot.Child(0), ReportType.CompletelyMissing);
                _ts.WriteLine("");
                _ts.WriteLine("");
                _ts.WriteLine("Partial DAT Sets - (Listing Missing ROMs)");
                _ts.WriteLine("-----------------------------------------");
                FindAllDats(DB.DirRoot.Child(0), ReportType.PartialMissing);
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
                FindAllDats(DB.DirRoot.Child(0), ReportType.Fixing);
                _ts.Close();
            }
        }

        private static void FindAllDats(RvFile b, ReportType rt)
        {
            RvFile d = b;
            if (!d.IsDirectory)
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

                            if (!c.IsDirectory || c.Dat != dat)
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
                if (d.IsDirectory)
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
                if (d.IsDirectory)
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