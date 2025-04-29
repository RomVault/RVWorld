using Compress;
using DATReader.DatClean;
using RomVaultCore.RvDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using File = RVIO.File;

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

    public enum HeaderType
    {
        Optional,
        Headered,
        Headerless
    }

    public class Settings
    {
        public static Settings rvSettings;

        public bool FilesOnly = false;

        public string DatRoot;
        public string CacheFile;
        public EFixLevel FixLevel;

        public List<DatRule> DatRules;
        public List<DirMapping> DirMappings;

        public List<string> IgnoreFiles;

        [XmlIgnore]
        public List<Regex> IgnoreFilesRegex;
        [XmlIgnore]
        public List<Regex> IgnoreFilesScanRegex;

        public List<EmulatorInfo> EInfo;

        public bool DoubleCheckDelete = true;
        public bool DebugLogsEnabled;
        public bool DetailedFixReporting = true;
        public bool CacheSaveTimerEnabled = true;
        public int CacheSaveTimePeriod = 10;

        public bool chkBoxShowComplete = true;
        public bool chkBoxShowPartial = true;
        public bool chkBoxShowEmpty = true;
        public bool chkBoxShowFixes = true;
        public bool chkBoxShowMIA = true;
        public bool chkBoxShowMerged = true;

        public string FixDatOutPath = null;

        public bool MIAAnon = false;
        public bool MIACallback = true;
        public bool DoNotReportFeedback = false;
        public bool DeleteOldCueFiles = false;

        [XmlElement(ElementName = "Darkness", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool Darkness = false;


        [XmlElement(ElementName = "CheckCHDVersion", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool CheckCHDVersion = false;

        public int zstdCompCount = 0;
        public int sevenZDefaultStruct = 3;


        public static bool isLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return p == 4 || p == 6 || p == 128;
            }
        }

        public static bool IsMono => Type.GetType("Mono.Runtime") != null;

        public static Settings SetDefaults(out string errorMessage)
        {
            errorMessage = "";

            Settings ret = ReadConfig();
            if (ret == null)
            {
                ret = new Settings
                {
                    CacheFile = "RomVault3_" + DBVersion.Version + ".Cache",
                    DatRoot = "DatRoot",
                    FixLevel = EFixLevel.Level2,
                    EInfo = new List<EmulatorInfo>(),
                    chkBoxShowComplete = true,
                    chkBoxShowPartial = true,
                    chkBoxShowFixes = true,
                    chkBoxShowMIA = true,
                    chkBoxShowMerged = false,
                    IgnoreFiles = new List<string>()
                };
                ret.ResetDatRules();
                ret.ResetDirMappings();
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
            }
            ret.DatRules.Sort();

            string repeatDatRules = "";
                for (int i = 0; i < ret.DatRules.Count - 1; i++)
                {
                    if (i + 1 >= ret.DatRules.Count)
                        break;
                    if (ret.DatRules[i].DirKey == ret.DatRules[i + 1].DirKey)
                    {
                        repeatDatRules += ret.DatRules[i].DirKey + "\n";
                        ret.DatRules.RemoveAt(i + 1);
                        i--;
                    }
                }

            ret.SetRegExRules();

            if (ret.DirMappings == null || ret.DirMappings.Count == 0)
            {
                ret.DirMappings = new List<DirMapping>();
                foreach (DatRule dr in ret.DatRules)
                {
                    if (string.IsNullOrEmpty(dr.DirPath))
                        continue;
                    ret.DirMappings.Add(new DirMapping() { DirKey = dr.DirKey, DirPath = dr.DirPath });
                    dr.DirPath = null;
                }
            }
            ret.DirMappings.Sort();

            string repeatDirMappings = "";
            for (int i = 0; i < ret.DirMappings.Count - 1; i++)
            {
                if (i + 1 >= ret.DirMappings.Count)
                    break;
                if (ret.DirMappings[i].DirKey == ret.DirMappings[i + 1].DirKey)
                {
                    repeatDirMappings += ret.DirMappings[i].DirKey + "\n";
                    ret.DirMappings.RemoveAt(i + 1);
                    i--;
                }
            }

            if (!string.IsNullOrWhiteSpace(repeatDatRules) || !string.IsNullOrWhiteSpace(repeatDirMappings))
            {
                errorMessage += "Multiple DAT rules / directory mappings exist for the following paths:\n\n";
            }

            if (!string.IsNullOrWhiteSpace(repeatDatRules))
            {
                errorMessage += "DAT Rules:\n";
                errorMessage += repeatDatRules+"\n\n";
            }
            if (!string.IsNullOrWhiteSpace(repeatDirMappings))
            {
                errorMessage += "Directory Mappings:\n";
                errorMessage += repeatDirMappings + "\n\n";
            }
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage += "The first instance of any duplicate rule will be used. Unused duplicates will be removed on the next configuration save.";
            }
            return ret;
        }

        public void SetRegExRules()
        {
            IgnoreFilesRegex = new List<Regex>();
            IgnoreFilesScanRegex = new List<Regex>();
            foreach (string str in IgnoreFiles)
            {
                bool mIgnore = str.ToLower().StartsWith("ignore:");
                if (mIgnore)
                    IgnoreFilesScanRegex.Add(WildcardToRegex(str.Substring(7)));
                else
                    IgnoreFilesRegex.Add(WildcardToRegex(str));
            }

            foreach (DatRule r in DatRules)
            {
                r.IgnoreFilesRegex = new List<Regex>();
                r.IgnoreFilesScanRegex = new List<Regex>();
                foreach (string str in r.IgnoreFiles)
                {
                    bool mIgnore = str.ToLower().StartsWith("ignore:");
                    if (mIgnore)
                        r.IgnoreFilesScanRegex.Add(WildcardToRegex(str.Substring(7)));
                    else
                        r.IgnoreFilesRegex.Add(WildcardToRegex(str));
                }
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
                    Compression = FileType.Zip,
                    CompressionOverrideDAT = false,
                    Merge = MergeType.None,
                    HeaderType=HeaderType.Optional,
                    MergeOverrideDAT = false,
                    SingleArchive = false,
                    MultiDATDirOverride = false,
                    IgnoreFiles = new List<string>()
                }
            };
        }
        public void ResetDirMappings()
        {
            DirMappings = new List<DirMapping>
            {
                new DirMapping
                {
                    DirKey = "RomVault",
                    DirPath = "RomRoot"
                }
            };
        }

        public static void WriteConfig(Settings settings)
        {
            string configPath = "RomVault3cfg.xml";
            string configPathTemp = "RomVault3cfg.xml.temp";
            string configPathBackup = "RomVault3cfg.xmlbackup";

            if (File.Exists(configPathTemp))
            {
                File.Delete(configPathTemp);
            }

            using (StreamWriter sw = new StreamWriter(configPathTemp))
            {
                XmlSerializer x = new XmlSerializer(typeof(Settings));
                x.Serialize(sw, settings);
                sw.Flush();
            }

            if (File.Exists(configPath))
            {
                if (File.Exists(configPathBackup))
                {
                    File.Delete(configPathBackup);
                }
                File.Move(configPath, configPathBackup);
            }

            File.Move(configPathTemp, configPath);
        }

        private static Settings ReadConfig()
        {
            string configPath = "RomVault3cfg.xml";
            if (!File.Exists(configPath))
            {
                return null;
            }
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

        public ZipStructure getDefault7ZStruct
        {
            get
            {
                switch (sevenZDefaultStruct)
                {
                    case 0: return ZipStructure.SevenZipSLZMA;
                    case 1: return ZipStructure.SevenZipNLZMA;
                    case 2: return ZipStructure.SevenZipSZSTD;
                    case 3: return ZipStructure.SevenZipNZSTD;
                    default: return ZipStructure.SevenZipNLZMA;
                }
            }
        }
    }

    public class DirMapping : IComparable<DirMapping>
    {
        public string DirKey;
        public string DirPath;

        public int CompareTo(DirMapping obj)
        {
            return Math.Sign(string.Compare(DirKey, obj.DirKey, StringComparison.Ordinal));
        }
    }

    public class DatRule : IComparable<DatRule>
    {
        public string DirKey;
        [XmlElement, DefaultValue(null)]
        public string DirPath;

        // compression
        // TZip,7Zip,File
        public FileType Compression = FileType.Zip;
        public bool CompressionOverrideDAT;

        public ZipStructure CompressionSub = ZipStructure.ZipTrrnt;
        public bool ConvertWhileFixing = true;


        // Merge Type
        // split,merge,nonmerged
        public MergeType Merge;
        public FilterType Filter;
        public HeaderType HeaderType;

        public bool MergeOverrideDAT;

        public bool SingleArchive;
        public RemoveSubType SubDirType;
        public bool MultiDATDirOverride;
        public bool UseDescriptionAsDirName;
        public bool UseIdForName;

        public bool CompleteOnly;

        public List<string> IgnoreFiles;

        [XmlIgnore]
        public List<Regex> IgnoreFilesRegex;
        [XmlIgnore]
        public List<Regex> IgnoreFilesScanRegex;

        public bool AddCategorySubDirs;
        public List<string> CategoryOrder;

        public int CompareTo(DatRule obj)
        {
            return Math.Sign(string.Compare(DirKey, obj.DirKey, StringComparison.Ordinal));
        }

    }

    public class EmulatorInfo
    {
        public string TreeDir;
        public string ExeName;
        public string CommandLine;
        public string WorkingDirectory;
        public string ExtraPath;
    }

}