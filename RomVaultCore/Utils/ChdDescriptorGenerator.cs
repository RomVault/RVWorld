using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CHDSharpLib;
using RomVaultCore.RvDB;

namespace RomVaultCore.Utils;

public static class ChdDescriptorGenerator
{
    public static string BuildCue(List<ChdCdTrackInfo> tracks, Dictionary<int, RvFile> expectedByTrack)
    {
        return BuildCue(tracks, expectedByTrack, false);
    }

    public static string BuildCue(
        List<ChdCdTrackInfo> tracks,
        Dictionary<int, RvFile> expectedByTrack,
        bool isGdRom)
    {
        int highDensityTrackIndex = -1;
        if (isGdRom)
        {
            if (tracks == null || tracks.Count < 3 || tracks[0].StartFrame != 0)
                throw new InvalidDataException("A GD-ROM CUE requires low-density tracks beginning at LBA 0 and a high-density track at LBA 45000.");
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].StartFrame == 45000)
                {
                    highDensityTrackIndex = i;
                    break;
                }
            }
            if (highDensityTrackIndex != 2)
                throw new InvalidDataException("A Redump GD-ROM CUE must begin its high-density area at track 03, LBA 45000.");
        }

        StringBuilder sb = new StringBuilder();
        if (isGdRom)
            sb.AppendLine("REM SINGLE-DENSITY AREA");
        for (int i = 0; i < tracks.Count; i++)
        {
            if (isGdRom && i == highDensityTrackIndex)
                sb.AppendLine("REM HIGH-DENSITY AREA");
            var t = tracks[i];
            string fileName = ResolveExpectedName(expectedByTrack, t.TrackNo, $"track{t.TrackNo:D2}.bin");
            EnsureCueRepresentable(t);
            sb.Append("FILE \"").Append(SanitizeDescriptorFileName(fileName)).AppendLine("\" BINARY");
            sb.Append("  TRACK ").Append(t.TrackNo.ToString("D2")).Append(' ').Append(ToCueType(t.TrackType, t.SectorSize)).AppendLine();
            if (t.PreGapFrames > 0 && t.PreGapDataStored)
            {
                sb.AppendLine("    INDEX 00 00:00:00");
                sb.Append("    INDEX 01 ").Append(FormatFrames(t.PreGapFrames)).AppendLine();
            }
            else
            {
                if (t.PreGapFrames > 0)
                    sb.Append("    PREGAP ").Append(FormatFrames(t.PreGapFrames)).AppendLine();
                sb.AppendLine("    INDEX 01 00:00:00");
            }
            if (t.PostGapFrames > 0)
                sb.Append("    POSTGAP ").Append(FormatFrames(t.PostGapFrames)).AppendLine();
        }
        return sb.ToString();
    }

    public static string BuildGdi(List<ChdCdTrackInfo> tracks, Dictionary<int, RvFile> expectedByTrack)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(tracks.Count.ToString());
        for (int i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i];
            string fileName = ResolveExpectedName(expectedByTrack, t.TrackNo, $"track{t.TrackNo:D2}.bin");
            EnsureGdiRepresentable(t);
            // StartFrame is derived from cumulative CHGD FRAMES.  FRAMES
            // includes PAD, which is exactly what advances the next GDI LBA.
            long lba = t.StartFrame;
            int mode = ToGdiMode(t.TrackType, t.SectorSize);
            long offset = 0;
            sb.Append(t.TrackNo).Append(' ')
              .Append(lba).Append(' ')
              .Append(mode).Append(' ')
              .Append(t.SectorSize).Append(' ')
              .Append('"').Append(SanitizeDescriptorFileName(fileName)).Append('"').Append(' ')
              .Append(offset)
              .AppendLine();
        }
        return sb.ToString();
    }

    private static string ResolveExpectedName(Dictionary<int, RvFile> expectedByTrack, int trackNo, string fallback)
    {
        if (expectedByTrack != null && expectedByTrack.TryGetValue(trackNo, out var rv) && rv?.Name != null)
            return rv.Name;
        return fallback;
    }

    private static string SanitizeDescriptorFileName(string fileName)
    {
        return fileName.Replace("\"", "").Replace("\r", "").Replace("\n", "");
    }

    private static string ToCueType(string trackType, int sectorSize)
    {
        string t = (trackType ?? "").ToUpperInvariant();
        switch (t)
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
        }

        throw new InvalidDataException("CUE cannot represent CHD track type '" + trackType +
                                       "' with " + sectorSize + "-byte sectors.");
    }

    private static int ToGdiMode(string trackType, int sectorSize)
    {
        string t = (trackType ?? "").ToUpperInvariant();
        if (t.Contains("AUDIO"))
            return 0;
        if (t == "MODE1" || t == "MODE1_RAW" || t.StartsWith("MODE1/", StringComparison.Ordinal))
            return 4;
        throw new InvalidDataException("GDI cannot represent CHD track type '" + trackType + "'.");
    }

    private static void EnsureCueRepresentable(ChdCdTrackInfo track)
    {
        if (HasSubcode(track.TrackSubtype) || track.SubcodeSize > 0 || HasSubcode(track.PreGapSubtype))
            throw new InvalidDataException("CUE cannot represent stored RW/RW_RAW subcode data; use TOC output.");
        if (track.PreGapDataStored && !string.IsNullOrWhiteSpace(track.PreGapTrackType) &&
            !string.Equals(ToCueType(track.PreGapTrackType, ResolveTypeSectorSize(track.PreGapTrackType)),
                           ToCueType(track.TrackType, track.SectorSize), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CUE cannot represent pregap data with a different track type.");
    }

    private static void EnsureGdiRepresentable(ChdCdTrackInfo track)
    {
        if (HasSubcode(track.TrackSubtype) || track.SubcodeSize > 0)
            throw new InvalidDataException("GDI cannot represent stored RW/RW_RAW subcode data.");
        string type = (track.TrackType ?? "").ToUpperInvariant();
        if (type.Contains("AUDIO"))
        {
            if (track.SectorSize != 2352)
                throw new InvalidDataException("GDI audio tracks must use 2352-byte sectors.");
            return;
        }
        if ((type == "MODE1" || type == "MODE1_RAW" || type.StartsWith("MODE1/", StringComparison.Ordinal)) &&
            (track.SectorSize == 2048 || track.SectorSize == 2352))
            return;
        throw new InvalidDataException("GDI cannot represent CHD track type '" + track.TrackType +
                                       "' with " + track.SectorSize + "-byte sectors.");
    }

    private static bool HasSubcode(string subtype)
    {
        string value = (subtype ?? "").Trim();
        return value.Length > 0 && !string.Equals(value, "NONE", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveTypeSectorSize(string trackType)
    {
        string value = (trackType ?? "").Trim().ToUpperInvariant();
        switch (value)
        {
            case "MODE1":
            case "MODE1/2048":
            case "MODE2_FORM1":
            case "MODE2/2048": return 2048;
            case "MODE2":
            case "MODE2/2336":
            case "MODE2_FORM_MIX": return 2336;
            case "MODE2_FORM2":
            case "MODE2/2324": return 2324;
            default: return 2352;
        }
    }

    private static string FormatFrames(long frames)
    {
        int mm, ss, ff;
        FramesToMSF(frames, out mm, out ss, out ff);
        return mm.ToString("D2") + ":" + ss.ToString("D2") + ":" + ff.ToString("D2");
    }

    private static void FramesToMSF(long frames, out int mm, out int ss, out int ff)
    {
        mm = (int)(frames / (60 * 75));
        frames -= mm * 60 * 75;
        ss = (int)(frames / 75);
        ff = (int)(frames - ss * 75);
    }
}
