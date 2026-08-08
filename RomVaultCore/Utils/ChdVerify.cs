using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CHDSharpLib;
using FileScanner;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RomVaultCore.Utils;
using RVUtils;

namespace RomVaultCore.Utils;

public static class ChdVerify
{
    public sealed class MapRow
    {
        public string Expected { get; set; }
        public string Extracted { get; set; }
        public string Reason { get; set; }
    }

    public static int TryGenerateReport(string chdPath, System.Collections.Generic.IReadOnlyList<RvFile> expectedMembers, out string report, out System.Collections.Generic.List<MapRow> mapping)
    {
        int rc = TryGenerateReportInternal(chdPath, expectedMembers, false, out report, out mapping, out _, out _);
        return rc;
    }

    public static int TryGenerateReport(string chdPath, out string report)
    {
        System.Collections.Generic.List<MapRow> dummy;
        int rc = TryGenerateReportInternal(chdPath, null, false, out report, out dummy, out _, out _);
        return rc;
    }

    public static int TryGenerateParityReport(string chdPath, System.Collections.Generic.IReadOnlyList<RvFile> expectedMembers, out string report)
    {
        report = "";
        if (expectedMembers == null || expectedMembers.Count == 0)
        {
            report = "parity failed: expected members required";
            return 2;
        }

        int rcStream = TryGenerateReportInternal(chdPath, expectedMembers, true, out string repStream, out _, out Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> expStream, out string modeStream);
        int rcExtract = TryGenerateReportInternal(chdPath, expectedMembers, false, out string repExtract, out _, out Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> expExtract, out string modeExtract);

        List<string> lines = new List<string>();
        lines.Add("CHD parity");
        lines.Add(ChdDiagnosticFormatter.RedactPath(chdPath));
        lines.Add("streamMode=" + modeStream + " rc=" + rcStream);
        lines.Add("extractMode=" + modeExtract + " rc=" + rcExtract);
        lines.Add("");

        if (rcStream != 0)
        {
            lines.Add("stream report:");
            lines.Add(repStream);
            report = string.Join(Environment.NewLine, lines);
            return rcStream;
        }
        if (rcExtract != 0)
        {
            lines.Add("extract report:");
            lines.Add(repExtract);
            report = string.Join(Environment.NewLine, lines);
            return rcExtract;
        }

        HashSet<string> all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in expStream.Keys) all.Add(k);
        foreach (var k in expExtract.Keys) all.Add(k);

        List<string> diffs = new List<string>();
        foreach (string k in all)
        {
            bool hs = expStream.TryGetValue(k, out var a);
            bool he = expExtract.TryGetValue(k, out var b);
            if (!hs || !he)
            {
                diffs.Add($"{k}: missing in " + (!hs ? "stream" : "extract"));
                continue;
            }
            if (a.size != b.size) diffs.Add($"{k}: size {a.size} != {b.size}");
            if (!ByteEq(a.crc, b.crc)) diffs.Add($"{k}: crc mismatch");
            if (!ByteEq(a.sha1, b.sha1)) diffs.Add($"{k}: sha1 mismatch");
            if (!ByteEq(a.md5, b.md5)) diffs.Add($"{k}: md5 mismatch");
        }

