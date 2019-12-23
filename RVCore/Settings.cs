using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32;
using RVCore.RvDB;
using File = RVIO.File;
using Path = RVIO.Path;

namespace RVCore
{
    public enum EScanLevel
    {
        Level1,
        Level2,
        Level3
    }


    public enum EFixLevel
    {
        TrrntZipLevel1,
        TrrntZipLevel2,
        TrrntZipLevel3,
        Level1,
        Level2,
        Level3
    }

    public enum MergeType
    {
        None,
        Split,
        Merge,
        NonMerged,
        CHDsMerge
    }

    public enum FilterType
    {
        KeepAll,
        RomsOnly,
        CHDsOnly
    }


    public class Settings
    {
        public static Settings rvSettings;

        public bool FilesOnly = false;

        public string DatRoot;
        public string CacheFile;
        public EScanLevel ScanLevel;
        public EFixLevel FixLevel;

        public List<DatRule> DatRules;

        public List<string> IgnoreFiles;

        public List<EmulatorInfo> EInfo;

        public bool DoubleCheckDelete = true;
        public bool DebugLogsEnabled;
        public bool DetailedFixReporting = false;
        public bool CacheSaveTimerEnabled = true;
        public int CacheSaveTimePeriod = 10;
        public bool ConvertToRV7Z = false;
        public bool UseFileMove = false;
        public bool UseFileSelection = false;

        public bool chkBoxShowCorrect = true;
        public bool chkBoxShowMissing = true;
        public bool chkBoxShowFixed = true;
        public bool chkBoxShowMerged = true;


        public static string EMail
        {
            get
            {
                RegistryKey regKey1 = Registry.CurrentUser;
                regKey1 = regKey1.CreateSubKey("Software\\RomVault3\\User");
                return regKey1.GetValue("Email", "").ToString();
            }

            set
            {
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.CreateSubKey("Software\\RomVault3\\User");
                regKey.SetValue("Email", value);
            }
        }

        public static string Username
        {
            get
            {
                RegistryKey regKey1 = Registry.CurrentUser;
                regKey1 = regKey1.CreateSubKey("Software\\RomVault3\\User");
                return regKey1.GetValue("UserName", "").ToString();
            }
            set
            {
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.CreateSubKey("Software\\RomVault3\\User");
                regKey.SetValue("UserName", value);
            }
        }



        public static bool OptOut
        {
            get
            {
                RegistryKey regKey1 = Registry.CurrentUser;
                regKey1 = regKey1.CreateSubKey("Software\\RomVault3\\User");
                return regKey1.GetValue("OptOut", "").ToString() == "True";
            }
            set
            {
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.CreateSubKey("Software\\RomVault3\\User");
                regKey.SetValue("OptOut", value.ToString());
            }
        }




        public static bool IsUnix
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return p == 4 || p == 6 || p == 128;
            }
        }

        public static bool IsMono => Type.GetType("Mono.Runtime") != null;

        public static Settings SetDefaults()
        {
            Settings ret = ReadConfig();
            if (ret == null)
            {
                ret = new Settings
                {
                    CacheFile = "RomVault3_" + DBVersion.Version + ".Cache",
                    DatRoot = "DatRoot",
                    ScanLevel = EScanLevel.Level2,
                    FixLevel = EFixLevel.TrrntZipLevel2,
                    IgnoreFiles = new List<string> { "_ReadMe_.txt" },
                    EInfo = new List<EmulatorInfo>(),

                    chkBoxShowCorrect = true,
                    chkBoxShowMissing = true,
                    chkBoxShowFixed = true,
                    chkBoxShowMerged = false
                };
                ret.ResetDatRules();
            }
            // fix old DatRules by adding a dir seprator on the end of the dirpaths
            foreach (DatRule r in ret.DatRules)
            {
                string lastchar = r.DirKey.Substring(r.DirKey.Length - 1);
                if (lastchar == "\\")
                    r.DirKey = r.DirKey.Substring(0, r.DirKey.Length - 1);
            }
            ret.DatRules.Sort();

            return ret;
        }


        public void ResetDatRules()
        {
            DatRules = new List<DatRule>
            {
                new DatRule
                {
                    DirKey = "RomVault",
                    DirPath="RomRoot",
                    Compression = FileType.Zip,
                    CompressionOverrideDAT = false,
                    Merge = MergeType.Split,
                    MergeOverrideDAT = false,
                    SingleArchive = false,
                    MultiDATDirOverride = false
                }
            };
        }

        public static void WriteConfig(Settings settings)
        {
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RomVault3cfg.xml")))
            {
                File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RomVault3cfg.xml"));
            }

            using (StreamWriter sw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RomVault3cfg.xml")))
            {
                XmlSerializer x = new XmlSerializer(typeof(Settings));
                x.Serialize(sw, settings);
                sw.Flush();
            }
        }

        private static Settings ReadConfig()
        {
            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RomVault3cfg.xml")))
                return null;

            Settings retSettings;
            using (StreamReader sr = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RomVault3cfg.xml")))
            {
                XmlSerializer x = new XmlSerializer(typeof(Settings));
                retSettings = (Settings)x.Deserialize(sr);
            }

            foreach (var rule in retSettings.DatRules)
            {
                if (rule.Merge == MergeType.CHDsMerge)
                {
                    rule.Merge = MergeType.Merge;
                    rule.Filter = FilterType.CHDsOnly;
                }
            }

            return retSettings;
        }
    }

    public class DatRule : IComparable<DatRule>
    {
        public string DirKey;
        public string DirPath;

        // compression
        // TZip,7Zip,File
        public FileType Compression = FileType.Zip;
        public bool CompressionOverrideDAT;

        // Merge Type
        // split,merge,nonmerged
        public MergeType Merge;
        public FilterType Filter;

        public bool MergeOverrideDAT;

        public bool SingleArchive;
        public bool MultiDATDirOverride;

        public int CompareTo(DatRule obj)
        {
            return Math.Sign(string.Compare(DirKey, obj.DirKey, StringComparison.Ordinal));
        }

    }

}