/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.IO;
using System.Text;
using System.Threading;
using File = RVIO.File;
using FileStream = System.IO.FileStream;

namespace RomVaultCore.RvDB
{
    public static class DBVersion
    {
        public const int Version = 1;
        public static int VersionNow;
    }

    public static class DB
    {
        private const ulong EndCacheMarker = 0x15a600dda7;

        public static ThreadWorker ThWrk;
        public static long DivideProgress;

        public static RvFile DirRoot;

        private static void OpenDefaultDB()
        {
            DirRoot = new RvFile(FileType.Dir)
            {
                Tree = new RvTreeRow(),
                DatStatus = DatStatus.InDatCollect
            };

            RvFile rv = new RvFile(FileType.Dir)
            {
                Name = "RomVault",
                Tree = new RvTreeRow(),
                DatStatus = DatStatus.InDatCollect
            };
            DirRoot.ChildAdd(rv);

            RvFile ts = new RvFile(FileType.Dir)
            {
                Name = "ToSort",
                Tree = new RvTreeRow(),
                DatStatus = DatStatus.InDatCollect
            };
            ts.FileStatusSet(FileStatus.PrimaryToSort | FileStatus.CacheToSort);
            DirRoot.ChildAdd(ts);
        }

        public static void Write()
        {
            string tname = Settings.rvSettings.CacheFile + "_tmp";
            if (File.Exists(tname))
            {
                File.Delete(tname);

                while (File.Exists(tname))
                {
                    Thread.Sleep(50);
                }
            }

            try
            {
                using (FileStream fs = new FileStream(tname, FileMode.CreateNew, FileAccess.Write))
                {
                    using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8, true))
                    {
                        DBVersion.VersionNow = DBVersion.Version;
                        bw.Write(DBVersion.Version);
                        DirRoot.Write(bw);

                        bw.Write(EndCacheMarker);

                        bw.Flush();
                        bw.Close();
                    }

                    fs.Close();
                }
            }
            catch (Exception e)
            {
                ReportError.UnhandledExceptionHandler($"Error Writing Cache File, your cache is now out of date, fix this error and rescan: {e.Message}");
                return;
            }

            if (File.Exists(Settings.rvSettings.CacheFile))
            {
                string bname = Settings.rvSettings.CacheFile + "Backup";
                if (File.Exists(bname))
                {
                    File.Delete(bname);

                    while (File.Exists(bname))
                    {
                        Thread.Sleep(50);
                    }
                }
                File.Move(Settings.rvSettings.CacheFile, bname);
                while (File.Exists(Settings.rvSettings.CacheFile))
                {
                    Thread.Sleep(50);
                }
            }

            File.Move(tname, Settings.rvSettings.CacheFile);
        }

        public static void Read(ThreadWorker thWrk)
        {
            ThWrk = thWrk;
            if (!File.Exists(Settings.rvSettings.CacheFile))
            {
                OpenDefaultDB();
                ThWrk = null;
                return;
            }
            DirRoot = new RvFile(FileType.Dir);
            using (FileStream fs = new FileStream(Settings.rvSettings.CacheFile, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length < 4)
                {
                    ReportError.UnhandledExceptionHandler("Cache is Corrupt, revert to Backup.");
                }

                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, true))
                {
                    DivideProgress = fs.Length / 1000;

                    DivideProgress = DivideProgress == 0 ? 1 : DivideProgress;

                    ThWrk?.Report(new bgwSetRange(1000));

                    DBVersion.VersionNow = br.ReadInt32();

                    if (DBVersion.VersionNow != DBVersion.Version)
                    {
                        ReportError.Show(
                            "Data Cache version is out of date you should now rescan your dat directory and roms directory.");
                        br.Close();
                        fs.Close();
                        fs.Dispose();

                        OpenDefaultDB();
                        ThWrk = null;
                        return;
                    }
                    else
                    {
                        DirRoot.Read(br, null);
                    }

                    if (fs.Position > fs.Length - 8)
                    {
                        ReportError.UnhandledExceptionHandler("Cache is Corrupt, revert to Backup.");
                    }

                    ulong testEOF = br.ReadUInt64();
                    if (testEOF != EndCacheMarker)
                    {
                        ReportError.UnhandledExceptionHandler("Cache is Corrupt, revert to Backup.");
                    }

                }
            }

            ThWrk = null;
        }

        public static string Fn(string v)
        {
            return v ?? "";
        }

        public static RvFile RvFileCache()
        {
            for (int i = 0; i < DirRoot.ChildCount; i++)
            {
                RvFile t = DirRoot.Child(i);
                if (t.FileStatusIs(FileStatus.CacheToSort))
                {
                    return t;
                }
            }

            return DirRoot.Child(1);
        }

        public static RvFile RvFileToSort()
        {
            for (int i = 0; i < DirRoot.ChildCount; i++)
            {
                RvFile t = DirRoot.Child(i);
                if (t.FileStatusIs(FileStatus.PrimaryToSort))
                {
                    return t;
                }
            }

            return DirRoot.Child(1);
        }

        public static string ToSort()
        {
            return RvFileToSort().Name;
        }

    }
}