        lines.Add("diffs:");
        if (diffs.Count == 0)
            lines.Add("OK");
        else
            lines.AddRange(diffs);
        lines.Add("");
        lines.Add("stream report:");
        lines.Add(repStream);
        lines.Add("");
        lines.Add("extract report:");
        lines.Add(repExtract);
        report = string.Join(Environment.NewLine, lines);
        return diffs.Count == 0 ? 0 : 5;
    }

    /// <summary>
    /// Generates a unified CHD health report that summarizes container metadata, scan mode,
    /// expected-hash outcomes, and optional stream-vs-extract parity.
    /// </summary>
    public static int TryGenerateHealthReport(string chdPath, System.Collections.Generic.IReadOnlyList<RvFile> expectedMembers, out string report)
    {
        report = "";
        int rcVerify = TryGenerateReportInternal(chdPath, expectedMembers, false, out string verifyReport, out _, out Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> expectedHashes, out string modeUsed);

        uint? ver = null;
        byte[] chdSha1 = null;
        byte[] chdMd5 = null;
        try
        {
            using (FileStream fs = System.IO.File.OpenRead(chdPath))
            {
                CHD.CheckFile(fs, chdPath, false, out ver, out chdSha1, out chdMd5);
            }
        }
        catch
        {
        }

        string chdmanVersion = "";
        string chdmanInfoCompression = "";
        string chdmanInfoHunkBytes = "";
        string chdmanInfoUnitBytes = "";
        string chdmanInfoLogicalBytes = "";
        string chdmanDumpMeta = "";
        string standardProfile = "missing";
        string reconstructionManifest = "missing";
        if (ChdEncodingProfile.TryRead(chdPath, out ChdEncodingProfile embeddedProfile))
        {
            standardProfile = $"{embeddedProfile.Profile}/r{embeddedProfile.ProfileRevision};family={embeddedProfile.Family};writer={embeddedProfile.WriterRevision};encoder={embeddedProfile.ChdmanText};toolsha256={embeddedProfile.ToolSha256}";
        }
        if (ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest embeddedManifest, out string manifestError))
        {
            reconstructionManifest = $"schema={embeddedManifest.Schema};family={embeddedManifest.Family};dialect={embeddedManifest.Dialect};tracks={embeddedManifest.Tracks.Count};descriptorBytes={embeddedManifest.DescriptorBytes.Length};capabilities={embeddedManifest.CapabilityFingerprint}";
        }
        else if (!string.IsNullOrWhiteSpace(manifestError))
        {
            reconstructionManifest = "invalid: " + manifestError;
        }
        try
        {
            string chdmanExe = ChdmanProcessTracker.FindExecutable();
            if (ChdmanService.TryGetIdentity(chdmanExe, ChdmanProbeLevel.Full, out ChdmanIdentity identity, out _))
                chdmanVersion = identity.Banner + " sha256=" + identity.BinarySha256 + " capabilities=" + identity.Capabilities?.Fingerprint;

            if (RunProcess(chdmanExe, $"info -i \"{chdPath}\"", Path.GetDirectoryName(chdPath) ?? Environment.CurrentDirectory, out string infoOut))
            {
                ParseInfoField(infoOut, "Compression", out chdmanInfoCompression);
                ParseInfoBytesField(infoOut, "Hunk Size", out chdmanInfoHunkBytes);
                ParseInfoBytesField(infoOut, "Unit Size", out chdmanInfoUnitBytes);
                ParseInfoBytesField(infoOut, "Logical size", out chdmanInfoLogicalBytes);
            }

            if (RunProcess(chdmanExe, $"dumpmeta -i \"{chdPath}\"", Path.GetDirectoryName(chdPath) ?? Environment.CurrentDirectory, out string metaOut))
            {
                chdmanDumpMeta = NormalizeDumpMeta(metaOut, 80);
            }
        }
        catch
        {
            chdmanVersion = "";
        }

        List<string> lines = new List<string>
        {
            "CHD health",
            ChdDiagnosticFormatter.RedactPath(chdPath),
            $"containerVersion={(ver.HasValue ? "V" + ver.Value : "unknown")}",
            $"containerSha1={chdSha1?.ToHexString() ?? ""}",
            $"containerMd5={chdMd5?.ToHexString() ?? ""}",
            $"scanMode={modeUsed}",
            $"chdmanVersion={chdmanVersion}",
            $"compression={chdmanInfoCompression}",
            $"hunkBytes={chdmanInfoHunkBytes}",
            $"unitBytes={chdmanInfoUnitBytes}",
            $"logicalBytes={chdmanInfoLogicalBytes}",
            $"standardProfile={standardProfile}",
            $"reconstructionManifest={reconstructionManifest}",
            $"verifyRc={rcVerify}"
        };

        int rcParity = 0;
        string parityReport = "";
        bool hasExpected = expectedMembers != null && expectedMembers.Count > 0;
        if (hasExpected && rcVerify == 0)
        {
            (int compared, int matched, int mismatched, int missing) = CompareExpectedHashes(expectedMembers, expectedHashes);
            bool hashMatch = mismatched == 0 && missing == 0;
            lines.Add($"expectedCompared={compared}");
            lines.Add($"expectedMatched={matched}");
            lines.Add($"expectedMismatched={mismatched}");
            lines.Add($"expectedMissing={missing}");
            lines.Add($"trackHashResult={(hashMatch ? "Match" : "Mismatch")}");

            rcParity = TryGenerateParityReport(chdPath, expectedMembers, out parityReport);
            lines.Add($"parityRc={rcParity}");
        }
        else
        {
            lines.Add("expectedCompared=0");
            lines.Add("trackHashResult=NotAvailable");
            lines.Add("parityRc=NotRun");
        }

        lines.Add("");
        lines.Add("verify report:");
        lines.Add(verifyReport);
        if (!string.IsNullOrWhiteSpace(parityReport))
        {
            lines.Add("");
            lines.Add("parity report:");
            lines.Add(parityReport);
        }
        if (!string.IsNullOrWhiteSpace(chdmanDumpMeta))
        {
            lines.Add("");
            lines.Add("metadata dump (truncated):");
            lines.Add(chdmanDumpMeta);
        }

        report = string.Join(Environment.NewLine, lines);

        if (rcVerify != 0)
            return rcVerify;
        if (hasExpected && rcParity != 0)
            return rcParity;
        return 0;
    }

    /// <summary>
    /// Generates a unified CHD health report without DAT expectations.
    /// </summary>
    public static int TryGenerateHealthReport(string chdPath, out string report)
    {
        return TryGenerateHealthReport(chdPath, null, out report);
    }

    private static bool ByteEq(byte[] a, byte[] b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static void ParseInfoField(string infoText, string fieldName, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(infoText) || string.IsNullOrWhiteSpace(fieldName))
            return;
        string[] lines = infoText.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i]?.Trim() ?? "";
            if (!line.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase))
                continue;
            int idx = line.IndexOf(':');
            if (idx < 0 || idx + 1 >= line.Length)
                continue;
            value = line.Substring(idx + 1).Trim();
            return;
        }
    }

    private static void ParseInfoBytesField(string infoText, string fieldName, out string bytesOnly)
    {
        bytesOnly = "";
        ParseInfoField(infoText, fieldName, out string raw);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        Match m = Regex.Match(raw, @"([0-9][0-9,]*)\s*bytes", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            bytesOnly = m.Groups[1].Value.Replace(",", "").Trim();
            return;
        }

        Match n = Regex.Match(raw, @"^\s*([0-9][0-9,]*)\s*$");
        if (n.Success)
            bytesOnly = n.Groups[1].Value.Replace(",", "").Trim();
    }

    private static string NormalizeDumpMeta(string text, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        string[] lines = text.Replace("\r", "").Split('\n');
        List<string> kept = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i]?.TrimEnd() ?? "";
            if (string.IsNullOrWhiteSpace(l))
                continue;
            kept.Add(l);
            if (kept.Count >= Math.Max(1, maxLines))
                break;
        }
        if (kept.Count == 0)
            return "";
        if (kept.Count < lines.Count(l => !string.IsNullOrWhiteSpace(l)))
            kept.Add("... (truncated)");
        return string.Join(Environment.NewLine, kept);
    }

    private static int TryGenerateReportInternal(string chdPath, System.Collections.Generic.IReadOnlyList<RvFile> expectedMembers, bool? forceStreaming, out string report, out System.Collections.Generic.List<MapRow> mapping, out Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> expectedHashes, out string modeUsed)
    {
        mapping = new System.Collections.Generic.List<MapRow>();
        expectedHashes = new Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)>(StringComparer.OrdinalIgnoreCase);
        modeUsed = "";
        report = "";
        if (string.IsNullOrWhiteSpace(chdPath) || !System.IO.File.Exists(chdPath))
        {
            report = "verify failed: file not found";
            return 2;
        }

        string baseTempDir = null;
        try
        {
            baseTempDir = DB.GetToSortCache()?.FullName;
        }
        catch
        {
        }
        if (string.IsNullOrWhiteSpace(baseTempDir))
            baseTempDir = System.IO.Path.GetTempPath();
        string tempDir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdverify." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string chdmanExe = ChdmanProcessTracker.FindExecutable();
            long? logicalSize = TryGetChdLogicalSizeBytes(chdmanExe, chdPath, tempDir);
            if (logicalSize.HasValue)
            {
                long free = GetFreeSpaceBytes(tempDir);
                if (free > 0 && free < logicalSize.Value + 256L * 1024 * 1024)
                {
                    report = $"verify failed: insufficient free space. required={logicalSize.Value} free={free}";
                    return 4;
                }
            }

            uint? ver = null;
            byte[] chdSha1 = null;
            byte[] chdMd5 = null;
            try
            {
                using (FileStream fs = System.IO.File.OpenRead(chdPath))
                {
                    CHD.CheckFile(fs, chdPath, false, out ver, out chdSha1, out chdMd5);
                }
            }
            catch
            {
            }

            List<string> extractedFiles = new List<string>();
            string mode = "";

            bool expectsIso = false;
            bool expectsGdi = false;
            RvFile expectedSingle = null;
            string singleFamily = null;
            if (expectedMembers != null)
            {
                for (int i = 0; i < expectedMembers.Count; i++)
                {
                    if (expectedMembers[i]?.Name != null && expectedMembers[i].Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                    {
                        expectsIso = true;
                    }
                    if (expectedMembers[i]?.Name != null && expectedMembers[i].Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))
                        expectsGdi = true;
                }
                if (!expectsIso && !expectsGdi && expectedMembers.Count == 1)
                {
                    expectedSingle = expectedMembers[0];
                    if (ChdEncodingProfile.TryDescribeExisting(chdPath, false, ChdStorageProfile.Archive, out ChdEncodingProfileSpec describedProfile, out _, out _) &&
                        (describedProfile.Family == "raw" || describedProfile.Family == "hdd" || describedProfile.Family == "laserdisc"))
                        singleFamily = describedProfile.Family;
                }
            }

            bool hasCdMetadata = false;
            bool metadataIsGdi = false;
            try
            {
                hasCdMetadata = CHDSharpLib.ChdMetadata.TryReadCdTrackLayout(chdPath, out var detectedTracks, out metadataIsGdi, out _) &&
                                detectedTracks != null && detectedTracks.Count > 0;
            }
            catch
            {
                hasCdMetadata = false;
                metadataIsGdi = false;
            }
            if (expectedMembers == null && metadataIsGdi)
                expectsGdi = true;
            bool treatAsDvd = expectsIso || (expectedMembers == null && !hasCdMetadata && string.IsNullOrWhiteSpace(singleFamily));

            bool wantsStreaming = forceStreaming ?? (Settings.rvSettings?.ChdStreaming == true);

            if (expectedMembers != null && expectsIso && wantsStreaming && string.IsNullOrWhiteSpace(singleFamily))
            {
                mode = "dvd-stream";
            }
            else if (expectedMembers != null && !expectsIso && wantsStreaming)
            {
                mode = "cd-stream";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(singleFamily))
                {
                    string outputName = System.IO.Path.GetFileName(expectedSingle.Name);
                    string outputPath = System.IO.Path.Combine(tempDir, outputName);
                    IChdExtractor extractor = new ChdmanChdExtractor(chdmanExe, tempDir);
                    bool ok;
                    string extractionError;
                    if (singleFamily == "raw")
                        ok = extractor.ExtractRaw(chdPath, outputPath, out extractionError);
                    else if (singleFamily == "hdd")
                        ok = extractor.ExtractHardDisk(chdPath, outputPath, out extractionError);
                    else
                        ok = extractor.ExtractLaserDisc(chdPath, outputPath, out extractionError);
                    if (!ok || !System.IO.File.Exists(outputPath))
                    {
                        report = $"verify failed: {singleFamily} extraction: {extractionError}";
                        return 3;
                    }
                    extractedFiles.Add(outputPath);
                    mode = singleFamily;
                }
                else if (treatAsDvd)
                {
                    string outIso = System.IO.Path.Combine(tempDir, "image.iso");
                    if (!RunProcess(chdmanExe, $"extractdvd -i \"{chdPath}\" -o \"{outIso}\" -f", tempDir, out string dvdError) ||
                        !System.IO.File.Exists(outIso))
                    {
                        report = $"verify failed: {dvdError}";
                        return 3;
                    }
                    extractedFiles.Add(outIso);
                    mode = "dvd";
                }
                else
                {
                    string outDescriptor = System.IO.Path.Combine(tempDir, expectsGdi ? "disc.gdi" : "disc.cue");
                    IChdExtractor extractor = new ChdmanChdExtractor(chdmanExe, tempDir);
                    if (!extractor.ExtractCd(chdPath, outDescriptor, out string err))
                    {
                        report = $"verify failed: {err}";
                        return 3;
                    }

                    mode = expectsGdi ? "cd-gdi-split" : "cd-cue-split";
                    foreach (string file in Directory.GetFiles(tempDir, "*", SearchOption.TopDirectoryOnly))
                        extractedFiles.Add(file);
                }
            }

            FileScan scanner = new FileScan();
            List<string> lines = new List<string>();
            lines.Add("CHD verify");
            lines.Add(ChdDiagnosticFormatter.RedactPath(chdPath));
            lines.Add("chdVersion=" + (ver?.ToString() ?? ""));
            lines.Add("chdSha1=" + (chdSha1?.ToHexString() ?? ""));
            lines.Add("chdMd5=" + (chdMd5?.ToHexString() ?? ""));
            lines.Add("mode=" + mode);
            lines.Add("");

            Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache = new Dictionary<string, (ulong, byte[], byte[], byte[])>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(mode, "dvd-stream", StringComparison.OrdinalIgnoreCase))
            {
                using (Stream s = CHDSharpLib.ChdLogicalStream.OpenRead(chdPath))
                {
                    ulong size = (ulong)s.Length;
                    ScannedFile sf = new ScannedFile(FileType.File)
                    {
                        Name = "image.iso",
                        FileModTimeStamp = System.IO.File.GetLastWriteTimeUtc(chdPath).ToFileTimeUtc(),
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = size
                    };
                    scanner.CheckSumRead(s, sf, size, true, false, null, 0, 0);
                    lines.Add($"{sf.Name} size={sf.Size} crc={sf.CRC?.ToHexString()} sha1={sf.SHA1?.ToHexString()} md5={sf.MD5?.ToHexString()}");
                    fileHashCache[sf.Name] = (size, sf.CRC, sf.SHA1, sf.MD5);
                }
            }
            else if (string.Equals(mode, "cd-stream", StringComparison.OrdinalIgnoreCase))
            {
                if (CHDSharpLib.ChdMetadata.TryReadCdTrackLayout(chdPath, out var cdTracks, out string metaErr))
                {
                    using (Stream s = CHDSharpLib.ChdLogicalStream.OpenRead(chdPath))
                    {
                        ulong cur = 0;
                        for (int i = 0; i < cdTracks.Count; i++)
                        {
                            var t = cdTracks[i];
                            ulong start = (ulong)Math.Max(0, t.StartFrame) * (ulong)Math.Max(1, t.SectorSize);
                            ulong len = (ulong)Math.Max(0, t.Frames) * (ulong)Math.Max(1, t.SectorSize);
                            if (start < cur)
                                start = cur;
                            if (start > cur)
                            {
                                SkipBytes(s, start - cur);
                                cur = start;
                            }
                            string key = "track:" + t.TrackNo.ToString("D2");
                            ScannedFile sf = new ScannedFile(FileType.File)
                            {
                                Name = key,
                                FileModTimeStamp = System.IO.File.GetLastWriteTimeUtc(chdPath).ToFileTimeUtc(),
                                GotStatus = GotStatus.Got,
                                DeepScanned = true,
                                Size = len
                            };
                            using (ReadOnlyLimitedStream limited = new ReadOnlyLimitedStream(s, (long)len))
                            {
                                scanner.CheckSumRead(limited, sf, len, true, false, null, 0, 0);
                            }
                            lines.Add($"{sf.Name} size={sf.Size} crc={sf.CRC?.ToHexString()} sha1={sf.SHA1?.ToHexString()} md5={sf.MD5?.ToHexString()}");
                            fileHashCache[sf.Name] = (len, sf.CRC, sf.SHA1, sf.MD5);
                            cur += len;
                        }
                    }
                    if (expectedMembers != null)
                    {
                        RvFile expCue = null, expGdi = null;
                        for (int i = 0; i < expectedMembers.Count; i++)
                        {
                            var e = expectedMembers[i];
                            if (e?.Name == null) continue;
                            if (e.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)) expCue = e;
                            if (e.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase)) expGdi = e;
                        }
                        if (expCue != null || expGdi != null)
                        {
                            var expByTrack = BuildExpectedTrackMapLocal(expectedMembers as System.Collections.Generic.List<RvFile> ?? new System.Collections.Generic.List<RvFile>(expectedMembers));
                            string descName = expGdi?.Name ?? expCue?.Name;
                            string descText = expGdi != null
                                ? ChdDescriptorGenerator.BuildGdi(cdTracks, expByTrack)
                                : ChdDescriptorGenerator.BuildCue(cdTracks, expByTrack);
                            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(descText ?? "");
                            ScannedFile dsf = new ScannedFile(FileType.File)
                            {
                                Name = descName,
                                FileModTimeStamp = System.IO.File.GetLastWriteTimeUtc(chdPath).ToFileTimeUtc(),
                                GotStatus = GotStatus.Got,
                                DeepScanned = true,
                                Size = (ulong)bytes.LongLength
                            };
                            using (var ms = new System.IO.MemoryStream(bytes, false))
                            {
                                scanner.CheckSumRead(ms, dsf, (ulong)bytes.LongLength, true, false, null, 0, 0);
                            }
                            lines.Add($"{dsf.Name} size={dsf.Size} crc={dsf.CRC?.ToHexString()} sha1={dsf.SHA1?.ToHexString()} md5={dsf.MD5?.ToHexString()}");
                            fileHashCache[dsf.Name] = ((ulong)bytes.LongLength, dsf.CRC, dsf.SHA1, dsf.MD5);
                            lines.Add("descriptorSource=synthetic");
                        }
                    }
                }
                else
                {
                    if (forceStreaming == true)
                    {
                        report = "verify failed: cd-stream metadata unavailable: " + metaErr;
                        return 3;
                    }
                    mode = "cd";
                }
            }
            else
            {
                for (int i = 0; i < extractedFiles.Count; i++)
                {
                    string f = extractedFiles[i];
                    FileInfo fi = new FileInfo(f);
                    ScannedFile sf = new ScannedFile(FileType.File)
                    {
                        Name = System.IO.Path.GetFileName(f),
                        FileModTimeStamp = fi.LastWriteTime.ToFileTimeUtc(),
                        GotStatus = GotStatus.Got,
                        DeepScanned = true,
                        Size = (ulong)fi.Length
                    };
                    using (Stream s = System.IO.File.OpenRead(f))
                    {
                        scanner.CheckSumRead(s, sf, (ulong)fi.Length, true, false, null, 0, 0);
                    }

                    lines.Add($"{sf.Name} size={sf.Size} crc={sf.CRC?.ToHexString()} sha1={sf.SHA1?.ToHexString()} md5={sf.MD5?.ToHexString()}");
                    fileHashCache[sf.Name] = ((ulong)fi.Length, sf.CRC, sf.SHA1, sf.MD5);
                }
                for (int i = 0; i < extractedFiles.Count; i++)
                {
                    string name = System.IO.Path.GetFileName(extractedFiles[i]);
                    if (name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add("descriptorSource=extracted");
                        break;
                    }
                }
            }

            if (expectedMembers != null && expectedMembers.Count > 0)
            {
                List<RvFile> expData = new List<RvFile>();
                foreach (var e in expectedMembers)
                {
                    if (e != null && e.IsFile)
                        expData.Add(e);
                }
                List<RvFile> expectedDataFiles = new List<RvFile>();
                foreach (var e in expData)
                {
                    if (IsDataName(e.Name))
                        expectedDataFiles.Add(e);
                }
                expectedDataFiles.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

                var mappingRes = BuildExactHashMappingLocal(fileHashCache, expectedDataFiles);
                lines.Add("");
                lines.Add("mapping:");
                foreach (var row in mappingRes)
                {
                    lines.Add($"{row.Expected} | {row.Extracted} | {row.Reason}");
                    mapping.Add(row);
                    if (!string.IsNullOrWhiteSpace(row.Expected) && !string.IsNullOrWhiteSpace(row.Extracted) && fileHashCache.TryGetValue(row.Extracted, out var h))
                        expectedHashes[row.Expected] = h;
                }
            }

            report = string.Join(Environment.NewLine, lines);
            modeUsed = mode;
            return 0;
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

    public static int Verify(string chdPath, string outputPath = null)
    {
        int rc = TryGenerateReport(chdPath, out string report);
        Write(outputPath, report);
        return rc;
    }

    public static int Health(string chdPath, string outputPath = null)
    {
        int rc = TryGenerateHealthReport(chdPath, out string report);
        Write(outputPath, report);
        return rc;
    }

    private static (int compared, int matched, int mismatched, int missing) CompareExpectedHashes(
        System.Collections.Generic.IReadOnlyList<RvFile> expectedMembers,
        Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> expectedHashes)
    {
        int compared = 0;
        int matched = 0;
        int mismatched = 0;
        int missing = 0;
        if (expectedMembers == null)
            return (0, 0, 0, 0);

        for (int i = 0; i < expectedMembers.Count; i++)
        {
            RvFile exp = expectedMembers[i];
            if (exp == null || !exp.IsFile || string.IsNullOrWhiteSpace(exp.Name))
                continue;

            bool hasConstraint = (exp.Size.HasValue && exp.Size.Value != 0) ||
                                 (exp.CRC != null && exp.CRC.Length > 0) ||
                                 (exp.SHA1 != null && exp.SHA1.Length > 0) ||
                                 (exp.MD5 != null && exp.MD5.Length > 0);
            if (!hasConstraint)
                continue;

            compared++;
            if (!expectedHashes.TryGetValue(exp.Name, out var got))
            {
                missing++;
                continue;
            }

            bool bad = false;
            if (exp.Size.HasValue && exp.Size.Value != 0 && got.size != exp.Size.Value) bad = true;
            if (exp.CRC != null && exp.CRC.Length > 0 && (got.crc == null || !ByteEq(exp.CRC, got.crc))) bad = true;
            if (exp.SHA1 != null && exp.SHA1.Length > 0 && (got.sha1 == null || !ByteEq(exp.SHA1, got.sha1))) bad = true;
            if (exp.MD5 != null && exp.MD5.Length > 0 && (got.md5 == null || !ByteEq(exp.MD5, got.md5))) bad = true;
            if (bad) mismatched++;
            else matched++;
        }

        return (compared, matched, mismatched, missing);
    }

    private static void Write(string outputPath, string text)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(text);
            return;
        }

        System.IO.File.WriteAllText(outputPath, text ?? "");
    }

    private static bool RunProcess(string exe, string args, string workingDir, out string output)
    {
        ChdmanRunResult result = ChdmanService.Run(exe, args, workingDir);
        output = result.Output;
        return result.Success;
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
        return ChdmanService.TryGetLogicalSize(chdmanExe, chdPath, workingDir);
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

    private static bool IsDataName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string ext = Path.GetExtension(name).ToLowerInvariant();
        return ext == ".bin" || ext == ".raw" || ext == ".iso" || ext == ".img" ||
               ext == ".hdd" || ext == ".hd" || ext == ".avi";
    }


    private static System.Collections.Generic.Dictionary<int, RvFile> BuildExpectedTrackMapLocal(System.Collections.Generic.List<RvFile> expectedChildren)
    {
        var map = new System.Collections.Generic.Dictionary<int, RvFile>();
        var patterns = new[]
        {
            new Regex(@"\(Track\s*(\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"track[\s_]*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"track(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        for (int i = 0; i < expectedChildren.Count; i++)
        {
            var f = expectedChildren[i];
            if (f?.Name == null) continue;
            int tno = -1;
            foreach (var r in patterns)
            {
                var m = r.Match(f.Name);
                if (m.Success && int.TryParse(m.Groups[1].Value, out tno))
                    break;
            }
            if (tno > 0 && !map.ContainsKey(tno))
                map.Add(tno, f);
        }
        return map;
    }

    private static System.Collections.Generic.List<MapRow> BuildExactHashMappingLocal(
        System.Collections.Generic.IDictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> fileHashCache,
        System.Collections.Generic.List<RvFile> expectedDataFiles)
    {
        var rows = new System.Collections.Generic.List<MapRow>();
        var usedExtracted = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extractedNames = new System.Collections.Generic.List<string>(fileHashCache.Keys);
        extractedNames.Sort(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < expectedDataFiles.Count; i++)
        {
            RvFile expected = expectedDataFiles[i];
            if (expected == null || string.IsNullOrWhiteSpace(expected.Name) || !HasExpectedHashLocal(expected))
                continue;

            for (int c = 0; c < extractedNames.Count; c++)
            {
                string extractedName = extractedNames[c];
                if (usedExtracted.Contains(extractedName))
                    continue;
                if (!fileHashCache.TryGetValue(extractedName, out var actual))
                    continue;
                if (!ExactHashesMatchLocal(expected, actual))
                    continue;

                rows.Add(new MapRow { Expected = expected.Name, Extracted = extractedName, Reason = "exact extracted payload hashes" });
                usedExtracted.Add(extractedName);
                break;
            }
        }
        return rows;
    }

    private static bool HasExpectedHashLocal(RvFile expected)
    {
        return expected != null &&
               ((expected.CRC != null && expected.CRC.Length > 0) ||
                (expected.SHA1 != null && expected.SHA1.Length > 0) ||
                (expected.MD5 != null && expected.MD5.Length > 0));
    }

    private static bool ExactHashesMatchLocal(
        RvFile expected,
        (ulong size, byte[] crc, byte[] sha1, byte[] md5) actual)
    {
        if (!HasExpectedHashLocal(expected))
            return false;
        if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != actual.size)
            return false;
        if (expected.CRC != null && expected.CRC.Length > 0 && (actual.crc == null || !ByteEq(expected.CRC, actual.crc)))
            return false;
        if (expected.SHA1 != null && expected.SHA1.Length > 0 && (actual.sha1 == null || !ByteEq(expected.SHA1, actual.sha1)))
            return false;
        if (expected.MD5 != null && expected.MD5.Length > 0 && (actual.md5 == null || !ByteEq(expected.MD5, actual.md5)))
            return false;
        return true;
    }

}
