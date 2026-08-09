using System;
using System.Collections.Generic;
using CHDSharpLib;

namespace RomVaultCore.Utils;

internal sealed class ChdEncodingProfileSpec
{
    public string Family { get; set; }
    public string Storage { get; set; }
    public string Codecs { get; set; }
    public int HunkSize { get; set; }
    public int UnitSize { get; set; }
    public int ProfileRevision { get; set; } = ChdEncodingProfile.CurrentProfileRevision;
    public int HddSectorSize { get; set; }
    public long HddCylinders { get; set; }
    public int HddHeads { get; set; }
    public int HddSectors { get; set; }
    public string HddGeometryMode { get; set; } = "";

    public string ToMetadata(ChdmanIdentity identity)
    {
        int writerRevision = identity?.Capabilities?.WriterRevision(Family) ?? 0;
        string geometry = Family == "hdd" ? $";sector={HddSectorSize};cylinders={HddCylinders};heads={HddHeads};sectors={HddSectors};geometry={HddGeometryMode}" : "";
        return $"schema={ChdEncodingProfile.CurrentProfileSchema};profile={ChdEncodingProfile.ProfileId};revision={ProfileRevision};writer={writerRevision};chdman={identity?.VersionText ?? ""};toolsha256={identity?.BinarySha256 ?? ""};family={Family};storage={Storage};codecs={Codecs};hunk={HunkSize};unit={UnitSize}{geometry}";
    }
}

internal sealed class ChdEncodingProfile
{
    public const string MetadataTag = "RVEP";
    public const string ProfileId = "rvworld-v1";
    public const int CurrentProfileRevision = 1;
    public const int CurrentProfileSchema = 1;

    public int Schema { get; private set; }
    public string Profile { get; private set; }
    public string ChdmanText { get; private set; }
    public Version ChdmanVersion { get; private set; }
    public string Family { get; private set; }
    public string Storage { get; private set; }
    public string Codecs { get; private set; }
    public int HunkSize { get; private set; }
    public int UnitSize { get; private set; }
    public int ProfileRevision { get; private set; }
    public int WriterRevision { get; private set; }
    public string ToolSha256 { get; private set; }
    public int HddSectorSize { get; private set; }
    public long HddCylinders { get; private set; }
    public int HddHeads { get; private set; }
    public int HddSectors { get; private set; }
    public string HddGeometryMode { get; private set; }

    public static ChdEncodingProfileSpec ForFamily(string family, ChdStorageProfile storageProfile, int dynamicHunkSize = 0, int dynamicUnitSize = 0)
    {
        string normalized = (family ?? "").Trim().ToLowerInvariant();
        bool playback = storageProfile == ChdStorageProfile.Playback;
        string storage = playback ? "playback" : "archive";
        switch (normalized)
        {
            case "cd":
            case "gdi":
                return new ChdEncodingProfileSpec
                {
                    Family = normalized,
                    Storage = storage,
                    Codecs = playback ? "cdzs,cdzl,cdfl" : "cdlz,cdzl,cdfl",
                    HunkSize = playback ? 19584 : 1047744,
                    UnitSize = 2448
                };
            case "psp":
                return new ChdEncodingProfileSpec
                {
                    Family = normalized,
                    Storage = storage,
                    Codecs = playback ? "zstd" : "lzma",
                    HunkSize = playback ? 2048 : 1048576,
                    UnitSize = 2048
                };
            case "raw":
                return new ChdEncodingProfileSpec
                {
                    Family = normalized,
                    Storage = storage,
                    Codecs = playback ? "zstd" : "lzma",
                    HunkSize = playback ? 4096 : 1048576,
                    UnitSize = 1
                };
            case "hdd":
                return new ChdEncodingProfileSpec
                {
                    Family = normalized,
                    Storage = storage,
                    Codecs = playback ? "zstd" : "lzma",
                    HunkSize = playback ? 4096 : 1048576,
                    UnitSize = 512
                };
            case "laserdisc":
                return new ChdEncodingProfileSpec
                {
                    Family = normalized,
                    Storage = storage,
                    Codecs = "avhu",
                    HunkSize = dynamicHunkSize,
                    UnitSize = dynamicUnitSize > 0 ? dynamicUnitSize : dynamicHunkSize
                };
            default:
                return new ChdEncodingProfileSpec
                {
                    Family = "dvd",
                    Storage = storage,
                    Codecs = playback ? "zstd" : "lzma",
                    HunkSize = playback ? 4096 : 1048576,
                    UnitSize = 2048
                };
        }
    }

