using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RomVaultCore.Utils;

/// <summary>
/// Rejects optical descriptor layouts whose reconstruction semantics are not
/// represented by the filename-independent v1 contract.
/// </summary>
internal static class ChdRoundTripInputPolicy
{
    private const long MaxDescriptorBytes = 1024 * 1024;
    private const int MaxDescriptorCharacters = 1024 * 1024;
    private const int MaxLines = 10000;
    private const int MaxLineCharacters = 16384;
    private const int MaxTracks = 99;

    internal static bool Validate(string inputPath, string family, out string error)
    {
        error = "";
        string normalizedFamily = (family ?? "").Trim().ToLowerInvariant();
        if (normalizedFamily != "cd" && normalizedFamily != "gdi")
            return true;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            error = "An optical input descriptor path is required.";
            return false;
        }

        string extension;
        try
        {
            extension = Path.GetExtension(inputPath).ToLowerInvariant();
        }
        catch
        {
            error = "The optical input descriptor path is invalid.";
            return false;
        }

        // TOC and other media paths retain their existing family-specific
        // validation. This policy only narrows CUE and GDI inputs.
        if (extension != ".cue" && extension != ".gdi")
            return true;

        if (!TryReadBoundedLines(inputPath, extension == ".cue" ? "CUE" : "GDI", out List<string> lines, out error))
            return false;

