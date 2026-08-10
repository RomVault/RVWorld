/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2026                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CHDSharpLib;
using Compress;
using FileScanner;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RVUtils;
using SortMethods;
using DirectoryInfo = RVIO.DirectoryInfo;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using Path = RVIO.Path;

namespace RomVaultCore.Scanner
{
    /// <summary>
    /// Populates <see cref="ScannedFile"/> structures from on-disk directories and archive-like containers.
    /// </summary>
    /// <remarks>
    /// This is the scanning bridge between:
    /// - the database tree (<see cref="RvFile"/>)
    /// - file-system content (directories, archives, and CHD containers)
    ///
    /// For CHDs, this class can scan containers either via streaming (no extraction) or via tool-based extraction.
    /// </remarks>
    public static class Populate
    {

        private static ThreadWorker _thWrk;
        private static FileScan _fileScans;

        /// <summary>
        /// Scans an archive-like container (ZIP/7z/CHD) and returns its members as a <see cref="ScannedFile"/>.
        /// </summary>
        /// <param name="dbDir">Database node representing the container file.</param>
        /// <param name="eScanLevel">Requested scan depth.</param>
        /// <param name="thWrk">Optional background worker used for progress reporting.</param>
        /// <returns>A populated <see cref="ScannedFile"/> on success; otherwise null.</returns>
        public static ScannedFile FromAZipFileArchive(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker thWrk, string chdParentPath = null)
        {
            if (_fileScans == null) _fileScans = new FileScan();
            _thWrk = thWrk;

            string filename = ResolveExistingFilePath(dbDir.FullNameCase);
            FileType sType = dbDir.FileType;
            if (sType == FileType.CHD)
            {
                ScannedFile chdScan = ScanChdContainer(dbDir, filename, eScanLevel, parentPath: chdParentPath);
                if (chdScan != null)
                    return chdScan;

                dbDir.GotStatus = GotStatus.Corrupt;
                return null;
            }
            ZipReturn zr = _fileScans.ScanArchiveFile(sType, filename, dbDir.FileModTimeStamp, eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3, out ScannedFile ar, progress: FileProgress);

            if (zr == ZipReturn.ZipGood)
            {
                dbDir.ZipStruct = ar.ZipStruct;
                return ar;
            }
            else if (zr == ZipReturn.ZipFileLocked)
            {
                thWrk.Report(new bgwShowError(filename, "Zip File Locked"));
                dbDir.FileModTimeStamp = long.MinValue;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else if (zr == ZipReturn.ZipErrorOpeningFile)
            {
                thWrk.Report(new bgwShowError(filename, "Zip Error Opening File"));
                dbDir.FileModTimeStamp = long.MinValue;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else if (zr == ZipReturn.ZipErrorTimeStamp)
            {
                thWrk.Report(new bgwShowError(filename, "Zip Error File Modified"));
                dbDir.FileModTimeStamp = long.MinValue;
                dbDir.GotStatus = GotStatus.FileLocked;
            }
            else
            {
                thWrk.Report(new bgwShowError(filename, CompressUtils.ZipErrorMessageText(zr)));
                dbDir.GotStatus = GotStatus.Corrupt;
            }
            return null;
        }

        /// <summary>
        /// Extracts a CHD through chdman for transactional round-trip validation.
        /// Streaming and persistent scan shortcuts are intentionally bypassed.
        /// </summary>
        public static ScannedFile ScanChdForRoundTrip(RvFile dbDir)
        {
            return ScanChdForRoundTrip(dbDir, dbDir?.FullNameCase);
        }

        /// <summary>
        /// Extracts the supplied physical CHD using the DAT members on <paramref name="dbDir"/>.
        /// </summary>
        public static ScannedFile ScanChdForRoundTrip(RvFile dbDir, string physicalPath)
        {
            if (dbDir == null || dbDir.FileType != FileType.CHD)
                return null;
            if (_fileScans == null)
                _fileScans = new FileScan();
            ThreadWorker previousWorker = _thWrk;
            try
            {
                _thWrk = null;
                string filename = ResolveExistingFilePath(physicalPath);
                return ScanChdContainer(dbDir, filename, EScanLevel.Level3, forceExtraction: true);
            }
            finally
            {
                _thWrk = previousWorker;
            }
        }

        private sealed class ChdCacheEntry
        {
            public string Name { get; set; }
            public ulong Size { get; set; }
            public string CRC { get; set; }
            public string SHA1 { get; set; }
            public string MD5 { get; set; }
            public string ChdStatus { get; set; }
            public string ChdScanMethod { get; set; }
            public string ChdHashMatchMode { get; set; }
            public string ChdDescriptorMatch { get; set; }
        }

        /// <summary>
        /// In-memory cached scan results for a single CHD file.
        /// </summary>
        /// <remarks>
        /// Cache validity is based on source path, size, timestamp, descriptor expectation, and a mapping fingerprint
        /// that encodes the hashing/mapping rules.
        /// </remarks>
        private sealed class ChdCacheFile
        {
            public int CacheVersion { get; set; }
            public string SourcePath { get; set; }
            public long SourceTimestamp { get; set; }
            public long SourceSize { get; set; }
            public uint? ChdVersion { get; set; }
            public string ContainerCRC { get; set; }
            public string ContainerSHA1 { get; set; }
            public string ContainerMD5 { get; set; }
            public bool IsDvd { get; set; }
            public string Descriptor { get; set; }
            public string DescriptorSha1 { get; set; }
            public string MappingFingerprint { get; set; }
            public string SettingsFingerprint { get; set; }
            public string ToolFingerprint { get; set; }
            public string ChdStatus { get; set; }
            public string ChdScanMethod { get; set; }
            public string ChdHashMatchMode { get; set; }
            public string ChdDescriptorMatch { get; set; }
            public List<ChdCacheEntry> Entries { get; set; } = new List<ChdCacheEntry>();
        }

        /// <summary>
        /// Current CHD scan cache schema version.
        /// </summary>
        private const int ChdScanCacheVersion = 10;

        /// <summary>
        /// Fingerprint describing the mapping and hashing behavior used when scanning CHDs.
        /// </summary>
        /// <remarks>
        /// This is used to invalidate old cache entries whenever descriptor selection or payload matching changes.
        /// </remarks>
        private const string ChdScanMappingFp = "mapfp11:dat-backed-members;splitbin;embedded-exact-descriptor;embedded-sbi;toc;all-family-extractors;exact-extracted-payload-hashes";
        private const int ChdScanCacheLimit = 512;
        private static readonly object ChdScanCacheLock = new object();
        private static readonly Dictionary<string, ChdCacheFile> ChdScanMemoryCache = new Dictionary<string, ChdCacheFile>(StringComparer.OrdinalIgnoreCase);
        private static readonly object ChdToolFingerprintLock = new object();
        private static readonly Dictionary<string, string> ChdToolFingerprintCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Scans a CHD file as a container and returns a directory-like <see cref="ScannedFile"/> containing member entries.
        /// </summary>
        /// <remarks>
        /// Members are emitted as <see cref="FileType.FileCHD"/> so the normal archive merge pipeline can match them against DAT expectations.
        /// The scan may run in streaming mode (no extraction) when CHD metadata is available; otherwise it falls back to chdman extraction.
        /// </remarks>
        private static ScannedFile ScanChdContainer(RvFile dbDir, string filename, EScanLevel eScanLevel, bool forceExtraction = false, string parentPath = null)
        {
            if (!File.Exists(filename))
                return null;

            if (!ChdMetadata.TryReadContainerInfo(filename, out ChdContainerInfo scanContainerInfo, out string containerInfoError))
            {
                _thWrk?.Report(new bgwShowError(filename, "Unable to read CHD header: " + containerInfoError));
                return null;
            }
            bool requiresParent = scanContainerInfo.RequiresParent;
            if (requiresParent)
            {
                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    if (!ChdParentResolver.TryResolveParent(filename, out parentPath, out string resolveError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD parent could not be resolved: " + resolveError));
                        return null;
                    }
                }
                else if (!ChdParentResolver.TryValidatePair(filename, parentPath, out string pairError))
                {
                    _thWrk?.Report(new bgwShowError(filename, "CHD parent did not match: " + pairError));
                    return null;
                }
                forceExtraction = true;
            }
            else
            {
                parentPath = null;
                ChdParentResolver.Register(filename, scanContainerInfo);
            }

            if (Settings.rvSettings.CheckCHDVersion)
            {
                try
                {
                    using (FileStream fs = System.IO.File.OpenRead(filename))
                    {
                        chd_error headerResult = CHD.CheckFile(fs, filename, false, out uint? ver, out _, out _);
                        if (headerResult == chd_error.CHDERR_NONE || headerResult == chd_error.CHDERR_REQUIRES_PARENT)
                        {
                            if (ver.GetValueOrDefault() != 5)
                                _thWrk?.Report(new bgwShowError(filename, "CHD header version is not V5"));
                        }
                        else
                        {
                            _thWrk?.Report(new bgwShowError(filename, "Unable to read CHD header for version check"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _thWrk?.Report(new bgwShowError(filename, "CHD version check failed: " + ex.Message));
                }
            }

            DatRule datRule = null;
            try
            {
                string ruleKey = (dbDir.Parent?.DatTreeFullName ?? "") + "\\";
                datRule = ReadDat.DatReader.FindDatRule(ruleKey);
            }
            catch
            {
            }

            bool expectedIsDvd = false;
            bool expectedIsGdi = false;
            bool expectedIsCue = false;
            bool expectedIsToc = false;
            string expectedSingleFamily = null;
            List<RvFile> expectedChildren = GetExpectedChdMembers(dbDir);
            try
            {
                for (int i = 0; i < expectedChildren.Count; i++)
                {
                    RvFile c = expectedChildren[i];
                    if (c?.Name == null)
                        continue;
                    if (c.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                        expectedIsDvd = true;
                    if (c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))
                        expectedIsGdi = true;
                    if (c.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
                        expectedIsCue = true;
                    if (c.Name.EndsWith(".toc", StringComparison.OrdinalIgnoreCase))
                        expectedIsToc = true;
                    string extension = System.IO.Path.GetExtension(c.Name).ToLowerInvariant();
                    if (extension == ".avi")
                        expectedSingleFamily = "laserdisc";
                    else if (extension == ".img" || extension == ".hdd" || extension == ".hd")
                        expectedSingleFamily = "hdd";
                    else if (extension == ".raw" && datRule?.ChdCompressionType == ChdCompressionType.HardDisk)
                        expectedSingleFamily = "hdd";
                    else if (extension == ".raw" && datRule?.ChdCompressionType == ChdCompressionType.Raw)
                        expectedSingleFamily = "raw";
                }
            }
            catch
            {
            }
            if (expectedIsCue || expectedIsGdi || expectedIsToc)
                expectedIsDvd = false;
            string expectedDescriptor = expectedSingleFamily ?? (expectedIsGdi ? "gdi" : expectedIsToc ? "toc" : expectedIsCue ? "cue" : expectedIsDvd ? "dvd" : "cue");

            if (expectedChildren.Count == 0)
            {
                try
                {
                    bool psp = datRule?.ChdCompressionType == ChdCompressionType.PSP;
                    ChdStorageProfile storage = datRule?.ChdStorageProfile ?? ChdStorageProfile.Archive;
                    if (ChdEncodingProfile.TryDescribeExisting(filename, psp, storage, out ChdEncodingProfileSpec described, out _, out _))
                    {
                        expectedDescriptor = described.Family == "cd" ? "cue"
                            : described.Family == "gdi" ? "gdi"
                            : described.Family == "dvd" || described.Family == "psp" ? "dvd"
                            : described.Family;
                        expectedSingleFamily = described.Family == "raw" || described.Family == "hdd" || described.Family == "laserdisc"
                            ? described.Family
                            : null;
                        expectedIsDvd = described.Family == "dvd" || described.Family == "psp";
                        expectedIsGdi = described.Family == "gdi";
                    }
                    else
                    {
                        expectedIsDvd = true;
                        expectedIsGdi = false;
                        expectedDescriptor = "dvd";
                    }
                }
                catch
                {
                    expectedIsDvd = true;
                    expectedIsGdi = false;
                    expectedDescriptor = "dvd";
                }
            }

            bool forceScan = (eScanLevel == EScanLevel.Level3) || requiresParent;
            string chdmanExe = ChdmanProcessTracker.FindExecutable();

            if (!forceScan &&
                Settings.rvSettings.ChdScanCacheEnabled &&
                TryLoadChdScanCache(filename, datRule, chdmanExe, out ChdCacheFile cache) &&
                cache.CacheVersion == ChdScanCacheVersion &&
                cache.IsDvd == expectedIsDvd &&
                string.Equals(cache.Descriptor ?? "", expectedDescriptor, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(cache.MappingFingerprint ?? "", ChdScanMappingFp, StringComparison.OrdinalIgnoreCase) &&
                ((expectedDescriptor != "cue" && expectedDescriptor != "gdi" && expectedDescriptor != "toc") || !string.IsNullOrWhiteSpace(cache.DescriptorSha1)))
            {
                bool cacheValid = cache.Entries != null && cache.Entries.Count > 0;
                if (cacheValid)
                {
                    for (int i = 0; i < cache.Entries.Count; i++)
                    {
                        ChdCacheEntry e = cache.Entries[i];
                        string name = e?.Name ?? "";
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            cacheValid = false;
                            break;
                        }
                        string ext = System.IO.Path.GetExtension(name);
                        if (string.Equals(ext, ".cue", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".gdi", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".toc", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (e.Size == 0)
                        {
                            cacheValid = false;
                            break;
                        }
                        if (string.IsNullOrWhiteSpace(e.CRC) && string.IsNullOrWhiteSpace(e.SHA1) && string.IsNullOrWhiteSpace(e.MD5))
                        {
                            cacheValid = false;
                            break;
                        }
                    }
                }

                if (!cacheValid)
                    goto SkipChdCache;

                ScannedFile cached = new ScannedFile(FileType.CHD)
                {
                    Name = filename,
                    FileModTimeStamp = cache.SourceTimestamp,
                    GotStatus = GotStatus.Got,
                    Size = cache.SourceSize < 0 ? 0 : (ulong)cache.SourceSize,
                    CRC = ParseHexToBytes(cache.ContainerCRC),
                    SHA1 = ParseHexToBytes(cache.ContainerSHA1),
                    MD5 = ParseHexToBytes(cache.ContainerMD5),
                    CHDVersion = cache.ChdVersion,
                    ZipStruct = ZipStructure.None,
                    Comment = "",
                    ChdStatus = cache.ChdStatus,
                    ChdScanMethod = cache.ChdScanMethod ?? "Cache",
                    ChdHashMatchMode = cache.ChdHashMatchMode,
                    ChdDescriptorMatch = cache.ChdDescriptorMatch
                };
                cached.FileStatusSet(FileStatus.SizeVerified |
                                     (cached.CRC != null ? FileStatus.CRCVerified : 0) |
                                     (cached.SHA1 != null ? FileStatus.SHA1Verified : 0) |
                                     (cached.MD5 != null ? FileStatus.MD5Verified : 0));
                for (int i = 0; i < cache.Entries.Count; i++)
                {
                    ChdCacheEntry e = cache.Entries[i];
                    ScannedFile sf = new ScannedFile(FileType.FileCHD)
                    {
                        Name = e.Name,
                        FileModTimeStamp = cache.SourceTimestamp,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = e.Size,
                        CRC = ParseHexToBytes(e.CRC),
                        SHA1 = ParseHexToBytes(e.SHA1),
                        MD5 = ParseHexToBytes(e.MD5),
                        ChdStatus = e.ChdStatus,
                        ChdScanMethod = e.ChdScanMethod ?? "Cache",
                        ChdHashMatchMode = e.ChdHashMatchMode,
                        ChdDescriptorMatch = e.ChdDescriptorMatch
                    };
                    sf.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                    cached.Add(sf);
                }
                cached.Sort();
                return cached;
            }

        SkipChdCache:
            string tempDir = "";
            try
            {
                IChdExtractor extractor = null;
                bool expectsIso = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));
                bool expectsGdi = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase));
                bool expectsCue = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase));
                bool expectsToc = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".toc", StringComparison.OrdinalIgnoreCase));
                bool expectsSbi = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".sbi", StringComparison.OrdinalIgnoreCase));
                string expectedIsoName = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))?.Name;
                RvFile expectedSingle = expectedChildren.Find(c => IsSingleImageMember(c?.Name, expectedSingleFamily));
                string expectedSingleName = expectedSingle?.Name;
                if (string.IsNullOrWhiteSpace(expectedSingleName) && !string.IsNullOrWhiteSpace(expectedSingleFamily))
                    expectedSingleName = expectedSingleFamily == "laserdisc" ? "image.avi" : expectedSingleFamily == "hdd" ? "image.img" : "image.raw";
                if (!expectsIso && expectedChildren.Count == 0 && string.Equals(expectedDescriptor, "dvd", StringComparison.OrdinalIgnoreCase))
                    expectsIso = true;

