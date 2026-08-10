using FileScanner;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace RomVaultCore.Utils;

public static class ChdExport
{
    public static int Export(string chdPath, string outputDir, IReadOnlyList<string> expectedMemberNames, out string report)
    {
        List<RvFile> expected = new List<RvFile>();
        foreach (string n in expectedMemberNames ?? Array.Empty<string>())
            expected.Add(new RvFile(FileType.File) { Name = n });
        return Export(chdPath, outputDir, expected, out report);
    }

    public static int Export(string chdPath, string outputDir, IReadOnlyList<RvFile> expectedMembers, out string report)
    {
        report = "";
        chdPath = NormalizeExistingPath(chdPath);
        if (string.IsNullOrWhiteSpace(chdPath) || !System.IO.File.Exists(chdPath))
        {
            report = "export failed: CHD not found";
            return 2;
        }
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            report = "export failed: output directory not specified";
            return 2;
        }

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            report = "export failed: cannot create output directory: " + ex.Message;
            return 2;
        }

        List<RvFile> expectedList = expectedMembers?.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Name)).ToList() ?? new List<RvFile>();
        if (expectedList.Count == 0)
        {
            report = "export failed: DAT members are required for verified CHD export";
            return 2;
        }
        bool expectsIso = expectedList.Any(n => n.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));
        bool expectsGdi = expectedList.Any(n => n.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase));
        string expectedIsoName = expectedList.FirstOrDefault(n => n.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))?.Name;
        string expectedCueName = expectedList.FirstOrDefault(n => n.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))?.Name;
        string expectedGdiName = expectedList.FirstOrDefault(n => n.Name.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))?.Name;
        string expectedTocName = expectedList.FirstOrDefault(n => n.Name.EndsWith(".toc", StringComparison.OrdinalIgnoreCase))?.Name;
        RvFile expectedSingle = !expectsIso && !expectsGdi && string.IsNullOrWhiteSpace(expectedCueName) && string.IsNullOrWhiteSpace(expectedTocName) && expectedList.Count == 1
            ? expectedList[0]
            : null;
        string singleFamily = null;
        if (expectedSingle != null &&
            ChdEncodingProfile.TryDescribeExisting(chdPath, false, ChdStorageProfile.Archive, out ChdEncodingProfileSpec describedProfile, out _, out _) &&
            (describedProfile.Family == "raw" || describedProfile.Family == "hdd" || describedProfile.Family == "laserdisc"))
            singleFamily = describedProfile.Family;

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
        baseTempDir = NormalizeDirectoryPath(baseTempDir);
        string tempDir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdexport." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string chdman = ChdmanProcessTracker.FindExecutable();
            IChdExtractor extractor = new ChdmanChdExtractor(chdman, tempDir);

            long? logicalSize = ChdmanService.TryGetLogicalSize(chdman, chdPath, tempDir);
            if (logicalSize.HasValue)
            {
                if (!ChdFreeSpace.TryGetAvailableBytes(tempDir, out long free, out string freeSpaceError))
                {
                    report = "export failed: " + freeSpaceError;
                    return 4;
                }
                if (free < logicalSize.Value + 256L * 1024 * 1024)
                {
                    report = $"export failed: insufficient free space. required={logicalSize.Value} free={free}";
                    return 4;
                }
            }

            List<string> exported = new List<string>();
            List<string> verifyErrors = new List<string>();
            if (!string.IsNullOrWhiteSpace(singleFamily))
            {
                string outputName = expectedSingle.Name;
                string temporary = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileName(outputName));
                bool extracted;
                string extractionError;
                switch (singleFamily)
                {
                    case "raw": extracted = extractor.ExtractRaw(chdPath, temporary, out extractionError); break;
                    case "hdd": extracted = extractor.ExtractHardDisk(chdPath, temporary, out extractionError); break;
                    default: extracted = extractor.ExtractLaserDisc(chdPath, temporary, out extractionError); break;
                }
                if (!extracted || !System.IO.File.Exists(temporary))
                {
                    report = $"export failed: {singleFamily} extraction: {extractionError}";
                    return 3;
                }
                if (!HasExpectedHash(expectedSingle))
                    verifyErrors.Add($"{outputName}: the DAT has no payload hash");
                else
                    VerifyFileAgainstExpected(temporary, expectedSingle, verifyErrors);
                if (verifyErrors.Count == 0)
                {
                    System.IO.File.Copy(temporary, ResolveOutputPath(outputDir, outputName), true);
                    exported.Add(outputName);
                }
                report = BuildExportReport(chdPath, outputDir, exported, verifyErrors);
                return verifyErrors.Count == 0 ? 0 : 5;
            }
            if (expectsIso && !expectsGdi && string.IsNullOrWhiteSpace(expectedCueName) && string.IsNullOrWhiteSpace(expectedTocName))
            {
                string destIsoName = string.IsNullOrWhiteSpace(expectedIsoName) ? "image.iso" : expectedIsoName;
                string destIso = ResolveOutputPath(outputDir, destIsoName);

                // Preservation exports always use chdman's canonical extractor;
                // logical streaming is intentionally limited to read-only scans.
                string outIso = System.IO.Path.Combine(tempDir, "image.iso");
                if (!extractor.ExtractDvd(chdPath, outIso, out string err))
                {
                    report = "export failed: extractdvd: " + err;
                    return 3;
                }
                if (!System.IO.File.Exists(outIso))
                {
                    report = "export failed: extractdvd produced no ISO";
                    return 3;
                }
                RvFile expectedIso = expectedList.FirstOrDefault(n => n.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));
                if (!HasExpectedHash(expectedIso))
                    verifyErrors.Add($"{destIsoName}: the DAT has no payload hash");
                else
                    VerifyFileAgainstExpected(outIso, expectedIso, verifyErrors);
                if (verifyErrors.Count == 0)
                {
                    System.IO.File.Copy(outIso, destIso, true);
                    exported.Add(destIsoName);
                }

                report = BuildExportReport(chdPath, outputDir, exported, verifyErrors);
                return verifyErrors.Count == 0 ? 0 : 5;
            }

            string outDescriptor = System.IO.Path.Combine(tempDir, expectsGdi ? "disc.gdi" : "disc.cue");
            if (!extractor.ExtractCd(chdPath, outDescriptor, out string errCd))
            {
                report = "export failed: extractcd: " + errCd;
                return 3;
            }
            if (!System.IO.File.Exists(outDescriptor))
            {
                report = "export failed: extractcd produced no descriptor";
                return 3;
            }

            List<(int trackNo, string fileName, string trackType)> tracks = expectsGdi ? ParseGdiTrackFiles(outDescriptor) : ParseCueTrackFiles(outDescriptor);
            if (!string.IsNullOrWhiteSpace(expectedTocName))
            {
                if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest tocManifest, out string tocManifestError))
                {
                    report = "export failed: TOC reconstruction metadata: " + tocManifestError;
                    return 3;
                }
                string nativeTocName = "rv-native-toc-payload.bin";
                string nativeTocPath = System.IO.Path.Combine(tempDir, nativeTocName);
                if (!ChdOpticalReconstruction.TryMaterializeSingleTocPayload(chdPath, tocManifest, nativeTocPath, out string tocError))
                {
                    report = "export failed: exact TOC payload: " + tocError;
                    return 3;
                }
                tracks.Clear();
                tracks.Add((tocManifest.Tracks[0].Number, nativeTocName, "TOC exact"));
            }
            HashSet<string> extractedNames = new HashSet<string>(tracks.Select(t => t.fileName).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
            foreach (string extra in extractedNames.ToArray())
            {
                string full = System.IO.Path.Combine(tempDir, extra);
                if (!System.IO.File.Exists(full))
                    extractedNames.Remove(extra);
            }

            FileScan scanner = new FileScan();
            Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> hashCache = new Dictionary<string, (ulong, byte[], byte[], byte[])>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in extractedNames)
            {
                string full = System.IO.Path.Combine(tempDir, name);
                FileInfo fi = new FileInfo(full);
                ScannedFile sf = new ScannedFile(FileType.File)
                {
                    Name = name,
                    FileModTimeStamp = fi.LastWriteTime.ToFileTimeUtc(),
                    GotStatus = GotStatus.Got,
                    DeepScanned = true,
                    Size = (ulong)fi.Length
                };
                using (Stream s = System.IO.File.OpenRead(full))
                {
                    scanner.CheckSumRead(s, sf, (ulong)fi.Length, true, false, null, 0, 0);
                }
                hashCache[name] = ((ulong)fi.Length, sf.CRC, sf.SHA1, sf.MD5);
            }

            List<RvFile> expectedData = expectedList.Where(e => IsPrimaryCdTrackDataFile(e.Name)).ToList();
            expectedData.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
            if (expectedData.Count == 0)
                verifyErrors.Add("the DAT contains no CHD payload tracks");
            foreach (RvFile expected in expectedData)
            {
                if (!HasExpectedHash(expected))
                    verifyErrors.Add($"{expected.Name}: the DAT has no payload hash");
            }

            Dictionary<int, string> extractedByTrack = new Dictionary<int, string>();
            foreach (var t in tracks)
            {
                if (!string.IsNullOrWhiteSpace(t.fileName) && extractedNames.Contains(t.fileName))
                    extractedByTrack[t.trackNo] = t.fileName;
            }

            Dictionary<int, string> expectedByTrack = BuildExpectedTrackNumberMap(expectedData.Select(e => e.Name).ToList());
            Dictionary<string, string> mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedExtracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Hash identity is authoritative. Track numbers only disambiguate
            // duplicate tracks; every mapping is hash-checked again below.
            foreach (RvFile expected in expectedData)
            {
                if (!HasExpectedHash(expected))
                    continue;
                List<string> matches = hashCache
                    .Where(kvp => !usedExtracted.Contains(kvp.Key) && HashesMatch(expected, kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();
                if (matches.Count != 1)
                    continue;
                mapping[expected.Name] = matches[0];
                usedExtracted.Add(matches[0]);
            }

            foreach (var kvp in expectedByTrack)
            {
                if (mapping.ContainsKey(kvp.Value))
                    continue;
                if (extractedByTrack.TryGetValue(kvp.Key, out string extName))
                {
                    if (usedExtracted.Contains(extName))
                        continue;
                    mapping[kvp.Value] = extName;
                    usedExtracted.Add(extName);
                }
            }

            List<(string source, string expectedName)> pendingCopies = new List<(string, string)>();
            foreach (var kvp in mapping)
            {
                string expectedName = kvp.Key;
                string extractedName = kvp.Value;
                if (string.IsNullOrWhiteSpace(expectedName) || string.IsNullOrWhiteSpace(extractedName))
                    continue;
                string src = System.IO.Path.Combine(tempDir, extractedName);
                if (!System.IO.File.Exists(src))
                    continue;
                pendingCopies.Add((src, expectedName));

                RvFile exp = expectedList.FirstOrDefault(e => string.Equals(e.Name, expectedName, StringComparison.OrdinalIgnoreCase));
                if (exp != null)
                    VerifyHashMatch(expectedName, exp, hashCache, extractedName, verifyErrors);
            }

            foreach (RvFile expected in expectedData)
            {
                if (!mapping.ContainsKey(expected.Name))
                    verifyErrors.Add($"{expected.Name}: no extracted track matched the DAT");
            }

            if (expectsIso)
            {
                if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest reconstruction, out string manifestError))
                {
                    verifyErrors.Add("ISO view: embedded reconstruction metadata is unavailable: " + manifestError);
                }
                else
                {
                    ChdManifestView isoView = ChdMultiView.GetIsoView(reconstruction);
                    if (isoView == null || reconstruction.Tracks.Count != 1)
                    {
                        verifyErrors.Add("ISO view: the CHD does not contain a proven reversible ISO representation");
                    }
                    else
                    {
                        List<RvFile> primaryMatches = expectedData.Where(item =>
                            ManifestTrackMatches(item, reconstruction.Tracks[0])).ToList();
                        string primaryExpectedName = primaryMatches.Count == 1 ? primaryMatches[0].Name : null;
                        if (string.IsNullOrWhiteSpace(primaryExpectedName) || !mapping.TryGetValue(primaryExpectedName, out string extractedPrimary))
                        {
                            verifyErrors.Add("ISO view: the primary extracted track could not be identified");
                        }
                        else
                        {
                            string isoTemp = System.IO.Path.Combine(tempDir, "view-" + System.IO.Path.GetFileName(expectedIsoName));
                            if (!ChdMultiView.TryMaterializeIsoView(System.IO.Path.Combine(tempDir, extractedPrimary), isoView, isoTemp, out string viewError))
                                verifyErrors.Add("ISO view: " + viewError);
                            else
                            {
                                RvFile expectedIso = expectedList.First(item => item.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));
                                VerifyFileAgainstExpected(isoTemp, expectedIso, verifyErrors);
                                pendingCopies.Add((isoTemp, expectedIso.Name));
                            }
                        }
                    }
                }
            }

            string destDescriptorName = expectsGdi ? expectedGdiName : !string.IsNullOrWhiteSpace(expectedTocName) ? expectedTocName : expectedCueName;
            string descriptorForCopy = null;
            if (verifyErrors.Count == 0 && !string.IsNullOrWhiteSpace(destDescriptorName))
            {
                bool expectsToc = !string.IsNullOrWhiteSpace(expectedTocName);
                descriptorForCopy = System.IO.Path.Combine(tempDir, expectsGdi ? "export.gdi" : expectsToc ? "export.toc" : "export.cue");
                bool usedEmbedded = TryWriteEmbeddedDescriptor(chdPath, descriptorForCopy, expectsGdi, expectsToc, expectedData, out string embeddedReason);
                if (!usedEmbedded)
                {
                    if (expectsToc)
                        verifyErrors.Add($"{destDescriptorName}: exact embedded TOC is unavailable: {embeddedReason}");
                    else if (!TryRewriteDescriptorFileNames(outDescriptor, descriptorForCopy, expectsGdi, mapping, out string descriptorError))
                        verifyErrors.Add($"{destDescriptorName}: {descriptorError}; embedded manifest: {embeddedReason}");
                }

                RvFile expectedDescriptor = expectedList.FirstOrDefault(e => string.Equals(e.Name, destDescriptorName, StringComparison.OrdinalIgnoreCase));
                // CUE/GDI are regenerated semantic views with the active DAT
                // filenames. Exact descriptor bytes (including old names,
                // whitespace and quoting) are intentionally not reconstructed.
                if (verifyErrors.Count == 0 && expectsToc && HasExpectedHash(expectedDescriptor))
                    VerifyFileAgainstExpected(descriptorForCopy, expectedDescriptor, verifyErrors);
            }

            List<RvFile> expectedAuxiliaries = expectedList.Where(item => item.Name.EndsWith(".sbi", StringComparison.OrdinalIgnoreCase)).ToList();
            if (expectedAuxiliaries.Count > 0)
            {
                if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest auxiliaryManifest, out string auxiliaryError))
                {
                    verifyErrors.Add("SBI: embedded reconstruction metadata is unavailable: " + auxiliaryError);
                }
                else
                {
                    for (int i = 0; i < expectedAuxiliaries.Count; i++)
                    {
                        RvFile expectedAuxiliary = expectedAuxiliaries[i];
                        ChdManifestAuxiliary auxiliary = auxiliaryManifest.Auxiliaries.FirstOrDefault(item =>
                            item != null &&
                            string.Equals(item.Role, "sbi-subchannel-correction", StringComparison.OrdinalIgnoreCase) &&
                            AuxiliaryMatches(expectedAuxiliary, item.Bytes));
                        if (auxiliary == null)
                        {
                            verifyErrors.Add(expectedAuxiliary.Name + ": exact embedded SBI payload is unavailable");
                            continue;
                        }
                        string auxiliaryPath = System.IO.Path.Combine(tempDir, "aux-" + i.ToString("D2") + ".sbi");
                        System.IO.File.WriteAllBytes(auxiliaryPath, auxiliary.Bytes ?? Array.Empty<byte>());
                        if (!HasExpectedHash(expectedAuxiliary))
                            verifyErrors.Add(expectedAuxiliary.Name + ": the DAT has no auxiliary hash");
                        else
                            VerifyFileAgainstExpected(auxiliaryPath, expectedAuxiliary, verifyErrors);
                        pendingCopies.Add((auxiliaryPath, expectedAuxiliary.Name));
                    }
                }
            }

            if (verifyErrors.Count == 0)
            {
                for (int i = 0; i < pendingCopies.Count; i++)
                {
                    string dst = ResolveOutputPath(outputDir, pendingCopies[i].expectedName);
                    System.IO.File.Copy(pendingCopies[i].source, dst, true);
                    exported.Add(pendingCopies[i].expectedName);
                }

                if (!string.IsNullOrWhiteSpace(destDescriptorName))
                {
                    string dst = ResolveOutputPath(outputDir, destDescriptorName);
                    System.IO.File.Copy(descriptorForCopy, dst, true);
                    exported.Add(destDescriptorName);
                }
            }

            exported.Sort(StringComparer.OrdinalIgnoreCase);
            report = BuildExportReport(chdPath, outputDir, exported, verifyErrors);
            return verifyErrors.Count == 0 ? 0 : 5;
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

    private static string BuildExportReport(string chdPath, string outputDir, List<string> files, List<string> verifyErrors)
    {
        List<string> lines = new List<string>();
        lines.Add("CHD export");
        lines.Add(ChdDiagnosticFormatter.RedactPath(chdPath));
        lines.Add("output=" + ChdDiagnosticFormatter.RedactPath(outputDir));
        lines.Add("");
        lines.Add("exported:");
        foreach (string f in files)
            lines.Add(f);
        lines.Add("");
        lines.Add("verify:");
        if (verifyErrors.Count == 0)
        {
            lines.Add("OK");
        }
        else
        {
            for (int i = 0; i < verifyErrors.Count; i++)
                lines.Add(verifyErrors[i]);
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static void VerifyHashMatch(string expectedName, RvFile expected, Dictionary<string, (ulong size, byte[] crc, byte[] sha1, byte[] md5)> extractedHashes, string extractedName, List<string> errors)
    {
        if (!extractedHashes.TryGetValue(extractedName, out var h))
        {
            errors.Add($"{expectedName}: extracted hashes unavailable");
            return;
        }
        if (expected.Size.HasValue && expected.Size.Value != 0 && h.size != expected.Size.Value)
            errors.Add($"{expectedName}: size mismatch");
        if (expected.CRC != null && (h.crc == null || !expected.CRC.SequenceEqual(h.crc)))
            errors.Add($"{expectedName}: crc mismatch");
        if (expected.SHA1 != null && (h.sha1 == null || !expected.SHA1.SequenceEqual(h.sha1)))
            errors.Add($"{expectedName}: sha1 mismatch");
        if (expected.MD5 != null && (h.md5 == null || !expected.MD5.SequenceEqual(h.md5)))
            errors.Add($"{expectedName}: md5 mismatch");
    }

    private static bool TryRewriteDescriptorFileNames(
        string sourceDescriptor,
        string destinationDescriptor,
        bool isGdi,
        IReadOnlyDictionary<string, string> expectedToExtracted,
        out string error)
    {
        error = "";
        try
        {
            Dictionary<string, string> extractedToExpected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in expectedToExtracted)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    continue;
                if (extractedToExpected.ContainsKey(pair.Value))
                {
                    error = $"multiple DAT members map to extracted file '{pair.Value}'";
                    return false;
                }
                extractedToExpected.Add(pair.Value, pair.Key);
            }

            string[] lines = System.IO.File.ReadAllLines(sourceDescriptor);
            Regex fileLine = isGdi
                ? new Regex(@"^(?<prefix>\s*\d+\s+\d+\s+\d+\s+\d+\s+)(?:""(?<quoted>[^""]+)""|(?<plain>\S+))(?<suffix>\s+\d+\s*)$", RegexOptions.IgnoreCase)
                : new Regex(@"^(?<prefix>\s*FILE\s+)(?:""(?<quoted>[^""]+)""|(?<plain>\S+))(?<suffix>\s+.+)$", RegexOptions.IgnoreCase);

            int rewritten = 0;
            int startLine = isGdi ? 1 : 0;
            for (int i = startLine; i < lines.Length; i++)
            {
                Match match = fileLine.Match(lines[i]);
                if (!match.Success)
                    continue;

                string extractedName = match.Groups["quoted"].Success
                    ? match.Groups["quoted"].Value
                    : match.Groups["plain"].Value;
                if (!extractedToExpected.TryGetValue(extractedName, out string expectedName))
                {
                    error = $"descriptor references unmapped extracted file '{extractedName}'";
                    return false;
                }
                if (expectedName.IndexOfAny(new[] { '\r', '\n', '"' }) >= 0)
                {
                    error = $"DAT member name cannot be represented in the descriptor: '{expectedName}'";
                    return false;
                }

                string descriptorName = expectedName.Replace('\\', '/');
                bool quoteName = !isGdi || descriptorName.Any(char.IsWhiteSpace);
                lines[i] = match.Groups["prefix"].Value +
                           (quoteName ? "\"" + descriptorName + "\"" : descriptorName) +
                           match.Groups["suffix"].Value;
                rewritten++;
            }

            if (rewritten == 0)
            {
                error = "extracted descriptor contains no track file references";
                return false;
            }

            System.IO.File.WriteAllLines(destinationDescriptor, lines, new System.Text.UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            error = "could not rewrite extracted descriptor: " + ex.Message;
            return false;
        }
    }

    private static bool TryWriteEmbeddedDescriptor(
        string chdPath,
        string destinationDescriptor,
        bool expectsGdi,
        bool expectsToc,
        IReadOnlyList<RvFile> expectedData,
        out string reason)
    {
        reason = "RVRM intentionally contains no descriptor filenames or source descriptor bytes";
        return false;
    }

    private static bool ManifestTrackMatches(RvFile expected, ChdManifestTrack track)
    {
        if (expected == null || track == null)
            return false;
        if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != (ulong)Math.Max(0, track.Size))
            return false;
        if (expected.CRC != null && (track.Crc32 == null || !expected.CRC.SequenceEqual(track.Crc32)))
            return false;
        if (expected.SHA1 != null && (track.Sha1 == null || !expected.SHA1.SequenceEqual(track.Sha1)))
            return false;
        if (expected.MD5 != null && (track.Md5 == null || !expected.MD5.SequenceEqual(track.Md5)))
            return false;
        return HasExpectedHash(expected);
    }

    private static bool AuxiliaryMatches(RvFile expected, byte[] bytes)
    {
        if (expected == null || bytes == null || !HasExpectedHash(expected))
            return false;
        if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != (ulong)bytes.LongLength)
            return false;
        if (expected.CRC != null && !expected.CRC.SequenceEqual(ComputeCrc32(bytes)))
            return false;
        if (expected.SHA1 != null)
        {
            using SHA1 sha1 = SHA1.Create();
            if (!expected.SHA1.SequenceEqual(sha1.ComputeHash(bytes)))
                return false;
        }
        if (expected.MD5 != null)
        {
            using MD5 md5 = MD5.Create();
            if (!expected.MD5.SequenceEqual(md5.ComputeHash(bytes)))
                return false;
        }
        return true;
    }

    private static byte[] ComputeCrc32(byte[] bytes)
    {
        uint crc = 0xffffffff;
        for (int i = 0; i < bytes.Length; i++)
        {
            crc ^= bytes[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        }
        crc ^= 0xffffffff;
        return new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc };
    }

    private static string NormalizeMemberName(string name)
    {
        return (name ?? "").Replace('\\', '/').TrimStart('.', '/');
    }

    private static bool HasExpectedHash(RvFile expected)
    {
        return expected != null &&
               ((expected.SHA1 != null && expected.SHA1.Length > 0) ||
                (expected.MD5 != null && expected.MD5.Length > 0) ||
                (expected.CRC != null && expected.CRC.Length > 0));
    }

    private static bool HashesMatch(RvFile expected, (ulong size, byte[] crc, byte[] sha1, byte[] md5) actual)
    {
        if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != actual.size)
            return false;
        if (expected.SHA1 != null && (actual.sha1 == null || !expected.SHA1.SequenceEqual(actual.sha1)))
            return false;
        if (expected.MD5 != null && (actual.md5 == null || !expected.MD5.SequenceEqual(actual.md5)))
            return false;
        if (expected.CRC != null && (actual.crc == null || !expected.CRC.SequenceEqual(actual.crc)))
            return false;
        return HasExpectedHash(expected);
    }

    private static void VerifyFileAgainstExpected(string filePath, RvFile expected, List<string> errors)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                errors.Add($"{expected.Name}: missing after export");
                return;
            }
            FileInfo fi = new FileInfo(filePath);
            FileScan scanner = new FileScan();
            ScannedFile sf = new ScannedFile(FileType.File)
            {
                Name = expected.Name,
                FileModTimeStamp = fi.LastWriteTime.ToFileTimeUtc(),
                GotStatus = GotStatus.Got,
                DeepScanned = true,
                Size = (ulong)fi.Length
            };
            using (Stream s = System.IO.File.OpenRead(filePath))
            {
                scanner.CheckSumRead(s, sf, (ulong)fi.Length, true, false, null, 0, 0);
            }

            if (expected.Size.HasValue && expected.Size.Value != 0 && (ulong)fi.Length != expected.Size.Value)
                errors.Add($"{expected.Name}: size mismatch");
            if (expected.CRC != null && (sf.CRC == null || !expected.CRC.SequenceEqual(sf.CRC)))
                errors.Add($"{expected.Name}: crc mismatch");
            if (expected.SHA1 != null && (sf.SHA1 == null || !expected.SHA1.SequenceEqual(sf.SHA1)))
                errors.Add($"{expected.Name}: sha1 mismatch");
            if (expected.MD5 != null && (sf.MD5 == null || !expected.MD5.SequenceEqual(sf.MD5)))
                errors.Add($"{expected.Name}: md5 mismatch");
        }
        catch (Exception ex)
        {
            errors.Add($"{expected.Name}: verify failed: {ex.Message}");
        }
    }

    private static bool IsPrimaryCdTrackDataFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext == ".bin" || ext == ".raw";
    }


    private static Dictionary<int, string> BuildExpectedTrackNumberMap(List<string> expectedNames)
    {
        Dictionary<int, string> map = new Dictionary<int, string>();
        System.Text.RegularExpressions.Regex[] patterns = new[]
        {
            new System.Text.RegularExpressions.Regex(@"\(Track\s*(\d+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled),
            new System.Text.RegularExpressions.Regex(@"track[\s_]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled),
            new System.Text.RegularExpressions.Regex(@"track(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled)
        };
        foreach (string name in expectedNames)
        {
            foreach (var r in patterns)
            {
                var m = r.Match(name);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int tno))
                {
                    if (!map.ContainsKey(tno))
                        map.Add(tno, name);
                    break;
                }
            }
        }
        return map;
    }

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

    private static string NormalizeExistingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        string p = path.Trim().Trim('"');
        if (System.IO.Path.IsPathRooted(p))
            return p;

        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, p));
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
        }

        try
        {
            string candidate = System.IO.Path.GetFullPath(p);
            if (System.IO.File.Exists(candidate))
                return candidate;
        }
        catch
        {
        }

        return p;
    }

    private static string NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        string p = path.Trim().Trim('"');
        if (System.IO.Path.IsPathRooted(p))
            return p;

        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, p));
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
        }

        return p;
    }

    private static string ResolveOutputPath(string outputDirectory, string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            throw new InvalidDataException("CHD member name is empty.");

        string root = System.IO.Path.GetFullPath(outputDirectory)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) +
            System.IO.Path.DirectorySeparatorChar;
        string relativeName = memberName.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
        if (System.IO.Path.IsPathRooted(relativeName))
            throw new InvalidDataException("CHD member path must be relative: " + memberName);

        string destination = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativeName));
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CHD member path escapes the output directory: " + memberName);

        string parent = System.IO.Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
        return destination;
    }
}