        return extension == ".cue"
            ? ValidateCue(lines, inputPath, normalizedFamily == "gdi", out error)
            : ValidateGdi(lines, inputPath, out error);
    }

    private static bool ValidateCue(List<string> lines, string descriptorPath, bool expectsGdRom, out string error)
    {
        error = "";
        HashSet<string> payloadNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> trackNumbers = new HashSet<int>();
        bool hasFile = false;
        bool currentFileHasTrack = false;
        bool hasTrack = false;
        bool currentTrackHasIndex00 = false;
        bool currentTrackHasIndex01 = false;
        bool currentTrackHasPregap = false;
        bool currentTrackHasPostgap = false;
        bool sawSingleDensity = false;
        bool sawHighDensity = false;
        bool sawTrackAfterHighDensity = false;
        string currentPayloadName = "";
        long currentPayloadFrames = 0;
        int previousTrackNumber = 0;
        int fileCount = 0;
        int trackCount = 0;

        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0)
                continue;

            SplitDirective(line, out string directive, out string arguments);
            string upperDirective = directive.ToUpperInvariant();
            int lineNumber = index + 1;

            if (upperDirective == "REM")
            {
                SplitDirective(arguments, out string remDirective, out _);
                if (IsSessionBoundaryDirective(remDirective))
                {
                    error = $"CUE line {lineNumber} uses an explicit session or lead-in/lead-out construct that v1 does not retain.";
                    return false;
                }

                if (TryParseDensityMarker(arguments, out bool highDensity))
                {
                    if (!expectsGdRom)
                    {
                        error = $"CUE line {lineNumber} uses a GD-ROM density marker for a non-GD family.";
                        return false;
                    }
                    if (!highDensity)
                    {
                        if (sawSingleDensity || sawHighDensity || trackCount != 0)
                        {
                            error = $"CUE line {lineNumber} has a duplicate or misplaced SINGLE-DENSITY AREA marker.";
                            return false;
                        }
                        sawSingleDensity = true;
                    }
                    else
                    {
                        // Redump GD-ROM CUEs contain two low-density tracks;
                        // the marker changes the area assigned to subsequent
                        // TRACK statements, so it must precede track 03.
                        if (!sawSingleDensity || sawHighDensity || trackCount != 2 ||
                            (hasTrack && !currentTrackHasIndex01))
                        {
                            error = $"CUE line {lineNumber} must place one HIGH-DENSITY AREA marker after track 02 and before track 03.";
                            return false;
                        }
                        sawHighDensity = true;
                    }
                }

                // Other REM lines are ordinary comments and intentionally do
                // not participate in the filename-independent contract.
                continue;
            }

            if (IsSessionBoundaryDirective(upperDirective))
            {
                error = $"CUE line {lineNumber} uses an explicit session or lead-in/lead-out construct that v1 does not retain.";
                return false;
            }

            switch (upperDirective)
            {
                case "FILE":
                    if (hasFile && !currentFileHasTrack)
                    {
                        error = $"CUE line {lineNumber} starts a new FILE block before the preceding block defines exactly one track.";
                        return false;
                    }
                    if (hasTrack && !currentTrackHasIndex01)
                    {
                        error = $"CUE line {lineNumber} starts a new FILE block before the preceding track defines INDEX 01.";
                        return false;
                    }
                    if (!TryTokenize(arguments, out List<string> fileTokens) || fileTokens.Count != 2 ||
                        string.IsNullOrWhiteSpace(fileTokens[0]))
                    {
                        error = $"CUE line {lineNumber} has an invalid FILE statement.";
                        return false;
                    }
                    if (!string.Equals(fileTokens[1], "BINARY", StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"CUE line {lineNumber} uses a non-BINARY FILE wrapper, which v1 cannot reconstruct filename-independently.";
                        return false;
                    }
                    if (!payloadNames.Add(fileTokens[0]))
                    {
                        error = $"CUE line {lineNumber} reuses a payload filename; v1 requires one unique file per track.";
                        return false;
                    }
                    fileCount++;
                    if (fileCount > MaxTracks)
                    {
                        error = "CUE contains too many FILE blocks.";
                        return false;
                    }
                    hasFile = true;
                    currentPayloadName = fileTokens[0];
                    currentPayloadFrames = 0;
                    currentFileHasTrack = false;
                    hasTrack = false;
                    currentTrackHasIndex00 = false;
                    currentTrackHasIndex01 = false;
                    currentTrackHasPregap = false;
                    currentTrackHasPostgap = false;
                    break;

                case "TRACK":
                    if (!hasFile)
                    {
                        error = $"CUE line {lineNumber} defines a track outside a FILE block.";
                        return false;
                    }
                    if (currentFileHasTrack)
                    {
                        error = $"CUE line {lineNumber} defines multiple tracks in one FILE block; v1 requires split-per-track BINARY files.";
                        return false;
                    }
                    if (!TryTokenize(arguments, out List<string> trackTokens) || trackTokens.Count != 2 ||
                        !TryParseInvariantInt(trackTokens[0], out int trackNumber) || trackNumber <= 0 || trackNumber > MaxTracks ||
                        !TryGetCueSectorSize(trackTokens[1], out int sectorSize))
                    {
                        error = $"CUE line {lineNumber} has an invalid or unsupported TRACK statement.";
                        return false;
                    }
                    if (trackNumber != previousTrackNumber + 1 || !trackNumbers.Add(trackNumber))
                    {
                        error = $"CUE line {lineNumber} must use consecutive track numbers beginning with 01.";
                        return false;
                    }
                    if (!TryGetPayloadFrameCount(descriptorPath, currentPayloadName, sectorSize,
                                                 out currentPayloadFrames, out string payloadError))
                    {
                        error = $"CUE line {lineNumber} payload '{currentPayloadName}' is not losslessly representable: {payloadError}";
                        return false;
                    }
                    previousTrackNumber = trackNumber;
                    trackCount++;
                    if (sawHighDensity)
                        sawTrackAfterHighDensity = true;
                    currentFileHasTrack = true;
                    hasTrack = true;
                    currentTrackHasIndex00 = false;
                    currentTrackHasIndex01 = false;
                    currentTrackHasPregap = false;
                    currentTrackHasPostgap = false;
                    break;

                case "INDEX":
                    if (!hasTrack)
                    {
                        error = $"CUE line {lineNumber} defines INDEX outside a track.";
                        return false;
                    }
                    if (!TryTokenize(arguments, out List<string> indexTokens) || indexTokens.Count != 2 ||
                        !TryParseInvariantInt(indexTokens[0], out int indexNumber) ||
                        !TryCueTimeToFrames(indexTokens[1], out long indexFrames))
                    {
                        error = $"CUE line {lineNumber} has an invalid INDEX statement.";
                        return false;
                    }
                    if (indexNumber >= 2)
                    {
                        error = $"CUE line {lineNumber} uses INDEX {indexNumber:D2}; v1 retains only INDEX 00 and INDEX 01 semantics.";
                        return false;
                    }
                    if (indexNumber < 0 || (indexNumber == 0 && (currentTrackHasIndex00 || currentTrackHasIndex01)) ||
                        (indexNumber == 1 && currentTrackHasIndex01))
                    {
                        error = $"CUE line {lineNumber} has a duplicate or out-of-order INDEX statement.";
                        return false;
                    }
                    if (indexNumber == 0)
                    {
                        if (currentTrackHasPregap)
                        {
                            error = $"CUE line {lineNumber} combines stored INDEX 00 data with a synthetic PREGAP.";
                            return false;
                        }
                        if (indexFrames != 0)
                        {
                            error = $"CUE line {lineNumber} starts INDEX 00 after unreferenced file data; v1 requires INDEX 00 at 00:00:00.";
                            return false;
                        }
                        currentTrackHasIndex00 = true;
                    }
                    else
                    {
                        if (currentTrackHasPregap && indexFrames != 0)
                        {
                            error = $"CUE line {lineNumber} combines a synthetic PREGAP with a non-zero INDEX 01 file offset.";
                            return false;
                        }
                        if (currentTrackHasIndex00 && indexFrames <= 0)
                        {
                            error = $"CUE line {lineNumber} does not place INDEX 01 after INDEX 00.";
                            return false;
                        }
                        if (indexFrames >= currentPayloadFrames)
                        {
                            error = $"CUE line {lineNumber} places INDEX 01 at or beyond the end of its payload file.";
                            return false;
                        }
                        if (!currentTrackHasIndex00 && !currentTrackHasPregap && indexFrames != 0)
                        {
                            error = $"CUE line {lineNumber} has unreferenced data before INDEX 01; v1 requires an explicit INDEX 00.";
                            return false;
                        }
                        currentTrackHasIndex01 = true;
                    }
                    break;

                case "PREGAP":
                case "POSTGAP":
                    if (!hasTrack)
                    {
                        error = $"CUE line {lineNumber} defines {upperDirective} outside a track.";
                        return false;
                    }
                    if (!TryTokenize(arguments, out List<string> gapTokens) || gapTokens.Count != 1 ||
                        !TryCueTimeToFrames(gapTokens[0], out _))
                    {
                        error = $"CUE line {lineNumber} has an invalid {upperDirective} statement.";
                        return false;
                    }
                    if ((upperDirective == "PREGAP" && currentTrackHasPregap) ||
                        (upperDirective == "POSTGAP" && currentTrackHasPostgap))
                    {
                        error = $"CUE line {lineNumber} repeats {upperDirective} for one track.";
                        return false;
                    }
                    if (upperDirective == "PREGAP")
                    {
                        if (currentTrackHasIndex00 || currentTrackHasIndex01)
                        {
                            error = $"CUE line {lineNumber} places a synthetic PREGAP after stored track indexes.";
                            return false;
                        }
                        currentTrackHasPregap = true;
                    }
                    else
                    {
                        if (!currentTrackHasIndex01)
                        {
                            error = $"CUE line {lineNumber} places POSTGAP before INDEX 01.";
                            return false;
                        }
                        currentTrackHasPostgap = true;
                    }
                    break;

                case "FLAGS":
                    error = $"CUE line {lineNumber} uses FLAGS, whose track semantics are not retained by v1.";
                    return false;

                case "CDTEXTFILE":
                    error = $"CUE line {lineNumber} uses CDTEXTFILE, whose external content is not retained by v1.";
                    return false;

                case "CATALOG":
                case "ISRC":
                case "PERFORMER":
                case "SONGWRITER":
                case "TITLE":
                    error = $"CUE line {lineNumber} uses {upperDirective}, which standard CHD metadata cannot reconstruct.";
                    return false;

                default:
                    error = $"CUE line {lineNumber} uses unsupported directive '{upperDirective}'; v1 cannot prove its layout semantics.";
                    return false;
            }
        }

        if (!hasFile || fileCount == 0 || trackCount == 0)
        {
            error = "CUE contains no reconstructable BINARY split-track layout.";
            return false;
        }
        if (!currentFileHasTrack || fileCount != trackCount)
        {
            error = "CUE must contain exactly one track in each unique BINARY FILE block.";
            return false;
        }
        if (!currentTrackHasIndex01)
        {
            error = "The final CUE track does not define INDEX 01.";
            return false;
        }
        if (expectsGdRom && (!sawSingleDensity || !sawHighDensity || !sawTrackAfterHighDensity))
        {
            error = "A Redump GD-ROM CUE requires one SINGLE-DENSITY AREA marker and one HIGH-DENSITY AREA marker before track 03.";
            return false;
        }
        return true;
    }

    private static bool ValidateGdi(List<string> lines, string descriptorPath, out string error)
    {
        error = "";
        int firstContentLine = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                firstContentLine = i;
                break;
            }
        }

        if (firstContentLine < 0 || !TryParseInvariantInt(lines[firstContentLine].Trim(), out int declaredTracks) ||
            declaredTracks <= 0 || declaredTracks > MaxTracks)
        {
            error = "GDI does not begin with a valid bounded track count.";
            return false;
        }

        HashSet<int> trackNumbers = new HashSet<int>();
        HashSet<string> payloadNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int entryCount = 0;
        int previousTrackNumber = 0;
        long previousLba = -1;
        long previousPayloadFrames = 0;
        for (int index = firstContentLine + 1; index < lines.Count; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0)
                continue;
            int lineNumber = index + 1;
            if (!TryTokenize(line, out List<string> tokens) || tokens.Count != 6)
            {
                error = $"GDI line {lineNumber} must contain exactly one six-field track entry.";
                return false;
            }
            if (!TryParseInvariantInt(tokens[0], out int trackNumber) || trackNumber <= 0 || trackNumber > declaredTracks ||
                !long.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long lba) || lba < 0 ||
                !TryParseInvariantInt(tokens[2], out int mode) ||
                !TryParseInvariantInt(tokens[3], out int sectorSize) || sectorSize <= 0 ||
                string.IsNullOrWhiteSpace(tokens[4]) ||
                !long.TryParse(tokens[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out long offset))
            {
                error = $"GDI line {lineNumber} contains invalid track fields.";
                return false;
            }
            if (offset != 0)
            {
                error = $"GDI line {lineNumber} uses a non-zero file offset; v1 requires one complete file per track.";
                return false;
            }
            if ((mode == 0 && sectorSize != 2352) ||
                (mode == 4 && sectorSize != 2048 && sectorSize != 2352) ||
                (mode != 0 && mode != 4))
            {
                error = $"GDI line {lineNumber} uses an unsupported mode/sector-size combination.";
                return false;
            }
            if (!TryGetPayloadFrameCount(descriptorPath, tokens[4], sectorSize,
                                         out long payloadFrames, out string payloadError))
            {
                error = $"GDI line {lineNumber} payload '{tokens[4]}' is not losslessly representable: {payloadError}";
                return false;
            }
            if ((entryCount == 0 && lba != 0) ||
                (entryCount > 0 && lba <= previousLba))
            {
                error = $"GDI line {lineNumber} must use LBA 0 for track 1 and strictly increasing LBAs thereafter.";
                return false;
            }
            if (entryCount > 0 &&
                (previousPayloadFrames > long.MaxValue - previousLba ||
                 lba < previousLba + previousPayloadFrames))
            {
                error = $"GDI line {lineNumber} overlaps the preceding payload instead of defining non-negative CHGD padding.";
                return false;
            }
            if (!trackNumbers.Add(trackNumber))
            {
                error = $"GDI line {lineNumber} repeats a track number.";
                return false;
            }
            if (trackNumber != previousTrackNumber + 1)
            {
                error = $"GDI line {lineNumber} must use consecutive track numbers beginning with 1.";
                return false;
            }
            if (!payloadNames.Add(tokens[4]))
            {
                error = $"GDI line {lineNumber} reuses a payload filename; v1 requires one unique file per track.";
                return false;
            }
            previousTrackNumber = trackNumber;
            previousLba = lba;
            previousPayloadFrames = payloadFrames;
            entryCount++;
            if (entryCount > declaredTracks)
            {
                error = "GDI contains more track entries than its declared count.";
                return false;
            }
        }

        if (entryCount != declaredTracks || trackNumbers.Count != declaredTracks)
        {
            error = $"GDI declares {declaredTracks} tracks but contains {entryCount} unique one-file-per-track entries.";
            return false;
        }
        return true;
    }

    private static bool TryReadBoundedLines(string path, string descriptorKind, out List<string> lines, out string error)
    {
        lines = new List<string>();
        error = "";
        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                       4096, FileOptions.SequentialScan))
            {
                if (stream.Length <= 0)
                {
                    error = descriptorKind + " descriptor is empty.";
                    return false;
                }
                if (stream.Length > MaxDescriptorBytes)
                {
                    error = descriptorKind + " descriptor exceeds the 1 MiB policy limit.";
                    return false;
                }

                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true), true, 4096, false))
                {
                    int characters = 0;
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length > MaxLineCharacters)
                        {
                            error = descriptorKind + " descriptor contains an excessively long line.";
                            return false;
                        }
                        characters += line.Length;
                        if (characters > MaxDescriptorCharacters)
                        {
                            error = descriptorKind + " descriptor exceeds the bounded text limit.";
                            return false;
                        }
                        if (line.IndexOf('\0') >= 0)
                        {
                            error = descriptorKind + " descriptor contains a NUL character.";
                            return false;
                        }
                        lines.Add(line);
                        if (lines.Count > MaxLines)
                        {
                            error = descriptorKind + " descriptor contains too many lines.";
                            return false;
                        }
                    }
                }
            }
        }
        catch (DecoderFallbackException)
        {
            error = descriptorKind + " descriptor is not valid BOM-marked Unicode or UTF-8 text.";
            return false;
        }
        catch (FileNotFoundException)
        {
            error = descriptorKind + " descriptor was not found.";
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            error = descriptorKind + " descriptor directory was not found.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = descriptorKind + " descriptor is not readable.";
            return false;
        }
        catch (IOException)
        {
            error = descriptorKind + " descriptor could not be read safely.";
            return false;
        }
        catch
        {
            error = descriptorKind + " descriptor could not be parsed safely.";
            return false;
        }
        return true;
    }

    private static bool TryTokenize(string value, out List<string> tokens)
    {
        tokens = new List<string>();
        value = value ?? "";
        int index = 0;
        while (index < value.Length)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            if (index >= value.Length)
                break;

            if (value[index] == '"')
            {
                int start = ++index;
                while (index < value.Length && value[index] != '"')
                    index++;
                if (index >= value.Length)
                    return false;
                tokens.Add(value.Substring(start, index - start));
                index++;
                if (index < value.Length && !char.IsWhiteSpace(value[index]))
                    return false;
            }
            else
            {
                int start = index;
                while (index < value.Length && !char.IsWhiteSpace(value[index]))
                {
                    if (value[index] == '"')
                        return false;
                    index++;
                }
                tokens.Add(value.Substring(start, index - start));
            }
        }
        return true;
    }

    private static void SplitDirective(string line, out string directive, out string arguments)
    {
        line = line ?? "";
        int index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;
        int start = index;
        while (index < line.Length && !char.IsWhiteSpace(line[index]))
            index++;
        directive = line.Substring(start, index - start);
        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;
        arguments = index < line.Length ? line.Substring(index).TrimEnd() : "";
    }

    private static bool IsSessionBoundaryDirective(string directive)
    {
        string normalized = (directive ?? "").Replace("-", "").Replace("_", "").ToUpperInvariant();
        return normalized == "SESSION" || normalized == "LEADIN" || normalized == "LEADOUT";
    }

    private static bool TryParseDensityMarker(string arguments, out bool highDensity)
    {
        highDensity = false;
        string value = (arguments ?? "").Trim();
        if (string.Equals(value, "SINGLE-DENSITY AREA", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(value, "HIGH-DENSITY AREA", StringComparison.OrdinalIgnoreCase))
        {
            highDensity = true;
            return true;
        }
        return false;
    }

    private static bool TryGetCueSectorSize(string trackType, out int sectorSize)
    {
        sectorSize = 0;
        switch ((trackType ?? "").Trim().ToUpperInvariant())
        {
            case "MODE1":
            case "MODE1/2048":
            case "MODE2_FORM1":
            case "MODE2/2048":
                sectorSize = 2048;
                return true;
            case "MODE2_FORM2":
            case "MODE2/2324":
                sectorSize = 2324;
                return true;
            case "MODE2":
            case "MODE2_FORM_MIX":
            case "MODE2/2336":
                sectorSize = 2336;
                return true;
            case "MODE1_RAW":
            case "MODE1/2352":
            case "MODE2_RAW":
            case "MODE2/2352":
            case "AUDIO":
                sectorSize = 2352;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetPayloadFrameCount(
        string descriptorPath,
        string memberName,
        int sectorSize,
        out long frames,
        out string error)
    {
        frames = 0;
        error = "";
        if (sectorSize <= 0 || string.IsNullOrWhiteSpace(memberName))
        {
            error = "the payload name or sector size is invalid";
            return false;
        }

        try
        {
            string descriptorDirectory = Path.GetDirectoryName(Path.GetFullPath(descriptorPath)) ?? Environment.CurrentDirectory;
            string platformName = memberName.Replace('/', Path.DirectorySeparatorChar)
                                            .Replace('\\', Path.DirectorySeparatorChar);
            string payloadPath = Path.GetFullPath(Path.Combine(descriptorDirectory, platformName));
            if (!File.Exists(payloadPath))
            {
                error = "the referenced file does not exist";
                return false;
            }

            long length = new FileInfo(payloadPath).Length;
            if (length <= 0)
            {
                error = "the referenced file is empty";
                return false;
            }
            if (length % sectorSize != 0)
            {
                error = $"its {length} bytes are not an exact multiple of the {sectorSize}-byte sector size";
                return false;
            }

            frames = length / sectorSize;
            return frames > 0;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                   ex is PathTooLongException || ex is UnauthorizedAccessException ||
                                   ex is IOException)
        {
            error = "the referenced file cannot be inspected safely";
            return false;
        }
    }

    private static bool TryParseInvariantInt(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryCueTimeToFrames(string value, out long totalFrames)
    {
        totalFrames = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        string[] parts = value.Split(':');
        if (parts.Length != 3 ||
            !TryParseInvariantInt(parts[0], out int minutes) ||
            !TryParseInvariantInt(parts[1], out int seconds) ||
            !TryParseInvariantInt(parts[2], out int frames))
            return false;
        if (minutes < 0 || seconds < 0 || seconds >= 60 || frames < 0 || frames >= 75)
            return false;
        try
        {
            totalFrames = checked((long)minutes * 60L * 75L + (long)seconds * 75L + frames);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
