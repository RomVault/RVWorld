using System;
using RVCore;
using RVCore.FindFix;
using RVCore.FixFile;
using RVCore.ReadDat;
using RVCore.RvDB;
using RVCore.Scanner;

namespace RVCmd
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
            if (args.Length == 0)
            {
                ShowHelp();
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
            Console.WriteLine("RomVault 3 Commandline");
            Console.WriteLine("-update  -u  : Update DATs");
            Console.WriteLine("-scan    -s  : Scan ROMs");
            Console.WriteLine("-fix     -f  : FindFixes / FixROMs");
            Console.WriteLine("-all     -a  : All of the above");
            Console.WriteLine("-scanfix -sf : Scan ROMs / FindFixes / FixROMs");
        }

        private static void DoWork()
        {

            Settings.rvSettings = new Settings();

            _thWrk = new ThreadWorker(StartUpCode) { wReport = BgwProgressChanged };
            _thWrk.Start();
            Console.WriteLine("");
            Console.WriteLine("");

            if (doUpdateDATs)
            {
                _thWrk = new ThreadWorker(DatUpdate.UpdateDat) {wReport = BgwProgressChanged};
                _thWrk.Start();
                Console.WriteLine("");
                Console.WriteLine("");
            }

            if (doScanROMs)
            {
                FileScanning.StartAt = null;
                FileScanning.EScanLevel = EScanLevel.Level2;
                _thWrk = new ThreadWorker(FileScanning.ScanFiles) {wReport = BgwProgressChanged};
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


        private static void StartUpCode(ThreadWorker e)
        {
            RepairStatus.InitStatusCheck();
            Settings.rvSettings = Settings.SetDefaults();
            DB.Read(e);
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

            Console.WriteLine($"Unknown report type {e.GetType()}");
        }


    }
}
