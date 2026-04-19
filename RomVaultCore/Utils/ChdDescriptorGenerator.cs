using System;
using System.Collections.Generic;
using System.Text;
using CHDSharpLib;
using RomVaultCore.RvDB;

namespace RomVaultCore.Utils;

/// <summary>
/// Generates synthetic CUE/GDI descriptors from CHD track metadata.
/// </summary>
/// <remarks>
/// These descriptors are used to satisfy DAT entries that expect a descriptor file when strict
/// byte-for-byte matching is not required (or when the descriptor is verified against DAT hashes).
/// </remarks>
public static class ChdDescriptorGenerator
{
    /// <summary>
    /// Builds a synthetic CUE file using CHD track metadata.
    /// </summary>
    /// <param name="tracks">Track layout as reported by CHD metadata.</param>
    /// <param name="expectedByTrack">Optional mapping of DAT track numbers to expected file names.</param>
    /// <returns>CUE text.</returns>
    public static string BuildCue(List<ChdCdTrackInfo> tracks, Dictionary<int, RvFile> expectedByTrack)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i];
            string fileName = ResolveExpectedName(expectedByTrack, t.TrackNo, $"track{t.TrackNo:D2}.bin");
            sb.Append("FILE \"").Append(fileName.Replace("\"", "")).AppendLine("\" BINARY");
            sb.Append("  TRACK ").Append(t.TrackNo.ToString("D2")).Append(' ').Append(ToCueType(t.TrackType, t.SectorSize)).AppendLine();
            sb.AppendLine("    INDEX 01 00:00:00");
            if (t.PreGapFrames > 0)
            {
                int mm, ss, ff;
                FramesToMSF(t.PreGapFrames, out mm, out ss, out ff);
                sb.Append("    PREGAP ").Append(mm.ToString("D2")).Append(':').Append(ss.ToString("D2")).Append(':').Append(ff.ToString("D2")).AppendLine();
            }
            if (t.PostGapFrames > 0)
            {
                int mm, ss, ff;
                FramesToMSF(t.PostGapFrames, out mm, out ss, out ff);
                sb.Append("    POSTGAP ").Append(mm.ToString("D2")).Append(':').Append(ss.ToString("D2")).Append(':').Append(ff.ToString("D2")).AppendLine();
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds a synthetic GDI file using CHD track metadata.
    /// </summary>
    /// <param name="tracks">Track layout as reported by CHD metadata.</param>
    /// <param name="expectedByTrack">Optional mapping of DAT track numbers to expected file names.</param>
    /// <returns>GDI text.</returns>
    public static string BuildGdi(List<ChdCdTrackInfo> tracks, Dictionary<int, RvFile> expectedByTrack)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(tracks.Count.ToString());
        for (int i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i];
            string fileName = ResolveExpectedName(expectedByTrack, t.TrackNo, $"track{t.TrackNo:D2}.bin");
            long lba = t.StartFrame; // use frames as LBA
            int mode = ToGdiMode(t.TrackType, t.SectorSize);
            long offset = 0;
            sb.Append(t.TrackNo).Append(' ')
              .Append(lba).Append(' ')
              .Append(mode).Append(' ')
              .Append(t.SectorSize).Append(' ')
              .Append('"').Append(fileName.Replace('"', ' ')).Append('"').Append(' ')
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

    private static string ToCueType(string trackType, int sectorSize)
    {
        string t = (trackType ?? "").ToUpperInvariant();
        if (t.Contains("AUDIO"))
            return "AUDIO";
        if (sectorSize == 2048 || t.Contains("2048"))
            return "MODE1/2048";
        if (sectorSize == 2352 || t.Contains("2352"))
            return "MODE1/2352";
        if (t.Contains("MODE2"))
            return "MODE2/2352";
        return "MODE1/2352";
    }

    private static int ToGdiMode(string trackType, int sectorSize)
    {
        string t = (trackType ?? "").ToUpperInvariant();
        if (t.Contains("AUDIO"))
            return 0;
        if (sectorSize == 2048 || t.Contains("2048"))
            return 4; // MODE1/2048
        if (t.Contains("MODE2"))
            return 5; // MODE2/2352
        return 4; // default MODE1/2048
    }

    private static void FramesToMSF(long frames, out int mm, out int ss, out int ff)
    {
        mm = (int)(frames / (60 * 75));
        frames -= mm * 60 * 75;
        ss = (int)(frames / 75);
        ff = (int)(frames - ss * 75);
    }
}
