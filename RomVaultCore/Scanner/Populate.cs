/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CHDSharpLib;
using Compress;
using FileScanner;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
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
        public static ScannedFile FromAZipFileArchive(RvFile dbDir, EScanLevel eScanLevel, ThreadWorker thWrk)
        {
            if (_fileScans == null) _fileScans = new FileScan();
            _thWrk = thWrk;

            string filename = ResolveExistingFilePath(dbDir.FullNameCase);
            FileType sType = dbDir.FileType;
            if (sType == FileType.CHD)
            {
                ScannedFile chdScan = ScanChdContainer(dbDir, filename, eScanLevel);
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
        /// Normalized track layout used when mapping CHD CD metadata to expected DAT members.
        /// </summary>
        private sealed class ChdTrack
        {
            public int TrackNo;
            public string TrackType;
            public long StartFrames;
            public long? Index00Frames;
            public long PreGapFrames;
            public long PostGapFrames;
            public bool IsSynthesizedPregap;
            public long SynthesizedPregapFrames;
            public bool IsSynthesizedPostgap;
            public long SynthesizedPostgapFrames;
        }

        /// <summary>
        /// Stream wrapper that appends a suffix of zero bytes after the base stream ends.
        /// </summary>
        private sealed class ZeroPadStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _padLength;
            private long _position;

            public ZeroPadStream(Stream baseStream, long padLength)
            {
                _baseStream = baseStream;
                _padLength = padLength;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _baseStream.Length + _padLength;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;
                if (_position < _baseStream.Length)
                {
                    int toRead = (int)Math.Min(count, _baseStream.Length - _position);
                    int r = _baseStream.Read(buffer, offset, toRead);
                    if (r > 0)
                    {
                        totalRead += r;
                        _position += r;
                        offset += r;
                        count -= r;
                    }
                }
                if (count > 0 && _position >= _baseStream.Length)
                {
                    long remainingPad = Length - _position;
                    if (remainingPad > 0)
                    {
                        int toPad = (int)Math.Min(count, remainingPad);
                        Array.Clear(buffer, offset, toPad);
                        totalRead += toPad;
                        _position += toPad;
                    }
                }
                return totalRead;
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) _baseStream.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Stream wrapper that prefixes a stream with a fixed number of zero bytes.
        /// </summary>
        private sealed class PrefixZeroStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _prefixLength;
            private long _position;

            public PrefixZeroStream(Stream baseStream, long prefixLength)
            {
                _baseStream = baseStream;
                _prefixLength = prefixLength;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _baseStream.Length + _prefixLength;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;
                if (_position < _prefixLength)
                {
                    long remainingPrefix = _prefixLength - _position;
                    int toPad = (int)Math.Min(count, remainingPrefix);
                    Array.Clear(buffer, offset, toPad);
                    totalRead += toPad;
                    _position += toPad;
                    offset += toPad;
                    count -= toPad;
                }
                if (count > 0)
                {
                    int r = _baseStream.Read(buffer, offset, count);
                    if (r > 0)
                    {
                        totalRead += r;
                        _position += r;
                    }
                }
                return totalRead;
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) _baseStream.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// A single cached member entry produced by CHD scanning.
        /// </summary>
        /// <remarks>
        /// The cache stores member hashes as hex strings so the scan can be reused without re-extracting.
        /// </remarks>
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
        /// Cached scan results for a single CHD file.
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
        private const int ChdScanCacheVersion = 5;

        /// <summary>
        /// Fingerprint describing the mapping and hashing behavior used when scanning CHDs.
        /// </summary>
        /// <remarks>
        /// This is used to invalidate old cache entries when mapping logic changes (pregap/index handling, ToSort naming, etc.).
        /// </remarks>
        private const string ChdScanMappingFp = "mapfp5:gaps;idx00;startframe;postgap;health;tosortnaming";
        private static readonly object ChdToolFingerprintLock = new object();
        private static readonly Dictionary<string, string> ChdToolFingerprintCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Scans a CHD file as a container and returns a directory-like <see cref="ScannedFile"/> containing member entries.
        /// </summary>
        /// <remarks>
        /// Members are emitted as <see cref="FileType.FileCHD"/> so the normal archive merge pipeline can match them against DAT expectations.
        /// The scan may run in streaming mode (no extraction) when CHD metadata is available; otherwise it falls back to chdman extraction.
        /// </remarks>
        private static ScannedFile ScanChdContainer(RvFile dbDir, string filename, EScanLevel eScanLevel)
        {
            if (!File.Exists(filename))
                return null;

            if (Settings.rvSettings.CheckCHDVersion)
            {
                try
                {
                    using (FileStream fs = System.IO.File.OpenRead(filename))
                    {
                        if (CHD.CheckFile(fs, filename, false, out uint? ver, out _, out _) == chd_error.CHDERR_NONE)
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

            bool expectedIsDvd = false;
            bool expectedIsGdi = false;
            try
            {
                for (int i = 0; i < dbDir.ChildCount; i++)
                {
                    RvFile c = dbDir.Child(i);
                    if (c?.Name == null)
                        continue;
                    if (c.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                        expectedIsDvd = true;
                    if (c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))
                        expectedIsGdi = true;
                }
            }
            catch
            {
            }
            string expectedDescriptor = expectedIsDvd ? "dvd" : expectedIsGdi ? "gdi" : "cue";

            if (dbDir.ChildCount == 0)
            {
                try
                {
                    if (CHDSharpLib.ChdMetadata.TryReadCdTrackLayout(filename, out List<CHDSharpLib.ChdCdTrackInfo> cdTracks, out _) &&
                        cdTracks != null && cdTracks.Count > 0)
                    {
                        expectedIsDvd = false;
                        expectedIsGdi = false;
                        expectedDescriptor = "cue";
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

            DatRule datRule = null;
            try
            {
                string ruleKey = (dbDir.Parent?.DatTreeFullName ?? "") + "\\";
                datRule = ReadDat.DatReader.FindDatRule(ruleKey);
            }
            catch
            {
            }

            bool forceScan = (eScanLevel == EScanLevel.Level3);
            string chdmanExe = FindChdmanExePath();

            if (!forceScan &&
                Settings.rvSettings.ChdScanCacheEnabled &&
                TryLoadChdScanCache(filename, datRule, chdmanExe, out ChdCacheFile cache) &&
                cache.CacheVersion == ChdScanCacheVersion &&
                cache.IsDvd == expectedIsDvd &&
                string.Equals(cache.Descriptor ?? "", expectedDescriptor, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(cache.MappingFingerprint ?? "", ChdScanMappingFp, StringComparison.OrdinalIgnoreCase) &&
                (expectedDescriptor == "dvd" || !string.IsNullOrWhiteSpace(cache.DescriptorSha1)))
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
                            string.Equals(ext, ".gdi", StringComparison.OrdinalIgnoreCase))
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
                    ZipStruct = ZipStructure.None,
                    Comment = "",
                    ChdStatus = cache.ChdStatus,
                    ChdScanMethod = cache.ChdScanMethod ?? "Cache",
                    ChdHashMatchMode = cache.ChdHashMatchMode,
                    ChdDescriptorMatch = cache.ChdDescriptorMatch
                };
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
            string baseTempDir = ResolveExistingDirectoryPath(DB.GetToSortCache()?.FullName);
            if (string.IsNullOrWhiteSpace(baseTempDir))
                baseTempDir = Environment.CurrentDirectory;
            string tempDir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdscan." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                IChdExtractor extractor = new ChdmanChdExtractor(chdmanExe, tempDir);
                List<RvFile> expectedChildren = new List<RvFile>();
                for (int i = 0; i < dbDir.ChildCount; i++)
                {
                    RvFile c = dbDir.Child(i);
                    if (c != null && c.IsFile)
                        expectedChildren.Add(c);
                }

                bool expectsIso = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));
                bool expectsGdi = expectedChildren.Exists(c => c.Name != null && c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase));
                string expectedIsoName = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))?.Name;
                if (!expectsIso && expectedChildren.Count == 0 && string.Equals(expectedDescriptor, "dvd", StringComparison.OrdinalIgnoreCase))
                    expectsIso = true;

                if (Settings.rvSettings.CheckCHDVersion && extractor.Info(filename, out string infoText))
                {
                    if (!infoText.Contains("zstd", StringComparison.OrdinalIgnoreCase))
                        _thWrk?.Report(new bgwShowError(filename, "CHD compression does not include zstd; consider rebuilding with zstd profiles"));
                }

                if (!Settings.rvSettings.ChdStreaming || !expectsIso)
                {
                    long? logicalSize = TryGetChdLogicalSizeBytes(chdmanExe, filename, tempDir);
                    if (logicalSize.HasValue)
                    {
                        long free = GetFreeSpaceBytes(tempDir);
                        long overhead = expectsIso ? 512L * 1024 * 1024 : 256L * 1024 * 1024;
                        long required = logicalSize.Value + overhead;
                        if (free > 0 && free < required)
                        {
                            _thWrk?.Report(new bgwShowError(filename, $"Insufficient free space for CHD extraction. required={required} free={free}"));
                            return null;
                        }
                    }
                }

                ScannedFile ar = new ScannedFile(FileType.CHD)
                {
                    Name = filename,
                    ZipStruct = ZipStructure.None,
                    Comment = ""
                };

                // Hash the CHD container itself
                using (FileStream fs = System.IO.File.OpenRead(filename))
                {
                    ar.Size = (ulong)fs.Length;
                    _fileScans.CheckSumRead(fs, ar, ar.Size.Value, true, false, null, 0, 0);
                }

                if (expectsIso)
                {
                    bool useStreaming = Settings.rvSettings.ChdStreaming;
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
                        }
                        else
                        {
                            _thWrk?.Report(new bgwShowError(filename, "CHD extractdvd failed: " + err));
                        }
                    }

                    if (Settings.rvSettings.ChdScanCacheEnabled)
                        SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: true, descriptor: "dvd", descriptorSha1: null);
                    return ar;
                }

                bool tryStream = Settings.rvSettings.ChdStreaming;
                if (tryStream)
                {
                    try
                    {
                        if (CHDSharpLib.ChdMetadata.TryReadCdTrackLayout(filename, out List<CHDSharpLib.ChdCdTrackInfo> cdTracks, out string metaErr) && cdTracks.Count > 0)
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
                                debug.AppendLine(filename);
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

                            Dictionary<string, (string extractedName, bool swap16)> mapping = BuildDeterministicMapping(trackFiles, fileHashCache, null, expectedByTrack, expectedDataFiles, debug, 
                                datRule?.ChdAudioTransform ?? ChdAudioTransform.None, 
                                datRule?.ChdLayoutStrictness ?? ChdLayoutStrictness.Normal);

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

                            foreach (KeyValuePair<string, (string extractedName, bool swap16)> kvp in mapping)
                            {
                                if (!fileHashCache.TryGetValue(kvp.Value.extractedName, out var h))
                                    continue;
                                if (h.size == 0 || h.crc == null || h.sha1 == null || h.md5 == null)
                                {
                                    if (debug != null)
                                        debug.AppendLine($"streaming hash missing for {kvp.Value.extractedName} size={h.size} crc={(h.crc == null ? "null" : "ok")} sha1={(h.sha1 == null ? "null" : "ok")} md5={(h.md5 == null ? "null" : "ok")}; will fall back to extractcd");
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
                                        : ChdDescriptorGenerator.BuildCue(cdTracks, expectedByTrack);
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
                                        dsf.ChdDescriptorMatch = ok ? "External" : "External";
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
                            if (Settings.rvSettings.ChdScanCacheEnabled)
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

                string descriptorSha1 = ComputeFileSha1Hex(outMain);
                List<(int trackNo, string fileName, string trackType)> trackFiles = expectsGdi ? ParseGdiTrackFiles(outMain) : ParseCueTrackFiles(outMain);
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
                    debug.AppendLine(filename);
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
                ConcurrentDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashSwap16Cache = new ConcurrentDictionary<string, (ulong, byte[], byte[], byte[])>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> uniqueExtractedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < trackFiles.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(trackFiles[i].fileName))
                        uniqueExtractedSet.Add(trackFiles[i].fileName);
                }
                Dictionary<string, bool> audioFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < trackFiles.Count; i++)
                {
                    string n = trackFiles[i].fileName;
                    if (string.IsNullOrWhiteSpace(n))
                        continue;
                    string tt = (trackFiles[i].trackType ?? "").Trim().ToUpperInvariant();
                    if (tt.Contains("AUDIO"))
                        audioFiles[n] = true;
                    else if (!audioFiles.ContainsKey(n))
                        audioFiles[n] = false;
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

                    if (audioFiles.TryGetValue(extractedName, out bool isAudio) && isAudio)
                    {
                        ScannedFile tmpSwap = new ScannedFile(FileType.FileCHD)
                        {
                            Name = extractedName,
                            FileModTimeStamp = fi.LastWriteTime,
                            GotStatus = GotStatus.Got,
                            DeepScanned = true,
                            Size = (ulong)fi.Length,
                            ChdScanMethod = "Extraction (CD)"
                        };
                        using (Stream s = System.IO.File.OpenRead(full))
                        using (Stream swap = new Swap16Stream(s))
                        {
                            _fileScans.CheckSumRead(swap, tmpSwap, (ulong)fi.Length, true, false, null, 0, 0);
                        }
                        fileHashSwap16Cache.TryAdd(extractedName, ((ulong)fi.Length, tmpSwap.CRC, tmpSwap.SHA1, tmpSwap.MD5));
                    }
                });

                HashSet<string> uniqueExtracted = new HashSet<string>(uniqueExtractedSet, StringComparer.OrdinalIgnoreCase);

                if (debug != null)
                {
                    debug.AppendLine("hashes:");
                    for (int i = 0; i < uniqueExtractedArr.Length; i++)
                    {
                        string n = uniqueExtractedArr[i];
                        if (!fileHashCache.TryGetValue(n, out var h))
                            continue;
                        string swap = "";
                        if (fileHashSwap16Cache.TryGetValue(n, out var hs))
                            swap = $" swap16_sha1={hs.sha1.ToHexString()} swap16_md5={hs.md5.ToHexString()}";
                        debug.AppendLine($"  {n} size={h.size} crc={h.crc.ToHexString()} sha1={h.sha1.ToHexString()} md5={h.md5.ToHexString()}{swap}");
                    }
                }

                if (!expectsGdi && uniqueExtracted.Count == 1 && expectedDataFiles.Count > 1)
                {
                    if (debug != null)
                        debug.AppendLine("single-bin cue detected; attempting fallback slicing");
                    if (TryScanSingleBinCueAsTracks(filename, chdmanExe, tempDir, outMain, expectedByTrack, expectedDataFiles, ar, debug, datRule))
                    {
                        ar.ChdScanMethod = "Extraction (CD Sliced)";
                        ar.Sort();
                        if (Settings.rvSettings.ChdScanCacheEnabled)
                        {
                            string descHash = ComputeFileSha1Hex(outMain);
                            SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: false, descriptor: expectsGdi ? "gdi" : "cue", descriptorSha1: descHash);
                        }
                        WriteChdDebugLog(filename, debug);
                        return ar;
                    }
                }

                Dictionary<string, (string extractedName, bool swap16)> mapping = BuildDeterministicMapping(trackFiles, fileHashCache, fileHashSwap16Cache, expectedByTrack, expectedDataFiles, debug,
                    datRule?.ChdAudioTransform ?? ChdAudioTransform.None,
                    datRule?.ChdLayoutStrictness ?? ChdLayoutStrictness.Normal);

                HashSet<string> missingHashedExpectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < expectedDataFiles.Count; i++)
                {
                    RvFile exp = expectedDataFiles[i];
                    if (exp?.Name == null)
                        continue;
                    if (!HasExpectedHash(exp))
                        continue;
                    if (mapping.ContainsKey(exp.Name))
                        continue;
                    missingHashedExpectedNames.Add(exp.Name);
                }

                bool pregapShiftApplied = false;
                int pregapShiftMatchedCount = 0;
                if (!expectsGdi && missingHashedExpectedNames.Count > 0)
                {
                    if (TryCuePregapShiftForExtractedBins(tempDir, outMain, trackFiles, expectedByTrack, missingHashedExpectedNames, _fileScans, debug,
                            out Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5, bool swap16)> fixedHashes))
                    {
                        pregapShiftApplied = true;
                        pregapShiftMatchedCount = fixedHashes.Count;
                        foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5, bool swap16)> kvp in fixedHashes)
                        {
                            ScannedFile sf = new ScannedFile(FileType.FileCHD)
                            {
                                Name = kvp.Key,
                                FileModTimeStamp = dbDir.FileModTimeStamp,
                                GotStatus = GotStatus.Got,
                                DeepScanned = true,
                                Size = kvp.Value.size,
                                CRC = kvp.Value.crc,
                                SHA1 = kvp.Value.sha1,
                                MD5 = kvp.Value.md5,
                                ChdScanMethod = "Extraction (CD)",
                                ChdHashMatchMode = kvp.Value.swap16 ? "PregapShiftSwap16" : "PregapShift"
                            };
                            sf.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                            ar.Add(sf);
                        }
                    }
                }

                HashSet<string> usedExtracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, (string extractedName, bool swap16)> kvp in mapping)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value.extractedName))
                        usedExtracted.Add(kvp.Value.extractedName);
                }
                if (pregapShiftApplied)
                {
                    for (int i = 0; i < trackFiles.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(trackFiles[i].fileName))
                            usedExtracted.Add(trackFiles[i].fileName);
                    }
                }
                foreach (KeyValuePair<string, (string extractedName, bool swap16)> kvp in mapping)
                {
                    string expectedName = kvp.Key;
                    string extractedName = kvp.Value.extractedName;
                    if (string.IsNullOrWhiteSpace(expectedName) || string.IsNullOrWhiteSpace(extractedName))
                        continue;
                    if (kvp.Value.swap16)
                    {
                        if (!fileHashSwap16Cache.TryGetValue(extractedName, out var hs))
                            continue;

                        ScannedFile sfSwap = new ScannedFile(FileType.FileCHD)
                        {
                            Name = expectedName,
                            FileModTimeStamp = dbDir.FileModTimeStamp,
                            GotStatus = GotStatus.Got,
                            DeepScanned = true,
                            Size = hs.size,
                            CRC = hs.crc,
                            SHA1 = hs.sha1,
                            MD5 = hs.md5,
                            ChdScanMethod = "Extraction (CD)",
                            ChdHashMatchMode = "Swap16"
                        };
                        sfSwap.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                        ar.Add(sfSwap);
                        continue;
                    }

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

                RvFile expectedCue = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase));
                RvFile expectedGdi = expectedChildren.Find(c => c.Name != null && c.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase));
                if (expectedCue != null || expectedGdi != null)
                {
                    string descName = expectedGdi?.Name ?? expectedCue?.Name;
                    RvFile expDesc = expectedGdi ?? expectedCue;
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
                        try
                        {
                            FileInfo fi = new FileInfo(outMain);
                            dsf.Size = (ulong)fi.Length;
                            using (Stream s = System.IO.File.OpenRead(outMain))
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

                        if (ok || (Settings.rvSettings.ChdPreferSynthetic && (expDesc == null || (!expDesc.Size.HasValue || expDesc.Size.Value == 0) && expDesc.CRC == null && expDesc.SHA1 == null && expDesc.MD5 == null)))
                        {
                            string descFileName = System.IO.Path.GetFileName((descName ?? "").Replace('\\', '/'));
                            if (keepDescriptor && !string.IsNullOrWhiteSpace(descFileName) && System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename) ?? "", descFileName)))
                                dsf.ChdDescriptorMatch = "External";
                            else
                                dsf.ChdDescriptorMatch = ok ? "True" : "Synthetic";
                            ar.Add(dsf);
                        }
                    }
                }

                bool hashMismatch = false;
                if (expectedDataFiles.Count > 0)
                {
                    Dictionary<string, ScannedFile> byName = new Dictionary<string, ScannedFile>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < ar.Count; i++)
                    {
                        if (ar[i]?.Name != null && !byName.ContainsKey(ar[i].Name))
                            byName.Add(ar[i].Name, ar[i]);
                    }
                    for (int i = 0; i < expectedDataFiles.Count; i++)
                    {
                        RvFile exp = expectedDataFiles[i];
                        if (exp?.Name == null)
                            continue;
                        if (!byName.TryGetValue(exp.Name, out ScannedFile got))
                        {
                            if (HasExpectedHash(exp))
                            {
                                hashMismatch = true;
                                break;
                            }
                            continue;
                        }
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
                }

                bool allowTrust = Settings.rvSettings.ChdTrustContainerForTracks;
                if (allowTrust)
                {
                    for (int i = 0; i < expectedDataFiles.Count; i++)
                    {
                        if (HasExpectedHash(expectedDataFiles[i]))
                        {
                            allowTrust = false;
                            break;
                        }
                    }
                }

                if (hashMismatch && allowTrust)
                {
                    ScannedFile trust = new ScannedFile(FileType.CHD)
                    {
                        Name = ar.Name,
                        ZipStruct = ar.ZipStruct,
                        Comment = ar.Comment,
                        ChdStatus = "Hash mismatch (Trust Container enabled)",
                        ChdScanMethod = ar.ChdScanMethod,
                        ChdHashMatchMode = "TrustContainer"
                    };
                    for (int i = 0; i < expectedChildren.Count; i++)
                    {
                        RvFile exp = expectedChildren[i];
                        if (exp == null || !exp.IsFile)
                            continue;
                        if (!IsTrackDataFile(exp.Name) && !(exp.Name?.EndsWith(".cue", StringComparison.OrdinalIgnoreCase) ?? false) && !(exp.Name?.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase) ?? false))
                            continue;
                        ScannedFile sf = new ScannedFile(FileType.FileCHD)
                        {
                            Name = exp.Name,
                            FileModTimeStamp = dbDir.FileModTimeStamp,
                            GotStatus = GotStatus.Got,
                            DeepScanned = true,
                            ChdScanMethod = ar.ChdScanMethod,
                            ChdHashMatchMode = "TrustContainer"
                        };
                        trust.Add(sf);
                    }
                    trust.Sort();
                    if (Settings.rvSettings.ChdScanCacheEnabled)
                        SaveChdScanCache(filename, datRule, chdmanExe, trust, isDvd: false, descriptor: "trust", descriptorSha1: descriptorSha1);
                    WriteChdDebugLog(filename, debug);
                    return trust;
                }

                if (pregapShiftApplied)
                    ar.ChdStatus = $"Pregap-shift applied ({pregapShiftMatchedCount} track(s))";

                ar.Sort();
                if (Settings.rvSettings.ChdScanCacheEnabled)
                    SaveChdScanCache(filename, datRule, chdmanExe, ar, isDvd: false, descriptor: expectsGdi ? "gdi" : "cue", descriptorSha1: descriptorSha1);
                WriteChdDebugLog(filename, debug);
                return ar;
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }
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

        /// <summary>
        /// Stream wrapper that swaps adjacent bytes (16-bit endianness) while reading.
        /// </summary>
        private sealed class Swap16Stream : Stream
        {
            private readonly Stream _source;
            private readonly byte[] _scratch;
            private bool _hasCarry;
            private byte _carry;

            public Swap16Stream(Stream source, int bufferSize = 64 * 1024)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                _scratch = new byte[Math.Max(1024, bufferSize)];
            }

            public override bool CanRead => _source.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _source.Length;
            public override long Position
            {
                get => _source.Position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count <= 0)
                    return 0;
                int want = Math.Min(count, _scratch.Length);
                int read = _source.Read(_scratch, 0, want);
                if (read <= 0)
                    return 0;

                int outPos = offset;
                int i = 0;
                if (_hasCarry)
                {
                    if (read >= 1)
                    {
                        buffer[outPos++] = _scratch[0];
                        buffer[outPos++] = _carry;
                        i = 1;
                        _hasCarry = false;
                    }
                    else
                    {
                        return 0;
                    }
                }

                for (; i + 1 < read; i += 2)
                {
                    buffer[outPos++] = _scratch[i + 1];
                    buffer[outPos++] = _scratch[i];
                }

                if (i < read)
                {
                    _carry = _scratch[i];
                    _hasCarry = true;
                }

                return outPos - offset;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _source.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class ConcatReadStream : Stream
        {
            private readonly Stream _a;
            private readonly Stream _b;
            private readonly long _length;
            private long _position;
            private bool _readingA;

            public ConcatReadStream(Stream a, Stream b)
            {
                _a = a ?? throw new ArgumentNullException(nameof(a));
                _b = b ?? throw new ArgumentNullException(nameof(b));
                _readingA = true;
                long la = 0;
                long lb = 0;
                try { la = a.CanSeek ? a.Length - a.Position : 0; } catch { la = 0; }
                try { lb = b.CanSeek ? b.Length - b.Position : 0; } catch { lb = 0; }
                _length = la + lb;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count <= 0)
                    return 0;
                int total = 0;
                while (total < count)
                {
                    Stream cur = _readingA ? _a : _b;
                    int read = cur.Read(buffer, offset + total, count - total);
                    if (read > 0)
                    {
                        total += read;
                        _position += read;
                        continue;
                    }
                    if (_readingA)
                    {
                        _readingA = false;
                        continue;
                    }
                    break;
                }
                return total;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _a.Dispose(); } catch { }
                    try { _b.Dispose(); } catch { }
                }
                base.Dispose(disposing);
            }
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

        private static bool TryCuePregapShiftForExtractedBins(
            string tempDir,
            string cuePath,
            List<(int trackNo, string fileName, string trackType)> trackFiles,
            Dictionary<int, RvFile> expectedByTrack,
            HashSet<string> missingHashedExpectedNames,
            FileScan scanner,
            System.Text.StringBuilder debug,
            out Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5, bool swap16)> fixedHashes)
        {
            fixedHashes = new Dictionary<string, (ulong, byte[], byte[], byte[], bool)>(StringComparer.OrdinalIgnoreCase);
            if (missingHashedExpectedNames == null || missingHashedExpectedNames.Count == 0)
                return false;
            if (string.IsNullOrWhiteSpace(tempDir) || string.IsNullOrWhiteSpace(cuePath) || trackFiles == null || trackFiles.Count < 2)
                return false;
            if (scanner == null)
                return false;

            List<ChdTrack> cueTracks = ParseCueTracksWithIndexes(cuePath);
            Dictionary<int, ChdTrack> cueByTrack = new Dictionary<int, ChdTrack>();
            for (int i = 0; i < cueTracks.Count; i++)
            {
                if (cueTracks[i] != null && cueTracks[i].TrackNo > 0 && !cueByTrack.ContainsKey(cueTracks[i].TrackNo))
                    cueByTrack.Add(cueTracks[i].TrackNo, cueTracks[i]);
            }

            List<(int trackNo, string fileName, string trackType)> ordered = new List<(int, string, string)>(trackFiles);
            ordered.Sort((a, b) => a.trackNo.CompareTo(b.trackNo));
            Dictionary<int, (string fileName, string trackType)> tfByNo = new Dictionary<int, (string, string)>();
            for (int i = 0; i < ordered.Count; i++)
                tfByNo[ordered[i].trackNo] = (ordered[i].fileName, ordered[i].trackType);

            List<int> missingTrackNos = new List<int>();
            foreach (KeyValuePair<int, RvFile> kvp in expectedByTrack)
            {
                if (kvp.Value?.Name == null)
                    continue;
                if (!missingHashedExpectedNames.Contains(kvp.Value.Name))
                    continue;
                missingTrackNos.Add(kvp.Key);
            }
            if (missingTrackNos.Count == 0)
                return false;

            missingTrackNos.Sort();

            Func<int, bool, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> compute = (trackNo, swap16) =>
            {
                if (!tfByNo.TryGetValue(trackNo, out var curTf))
                    return (0, null, null, null);

                long skipBytes = 0;
                if (cueByTrack.TryGetValue(trackNo, out ChdTrack curCue) && curCue != null)
                {
                    int sec = ResolveSectorSize(curTf.trackType, 2352);
                    skipBytes = (long)Math.Max(0, curCue.PreGapFrames) * (long)Math.Max(1, sec);
                }

                long appendBytes = 0;
                if (tfByNo.TryGetValue(trackNo + 1, out var nextTf) && cueByTrack.TryGetValue(trackNo + 1, out ChdTrack nextCue) && nextCue != null)
                {
                    int sec = ResolveSectorSize(nextTf.trackType, 2352);
                    appendBytes = (long)Math.Max(0, nextCue.PreGapFrames) * (long)Math.Max(1, sec);
                    if (appendBytes > 0 && string.Equals(curTf.fileName, nextTf.fileName, StringComparison.OrdinalIgnoreCase))
                        appendBytes = 0;
                }

                string curPath = System.IO.Path.Combine(tempDir, curTf.fileName ?? "");
                if (!System.IO.File.Exists(curPath))
                    return (0, null, null, null);
                FileInfo curFi = new FileInfo(curPath);
                long curLen = curFi.Length;
                if (skipBytes < 0) skipBytes = 0;
                if (skipBytes > curLen) skipBytes = curLen;
                long mainLen = curLen - skipBytes;

                Stream mainStream;
                FileStream curFs = System.IO.File.OpenRead(curPath);
                if (skipBytes > 0)
                    mainStream = new ReadOnlySliceStream(curFs, skipBytes, mainLen);
                else
                    mainStream = curFs;

                Stream combined = mainStream;
                if (appendBytes > 0)
                {
                    if (!tfByNo.TryGetValue(trackNo + 1, out var nextTf2))
                    {
                        combined.Dispose();
                        return (0, null, null, null);
                    }
                    string nextPath = System.IO.Path.Combine(tempDir, nextTf2.fileName ?? "");
                    if (!System.IO.File.Exists(nextPath))
                    {
                        combined.Dispose();
                        return (0, null, null, null);
                    }
                    FileInfo nextFi = new FileInfo(nextPath);
                    long nextLen = nextFi.Length;
                    if (appendBytes > nextLen) appendBytes = nextLen;
                    FileStream nextFs = System.IO.File.OpenRead(nextPath);
                    Stream suffix = new ReadOnlySliceStream(nextFs, 0, appendBytes);
                    combined = new ConcatReadStream(mainStream, suffix);
                }

                ulong totalLen = (ulong)(mainLen + appendBytes);
                ScannedFile tmp = new ScannedFile(FileType.FileCHD)
                {
                    Name = "pregapshift:" + trackNo.ToString("D2"),
                    FileModTimeStamp = curFi.LastWriteTime,
                    GotStatus = GotStatus.Got,
                    DeepScanned = true,
                    Size = totalLen
                };

                try
                {
                    Stream hashStream = combined;
                    if (swap16)
                        hashStream = new Swap16Stream(hashStream);
                    using (hashStream)
                    {
                        scanner.CheckSumRead(hashStream, tmp, totalLen, true, false, null, 0, 0);
                    }
                    return (totalLen, tmp.CRC, tmp.SHA1, tmp.MD5);
                }
                catch
                {
                    try { combined.Dispose(); } catch { }
                    return (0, null, null, null);
                }
            };

            for (int i = 0; i < missingTrackNos.Count; i++)
            {
                int trackNo = missingTrackNos[i];
                if (!expectedByTrack.TryGetValue(trackNo, out RvFile exp) || exp?.Name == null)
                    return false;

                bool isAudio = tfByNo.TryGetValue(trackNo, out var tt) && (tt.trackType ?? "").Trim().ToUpperInvariant().Contains("AUDIO");
                var h = compute(trackNo, false);
                bool match =
                    (exp.Size == null || exp.Size == 0 || (ulong)exp.Size == h.size) &&
                    (exp.CRC == null || (h.crc != null && exp.CRC.AsSpan().SequenceEqual(h.crc))) &&
                    (exp.SHA1 == null || (h.sha1 != null && exp.SHA1.AsSpan().SequenceEqual(h.sha1))) &&
                    (exp.MD5 == null || (h.md5 != null && exp.MD5.AsSpan().SequenceEqual(h.md5)));

                bool swap16Used = false;
                if (!match && isAudio)
                {
                    var hs = compute(trackNo, true);
                    bool matchSwap =
                        (exp.Size == null || exp.Size == 0 || (ulong)exp.Size == hs.size) &&
                        (exp.CRC == null || (hs.crc != null && exp.CRC.AsSpan().SequenceEqual(hs.crc))) &&
                        (exp.SHA1 == null || (hs.sha1 != null && exp.SHA1.AsSpan().SequenceEqual(hs.sha1))) &&
                        (exp.MD5 == null || (hs.md5 != null && exp.MD5.AsSpan().SequenceEqual(hs.md5)));
                    if (matchSwap)
                    {
                        h = hs;
                        swap16Used = true;
                        match = true;
                    }
                }

                if (!match)
                    return false;

                fixedHashes[exp.Name] = (h.size, h.crc, h.sha1, h.md5, swap16Used);
            }

            if (debug != null)
            {
                debug.AppendLine("pregap-shift:");
                foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5, bool swap16)> kvp in fixedHashes)
                    debug.AppendLine($"  {kvp.Key} size={kvp.Value.size} crc={kvp.Value.crc.ToHexString()} sha1={kvp.Value.sha1.ToHexString()} md5={kvp.Value.md5.ToHexString()}{(kvp.Value.swap16 ? " (swap16)" : "")}");
            }

            return fixedHashes.Count == missingHashedExpectedNames.Count;
        }

        /// <summary>
        /// Builds a deterministic mapping from expected DAT members to extracted/streamed track blobs.
        /// </summary>
        /// <remarks>
        /// Mapping proceeds in stages:
        /// - Hash match (SHA1/MD5/CRC+Size), optionally allowing swap16 for audio when configured.
        /// - Track-number match when a track number can be inferred from expected filenames.
        /// - Unique-size match, optionally constrained by inferred "audio/data" category or filename extension.
        /// - Optional order fallback (disabled when layout strictness is <c>Strict</c>).
        ///
        /// The returned map is used to emit <see cref="FileType.FileCHD"/> child entries named to match the DAT.
        /// </remarks>
        /// <returns>A map from expected member name to extracted name and whether swap16 was used.</returns>
        private static Dictionary<string, (string extractedName, bool swap16)> BuildDeterministicMapping(
            List<(int trackNo, string fileName, string trackType)> trackFiles,
            IDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache,
            IDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashSwap16Cache,
            Dictionary<int, RvFile> expectedByTrack,
            List<RvFile> expectedDataFiles,
            System.Text.StringBuilder debug,
            ChdAudioTransform audioTransform = ChdAudioTransform.None,
            ChdLayoutStrictness layoutStrictness = ChdLayoutStrictness.Normal)
        {
            Dictionary<string, (string extractedName, bool swap16)> mapping = new Dictionary<string, (string extractedName, bool swap16)>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedExtracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedExpected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Func<RvFile, bool> hasExpectedHash = f =>
                f != null &&
                ((f.SHA1 != null && f.SHA1.Length > 0) ||
                 (f.MD5 != null && f.MD5.Length > 0) ||
                 (f.CRC != null && f.CRC.Length > 0));
            Action<string, string, bool, string> reason = (exp, ext, swap16, why) =>
            {
                if (debug != null)
                    debug.AppendLine($"  reason: {exp} <= {ext}{(swap16 ? " (swap16)" : "")} :: {why}");
            };

            for (int i = 0; i < expectedDataFiles.Count; i++)
            {
                RvFile exp = expectedDataFiles[i];
                if (exp?.Name == null || usedExpected.Contains(exp.Name))
                    continue;

                var hit = FindExtractedByHash(fileHashCache,
                    fileHashSwap16Cache,
                    usedExtracted, exp.SHA1, exp.MD5, exp.CRC, exp.Size);

                if (string.IsNullOrWhiteSpace(hit.extractedName))
                    continue;
                mapping[exp.Name] = (hit.extractedName, hit.swap16);
                usedExpected.Add(exp.Name);
                usedExtracted.Add(hit.extractedName);
                reason(exp.Name, hit.extractedName, hit.swap16, "by hash");
            }

            Dictionary<string, string> extractedCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < trackFiles.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(trackFiles[i].fileName))
                    continue;
                if (!extractedCategory.ContainsKey(trackFiles[i].fileName))
                    extractedCategory[trackFiles[i].fileName] = GetTrackCategory(trackFiles[i].trackType, trackFiles[i].fileName);
            }

            Dictionary<int, List<string>> extractedByTrackNo = new Dictionary<int, List<string>>();
            for (int i = 0; i < trackFiles.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(trackFiles[i].fileName))
                    continue;
                if (!fileHashCache.ContainsKey(trackFiles[i].fileName))
                    continue;
                if (!extractedByTrackNo.TryGetValue(trackFiles[i].trackNo, out List<string> list))
                {
                    list = new List<string>();
                    extractedByTrackNo.Add(trackFiles[i].trackNo, list);
                }
                if (!list.Contains(trackFiles[i].fileName))
                    list.Add(trackFiles[i].fileName);
            }

            foreach (KeyValuePair<int, RvFile> kvp in expectedByTrack)
            {
                if (kvp.Value?.Name == null)
                    continue;
                if (hasExpectedHash(kvp.Value))
                    continue;
                if (usedExpected.Contains(kvp.Value.Name))
                    continue;
                if (!extractedByTrackNo.TryGetValue(kvp.Key, out List<string> list) || list.Count == 0)
                    continue;
                string extractedName = null;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!usedExtracted.Contains(list[i]))
                    {
                        extractedName = list[i];
                        break;
                    }
                }
                if (extractedName == null)
                    continue;

                mapping[kvp.Value.Name] = (extractedName, false);
                usedExpected.Add(kvp.Value.Name);
                usedExtracted.Add(extractedName);
                reason(kvp.Value.Name, extractedName, false, "by track number");
            }

            Dictionary<ulong, List<string>> extractedBySize = new Dictionary<ulong, List<string>>();
            foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashCache)
            {
                if (usedExtracted.Contains(kvp.Key))
                    continue;
                if (!extractedBySize.TryGetValue(kvp.Value.size, out List<string> list))
                {
                    list = new List<string>();
                    extractedBySize.Add(kvp.Value.size, list);
                }
                list.Add(kvp.Key);
            }

            for (int i = 0; i < expectedDataFiles.Count; i++)
            {
                RvFile expectedFile = expectedDataFiles[i];
                string expectedName = expectedFile.Name;
                if (string.IsNullOrWhiteSpace(expectedName) || usedExpected.Contains(expectedName))
                    continue;
                if (hasExpectedHash(expectedFile))
                    continue;
                ulong expectedSize = expectedFile.Size ?? 0;
                if (expectedSize == 0)
                    continue;
                if (!extractedBySize.TryGetValue(expectedSize, out List<string> candidates))
                    continue;
                string extractedName = null;
                if (candidates.Count == 1)
                {
                    extractedName = candidates[0];
                    reason(expectedName, extractedName, false, "by unique size");
                }
                else
                {
                    string expectedCat = GetExpectedCategory(expectedName);
                    if (!string.IsNullOrWhiteSpace(expectedCat))
                    {
                        List<string> filtered = new List<string>();
                        for (int c = 0; c < candidates.Count; c++)
                        {
                            if (extractedCategory.TryGetValue(candidates[c], out string cat) && string.Equals(cat, expectedCat, StringComparison.OrdinalIgnoreCase))
                                filtered.Add(candidates[c]);
                        }
                        if (filtered.Count == 1)
                        {
                            extractedName = filtered[0];
                            reason(expectedName, extractedName, false, "by size+type");
                        }
                    }

                    if (extractedName == null)
                    {
                        string expectedExt = System.IO.Path.GetExtension(expectedName) ?? "";
                        if (!string.IsNullOrWhiteSpace(expectedExt))
                        {
                            List<string> filtered = new List<string>();
                            for (int c = 0; c < candidates.Count; c++)
                            {
                                string ext = System.IO.Path.GetExtension(candidates[c]) ?? "";
                                if (string.Equals(ext, expectedExt, StringComparison.OrdinalIgnoreCase))
                                    filtered.Add(candidates[c]);
                            }
                            if (filtered.Count == 1)
                            {
                                extractedName = filtered[0];
                                reason(expectedName, extractedName, false, "by size+ext");
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(extractedName))
                    continue;
                mapping[expectedName] = (extractedName, false);
                usedExpected.Add(expectedName);
                usedExtracted.Add(extractedName);
            }

            if (layoutStrictness != ChdLayoutStrictness.Strict)
            {
                int fallbackIndex = 0;
                for (int i = 0; i < trackFiles.Count; i++)
                {
                    string extractedName = trackFiles[i].fileName;
                    if (string.IsNullOrWhiteSpace(extractedName))
                        continue;
                    if (usedExtracted.Contains(extractedName))
                        continue;
                    if (!fileHashCache.ContainsKey(extractedName))
                        continue;

                    while (fallbackIndex < expectedDataFiles.Count && (usedExpected.Contains(expectedDataFiles[fallbackIndex].Name) || hasExpectedHash(expectedDataFiles[fallbackIndex])))
                        fallbackIndex++;
                    if (fallbackIndex >= expectedDataFiles.Count)
                        break;

                    string expectedName = expectedDataFiles[fallbackIndex++].Name;
                    if (string.IsNullOrWhiteSpace(expectedName))
                        continue;

                    mapping[expectedName] = (extractedName, false);
                    usedExpected.Add(expectedName);
                    usedExtracted.Add(extractedName);
                    reason(expectedName, extractedName, false, "by order fallback");
                }
            }

            if (debug != null)
            {
                debug.AppendLine("mapping:");
                foreach (KeyValuePair<string, (string extractedName, bool swap16)> kvp in mapping)
                    debug.AppendLine($"  {kvp.Key} <= {kvp.Value.extractedName}{(kvp.Value.swap16 ? " (swap16)" : "")}");
            }

            return mapping;
        }

        /// <summary>
        /// Finds an unused extracted entry that matches the expected hashes.
        /// </summary>
        /// <remarks>
        /// Hash preference order is:
        /// - SHA1
        /// - MD5
        /// - CRC+Size (only when size is known)
        ///
        /// When <paramref name="fileHashSwap16Cache"/> is provided, swap16 variants are also considered.
        /// </remarks>
        private static (string extractedName, bool swap16) FindExtractedByHash(
            IDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache,
            IDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashSwap16Cache,
            HashSet<string> usedExtracted,
            byte[] expSha1,
            byte[] expMd5,
            byte[] expCrc,
            ulong? expSize)
        {
            if (fileHashCache == null || usedExtracted == null)
                return (null, false);

            if (expSha1 != null && expSha1.Length > 0)
            {
                foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashCache)
                {
                    if (usedExtracted.Contains(kvp.Key))
                        continue;
                    if (kvp.Value.sha1 == null)
                        continue;
                    if (kvp.Value.sha1.AsSpan().SequenceEqual(expSha1))
                        return (kvp.Key, false);
                }
                if (fileHashSwap16Cache != null)
                {
                    foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashSwap16Cache)
                    {
                        if (usedExtracted.Contains(kvp.Key))
                            continue;
                        if (kvp.Value.sha1 == null)
                            continue;
                        if (kvp.Value.sha1.AsSpan().SequenceEqual(expSha1))
                            return (kvp.Key, true);
                    }
                }
            }

            if (expMd5 != null && expMd5.Length > 0)
            {
                foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashCache)
                {
                    if (usedExtracted.Contains(kvp.Key))
                        continue;
                    if (kvp.Value.md5 == null)
                        continue;
                    if (kvp.Value.md5.AsSpan().SequenceEqual(expMd5))
                        return (kvp.Key, false);
                }
                if (fileHashSwap16Cache != null)
                {
                    foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashSwap16Cache)
                    {
                        if (usedExtracted.Contains(kvp.Key))
                            continue;
                        if (kvp.Value.md5 == null)
                            continue;
                        if (kvp.Value.md5.AsSpan().SequenceEqual(expMd5))
                            return (kvp.Key, true);
                    }
                }
            }

            if (expCrc != null && expCrc.Length > 0 && expSize.HasValue && expSize.Value != 0)
            {
                foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashCache)
                {
                    if (usedExtracted.Contains(kvp.Key))
                        continue;
                    if (kvp.Value.crc == null)
                        continue;
                    if (kvp.Value.size != expSize.Value)
                        continue;
                    if (kvp.Value.crc.AsSpan().SequenceEqual(expCrc))
                        return (kvp.Key, false);
                }
                if (fileHashSwap16Cache != null)
                {
                    foreach (KeyValuePair<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> kvp in fileHashSwap16Cache)
                    {
                        if (usedExtracted.Contains(kvp.Key))
                            continue;
                        if (kvp.Value.crc == null)
                            continue;
                        if (kvp.Value.size != expSize.Value)
                            continue;
                        if (kvp.Value.crc.AsSpan().SequenceEqual(expCrc))
                            return (kvp.Key, true);
                    }
                }
            }

            return (null, false);
        }

        private static string GetExpectedCategory(string expectedName)
        {
            if (string.IsNullOrWhiteSpace(expectedName))
                return "";
            string ext = (System.IO.Path.GetExtension(expectedName) ?? "").ToLowerInvariant();
            string name = expectedName.ToLowerInvariant();
            if (ext == ".iso")
                return "data";
            if (name.Contains("data"))
                return "data";
            if (name.Contains("audio"))
                return "audio";
            if (name.Contains("track"))
                return "audio";
            return "";
        }

        private static string GetTrackCategory(string trackType, string fileName)
        {
            string tt = (trackType ?? "").Trim().ToUpperInvariant();
            if (tt.Contains("AUDIO"))
                return "audio";
            if (tt.Contains("2048"))
                return "data";
            string ext = (System.IO.Path.GetExtension(fileName) ?? "").ToLowerInvariant();
            if (ext == ".iso")
                return "data";
            return "";
        }

        /// <summary>
        /// Fallback scan mode for single-BIN CUEs where the DAT expects per-track files.
        /// </summary>
        /// <remarks>
        /// Some CHD extracts produce a single <c>disc.bin</c> referenced by a CUE, but DATs may list one file per track.
        /// This mode extracts to a single BIN, slices it into track windows (including synthesized pregap/postgap where applicable),
        /// hashes each slice, and maps slices to expected DAT members.
        /// </remarks>
        private static bool TryScanSingleBinCueAsTracks(
            string chdPath,
            string chdmanExe,
            string tempDir,
            string cuePath,
            Dictionary<int, RvFile> expectedByTrack,
            List<RvFile> expectedDataFiles,
            ScannedFile archive,
            System.Text.StringBuilder debug,
            DatRule datRule)
        {
            string outBin = System.IO.Path.Combine(tempDir, "disc.bin");
            string extractArgs = $"extractcd -i \"{chdPath}\" -o \"{cuePath}\" -ob \"{outBin}\" -f";
            if (!RunProcess(chdmanExe, extractArgs, tempDir, out string err))
            {
                _thWrk?.Report(new bgwShowError(chdPath, "CHD extractcd (single bin) failed: " + err));
                return false;
            }

            if (!System.IO.File.Exists(outBin))
                return false;

            List<ChdTrack> tracks = ParseCueTracksWithIndexes(cuePath);
            if (tracks.Count == 0)
                return false;
            tracks.Sort((a, b) => a.TrackNo.CompareTo(b.TrackNo));

            long binLength = new FileInfo(outBin).Length;
            int sectorSize = (binLength % 2352L == 0) ? 2352 : (binLength % 2048L == 0 ? 2048 : 2352);

            List<(int trackNo, long offset, long length, long prefixPadLength, long suffixPadLength)> segments = new List<(int, long, long, long, long)>();
            for (int i = 0; i < tracks.Count; i++)
            {
                long thisStartFrames = (tracks[i].Index00Frames.HasValue && !tracks[i].IsSynthesizedPregap) ? tracks[i].Index00Frames.Value : tracks[i].StartFrames;
                long startBytes = thisStartFrames * sectorSize;

                long endBytes = binLength;
                if (i + 1 < tracks.Count)
                {
                    long nextStartFrames = (tracks[i + 1].Index00Frames.HasValue && !tracks[i + 1].IsSynthesizedPregap) ? tracks[i + 1].Index00Frames.Value : tracks[i + 1].StartFrames;
                    endBytes = nextStartFrames * sectorSize;
                }

                if (endBytes < startBytes)
                    endBytes = startBytes;
                long len = endBytes - startBytes;

                long prefixPadLength = 0;
                long suffixPadLength = 0;
                if (tracks[i].IsSynthesizedPregap)
                    prefixPadLength = Math.Max(0, tracks[i].SynthesizedPregapFrames) * sectorSize;
                if (tracks[i].IsSynthesizedPostgap)
                    suffixPadLength = Math.Max(0, tracks[i].SynthesizedPostgapFrames) * sectorSize;

                segments.Add((tracks[i].TrackNo, startBytes, len, prefixPadLength, suffixPadLength));
            }

            Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> sliceHashes = new Dictionary<string, (ulong, byte[], byte[], byte[])>(StringComparer.OrdinalIgnoreCase);
            using (FileStream binStream = System.IO.File.OpenRead(outBin))
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    string key = "track:" + segments[i].trackNo.ToString("D2");
                    ulong totalLength = (ulong)(segments[i].length + segments[i].prefixPadLength + segments[i].suffixPadLength);
                    ScannedFile tmp = new ScannedFile(FileType.FileCHD)
                    {
                        Name = key,
                        FileModTimeStamp = new FileInfo(outBin).LastWriteTime,
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = totalLength
                    };
                    using (ReadOnlySliceStream slice = new ReadOnlySliceStream(binStream, segments[i].offset, segments[i].length))
                    {
                        if (segments[i].prefixPadLength > 0 || segments[i].suffixPadLength > 0)
                        {
                            Stream s = slice;
                            if (segments[i].suffixPadLength > 0)
                                s = new ZeroPadStream(s, segments[i].suffixPadLength);
                            if (segments[i].prefixPadLength > 0)
                                s = new PrefixZeroStream(s, segments[i].prefixPadLength);
                            using (s)
                            {
                                _fileScans.CheckSumRead(s, tmp, totalLength, true, false, null, 0, 0);
                            }
                        }
                        else
                        {
                            _fileScans.CheckSumRead(slice, tmp, totalLength, true, false, null, 0, 0);
                        }
                    }
                    sliceHashes[key] = (totalLength, tmp.CRC, tmp.SHA1, tmp.MD5);
                }
            }

            List<(int trackNo, string fileName, string trackType)> pseudoTracks = new List<(int, string, string)>();
            for (int i = 0; i < segments.Count; i++)
                pseudoTracks.Add((segments[i].trackNo, "track:" + segments[i].trackNo.ToString("D2"), ""));

            Dictionary<string, (string extractedName, bool swap16)> mapping = BuildDeterministicMapping(pseudoTracks, sliceHashes, null, expectedByTrack, expectedDataFiles, debug,
                datRule?.ChdAudioTransform ?? ChdAudioTransform.None,
                datRule?.ChdLayoutStrictness ?? ChdLayoutStrictness.Normal);
            foreach (KeyValuePair<string, (string extractedName, bool swap16)> kvp in mapping)
            {
                if (!sliceHashes.TryGetValue(kvp.Value.extractedName, out var h))
                    continue;
                ScannedFile sf = new ScannedFile(FileType.FileCHD)
                {
                    Name = kvp.Key,
                    FileModTimeStamp = new FileInfo(outBin).LastWriteTime,
                    GotStatus = GotStatus.Got,
                    DeepScanned = true,
                    Size = h.size,
                    CRC = h.crc,
                    SHA1 = h.sha1,
                    MD5 = h.md5
                };
                sf.FileStatusSet(FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified);
                archive.Add(sf);
            }

            return archive.Count > 0;
        }

        private static List<ChdTrack> ParseCueTracksWithIndexes(string cuePath)
        {
            List<ChdTrack> tracks = new List<ChdTrack>();
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(cuePath);
            }
            catch
            {
                return tracks;
            }

            ChdTrack current = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int trackNo))
                    {
                        current = new ChdTrack
                        {
                            TrackNo = trackNo,
                            TrackType = parts[2].Trim(),
                            StartFrames = 0,
                            Index00Frames = null,
                            PreGapFrames = 0,
                            PostGapFrames = 0,
                            IsSynthesizedPregap = false,
                            SynthesizedPregapFrames = 0,
                            IsSynthesizedPostgap = false,
                            SynthesizedPostgapFrames = 0
                        };
                        tracks.Add(current);
                    }
                    continue;
                }

                if (current != null && line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (TryParseMsf(parts[2], out long frames))
                            current.StartFrames = frames;
                        if (current.Index00Frames.HasValue)
                            current.PreGapFrames = Math.Max(current.PreGapFrames, Math.Max(0, current.StartFrames - current.Index00Frames.Value));
                    }
                }

                if (current != null && line.StartsWith("INDEX 00", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (TryParseMsf(parts[2], out long frames))
                        {
                            current.Index00Frames = frames;
                            if (current.StartFrames > 0)
                                current.PreGapFrames = Math.Max(current.PreGapFrames, Math.Max(0, current.StartFrames - frames));
                        }
                    }
                }

                if (current != null && line.StartsWith("PREGAP", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        if (TryParseMsf(parts[1], out long frames))
                        {
                            current.PreGapFrames = Math.Max(current.PreGapFrames, frames);
                            current.IsSynthesizedPregap = true;
                            current.SynthesizedPregapFrames = frames;
                        }
                    }
                }

                if (current != null && line.StartsWith("POSTGAP", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        if (TryParseMsf(parts[1], out long frames))
                        {
                            current.PostGapFrames = Math.Max(current.PostGapFrames, frames);
                            current.IsSynthesizedPostgap = true;
                            current.SynthesizedPostgapFrames = frames;
                        }
                    }
                }
            }

            return tracks;
        }

        private static int ResolveSectorSize(string trackType, int fallback)
        {
            if (string.IsNullOrWhiteSpace(trackType))
                return fallback;
            string t = trackType.Trim().ToUpperInvariant();
            if (t.Contains("2048"))
                return 2048;
            if (t.Contains("AUDIO"))
                return 2352;
            if (t.Contains("2352"))
                return 2352;
            return fallback;
        }

        private static string GetChdScanDebugPath(string chdPath)
        {
            string baseTempDir = ResolveExistingDirectoryPath(DB.GetToSortCache()?.FullName);
            if (string.IsNullOrWhiteSpace(baseTempDir))
                baseTempDir = Environment.CurrentDirectory;
            string dir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdscanCache");
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
        /// Returns the filesystem path to the JSON cache entry for a CHD file.
        /// </summary>
        /// <remarks>
        /// Cache keys are derived from the CHD's full path (MD5 hex) and stored under ToSortCache.
        /// </remarks>
        private static string GetChdScanCachePath(string chdPath)
        {
            string baseTempDir = ResolveExistingDirectoryPath(DB.GetToSortCache()?.FullName);
            if (string.IsNullOrWhiteSpace(baseTempDir))
                baseTempDir = Environment.CurrentDirectory;
            string dir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdscanCache");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
            }

            string key = ComputeMd5Hex(chdPath.ToLowerInvariant());
            return System.IO.Path.Combine(dir, key + ".json");
        }

        /// <summary>
        /// Attempts to load a cached scan result for a CHD file and validates it against current rules.
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
                string cachePath = GetChdScanCachePath(chdPath);
                if (!System.IO.File.Exists(cachePath))
                    return false;

                FileInfo fi = new FileInfo(chdPath);
                string json = System.IO.File.ReadAllText(cachePath);
                ChdCacheFile loaded = JsonSerializer.Deserialize<ChdCacheFile>(json);
                if (loaded == null)
                    return false;

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
        /// Writes the scan result of a CHD container to the JSON cache for reuse on future scans.
        /// </summary>
        /// <remarks>
        /// Only member entries (<see cref="FileType.FileCHD"/>) are persisted.
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

                string cachePath = GetChdScanCachePath(chdPath);
                string json = JsonSerializer.Serialize(cache);
                System.IO.File.WriteAllText(cachePath, json);
            }
            catch
            {
            }
        }

        private static string ComputeChdSettingsFingerprint(DatRule datRule)
        {
            try
            {
                var s = Settings.rvSettings;
                string text =
                    $"stream={s?.ChdStreaming};trust={s?.ChdTrustContainerForTracks};strict={s?.ChdStrictCueGdi};keep={s?.ChdKeepCueGdi};" +
                    $"synthetic={s?.ChdPreferSynthetic};audio={s?.ChdAudioTransform};layout={s?.ChdLayoutStrictness};verchk={s?.CheckCHDVersion};" +
                    $"ruleStrict={datRule?.ChdStrictCueGdi};ruleKeep={datRule?.ChdKeepCueGdi};ruleAudio={datRule?.ChdAudioTransform};ruleLayout={datRule?.ChdLayoutStrictness}";
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

            string output = "";
            bool ok = RunProcess(chdmanExe, "--version", workingDir, out output);
            if (!ok || string.IsNullOrWhiteSpace(output))
                ok = RunProcess(chdmanExe, "-version", workingDir, out output);
            if (!ok || string.IsNullOrWhiteSpace(output))
            {
                lock (ChdToolFingerprintLock)
                    ChdToolFingerprintCache[key] = "";
                return "";
            }

            string first = output.Replace("\r", "").Split('\n')[0].Trim();
            string libVer = "";
            try { libVer = typeof(CHD).Assembly.GetName().Version?.ToString() ?? ""; } catch { }
            string fp = $"chdman={first};chdlib={libVer}";
            lock (ChdToolFingerprintLock)
                ChdToolFingerprintCache[key] = fp;
            return fp;
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
            return ext == ".bin" || ext == ".raw" || ext == ".iso";
        }

        private static List<ChdTrack> ParseCueTracks(string cuePath)
        {
            List<ChdTrack> tracks = new List<ChdTrack>();
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(cuePath);
            }
            catch
            {
                return tracks;
            }

            ChdTrack current = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int trackNo))
                    {
                        current = new ChdTrack
                        {
                            TrackNo = trackNo,
                            TrackType = parts[2].Trim(),
                            StartFrames = 0
                        };
                        tracks.Add(current);
                    }
                    continue;
                }

                if (current != null && line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (TryParseMsf(parts[2], out long frames))
                            current.StartFrames = frames;
                    }
                }
            }

            return tracks;
        }

        private static bool TryParseMsf(string msf, out long frames)
        {
            frames = 0;
            if (string.IsNullOrWhiteSpace(msf))
                return false;
            string[] parts = msf.Trim().Split(':');
            if (parts.Length != 3)
                return false;
            if (!int.TryParse(parts[0], out int mm))
                return false;
            if (!int.TryParse(parts[1], out int ss))
                return false;
            if (!int.TryParse(parts[2], out int ff))
                return false;
            frames = (mm * 60L + ss) * 75L + ff;
            return true;
        }

        private static string BuildPerTrackCueText(List<RvFile> expectedBins, List<ChdTrack> tracks)
        {
            Dictionary<int, string> byTrackNo = new Dictionary<int, string>();
            Regex r = new Regex(@"\(Track\s*(\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            for (int i = 0; i < expectedBins.Count; i++)
            {
                Match m = r.Match(expectedBins[i].Name ?? "");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int tno))
                {
                    if (!byTrackNo.ContainsKey(tno))
                        byTrackNo.Add(tno, expectedBins[i].Name);
                }
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < tracks.Count; i++)
            {
                string fileName = byTrackNo.TryGetValue(tracks[i].TrackNo, out string name) ? name : $"Track {tracks[i].TrackNo:D2}.bin";
                sb.Append("FILE \"").Append(fileName).AppendLine("\" BINARY");
                sb.Append("  TRACK ").Append(tracks[i].TrackNo.ToString("D2")).Append(' ').AppendLine(tracks[i].TrackType);
                sb.AppendLine("    INDEX 01 00:00:00");
            }
            return sb.ToString();
        }

        private static string FindChdmanExePath()
        {
            string baseDir = "";
            try
            {
                baseDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                string candidate = System.IO.Path.Combine(baseDir, "chdman.exe");
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }

            string cwd = "";
            try
            {
                cwd = Environment.CurrentDirectory;
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(cwd))
            {
                string candidate = System.IO.Path.Combine(cwd, "chdman.exe");
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }

            return "chdman.exe";
        }

        private static bool RunProcess(string exe, string args, string workingDir, out string output)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = new Process { StartInfo = psi })
                {
                    p.Start();
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    output = (stdout + Environment.NewLine + stderr).Trim();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }

        private static long GetFreeSpaceBytes(string path)
        {
            try
            {
                string root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path));
                DriveInfo di = new DriveInfo(root);
                return di.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        private static long? TryGetChdLogicalSizeBytes(string chdmanExe, string chdPath, string workingDir)
        {
            if (!RunProcess(chdmanExe, $"info -i \"{chdPath}\"", workingDir, out string output))
                return null;

            try
            {
                Match m = Regex.Match(output ?? "", @"\((\d+)\s+bytes\)", RegexOptions.IgnoreCase);
                if (m.Success && long.TryParse(m.Groups[1].Value, out long v))
                    return v;
            }
            catch
            {
            }

            return null;
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
