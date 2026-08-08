using System;
using System.Text.RegularExpressions;
using CHDSharpLib;

namespace RomVaultCore.Utils;

internal sealed class ChdHddGeometry
{
    public int SectorSize { get; set; } = 512;
    public long Cylinders { get; set; }
    public int Heads { get; set; }
    public int Sectors { get; set; }
    public string Mode { get; set; } = "canonical";

    public string ChdmanArguments => "-ss " + SectorSize + " -chs " + Cylinders + "," + Heads + "," + Sectors;

    public static ChdHddGeometry Resolve(long byteLength, ChdStorageProfile storage, ChdHddGeometryMode configured)
    {
        if (byteLength <= 0 || byteLength % 512 != 0)
            throw new ArgumentException("Hard-disk payload must contain whole 512-byte sectors.");
        long total = byteLength / 512;
        ChdHddGeometryMode mode = configured;
        if (mode == ChdHddGeometryMode.Auto || mode == ChdHddGeometryMode.Preserve)
            mode = storage == ChdStorageProfile.Playback ? ChdHddGeometryMode.Compatible : ChdHddGeometryMode.Canonical;
        if (mode == ChdHddGeometryMode.Compatible)
        {
            int[,] candidates = { { 16, 63 }, { 16, 32 }, { 4, 17 } };
            for (int i = 0; i < candidates.GetLength(0); i++)
            {
                int heads = candidates[i, 0];
                int sectors = candidates[i, 1];
                long divisor = heads * sectors;
                if (total % divisor == 0 && total / divisor <= int.MaxValue)
                    return new ChdHddGeometry { Cylinders = total / divisor, Heads = heads, Sectors = sectors, Mode = "compatible" };
            }
        }
        return new ChdHddGeometry { Cylinders = total, Heads = 1, Sectors = 1, Mode = mode == ChdHddGeometryMode.Compatible ? "compatible-exact" : "canonical" };
    }

    public static bool TryRead(string chdPath, out ChdHddGeometry geometry)
    {
        geometry = null;
        if (!ChdMetadata.TryReadTextMetadata(chdPath, "GDDD", 0, out string text, out _))
            return false;
        Match c = Regex.Match(text ?? "", @"CYLS\s*[:=]\s*(\d+)", RegexOptions.IgnoreCase);
        Match h = Regex.Match(text ?? "", @"HEADS\s*[:=]\s*(\d+)", RegexOptions.IgnoreCase);
        Match s = Regex.Match(text ?? "", @"SECS\s*[:=]\s*(\d+)", RegexOptions.IgnoreCase);
        Match b = Regex.Match(text ?? "", @"BPS\s*[:=]\s*(\d+)", RegexOptions.IgnoreCase);
        if (!c.Success || !h.Success || !s.Success || !long.TryParse(c.Groups[1].Value, out long cylinders) ||
            !int.TryParse(h.Groups[1].Value, out int heads) || !int.TryParse(s.Groups[1].Value, out int sectors))
            return false;
        int sectorSize = b.Success && int.TryParse(b.Groups[1].Value, out int parsedBps) ? parsedBps : 512;
        geometry = new ChdHddGeometry { Cylinders = cylinders, Heads = heads, Sectors = sectors, SectorSize = sectorSize, Mode = "preserved" };
        return cylinders > 0 && heads > 0 && sectors > 0 && sectorSize > 0;
    }

    public static bool Matches(string chdPath, ChdEncodingProfileSpec profile)
    {
        if (profile == null || profile.Family != "hdd") return true;
        return TryRead(chdPath, out ChdHddGeometry actual) && actual.SectorSize == profile.HddSectorSize &&
               actual.Cylinders == profile.HddCylinders && actual.Heads == profile.HddHeads && actual.Sectors == profile.HddSectors;
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        try
        {
            ChdHddGeometry playback = Resolve(1024L * 16 * 63 * 512, ChdStorageProfile.Playback, ChdHddGeometryMode.Auto);
            ChdHddGeometry archive = Resolve(1024L * 16 * 63 * 512, ChdStorageProfile.Archive, ChdHddGeometryMode.Auto);
            if (playback.Cylinders != 1024 || playback.Heads != 16 || playback.Sectors != 63 ||
                archive.Cylinders != 1024L * 16 * 63 || archive.Heads != 1 || archive.Sectors != 1)
                throw new InvalidOperationException("HDD geometry profiles are not exact.");
            ChdHddGeometry odd = Resolve(997 * 512L, ChdStorageProfile.Playback, ChdHddGeometryMode.Compatible);
            if (odd.Cylinders * odd.Heads * odd.Sectors != 997)
                throw new InvalidOperationException("Fallback HDD geometry changes payload size.");
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
}