                FileInfo containerFile = new FileInfo(filename);
                ChdContainerInfo containerInfo = scanContainerInfo;
                ScannedFile ar = new ScannedFile(FileType.CHD)
                {
                    Name = filename,
                    FileModTimeStamp = containerFile.LastWriteTime,
                    GotStatus = GotStatus.Got,
                    CHDVersion = containerInfo?.Version,
                    ZipStruct = ZipStructure.None,
                    Comment = ""
                };

                // Hash the CHD container itself
                using (FileStream fs = System.IO.File.OpenRead(filename))
                {
                    ar.Size = (ulong)fs.Length;
                    _fileScans.CheckSumRead(fs, ar, ar.Size.Value, true, false, null, 0, 0);
                }
                System.Text.StringBuilder familyDebug = Settings.rvSettings.ChdDebug ? new System.Text.StringBuilder() : null;
                if (familyDebug != null)
                {
                    familyDebug.AppendLine("CHD scan");
                    familyDebug.AppendLine("path=" + ChdDiagnosticFormatter.RedactPath(filename));
                    familyDebug.AppendLine("family=" + expectedDescriptor);
                    familyDebug.AppendLine("containerSize=" + ar.Size);
                }

                if (!string.IsNullOrWhiteSpace(expectedSingleFamily))
                {
                    if (!TryPrepareChdScanWorkspace(filename, chdmanExe, false, parentPath, false, out tempDir, out extractor, out string spaceError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, spaceError));
                        return null;
                    }
                    string physicalName = expectedSingleFamily == "laserdisc" ? "image.avi" : expectedSingleFamily == "hdd" ? "image.img" : "image.raw";
                    string outputPath = System.IO.Path.Combine(tempDir, physicalName);
                    bool extracted;
                    string extractionName;
                    string extractionError;
                    switch (expectedSingleFamily)
                    {
                        case "raw":
                            extracted = extractor.ExtractRaw(filename, outputPath, out extractionError);
                            extractionName = "Raw";
                            break;
                        case "hdd":
                            extracted = extractor.ExtractHardDisk(filename, outputPath, out extractionError);
                            extractionName = "HDD";
                            break;
                        case "laserdisc":
                            extracted = extractor.ExtractLaserDisc(filename, outputPath, out extractionError);
                            extractionName = "LaserDisc";
                            break;
                        default:
                            extracted = false;
                            extractionName = expectedSingleFamily;
                            extractionError = "Unsupported CHD media family.";
                            break;
                    }

                    if (!extracted || !System.IO.File.Exists(outputPath))
                    {
                        _thWrk?.Report(new bgwShowError(filename, $"CHD {extractionName} extraction failed: {extractionError}"));
                        return null;
                    }

