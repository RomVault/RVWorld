using CHDSharpLib;
using RomVaultCore.Scanner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace RomVaultCore.Utils;

internal sealed class ChdOpticalLayoutSnapshot
{
    public bool IsGdRom { get; set; }
    public List<ChdCdTrackInfo> Tracks { get; set; } = new List<ChdCdTrackInfo>();
}

/// <summary>
/// Independently materializes every payload bound by RVRM. A matching CHD raw
/// SHA-1 is not enough for optical media because metadata can change how the
/// same logical bytes are split or interpreted.
/// </summary>
internal static class ChdBoundPayloadValidator
{
    public static bool TryCaptureOpticalLayout(string chdPath, ChdReconstructionManifest manifest,
        out ChdOpticalLayoutSnapshot snapshot, out string error)
    {
        snapshot = null;
        error = "";
        string family = (manifest?.Family ?? "").Trim().ToLowerInvariant();
        if (family != "cd" && family != "gdi")
            return true;
        if (!ChdMetadata.TryReadCdTrackLayout(chdPath, out List<ChdCdTrackInfo> tracks, out bool isGdRom, out string layoutError) ||
            tracks == null || tracks.Count == 0)
            return Fail("Could not capture the source optical layout before conversion: " + layoutError, out error);
        if ((family == "gdi") != isGdRom)
            return Fail("The source RVRM media family does not match its native CD/GD metadata.", out error);
        snapshot = new ChdOpticalLayoutSnapshot { IsGdRom = isGdRom, Tracks = CloneTracks(tracks) };
        return TryValidateLayoutShape(snapshot, out error);
    }