    public static ChdEncodingProfileSpec ApplyHddGeometry(ChdEncodingProfileSpec spec, long byteLength, ChdStorageProfile storageProfile, ChdHddGeometryMode mode)
    {
        if (spec == null || spec.Family != "hdd") return spec;
        ChdHddGeometry geometry = ChdHddGeometry.Resolve(byteLength, storageProfile, mode);
        spec.HddSectorSize = geometry.SectorSize;
        spec.HddCylinders = geometry.Cylinders;
        spec.HddHeads = geometry.Heads;
        spec.HddSectors = geometry.Sectors;
        spec.HddGeometryMode = geometry.Mode;
        return spec;
    }

    public static ChdEncodingProfileSpec ApplyDialect(ChdEncodingProfileSpec spec, string dialect)
    {
        if (spec == null || spec.Storage != "archive" || spec.Family != "cd")
            return spec;
        string value = (dialect ?? "").ToLowerInvariant();
        // chdman 0.289's CD-LZMA codec does not decode MODE1/2048 or
        // MODE2/2352 fixtures reliably.  The FLAC/deflate pair remains exact;
        // retain the large archive hunk while omitting only the unsafe codec.
        if (value.Contains("mode1-2048") || value.Contains("mode2-2352") || value == "cue-mixed" || value == "toc-exact" ||
            value == "cue-complex-layout" || value == "cue-unicode")
            spec.Codecs = "cdzl,cdfl";
        if (value.Contains("mode1-2048"))
            spec.HunkSize = 19584;
        if (value == "cue-unicode")
            spec.HunkSize = 19584;
        return spec;
    }

    public static bool TryRead(string chdPath, out ChdEncodingProfile profile)
    {
        profile = null;
        if (!ChdMetadata.TryReadTextMetadata(chdPath, MetadataTag, 0, out string text, out _))
            return false;

        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] fields = (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < fields.Length; i++)
        {
            int equals = fields[i].IndexOf('=');
            if (equals <= 0 || equals == fields[i].Length - 1)
                continue;
            values[fields[i].Substring(0, equals).Trim()] = fields[i].Substring(equals + 1).Trim();
        }

        if (!values.TryGetValue("schema", out string schemaText) || !int.TryParse(schemaText, out int schema) ||
            !values.TryGetValue("profile", out string profileId) ||
            !values.TryGetValue("chdman", out string chdmanText) || !Version.TryParse(chdmanText, out Version chdmanVersion) ||
            !values.TryGetValue("family", out string family) ||
            !values.TryGetValue("codecs", out string codecs) ||
            !values.TryGetValue("hunk", out string hunkText) || !int.TryParse(hunkText, out int hunkSize))
        {
            return false;
        }
        if (schema != CurrentProfileSchema || !string.Equals(profileId, ProfileId, StringComparison.Ordinal))
            return false;