                    FileInfo fi = new FileInfo(outputPath);
                    ScannedFile payload = new ScannedFile(FileType.FileCHD)
                    {
                        Name = expectedSingleName,
                        FileModTimeStamp = fi.LastWriteTime,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        ChdScanMethod = $"Extraction ({extractionName})",
                        ChdHashMatchMode = "Exact",
                        Size = (ulong)fi.Length
                    };
                    using (Stream stream = System.IO.File.OpenRead(outputPath))
                        _fileScans.CheckSumRead(stream, payload, (ulong)fi.Length, true, false, null, 0, 0);
                    ar.Add(payload);
                    ar.ChdScanMethod = payload.ChdScanMethod;
                    ar.ChdHashMatchMode = "Exact";
                    if (familyDebug != null)
                    {
                        familyDebug.AppendLine("method=" + payload.ChdScanMethod);
                        familyDebug.AppendLine($"payload={payload.Name};size={payload.Size};crc={payload.CRC.ToHexString()};sha1={payload.SHA1.ToHexString()};md5={payload.MD5.ToHexString()}");
                    }
                    if (Settings.rvSettings.ChdScanCacheEnabled && !requiresParent)
                        SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: false, descriptor: expectedSingleFamily, descriptorSha1: null);
                    WriteChdDebugLog(filename, familyDebug);
                    return ar;
                }

                if (expectsIso && !expectsGdi && !expectsCue)
                {
                    bool useStreaming = !forceExtraction && Settings.rvSettings.ChdStreaming &&
                                        Settings.rvSettings.ChdNativeVerification && Settings.rvSettings.ChdHealthDatabase &&
                                        ChdHealthStore.HasCurrentExternalParity(filename, Settings.rvSettings.ChdExternalParityDays);
                    if (useStreaming)
                    {
                        ScannedFile isoSf = new ScannedFile(FileType.FileCHD)
                        {
                            Name = expectedIsoName ?? "image.iso",
                            FileModTimeStamp = dbDir.FileModTimeStamp,
                            GotStatus = GotStatus.Got,
                            DeepScanned = true,
                            ChdScanMethod = "Streaming (DVD)"
                        };
                        try
                        {
                            using (Stream s = CHDSharpLib.ChdLogicalStream.OpenRead(filename))
                            {
                                ulong size = (ulong)s.Length;
                                _fileScans.CheckSumRead(s, isoSf, size, true, false, null, 0, 0);
                                isoSf.Size = size;
                            }
                            ar.Add(isoSf);
                            ar.ChdScanMethod = "Streaming (DVD)";
                        }
                        catch (Exception ex)
                        {
                            _thWrk?.Report(new bgwShowError(filename, "CHD streaming failed; falling back to extractdvd. " + ex.Message));
                            useStreaming = false;
                        }
                    }
                    if (!useStreaming)
                    {
                        if (!TryPrepareChdScanWorkspace(filename, chdmanExe, true, parentPath, false, out tempDir, out extractor, out string spaceError))
                        {
                            _thWrk?.Report(new bgwShowError(filename, spaceError));
                            return null;
                        }
                        string outIso = System.IO.Path.Combine(tempDir, "image.iso");
                        if (extractor.ExtractDvd(filename, outIso, out string err))
                        {
                            FileInfo fi = new FileInfo(outIso);
                            ScannedFile isoSf = new ScannedFile(FileType.FileCHD)
                            {
                                Name = expectedIsoName ?? "image.iso",
                                FileModTimeStamp = fi.LastWriteTime,
                                GotStatus = GotStatus.Got,
                                DeepScanned = true,
                                ChdScanMethod = "Extraction (DVD)"
                            };
                            using (Stream s = System.IO.File.OpenRead(outIso))
                            {
                                _fileScans.CheckSumRead(s, isoSf, (ulong)fi.Length, true, false, null, 0, 0);
                            }
                            isoSf.Size = (ulong)fi.Length;
                            ar.Add(isoSf);
                            ar.ChdScanMethod = "Extraction (DVD)";
                            if (Settings.rvSettings.ChdHealthDatabase && !requiresParent)
                            {
                                string externalHash;
                                using (Stream externalStream = System.IO.File.OpenRead(outIso))
                                    externalHash = ChdScrubber.HashSha256(externalStream);
                                string nativeHash = "";
                                bool parity = false;
                                try
                                {
                                    using (Stream nativeStream = CHDSharpLib.ChdLogicalStream.OpenRead(filename))
                                        nativeHash = ChdScrubber.HashSha256(nativeStream);
                                    parity = string.Equals(nativeHash, externalHash, StringComparison.OrdinalIgnoreCase);
                                }
                                catch { parity = false; }
                                ChdHealthStore.RecordParity(filename, "dvd", datRule?.ChdStorageProfile.ToString().ToLowerInvariant(), GetChdmanSha256(chdmanExe), nativeHash, externalHash, "scan-native-external", parity);
                            }
                        }
                        else
                        {
                            _thWrk?.Report(new bgwShowError(filename, "CHD extractdvd failed: " + err));
                            return null;
                        }
                    }

                    if (ar.Count == 0)
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD scan produced no DVD payload."));
                        return null;
                    }

                    if (Settings.rvSettings.ChdScanCacheEnabled && !requiresParent)
                        SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: true, descriptor: "dvd", descriptorSha1: null);
                    if (familyDebug != null && ar.Count > 0)
                    {
                        ScannedFile payload = ar[0];
                        familyDebug.AppendLine("method=" + ar.ChdScanMethod);
                        familyDebug.AppendLine($"payload={payload.Name};size={payload.Size};crc={payload.CRC.ToHexString()};sha1={payload.SHA1.ToHexString()};md5={payload.MD5.ToHexString()}");
                    }
                    WriteChdDebugLog(filename, familyDebug);
                    return ar;
                }

                bool tryStream = !forceExtraction && Settings.rvSettings.ChdStreaming && Settings.rvSettings.ChdNativeVerification &&
                                 Settings.rvSettings.ChdHealthDatabase && ChdHealthStore.HasCurrentExternalParity(filename, Settings.rvSettings.ChdExternalParityDays) &&
                                 !(expectsIso && (expectsCue || expectsGdi || expectsToc)) && !expectsToc && !expectsSbi;
                if (tryStream)
                {
                    try
                    {
                        if (CHDSharpLib.ChdMetadata.TryReadCdTrackLayout(filename, out List<CHDSharpLib.ChdCdTrackInfo> cdTracks, out bool nativeIsGdRom, out string metaErr) && cdTracks.Count > 0)
                        {
                            Dictionary<int, RvFile> expectedByTrack = BuildExpectedTrackMap(expectedChildren);
                            List<RvFile> expectedDataFiles = expectedChildren.FindAll(c => IsTrackDataFile(c.Name));
                            expectedDataFiles.Sort((a, b) => Sorters.DirectoryNameCompareCase(a.Name, b.Name));

                            if (expectedChildren.Count == 0)
                            {
                                // ToSort scanning: produce default track list based on metadata
                                for (int i = 0; i < cdTracks.Count; i++)
                                {
                                    expectedDataFiles.Add(new RvFile(FileType.File) { Name = $"Track {cdTracks[i].TrackNo:D2}.bin" });
                                }
                                expectedByTrack = BuildExpectedTrackMap(expectedDataFiles);
                            }

                            System.Text.StringBuilder debug = Settings.rvSettings.ChdDebug ? new System.Text.StringBuilder() : null;
                            if (debug != null)
                            {
                                debug.AppendLine("CHD scan");
                                debug.AppendLine(ChdDiagnosticFormatter.RedactPath(filename));
                                debug.AppendLine("descriptor=stream");
                                debug.AppendLine("expected:");
                                    for (int i = 0; i < expectedDataFiles.Count; i++)
                                    {
                                        RvFile ef = expectedDataFiles[i];
                                        debug.AppendLine($"  {ef.Name} size={(ef.Size?.ToString() ?? "")} crc={ef.CRC.ToHexString()} sha1={ef.SHA1.ToHexString()} md5={ef.MD5.ToHexString()}");
                                    }
                                debug.AppendLine("tracks:");
                                for (int i = 0; i < cdTracks.Count; i++)
                                    debug.AppendLine($"  track={cdTracks[i].TrackNo:D2} type={cdTracks[i].TrackType} frames={cdTracks[i].Frames} pregap={cdTracks[i].PreGapFrames} postgap={cdTracks[i].PostGapFrames} sector={cdTracks[i].SectorSize}");
                            }

                            ar.ChdScanMethod = "Streaming (CD)";
                            List<(int trackNo, string fileName, string trackType)> trackFiles = new List<(int, string, string)>();
                            Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache = new Dictionary<string, (ulong, byte[], byte[], byte[])>(StringComparer.OrdinalIgnoreCase);

                            using (Stream logical = CHDSharpLib.ChdLogicalStream.OpenRead(filename))
                            {
                                ulong cur = 0;
                                for (int i = 0; i < cdTracks.Count; i++)
                                {
                                    CHDSharpLib.ChdCdTrackInfo t = cdTracks[i];
                                    ulong startBytes = (ulong)Math.Max(0, t.StartFrame) * (ulong)Math.Max(1, t.SectorSize);
                                    ulong lenBytes = (ulong)Math.Max(0, t.Frames) * (ulong)Math.Max(1, t.SectorSize);
                                    if (startBytes < cur)
                                        startBytes = cur;
                                    if (startBytes > cur)
                                    {
                                        SkipBytes(logical, startBytes - cur);
                                        cur = startBytes;
                                    }

                                    string key = "track:" + t.TrackNo.ToString("D2");
                                    trackFiles.Add((t.TrackNo, key, t.TrackType));

                                    ScannedFile tmp = new ScannedFile(FileType.FileCHD)
                                    {
                                        Name = key,
                                        FileModTimeStamp = dbDir.FileModTimeStamp,
                                        GotStatus = GotStatus.Got,
                                        DeepScanned = true,
                                        Size = lenBytes,
                                        ChdScanMethod = "Streaming (CD)"
                                    };
                                    using (ReadOnlyLimitedStream limited = new ReadOnlyLimitedStream(logical, (long)lenBytes))
                                    {
                                        _fileScans.CheckSumRead(limited, tmp, lenBytes, true, false, null, 0, 0);
                                    }
                                    fileHashCache[key] = (lenBytes, tmp.CRC, tmp.SHA1, tmp.MD5);
                                    cur += lenBytes;
                                }
                            }

                            Dictionary<string, string> mapping = BuildDeterministicMapping(fileHashCache, expectedDataFiles, debug);

                            if (debug != null)
                            {
                                debug.AppendLine("hashes:");
                                foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashCache)
                                {
                                    debug.AppendLine($"  {kvp.Key} size={kvp.Value.size} crc={kvp.Value.crc.ToHexString()} sha1={kvp.Value.sha1.ToHexString()} md5={kvp.Value.md5.ToHexString()}");
                                }
                            }

                            if (expectedDataFiles.Count > 0)
                            {
                                for (int i = 0; i < expectedDataFiles.Count; i++)
                                {
                                    RvFile exp = expectedDataFiles[i];
                                    if (exp?.Name == null)
                                        continue;
                                    bool hasHash =
                                        (exp.SHA1 != null && exp.SHA1.Length > 0) ||
                                        (exp.MD5 != null && exp.MD5.Length > 0) ||
                                        (exp.CRC != null && exp.CRC.Length > 0);
                                    if (!hasHash)
                                        continue;
                                    if (mapping.ContainsKey(exp.Name))
                                        continue;
                                    if (debug != null)
                                        debug.AppendLine($"streaming could not map hashed expected member: {exp.Name}; will fall back to extractcd");
                                    WriteChdDebugLog(filename, debug);
                                    throw new Exception("stream hash mismatch");
                                }
                            }

                            foreach (KeyValuePair<string, string> kvp in mapping)
                            {
                                if (!fileHashCache.TryGetValue(kvp.Value, out var h))
                                    continue;
                                if (h.size == 0 || h.crc == null || h.sha1 == null || h.md5 == null)
                                {
                                    if (debug != null)
                                        debug.AppendLine($"streaming hash missing for {kvp.Value} size={h.size} crc={(h.crc == null ? "null" : "ok")} sha1={(h.sha1 == null ? "null" : "ok")} md5={(h.md5 == null ? "null" : "ok")}; will fall back to extractcd");
                                    WriteChdDebugLog(filename, debug);
                                    throw new Exception("stream hash missing");
                                }
                                ScannedFile sf = new ScannedFile(FileType.FileCHD)
                                {
                                    Name = kvp.Key,
                                    FileModTimeStamp = dbDir.FileModTimeStamp,
                                    GotStatus = GotStatus.Got,
                                    DeepScanned = true,
                                    Size = h.size,
                                    CRC = h.crc,
                                    SHA1 = h.sha1,
                                    MD5 = h.md5,
                                    ChdScanMethod = "Streaming (CD)",
                                    ChdHashMatchMode = "Exact"
                                };
                                sf.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                                ar.Add(sf);
                            }

                            Dictionary<string, ScannedFile> byName = new Dictionary<string, ScannedFile>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < ar.Count; i++)
                            {
                                if (ar[i]?.Name != null && !byName.ContainsKey(ar[i].Name))
                                    byName.Add(ar[i].Name, ar[i]);
                            }
                            bool hashMismatch = false;
                            for (int i = 0; i < expectedDataFiles.Count; i++)
                            {
                                RvFile exp = expectedDataFiles[i];
                                if (exp?.Name == null)
                                    continue;
                                if (!byName.TryGetValue(exp.Name, out ScannedFile got))
                                    continue;
                                if (exp.Size.HasValue && exp.Size.Value != 0 && got.Size != exp.Size.Value)
                                    hashMismatch = true;
                                if (exp.CRC != null && got.CRC != null && !exp.CRC.AsSpan().SequenceEqual(got.CRC))
                                    hashMismatch = true;
                                if (exp.SHA1 != null && got.SHA1 != null && !exp.SHA1.AsSpan().SequenceEqual(got.SHA1))
                                    hashMismatch = true;
                                if (exp.MD5 != null && got.MD5 != null && !exp.MD5.AsSpan().SequenceEqual(got.MD5))
                                    hashMismatch = true;
                                if (hashMismatch)
                                    break;
                            }
                            if (hashMismatch)
                            {
                                if (debug != null)
                                    debug.AppendLine("streaming hash mismatch; will fall back to extractcd");
                                WriteChdDebugLog(filename, debug);
                                throw new Exception("stream hash mismatch");
                            }

                            RvFile expectedCue = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase));
                            RvFile expectedGdi = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase));
                            if (expectedCue != null || expectedGdi != null)
                            {
                                string descName = expectedGdi?.Name ?? expectedCue?.Name;
                                ScannedFile dsf = new ScannedFile(FileType.FileCHD)
                                {
                                    Name = descName,
                                    FileModTimeStamp = dbDir.FileModTimeStamp,
                                    GotStatus = GotStatus.Got,
                                    DeepScanned = true,
                                    Size = 0
                                };
                                bool haveDescriptor = false;
                                bool keepDescriptor = Settings.rvSettings.ChdKeepCueGdi;
                                if (keepDescriptor && !string.IsNullOrWhiteSpace(descName))
                                {
                                    try
                                    {
                                        string descFileName = System.IO.Path.GetFileName((descName ?? "").Replace('\\', '/'));
                                        string externalDesc = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename) ?? "", descFileName);
                                        if (System.IO.File.Exists(externalDesc))
                                        {
                                            FileInfo fi = new FileInfo(externalDesc);
                                            dsf.Size = (ulong)fi.Length;
                                            using (Stream s = System.IO.File.OpenRead(externalDesc))
                                            {
                                                _fileScans.CheckSumRead(s, dsf, (ulong)fi.Length, true, false, null, 0, 0);
                                            }
                                            haveDescriptor = true;
                                        }
                                    }
                                    catch
                                    {
                                    }
                                }
                                if (!haveDescriptor)
                                {
                                    string descText = expectedGdi != null
                                        ? ChdDescriptorGenerator.BuildGdi(cdTracks, expectedByTrack)
                                        : ChdDescriptorGenerator.BuildCue(cdTracks, expectedByTrack, nativeIsGdRom);
                                    byte[] bytes = System.Text.Encoding.ASCII.GetBytes(descText ?? "");
                                    dsf.Size = (ulong)bytes.LongLength;
                                    using (var ms = new System.IO.MemoryStream(bytes, false))
                                    {
                                        _fileScans.CheckSumRead(ms, dsf, (ulong)bytes.LongLength, true, false, null, 0, 0);
                                    }
                                }
                                bool ok = true;
                                RvFile expDesc = expectedGdi ?? expectedCue;
                                if (expDesc != null)
                                {
                                    if (expDesc.Size.HasValue && expDesc.Size.Value != 0 && expDesc.Size.Value != dsf.Size)
                                        ok = false;
                                    if (expDesc.CRC != null && dsf.CRC != null && !expDesc.CRC.AsSpan().SequenceEqual(dsf.CRC))
                                        ok = false;
                                    if (expDesc.SHA1 != null && dsf.SHA1 != null && !expDesc.SHA1.AsSpan().SequenceEqual(dsf.SHA1))
                                        ok = false;
                                    if (expDesc.MD5 != null && dsf.MD5 != null && !expDesc.MD5.AsSpan().SequenceEqual(dsf.MD5))
                                        ok = false;
                                }
                                if (ok || (Settings.rvSettings.ChdPreferSynthetic && (expDesc == null || (!expDesc.Size.HasValue || expDesc.Size.Value == 0) && expDesc.CRC == null && expDesc.SHA1 == null && expDesc.MD5 == null)))
                                {
                                    if (keepDescriptor && haveDescriptor)
                                        dsf.ChdDescriptorMatch = "External";
                                    else
                                        dsf.ChdDescriptorMatch = ok ? "True" : "Synthetic";
                                    ar.Add(dsf);
                                }
                                else
                                {
                                    if (debug != null)
                                        debug.AppendLine("synthetic descriptor hash mismatch; will fall back to extractcd");
                                    WriteChdDebugLog(filename, debug);
                                    throw new Exception("synthetic descriptor mismatch");
                                }
                            }

                            if (hashMismatch)
                {
                    ar.ChdStatus = "Hash mismatch (Track hashes do not match DAT)";
                }

                ar.Sort();
                            if (Settings.rvSettings.ChdScanCacheEnabled && !requiresParent)
                            {
                                string layoutHash = ComputeTrackLayoutSha1Hex(cdTracks);
                                SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: false, descriptor: expectsGdi ? "gdi" : "cue", descriptorSha1: layoutHash);
                            }
                            WriteChdDebugLog(filename, debug);
                            return ar;
                        }

                        if (Settings.rvSettings.ChdStreaming)
                            _thWrk?.Report(new bgwShowError(filename, "CHD streaming track metadata not available; falling back to extractcd. " + metaErr));
                    }
                    catch (Exception)
                    {
                        tryStream = false;
                        ar = new ScannedFile(FileType.CHD)
                        {
                            Name = filename,
                            ZipStruct = ZipStructure.None,
                            Comment = ""
                        };
                        using (FileStream fs = System.IO.File.OpenRead(filename))
                        {
                            ar.Size = (ulong)fs.Length;
                            _fileScans.CheckSumRead(fs, ar, ar.Size.Value, true, false, null, 0, 0);
                        }
                    }
                }
                {
                if (!TryPrepareChdScanWorkspace(filename, chdmanExe, false, parentPath, requiresParent && expectsToc, out tempDir, out extractor, out string spaceError))
                {
                    _thWrk?.Report(new bgwShowError(filename, spaceError));
                    return null;
                }
                string outMain = System.IO.Path.Combine(tempDir, expectsGdi ? "disc.gdi" : "disc.cue");
                if (!extractor.ExtractCd(filename, outMain, out string err1))
                {
                    _thWrk?.Report(new bgwShowError(filename, "CHD extractcd failed: " + err1));
                    return null;
                }

                if (!System.IO.File.Exists(outMain))
                {
                    _thWrk?.Report(new bgwShowError(filename, "CHD extraction did not produce main descriptor: " + outMain));
                    return null;
                }

                string descriptorForHash = outMain;
                if (ChdReconstructionManifest.TryRead(filename, out ChdReconstructionManifest embeddedDescriptorManifest, out _) &&
                    embeddedDescriptorManifest.DescriptorBytes != null && embeddedDescriptorManifest.DescriptorBytes.Length > 0)
                {
                    string embeddedExtension = System.IO.Path.GetExtension(embeddedDescriptorManifest.DescriptorName ?? "");
                    bool matchingFamily = expectsGdi
                        ? string.Equals(embeddedExtension, ".gdi", StringComparison.OrdinalIgnoreCase)
                        : expectsToc
                            ? string.Equals(embeddedExtension, ".toc", StringComparison.OrdinalIgnoreCase)
                            : string.Equals(embeddedExtension, ".cue", StringComparison.OrdinalIgnoreCase);
                    if (matchingFamily)
                    {
                        descriptorForHash = System.IO.Path.Combine(tempDir,
                            expectsGdi ? "embedded.gdi" : expectsToc ? "embedded.toc" : "embedded.cue");
                        System.IO.File.WriteAllBytes(descriptorForHash, embeddedDescriptorManifest.DescriptorBytes);
                    }
                }
                string descriptorSha1 = ComputeFileSha1Hex(descriptorForHash);
                List<(int trackNo, string fileName, string trackType)> trackFiles = expectsGdi ? ParseGdiTrackFiles(outMain) : ParseCueTrackFiles(outMain);
                if (expectsToc)
                {
                    if (!ChdReconstructionManifest.TryRead(filename, out ChdReconstructionManifest tocManifest, out string tocManifestError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD TOC reconstruction metadata is unavailable: " + tocManifestError));
                        return null;
                    }
                    string tocPayloadName = "rv-native-toc-payload.bin";
                    string tocPayloadPath = System.IO.Path.Combine(tempDir, tocPayloadName);
                    string tocLogicalSource = filename;
                    if (requiresParent && !TryMaterializeStandaloneForScan(chdmanExe, filename, parentPath, tempDir, out tocLogicalSource, out string standaloneError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD parent materialization failed: " + standaloneError));
                        return null;
                    }
                    if (!ChdOpticalReconstruction.TryMaterializeSingleTocPayload(tocLogicalSource, tocManifest, tocPayloadPath, out string tocError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD exact TOC extraction failed: " + tocError));
                        return null;
                    }
                    trackFiles.Clear();
                    trackFiles.Add((tocManifest.Tracks[0].Number, tocPayloadName, "TOC exact"));
                }
                if (trackFiles.Count == 0)
                {
                    _thWrk?.Report(new bgwShowError(filename, "No track files listed in extracted " + (expectsGdi ? "GDI" : "CUE")));
                    return null;
                }

                Dictionary<int, RvFile> expectedByTrack = BuildExpectedTrackMap(expectedChildren);
                List<RvFile> expectedDataFiles = expectedChildren.FindAll(c => IsTrackDataFile(c.Name));
                expectedDataFiles.Sort((a, b) => Sorters.DirectoryNameCompareCase(a.Name, b.Name));

                if (expectedChildren.Count == 0)
                {
                    // ToSort scanning: produce default track list based on extracted files
                    // We use "Track NN.bin" names so they are distinct and have track numbers for slicing/mapping.
                    for (int i = 0; i < trackFiles.Count; i++)
                    {
                        string ext = System.IO.Path.GetExtension(trackFiles[i].fileName) ?? ".bin";
                        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
                        expectedDataFiles.Add(new RvFile(FileType.File) { Name = $"Track {trackFiles[i].trackNo:D2}{ext}" });
                    }
                    expectedByTrack = BuildExpectedTrackMap(expectedDataFiles);
                }

                System.Text.StringBuilder debug = Settings.rvSettings.ChdDebug ? new System.Text.StringBuilder() : null;
                if (debug != null)
                {
                    debug.AppendLine("CHD scan");
                    debug.AppendLine(ChdDiagnosticFormatter.RedactPath(filename));
                    debug.AppendLine("descriptor=" + (expectsGdi ? "gdi" : "cue"));
                    debug.AppendLine("expected:");
                    for (int i = 0; i < expectedDataFiles.Count; i++)
                    {
                        RvFile ef = expectedDataFiles[i];
                        debug.AppendLine($"  {ef.Name} size={(ef.Size?.ToString() ?? "")} crc={ef.CRC.ToHexString()} sha1={ef.SHA1.ToHexString()} md5={ef.MD5.ToHexString()}");
                    }
                    debug.AppendLine("extracted:");
                    for (int i = 0; i < trackFiles.Count; i++)
                        debug.AppendLine($"  track={trackFiles[i].trackNo:D2} file={trackFiles[i].fileName} type={trackFiles[i].trackType}");
                }

                ar.ChdScanMethod = "Extraction (CD)";
                ConcurrentDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache = new ConcurrentDictionary<string, (ulong, byte[], byte[], byte[])>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> uniqueExtractedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < trackFiles.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(trackFiles[i].fileName))
                        uniqueExtractedSet.Add(trackFiles[i].fileName);
                }
                string[] uniqueExtractedArr = new string[uniqueExtractedSet.Count];
                uniqueExtractedSet.CopyTo(uniqueExtractedArr);
                Parallel.ForEach(uniqueExtractedArr, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 4)) }, extractedName =>
                {
                    string full = System.IO.Path.Combine(tempDir, extractedName);
                    if (!System.IO.File.Exists(full))
                        return;
                    if (fileHashCache.ContainsKey(extractedName))
                        return;
                    FileInfo fi = new FileInfo(full);
                    ScannedFile tmp = new ScannedFile(FileType.FileCHD)
                    {
                        Name = extractedName,
                        FileModTimeStamp = fi.LastWriteTime,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = (ulong)fi.Length,
                        ChdScanMethod = "Extraction (CD)"
                    };
                    using (Stream s = System.IO.File.OpenRead(full))
                    {
                        _fileScans.CheckSumRead(s, tmp, (ulong)fi.Length, true, false, null, 0, 0);
                    }
                    fileHashCache.TryAdd(extractedName, ((ulong)fi.Length, tmp.CRC, tmp.SHA1, tmp.MD5));

                });

                if (debug != null)
                {
                    debug.AppendLine("hashes:");
                    for (int i = 0; i < uniqueExtractedArr.Length; i++)
                    {
                        string n = uniqueExtractedArr[i];
                        if (!fileHashCache.TryGetValue(n, out var h))
                            continue;
                        debug.AppendLine($"  {n} size={h.size} crc={h.crc.ToHexString()} sha1={h.sha1.ToHexString()} md5={h.md5.ToHexString()}");
                    }
                }

                Dictionary<string, string> mapping = BuildDeterministicMapping(fileHashCache, expectedDataFiles, debug);

                HashSet<string> usedExtracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> kvp in mapping)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                        usedExtracted.Add(kvp.Value);
                }
                foreach (KeyValuePair<string, string> kvp in mapping)
                {
                    string expectedName = kvp.Key;
                    string extractedName = kvp.Value;
                    if (string.IsNullOrWhiteSpace(expectedName) || string.IsNullOrWhiteSpace(extractedName))
                        continue;
                    if (!fileHashCache.TryGetValue(extractedName, out var h))
                        continue;

                    ScannedFile sf = new ScannedFile(FileType.FileCHD)
                    {
                        Name = expectedName,
                        FileModTimeStamp = dbDir.FileModTimeStamp,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = h.size,
                        CRC = h.crc,
                        SHA1 = h.sha1,
                        MD5 = h.md5,
                        ChdScanMethod = "Extraction (CD)",
                        ChdHashMatchMode = "Exact"
                    };
                    sf.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                    ar.Add(sf);
                }

                for (int i = 0; i < trackFiles.Count; i++)
                {
                    string extractedName = trackFiles[i].fileName;
                    if (string.IsNullOrWhiteSpace(extractedName))
                        continue;
                    if (usedExtracted.Contains(extractedName))
                        continue;
                    if (!fileHashCache.TryGetValue(extractedName, out var h))
                        continue;
                    ScannedFile extra = new ScannedFile(FileType.FileCHD)
                    {
                        Name = extractedName,
                        FileModTimeStamp = dbDir.FileModTimeStamp,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = h.size,
                        CRC = h.crc,
                        SHA1 = h.sha1,
                        MD5 = h.md5,
                        ChdScanMethod = "Extraction (CD)",
                        ChdHashMatchMode = "Unmapped"
                    };
                    extra.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                    ar.Add(extra);
                }

                if (expectsIso)
                {
                    if (!ChdReconstructionManifest.TryRead(filename, out ChdReconstructionManifest multiManifest, out string multiManifestError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD ISO view metadata is unavailable: " + multiManifestError));
                        return null;
                    }
                    ChdManifestView isoView = ChdMultiView.GetIsoView(multiManifest);
                    if (isoView == null || multiManifest.Tracks.Count != 1)
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD does not contain a proven reversible ISO view."));
                        return null;
                    }
                    List<RvFile> primaryMatches = expectedDataFiles.FindAll(item =>
                        ManifestTrackMatchesExpected(multiManifest.Tracks[0], item));
                    RvFile primaryExpected = primaryMatches.Count == 1 ? primaryMatches[0] : null;
                    if (primaryExpected == null || !mapping.TryGetValue(primaryExpected.Name, out string primaryExtracted))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD primary track could not be mapped for ISO reconstruction."));
                        return null;
                    }
                    string isoOutput = System.IO.Path.Combine(tempDir, "view.iso");
                    if (!ChdMultiView.TryMaterializeIsoView(System.IO.Path.Combine(tempDir, primaryExtracted), isoView, isoOutput, out string isoError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD ISO view reconstruction failed: " + isoError));
                        return null;
                    }
                    FileInfo isoInfo = new FileInfo(isoOutput);
                    ScannedFile isoScanned = new ScannedFile(FileType.FileCHD)
                    {
                        Name = expectedIsoName ?? isoView.Tracks[0].Name,
                        FileModTimeStamp = isoInfo.LastWriteTime,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = (ulong)isoInfo.Length,
                        ChdScanMethod = "Reconstruction (ISO view)",
                        ChdHashMatchMode = "Exact"
                    };
                    using (Stream isoStream = System.IO.File.OpenRead(isoOutput))
                        _fileScans.CheckSumRead(isoStream, isoScanned, (ulong)isoInfo.Length, true, false, null, 0, 0);
                    isoScanned.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                    ar.Add(isoScanned);
                }

                RvFile expectedSbi = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".sbi", StringComparison.OrdinalIgnoreCase));
                if (expectedSbi != null)
                {
                    if (!ChdReconstructionManifest.TryRead(filename, out ChdReconstructionManifest sbiManifest, out string sbiManifestError))
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD SBI reconstruction metadata is unavailable: " + sbiManifestError));
                        return null;
                    }
                    ChdManifestAuxiliary sbi = sbiManifest.Auxiliaries.Find(item =>
                        item != null &&
                        string.Equals(item.Role, "sbi-subchannel-correction", StringComparison.OrdinalIgnoreCase));
                    if (sbi == null || sbi.Bytes == null)
                    {
                        _thWrk?.Report(new bgwShowError(filename, "CHD does not contain the exact DAT SBI payload."));
                        return null;
                    }
                    ScannedFile sbiScanned = new ScannedFile(FileType.FileCHD)
                    {
                        Name = expectedSbi.Name,
                        FileModTimeStamp = dbDir.FileModTimeStamp,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = (ulong)sbi.Bytes.Length,
                        ChdScanMethod = "Reconstruction (embedded SBI)",
                        ChdHashMatchMode = "Exact"
                    };
                    using (MemoryStream sbiStream = new MemoryStream(sbi.Bytes, false))
                        _fileScans.CheckSumRead(sbiStream, sbiScanned, (ulong)sbi.Bytes.Length, true, false, null, 0, 0);
                    bool sbiMatches = HasExpectedHash(expectedSbi) &&
                        (!expectedSbi.Size.HasValue || expectedSbi.Size.Value == 0 || expectedSbi.Size.Value == sbiScanned.Size) &&
                        (expectedSbi.CRC == null || (sbiScanned.CRC != null && expectedSbi.CRC.AsSpan().SequenceEqual(sbiScanned.CRC))) &&
                        (expectedSbi.SHA1 == null || (sbiScanned.SHA1 != null && expectedSbi.SHA1.AsSpan().SequenceEqual(sbiScanned.SHA1))) &&
                        (expectedSbi.MD5 == null || (sbiScanned.MD5 != null && expectedSbi.MD5.AsSpan().SequenceEqual(sbiScanned.MD5)));
                    if (!sbiMatches)
                    {
                        _thWrk?.Report(new bgwShowError(filename, "Embedded SBI payload does not match the DAT."));
                        return null;
                    }
                    sbiScanned.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                    ar.Add(sbiScanned);
                }

                RvFile expectedCue = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase));
                RvFile expectedGdi = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase));
                RvFile expectedToc = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".toc", StringComparison.OrdinalIgnoreCase));
                if (expectedCue != null || expectedGdi != null || expectedToc != null)
                {
                    string descName = expectedGdi?.Name ?? expectedToc?.Name ?? expectedCue?.Name;
                    RvFile expDesc = expectedGdi ?? expectedToc ?? expectedCue;
                    bool keepDescriptor = Settings.rvSettings.ChdKeepCueGdi;

                    ScannedFile dsf = new ScannedFile(FileType.FileCHD)
                    {
                        Name = descName,
                        FileModTimeStamp = dbDir.FileModTimeStamp,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = 0
                    };

                    bool haveDescriptor = false;
                    bool externalDescriptor = false;
                    if (keepDescriptor && !string.IsNullOrWhiteSpace(descName))
                    {
                        try
                        {
                            string descFileName = System.IO.Path.GetFileName((descName ?? "").Replace('\\', '/'));
                            string externalDesc = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename) ?? "", descFileName);
                            if (System.IO.File.Exists(externalDesc))
                            {
                                FileInfo fi = new FileInfo(externalDesc);
                                dsf.Size = (ulong)fi.Length;
                                using (Stream s = System.IO.File.OpenRead(externalDesc))
                                {
                                    _fileScans.CheckSumRead(s, dsf, (ulong)fi.Length, true, false, null, 0, 0);
                                }
                                haveDescriptor = true;
                                externalDescriptor = true;
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (!haveDescriptor)
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(descriptorForHash);
                            dsf.Size = (ulong)fi.Length;
                            using (Stream s = System.IO.File.OpenRead(descriptorForHash))
                            {
                                _fileScans.CheckSumRead(s, dsf, (ulong)fi.Length, true, false, null, 0, 0);
                            }
                            haveDescriptor = true;
                        }
                        catch
                        {
                            haveDescriptor = false;
                        }
                    }

                    if (haveDescriptor)
                    {
                        bool ok = true;
                        if (expDesc != null)
                        {
                            if (expDesc.Size.HasValue && expDesc.Size.Value != 0 && expDesc.Size.Value != dsf.Size)
                                ok = false;
                            if (expDesc.CRC != null && dsf.CRC != null && !expDesc.CRC.AsSpan().SequenceEqual(dsf.CRC))
                                ok = false;
                            if (expDesc.SHA1 != null && dsf.SHA1 != null && !expDesc.SHA1.AsSpan().SequenceEqual(dsf.SHA1))
                                ok = false;
                            if (expDesc.MD5 != null && dsf.MD5 != null && !expDesc.MD5.AsSpan().SequenceEqual(dsf.MD5))
                                ok = false;
                        }

                        bool semanticOpticalDescriptor = !ok && !externalDescriptor && expectedToc == null &&
                            (expectedCue != null || expectedGdi != null);
                        if (ok || semanticOpticalDescriptor || (Settings.rvSettings.ChdPreferSynthetic && (expDesc == null || (!expDesc.Size.HasValue || expDesc.Size.Value == 0) && expDesc.CRC == null && expDesc.SHA1 == null && expDesc.MD5 == null)))
                        {
                            string descFileName = System.IO.Path.GetFileName((descName ?? "").Replace('\\', '/'));
                            if (keepDescriptor && !string.IsNullOrWhiteSpace(descFileName) && System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename) ?? "", descFileName)))
                                dsf.ChdDescriptorMatch = "External";
                            else if (!string.Equals(descriptorForHash, outMain, StringComparison.OrdinalIgnoreCase))
                                dsf.ChdDescriptorMatch = "Embedded Exact";
                            else
                                dsf.ChdDescriptorMatch = ok ? "True" : semanticOpticalDescriptor ? "Semantic" : "Synthetic";
                            ar.Add(dsf);
                        }
                    }
                }

                ar.Sort();
                if (Settings.rvSettings.ChdHealthDatabase && !requiresParent && !expectsIso &&
                    !ChdHealthStore.HasCurrentExternalParity(filename, Settings.rvSettings.ChdExternalParityDays))
                {
                    int parityCode = ChdVerify.TryGenerateParityReport(filename, expectedChildren, out _);
                    ChdHealthStore.RecordParity(filename, expectsGdi ? "gdi" : "cd", datRule?.ChdStorageProfile.ToString().ToLowerInvariant(),
                        GetChdmanSha256(chdmanExe), "track-stream", "track-extract", "scan-track-parity", parityCode == 0);
                }
                if (Settings.rvSettings.ChdScanCacheEnabled && !requiresParent)
                    SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: false, descriptor: expectsGdi ? "gdi" : expectsToc ? "toc" : "cue", descriptorSha1: descriptorSha1);
                WriteChdDebugLog(filename, debug);
                return ar;
                }
            }
            finally
            {
                if (!ChdTemporaryWorkspace.TryDelete(tempDir, out string cleanupError))
                    _thWrk?.Report(new bgwShowError(filename, "Could not clean the CHD scan workspace: " + cleanupError));
            }
        }

        internal static List<RvFile> GetExpectedChdMembers(RvFile dbDir)
        {
            List<RvFile> expected = new List<RvFile>();
            for (int i = 0; dbDir != null && i < dbDir.ChildCount; i++)
            {
                RvFile child = dbDir.Child(i);
                if (child == null || !child.IsFile)
                    continue;

                switch (child.DatStatus)
                {
                    case DatStatus.InDatCollect:
                    case DatStatus.InDatMerged:
                    case DatStatus.InDatNoDump:
                        expected.Add(child);
                        break;
                }
            }
            return expected;
        }

        internal static bool RunExpectedChdMembersSelfTest(out string error)
        {
            error = "";
            try
            {
                RepairStatus.InitStatusCheck();
                RvFile chd = new RvFile(FileType.CHD) { Name = "disc.cue.chd" };
                RvFile expectedTrack = new RvFile(FileType.FileCHD) { Name = "disc (Track 01).bin" };
                expectedTrack.SetDatGotStatus(DatStatus.InDatCollect, GotStatus.NotGot);
                chd.ChildAdd(expectedTrack);

                RvFile staleCue = new RvFile(FileType.FileCHD) { Name = "disc.cue" };
                staleCue.SetDatGotStatus(DatStatus.NotInDat, GotStatus.Got);
                chd.ChildAdd(staleCue);

                List<RvFile> expected = GetExpectedChdMembers(chd);
                if (expected.Count != 1 || !ReferenceEquals(expected[0], expectedTrack))
                    throw new InvalidOperationException("A stale virtual CHD descriptor was treated as a DAT expectation.");

                chd.MarkAsMissing();
                if (chd.ChildCount != 1 || !ReferenceEquals(chd.Child(0), expectedTrack))
                    throw new InvalidOperationException("A stale virtual CHD descriptor survived the next container scan.");

                RvFile mergedCue = new RvFile(FileType.FileCHD) { Name = "disc-original.cue" };
                mergedCue.SetDatGotStatus(DatStatus.InDatMerged, GotStatus.NotGot);
                chd.ChildAdd(mergedCue);
                expected = GetExpectedChdMembers(chd);
                if (expected.Count != 2 || !expected.Contains(mergedCue))
                    throw new InvalidOperationException("A DAT-backed merged descriptor was omitted from CHD expectations.");

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string ResolveExistingFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            path = NormalizePossiblyConcatenatedPath(path);

            try
            {
                if (System.IO.Path.IsPathRooted(path))
                {
                    if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
                        return path;
                }
            }
            catch
            {
            }

            try
            {
                string baseDir = "";
                try { baseDir = AppDomain.CurrentDomain.BaseDirectory; } catch { }
                System.IO.DirectoryInfo di = string.IsNullOrWhiteSpace(baseDir) ? null : new System.IO.DirectoryInfo(baseDir);
                for (int i = 0; i < 10 && di != null; i++)
                {
                    string attempt = System.IO.Path.Combine(di.FullName, path);
                    if (System.IO.File.Exists(attempt) || System.IO.Directory.Exists(attempt))
                        return attempt;
                    di = di.Parent;
                }
            }
            catch
            {
            }

            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private static string ResolveExistingDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string p = NormalizePossiblyConcatenatedPath(path);
            try
            {
                if (System.IO.Path.IsPathRooted(p))
                {
                    if (System.IO.Directory.Exists(p))
                        return p;
                }
            }
            catch
            {
            }

            try
            {
                if (System.IO.Directory.Exists(p))
                    return System.IO.Path.GetFullPath(p);
            }
            catch
            {
            }

            try
            {
                string baseDir = "";
                try { baseDir = AppDomain.CurrentDomain.BaseDirectory; } catch { }
                System.IO.DirectoryInfo di = string.IsNullOrWhiteSpace(baseDir) ? null : new System.IO.DirectoryInfo(baseDir);
                for (int i = 0; i < 10 && di != null; i++)
                {
                    string attempt = System.IO.Path.Combine(di.FullName, p);
                    if (System.IO.Directory.Exists(attempt))
                        return attempt;
                    di = di.Parent;
                }
            }
            catch
            {
            }

            try
            {
                return System.IO.Path.GetFullPath(p);
            }
            catch
            {
                return p;
            }
        }

        private static string NormalizePossiblyConcatenatedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            string p = path.Trim();
            int last = -1;
            for (int i = 0; i + 2 < p.Length; i++)
            {
                char c0 = p[i];
                char c1 = p[i + 1];
                char c2 = p[i + 2];
                if (((c0 >= 'A' && c0 <= 'Z') || (c0 >= 'a' && c0 <= 'z')) && c1 == ':' && (c2 == '\\' || c2 == '/'))
                    last = i;
            }
            if (last > 0)
                return p.Substring(last);
            return p;
        }

        private static bool HasExpectedHash(RvFile f)
        {
            if (f == null)
                return false;
            if (f.SHA1 != null && f.SHA1.Length > 0)
                return true;
            if (f.MD5 != null && f.MD5.Length > 0)
                return true;
            if (f.CRC != null && f.CRC.Length > 0)
                return true;
            return false;
        }

        /// <summary>
        /// Maps DAT members only when every supplied size/hash constraint matches bytes read directly
        /// from the extracted or streamed CHD payload.
        /// </summary>
        private static Dictionary<string, string> BuildDeterministicMapping(
            IDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache,
            List<RvFile> expectedDataFiles,
            System.Text.StringBuilder debug)
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedExtracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> extractedNames = new List<string>(fileHashCache.Keys);
            extractedNames.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < expectedDataFiles.Count; i++)
            {
                RvFile expected = expectedDataFiles[i];
                if (expected == null || string.IsNullOrWhiteSpace(expected.Name) || !HasExpectedHash(expected))
                    continue;

                for (int c = 0; c < extractedNames.Count; c++)
                {
                    string extractedName = extractedNames[c];
                    if (usedExtracted.Contains(extractedName))
                        continue;
                    if (!fileHashCache.TryGetValue(extractedName, out var actual))
                        continue;
                    if (!ExtractedHashesMatchExpected(expected, actual))
                        continue;

                    mapping.Add(expected.Name, extractedName);
                    usedExtracted.Add(extractedName);
                    if (debug != null)
                        debug.AppendLine($"  reason: {expected.Name} <= {extractedName} :: exact extracted payload hashes");
                    break;
                }
            }

            if (debug != null)
            {
                debug.AppendLine("mapping:");
                foreach (KeyValuePair<string, string> kvp in mapping)
                    debug.AppendLine($"  {kvp.Key} <= {kvp.Value} (exact)");
            }

            return mapping;
        }

        private static bool ExtractedHashesMatchExpected(
            RvFile expected,
            (ulong size, byte[] crc, byte[] sha1, byte[] md5) actual)
        {
            if (expected == null || !HasExpectedHash(expected))
                return false;
            if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != actual.size)
                return false;
            if (expected.CRC != null && expected.CRC.Length > 0 &&
                (actual.crc == null || !expected.CRC.AsSpan().SequenceEqual(actual.crc)))
                return false;
            if (expected.SHA1 != null && expected.SHA1.Length > 0 &&
                (actual.sha1 == null || !expected.SHA1.AsSpan().SequenceEqual(actual.sha1)))
                return false;
            if (expected.MD5 != null && expected.MD5.Length > 0 &&
                (actual.md5 == null || !expected.MD5.AsSpan().SequenceEqual(actual.md5)))
                return false;
            return true;
        }

        private static bool ManifestTrackMatchesExpected(ChdManifestTrack track, RvFile expected)
        {
            if (track == null || expected == null || !HasExpectedHash(expected))
                return false;
            if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != (ulong)Math.Max(0, track.Size))
                return false;
            if (expected.CRC != null && expected.CRC.Length > 0 &&
                (track.Crc32 == null || !expected.CRC.AsSpan().SequenceEqual(track.Crc32)))
                return false;
            if (expected.SHA1 != null && expected.SHA1.Length > 0 &&
                (track.Sha1 == null || !expected.SHA1.AsSpan().SequenceEqual(track.Sha1)))
                return false;
            if (expected.MD5 != null && expected.MD5.Length > 0 &&
                (track.Md5 == null || !expected.MD5.AsSpan().SequenceEqual(track.Md5)))
                return false;
            return true;
        }

        public static string GetChdScanDebugDirectory()
        {
            string baseTempDir = ResolveExistingDirectoryPath(DB.GetToSortCache()?.FullName);
            if (string.IsNullOrWhiteSpace(baseTempDir))
                baseTempDir = Environment.CurrentDirectory;
            return System.IO.Path.Combine(baseTempDir, "__RomVault.chdlogs");
        }

        private static string GetChdScanDebugPath(string chdPath)
        {
            string dir = GetChdScanDebugDirectory();
            string key = ComputeMd5Hex(chdPath.ToLowerInvariant());
            return System.IO.Path.Combine(dir, key + ".log");
        }

        private static void WriteChdDebugLog(string chdPath, System.Text.StringBuilder debug)
        {
            if (debug == null)
                return;
            try
            {
                string path = GetChdScanDebugPath(chdPath);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, debug.ToString());
            }
            catch
            {
            }
        }

        /// <summary>
        /// Parses a CUE file and returns referenced track files with inferred track numbers and types.
        /// </summary>
        private static List<(int trackNo, string fileName, string trackType)> ParseCueTrackFiles(string cuePath)
        {
            List<(int, string, string)> list = new List<(int, string, string)>();
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(cuePath);
            }
            catch
            {
                return list;
            }

            string currentFile = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length == 0)
                    continue;

                if (trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                {
                    int q1 = trimmed.IndexOf('"');
                    if (q1 >= 0)
                    {
                        int q2 = trimmed.IndexOf('"', q1 + 1);
                        if (q2 > q1)
                        {
                            currentFile = trimmed.Substring(q1 + 1, q2 - q1 - 1);
                            continue;
                        }
                    }
                    string[] p = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 2)
                        currentFile = p[1].Trim('"');
                    continue;
                }

                if (trimmed.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
                {
                    string[] p = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3 && int.TryParse(p[1], out int trackNo))
                    {
                        string trackType = p[2].Trim();
                        if (!string.IsNullOrWhiteSpace(currentFile))
                            list.Add((trackNo, currentFile, trackType));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Parses a GDI file and returns referenced track files with inferred track numbers and types.
        /// </summary>
        /// <remarks>
        /// Filenames may contain spaces, so parsing must respect quoted filenames where present.
        /// </remarks>
        private static List<(int trackNo, string fileName, string trackType)> ParseGdiTrackFiles(string gdiPath)
        {
            List<(int, string, string)> list = new List<(int, string, string)>();
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(gdiPath);
            }
            catch
            {
                return list;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;

                if (!int.TryParse(parts[0], out int trackNo))
                    continue;

                string name = parts[4].Trim().Trim('"');
                string trackType = parts.Length >= 3 ? parts[2] : "";
                if (!string.IsNullOrWhiteSpace(name))
                    list.Add((trackNo, name, trackType));
            }

            return list;
        }

        /// <summary>
        /// Attempts to load an in-memory scan result for a CHD file and validates it against current rules.
        /// </summary>
        /// <remarks>
        /// Cache validity requires:
        /// - same source path, size, and last write time
        /// - same cache version and mapping fingerprint
        /// - same expected descriptor type (dvd/cue/gdi/trust)
        /// </remarks>
        private static bool TryLoadChdScanCache(string chdPath, DatRule datRule, string chdmanExe, out ChdCacheFile cache)
        {
            cache = null;
            try
            {
                FileInfo fi = new FileInfo(chdPath);
                string cacheKey = System.IO.Path.GetFullPath(chdPath);
                ChdCacheFile loaded;
                lock (ChdScanCacheLock)
                {
                    if (!ChdScanMemoryCache.TryGetValue(cacheKey, out loaded))
                        return false;
                }

                if (loaded.CacheVersion != ChdScanCacheVersion)
                    return false;
                if (!string.Equals(loaded.SourcePath, chdPath, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (loaded.SourceTimestamp != fi.LastWriteTime)
                    return false;
                if (loaded.SourceSize != fi.Length)
                    return false;
                string settingsFingerprint = ComputeChdSettingsFingerprint(datRule);
                if (!string.Equals(loaded.SettingsFingerprint ?? "", settingsFingerprint ?? "", StringComparison.OrdinalIgnoreCase))
                    return false;

                string workingDir = "";
                try { workingDir = System.IO.Path.GetDirectoryName(chdPath) ?? Environment.CurrentDirectory; } catch { workingDir = Environment.CurrentDirectory; }
                string toolFingerprint = ComputeChdToolFingerprint(chdmanExe, workingDir);
                if (!string.IsNullOrWhiteSpace(toolFingerprint) &&
                    !string.Equals(loaded.ToolFingerprint ?? "", toolFingerprint, StringComparison.OrdinalIgnoreCase))
                    return false;

                cache = loaded;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stores the scan result of a CHD container in the bounded process-local cache.
        /// </summary>
        /// <remarks>
        /// Only member entries (<see cref="FileType.FileCHD"/>) are retained, and nothing is written beside the user's CHDs.
        /// </remarks>
        private static void SaveChdScanCache(string chdPath, DatRule datRule, string chdmanExe, ScannedFile archive, bool isDvd, string descriptor, string descriptorSha1)
        {
            try
            {
                FileInfo fi = new FileInfo(chdPath);
                string workingDir = "";
                try { workingDir = System.IO.Path.GetDirectoryName(chdPath) ?? Environment.CurrentDirectory; } catch { workingDir = Environment.CurrentDirectory; }
                ChdCacheFile cache = new ChdCacheFile
                {
                    CacheVersion = ChdScanCacheVersion,
                    SourcePath = chdPath,
                    SourceTimestamp = fi.LastWriteTime,
                    SourceSize = fi.Length,
                    ChdVersion = archive.CHDVersion,
                    ContainerCRC = archive.CRC?.ToHexString(),
                    ContainerSHA1 = archive.SHA1?.ToHexString(),
                    ContainerMD5 = archive.MD5?.ToHexString(),
                    IsDvd = isDvd,
                    Descriptor = descriptor,
                    DescriptorSha1 = descriptorSha1,
                    MappingFingerprint = ChdScanMappingFp,
                    SettingsFingerprint = ComputeChdSettingsFingerprint(datRule),
                    ToolFingerprint = ComputeChdToolFingerprint(chdmanExe, workingDir),
                    ChdStatus = archive.ChdStatus,
                    ChdScanMethod = archive.ChdScanMethod,
                    ChdHashMatchMode = archive.ChdHashMatchMode,
                    ChdDescriptorMatch = archive.ChdDescriptorMatch
                };

                for (int i = 0; i < archive.Count; i++)
                {
                    ScannedFile c = archive[i];
                    if (c == null || c.FileType != FileType.FileCHD)
                        continue;
                    cache.Entries.Add(new ChdCacheEntry
                    {
                        Name = c.Name,
                        Size = c.Size ?? 0,
                        CRC = c.CRC?.ToHexString(),
                        SHA1 = c.SHA1?.ToHexString(),
                        MD5 = c.MD5?.ToHexString(),
                        ChdStatus = c.ChdStatus,
                        ChdScanMethod = c.ChdScanMethod,
                        ChdHashMatchMode = c.ChdHashMatchMode,
                        ChdDescriptorMatch = c.ChdDescriptorMatch
                    });
                }

                string cacheKey = System.IO.Path.GetFullPath(chdPath);
                lock (ChdScanCacheLock)
                {
                    if (!ChdScanMemoryCache.ContainsKey(cacheKey) && ChdScanMemoryCache.Count >= ChdScanCacheLimit)
                    {
                        string oldestKey = null;
                        foreach (string key in ChdScanMemoryCache.Keys)
                        {
                            oldestKey = key;
                            break;
                        }
                        if (oldestKey != null)
                            ChdScanMemoryCache.Remove(oldestKey);
                    }
                    ChdScanMemoryCache[cacheKey] = cache;
                }
            }
            catch
            {
            }
        }

        public static void PurgeChdScanCache()
        {
            lock (ChdScanCacheLock)
                ChdScanMemoryCache.Clear();
        }

        private static string ComputeChdSettingsFingerprint(DatRule datRule)
        {
            try
            {
                var s = Settings.rvSettings;
                string text =
                    $"stream={s?.ChdStreaming};strict={s?.ChdStrictCueGdi};keep={s?.ChdKeepCueGdi};" +
                    $"synthetic={s?.ChdPreferSynthetic};verchk={s?.CheckCHDVersion};multi={s?.ChdMultiView};native={s?.ChdNativeVerification};health={s?.ChdHealthDatabase};parityDays={s?.ChdExternalParityDays};" +
                    $"ruleStrict={datRule?.ChdStrictCueGdi};ruleKeep={datRule?.ChdKeepCueGdi};" +
                    $"ruleMedia={datRule?.ChdCompressionType};ruleStorage={datRule?.ChdStorageProfile};ruleGeometry={datRule?.ChdHddGeometry};ruleMulti={datRule?.ChdMultiView};graph=4";
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                    return sha1.ComputeHash(bytes).ToHexString();
                }
            }
            catch
            {
                return "";
            }
        }

        private static string ComputeChdToolFingerprint(string chdmanExe, string workingDir)
        {
            string key = (chdmanExe ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                return "";

            lock (ChdToolFingerprintLock)
            {
                if (ChdToolFingerprintCache.TryGetValue(key, out string cached))
                    return cached;
            }

            if (!ChdmanService.TryGetIdentity(chdmanExe, ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out _))
            {
                lock (ChdToolFingerprintLock)
                    ChdToolFingerprintCache[key] = "";
                return "";
            }

            string libVer = "";
            try { libVer = typeof(CHD).Assembly.GetName().Version?.ToString() ?? ""; } catch { }
            string fp = $"chdman={identity.VersionText};sha256={identity.BinarySha256};chdlib={libVer}";
            lock (ChdToolFingerprintLock)
                ChdToolFingerprintCache[key] = fp;
            return fp;
        }

        private static string GetChdmanSha256(string executable)
        {
            return ChdmanService.TryGetIdentity(executable, ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out _)
                ? identity.BinarySha256
                : "";
        }

        private static string ComputeMd5Hex(string text)
        {
            try
            {
                using (MD5 md5 = MD5.Create())
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text ?? "");
                    byte[] hash = md5.ComputeHash(bytes);
                    return hash.ToHexString();
                }
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        private static string ComputeFileSha1Hex(string path)
        {
            try
            {
                using (var sha1 = SHA1.Create())
                using (var fs = System.IO.File.OpenRead(path))
                {
                    var hash = sha1.ComputeHash(fs);
                    return hash.ToHexString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeTrackLayoutSha1Hex(List<CHDSharpLib.ChdCdTrackInfo> tracks)
        {
            try
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    for (int i = 0; i < tracks.Count; i++)
                    {
                        string line = $"{tracks[i].TrackNo:D2}|{tracks[i].TrackType}|{tracks[i].StartFrame}|{tracks[i].Frames}|{tracks[i].PreGapFrames}|{tracks[i].PostGapFrames}|{tracks[i].SectorSize}\n";
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line);
                        sha1.TransformBlock(bytes, 0, bytes.Length, null, 0);
                    }
                    sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return sha1.Hash.ToHexString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static void SkipBytes(Stream s, ulong bytes)
        {
            if (bytes == 0)
                return;
            byte[] buffer = new byte[64 * 1024];
            while (bytes > 0)
            {
                int toRead = (int)Math.Min((ulong)buffer.Length, bytes);
                int read = s.Read(buffer, 0, toRead);
                if (read <= 0)
                    break;
                bytes -= (ulong)read;
            }
        }

        private static byte[] ParseHexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return null;
            hex = hex.Trim();
            if (hex.Length % 2 != 0)
                return null;
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out byte b))
                    return null;
                bytes[i] = b;
            }
            return bytes;
        }

        private static Dictionary<int, RvFile> BuildExpectedTrackMap(List<RvFile> expectedChildren)
        {
            Dictionary<int, RvFile> map = new Dictionary<int, RvFile>();
            Regex[] patterns = new[]
            {
                new Regex(@"\(Track\s*(\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"track[\s_]*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"track(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"\b(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };
            for (int i = 0; i < expectedChildren.Count; i++)
            {
                RvFile f = expectedChildren[i];
                if (f?.Name == null)
                    continue;
                int tno = ExtractTrackNumber(f.Name, patterns);
                if (tno > 0)
                {
                    if (!map.ContainsKey(tno))
                        map.Add(tno, f);
                }
            }
            return map;
        }

        private static int ExtractTrackNumber(string name, Regex[] patterns)
        {
            if (string.IsNullOrWhiteSpace(name))
                return -1;
            foreach (var r in patterns)
            {
                Match m = r.Match(name);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int tno))
                    return tno;
            }
            return -1;
        }

        private static bool IsTrackDataFile(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            return ext == ".bin" || ext == ".raw";
        }

        private static string NormalizeChdMemberName(string name)
        {
            return (name ?? "").Replace('\\', '/').TrimStart('.', '/');
        }

        private static bool IsSingleImageMember(string name, string family)
        {
            string extension = System.IO.Path.GetExtension(name ?? "").ToLowerInvariant();
            switch (family)
            {
                case "raw": return extension == ".raw";
                case "hdd": return extension == ".img" || extension == ".hdd" || extension == ".hd" || extension == ".raw";
                case "laserdisc": return extension == ".avi";
                default: return false;
            }
        }

        private static bool RunProcess(string exe, string args, string workingDir, out string output)
        {
            ChdmanRunResult result = ChdmanService.Run(exe, args, workingDir);
            output = result.Output;
            return result.Success;
        }

        private static long? TryGetChdLogicalSizeBytes(string chdmanExe, string chdPath, string workingDir)
        {
            if (ChdMetadata.TryReadContainerInfo(chdPath, out ChdContainerInfo info, out _) && info.LogicalSize <= long.MaxValue)
                return (long)info.LogicalSize;
            return ChdmanService.TryGetLogicalSize(chdmanExe, chdPath, workingDir);
        }

        private static bool HasChdExtractionSpace(string chdmanExe, string chdPath, string workingDir, bool isIso, bool reserveParentedTocPeak, out string error)
        {
            error = "";
            long? logicalSize = TryGetChdLogicalSizeBytes(chdmanExe, chdPath, workingDir);
            if (!logicalSize.HasValue)
            {
                error = "Could not determine the CHD logical size for extraction-space preflight.";
                return false;
            }

            if (!ChdFreeSpace.TryGetAvailableBytes(workingDir, out long free, out error))
                return false;
            long overhead = isIso ? 512L * 1024 * 1024 : 256L * 1024 * 1024;
            long required;
            try
            {
                // Parented exact-TOC reconstruction temporarily retains the
                // extractcd payload, a standalone materialized CHD, and the
                // exact TOC payload. Three logical payloads is deliberately
                // conservative for an incompressible standalone stage.
                int logicalCopies = reserveParentedTocPeak ? 3 : 1;
                required = checked(logicalSize.Value * logicalCopies + overhead);
            }
            catch (OverflowException)
            {
                error = "CHD extraction size is too large to preflight safely.";
                return false;
            }

            if (free < required)
            {
                error = $"Insufficient free space on the selected CHD workspace volume for extraction. required={required} free={free}";
                return false;
            }
            return true;
        }

        private static bool TryPrepareChdScanWorkspace(
            string chdPath,
            string chdmanExe,
            bool isIso,
            string parentPath,
            bool reserveParentedTocPeak,
            out string tempDir,
            out IChdExtractor extractor,
            out string error)
        {
            tempDir = "";
            extractor = null;
            error = "";
            if (!ChdTemporaryWorkspace.TryCreateForSource(chdPath, ChdWorkspacePurpose.Scan, out tempDir, out string workspaceError))
            {
                error = "Could not create a writable CHD scan workspace: " + workspaceError;
                return false;
            }
            if (!HasChdExtractionSpace(chdmanExe, chdPath, tempDir, isIso, reserveParentedTocPeak, out error))
                return false;
            extractor = new ChdmanChdExtractor(chdmanExe, tempDir, parentPath);
            return true;
        }

        private static bool TryMaterializeStandaloneForScan(
            string chdmanExe,
            string childPath,
            string parentPath,
            string workingDirectory,
            out string standalonePath,
            out string error)
        {
            standalonePath = System.IO.Path.Combine(workingDirectory, "rv-parent-standalone.chd");
            error = "";
            if (!ChdmanService.TryValidateExternalPaths(out error, childPath, parentPath, standalonePath, workingDirectory))
                return false;
            ChdmanRunResult result = ChdmanService.Run(chdmanExe,
                $"copy -i {ChdmanService.Quote(childPath)} -ip {ChdmanService.Quote(parentPath)} -o {ChdmanService.Quote(standalonePath)} -f",
                workingDirectory,
                600000);
            if (result.Success)
                return true;
            error = result.Output;
            return false;
        }


        public static ScannedFile FromADir(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker thWrk, int? fileIndex, ref bool fileErrorAbort)
        {
            _thWrk = thWrk;
            string fullName = dbDir.FullName;
            string fullNameCase = dbDir.FullNameCase;
            // DatStatus datStatus = dbDir.IsInToSort ? DatStatus.InToSort : DatStatus.NotInDat;

            ScannedFile fileDir = new ScannedFile(FileType.Dir);

            thWrk.Report(new bgwText("Scanning Dir : " + fullName));

            DirectoryInfo oDir = new DirectoryInfo(fullNameCase);
            DirectoryInfo[] oDirs = oDir.GetDirectories();
            FileInfo[] oFiles = oDir.GetFiles();

            // add all the subdirectories into scanDir 
            foreach (DirectoryInfo dir in oDirs)
            {
                if (ChdTemporaryWorkspace.IsOwnedWorkspaceName(dir.Name))
                    continue;
                ScannedFile tDir = new ScannedFile(FileType.Dir)
                {
                    Name = dir.Name,
                    FileModTimeStamp = dir.LastWriteTime,
                    GotStatus = GotStatus.Got
                };
                fileDir.Add(tDir);
            }


            DatRule datRule = ReadDat.DatReader.FindDatRule(dbDir.DatTreeFullName + "\\");
            List<Regex> regexList = (datRule != null && datRule.IgnoreFilesRegex != null && datRule.IgnoreFilesScanRegex.Count > 0)
                ? datRule.IgnoreFilesScanRegex
                : Settings.rvSettings.IgnoreFilesScanRegex;

            bool isFileOnly = IsFileOnly.isFileOnly(dbDir);

            // add all the files into scanDir
            foreach (FileInfo oFile in oFiles)
            {
                string fName = oFile.Name;
                if (ChdArtifactPaths.IsOwnedArtifactName(fName))
                    continue;
                if (fName.StartsWith("__RomVault.") && fName.EndsWith(".tmp"))
                {
                    try
                    {
                        File.Delete(oFile.FullName);
                    }
                    catch
                    {
                        thWrk.Report(new bgwShowError(oFile.FullName, "Could not delete, un-needed tmp file."));
                    }
                    continue;
                }

                bool found = false;
                foreach (Regex file in regexList)
                {
                    if (file.IsMatch(fName))
                    {
                        found = true;
                        continue;
                    }
                }
                if (found)
                    continue;

                string fExt = Path.GetExtension(oFile.Name);

                FileType ft = DBTypeGet.fromExtention(fExt);

                if (Settings.rvSettings.FilesOnly || dbDir.ToSortStatusIs(RvFile.ToSortDirType.ToSortFileOnly) || isFileOnly)
                    ft = FileType.File;

                ScannedFile tFile = new ScannedFile(ft)
                {
                    Name = oFile.Name,
                    Size = (ulong)oFile.Length,
                    FileModTimeStamp = oFile.LastWriteTime,
                    GotStatus = GotStatus.Got
                };
                tFile.FileStatusSet(FileStatus.SizeVerified);

                if (eScanLevel == EScanLevel.Level3 && tFile.FileType == FileType.File)
                {
                    if (fileIndex != null)
                        thWrk.Report(new bgwValue2((int)fileIndex));
                    thWrk.Report(new bgwText2(tFile.Name));
                    FromAFile(tFile, fullNameCase, eScanLevel, thWrk, ref fileErrorAbort);
                }

                fileDir.Add(tFile);
            }
            return fileDir;
        }
        public static void FromAFile(ScannedFile file, string directory, EScanLevel eScanLevel, ThreadWorker thWrk, ref bool fileErrorAbort)
        {
            if (_fileScans == null) _fileScans = new FileScan();

            _thWrk = thWrk;
            string filename = Path.Combine(directory, file.Name);

            thWrk.Report(new bgwText2(file.Name));
            ZipReturn zr = _fileScans.ScanArchiveFile(FileType.Dir, filename, file.FileModTimeStamp, true, out ScannedFile scannedItem, progress: FileProgress);

            if (zr == ZipReturn.ZipFileLocked)
            {
                thWrk.Report(new bgwShowError(filename, "File Locked"));
                file.GotStatus = GotStatus.FileLocked;
                return;
            }
            if (zr == ZipReturn.ZipErrorOpeningFile)
            {
                thWrk.Report(new bgwShowError(filename, "Error Opening File"));
                file.GotStatus = GotStatus.FileLocked;
                return;
            }

            if (zr != ZipReturn.ZipGood)
            {
                string error = zr.ToString();
                if (error.ToLower().StartsWith("zip"))
                    error = error.Substring(3);

                ReportError.Show($"File: {filename} Error: {error}. Scan Aborted.");
                file.GotStatus = GotStatus.FileLocked;
                fileErrorAbort = true;
                return;
            }

            if (_fileScans == null)
                _fileScans = new FileScan();

            //report

            ScannedFile fr = scannedItem[0];
            if (fr.GotStatus != GotStatus.Got)
            {
                thWrk.Report(new bgwShowError(filename, "Error Scanning File"));
                file.GotStatus = fr.GotStatus;
                return;
            }

            file.HeaderFileType = fr.HeaderFileType;
            file.Size = fr.Size;
            file.CRC = fr.CRC;
            file.SHA1 = fr.SHA1;
            file.MD5 = fr.MD5;
            file.AltSize = fr.AltSize;
            file.AltCRC = fr.AltCRC;
            file.AltSHA1 = fr.AltSHA1;
            file.AltMD5 = fr.AltMD5;
            file.GotStatus = fr.GotStatus;


            file.FileStatusSet(
                FileStatus.SizeVerified |
                (file.HeaderFileType != HeaderFileType.Nothing ? FileStatus.HeaderFileTypeFromHeader : 0) |
                (file.CRC != null ? FileStatus.CRCVerified : 0) |
                (file.SHA1 != null ? FileStatus.SHA1Verified : 0) |
                (file.MD5 != null ? FileStatus.MD5Verified : 0) |
                (file.AltSize != null ? FileStatus.AltSizeVerified : 0) |
                (file.AltCRC != null ? FileStatus.AltCRCVerified : 0) |
                (file.AltSHA1 != null ? FileStatus.AltSHA1Verified : 0) |
                (file.AltMD5 != null ? FileStatus.AltMD5Verified : 0)
            );

            if (fr.HeaderFileType == HeaderFileType.CHD)
            {
                bool deepCheck = (eScanLevel == EScanLevel.Level2 || eScanLevel == EScanLevel.Level3);
                uint? chdVersion = null;
                byte[] chdSHA1 = null;
                byte[] chdMD5 = null;

                CHD.fileProcessInfo = FileProcess;
                CHD.progress = FileProgress;

                int taskCount = Environment.ProcessorCount - 1;
                if (taskCount < 2) taskCount = 2;
                if (taskCount > 8) taskCount = 8;
                CHD.taskCount = taskCount;


                chd_error result = chd_error.CHDERR_NONE;
                if (!File.Exists(filename))
                {
                    FileSystemError("File: " + filename + " Error: File Could not be opened.");
                    fileErrorAbort = true;
                    result = chd_error.CHDERR_CANNOT_OPEN_FILE;
                }

                Stream s = null;
                int retval = RVIO.FileStream.OpenFileRead(filename, RVIO.FileStream.BufSizeMax, out s);
                if (retval != 0)
                {
                    FileSystemError("File: " + filename + " Error: File Could not be opened.");
                    fileErrorAbort = true;
                    result = chd_error.CHDERR_CANNOT_OPEN_FILE;
                }
                if (s == null)
                {
                    FileSystemError("File: " + filename + " Error: File Could not be opened.");
                    fileErrorAbort = true;
                    result = chd_error.CHDERR_CANNOT_OPEN_FILE;
                }

                if (result == chd_error.CHDERR_NONE)
                {
                    result = CHD.CheckFile(s, filename, deepCheck, out chdVersion, out chdSHA1, out chdMD5);
                }
                try
                {
                    s?.Close();
                    s?.Dispose();
                }
                catch
                { }

                if (result == chd_error.CHDERR_REQUIRES_PARENT)
                {
                    deepCheck = false;
                    result = chd_error.CHDERR_NONE;
                }
                switch (result)
                {
                    case chd_error.CHDERR_NONE:
                        break;

                    case chd_error.CHDERR_INVALID_FILE:
                    case chd_error.CHDERR_INVALID_DATA:
                    case chd_error.CHDERR_READ_ERROR:
                    case chd_error.CHDERR_DECOMPRESSION_ERROR:
                    case chd_error.CHDERR_CANT_VERIFY:
                        thWrk.Report(new bgwShowError(filename, $"CHD ERROR : {result}"));
                        file.GotStatus = GotStatus.Corrupt;
                        break;
                    default:
                        ReportError.UnhandledExceptionHandler(result.ToString());
                        break;
                }
                file.CHDVersion = chdVersion;
                if (chdSHA1 != null)
                {
                    file.AltSHA1 = chdSHA1;
                    file.FileStatusSet(FileStatus.AltSHA1FromHeader);
                    if (deepCheck && result == chd_error.CHDERR_NONE)
                        file.FileStatusSet(FileStatus.AltSHA1Verified);
                }

                if (chdMD5 != null)
                {
                    file.AltMD5 = chdMD5;
                    file.FileStatusSet(FileStatus.AltMD5FromHeader);
                    if (deepCheck && result == chd_error.CHDERR_NONE)
                        file.FileStatusSet(FileStatus.AltMD5Verified);
                }

                thWrk.Report(new bgwText3(""));
            }
        }

        private static void FileProcess(string filename)
        {
            _thWrk?.Report(new bgwText2(filename));
        }
        private static void FileProgress(string status)
        {
            _thWrk?.Report(new bgwText3(status));
        }
        private static void FileSystemError(string status)
        {
            ReportError.Show(status);
        }
    }
}
