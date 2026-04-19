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

    public enum ChdCompressionType
    {
        Auto,
        Normal,
        CD,
        DVD,
        PSP,
        Dreamcast
    }

    public enum ChdAudioTransform
    {
        None,
        AllowSwap16,
        AllowRawToWav
    }

    public enum ChdLayoutStrictness
    {
        Normal,
        Strict,
        Relaxed
    }

    /// <summary>
    /// Global persisted configuration (scan/fix behavior, roots, DAT rules, CHD options, UI preferences).
    /// </summary>
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

        /// <summary>
        /// Enables the CHD scan cache used to avoid repeated extraction/rehashing across rescans.
        /// </summary>
        [XmlElement(ElementName = "ChdScanCacheEnabled", DataType = "boolean", IsNullable = false), DefaultValue(true)]
        public bool ChdScanCacheEnabled = true;

        /// <summary>
        /// Enables per-CHD scan debug logs (mapping decisions, expected members, and fallback reasons).
        /// </summary>
        [XmlElement(ElementName = "ChdScanDebugEnabled", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool ChdDebug = false;

        /// <summary>
        /// When enabled, requires the original CUE/GDI descriptor to be present (do not accept synthetic descriptors).
        /// </summary>
        [XmlElement(ElementName = "ChdStrictCueGdi", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool ChdStrictCueGdi = false;

        /// <summary>
        /// When enabled, keeps/moves sidecar .cue/.gdi files alongside CHDs during fix operations.
        /// </summary>
        [XmlElement(ElementName = "ChdKeepCueGdi", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool ChdKeepCueGdi = false;

        /// <summary>
        /// Audio transformation mode applied when exporting or matching CHD tracks.
        /// </summary>
        public ChdAudioTransform ChdAudioTransform = ChdAudioTransform.None;

        /// <summary>
        /// Strictness profile used when comparing CHD disc layouts to DAT expectations.
        /// </summary>
        public ChdLayoutStrictness ChdLayoutStrictness = ChdLayoutStrictness.Normal;

        /// <summary>
        /// Enables exporting CHD tracks during Fix workflows (e.g., to materialize track files when required).
        /// </summary>
        [XmlElement(ElementName = "ChdExportTracksOnFix", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool ChdExportTracksOnFix = false;

        /// <summary>
        /// Enables CHD streaming mode for scanning (hash directly from the logical stream without extraction).
        /// </summary>
        [XmlElement(ElementName = "ChdStreamingEnabled", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool ChdStreaming = false;

        /// <summary>
        /// When enabled, prefers a synthetic CUE/GDI derived from CHD metadata when it matches DAT expectations.
        /// </summary>
        [XmlElement(ElementName = "ChdPreferSyntheticDescriptor", DataType = "boolean", IsNullable = false), DefaultValue(false)]
        public bool ChdPreferSynthetic = false;

        /// <summary>
        /// When enabled, allows treating a CHD container as satisfying track-file expectations even when byte-level track hashes differ.
        /// </summary>
        [XmlElement(ElementName = "ChdTrustContainerForTracks", DataType = "boolean", IsNullable = false), DefaultValue(true)]
        public bool ChdTrustContainerForTracks = true;

        /// <summary>
        /// DVD CHD hunk size used during creation (in KiB); larger hunks may improve compression ratio.
        /// </summary>
        [XmlElement(ElementName = "ChdDvdHunkSizeKiB", DataType = "int", IsNullable = false), DefaultValue(256)]
        public int ChdDvdHunkSizeKiB = 256;

        /// <summary>
        /// Maximum number of concurrent processors used by chdman (`-np`) during CHD creation.
        /// Set to 0 to let chdman choose automatically.
        /// </summary>
        [XmlElement(ElementName = "ChdNumProcessors", DataType = "int", IsNullable = false), DefaultValue(0)]
        public int ChdNumProcessors = 0;

        /// <summary>
        /// Enables additional CHD validation checks during scanning and creation flows.
        /// </summary>
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

            if (ret.DatRules == null || ret.DatRules.Count == 0)
                ret.ResetDatRules();

            bool hasRootRule = false;
            foreach (DatRule r in ret.DatRules)
            {
                if (r.IgnoreFiles == null)
                    r.IgnoreFiles = new List<string>();
                if (string.Equals(r.DirKey, "RomVault", StringComparison.OrdinalIgnoreCase))
                    hasRootRule = true;
            }

            if (!hasRootRule)
            {
                ret.DatRules.Insert(0, new DatRule
                {
                    DirKey = "RomVault",
                    Compression = FileType.Zip,
                    CompressionOverrideDAT = false,
                    Merge = MergeType.None,
                    HeaderType = HeaderType.Optional,
                    MergeOverrideDAT = false,
                    SingleArchive = false,
                    MultiDATDirOverride = false,
                    IgnoreFiles = new List<string>()
                });
            }

            // fix old DatRules by adding a dir seprator on the end of the dirpaths
            foreach (DatRule r in ret.DatRules)
            {
                if (string.IsNullOrEmpty(r.DirKey))
                    continue;
                string lastchar = r.DirKey.Substring(r.DirKey.Length - 1);
                if (lastchar == "\\")
                    r.DirKey = r.DirKey.Substring(0, r.DirKey.Length - 1);

                if ((r.DiscArchiveAsCHD || r.Compression == FileType.CHD) && r.Filter == FilterType.CHDsOnly)
                    r.Filter = FilterType.KeepAll;
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

                if ((rule.DiscArchiveAsCHD || rule.Compression == FileType.CHD) && rule.Filter == FilterType.CHDsOnly)
                    rule.Filter = FilterType.KeepAll;
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

    /// <summary>
    /// Maps a DAT-relative directory key to a physical on-disk directory path override.
    /// </summary>
    public class DirMapping : IComparable<DirMapping>
    {
        public string DirKey;
        public string DirPath;

        public int CompareTo(DirMapping obj)
        {
            return Math.Sign(string.Compare(DirKey, obj.DirKey, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Per-directory rule controlling merge/compression/header behavior and filtering during scan/fix.
    /// </summary>
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

        public bool DiscArchiveAsCHD = false;
        public ChdCompressionType ChdCompressionType = ChdCompressionType.Normal;
        public bool ChdStrictCueGdi = false;
        public bool ChdKeepCueGdi = false;
        public ChdAudioTransform ChdAudioTransform = ChdAudioTransform.None;
        public ChdLayoutStrictness ChdLayoutStrictness = ChdLayoutStrictness.Normal;


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

    /// <summary>
    /// Emulator launch configuration for a specific tree directory.
    /// </summary>
    public class EmulatorInfo
    {
        public string TreeDir;
        public string ExeName;
        public string CommandLine;
        public string WorkingDirectory;
        public string ExtraPath;
    }

}
