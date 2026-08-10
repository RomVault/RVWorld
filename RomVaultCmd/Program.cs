using System;
using System.Reflection;
using System.Threading;
using RomVaultCore;
using RomVaultCore.FindFix;
using RomVaultCore.FixFile;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RomVaultCore.Utils;

namespace RomVaultCmd
{
    class Program
    {

        private static ThreadWorker _thWrk;

        private static bool doUpdateDATs = false;
        private static bool doScanROMs = false;
        private static bool doFindFixes = false;
        private static bool doFixROMs = false;

        static void Main(string[] args)
        {
            // Maintenance commands run before the normal worker startup, so
            // load persisted CHD tool paths, pins, and policy here as well.
            Settings.rvSettings = Settings.SetDefaults(out _);
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            if (args.Length >= 2 && string.Equals(args[0], "-verifychd", StringComparison.OrdinalIgnoreCase))
            {
                string chdPath = args[1];
                string outPath = args.Length >= 3 ? args[2] : null;
                Environment.ExitCode = ChdVerify.Verify(chdPath, outPath);
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "-chdhealth", StringComparison.OrdinalIgnoreCase))
            {
                string chdPath = args[1];
                string outPath = args.Length >= 3 ? args[2] : null;
                Environment.ExitCode = ChdVerify.Health(chdPath, outPath);
                return;
            }
            if (args.Length >= 1 && string.Equals(args[0], "-testchdman", StringComparison.OrdinalIgnoreCase))
            {
                string executable = args.Length >= 2 ? args[1] : null;
                Environment.ExitCode = ChdmanDiagnostics.RunPreservationMatrix(executable, out string matrixReport);
                Console.WriteLine(matrixReport);
                return;
            }
            if (args.Length >= 1 && string.Equals(args[0], "-testchdmans", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = ChdmanDiagnostics.RunCrossVersionMatrix(args.Length > 1 ? args[1..] : Array.Empty<string>(), out string matrixReport);
                Console.WriteLine(matrixReport);
                return;
            }
            if (args.Length >= 1 && string.Equals(args[0], "-chdtools", StringComparison.OrdinalIgnoreCase))
            {
                bool full = args.Length >= 2 && string.Equals(args[1], "full", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine(ChdmanDiagnostics.GetToolchainReport(full));
                return;
            }
            if (args.Length >= 3 && string.Equals(args[0], "-chdscrub", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse(args[1], true, out ChdScrubMode scrubMode))
                {
                    Console.WriteLine("Scrub mode must be container, native, or full.");
                    Environment.ExitCode = 2;
                    return;
                }
                Environment.ExitCode = ChdScrubber.Scrub(args[2], scrubMode, out string scrubReport);
                EmitReport(scrubReport, args.Length >= 4 ? args[3] : null);
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "-chdparent", StringComparison.OrdinalIgnoreCase))
            {
                string action = args[1].ToLowerInvariant();
                string parentReport;
                if (action == "verify" && args.Length == 4)
                    Environment.ExitCode = ChdParentGraph.Verify(args[2], args[3], out parentReport);
                else if (action == "create" && args.Length == 5)
                    Environment.ExitCode = ChdParentGraph.CreateChild(args[2], args[3], args[4], out parentReport);
                else if (action == "standalone" && args.Length == 5)
                    Environment.ExitCode = ChdParentGraph.MaterializeStandalone(args[2], args[3], args[4], out parentReport);
                else if (action == "graph" && args.Length >= 3)
                    Environment.ExitCode = ChdParentGraph.ValidateGraph(args[2..], out parentReport);
                else
                {
                    parentReport = "Usage: -chdparent verify <child> <parent> | create <standalone> <parent> <output> | standalone <child> <parent> <output> | graph <chd...>";
                    Environment.ExitCode = 2;
                }
                Console.WriteLine(parentReport);
                return;
            }
            if (args.Length == 1 && string.Equals(args[0], "-chdhealthdb", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(ChdHealthStore.BuildReport());
                return;
            }
            if (args.Length >= 3 && string.Equals(args[0], "-chdschedule", StringComparison.OrdinalIgnoreCase))
            {
                string action = args[1].ToLowerInvariant();
                int limit = args.Length >= 4 && int.TryParse(args[3], out int parsedLimit) ? parsedLimit : Settings.rvSettings.ChdScheduledScrubBatch;
                if (action == "plan")
                {
                    Environment.ExitCode = ChdScrubScheduler.Plan(args[2], Settings.rvSettings.ChdExternalParityDays, limit, out _, out string scheduleReport);
                    Console.WriteLine(scheduleReport);
                }
                else if (action == "run")
                {
                    Environment.ExitCode = ChdScrubScheduler.RunDue(args[2], ChdScrubMode.Full, Settings.rvSettings.ChdExternalParityDays, limit, out string scheduleReport);
                    Console.WriteLine(scheduleReport);
                }
                else
                {
                    Console.WriteLine("Usage: -chdschedule <plan|run> <root> [maximum-items]");
                    Environment.ExitCode = 2;
                }
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "-chdparity", StringComparison.OrdinalIgnoreCase))
            {
                string action = args[1].ToLowerInvariant();
                string parityReport;
                if (action == "create" && args.Length >= 4)
                {
                    int groupSize = args.Length >= 5 && int.TryParse(args[4], out int parsedGroup) ? parsedGroup : 8;
                    int blockKiB = args.Length >= 6 && int.TryParse(args[5], out int parsedBlock) ? parsedBlock : 1024;
                    Environment.ExitCode = ChdCollectionParity.Create(args[2], args[3], groupSize, checked(blockKiB * 1024), out parityReport);
                }
                else if (action == "verify" && args.Length == 4)
                    Environment.ExitCode = ChdCollectionParity.Verify(args[2], args[3], out parityReport);
                else if (action == "repair" && args.Length == 5)
                    Environment.ExitCode = ChdCollectionParity.Repair(args[2], args[3], args[4], out parityReport);
                else
                {
                    parityReport = "Usage: -chdparity create <collection> <volume> [group-size] [block-KiB] | verify <volume> <collection> | repair <volume> <collection> <output>";
                    Environment.ExitCode = 2;
                }
                Console.WriteLine(parityReport);
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "-chdconvert", StringComparison.OrdinalIgnoreCase))
            {
                string action = args[1].ToLowerInvariant();
                string conversionReport;
                if (action == "plan" && args.Length == 5 && Enum.TryParse(args[2], true, out ChdStorageProfile target))
                    Environment.ExitCode = ChdConversionQueue.Create(args[3], target, args[4], out conversionReport);
                else if (action == "run" && args.Length >= 3)
                {
                    int limit = args.Length >= 4 && int.TryParse(args[3], out int parsedLimit) ? parsedLimit : int.MaxValue;
                    Environment.ExitCode = ChdConversionQueue.Run(args[2], limit, out conversionReport);
                }
                else if (action == "status" && args.Length == 3)
                    Environment.ExitCode = ChdConversionQueue.Status(args[2], out conversionReport);
                else
                {
                    conversionReport = "Usage: -chdconvert plan <playback|archive> <root> <queue.rvchdqueue> | run <queue.rvchdqueue> [limit] | status <queue.rvchdqueue>";
                    Environment.ExitCode = 2;
                }
                Console.WriteLine(conversionReport);
                return;
            }

