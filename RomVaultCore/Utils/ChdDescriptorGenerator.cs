using System;
using System.Collections.Generic;
using System.Text;
using CHDSharpLib;
using RomVaultCore.RvDB;

namespace RomVaultCore.Utils;

public static class ChdDescriptorGenerator
{
    public static string BuildCue(List<ChdCdTrackInfo> tracks, Dictionary<int, RvFile> expectedByTrack)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i];
            string fileName = ResolveExpectedName(expectedByTrack, t.TrackNo, $"track{t.TrackNo:D2}.bin");
            sb.Append("FILE \"").Append(SanitizeDescriptorFileName(fileName)).AppendLine("\" BINARY");
            sb.Append("  TRACK ").Append(t.TrackNo.ToString("D2")).Append(' ').Append(ToCueType(t.TrackType, t.SectorSize)).AppendLine();
            if (t.PreGapFrames > 0)
            {
                int mm, ss, ff;
                FramesToMSF(t.PreGapFrames, out mm, out ss, out ff);
                sb.Append("    PREGAP ").Append(mm.ToString("D2")).Append(':').Append(ss.ToString("D2")).Append(':').Append(ff.ToString("D2")).AppendLine();
            }
            sb.AppendLine("    INDEX 01 00:00:00");
            if (t.PostGapFrames > 0)
            {
                int mm, ss, ff;
                FramesToMSF(t.PostGapFrames, out mm, out ss, out ff);
                sb.Append("    POSTGAP ").Append(mm.ToString("D2")).Append(':').Append(ss.ToString("D2")).Append(':').Append(ff.ToString("D2")).AppendLine();
            }
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
            long lba = t.StartFrame; // use frames as LBA
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
        if (t.Contains("AUDIO"))
            return "AUDIO";
        if (t.Contains("MODE2"))
            return sectorSize == 2048 || t.Contains("2048") ? "MODE2/2048" : "MODE2/2352";
        if (sectorSize == 2048 || t.Contains("2048"))
            return "MODE1/2048";
        if (sectorSize == 2352 || t.Contains("2352"))
            return "MODE1/2352";
        return "MODE1/2352";
    }

    private static int ToGdiMode(string trackType, int sectorSize)
    {
        string t = (trackType ?? "").ToUpperInvariant();
        if (t.Contains("AUDIO"))
            return 0;
        return 4;
    }

    private static void FramesToMSF(long frames, out int mm, out int ss, out int ff)
    {
        mm = (int)(frames / (60 * 75));
        frames -= mm * 60 * 75;
        ss = (int)(frames / 75);
        ff = (int)(frames - ss * 75);
    }
}
