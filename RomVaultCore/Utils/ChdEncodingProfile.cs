using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        if (ProfileRevision != ChdEncodingProfile.CurrentProfileRevision || !ChdEncodingProfile.IsStorage(Storage))
            throw new InvalidOperationException("CHD encoder profile identity is invalid.");
        int writerRevision = identity?.Capabilities?.WriterRevision(Family) ?? 0;
        StringBuilder metadata = new StringBuilder()
            .Append("schema=").Append(ChdEncodingProfile.CurrentProfileSchema.ToString(CultureInfo.InvariantCulture))
            .Append(";profile=").Append(ChdEncodingProfile.ProfileId)
            .Append(";revision=").Append(ProfileRevision.ToString(CultureInfo.InvariantCulture))
            .Append(";storage=").Append(Storage.ToLowerInvariant());

        if (writerRevision > 0)
            metadata.Append(";writer=").Append(writerRevision.ToString(CultureInfo.InvariantCulture));
        if (Version.TryParse(identity?.VersionText, out Version chdmanVersion))
            metadata.Append(";chdman=").Append(chdmanVersion.ToString());
        if (ChdEncodingProfile.IsSha256(identity?.BinarySha256))
            metadata.Append(";toolsha256=").Append(identity.BinarySha256.ToLowerInvariant());
        return metadata.ToString();
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
    // RVEP does not serialize these physical facts; TryRead hydrates them
    // from RVRM/native CHD metadata.
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

        if (!TryParseMetadata(text, out profile))
            return TryParseUnsupportedSchemaEnvelope(text, out profile);

        PopulateNativeFacts(chdPath, profile);
        return true;
    }

    internal static bool TryParseMetadata(string text, out ChdEncodingProfile profile)
    {
        profile = null;
        if (!TryTokenizeMetadata(text, out Dictionary<string, string> values))
            return false;

        if (!values.TryGetValue("schema", out string schemaText) || !int.TryParse(schemaText, NumberStyles.None, CultureInfo.InvariantCulture, out int schema) ||
            !values.TryGetValue("profile", out string profileId) ||
            !values.TryGetValue("revision", out string revisionText) || !int.TryParse(revisionText, NumberStyles.None, CultureInfo.InvariantCulture, out int revision) || revision <= 0 ||
            !values.TryGetValue("storage", out string storage) || !IsStorage(storage))
        {
            return false;
        }
        if (schema != CurrentProfileSchema ||
            !string.Equals(profileId, ProfileId, StringComparison.Ordinal))
            return false;

        if (!ContainsOnlyKnownFields(values))
            return false;

        string chdmanText = "";
        Version chdmanVersion = null;
        if (values.TryGetValue("chdman", out string parsedChdmanText))
        {
            if (!Version.TryParse(parsedChdmanText, out chdmanVersion))
                return false;
            chdmanText = parsedChdmanText;
        }

        int writerRevision = 0;
        if (values.TryGetValue("writer", out string writerText) &&
            (!int.TryParse(writerText, NumberStyles.None, CultureInfo.InvariantCulture, out writerRevision) || writerRevision < 0 ||
             writerRevision == 0))
            return false;

        string toolSha256 = "";
        if (values.TryGetValue("toolsha256", out string parsedToolSha256))
        {
            if (!IsSha256(parsedToolSha256))
                return false;
            toolSha256 = parsedToolSha256;
        }

        profile = new ChdEncodingProfile
        {
            Schema = schema,
            Profile = profileId,
            ChdmanText = chdmanText,
            ChdmanVersion = chdmanVersion,
            Family = "",
            Storage = storage.ToLowerInvariant(),
            Codecs = "",
            HunkSize = 0,
            UnitSize = 0,
            ProfileRevision = revision,
            WriterRevision = writerRevision,
            ToolSha256 = toolSha256.ToLowerInvariant()
        };
        return true;
    }

    // Unknown schema numbers are not part of the current wire contract. When
    // one is embedded in a CHD, retain only enough of its envelope to make
    // policy callers fail closed instead of treating it as missing metadata
    // and rewriting the CHD with today's rules.
    private static bool TryParseUnsupportedSchemaEnvelope(string text, out ChdEncodingProfile profile)
    {
        profile = null;
        if (!TryTokenizeMetadata(text, out Dictionary<string, string> values) ||
            !values.TryGetValue("schema", out string schemaText) ||
            !int.TryParse(schemaText, NumberStyles.None, CultureInfo.InvariantCulture, out int schema) ||
            schema <= 0 || schema == CurrentProfileSchema)
            return false;

        int revision = 0;
        if (values.TryGetValue("revision", out string revisionText))
            int.TryParse(revisionText, NumberStyles.None, CultureInfo.InvariantCulture, out revision);

        profile = new ChdEncodingProfile
        {
            Schema = schema,
            Profile = values.TryGetValue("profile", out string profileId) ? profileId : "",
            Storage = values.TryGetValue("storage", out string storage) ? storage.ToLowerInvariant() : "",
            ProfileRevision = revision
        };
        return true;
    }

    private static bool TryTokenizeMetadata(string text, out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] fields = (text ?? "").Split(new[] { ';' }, StringSplitOptions.None);
        for (int i = 0; i < fields.Length; i++)
        {
            int equals = fields[i].IndexOf('=');
            if (equals <= 0)
                return false;
            string key = fields[i].Substring(0, equals).Trim();
            string value = fields[i].Substring(equals + 1).Trim();
            if (key.Length == 0 || values.ContainsKey(key))
                return false;
            values.Add(key, value);
        }
        return true;
    }

    private static bool ContainsOnlyKnownFields(Dictionary<string, string> values)
    {
        foreach (string key in values.Keys)
        {
            switch (key.ToLowerInvariant())
            {
                case "schema":
                case "profile":
                case "revision":
                case "storage":
                case "writer":
                case "chdman":
                case "toolsha256":
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    // Keep parsing separate from policy interpretation.  A future RVEP
    // revision must remain visible to diagnostics, but this build must never
    // apply today's codecs, hunk sizes, or geometry rules to it.
    internal static bool CanUseCurrentPhysicalPolicy(ChdEncodingProfile profile, out string error)
    {
        error = "";
        if (profile == null)
        {
            error = "CHD has no valid RomVault encoder profile.";
            return false;
        }
        if (profile.Schema != CurrentProfileSchema)
        {
            error = $"CHD uses unsupported RomVault encoder profile schema {profile.Schema}; " +
                    $"this build supports schema {CurrentProfileSchema}. The CHD was left unchanged.";
            return false;
        }
        if (profile.ProfileRevision != CurrentProfileRevision)
        {
            error = $"CHD uses unsupported RomVault encoder profile revision {profile.ProfileRevision}; " +
                    $"this build supports revision {CurrentProfileRevision}. The CHD was left unchanged.";
            return false;
        }
        return true;
    }

    private static void PopulateNativeFacts(string chdPath, ChdEncodingProfile profile)
    {
        if (profile == null || !ChdMetadata.TryReadContainerInfo(chdPath, out ChdContainerInfo container, out _))
            return;

        string legacyFamily = profile.Family;
        profile.Codecs = NormalizeCodecs(string.Join(",", container.Codecs));
        profile.HunkSize = (int)container.HunkSize;
        profile.UnitSize = (int)container.UnitSize;
        profile.HddSectorSize = 0;
        profile.HddCylinders = 0;
        profile.HddHeads = 0;
        profile.HddSectors = 0;
        profile.HddGeometryMode = "";

        if (ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest manifest, out _) &&
            !string.IsNullOrWhiteSpace(manifest.Family))
        {
            profile.Family = manifest.Family.Trim().ToLowerInvariant();
        }
        else
        {
            profile.Family = DetectNativeFamily(chdPath, container, legacyFamily);
        }

        if (profile.Family == "hdd" && ChdHddGeometry.TryRead(chdPath, out ChdHddGeometry geometry))
        {
            profile.HddSectorSize = geometry.SectorSize;
            profile.HddCylinders = geometry.Cylinders;
            profile.HddHeads = geometry.Heads;
            profile.HddSectors = geometry.Sectors;
            profile.HddGeometryMode = geometry.Mode;
        }
    }

    private static string DetectNativeFamily(string chdPath, ChdContainerInfo container, string legacyFamily)
    {
        bool hasCdCodec = false;
        bool hasAvCodec = false;
        for (int i = 0; i < (container?.Codecs?.Count ?? 0); i++)
        {
            string codec = container.Codecs[i] ?? "";
            hasCdCodec |= codec.StartsWith("cd", StringComparison.OrdinalIgnoreCase);
            hasAvCodec |= string.Equals(codec, "avhu", StringComparison.OrdinalIgnoreCase);
        }

        bool isGdRom = false;
        bool hasCdTracks = ChdMetadata.TryReadCdTrackLayout(chdPath, out _, out isGdRom, out _);
        if (hasCdTracks || hasCdCodec)
            return isGdRom ? "gdi" : "cd";
        if (hasAvCodec || ChdMetadata.TryReadBinaryMetadata(chdPath, "AVAV", 0, out _, out _))
            return "laserdisc";
        if (ChdMetadata.TryReadBinaryMetadata(chdPath, "GDDD", 0, out _, out _))
            return "hdd";
        if (ChdMetadata.TryReadBinaryMetadata(chdPath, "DVD ", 0, out _, out _))
            return string.Equals(legacyFamily, "psp", StringComparison.OrdinalIgnoreCase) ? "psp" : "dvd";
        return "raw";
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

        bool hasStoredProfile = TryRead(chdPath, out ChdEncodingProfile stored);
        if (hasStoredProfile && !CanUseCurrentPhysicalPolicy(stored, out error))
            return false;

        if (hasStoredProfile && !string.IsNullOrWhiteSpace(stored.Family))
        {
            string storedFamily = psp && stored.Family == "dvd" ? "psp" : stored.Family;
            spec = ForFamily(storedFamily, storageProfile,
                storedFamily == "laserdisc" ? (int)container.HunkSize : 0,
                storedFamily == "laserdisc" ? (int)container.UnitSize : 0);
            if (storedFamily == "hdd")
                ApplyHddGeometry(spec, (long)container.LogicalSize, storageProfile, Settings.rvSettings?.ChdHddGeometry ?? ChdHddGeometryMode.Auto);
            if (storedFamily == "cd" && ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest storedManifest, out _))
                ApplyDialect(spec, storedManifest.Dialect);
            return true;
        }

        string family = DetectNativeFamily(chdPath, container, psp ? "psp" : "");
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
        if (hasProfile && !CanUseCurrentPhysicalPolicy(stored, out reason))
            return false;
        if (hasProfile && stored.ChdmanVersion != null && installed?.Version != null &&
            stored.ChdmanVersion.CompareTo(installed.Version) > 0)
        {
            reason = $"CHD was encoded by newer chdman {stored.ChdmanText}.";
            return false;
        }

        if (!hasProfile)
        {
            reason = "CHD has no valid RomVault encoder profile.";
            return true;
        }

        string actualCodecs = NormalizeCodecs(string.Join(",", actual?.Codecs ?? new List<string>()));
        string expectedCodecs = NormalizeCodecs(expected.Codecs);
        if (stored.Schema != CurrentProfileSchema ||
            !string.Equals(stored.Profile, ProfileId, StringComparison.Ordinal) ||
            stored.ProfileRevision != expected.ProfileRevision ||
            !string.Equals(stored.Storage, expected.Storage, StringComparison.Ordinal) ||
            actual == null ||
            actual.HunkSize != expected.HunkSize ||
            (expected.UnitSize > 0 && actual.UnitSize != expected.UnitSize) ||
            !string.Equals(actualCodecs, expectedCodecs, StringComparison.Ordinal))
        {
            reason = "CHD does not match the standard RomVault encoding profile.";
            return true;
        }
        if (expected.Family == "hdd" &&
            !ChdHddGeometry.Matches(chdPath, expected))
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
            !string.Equals(manifest.Family, expected.Family, StringComparison.Ordinal) ||
            manifest.Tracks == null || manifest.Tracks.Count == 0)
        {
            reason = "CHD reconstruction metadata is incomplete or does not match the media family.";
            return true;
        }
        if (!ChdReconstructionManifest.HasCompleteOpticalIdentity(manifest, out string descriptorError))
        {
            reason = descriptorError;
            return true;
        }

        int installedWriterRevision = installed?.Capabilities?.WriterRevision(expected.Family) ?? 0;
        if (stored.WriterRevision > 0 && installedWriterRevision > stored.WriterRevision)
        {
            reason = $"Installed chdman {installed.VersionText} has a newer validated {expected.Family.ToUpperInvariant()} writer revision.";
            return true;
        }

        if (Settings.rvSettings?.ChdRecompressOnEncoderUpdate == true && stored.ChdmanVersion != null && installed?.Version != null &&
            stored.ChdmanVersion.CompareTo(installed.Version) < 0)
        {
            reason = $"Installed chdman {installed.VersionText} is newer than encoder {stored.ChdmanText}, and full encoder upgrades are enabled.";
            return true;
        }

        return false;
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        try
        {
            string sha256 = new string('A', 64);
            ChdEncodingProfileSpec spec = ForFamily("hdd", ChdStorageProfile.Archive);
            ChdmanIdentity identity = new ChdmanIdentity
            {
                VersionText = "0.289",
                Version = new Version(0, 289),
                BinarySha256 = sha256,
                Capabilities = new ChdmanCapabilities { HddArchiveRoundTrip = true }
            };
            string encoded = spec.ToMetadata(identity);
            string expected = "schema=1;profile=rvworld-v1;revision=1;storage=archive;writer=1;chdman=0.289;toolsha256=" + sha256.ToLowerInvariant();
            if (!string.Equals(encoded, expected, StringComparison.Ordinal) ||
                encoded.Contains(";family=", StringComparison.Ordinal) ||
                encoded.Contains(";codecs=", StringComparison.Ordinal) ||
                encoded.Contains(";hunk=", StringComparison.Ordinal) ||
                encoded.Contains(";unit=", StringComparison.Ordinal) ||
                encoded.Contains(";sector=", StringComparison.Ordinal))
                throw new InvalidDataException("RVEP schema 1 is not canonical and slim.");

            if (!TryParseMetadata(encoded, out ChdEncodingProfile parsed) || parsed.Schema != 1 ||
                parsed.ProfileRevision != 1 || parsed.Storage != "archive" || parsed.WriterRevision != 1 ||
                parsed.ChdmanVersion != new Version(0, 289) || parsed.ToolSha256 != sha256.ToLowerInvariant())
                throw new InvalidDataException("Canonical RVEP schema 1 did not round trip.");

            string minimum = "schema=1;profile=rvworld-v1;revision=1;storage=playback";
            if (!TryParseMetadata(minimum, out ChdEncodingProfile minimal) || minimal.ChdmanVersion != null ||
                minimal.WriterRevision != 0 || minimal.ToolSha256.Length != 0)
                throw new InvalidDataException("Minimum RVEP schema 1 metadata was rejected.");
            if (!CanUseCurrentPhysicalPolicy(minimal, out string currentPolicyError))
                throw new InvalidDataException("Current RVEP revision was rejected: " + currentPolicyError);

            for (int futureRevision = CurrentProfileRevision + 1; futureRevision <= CurrentProfileRevision + 2; futureRevision++)
            {
                string future = "schema=1;profile=rvworld-v1;revision=" + futureRevision.ToString(CultureInfo.InvariantCulture) + ";storage=playback";
                if (!TryParseMetadata(future, out ChdEncodingProfile futureProfile) || futureProfile.ProfileRevision != futureRevision)
                    throw new InvalidDataException("A future RVEP revision could not be distinguished by diagnostics.");
                if (CanUseCurrentPhysicalPolicy(futureProfile, out string futureError) ||
                    string.IsNullOrWhiteSpace(futureError) || !futureError.Contains(futureRevision.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                    throw new InvalidDataException("A future RVEP revision was interpreted with the current physical policy.");
            }

            string oldFullRecord = "schema=1;profile=rvworld-v1;revision=1;writer=0;chdman=0.289;toolsha256=;family=hdd;storage=archive;codecs=lzma;hunk=1048576;unit=512;sector=512;cylinders=10;heads=1;sectors=1;geometry=canonical";
            AssertMetadataRejected(oldFullRecord, "the retired full schema-1 shape");

            spec.ProfileRevision = CurrentProfileRevision + 1;
            try
            {
                spec.ToMetadata(identity);
                throw new InvalidDataException("The current writer emitted an unsupported RVEP profile revision.");
            }
            catch (InvalidOperationException)
            {
            }

            AssertMetadataRejected(minimum + ";STORAGE=archive", "duplicate key");
            AssertMetadataRejected("schema=1;profile=rvworld-v1;revision=1;storage=cold", "invalid storage");
            AssertMetadataRejected(minimum + ";toolsha256=abc", "invalid tool hash");
            AssertMetadataRejected(minimum + ";chdman=not-a-version", "invalid tool version");
            AssertMetadataRejected(minimum + ";writer=0", "invalid writer revision");
            AssertMetadataRejected(minimum + ";family=hdd", "retired physical fields");
            AssertMetadataRejected(minimum + ";unknown=value", "unknown fields");
            AssertMetadataRejected(minimum + ";", "a trailing empty field");
            int futureSchema = CurrentProfileSchema + 1;
            string unsupportedSchema = "schema=" + futureSchema.ToString(CultureInfo.InvariantCulture) +
                                       ";profile=rvworld-v1;revision=1;storage=archive;future=value";
            AssertMetadataRejected(unsupportedSchema, "unsupported schema");
            if (!TryParseUnsupportedSchemaEnvelope(unsupportedSchema, out ChdEncodingProfile unsupported) ||
                unsupported.Schema != futureSchema || CanUseCurrentPhysicalPolicy(unsupported, out string unsupportedError) ||
                string.IsNullOrWhiteSpace(unsupportedError) ||
                !unsupportedError.Contains("schema " + futureSchema.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                throw new InvalidDataException("A future RVEP schema was not retained as a fail-closed envelope.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void AssertMetadataRejected(string metadata, string description)
    {
        if (TryParseMetadata(metadata, out _))
            throw new InvalidDataException("RVEP accepted " + description + ".");
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

    internal static bool IsStorage(string storage)
    {
        return string.Equals(storage, "playback", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(storage, "archive", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }
}
