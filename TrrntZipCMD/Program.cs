using System;
using System.IO;
using System.Reflection;
using Trrntzip;
using Directory = RVIO.Directory;
using DirectoryInfo = RVIO.DirectoryInfo;
using FileInfo = RVIO.FileInfo;
using Path = RVIO.Path;


namespace TrrntZipCMD
{
    internal class Program
    {
        private static bool _noRecursion;
        private static bool _guiLaunch;

        private static TorrentZip tz;

        private static StreamWriter logStream = null;

        private static void Main(string[] args)
        {
            try
            {

                if (args.Length == 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine("trrntzip: missing path");
                    Console.WriteLine("Usage: trrntzip [OPTIONS] [PATH/ZIP FILES]");
                    return;
                }

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg.Length < 2)
                    {
                        continue;
                    }
                    if (arg.Substring(0, 1) != "-")
                    {
                        continue;
                    }

                    switch (arg.Substring(1, 1))
                    {
                        case "?":
                            Console.WriteLine("TorrentZip.Net v{0}\n", Assembly.GetExecutingAssembly().GetName().Version);
                            Console.WriteLine("Copyright (C) 2018 GordonJ");
                            Console.WriteLine("Homepage : http://www.romvault.com/trrntzip\n");
                            Console.WriteLine("Usage: trrntzip [OPTIONS] [PATH/ZIP FILE]\n");
                            Console.WriteLine("Options:\n");
                            Console.WriteLine("-? : show this help");
                            Console.WriteLine("-s : prevent sub-directory recursion");
                            Console.WriteLine("-f : force re-zip");
                            Console.WriteLine("-c : Check files only do not repair");
                            Console.WriteLine("-l : verbose logging");
                            Console.WriteLine("-v : show version");
                            Console.WriteLine("-g : pause when finished");
                            return;
                        case "s":
                            _noRecursion = true;
                            break;
                        case "f":
                            Trrntzip.Program.ForceReZip = true;
                            break;
                        case "c":
                            Trrntzip.Program.CheckOnly = true;
                            break;
                        case "l":
                            Trrntzip.Program.VerboseLogging = true;
                            string logtime = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                            logStream = new StreamWriter($"outlog-{logtime}.txt");
                            break;
                        case "v":
                            Console.WriteLine("TorrentZip v{0}", Assembly.GetExecutingAssembly().GetName().Version);
                            return;
                        case "g":
                            _guiLaunch = true;
                            break;
                    }
                }

                tz = new TorrentZip
                {
                    StatusCallBack = StatusCallBack,
                    StatusLogCallBack = StatusLogCallBack
                };

                foreach (string tArg in args)
                {
                    string arg = tArg;
                    if (arg.Length < 2)
                    {
                        continue;
                    }
                    if (arg.Substring(0, 1) == "-")
                    {
                        continue;
                    }

                    if (arg.Length > 2 && arg.Substring(0, 2) == ".\\")
                        arg = arg.Substring(2);
                    // first check if arg is a directory
                    if (Directory.Exists(arg))
                    {
                        ProcessDir(arg);
                        continue;
                    }

                    // now check if arg is a directory/filename with possible wild cards.

                    string dir = Path.GetDirectoryName(arg);
                    if (string.IsNullOrEmpty(dir))
                    {
                        dir = Environment.CurrentDirectory;
                    }

                    string filename = Path.GetFileName(arg);

                    DirectoryInfo dirInfo = new DirectoryInfo(dir);
                    FileInfo[] fileInfo = dirInfo.GetFiles(filename);
                    foreach (FileInfo file in fileInfo)
                    {
                        string ext = Path.GetExtension(file.FullName);
                        if (!string.IsNullOrEmpty(ext) && ((ext.ToLower() == ".zip") || (ext.ToLower() == ".7z")))
                        {
                            tz.Process(new FileInfo(file.FullName));
                        }
                    }
                }

                logStream?.Flush();
                logStream?.Close();
                logStream?.Dispose();
                logStream = null;

                if (_guiLaunch)
                {
                    Console.WriteLine("Complete.");
                    Console.ReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught.", e);
                logStream?.WriteLine("{0} Exception caught.", e);
                logStream?.Flush();
                logStream?.Close();
                logStream?.Dispose();
                logStream = null;
            }
        }

        private static void ProcessDir(string dirName)
        {
            Console.WriteLine("Checking Dir : " + dirName);

            DirectoryInfo di = new DirectoryInfo(dirName);
            FileInfo[] fi = di.GetFiles();
            for (int i = 0; i < fi.Length; i++)
            {
                string filename = fi[i].FullName;
                string ext = Path.GetExtension(filename);
                if (!string.IsNullOrEmpty(ext) && ((ext.ToLower() == ".zip") || (ext.ToLower() == ".7z")))
                {
                    tz.Process(new FileInfo(filename));
                }
            }

            if (_noRecursion)
            {
                return;
            }

            string[] directories = System.IO.Directory.GetDirectories(dirName);
            foreach (string dir in directories)
            {
                ProcessDir(dir);
            }
        }


        private static void StatusCallBack(int processID, int percent)
        {
            Console.Write($"{percent,3}%");
        }

        private static void StatusLogCallBack(int processId, string log)
        {
            logStream?.WriteLine(log);
            Console.WriteLine(log);
        }
    }
}