            foreach (string arg in args)
            {
                bool isflag = arg.Substring(0, 1) == "-";
                if (isflag)
                {
                    string flag = arg.Substring(1).ToLower();
                    switch (flag)
                    {
                        case "help":
                        case "h":
                        case "?":
                            ShowHelp();
                            return;
                        case "update":
                        case "u":
                            doUpdateDATs = true;
                            break;
                        case "scan":
                        case "s":
                            doScanROMs = true;
                            break;
                        case "fix":
                        case "f":
                            doFindFixes = true;
                            doFixROMs = true;
                            break;
                        case "all":
                        case "a":
                            doUpdateDATs = true;
                            doScanROMs = true;
                            doFindFixes = true;
                            doFixROMs = true;
                            break;
                        case "scanfix":
                        case "sf":
                            doScanROMs = true;
                            doFindFixes = true;
                            doFixROMs = true;
                            break;
                        default:
                            Console.WriteLine("Unknown arg: " + arg);
                            return;
                    }

                }
                else
                {
                    Console.WriteLine("Unknown arg: " + arg);
                    return;
                }
            }

            if (!doUpdateDATs && !doScanROMs && !doFindFixes && !doFixROMs)
            {
                ShowHelp();
                return;
            }

            DoWork();
        }

        private static void ShowHelp()
        {
            Console.WriteLine($"RomVault v{Assembly.GetEntryAssembly().GetName().Version.ToString(3)} Commandline");
            Console.WriteLine("");
            Console.WriteLine("Copyright (C) 2026 GordonJ");
            Console.WriteLine("Homepage : https://www.romvault.com/");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("");
            Console.WriteLine("-help    -?  : Show this help");
            Console.WriteLine("-update  -u  : Update DATs");
            Console.WriteLine("-scan    -s  : Scan ROMs");
            Console.WriteLine("-fix     -f  : FindFixes / FixROMs");
            Console.WriteLine("-all     -a  : All of the above");
            Console.WriteLine("-scanfix -sf : Scan ROMs / FindFixes / FixROMs");
            Console.WriteLine("-verifychd   : Verify CHD by extracting and hashing contents");
            Console.WriteLine("              RomVaultCmd -verifychd <path-to-chd> [optional-output-file]");
            Console.WriteLine("-chdhealth   : Unified CHD health report (container + scan + parity summary)");
            Console.WriteLine("              RomVaultCmd -chdhealth <path-to-chd> [optional-output-file]");
            Console.WriteLine("-testchdman  : Run Playback/Archive CD/GDI/DVD/PSP/Raw/HDD/LaserDisc and preservation tests");
            Console.WriteLine("              RomVaultCmd -testchdman [optional-path-to-chdman]");
            Console.WriteLine("-testchdmans : Run the preservation matrix across multiple chdman binaries");
            Console.WriteLine("              RomVaultCmd -testchdmans <chdman-path> [more-paths...]");
            Console.WriteLine("-chdtools    : List discovered/pinned chdman binaries; add 'full' for capability probes");
            Console.WriteLine("-chdscrub    : Scrub one CHD or a directory: <container|native|full> <path> [report]");
            Console.WriteLine("-chdparent   : Verify/create/materialize/validate optional parent CHD graphs");
            Console.WriteLine("-chdhealthdb : Show the persistent redacted CHD health database summary");
            Console.WriteLine("-chdschedule : Plan or run risk-prioritized due full scrubs: <plan|run> <root> [limit]");
            Console.WriteLine("-chdparity   : Create, verify, or recover from two-shard collection recovery volumes");
            Console.WriteLine("-chdconvert  : Create, resume, or inspect a transactional Playback/Archive conversion queue");
        }

