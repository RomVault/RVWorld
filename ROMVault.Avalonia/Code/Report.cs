using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

namespace ROMVault.Avalonia.Code
{
    /// <summary>
    /// Provides functionality for generating various reports about the ROM collection.
    /// </summary>
    public static class Report
    {
        private static StreamWriter? _ts;

        private static int _fileNameLength;
        private static int _fileSizeLength;
        private static int _repStatusLength;

        /// <summary>
        /// Statuses considered for partial missing reports.
        /// </summary>
        private static readonly RepStatus[] Partial =
        {
            RepStatus.UnScanned,
            RepStatus.Missing,
            RepStatus.Corrupt,
            RepStatus.CanBeFixed,
            RepStatus.CanBeFixedMIA,
            RepStatus.CorruptCanBeFixed
        };

        /// <summary>
        /// Statuses considered for fixing reports.
        /// </summary>
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

        /// <summary>
        /// Generates a clean timestamp string for filenames.
        /// </summary>
        /// <returns>A formatted date string.</returns>
        private static string CleanTime()
        {
            return " (" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ")";
        }

        /// <summary>
        /// Generates a full report of the collection status.
        /// </summary>
        /// <param name="parent">The parent window for the file picker dialog.</param>
        public static async Task GenerateReport(Window parent)
        {
            var topLevel = TopLevel.GetTopLevel(parent);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Generate Full Report",
                SuggestedFileName = "RVFullReport" + CleanTime() + ".txt",
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Rom Vault Report") { Patterns = new[] { "*.txt" } },
                    FilePickerFileTypes.All
                }
            });

            if (file != null)
            {
                using (var stream = await file.OpenWriteAsync())
                using (_ts = new StreamWriter(stream))
                {
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
                }
                _ts = null;
            }
        }

        /// <summary>
        /// Generates a report specifically for fixable issues.
        /// </summary>
        /// <param name="parent">The parent window for the file picker dialog.</param>
        public static async Task GenerateFixReport(Window parent)
        {
            var topLevel = TopLevel.GetTopLevel(parent);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Generate Fix Report",
                SuggestedFileName = "RVFixReport" + CleanTime() + ".txt",
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Rom Vault Report") { Patterns = new[] { "*.txt" } },
                    FilePickerFileTypes.All
                }
            });

            if (file != null)
            {
                using (var stream = await file.OpenWriteAsync())
                using (_ts = new StreamWriter(stream))
                {
                    _ts.WriteLine("Listing Fixes");
                    _ts.WriteLine("-----------------------------------------");
                    FindAllDats(DB.DirRoot.Child(0), ReportType.Fixing);
                }
                _ts = null;
            }
        }

        /// <summary>
        /// Creates fix DAT files for the selected directory.
        /// </summary>
        /// <param name="parent">The parent window for the folder picker dialog.</param>
        /// <param name="baseDir">The base directory to start creating fix DATs from.</param>
        /// <param name="redOnly">If true, only includes missing items (red status).</param>
        public static async Task CreateFixDat(Window parent, RvFile baseDir, bool redOnly)
        {
            var topLevel = TopLevel.GetTopLevel(parent);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Please select fixdat files destination",
                AllowMultiple = false
            });

            if (folders.Count == 0) return;

            string selectedPath = folders[0].Path.LocalPath;

            if (selectedPath != Settings.rvSettings.FixDatOutPath)
            {
                Settings.rvSettings.FixDatOutPath = selectedPath;
                Settings.WriteConfig(Settings.rvSettings);
            }

            FixDatReport.RecursiveDatTree(Settings.rvSettings.FixDatOutPath, baseDir, redOnly);
        }

        /// <summary>
        /// Recursively traverses the directory tree to generate report data for a specific DAT.
        /// </summary>
        /// <param name="b">The current directory or file being processed.</param>
        /// <param name="rt">The type of report to generate.</param>
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
                                _ts?.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                            }
                            break;
                        case ReportType.CompletelyMissing:
                            if (correct == 0 && missing > 0 && fixesNeeded == 0)
                            {
                                _ts?.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                            }
                            break;
                        case ReportType.PartialMissing:
                            if (correct > 0 && missing > 0 || fixesNeeded > 0)
                            {
                                _ts?.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                                _fileNameLength = 0;
                                _fileSizeLength = 0;
                                _repStatusLength = 0;
                                ReportMissingFindSizes(d, dat, rt);
                                ReportDrawBars();
                                ReportMissing(d, dat, rt);
                                ReportDrawBars();
                                _ts?.WriteLine();
                            }
                            break;
                        case ReportType.Fixing:
                            if (fixesNeeded > 0)
                            {
                                _ts?.WriteLine(RemoveBase(dat.GetData(RvDat.DatData.DatRootFullName)));
                                _fileNameLength = 0;
                                _fileSizeLength = 0;
                                _repStatusLength = 0;
                                ReportMissingFindSizes(d, dat, rt);
                                ReportDrawBars();
                                ReportMissing(d, dat, rt);
                                ReportDrawBars();
                                _ts?.WriteLine();
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

        /// <summary>
        /// Removes the base directory from a path string.
        /// </summary>
        /// <param name="name">The full path.</param>
        /// <returns>The path relative to the base directory.</returns>
        private static string RemoveBase(string name)
        {
            int p = name.IndexOf("\\", StringComparison.Ordinal);
            return p > 0 ? name.Substring(p + 1) : name;
        }

        /// <summary>
        /// Calculates the maximum column widths for the report table by scanning the files.
        /// </summary>
        /// <param name="dir">The directory to scan.</param>
        /// <param name="dat">The DAT file associated with the report.</param>
        /// <param name="rt">The report type.</param>
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
                        string sizeStr = f.Size?.ToString() ?? "0";
                        int fileSizeLength = sizeStr.Length;
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

        /// <summary>
        /// Writes the horizontal separator bars for the report table.
        /// </summary>
        private static void ReportDrawBars()
        {
            _ts?.WriteLine("+" + new string('-', _fileNameLength + 2) + "+" + new string('-', _fileSizeLength + 2) + "+----------+" + new string('-', _repStatusLength + 2) + "+");
        }

        /// <summary>
        /// Writes the missing file details to the report.
        /// </summary>
        /// <param name="dir">The directory to scan.</param>
        /// <param name="dat">The DAT file associated with the report.</param>
        /// <param name="rt">The report type.</param>
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
                        string sizeStr = f.Size?.ToString() ?? "0";
                        _ts?.WriteLine("| " + filename + new string(' ', _fileNameLength + 1 - filename.Length) + "| "
                                      + sizeStr + new string(' ', _fileSizeLength + 1 - sizeStr.Length) + "| "
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

        /// <summary>
        /// Defines the types of reports that can be generated.
        /// </summary>
        private enum ReportType
        {
            /// <summary>
            /// Report for fully complete DAT sets.
            /// </summary>
            Complete,
            /// <summary>
            /// Report for completely missing DAT sets.
            /// </summary>
            CompletelyMissing,
            /// <summary>
            /// Report for DAT sets with some missing files.
            /// </summary>
            PartialMissing,
            /// <summary>
            /// Report for items that need fixing.
            /// </summary>
            Fixing
        }
    }
}