        profile = new ChdEncodingProfile
        {
            Schema = schema,
            Profile = profileId,
            ChdmanText = chdmanText,
            ChdmanVersion = chdmanVersion,
            Family = family.ToLowerInvariant(),
            Storage = values.TryGetValue("storage", out string storage) ? storage.ToLowerInvariant() : "",
            Codecs = NormalizeCodecs(codecs),
            HunkSize = hunkSize,
            UnitSize = values.TryGetValue("unit", out string unitText) && int.TryParse(unitText, out int unitSize) ? unitSize : 0
        };
        if (values.TryGetValue("revision", out string revisionText) && int.TryParse(revisionText, out int revision))
            profile.ProfileRevision = revision;
        if (values.TryGetValue("writer", out string writerText) && int.TryParse(writerText, out int writer))
            profile.WriterRevision = writer;
        if (values.TryGetValue("toolsha256", out string toolSha256))
            profile.ToolSha256 = toolSha256;
        if (values.TryGetValue("sector", out string sectorText) && int.TryParse(sectorText, out int sector)) profile.HddSectorSize = sector;
        if (values.TryGetValue("cylinders", out string cylinderText) && long.TryParse(cylinderText, out long cylinders)) profile.HddCylinders = cylinders;
        if (values.TryGetValue("heads", out string headsText) && int.TryParse(headsText, out int heads)) profile.HddHeads = heads;
        if (values.TryGetValue("sectors", out string sectorsText) && int.TryParse(sectorsText, out int sectors)) profile.HddSectors = sectors;
        if (values.TryGetValue("geometry", out string geometry)) profile.HddGeometryMode = geometry;
        return true;
    }

    public static bool TryDescribeExisting(string chdPath, bool psp, ChdStorageProfile storageProfile, out ChdEncodingProfileSpec spec, out ChdContainerInfo container, out string error)
    {
        spec = null;
        if (!ChdMetadata.TryReadContainerInfo(chdPath, out container, out error))
            return false;
        if (container.Version != 5)
        {
            error = $"CHD is not V5 (found V{container.Version}).";
            return false;
        }

        if (TryRead(chdPath, out ChdEncodingProfile stored) && !string.IsNullOrWhiteSpace(stored.Family))
        {
            spec = ForFamily(stored.Family, storageProfile,
                stored.Family == "laserdisc" ? (int)container.HunkSize : 0,
                stored.Family == "laserdisc" ? (int)container.UnitSize : 0);
            if (stored.Family == "hdd")
                ApplyHddGeometry(spec, (long)container.LogicalSize, storageProfile, Settings.rvSettings?.ChdHddGeometry ?? ChdHddGeometryMode.Auto);
            if (stored.Family == "cd" && ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest storedManifest, out _))
                ApplyDialect(spec, storedManifest.Dialect);
            return true;
        }

        bool hasCdCodec = false;
        bool hasAvCodec = false;
        for (int i = 0; i < container.Codecs.Count; i++)
        {
            if (container.Codecs[i].StartsWith("cd", StringComparison.OrdinalIgnoreCase))
            {
                hasCdCodec = true;
                break;
            }
            if (string.Equals(container.Codecs[i], "avhu", StringComparison.OrdinalIgnoreCase))
                hasAvCodec = true;
        }

        bool isGdRom = false;
        bool hasCdTracks = ChdMetadata.TryReadCdTrackLayout(chdPath, out _, out isGdRom, out _);
        string family;
        if (hasCdTracks || hasCdCodec)
            family = isGdRom ? "gdi" : "cd";
        else if (hasAvCodec || ChdMetadata.TryReadBinaryMetadata(chdPath, "AVAV", 0, out _, out _))
            family = "laserdisc";
        else if (ChdMetadata.TryReadBinaryMetadata(chdPath, "GDDD", 0, out _, out _))
            family = "hdd";
        else if (ChdMetadata.TryReadBinaryMetadata(chdPath, "DVD ", 0, out _, out _))
            family = psp ? "psp" : "dvd";
        else
            family = "raw";
        spec = ForFamily(family, storageProfile,
            family == "laserdisc" ? (int)container.HunkSize : 0,
            family == "laserdisc" ? (int)container.UnitSize : 0);
        if (family == "hdd")
            ApplyHddGeometry(spec, (long)container.LogicalSize, storageProfile, Settings.rvSettings?.ChdHddGeometry ?? ChdHddGeometryMode.Auto);
        if (family == "cd" && ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest manifest, out _))
            ApplyDialect(spec, manifest.Dialect);
        return true;
    }

    public static bool NeedsRecompression(string chdPath, ChdEncodingProfileSpec expected, ChdContainerInfo actual, ChdmanIdentity installed, out string reason)
    {
        reason = "";
        bool hasProfile = TryRead(chdPath, out ChdEncodingProfile stored);
        if (hasProfile && stored.ChdmanVersion.CompareTo(installed.Version) > 0)
        {
            reason = $"CHD was encoded by newer chdman {stored.ChdmanText}.";
            return false;
        }

        if (!hasProfile)
        {
            reason = "CHD has no valid RomVault encoder profile.";
            return true;
        }

        string actualCodecs = NormalizeCodecs(string.Join(",", actual.Codecs));
        string expectedCodecs = NormalizeCodecs(expected.Codecs);
        if (stored.Schema != CurrentProfileSchema ||
            !string.Equals(stored.Profile, ProfileId, StringComparison.Ordinal) ||
            stored.ProfileRevision != expected.ProfileRevision ||
            !string.Equals(stored.Family, expected.Family, StringComparison.Ordinal) ||
            !string.Equals(stored.Storage, expected.Storage, StringComparison.Ordinal) ||
            !string.Equals(stored.Codecs, expectedCodecs, StringComparison.Ordinal) ||
            stored.HunkSize != expected.HunkSize ||
            stored.UnitSize != expected.UnitSize ||
            actual.HunkSize != expected.HunkSize ||
            (expected.UnitSize > 0 && actual.UnitSize != expected.UnitSize) ||
            !string.Equals(actualCodecs, expectedCodecs, StringComparison.Ordinal))
        {
            reason = "CHD does not match the standard RomVault encoding profile.";
            return true;
        }
        if (expected.Family == "hdd" &&
            (stored.HddSectorSize != expected.HddSectorSize || stored.HddCylinders != expected.HddCylinders ||
             stored.HddHeads != expected.HddHeads || stored.HddSectors != expected.HddSectors ||
             !ChdHddGeometry.Matches(chdPath, expected)))
        {
            reason = "Hard-disk CHD geometry does not match the selected playback/archive profile.";
            return true;
        }

        if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest manifest, out _))
        {
            reason = "CHD has no valid embedded reconstruction manifest.";
            return true;
        }
        if (manifest.Schema != ChdReconstructionManifest.CurrentSchema ||
            manifest.ProfileRevision != expected.ProfileRevision ||
            !string.Equals(manifest.ProfileId, ProfileId, StringComparison.Ordinal) ||
            !string.Equals(manifest.Family, expected.Family, StringComparison.Ordinal) ||
            !string.Equals(manifest.Storage, expected.Storage, StringComparison.Ordinal))
        {
            reason = "CHD reconstruction metadata does not match the standard profile.";
            return true;
        }

        int installedWriterRevision = installed.Capabilities?.WriterRevision(expected.Family) ?? 0;
        if (installedWriterRevision > stored.WriterRevision || installedWriterRevision > manifest.WriterRevision)
        {
            reason = $"Installed chdman {installed.VersionText} has a newer validated {expected.Family.ToUpperInvariant()} writer revision.";
            return true;
        }

        if (Settings.rvSettings?.ChdRecompressOnEncoderUpdate == true && stored.ChdmanVersion.CompareTo(installed.Version) < 0)
        {
            reason = $"Installed chdman {installed.VersionText} is newer than encoder {stored.ChdmanText}, and full encoder upgrades are enabled.";
            return true;
        }

        return false;
    }

    public static int GreatestCommonDivisor(int left, int right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);
        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }
        return left;
    }

    private static string NormalizeCodecs(string codecs)
    {
        return (codecs ?? "").Replace(" ", "").Trim().ToLowerInvariant();
    }
}