        private static void EmitReport(string report, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                Console.WriteLine(report);
            else
            {
                System.IO.File.WriteAllText(outputPath, report ?? "");
                Console.WriteLine("Wrote " + outputPath);
            }
        }

        private static void DoWork()
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(DoCleanShutdown);

            Settings.rvSettings = new Settings();

            _thWrk = new ThreadWorker(StartUpCode) { wReport = BgwProgressChanged };
            _thWrk.Start();
            Console.WriteLine("");
            Console.WriteLine("");

            if (doUpdateDATs)
            {
                _thWrk = new ThreadWorker(DatUpdate.UpdateDat) { wReport = BgwProgressChanged };
                _thWrk.Start();
                Console.WriteLine("");
                Console.WriteLine("");
            }

            if (doScanROMs)
            {
                FileScanning.StartAt = null;
                FileScanning.EScanLevel = EScanLevel.Level2;
                _thWrk = new ThreadWorker(FileScanning.ScanFiles) { wReport = BgwProgressChanged };
                _thWrk.Start();
                Console.WriteLine("");
                Console.WriteLine("");
            }

            if (doFindFixes)
            {
                _thWrk = new ThreadWorker(FindFixes.ScanFiles) { wReport = BgwProgressChanged };
                _thWrk.Start();
                Console.WriteLine("");
                Console.WriteLine("");
            }

            if (doFixROMs)
            {
                _thWrk = new ThreadWorker(Fix.PerformFixes) { wReport = BgwProgressChanged };
                _thWrk.Start();
                Console.WriteLine("");
                Console.WriteLine("");
            }

        }


        private static void StartUpCode(ThreadWorker thWrk)
        {
            RepairStatus.InitStatusCheck();
            Settings.rvSettings = Settings.SetDefaults(out _);
            string chdRecoveryReport = ChdUpgradeRecovery.RecoverPending();
            if (!string.IsNullOrWhiteSpace(chdRecoveryReport))
                Console.WriteLine(chdRecoveryReport);
            DB.Read(thWrk);
        }



        private static void BgwProgressChanged(object e)
        {
            if (e is int percent)
            {
                Console.WriteLine($"{e}");
                return;
            }

            if (e is bgwText bgwT)
            {
                Console.WriteLine($"{bgwT.Text}");
                return;
            }
            if (e is bgwText2 bgwT2)
            {
                Console.WriteLine($"{bgwT2.Text}");
                return;
            }
            if (e is bgwText3 bgwt3)
            {
                return;
            }

            if (e is bgwShowFix bgwSF)
            {
                Console.WriteLine($"{bgwSF.Dir} , {bgwSF.FixDir} , {bgwSF.FixZip} , {bgwSF.FixFile} , {bgwSF.Size} , {bgwSF.SourceDir} , {bgwSF.SourceZip} , {bgwSF.SourceFile}");
                return;
            }

            if (e is bgwSetRange2 bgwsr2)
            {
                return;
            }
            if (e is bgwSetRange bgwsr)
            {
                return;
            }
            if (e is bgwRange2Visible bgwr2v)
            {
                return;
            }
            if (e is bgwProgress bgp)
            {
                return;
            }
            if (e is bgwValue2 bgwv2)
            {
                return;
            }
            if (e is string message)
            {
                Console.WriteLine(message);
                return;
            }

            Console.WriteLine($"Unknown report type {e.GetType()}");
        }

        protected static void DoCleanShutdown(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nKeyboard Interrupt Detected. Shudown Started...\nPlease Wait for Worker Theads to Finish\n");
            _thWrk.Cancel();

            var messageLimiter = 0;
            while (!_thWrk.Finished)
            {
                Thread.Sleep(1000);
                if (messageLimiter++ % 10 == 0)
                {
                    Console.WriteLine("Waiting...");
                }
            }
        }

    }
}
