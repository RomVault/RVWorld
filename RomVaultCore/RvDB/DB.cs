/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
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
        public const int Version = 3;
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
                DatStatus = DatStatus.InToSort,
            };
            ts.ToSortStatusSet(RvFile.ToSortDirType.ToSortPrimary | RvFile.ToSortDirType.ToSortCache);
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
                using (FileStream fs = new FileStream(tname, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096 * 1024))
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
                ReportError.Show($"Error Writing Cache File, your cache is now out of date, fix this error and rescan: {e.Message}");
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
            string cacheFilename = Settings.rvSettings.CacheFile;
            if (!File.Exists(cacheFilename))
            {
                cacheFilename = cacheFilename.Replace("3_3.Cache", "3_2.Cache");
                if (!File.Exists(cacheFilename))
                {
                    OpenDefaultDB();
                    ThWrk = null;
                    return;
                }
            }

            using (FileStream fs = new FileStream(cacheFilename, FileMode.Open, FileAccess.Read))
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
                        if (DBVersion.VersionNow < 2)
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
                    }
                    if ((DBVersion.VersionNow == 2 || DBVersion.VersionNow == 3) && Settings.rvSettings.CacheFile.Contains("3_2.Cache"))
                    {
                        DirRoot = new RvFile(br);
                        Settings.rvSettings.CacheFile = Settings.rvSettings.CacheFile.Replace("3_2.Cache", "3_3.Cache");
                        Write();
                        Settings.WriteConfig(Settings.rvSettings);
                    }
                    else
                        DirRoot = new RvFile(br);


                    UpdateFixToSortStatus(DirRoot);


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

        private static void UpdateFixToSortStatus(RvFile DirRoot)
        {
            for (int i = 0; i < DirRoot.ChildCount; i++)
            {
                if (DirRoot.Child(i).ToSortStatusIs(RvFile.ToSortDirType.ToSortPrimary | RvFile.ToSortDirType.ToSortCache))
                    return;
            }
            for (int i = 0; i < DirRoot.ChildCount; i++)
            {
                if (DirRoot.Child(i).FileStatusIs((FileStatus)(1 << 30))) // Old ToSortPrimary
                    DirRoot.Child(i).ToSortStatusSet(RvFile.ToSortDirType.ToSortPrimary);
                if (DirRoot.Child(i).FileStatusIs((FileStatus)(1 << 31))) // Old ToSortCache
                    DirRoot.Child(i).ToSortStatusSet(RvFile.ToSortDirType.ToSortCache);
                DirRoot.Child(i).FileStatusClear((FileStatus)((1 << 30) | (1 << 31)));
            }
        }

        public static string FixNull(string v)
        {
            return v ?? "";
        }

        public static RvFile GetToSortCache()
        {
            for (int i = 0; i < DirRoot.ChildCount; i++)
            {
                RvFile t = DirRoot.Child(i);
                if (t.ToSortStatusIs(RvFile.ToSortDirType.ToSortCache))
                {
                    return t;
                }
            }

            return DirRoot.Child(1);
        }

        public static RvFile GetToSortPrimary()
        {
            for (int i = 1; i < DirRoot.ChildCount; i++)
            {
                RvFile t = DirRoot.Child(i);
                if (t.ToSortStatusIs(RvFile.ToSortDirType.ToSortPrimary))
                {
                    return t;
                }
            }

            return DirRoot.Child(1);
        }

        public static RvFile GetToSortFileOnly()
        {
            for (int i = 1; i < DirRoot.ChildCount; i++)
            {
                RvFile t = DirRoot.Child(i);
                if (t.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly))
                {
                    return t;
                }
            }

            return GetToSortPrimary();
        }

        public static void MoveToSortUp(RvFile t)
        {
            for (int i = 2; i < DirRoot.ChildCount; i++)
            {
                if (DirRoot.Child(i) == t)
                {
                    DirRoot.ChildRemove(i);
                    DirRoot.ChildAdd(t, i - 1);
                    return;
                }
            }
        }
        public static void MoveToSortDown(RvFile t)
        {
            for (int i = 1; i < DirRoot.ChildCount - 1; i++)
            {
                if (DirRoot.Child(i) == t)
                {
                    DirRoot.ChildRemove(i);
                    DirRoot.ChildAdd(t, i + 1);
                    return;
                }
            }

        }
    }
}