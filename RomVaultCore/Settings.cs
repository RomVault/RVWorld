using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using DATReader.DatClean;
using Microsoft.Win32;
using RomVaultCore.RvDB;
using File = RVIO.File;
using Path = RVIO.Path;

namespace RomVaultCore
{
    public enum EScanLevel
    {
        Level1,
        Level2,
        Level3
    }


    public enum EFixLevel
    {
        Level1,
        Level2,
        Level3,
        //  Level1Old,
        //  Level2Old,
        //  Level3Old
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
        public static byte[] FCUsername;

        public bool FilesOnly = false;
        public bool zstd = false;

        public string DatRoot;
        public string CacheFile;
        public EFixLevel FixLevel;

        public List<DatRule> DatRules;

        public List<string> IgnoreFiles;

        [XmlIgnore]
        public List<Regex> IgnoreFilesRegex;

        public List<EmulatorInfo> EInfo;

        public bool DoubleCheckDelete = true;
        public bool DebugLogsEnabled;
        public bool DetailedFixReporting = true;
        public bool CacheSaveTimerEnabled = true;
        public int CacheSaveTimePeriod = 10;

        public bool ConvertToTrrntzip = true;
        public bool ConvertToRV7Z = false;

        public bool chkBoxShowCorrect = true;
        public bool chkBoxShowMissing = true;
        public bool chkBoxShowFixed = true;
        public bool chkBoxShowMerged = true;

        public static bool isLinux
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
                    FixLevel = EFixLevel.Level2,
                    EInfo = new List<EmulatorInfo>(),
                    ConvertToTrrntzip = true,
                    chkBoxShowCorrect = true,
                    chkBoxShowMissing = true,
                    chkBoxShowFixed = true,
                    chkBoxShowMerged = false,
                    IgnoreFiles = new List<string>()
                };
                ret.ResetDatRules();
            }

            // check this incase no ignorefiles list was read from the file
            if (ret.IgnoreFiles == null)
                ret.IgnoreFiles = new List<string>();
            
            // fix old DatRules by adding a dir seprator on the end of the dirpaths
            foreach (DatRule r in ret.DatRules)
            {
                string lastchar = r.DirKey.Substring(r.DirKey.Length - 1);
                if (lastchar == "\\")
                    r.DirKey = r.DirKey.Substring(0, r.DirKey.Length - 1);

                if (r.SubDirType == RemoveSubType.RemoveSubIfNameMatches)
                    r.SubDirType = RemoveSubType.RemoveSubIfSingleFiles;
            }
            ret.DatRules.Sort();

            ret.SetRegExRules();
            return ret;
        }

        public void SetRegExRules()
        {
            IgnoreFilesRegex = new List<Regex>();
            foreach (string str in IgnoreFiles)
            {
                IgnoreFilesRegex.Add(WildcardToRegex(str));
            }

            foreach (DatRule r in DatRules)
            {
                r.IgnoreFilesRegex = new List<Regex>();
                foreach (string str in r.IgnoreFiles)
                    r.IgnoreFilesRegex.Add(WildcardToRegex(str));
            }
        }

        private static Regex WildcardToRegex(string pattern)
        {
            if (pattern.ToLower().StartsWith("regex:"))
                return new Regex(pattern.Substring(6), RegexOptions.IgnoreCase);

            return new Regex("^" + Regex.Escape(pattern).
            Replace("\\*", ".*").
            Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
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
                    Merge = MergeType.None,
                    MergeOverrideDAT = false,
                    SingleArchive = false,
                    MultiDATDirOverride = false,
                    IgnoreFiles = new List<string>()
                }
            };
        }

        public static void WriteConfig(Settings settings)
        {
            string configPath = Path.Combine(Environment.CurrentDirectory, "RomVault3cfg.xml");
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            using (StreamWriter sw = new StreamWriter(configPath))
            {
                XmlSerializer x = new XmlSerializer(typeof(Settings));
                x.Serialize(sw, settings);
                sw.Flush();
            }
        }

        private static Settings ReadConfig()
        {
            string configPath = Path.Combine(Environment.CurrentDirectory, "RomVault3cfg.xml");
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"{configPath} not Found");
                return null;
            }
            Console.WriteLine($"Reading {configPath}");
            string strXml = System.IO.File.ReadAllText(configPath);

            // converting old enum to new:
            strXml = strXml.Replace("TrrntZipLevel", "Level");

            Settings retSettings;
            using (TextReader sr = new StringReader(strXml))
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
        public RemoveSubType SubDirType;
        public bool MultiDATDirOverride;
        public bool UseDescriptionAsDirName;

        public List<string> IgnoreFiles;

        [XmlIgnore]
        public List<Regex> IgnoreFilesRegex;


        public int CompareTo(DatRule obj)
        {
            return Math.Sign(string.Compare(DirKey, obj.DirKey, StringComparison.Ordinal));
        }

    }

}