    public static bool TryValidate(string chdPath, string chdmanPath, string workingRoot,
        ChdReconstructionManifest expectedManifest, ChdOpticalLayoutSnapshot expectedOpticalLayout, out string error)
    {
        error = "";
        if (expectedManifest == null)
            return Fail("The expected reconstruction manifest is missing.", out error);
        if (!ChdmanService.TryValidateExternalPaths(out error, chdPath))
            return false;
        if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest actualManifest, out string manifestError))
            return Fail("Could not read the candidate RVRM before payload validation: " + manifestError, out error);
        if (!RvrmWireFormat.CanonicallyEquals(expectedManifest, actualManifest, out string identityError))
            return Fail("The candidate RVRM changed before payload validation: " + identityError, out error);
        if (!ChdMetadata.TryReadContainerInfo(chdPath, out ChdContainerInfo container, out string containerError))
            return Fail("Could not size the independent payload-validation workspace: " + containerError, out error);
        if (!TryValidateCandidateOpticalLayout(chdPath, expectedManifest, expectedOpticalLayout, out error))
            return false;

        long requiredBytes = EstimateWorkspaceBytes(container, expectedManifest);
        if (!ChdTemporaryWorkspace.TryCreateForRoot(ResolveWorkspaceRoot(chdPath, workingRoot),
                ChdWorkspacePurpose.Verify, requiredBytes, out string workspace, out error))
        {
            error = "Could not create a writable workspace for independent RVRM payload validation: " + error;
            return false;
        }

        bool succeeded = false;
        try
        {
            if (!TryExtractPrimaryPayloads(chdPath, chdmanPath, workspace, expectedManifest, expectedOpticalLayout,
                    out List<string> primaryPaths, out Dictionary<int, string> opticalBindings, out error))
                throw new InvalidDataException(error);
            if (!TryMatchPayloads(expectedManifest.Tracks, primaryPaths, "primary",
                    opticalBindings, out Dictionary<ChdManifestTrack, string> primaryMatches, out error))
                throw new InvalidDataException(error);

            List<ChdManifestView> views = expectedManifest.Views ?? new List<ChdManifestView>();
            for (int i = 0; i < views.Count; i++)
            {
                ChdManifestView view = views[i];
                if (view == null || expectedManifest.Tracks == null || expectedManifest.Tracks.Count != 1 ||
                    !primaryMatches.TryGetValue(expectedManifest.Tracks[0], out string primaryPath))
                    throw new InvalidDataException("Alternate view " + (i + 1) + " has no unambiguous primary payload to reconstruct.");

                string viewPath = Path.Combine(workspace, "alternate-view-" + (i + 1).ToString("D4") + ".iso");
                if (!ChdMultiView.TryMaterializeIsoView(primaryPath, view, viewPath, out string viewError))
                    throw new InvalidDataException("Alternate view " + (i + 1) + " could not be reconstructed: " + viewError);
                if (!TryMatchPayloads(view.Tracks, new List<string> { viewPath }, "alternate view " + (i + 1), null, out _, out error))
                    throw new InvalidDataException(error);
            }

            if (!TryCompareAuxiliaries(expectedManifest.Auxiliaries, actualManifest.Auxiliaries, out error))
                throw new InvalidDataException(error);
            succeeded = true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (!ChdTemporaryWorkspace.TryDelete(workspace, out string cleanupError))
        {
            error = (succeeded ? "Independent RVRM payload validation completed, but" : error + " Cleanup also failed because") +
                    " the validation workspace could not be removed: " + cleanupError;
            return false;
        }
        return succeeded;
    }

    internal static bool RunSelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-bound-payload-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            byte[] payload = new byte[8193];
            for (int i = 0; i < payload.Length; i++) payload[i] = unchecked((byte)(i * 47 + 3));
            string first = Path.Combine(root, "first.bin");
            string second = Path.Combine(root, "second.bin");
            File.WriteAllBytes(first, payload);
            File.WriteAllBytes(second, payload);
            PayloadDigest digest = HashFile(first);
            List<ChdManifestTrack> expected = new List<ChdManifestTrack> { ToTrack(1, digest), ToTrack(2, digest) };
            if (!TryMatchPayloads(expected, new List<string> { first, second }, "self-test", null, out _, out error))
                return false;
            if (TryMatchPayloads(expected, new List<string> { first, second, first }, "self-test", null, out _, out _))
                throw new InvalidDataException("An unbound reconstructed payload was accepted.");

            string third = Path.Combine(root, "third.bin");
            byte[] thirdBytes = payload.Select(value => unchecked((byte)(value ^ 0x5a))).ToArray();
            File.WriteAllBytes(third, thirdBytes);
            List<ChdManifestTrack> numberedExpected = new List<ChdManifestTrack>
            {
                ToTrack(1, HashFile(first)),
                ToTrack(2, HashFile(third))
            };
            Dictionary<int, string> correctBindings = new Dictionary<int, string> { [1] = first, [2] = third };
            if (!TryMatchPayloads(numberedExpected, new List<string> { first, third }, "numbered self-test",
                    correctBindings, out _, out error))
                return false;
            Dictionary<int, string> swappedBindings = new Dictionary<int, string> { [1] = third, [2] = first };
            if (TryMatchPayloads(numberedExpected, new List<string> { first, third }, "numbered self-test",
                    swappedBindings, out _, out _))
                throw new InvalidDataException("Swapped descriptor track payloads passed numbered identity validation.");

            ChdOpticalLayoutSnapshot layout = CreateLayoutFixture();
            ChdOpticalLayoutSnapshot identicalLayout = new ChdOpticalLayoutSnapshot
            {
                IsGdRom = layout.IsGdRom,
                Tracks = CloneTracks(layout.Tracks)
            };
            if (!TryCompareOpticalLayouts(layout, identicalLayout, out error))
                return false;
            ChdOpticalLayoutSnapshot swappedLayout = new ChdOpticalLayoutSnapshot
            {
                IsGdRom = false,
                Tracks = new List<ChdCdTrackInfo> { CloneTracks(layout.Tracks)[1], CloneTracks(layout.Tracks)[0] }
            };
            if (TryCompareOpticalLayouts(layout, swappedLayout, out _))
                throw new InvalidDataException("Swapped native optical track order passed layout validation.");
            ChdOpticalLayoutSnapshot changedType = new ChdOpticalLayoutSnapshot { IsGdRom = false, Tracks = CloneTracks(layout.Tracks) };
            changedType.Tracks[0].TrackType = "AUDIO";
            if (TryCompareOpticalLayouts(layout, changedType, out _))
                throw new InvalidDataException("Changed native optical track type passed layout validation.");
            ChdOpticalLayoutSnapshot changedGap = new ChdOpticalLayoutSnapshot { IsGdRom = false, Tracks = CloneTracks(layout.Tracks) };
            changedGap.Tracks[0].PreGapFrames++;
            if (TryCompareOpticalLayouts(layout, changedGap, out _))
                throw new InvalidDataException("Changed native optical gap/index semantics passed layout validation.");
            ChdOpticalLayoutSnapshot changedPadding = new ChdOpticalLayoutSnapshot { IsGdRom = false, Tracks = CloneTracks(layout.Tracks) };
            changedPadding.Tracks[1].PadFrames++;
            if (TryCompareOpticalLayouts(layout, changedPadding, out _))
                throw new InvalidDataException("Changed native GD padding semantics passed layout validation.");

            string cueTrack1 = Path.Combine(root, "track (Track 01).bin");
            string cueTrack2 = Path.Combine(root, "track (Track 02).bin");
            File.Copy(first, cueTrack1);
            File.Copy(third, cueTrack2);
            string cue = Path.Combine(root, "validation.cue");
            string cueText =
                "FILE \"track (Track 01).bin\" BINARY\r\n" +
                "  TRACK 01 MODE1/2352\r\n" +
                "    INDEX 00 00:00:00\r\n" +
                "    INDEX 01 00:02:00\r\n" +
                "    POSTGAP 00:01:00\r\n" +
                "FILE \"track (Track 02).bin\" BINARY\r\n" +
                "  TRACK 02 AUDIO\r\n" +
                "    INDEX 01 00:00:00\r\n";
            File.WriteAllText(cue, cueText);
            if (!TryBindExtractedDescriptor(cue, root, layout, out _, out error))
                return false;
            File.WriteAllText(cue, cueText.Replace("TRACK 01 MODE1/2352", "TRACK 01 AUDIO"));
            if (TryBindExtractedDescriptor(cue, root, layout, out _, out _))
                throw new InvalidDataException("Changed extracted CUE track type passed descriptor validation.");
            File.WriteAllText(cue, cueText.Replace("INDEX 01 00:02:00", "INDEX 01 00:02:01"));
            if (TryBindExtractedDescriptor(cue, root, layout, out _, out _))
                throw new InvalidDataException("Changed extracted CUE index/gap passed descriptor validation.");

            payload[payload.Length - 1] ^= 0xff;
            File.WriteAllBytes(second, payload);
            if (TryMatchPayloads(expected, new List<string> { first, second }, "self-test", null, out _, out _))
                throw new InvalidDataException("A changed bound payload passed the fixed-hash comparison.");

            byte[] auxiliaryBytes = new byte[] { 0x53, 0x42, 0x49, 0x00, 0x01, 0x02 };
            List<ChdManifestAuxiliary> expectedAuxiliaries = new List<ChdManifestAuxiliary>
            {
                new ChdManifestAuxiliary { Role = "sbi-subchannel-correction", Bytes = (byte[])auxiliaryBytes.Clone() }
            };
            List<ChdManifestAuxiliary> actualAuxiliaries = new List<ChdManifestAuxiliary>
            {
                new ChdManifestAuxiliary { Role = "sbi-subchannel-correction", Bytes = (byte[])auxiliaryBytes.Clone() }
            };
            if (!TryCompareAuxiliaries(expectedAuxiliaries, actualAuxiliaries, out error))
                return false;
            actualAuxiliaries[0].Bytes[0] ^= 0xff;
            if (TryCompareAuxiliaries(expectedAuxiliaries, actualAuxiliaries, out _))
                throw new InvalidDataException("Changed auxiliary reconstruction bytes passed the fixed-hash comparison.");

            expected[0].Md5 = Array.Empty<byte>();
            if (TryMatchPayloads(expected, new List<string> { first, first }, "self-test", null, out _, out _))
                throw new InvalidDataException("An incomplete fixed hash set was accepted.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static bool TryValidateCandidateOpticalLayout(string chdPath, ChdReconstructionManifest manifest,
        ChdOpticalLayoutSnapshot expected, out string error)
    {
        error = "";
        string family = (manifest?.Family ?? "").Trim().ToLowerInvariant();
        bool optical = family == "cd" || family == "gdi";
        if (!optical)
            return expected == null || Fail("A non-optical RVRM was paired with an optical layout snapshot.", out error);
        if (expected == null)
            return Fail("The source optical layout snapshot is missing; conversion cannot prove track semantics.", out error);
        if (!ChdMetadata.TryReadCdTrackLayout(chdPath, out List<ChdCdTrackInfo> tracks, out bool isGdRom, out string layoutError))
            return Fail("Could not read the candidate optical layout: " + layoutError, out error);
        ChdOpticalLayoutSnapshot actual = new ChdOpticalLayoutSnapshot { IsGdRom = isGdRom, Tracks = CloneTracks(tracks) };
        return TryCompareOpticalLayouts(expected, actual, out error);
    }

    private static bool TryCompareOpticalLayouts(ChdOpticalLayoutSnapshot expected,
        ChdOpticalLayoutSnapshot actual, out string error)
    {
        error = "";
        if (!TryValidateLayoutShape(expected, out error) || !TryValidateLayoutShape(actual, out error))
            return false;
        if (expected.IsGdRom != actual.IsGdRom)
            return Fail("The candidate changed native CD/GD media classification.", out error);
        if (expected.Tracks.Count != actual.Tracks.Count)
            return Fail("The candidate changed the native optical track count.", out error);
        for (int i = 0; i < expected.Tracks.Count; i++)
        {
            ChdCdTrackInfo left = expected.Tracks[i];
            ChdCdTrackInfo right = actual.Tracks[i];
            if (left.TrackNo != right.TrackNo)
                return Fail("The candidate changed native optical track order or numbering at position " + (i + 1) + ".", out error);
            if (!SemanticStringEquals(left.TrackType, right.TrackType) ||
                !SemanticStringEquals(left.TrackSubtype, right.TrackSubtype) ||
                left.SectorSize != right.SectorSize || left.SubcodeSize != right.SubcodeSize)
                return Fail("The candidate changed native type, subtype, sector, or subchannel semantics for track " + left.TrackNo + ".", out error);
            if (left.StartFrame != right.StartFrame || left.Frames != right.Frames || left.PadFrames != right.PadFrames)
                return Fail("The candidate changed native start, frame-count, or GD padding semantics for track " + left.TrackNo + ".", out error);
            if (left.PreGapFrames != right.PreGapFrames || left.PreGapDataStored != right.PreGapDataStored ||
                !SemanticStringEquals(left.PreGapTrackType, right.PreGapTrackType) ||
                !SemanticStringEquals(left.PreGapSubtype, right.PreGapSubtype) ||
                left.PostGapFrames != right.PostGapFrames)
                return Fail("The candidate changed native index, pregap, or postgap semantics for track " + left.TrackNo + ".", out error);
        }
        return true;
    }

    private static bool TryValidateLayoutShape(ChdOpticalLayoutSnapshot snapshot, out string error)
    {
        error = "";
        if (snapshot?.Tracks == null || snapshot.Tracks.Count == 0)
            return Fail("The optical layout snapshot is empty.", out error);
        int previousTrackNo = 0;
        long expectedStart = 0;
        for (int i = 0; i < snapshot.Tracks.Count; i++)
        {
            ChdCdTrackInfo track = snapshot.Tracks[i];
            if (track == null || track.TrackNo <= previousTrackNo || track.StartFrame != expectedStart ||
                track.Frames <= 0 || track.PreGapFrames < 0 || track.PostGapFrames < 0 || track.PadFrames < 0 ||
                track.PadFrames > track.Frames || track.SectorSize <= 0 || track.SubcodeSize < 0)
                return Fail("The optical layout snapshot has invalid ordering, frame, gap, padding, or sector data.", out error);
            previousTrackNo = track.TrackNo;
            if (expectedStart > long.MaxValue - track.Frames)
                return Fail("The optical layout snapshot frame range overflows.", out error);
            expectedStart += track.Frames;
        }
        return true;
    }

    private static bool TryBindExtractedDescriptor(string descriptorPath, string workspace,
        ChdOpticalLayoutSnapshot expectedLayout, out Dictionary<int, string> bindings, out string error)
    {
        bindings = new Dictionary<int, string>();
        error = "";
        if (expectedLayout == null || !TryValidateLayoutShape(expectedLayout, out error))
            return false;
        string extension = Path.GetExtension(descriptorPath).ToLowerInvariant();
        if (extension == ".gdi")
            return TryBindGdiDescriptor(descriptorPath, workspace, expectedLayout, bindings, out error);
        if (extension == ".cue")
            return TryBindCueDescriptor(descriptorPath, workspace, expectedLayout, bindings, out error);
        return Fail("Unsupported extracted optical descriptor: " + extension + ".", out error);
    }

    private static bool TryBindGdiDescriptor(string descriptorPath, string workspace,
        ChdOpticalLayoutSnapshot layout, Dictionary<int, string> bindings, out string error)
    {
        error = "";
        string[] lines = File.ReadAllLines(descriptorPath);
        List<string> content = lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (content.Count == 0 || !int.TryParse(content[0], out int declaredCount) ||
            declaredCount != layout.Tracks.Count || content.Count != declaredCount + 1)
            return Fail("The extracted GDI descriptor has an invalid track count.", out error);
        Regex linePattern = new Regex(@"^(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(?:""([^""]+)""|(\S+))\s+(\d+)\s*$", RegexOptions.CultureInvariant);
        for (int i = 0; i < layout.Tracks.Count; i++)
        {
            Match match = linePattern.Match(content[i + 1]);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int number) ||
                !long.TryParse(match.Groups[2].Value, out long lba) || !int.TryParse(match.Groups[3].Value, out int mode) ||
                !int.TryParse(match.Groups[4].Value, out int sector) || !long.TryParse(match.Groups[7].Value, out long offset))
                return Fail("The extracted GDI descriptor has a malformed track row.", out error);
            ChdCdTrackInfo expected = layout.Tracks[i];
            if (number != expected.TrackNo || lba != expected.StartFrame || mode != ExpectedGdiMode(expected) ||
                sector != expected.SectorSize || offset != 0)
                return Fail("The extracted GDI descriptor changed track order, LBA, type, sector size, or offset for track " + expected.TrackNo + ".", out error);
            string name = match.Groups[5].Success ? match.Groups[5].Value : match.Groups[6].Value;
            if (!TryResolveDescriptorPayload(workspace, name, out string payloadPath, out error) ||
                bindings.ContainsKey(number) || bindings.Values.Contains(payloadPath, StringComparer.OrdinalIgnoreCase))
                return false;
            bindings.Add(number, payloadPath);
        }
        return true;
    }

    private static bool TryBindCueDescriptor(string descriptorPath, string workspace,
        ChdOpticalLayoutSnapshot layout, Dictionary<int, string> bindings, out string error)
    {
        error = "";
        List<CueTrackBinding> parsed = new List<CueTrackBinding>();
        string currentFile = null;
        CueTrackBinding currentTrack = null;
        string[] lines = File.ReadAllLines(descriptorPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            Match fileMatch = Regex.Match(line, @"^\s*FILE\s+(?:""([^""]+)""|(\S+))\s+\S+\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (fileMatch.Success)
            {
                currentFile = fileMatch.Groups[1].Success ? fileMatch.Groups[1].Value : fileMatch.Groups[2].Value;
                continue;
            }
            Match trackMatch = Regex.Match(line, @"^\s*TRACK\s+(\d+)\s+(\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (trackMatch.Success)
            {
                if (string.IsNullOrWhiteSpace(currentFile) || !int.TryParse(trackMatch.Groups[1].Value, out int number))
                    return Fail("The extracted CUE descriptor has an unbound or malformed TRACK row.", out error);
                currentTrack = new CueTrackBinding { Number = number, Type = trackMatch.Groups[2].Value, FileName = currentFile };
                parsed.Add(currentTrack);
                continue;
            }
            Match indexMatch = Regex.Match(line, @"^\s*INDEX\s+(\d+)\s+(\d+:\d+:\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (indexMatch.Success)
            {
                if (currentTrack == null)
                    return Fail("The extracted CUE descriptor has INDEX data outside a track.", out error);
                if (!int.TryParse(indexMatch.Groups[1].Value, out int indexNumber) ||
                    !TryParseCueFrames(indexMatch.Groups[2].Value, out long frames) ||
                    currentTrack.Indexes.ContainsKey(indexNumber))
                    return Fail("The extracted CUE descriptor has malformed or duplicate INDEX data.", out error);
                currentTrack.Indexes.Add(indexNumber, frames);
                continue;
            }
            Match gapMatch = Regex.Match(line, @"^\s*(PREGAP|POSTGAP)\s+(\d+:\d+:\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (gapMatch.Success)
            {
                if (currentTrack == null)
                    return Fail("The extracted CUE descriptor has gap data outside a track.", out error);
                if (!TryParseCueFrames(gapMatch.Groups[2].Value, out long frames))
                    return Fail("The extracted CUE descriptor has malformed gap data.", out error);
                if (gapMatch.Groups[1].Value.Equals("PREGAP", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentTrack.PreGap.HasValue) return Fail("The extracted CUE descriptor has duplicate PREGAP data.", out error);
                    currentTrack.PreGap = frames;
                }
                else
                {
                    if (currentTrack.PostGap.HasValue) return Fail("The extracted CUE descriptor has duplicate POSTGAP data.", out error);
                    currentTrack.PostGap = frames;
                }
                continue;
            }
            string semantic = line.TrimStart();
            if (semantic.StartsWith("FILE", StringComparison.OrdinalIgnoreCase) ||
                semantic.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase) ||
                semantic.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase) ||
                semantic.StartsWith("PREGAP", StringComparison.OrdinalIgnoreCase) ||
                semantic.StartsWith("POSTGAP", StringComparison.OrdinalIgnoreCase))
                return Fail("The extracted CUE descriptor contains a malformed semantic directive.", out error);
        }
        if (parsed.Count != layout.Tracks.Count)
            return Fail("The extracted CUE descriptor track count changed.", out error);
        for (int i = 0; i < parsed.Count; i++)
        {
            CueTrackBinding actual = parsed[i];
            ChdCdTrackInfo expected = layout.Tracks[i];
            if (actual.Number != expected.TrackNo || !string.Equals(actual.Type, ExpectedCueType(expected), StringComparison.OrdinalIgnoreCase))
                return Fail("The extracted CUE descriptor changed track order, number, type, or sector semantics for track " + expected.TrackNo + ".", out error);
            bool indexShapeMatches = actual.Indexes.TryGetValue(1, out long index1) &&
                (expected.PreGapDataStored
                    ? actual.Indexes.TryGetValue(0, out long index0) && index0 == 0 && index1 == expected.PreGapFrames && !actual.PreGap.HasValue
                    : !actual.Indexes.ContainsKey(0) && index1 == 0 && (actual.PreGap ?? 0) == expected.PreGapFrames);
            if (!indexShapeMatches || actual.Indexes.Keys.Any(number => number != 0 && number != 1) ||
                (actual.PostGap ?? 0) != expected.PostGapFrames)
                return Fail("The extracted CUE descriptor changed INDEX, PREGAP, or POSTGAP semantics for track " + expected.TrackNo + ".", out error);
            if (!TryResolveDescriptorPayload(workspace, actual.FileName, out string payloadPath, out error) ||
                bindings.ContainsKey(actual.Number) || bindings.Values.Contains(payloadPath, StringComparer.OrdinalIgnoreCase))
                return false;
            bindings.Add(actual.Number, payloadPath);
        }
        return true;
    }

    private static bool TryResolveDescriptorPayload(string workspace, string relativeName,
        out string payloadPath, out string error)
    {
        payloadPath = "";
        error = "";
        try
        {
            string root = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalized = (relativeName ?? "").Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, normalized));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
                return Fail("The extracted descriptor references a missing or unsafe payload file.", out error);
            payloadPath = candidate;
            return true;
        }
        catch (Exception ex)
        {
            return Fail("The extracted descriptor payload path is invalid: " + ex.Message, out error);
        }
    }

    private static bool TryParseCueFrames(string value, out long frames)
    {
        frames = 0;
        string[] parts = (value ?? "").Split(':');
        if (parts.Length != 3 || !long.TryParse(parts[0], out long minutes) ||
            !int.TryParse(parts[1], out int seconds) || !int.TryParse(parts[2], out int frame) ||
            minutes < 0 || seconds < 0 || seconds >= 60 || frame < 0 || frame >= 75)
            return false;
        try { frames = checked(minutes * 60L * 75L + seconds * 75L + frame); return true; }
        catch { frames = 0; return false; }
    }

    private static string ExpectedCueType(ChdCdTrackInfo track)
    {
        string type = (track?.TrackType ?? "").Trim().ToUpperInvariant();
        switch (type)
        {
            case "AUDIO": return "AUDIO";
            case "MODE1":
            case "MODE1/2048": return "MODE1/2048";
            case "MODE1_RAW":
            case "MODE1/2352": return "MODE1/2352";
            case "MODE2":
            case "MODE2/2336":
            case "MODE2_FORM_MIX": return "MODE2/2336";
            case "MODE2_FORM1":
            case "MODE2/2048": return "MODE2/2048";
            case "MODE2_FORM2":
            case "MODE2/2324": return "MODE2/2324";
            case "MODE2_RAW":
            case "MODE2/2352": return "MODE2/2352";
            default: return "";
        }
    }

    private static int ExpectedGdiMode(ChdCdTrackInfo track)
    {
        string type = (track?.TrackType ?? "").Trim().ToUpperInvariant();
        if (type.Contains("AUDIO")) return 0;
        if (type == "MODE1" || type == "MODE1_RAW" || type.StartsWith("MODE1/", StringComparison.Ordinal)) return 4;
        return -1;
    }

    private static bool SemanticStringEquals(string left, string right) =>
        string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static List<ChdCdTrackInfo> CloneTracks(List<ChdCdTrackInfo> tracks)
    {
        List<ChdCdTrackInfo> result = new List<ChdCdTrackInfo>();
        for (int i = 0; tracks != null && i < tracks.Count; i++)
        {
            ChdCdTrackInfo source = tracks[i];
            if (source == null) { result.Add(null); continue; }
            result.Add(new ChdCdTrackInfo
            {
                TrackNo = source.TrackNo,
                TrackType = source.TrackType,
                TrackSubtype = source.TrackSubtype,
                StartFrame = source.StartFrame,
                Frames = source.Frames,
                PreGapFrames = source.PreGapFrames,
                PreGapTrackType = source.PreGapTrackType,
                PreGapSubtype = source.PreGapSubtype,
                PreGapDataStored = source.PreGapDataStored,
                PostGapFrames = source.PostGapFrames,
                PadFrames = source.PadFrames,
                SectorSize = source.SectorSize,
                SubcodeSize = source.SubcodeSize
            });
        }
        return result;
    }

    private static ChdOpticalLayoutSnapshot CreateLayoutFixture()
    {
        return new ChdOpticalLayoutSnapshot
        {
            IsGdRom = false,
            Tracks = new List<ChdCdTrackInfo>
            {
                new ChdCdTrackInfo
                {
                    TrackNo = 1,
                    TrackType = "MODE1_RAW",
                    TrackSubtype = "NONE",
                    StartFrame = 0,
                    Frames = 300,
                    PreGapFrames = 150,
                    PreGapTrackType = "MODE1_RAW",
                    PreGapSubtype = "NONE",
                    PreGapDataStored = true,
                    PostGapFrames = 75,
                    PadFrames = 0,
                    SectorSize = 2352,
                    SubcodeSize = 0
                },
                new ChdCdTrackInfo
                {
                    TrackNo = 2,
                    TrackType = "AUDIO",
                    TrackSubtype = "NONE",
                    StartFrame = 300,
                    Frames = 450,
                    PreGapFrames = 0,
                    PreGapTrackType = "",
                    PreGapSubtype = "NONE",
                    PreGapDataStored = false,
                    PostGapFrames = 0,
                    PadFrames = 0,
                    SectorSize = 2352,
                    SubcodeSize = 0
                }
            }
        };
    }

    private static bool TryExtractPrimaryPayloads(string chdPath, string chdmanPath, string workspace,
        ChdReconstructionManifest manifest, ChdOpticalLayoutSnapshot expectedOpticalLayout,
        out List<string> payloadPaths, out Dictionary<int, string> opticalBindings, out string error)
    {
        payloadPaths = new List<string>();
        opticalBindings = null;
        error = "";
        string family = (manifest?.Family ?? "").Trim().ToLowerInvariant();
        IChdExtractor extractor = new ChdmanChdExtractor(chdmanPath, workspace);

        if ((family == "cd" || family == "gdi") && string.Equals(manifest.Dialect, "toc-exact", StringComparison.OrdinalIgnoreCase))
        {
            string tocPayload = Path.Combine(workspace, "toc-primary.bin");
            if (!ChdOpticalReconstruction.TryMaterializeSingleTocPayload(chdPath, manifest, tocPayload, out error))
                return false;
            payloadPaths.Add(tocPayload);
            if (manifest.Tracks == null || manifest.Tracks.Count != 1)
                return Fail("Exact TOC reconstruction has no unambiguous RVRM payload number.", out error);
            opticalBindings = new Dictionary<int, string> { [manifest.Tracks[0].Number] = tocPayload };
            return true;
        }
        if (family == "cd" || family == "gdi")
        {
            string extension = family == "gdi" && !string.Equals(manifest.Dialect, "redump-gdrom-cue", StringComparison.OrdinalIgnoreCase)
                ? ".gdi" : ".cue";
            string descriptor = Path.Combine(workspace, "validation" + extension);
            if (!extractor.ExtractCd(chdPath, descriptor, out error))
                return false;
            if (!TryBindExtractedDescriptor(descriptor, workspace, expectedOpticalLayout, out opticalBindings, out error))
                return false;
            payloadPaths.AddRange(opticalBindings.OrderBy(item => item.Key).Select(item => item.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            HashSet<string> boundPaths = new HashSet<string>(payloadPaths, StringComparer.OrdinalIgnoreCase);
            List<string> unboundFiles = Directory.GetFiles(workspace, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(path, descriptor, StringComparison.OrdinalIgnoreCase) && !boundPaths.Contains(path))
                .ToList();
            if (unboundFiles.Count != 0)
                return Fail("Optical extraction produced files that are not bound to descriptor track numbers.", out error);
            return payloadPaths.Count > 0 || Fail("Optical extraction produced no primary payload files.", out error);
        }

        string output;
        bool extracted;
        switch (family)
        {
            case "dvd":
            case "psp":
                output = Path.Combine(workspace, "primary.iso");
                extracted = extractor.ExtractDvd(chdPath, output, out error);
                break;
            case "hdd":
                output = Path.Combine(workspace, "primary.img");
                extracted = extractor.ExtractHardDisk(chdPath, output, out error);
                break;
            case "laserdisc":
                output = Path.Combine(workspace, "primary.avi");
                extracted = extractor.ExtractLaserDisc(chdPath, output, out error);
                break;
            case "raw":
                output = Path.Combine(workspace, "primary.raw");
                extracted = extractor.ExtractRaw(chdPath, output, out error);
                break;
            default:
                return Fail("Unsupported RVRM media family for independent payload extraction: " + family + ".", out error);
        }
        if (!extracted || !File.Exists(output))
            return false;
        payloadPaths.Add(output);
        return true;
    }

    private static bool TryMatchPayloads(List<ChdManifestTrack> expected, List<string> actualPaths, string label,
        Dictionary<int, string> numberedBindings, out Dictionary<ChdManifestTrack, string> matches, out string error)
    {
        matches = new Dictionary<ChdManifestTrack, string>();
        error = "";
        expected = expected ?? new List<ChdManifestTrack>();
        List<PayloadDigest> actual = (actualPaths ?? new List<string>()).Where(File.Exists).Select(HashFile).ToList();
        bool[] used = new bool[actual.Count];
        if (expected.Count == 0)
            return Fail("RVRM binds no " + label + " payloads.", out error);
        if (numberedBindings != null && numberedBindings.Count != expected.Count)
            return Fail("The extracted descriptor track count does not match the numbered " + label + " RVRM payload count.", out error);
        for (int i = 0; i < expected.Count; i++)
        {
            ChdManifestTrack item = expected[i];
            if (!HasCompleteIdentity(item))
                return Fail("RVRM has an incomplete fixed identity for " + label + " payload #" + (item?.Number ?? 0) + ".", out error);
            int found = -1;
            for (int j = 0; j < actual.Count; j++)
            {
                bool correctNumber = numberedBindings == null ||
                    (numberedBindings.TryGetValue(item.Number, out string numberedPath) &&
                     string.Equals(numberedPath, actual[j].Path, StringComparison.OrdinalIgnoreCase));
                if (!used[j] && correctNumber && Matches(item, actual[j])) { found = j; break; }
            }
            if (found < 0)
                return Fail("Independent reconstruction did not match the size and CRC32/MD5/SHA1/SHA256 identity of " +
                            label + " payload #" + item.Number + ".", out error);
            used[found] = true;
            matches[item] = actual[found].Path;
        }
        if (used.Any(value => !value))
            return Fail("Independent reconstruction produced payloads not bound by the " + label + " RVRM identity.", out error);
        return true;
    }

    private static bool TryCompareAuxiliaries(List<ChdManifestAuxiliary> expected,
        List<ChdManifestAuxiliary> actual, out string error)
    {
        error = "";
        expected = expected ?? new List<ChdManifestAuxiliary>();
        actual = actual ?? new List<ChdManifestAuxiliary>();
        if (expected.Count != actual.Count)
            return Fail("The candidate exposes a different number of RVRM-bound auxiliary payloads.", out error);
        List<AuxiliaryDigest> actualDigests = actual.Select(item => new AuxiliaryDigest
        {
            Role = item?.Role ?? "",
            Digest = HashBytes(item?.Bytes ?? Array.Empty<byte>())
        }).ToList();
        bool[] used = new bool[actualDigests.Count];
        for (int i = 0; i < expected.Count; i++)
        {
            ChdManifestAuxiliary item = expected[i];
            PayloadDigest expectedDigest = HashBytes(item?.Bytes ?? Array.Empty<byte>());
            int found = -1;
            for (int j = 0; j < actualDigests.Count; j++)
            {
                if (!used[j] && string.Equals(item?.Role ?? "", actualDigests[j].Role, StringComparison.Ordinal) &&
                    Matches(expectedDigest, actualDigests[j].Digest)) { found = j; break; }
            }
            if (found < 0)
                return Fail("RVRM-bound auxiliary payload " + (i + 1) +
                            " changed size or CRC32/MD5/SHA1/SHA256 identity.", out error);
            used[found] = true;
        }
        return true;
    }

    private static bool HasCompleteIdentity(ChdManifestTrack item) =>
        item != null && item.Number > 0 && item.Size >= 0 && item.Crc32?.Length == 4 &&
        item.Md5?.Length == 16 && item.Sha1?.Length == 20 && item.Sha256?.Length == 32;

    private static bool Matches(ChdManifestTrack expected, PayloadDigest actual) =>
        actual != null && expected.Size == actual.Size && Equal(expected.Crc32, actual.Crc32) &&
        Equal(expected.Md5, actual.Md5) && Equal(expected.Sha1, actual.Sha1) && Equal(expected.Sha256, actual.Sha256);

    private static bool Matches(PayloadDigest expected, PayloadDigest actual) =>
        expected != null && actual != null && expected.Size == actual.Size && Equal(expected.Crc32, actual.Crc32) &&
        Equal(expected.Md5, actual.Md5) && Equal(expected.Sha1, actual.Sha1) && Equal(expected.Sha256, actual.Sha256);

    private static PayloadDigest HashFile(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        {
            PayloadDigest digest = HashStream(stream);
            digest.Path = path;
            return digest;
        }
    }

    private static PayloadDigest HashBytes(byte[] bytes)
    {
        using (MemoryStream stream = new MemoryStream(bytes ?? Array.Empty<byte>(), false)) return HashStream(stream);
    }

    private static PayloadDigest HashStream(Stream stream)
    {
        uint crc = 0xffffffff;
        using (SHA1 sha1 = SHA1.Create())
        using (MD5 md5 = MD5.Create())
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] buffer = new byte[1024 * 1024];
            long size = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha1.TransformBlock(buffer, 0, read, null, 0);
                md5.TransformBlock(buffer, 0, read, null, 0);
                sha256.TransformBlock(buffer, 0, read, null, 0);
                for (int i = 0; i < read; i++) crc = UpdateCrc32(crc, buffer[i]);
                size += read;
            }
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            crc ^= 0xffffffff;
            return new PayloadDigest
            {
                Size = size,
                Crc32 = new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc },
                Md5 = md5.Hash,
                Sha1 = sha1.Hash,
                Sha256 = sha256.Hash
            };
        }
    }

    private static ChdManifestTrack ToTrack(int number, PayloadDigest digest) => new ChdManifestTrack
    {
        Number = number,
        Size = digest.Size,
        Crc32 = (byte[])digest.Crc32.Clone(),
        Md5 = (byte[])digest.Md5.Clone(),
        Sha1 = (byte[])digest.Sha1.Clone(),
        Sha256 = (byte[])digest.Sha256.Clone()
    };

    private static long EstimateWorkspaceBytes(ChdContainerInfo container, ChdReconstructionManifest manifest)
    {
        long logical = container?.LogicalSize > long.MaxValue ? long.MaxValue : (long)(container?.LogicalSize ?? 0);
        long views = 0;
        List<ChdManifestView> alternateViews = manifest?.Views ?? new List<ChdManifestView>();
        for (int i = 0; i < alternateViews.Count; i++) views = SaturatingAdd(views, SumSizes(alternateViews[i]?.Tracks));
        return SaturatingAdd(SaturatingAdd(Math.Max(logical, SumSizes(manifest?.Tracks)), views), 256L * 1024 * 1024);
    }

    private static long SumSizes(List<ChdManifestTrack> tracks)
    {
        long total = 0;
        for (int i = 0; tracks != null && i < tracks.Count; i++) total = SaturatingAdd(total, Math.Max(0, tracks[i]?.Size ?? 0));
        return total;
    }

    private static long SaturatingAdd(long left, long right) =>
        right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;

    private static string ResolveWorkspaceRoot(string chdPath, string workingRoot)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(workingRoot) && Directory.Exists(workingRoot)) return Path.GetFullPath(workingRoot);
            string directory = Path.GetDirectoryName(Path.GetFullPath(chdPath));
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) return directory;
        }
        catch { }
        return Path.GetTempPath();
    }

    private static bool Fail(string message, out string error) { error = message; return false; }

    private static bool Equal(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        int difference = 0;
        for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
        return difference == 0;
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        return crc;
    }

    private sealed class PayloadDigest
    {
        public string Path { get; set; } = "";
        public long Size { get; set; }
        public byte[] Crc32 { get; set; }
        public byte[] Md5 { get; set; }
        public byte[] Sha1 { get; set; }
        public byte[] Sha256 { get; set; }
    }

    private sealed class AuxiliaryDigest
    {
        public string Role { get; set; } = "";
        public PayloadDigest Digest { get; set; }
    }

    private sealed class CueTrackBinding
    {
        public int Number { get; set; }
        public string Type { get; set; } = "";
        public string FileName { get; set; } = "";
        public Dictionary<int, long> Indexes { get; } = new Dictionary<int, long>();
        public long? PreGap { get; set; }
        public long? PostGap { get; set; }
    }
}
