using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CHDSharpLib;
using RomVaultCore.RvDB;

namespace RomVaultCore.Utils;

internal sealed class ChdManifestTrack
{
    public int Number { get; set; }
    // Transient operation label. The compact wire layout never serializes it;
    // deserialization supplies a fixed internal ordinal instead.
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public byte[] Crc32 { get; set; }
    public byte[] Sha1 { get; set; }
    public byte[] Md5 { get; set; }
    public byte[] Sha256 { get; set; }
}

internal sealed class ChdManifestView
{
    public string Name { get; set; } = "";
    public string Dialect { get; set; } = "";
    // Transient descriptor fields are accepted only as create-time inputs.
    // They do not exist in the compact wire layout.
    public string DescriptorName { get; set; } = "";
    public byte[] DescriptorBytes { get; set; } = Array.Empty<byte>();
    public byte[] DescriptorSha256 { get; set; } = Array.Empty<byte>();
    public List<ChdManifestTrack> Tracks { get; set; } = new List<ChdManifestTrack>();
}

internal sealed class ChdManifestAuxiliary
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public byte[] Sha256 { get; set; } = Array.Empty<byte>();
}

internal sealed class ChdReconstructionManifest
{
    public const string MetadataTag = "RVRM";
    public const int CurrentSchema = 1;
    private const uint Magic = 0x5256524d; // RVRM
    private const int MaxManifestBytes = RvrmWireFormat.MaxManifestBytes;
    private const int MaxDescriptorBytes = 16 * 1024 * 1024;
    private const int MaxAuxiliaryBytes = RvrmWireFormat.MaxAuxiliaryBytes;
    private const int MaxTracks = 1000;
    private const int MaxViews = 16;
    private const int MaxAuxiliaries = 64;
    private const int IntegrityBytes = 32;

    public int Schema { get; set; } = CurrentSchema;
    public string ProfileId { get; set; } = ChdEncodingProfile.ProfileId;
    public int ProfileRevision { get; set; }
    public int WriterRevision { get; set; }
    public string Family { get; set; } = "";
    public string Storage { get; set; } = "";
    public string Dialect { get; set; } = "";
    public string CreateMode { get; set; } = "";
    public string ExtractMode { get; set; } = "";
    public string ChdmanVersion { get; set; } = "";
    public string ChdmanBanner { get; set; } = "";
    public string ChdmanSha256 { get; set; } = "";
    public string CapabilityFingerprint { get; set; } = "";
    // Transient descriptor fields. They do not exist in the compact wire
    // layout, and compact deserialization therefore leaves them empty.
    public string DescriptorName { get; set; } = "";
    public byte[] DescriptorBytes { get; set; } = Array.Empty<byte>();
    public byte[] DescriptorSha256 { get; set; } = Array.Empty<byte>();
    public List<ChdManifestTrack> Tracks { get; set; } = new List<ChdManifestTrack>();
    public List<ChdManifestView> Views { get; set; } = new List<ChdManifestView>();
    public List<ChdManifestAuxiliary> Auxiliaries { get; set; } = new List<ChdManifestAuxiliary>();
    public List<RvrmOpaqueSection> OpaqueSections { get; set; } = new List<RvrmOpaqueSection>();

    public static bool TryRead(string chdPath, out ChdReconstructionManifest manifest, out string error)
    {
        manifest = null;
        if (!ChdMetadata.TryReadBinaryMetadata(chdPath, MetadataTag, 0, out byte[] data, out error))
            return false;
        return TryDeserialize(data, out manifest, out error);
    }

    public static bool TryCreateFromSource(
        string inputPath,
        ChdEncodingProfileSpec profile,
        ChdmanIdentity identity,
        out ChdReconstructionManifest manifest,
        out string error)
    {
        manifest = null;
        error = "";
        try
        {
            string fullInput = Path.GetFullPath(inputPath);
            string extension = Path.GetExtension(fullInput).ToLowerInvariant();
            if (!ChdRoundTripInputPolicy.Validate(fullInput, profile?.Family, out error))
                return false;
            ChdReconstructionManifest created = CreateBase(profile, identity);
            created.DescriptorName = extension == ".cue" || extension == ".gdi" || extension == ".toc"
                ? Path.GetFileName(fullInput)
                : "";
            created.Dialect = ChdDialect.DetectSource(fullInput, profile.Family);
            created.CreateMode = ResolveCreateMode(profile.Family);
            created.ExtractMode = ResolveExtractMode(profile.Family);

            if (!string.IsNullOrWhiteSpace(created.DescriptorName))
            {
                long descriptorLength = new FileInfo(fullInput).Length;
                if (descriptorLength <= 0 || descriptorLength > MaxDescriptorBytes)
                {
                    error = "Disc descriptor is empty or exceeds the 16 MiB reconstruction limit.";
                    return false;
                }
                created.DescriptorBytes = File.ReadAllBytes(fullInput);
                created.DescriptorSha256 = HashBytes(created.DescriptorBytes, SHA256.Create());
            }

            // LibCrypt/SBI correction data is part of the disc representation,
            // not optional UI state.  Store it inside RVRM so a lone CHD can
            // always recreate the exact DAT member without a JSON/sidecar file.
            if (extension == ".cue" || extension == ".toc")
            {
                string sbiPath = Path.ChangeExtension(fullInput, ".sbi");
                if (File.Exists(sbiPath))
                {
                    long auxiliaryLength = new FileInfo(sbiPath).Length;
                    if (!RvrmWireFormat.IsSupportedAuxiliaryLength(auxiliaryLength))
                    {
                        error = "SBI reconstruction data is empty or exceeds the 15 MiB reconstruction limit.";
                        return false;
                    }
                    byte[] bytes = File.ReadAllBytes(sbiPath);
                    created.Auxiliaries.Add(new ChdManifestAuxiliary
                    {
                        Name = Path.GetFileName(sbiPath),
                        Role = "sbi-subchannel-correction",
                        Bytes = bytes,
                        Sha256 = HashBytes(bytes, SHA256.Create())
                    });
                }
            }

            List<(int number, string name)> members = GetSourceMembers(fullInput);
            if ((extension == ".cue" || extension == ".gdi" || extension == ".toc") && members.Count == 0)
            {
                error = "Disc descriptor contains no readable payload references.";
                return false;
            }
            if (extension == ".toc" && members.Count != 1)
            {
                error = "Exact TOC reconstruction currently requires one source payload file; keep this multi-file TOC unchanged until its layout passes a dedicated conformance fixture.";
                return false;
            }
            if (members.Count == 0 && File.Exists(fullInput))
                members.Add((1, Path.GetFileName(fullInput)));
            string directory = Path.GetDirectoryName(fullInput) ?? Environment.CurrentDirectory;
            for (int i = 0; i < members.Count; i++)
            {
                string safeName = NormalizeRelativeMemberName(members[i].name);
                string memberPath = Path.GetFullPath(Path.Combine(directory, safeName));
                if (!IsWithinDirectory(memberPath, directory) || !File.Exists(memberPath))
                {
                    error = "Manifest source member is missing or unsafe: " + members[i].name;
                    return false;
                }
                created.Tracks.Add(HashTrack(memberPath, members[i].number, safeName));
            }
            manifest = created;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static ChdReconstructionManifest CreateFromDat(
        RvFile destination,
        ChdEncodingProfileSpec profile,
        ChdmanIdentity identity,
        ChdReconstructionManifest previous = null)
    {
        ChdReconstructionManifest manifest = CreateBase(profile, identity);
        manifest.Dialect = ResolveDatDialect(destination, profile, previous);
        manifest.CreateMode = previous?.CreateMode ?? (profile.Family == "cd" || profile.Family == "gdi" ? "copy" : "copy");
        manifest.ExtractMode = previous?.ExtractMode ?? ResolveExtractMode(profile.Family);
        manifest.DescriptorName = previous?.DescriptorName ?? "";
        manifest.DescriptorBytes = previous?.DescriptorBytes ?? Array.Empty<byte>();
        manifest.DescriptorSha256 = previous?.DescriptorSha256 ?? Array.Empty<byte>();
        if (previous?.Views != null)
            manifest.Views.AddRange(previous.Views);
        if (previous?.Auxiliaries != null)
            manifest.Auxiliaries.AddRange(previous.Auxiliaries);
        if (previous?.OpaqueSections != null)
            manifest.OpaqueSections = CloneOpaqueSections(previous.OpaqueSections);

        if (destination != null)
        {
            int fallbackTrack = 1;
            HashSet<ChdManifestTrack> usedPreviousTracks = new HashSet<ChdManifestTrack>();
            for (int i = 0; i < destination.ChildCount; i++)
            {
                RvFile child = destination.Child(i);
                if (child == null || !child.IsFile || !IsPayloadName(child.Name, profile.Family))
                    continue;
                ChdManifestTrack previousTrack = FindMatchingTrack(previous?.Tracks, child, usedPreviousTracks);
                if (previousTrack != null)
                    usedPreviousTracks.Add(previousTrack);
                int number = previousTrack?.Number > 0 ? previousTrack.Number : fallbackTrack;
                fallbackTrack = Math.Max(fallbackTrack + 1, number + 1);
                manifest.Tracks.Add(new ChdManifestTrack
                {
                    Number = number,
                    // This name is transient and is canonicalized before RVRM is
                    // serialized.  It is retained here only for the current
                    // create/verify operation.
                    Name = NormalizeRelativeMemberName(child.Name),
                    Size = child.Size.HasValue && child.Size.Value <= long.MaxValue ? (long)child.Size.Value : 0,
                    Crc32 = Clone(child.CRC),
                    Sha1 = Clone(child.SHA1),
                    Md5 = Clone(child.MD5),
                    Sha256 = Clone(previousTrack?.Sha256)
                });
            }
        }

        if (manifest.Tracks.Count == 0 && previous?.Tracks != null)
            manifest.Tracks.AddRange(previous.Tracks);
        manifest.Tracks.Sort((left, right) => left.Number.CompareTo(right.Number));
        return manifest;
    }

    public static bool TryCreateFromDat(
        string sourceChdPath,
        RvFile destination,
        ChdEncodingProfileSpec profile,
        ChdmanIdentity identity,
        ChdReconstructionManifest previous,
        out ChdReconstructionManifest manifest,
        out string error)
    {
        manifest = CreateFromDat(destination, profile, identity, previous);
        error = "";
        if (!IsOpticalFamily(profile?.Family))
            return true;

        if (!ChdMetadata.TryReadCdTrackLayout(sourceChdPath, out List<ChdCdTrackInfo> tracks, out string trackError) || tracks.Count == 0)
        {
            error = "The source CHD has no readable optical track layout: " + trackError;
            manifest = null;
            return false;
        }

        if (!TryAttachOpticalDescriptor(manifest, destination, previous, tracks, sourceChdPath, out error) ||
            !TryAttachRequiredSbiAuxiliaries(manifest, destination, previous, sourceChdPath, out error))
        {
            manifest = null;
            return false;
        }

        return true;
    }

    internal static bool HasCompleteOpticalIdentity(ChdReconstructionManifest manifest, out string error)
    {
        error = "";
        if (manifest == null)
        {
            error = "Reconstruction metadata is unavailable.";
            return false;
        }
        if (!IsOpticalFamily(manifest.Family))
            return true;
        if (manifest.Tracks == null || manifest.Tracks.Count == 0)
        {
            error = "Optical reconstruction metadata has no payload identities.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(manifest.Dialect))
        {
            error = "Optical reconstruction metadata has no media dialect.";
            return false;
        }
        for (int i = 0; i < manifest.Tracks.Count; i++)
        {
            ChdManifestTrack track = manifest.Tracks[i];
            bool completeHashes = track != null && track.Sha256 != null && track.Sha256.Length == 32;
            if (manifest.Schema == CurrentSchema)
            {
                completeHashes = completeHashes && track.Crc32 != null && track.Crc32.Length == 4 &&
                                 track.Md5 != null && track.Md5.Length == 16 &&
                                 track.Sha1 != null && track.Sha1.Length == 20;
            }
            if (track == null || track.Number <= 0 || track.Size < 0 || !completeHashes)
            {
                error = "Optical reconstruction metadata has an incomplete payload identity.";
                return false;
            }
        }
        return true;
    }

    internal static bool TryAttachOpticalDescriptor(
        ChdReconstructionManifest manifest,
        RvFile destination,
        ChdReconstructionManifest previous,
        List<ChdCdTrackInfo> tracks,
        string sourceChdPath,
        out string error)
    {
        error = "";
        if (manifest == null || !IsOpticalFamily(manifest.Family))
            return true;
        if (tracks == null || tracks.Count == 0)
        {
            error = "An optical descriptor cannot be reconstructed without a track layout.";
            return false;
        }

        // Descriptor filenames and bytes are intentionally not part of the
        // persistent contract.  CUE/GDI text is generated for the current DAT
        // names at scan/export time from the CHD track layout.
        manifest.DescriptorName = "";
        manifest.DescriptorBytes = Array.Empty<byte>();
        manifest.DescriptorSha256 = Array.Empty<byte>();
        return true;
    }

    private static bool TryAttachRequiredSbiAuxiliaries(
        ChdReconstructionManifest manifest,
        RvFile destination,
        ChdReconstructionManifest previous,
        string sourceChdPath,
        out string error)
    {
        error = "";
        List<RvFile> expectedSbis = GetExpectedMembers(destination, ".sbi");
        if (expectedSbis.Count == 0)
            return true;

        List<ChdManifestAuxiliary> retained = (manifest.Auxiliaries ?? new List<ChdManifestAuxiliary>())
            .Where(item => item != null && !string.Equals(item.Role, "sbi-subchannel-correction", StringComparison.OrdinalIgnoreCase))
            .ToList();
        HashSet<ChdManifestAuxiliary> usedPrevious = new HashSet<ChdManifestAuxiliary>();

        for (int i = 0; i < expectedSbis.Count; i++)
        {
            RvFile expected = expectedSbis[i];
            if (!HasExpectedContentHash(expected))
            {
                error = "The DAT declares SBI reconstruction data without a CRC, SHA-1, or MD5, so exact auxiliary bytes cannot be proven.";
                return false;
            }
            byte[] exact = null;
            if (previous?.Auxiliaries != null)
            {
                for (int j = 0; j < previous.Auxiliaries.Count; j++)
                {
                    ChdManifestAuxiliary candidate = previous.Auxiliaries[j];
                    if (candidate == null || usedPrevious.Contains(candidate) ||
                        !string.Equals(candidate.Role, "sbi-subchannel-correction", StringComparison.OrdinalIgnoreCase) ||
                        !DescriptorMatchesExpected(expected, candidate.Bytes, out _))
                        continue;
                    exact = candidate.Bytes;
                    usedPrevious.Add(candidate);
                    break;
                }
            }

            if (exact == null)
            {
                foreach (string sidecarPath in GetSidecarCandidates(sourceChdPath, expected.Name, ".sbi"))
                {
                    try
                    {
                        if (!TryReadBoundedSidecar(sidecarPath, out byte[] candidate))
                            continue;
                        if (DescriptorMatchesExpected(expected, candidate, out _))
                        {
                            exact = candidate;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (exact == null)
            {
                error = "The DAT requires exact SBI reconstruction data, but no matching embedded or sidecar SBI was available.";
                return false;
            }
            retained.Add(new ChdManifestAuxiliary
            {
                Name = expected.Name,
                Role = "sbi-subchannel-correction",
                Bytes = Clone(exact),
                Sha256 = HashBytes(exact, SHA256.Create())
            });
        }

        manifest.Auxiliaries = retained;
        return true;
    }

    public byte[] Serialize()
    {
        if (Schema != CurrentSchema)
            throw new InvalidDataException("Unsupported reconstruction metadata schema: " + Schema);
        ChdReconstructionManifest stored = CreateStorageManifest();
        stored.ValidateForSerialization(true);
        return RvrmWireFormat.Serialize(stored);
    }

    private ChdReconstructionManifest CreateStorageManifest()
    {
        ChdReconstructionManifest stored = new ChdReconstructionManifest
        {
            Schema = CurrentSchema,
            ProfileId = ProfileId,
            ProfileRevision = ProfileRevision,
            WriterRevision = WriterRevision,
            Family = Family,
            Storage = Storage,
            Dialect = Dialect,
            CreateMode = CreateMode,
            ExtractMode = ExtractMode,
            ChdmanVersion = ChdmanVersion,
            ChdmanBanner = ChdmanBanner,
            ChdmanSha256 = ChdmanSha256,
            CapabilityFingerprint = CapabilityFingerprint,

            // RVRM is content-addressed.  Source descriptor names and bytes are
            // deliberately transient because descriptor text contains external
            // filenames.  The active DAT supplies presentation names later.
            DescriptorName = "",
            DescriptorBytes = Array.Empty<byte>(),
            DescriptorSha256 = Array.Empty<byte>(),
            Tracks = CanonicalizeTracks(Tracks, "payload"),
            OpaqueSections = CloneOpaqueSections(OpaqueSections)
        };

        List<ChdManifestView> views = Views ?? new List<ChdManifestView>();
        for (int i = 0; i < views.Count; i++)
        {
            ChdManifestView view = views[i] ?? new ChdManifestView();
            stored.Views.Add(new ChdManifestView
            {
                Name = string.Equals(view.Name, "iso", StringComparison.OrdinalIgnoreCase) ? "iso" : "view-" + (i + 1).ToString("D4"),
                Dialect = view.Dialect,
                DescriptorName = "",
                DescriptorBytes = Array.Empty<byte>(),
                DescriptorSha256 = Array.Empty<byte>(),
                Tracks = CanonicalizeTracks(view.Tracks, "view-payload")
            });
        }

        List<ChdManifestAuxiliary> auxiliaries = (Auxiliaries ?? new List<ChdManifestAuxiliary>())
            .Where(item => item != null)
            .OrderBy(item => item.Role ?? "", StringComparer.Ordinal)
            .ThenBy(item => Hex(item.Sha256), StringComparer.Ordinal)
            .ToList();
        for (int i = 0; i < auxiliaries.Count; i++)
        {
            ChdManifestAuxiliary auxiliary = auxiliaries[i];
            stored.Auxiliaries.Add(new ChdManifestAuxiliary
            {
                Name = "auxiliary-" + (i + 1).ToString("D4"),
                Role = auxiliary.Role,
                Bytes = Clone(auxiliary.Bytes),
                Sha256 = Clone(auxiliary.Sha256)
            });
        }
        return stored;
    }

    private static string Hex(byte[] value)
    {
        if (value == null || value.Length == 0)
            return "";
        StringBuilder text = new StringBuilder(value.Length * 2);
        for (int i = 0; i < value.Length; i++)
            text.Append(value[i].ToString("x2"));
        return text.ToString();
    }

    private static List<ChdManifestTrack> CanonicalizeTracks(List<ChdManifestTrack> tracks, string prefix)
    {
        List<ChdManifestTrack> source = (tracks ?? new List<ChdManifestTrack>())
            .OrderBy(track => track?.Number ?? 0)
            .ToList();
        List<ChdManifestTrack> result = new List<ChdManifestTrack>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            ChdManifestTrack track = source[i] ?? new ChdManifestTrack();
            result.Add(new ChdManifestTrack
            {
                Number = track.Number,
                Name = prefix + "-" + (i + 1).ToString("D4"),
                Size = track.Size,
                Crc32 = Clone(track.Crc32),
                Sha1 = Clone(track.Sha1),
                Md5 = Clone(track.Md5),
                Sha256 = Clone(track.Sha256)
            });
        }
        return result;
    }

    private static List<RvrmOpaqueSection> CloneOpaqueSections(List<RvrmOpaqueSection> sections)
    {
        List<RvrmOpaqueSection> result = new List<RvrmOpaqueSection>();
        for (int i = 0; i < (sections?.Count ?? 0); i++)
        {
            RvrmOpaqueSection source = sections[i];
            if (source == null)
            {
                result.Add(null);
                continue;
            }
            result.Add(new RvrmOpaqueSection
            {
                Id = source.Id,
                Flags = source.Flags,
                Payload = Clone(source.Payload)
            });
        }
        return result;
    }

    public static bool TryDeserialize(byte[] data, out ChdReconstructionManifest manifest, out string error)
    {
        manifest = null;
        error = "";
        if (data == null || data.Length < 12 + IntegrityBytes || !RvrmWireFormat.IsSupportedManifestLength(data.Length))
        {
            error = "Reconstruction metadata is empty or truncated.";
            return false;
        }
        try
        {
            int payloadLength = data.Length - IntegrityBytes;
            int declaredSchema;
            uint declaredLayout;
            using (MemoryStream headerMemory = new MemoryStream(data, false))
            using (BinaryReader headerReader = new BinaryReader(headerMemory, Encoding.UTF8, true))
            {
                if (headerReader.ReadUInt32() != Magic)
                {
                    error = "Reconstruction metadata has an invalid signature.";
                    return false;
                }
                declaredSchema = headerReader.ReadInt32();
                declaredLayout = headerReader.ReadUInt32();
            }
            if (declaredSchema != CurrentSchema)
            {
                error = "Unsupported reconstruction metadata schema: " + declaredSchema;
                return false;
            }
            if (declaredLayout != RvrmWireFormat.LayoutMarker)
            {
                error = "Unsupported reconstruction metadata layout for schema " + declaredSchema + ".";
                return false;
            }
            byte[] expected = new byte[IntegrityBytes];
            Buffer.BlockCopy(data, payloadLength, expected, 0, expected.Length);
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 0, payload, 0, payload.Length);
            if (!BytesEqual(expected, HashBytes(payload, SHA256.Create())))
                throw new InvalidDataException("Reconstruction metadata integrity checksum mismatch.");
            return RvrmWireFormat.TryDeserialize(payload, out manifest, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            manifest = null;
            return false;
        }
    }

    public static bool RunSerializationSelfTest(out string error)
    {
        error = "";
        try
        {
            const string sourceTrackName = "private-source-track-name.bin";
            const string sourceDescriptorName = "private-source-descriptor.cue";
            const string sourceAuxiliaryName = "private-source-sidecar.sbi";
            const string sourceBanner = "PRIVATE-CHDMAN-BANNER";
            byte[] descriptor = Encoding.ASCII.GetBytes("FILE \"" + sourceTrackName + "\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            byte[] auxiliaryBytes = new byte[] { 0x53, 0x42, 0x49, 0x00 };
            ChdReconstructionManifest original = new ChdReconstructionManifest
            {
                ProfileId = "PRIVATE-PROFILE-ID",
                ProfileRevision = 97,
                WriterRevision = 53,
                Family = "cd",
                Storage = "PRIVATE-STORAGE",
                Dialect = "cue-exact",
                CreateMode = "PRIVATE-CREATE-MODE",
                ExtractMode = "PRIVATE-EXTRACT-MODE",
                ChdmanVersion = "PRIVATE-CHDMAN-VERSION",
                ChdmanBanner = sourceBanner,
                ChdmanSha256 = "PRIVATE-CHDMAN-SHA256",
                CapabilityFingerprint = "PRIVATE-CAPABILITY-FINGERPRINT",
                DescriptorName = sourceDescriptorName,
                DescriptorBytes = descriptor,
                DescriptorSha256 = HashBytes(descriptor, SHA256.Create()),
                Tracks = new List<ChdManifestTrack>
                {
                    new ChdManifestTrack
                    {
                        Number = 1,
                        Name = sourceTrackName,
                        Size = 2352,
                        Crc32 = new byte[] { 1, 2, 3, 4 },
                        Sha1 = new byte[20],
                        Md5 = new byte[16],
                        Sha256 = new byte[32]
                    }
                },
                Views = new List<ChdManifestView>
                {
                    new ChdManifestView
                    {
                        Name = "iso",
                        Dialect = "iso-from-mode1-2352",
                        DescriptorName = "private-view-descriptor.cue",
                        DescriptorBytes = descriptor,
                        DescriptorSha256 = HashBytes(descriptor, SHA256.Create()),
                        Tracks = new List<ChdManifestTrack>
                        {
                            new ChdManifestTrack
                            {
                                Number = 1,
                                Name = "private-view-payload.iso",
                                Size = 2048,
                                Crc32 = new byte[4],
                                Md5 = new byte[16],
                                Sha1 = new byte[20],
                                Sha256 = new byte[32]
                            }
                        }
                    }
                },
                Auxiliaries = new List<ChdManifestAuxiliary>
                {
                    new ChdManifestAuxiliary
                    {
                        Name = sourceAuxiliaryName,
                        Role = "sbi-subchannel-correction",
                        Bytes = auxiliaryBytes,
                        Sha256 = HashBytes(auxiliaryBytes, SHA256.Create())
                    }
                }
            };
            byte[] encoded = original.Serialize();
            if (!TryDeserialize(encoded, out ChdReconstructionManifest roundTrip, out error))
                return false;
            if (roundTrip.Schema != CurrentSchema || roundTrip.Tracks.Count != 1 || roundTrip.Views.Count != 1 || roundTrip.Auxiliaries.Count != 1 ||
                !string.Equals(roundTrip.Family, "cd", StringComparison.Ordinal) ||
                !string.Equals(roundTrip.Dialect, "cue-exact", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(roundTrip.Storage) ||
                !string.Equals(roundTrip.Tracks[0].Name, "payload-0001", StringComparison.Ordinal) ||
                roundTrip.Tracks[0].Number != 1 || roundTrip.Tracks[0].Size != 2352 ||
                !BytesEqual(roundTrip.Tracks[0].Crc32, original.Tracks[0].Crc32) ||
                !BytesEqual(roundTrip.Tracks[0].Sha1, original.Tracks[0].Sha1) ||
                !BytesEqual(roundTrip.Tracks[0].Md5, original.Tracks[0].Md5) ||
                !BytesEqual(roundTrip.Tracks[0].Sha256, original.Tracks[0].Sha256) ||
                roundTrip.DescriptorBytes.Length != 0 ||
                !string.Equals(roundTrip.Views[0].Name, "iso", StringComparison.Ordinal) ||
                !string.Equals(roundTrip.Views[0].Dialect, "iso-from-mode1-2352", StringComparison.Ordinal) ||
                !string.Equals(roundTrip.Views[0].Tracks[0].Name, "view-payload-0001", StringComparison.Ordinal) ||
                !string.Equals(roundTrip.Auxiliaries[0].Name, "auxiliary-0001", StringComparison.Ordinal) ||
                !string.Equals(roundTrip.Auxiliaries[0].Role, "sbi-subchannel-correction", StringComparison.Ordinal) ||
                !BytesEqual(roundTrip.Auxiliaries[0].Bytes, auxiliaryBytes) ||
                !BytesEqual(roundTrip.Auxiliaries[0].Sha256, HashBytes(auxiliaryBytes, SHA256.Create())))
            {
                error = "Manifest serialization round trip changed reconstruction data.";
                return false;
            }

            string[] forbidden =
            {
                sourceTrackName,
                sourceDescriptorName,
                sourceAuxiliaryName,
                sourceBanner,
                "PRIVATE-PROFILE-ID",
                "PRIVATE-STORAGE",
                "PRIVATE-CREATE-MODE",
                "PRIVATE-EXTRACT-MODE",
                "PRIVATE-CHDMAN-VERSION",
                "PRIVATE-CHDMAN-SHA256",
                "PRIVATE-CAPABILITY-FINGERPRINT",
                "private-view-descriptor.cue",
                "private-view-payload.iso",
                "cue-exact",
                "iso-from-mode1-2352",
                "sbi-subchannel-correction"
            };
            for (int i = 0; i < forbidden.Length; i++)
            {
                if (ContainsBytes(encoded, Encoding.UTF8.GetBytes(forbidden[i])))
                {
                    error = "Compact reconstruction metadata leaked an omitted string: " + forbidden[i];
                    return false;
                }
            }

            foreach (RvrmRecipe recipe in Enum.GetValues(typeof(RvrmRecipe)))
            {
                string recipeName = RvrmWireFormat.RecipeName(recipe);
                if (!RvrmWireFormat.TryMapRecipe(recipeName, out RvrmRecipe parsedRecipe) || parsedRecipe != recipe)
                {
                    error = "A typed reconstruction recipe did not round trip: " + recipe + ".";
                    return false;
                }
            }

            if (!TryDeserialize(encoded, out ChdReconstructionManifest orderFixture, out error))
                return false;
            orderFixture.Auxiliaries.Add(new ChdManifestAuxiliary
            {
                Name = "second.sbi",
                Role = "sbi-subchannel-correction",
                Bytes = new byte[] { 0x53, 0x42, 0x49, 0x01 },
                Sha256 = HashBytes(new byte[] { 0x53, 0x42, 0x49, 0x01 }, SHA256.Create())
            });
            byte[] ordered = orderFixture.Serialize();
            orderFixture.Auxiliaries.Reverse();
            if (!BytesEqual(ordered, orderFixture.Serialize()))
            {
                error = "Auxiliary insertion order changed canonical RVRM bytes.";
                return false;
            }

            ChdReconstructionManifest plainIsoView = original.CreateStorageManifest();
            plainIsoView.Views[0].Dialect = "iso";
            try
            {
                plainIsoView.Serialize();
                error = "Schema 1 accepted a plain ISO alternate view without a derivation recipe.";
                return false;
            }
            catch (InvalidDataException)
            {
            }

            ChdReconstructionManifest multipleViews = original.CreateStorageManifest();
            multipleViews.Views.Add(new ChdManifestView
            {
                Name = "second-iso",
                Dialect = "iso-from-mode1-2048",
                Tracks = CanonicalizeTracks(original.Views[0].Tracks, "second-view-payload")
            });
            try
            {
                multipleViews.Serialize();
                error = "Schema 1 accepted multiple alternate reconstruction views.";
                return false;
            }
            catch (InvalidDataException)
            {
            }

            original.ProfileId = "renamed-profile";
            original.ProfileRevision++;
            original.WriterRevision++;
            original.Storage = "renamed-storage";
            original.CreateMode = "renamed-create-mode";
            original.ExtractMode = "renamed-extract-mode";
            original.ChdmanVersion = "renamed-version";
            original.ChdmanBanner = "renamed-banner";
            original.ChdmanSha256 = "renamed-tool-hash";
            original.CapabilityFingerprint = "renamed-capabilities";
            original.Tracks[0].Name = "renamed-anywhere.bin";
            original.DescriptorName = "renamed.cue";
            original.DescriptorBytes = Encoding.ASCII.GetBytes("FILE \"renamed-anywhere.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            original.DescriptorSha256 = HashBytes(original.DescriptorBytes, SHA256.Create());
            original.Views[0].Tracks[0].Name = "renamed.iso";
            original.Views[0].DescriptorName = "renamed-view.cue";
            original.Views[0].DescriptorBytes = original.DescriptorBytes;
            original.Views[0].DescriptorSha256 = original.DescriptorSha256;
            original.Auxiliaries[0].Name = "renamed.sbi";
            if (!BytesEqual(encoded, original.Serialize()))
            {
                error = "Omitted names, descriptors, or provenance altered compact reconstruction metadata.";
                return false;
            }

            byte[] unknownOptional = AppendTestSection(encoded, 60000, 0, new byte[] { 1, 2, 3 });
            if (!TryDeserialize(unknownOptional, out ChdReconstructionManifest optionalRoundTrip, out error))
            {
                error = "An unknown optional schema-1 section was not ignored: " + error;
                return false;
            }
            if (!BytesEqual(unknownOptional, optionalRoundTrip.Serialize()))
            {
                error = "An unknown optional schema-1 section was not preserved byte-for-byte.";
                return false;
            }
            ChdReconstructionManifest rebuiltFromDat = CreateFromDat(
                null,
                ChdEncodingProfile.ForFamily("cd", ChdStorageProfile.Archive),
                null,
                optionalRoundTrip);
            if (!BytesEqual(unknownOptional, rebuiltFromDat.Serialize()))
            {
                error = "DAT-aware metadata rebuilding discarded an unknown optional schema-1 section.";
                return false;
            }
            if (!RvrmWireFormat.IsSupportedManifestLength(MaxManifestBytes) ||
                RvrmWireFormat.IsSupportedManifestLength((long)MaxManifestBytes + 1) ||
                !RvrmWireFormat.IsSupportedAuxiliaryLength(MaxAuxiliaryBytes) ||
                RvrmWireFormat.IsSupportedAuxiliaryLength((long)MaxAuxiliaryBytes + 1))
            {
                error = "RVRM metadata length boundaries do not match the CHD 24-bit storage limit.";
                return false;
            }
            byte[] unknownRequired = AppendTestSection(encoded, 60001, 1, Array.Empty<byte>());
            if (TryDeserialize(unknownRequired, out _, out _))
            {
                error = "An unknown required schema-1 section was ignored.";
                return false;
            }
            byte[] unknownFeature = (byte[])encoded.Clone();
            unknownFeature[19] |= 0x80;
            RewriteIntegrityTrailer(unknownFeature);
            if (TryDeserialize(unknownFeature, out _, out _))
            {
                error = "An unknown required schema-1 feature was ignored.";
                return false;
            }
            if (TryDeserialize(SetSectionFlagsForTest(encoded, 1, 0), out _, out _))
            {
                error = "Schema 1 accepted a core section without its canonical required flag.";
                return false;
            }
            if (TryDeserialize(AppendTestSection(encoded, 0, 0, Array.Empty<byte>()), out _, out _))
            {
                error = "Schema 1 accepted sections outside canonical id order.";
                return false;
            }
            if (TryDeserialize(ReverseAuxiliariesForTest(ordered), out _, out _))
            {
                error = "Schema 1 accepted auxiliaries outside canonical byte order.";
                return false;
            }

            ChdReconstructionManifest twoPayloads = new ChdReconstructionManifest
            {
                Family = "raw",
                Dialect = "raw-exact",
                Tracks = new List<ChdManifestTrack>
                {
                    new ChdManifestTrack
                    {
                        Number = 1, Name = "first.raw", Size = 1,
                        Crc32 = new byte[] { 1, 0, 0, 0 }, Md5 = new byte[16], Sha1 = new byte[20], Sha256 = new byte[32]
                    },
                    new ChdManifestTrack
                    {
                        Number = 2, Name = "second.raw", Size = 1,
                        Crc32 = new byte[] { 2, 0, 0, 0 }, Md5 = new byte[16], Sha1 = new byte[20], Sha256 = new byte[32]
                    }
                }
            };
            if (TryDeserialize(SwapPrimaryPayloadsForTest(twoPayloads.Serialize()), out _, out _))
            {
                error = "Schema 1 accepted primary payload identities outside canonical number order.";
                return false;
            }

            ChdReconstructionManifest canonicalFixture = new ChdReconstructionManifest
            {
                Family = "raw",
                Dialect = "raw-exact",
                Tracks = new List<ChdManifestTrack>
                {
                    new ChdManifestTrack
                    {
                        Number = 1,
                        Name = "ignored.raw",
                        Size = 3,
                        Crc32 = new byte[] { 1, 2, 3, 4 },
                        Md5 = new byte[16],
                        Sha1 = new byte[20],
                        Sha256 = new byte[32]
                    }
                }
            };
            // Frozen canonical schema-1 bytes. This fixture is intentionally
            // independent of the reader so synchronized drift cannot hide a
            // wire-format change.
            byte[] canonicalSchema1 = Convert.FromBase64String(
                "TVJWUgEAAABSVkMxBwAAAAAAAAABAAAAAQABAFsAAAAFAQABAAAAAQAAAAMAAAAAAAAAAQIDBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAw4/q2hbvCf8RJSqURBAdGSK8SCr9M2Dd7gRJL9T3i9k=");
            if (!BytesEqual(canonicalFixture.Serialize(), canonicalSchema1) ||
                !TryDeserialize(canonicalSchema1, out ChdReconstructionManifest canonicalRoundTrip, out error) ||
                canonicalRoundTrip.Schema != 1 || canonicalRoundTrip.Tracks.Count != 1 ||
                canonicalRoundTrip.Tracks[0].Number != 1 || canonicalRoundTrip.Tracks[0].Size != 3)
            {
                error = "The canonical schema-1 golden fixture changed or could not be read: " + error;
                return false;
            }

            // Frozen layouts from abandoned pre-release implementations.
            // They intentionally remain rejection fixtures: schema 1 now has one
            // strict typed/canonical wire contract and no migration ambiguity.
            byte[] experimentalCompactSchema1 = Convert.FromBase64String(
                "TVJWUgEAAABSVkMxAwAAAHJhdwkAAAByYXctZXhhY3QBAAAAAQAAAAMAAAAAAAAADwECAwQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABHuV26LDDw9OLc1ZTP5CIc+O7aB67z1wQII0RqZd/Kg=");
            if (TryDeserialize(experimentalCompactSchema1, out _, out _))
            {
                error = "The canonical schema-1 parser accepted an obsolete compact schema-1 layout.";
                return false;
            }

            byte[] experimentalVerboseSchema1 = Convert.FromBase64String(
                "TVJWUgEAAAAKAAAAcnZ3b3JsZC12MQcAAAAJAAAAAgAAAGNkBwAAAGFyY2hpdmUOAAAAY3VlLW1vZGUxLTIzNTIIAAAAY3JlYXRl" +
                "Y2QJAAAAZXh0cmFjdGNkBQAAADAuMjg5DAAAAGNoZG1hbiAwLjI4OUAAAABhYmFiYWJhYmFiYWJhYmFiYWJhYmFiYWJhYmFiYWJh" +
                "YmFiYWJhYmFiYWJhYmFiYWJhYmFiYWJhYmFiYWJhYmFiEgAAAGZpeHR1cmUtY2FwYWJpbGl0eQgAAABkaXNjLmN1ZUkAAABGSUxF" +
                "ICJ0cmFjazAxLmJpbiIgQklOQVJZDQogIFRSQUNLIDAxIE1PREUxLzIzNTINCiAgICBJTkRFWCAwMSAwMDowMDowMA0KIAAAAMKI" +
                "Wh0Vrx0dWS440B247TmFzb5TJAwMvZ9fFlQW9SbjAQAAAAEAAAALAAAAdHJhY2swMS5iaW4wCQAAAAAAAAQAAAABAgMEFAAAAAEC" +
                "AwQFBgcICQoLDA0ODxAREhMUEAAAACEiIyQlJicoKSorLC0uLzAgAAAAQUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVpbXF1eX2AB" +
                "AAAAAwAAAGlzbxMAAABpc28tZnJvbS1tb2RlMS0yMzUyAAAAAAAAAAAAAAAAAQAAAAEAAAAIAAAAZGlzYy5pc28ACAAAAAAAAAQA" +
                "AAALDA0OFAAAAAsMDQ4PEBESExQVFhcYGRobHB0eEAAAACssLS4vMDEyMzQ1Njc4OTogAAAAS0xNTk9QUVJTVFVWV1hZWltcXV5f" +
                "YGFiY2RlZmdoaWoBAAAACAAAAGRpc2Muc2JpGQAAAHNiaS1zdWJjaGFubmVsLWNvcnJlY3Rpb24EAAAAU0JJACAAAAALvF/H0Wur" +
                "LgIc+zDps9TgRu2LoGAJ9SWFbjsAFBvWoONg99K2IAC8+MgX7qPOd/GqnrnDYPXsRwYe8XV1VysX");
            if (TryDeserialize(experimentalVerboseSchema1, out _, out _))
            {
                error = "The canonical schema-1 parser accepted an obsolete verbose schema-1 layout.";
                return false;
            }

            byte[] transitionalSchema2 = (byte[])encoded.Clone();
            Buffer.BlockCopy(BitConverter.GetBytes(2), 0, transitionalSchema2, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0x32435652u), 0, transitionalSchema2, 8, 4); // RVC2
            RewriteIntegrityTrailer(transitionalSchema2);
            if (TryDeserialize(transitionalSchema2, out _, out _))
            {
                error = "The canonical schema-1 parser accepted the transitional schema-2 layout.";
                return false;
            }

            ChdReconstructionManifest incompleteCanonical = original.CreateStorageManifest();
            incompleteCanonical.Tracks[0].Md5 = Array.Empty<byte>();
            try
            {
                incompleteCanonical.Serialize();
                error = "Schema 1 accepted a payload identity with an optional hash set.";
                return false;
            }
            catch (InvalidDataException)
            {
                // Schema 1 intentionally fixes the hash set so DAT richness can
                // no longer change canonical RVRM bytes.
            }
            ChdReconstructionManifest unsupportedSchema = original.CreateStorageManifest();
            unsupportedSchema.Schema = 99;
            try
            {
                unsupportedSchema.Serialize();
                error = "An unsupported in-memory RVRM schema was silently rewritten.";
                return false;
            }
            catch (InvalidDataException)
            {
            }

            byte[] damaged = (byte[])encoded.Clone();
            damaged[damaged.Length - IntegrityBytes - 1] ^= 0x40;
            if (TryDeserialize(damaged, out _, out _))
            {
                error = "Manifest integrity checksum accepted modified metadata.";
                return false;
            }

            byte[] obsoleteSchema = (byte[])encoded.Clone();
            obsoleteSchema[4] = 5;
            if (TryDeserialize(obsoleteSchema, out _, out _))
            {
                error = "Manifest parser accepted an unreleased migration schema.";
                return false;
            }

            byte[] oldLayout = (byte[])encoded.Clone();
            oldLayout[8] = 10; // old schema-1 started with a length-prefixed profile id
            oldLayout[9] = oldLayout[10] = oldLayout[11] = 0;
            RewriteIntegrityTrailer(oldLayout);
            if (TryDeserialize(oldLayout, out _, out _))
            {
                error = "Manifest parser accepted the pre-release schema-1 wire layout.";
                return false;
            }

            ChdReconstructionManifest manyTracks = new ChdReconstructionManifest
            {
                Family = "gdi",
                Dialect = "redump-gdrom-cue"
            };
            for (int i = 0; i < 99; i++)
            {
                manyTracks.Tracks.Add(new ChdManifestTrack
                {
                    Number = i + 1,
                    Name = "an-external-track-name-that-must-not-be-stored-" + (i + 1).ToString("D2") + ".bin",
                    Size = 2352L * (300 + i),
                    Crc32 = new byte[4],
                    Sha1 = new byte[20],
                    Md5 = new byte[16],
                    Sha256 = new byte[32]
                });
            }
            byte[] manyEncoded = manyTracks.Serialize();
            if (manyEncoded.Length >= 12 * 1024)
            {
                error = "A 99-track compact reconstruction manifest is unexpectedly large: " + manyEncoded.Length + " bytes.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool ContainsBytes(byte[] haystack, byte[] needle)
    {
        if (haystack == null || needle == null || needle.Length == 0 || needle.Length > haystack.Length)
            return false;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
                j++;
            if (j == needle.Length)
                return true;
        }
        return false;
    }

    private static byte[] AppendTestSection(byte[] encoded, ushort id, ushort flags, byte[] sectionPayload)
    {
        sectionPayload = sectionPayload ?? Array.Empty<byte>();
        int oldPayloadLength = encoded.Length - IntegrityBytes;
        byte[] expanded = new byte[encoded.Length + 8 + sectionPayload.Length];
        Buffer.BlockCopy(encoded, 0, expanded, 0, oldPayloadLength);
        int sectionCount = BitConverter.ToInt32(expanded, 20);
        Buffer.BlockCopy(BitConverter.GetBytes(sectionCount + 1), 0, expanded, 20, 4);
        int offset = oldPayloadLength;
        Buffer.BlockCopy(BitConverter.GetBytes(id), 0, expanded, offset, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(flags), 0, expanded, offset + 2, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(sectionPayload.Length), 0, expanded, offset + 4, 4);
        Buffer.BlockCopy(sectionPayload, 0, expanded, offset + 8, sectionPayload.Length);
        RewriteIntegrityTrailer(expanded);
        return expanded;
    }

    private static byte[] SetSectionFlagsForTest(byte[] encoded, ushort id, ushort flags)
    {
        byte[] changed = (byte[])encoded.Clone();
        int payloadOffset = FindSectionPayloadForTest(changed, id, out _);
        Buffer.BlockCopy(BitConverter.GetBytes(flags), 0, changed, payloadOffset - 6, 2);
        RewriteIntegrityTrailer(changed);
        return changed;
    }

    private static byte[] SwapPrimaryPayloadsForTest(byte[] encoded)
    {
        byte[] changed = (byte[])encoded.Clone();
        int payloadOffset = FindSectionPayloadForTest(changed, 1, out int sectionLength);
        const int identityBytes = 4 + 8 + 4 + 16 + 20 + 32;
        int countOffset = payloadOffset + 3;
        if (sectionLength < 7 + (2 * identityBytes) || BitConverter.ToInt32(changed, countOffset) != 2)
            throw new InvalidDataException("Primary-payload ordering fixture is invalid.");
        int firstOffset = payloadOffset + 7;
        byte[] first = new byte[identityBytes];
        Buffer.BlockCopy(changed, firstOffset, first, 0, identityBytes);
        Buffer.BlockCopy(changed, firstOffset + identityBytes, changed, firstOffset, identityBytes);
        Buffer.BlockCopy(first, 0, changed, firstOffset + identityBytes, identityBytes);
        RewriteIntegrityTrailer(changed);
        return changed;
    }

    private static byte[] ReverseAuxiliariesForTest(byte[] encoded)
    {
        byte[] changed = (byte[])encoded.Clone();
        int payloadOffset = FindSectionPayloadForTest(changed, 3, out int sectionLength);
        if (sectionLength < 4 || BitConverter.ToInt32(changed, payloadOffset) != 2)
            throw new InvalidDataException("Auxiliary-ordering fixture is invalid.");
        int firstOffset = payloadOffset + 4;
        int firstBytes = 6 + BitConverter.ToInt32(changed, firstOffset + 2);
        int secondOffset = firstOffset + firstBytes;
        int secondBytes = 6 + BitConverter.ToInt32(changed, secondOffset + 2);
        if (firstBytes < 6 || secondBytes < 6 || 4 + firstBytes + secondBytes != sectionLength)
            throw new InvalidDataException("Auxiliary-ordering fixture has invalid lengths.");
        byte[] first = new byte[firstBytes];
        byte[] second = new byte[secondBytes];
        Buffer.BlockCopy(changed, firstOffset, first, 0, first.Length);
        Buffer.BlockCopy(changed, secondOffset, second, 0, second.Length);
        Buffer.BlockCopy(second, 0, changed, firstOffset, second.Length);
        Buffer.BlockCopy(first, 0, changed, firstOffset + second.Length, first.Length);
        RewriteIntegrityTrailer(changed);
        return changed;
    }

    private static int FindSectionPayloadForTest(byte[] encoded, ushort targetId, out int sectionLength)
    {
        int payloadLength = (encoded?.Length ?? 0) - IntegrityBytes;
        if (payloadLength < 24)
            throw new InvalidDataException("Schema-1 section fixture is truncated.");
        using (MemoryStream memory = new MemoryStream(encoded, 0, payloadLength, false))
        using (BinaryReader reader = new BinaryReader(memory, Encoding.UTF8, true))
        {
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != CurrentSchema ||
                reader.ReadUInt32() != RvrmWireFormat.LayoutMarker)
                throw new InvalidDataException("Schema-1 section fixture has an invalid header.");
            reader.ReadUInt64();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                ushort id = reader.ReadUInt16();
                reader.ReadUInt16();
                int length = reader.ReadInt32();
                int offset = checked((int)memory.Position);
                if (length < 0 || length > memory.Length - memory.Position)
                    throw new InvalidDataException("Schema-1 section fixture has an invalid length.");
                if (id == targetId)
                {
                    sectionLength = length;
                    return offset;
                }
                memory.Position += length;
            }
        }
        throw new InvalidDataException("Schema-1 section fixture is missing section " + targetId + ".");
    }

    private static void RewriteIntegrityTrailer(byte[] data)
    {
        if (data == null || data.Length < IntegrityBytes)
            throw new InvalidDataException("Reconstruction metadata is too short for an integrity trailer.");
        int payloadLength = data.Length - IntegrityBytes;
        byte[] payload = new byte[payloadLength];
        Buffer.BlockCopy(data, 0, payload, 0, payloadLength);
        byte[] digest = HashBytes(payload, SHA256.Create());
        Buffer.BlockCopy(digest, 0, data, payloadLength, IntegrityBytes);
    }

    private static ChdReconstructionManifest CreateBase(ChdEncodingProfileSpec profile, ChdmanIdentity identity)
    {
        return new ChdReconstructionManifest
        {
            ProfileId = ChdEncodingProfile.ProfileId,
            ProfileRevision = profile.ProfileRevision,
            WriterRevision = identity?.Capabilities?.WriterRevision(profile.Family) ?? 0,
            Family = profile.Family,
            Storage = profile.Storage,
            ChdmanVersion = identity?.VersionText ?? "",
            ChdmanBanner = identity?.Banner ?? "",
            ChdmanSha256 = identity?.BinarySha256 ?? "",
            CapabilityFingerprint = identity?.Capabilities?.Fingerprint ?? ""
        };
    }

    private static ChdManifestTrack FindMatchingTrack(List<ChdManifestTrack> candidates, RvFile expected, HashSet<ChdManifestTrack> used)
    {
        if (candidates == null || expected == null)
            return null;
        List<ChdManifestTrack> matches = candidates.Where(track =>
            track != null &&
            (used == null || !used.Contains(track)) &&
            (!expected.Size.HasValue || expected.Size.Value == 0 || expected.Size.Value == (ulong)Math.Max(0, track.Size)) &&
            OptionalHashEqual(expected.CRC, track.Crc32) &&
            OptionalHashEqual(expected.SHA1, track.Sha1) &&
            OptionalHashEqual(expected.MD5, track.Md5)).ToList();
        return matches.OrderBy(track => track.Number).FirstOrDefault();
    }

    private static bool OptionalHashEqual(byte[] expected, byte[] actual)
    {
        if (expected == null || expected.Length == 0)
            return true;
        return actual != null && BytesEqual(expected, actual);
    }

    private void ValidateForSerialization(bool requireCompleteHashes = false)
    {
        if (Schema != CurrentSchema)
            throw new InvalidDataException("Unsupported reconstruction metadata schema: " + Schema);
        ValidateSafeName(DescriptorName, "descriptor");
        ValidateDescriptor(DescriptorBytes, DescriptorSha256, "primary descriptor");
        ValidateTracks(Tracks, "primary view", requireCompleteHashes);
        if (DescriptorBytes != null && DescriptorBytes.Length > 0 &&
            !DescriptorGraphMatches(DescriptorName, DescriptorBytes, Tracks, out string descriptorGraphError))
            throw new InvalidDataException("Invalid primary descriptor graph: " + descriptorGraphError);
        if (Views == null)
            Views = new List<ChdManifestView>();
        if (Views.Count > MaxViews)
            throw new InvalidDataException("Too many reconstruction views.");
        HashSet<string> viewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Views.Count; i++)
        {
            ChdManifestView view = Views[i] ?? throw new InvalidDataException("Null reconstruction view.");
            if (string.IsNullOrWhiteSpace(view.Name) || !viewNames.Add(view.Name))
                throw new InvalidDataException("Reconstruction view names must be non-empty and unique.");
            ValidateSafeName(view.DescriptorName, "view descriptor");
            ValidateDescriptor(view.DescriptorBytes, view.DescriptorSha256, "view descriptor");
            ValidateTracks(view.Tracks, "view " + view.Name, requireCompleteHashes);
            if (view.DescriptorBytes != null && view.DescriptorBytes.Length > 0 &&
                !DescriptorGraphMatches(view.DescriptorName, view.DescriptorBytes, view.Tracks, out string viewDescriptorGraphError))
                throw new InvalidDataException("Invalid descriptor graph for view " + view.Name + ": " + viewDescriptorGraphError);
        }
        if (Auxiliaries == null)
            Auxiliaries = new List<ChdManifestAuxiliary>();
        if (Auxiliaries.Count > MaxAuxiliaries)
            throw new InvalidDataException("Too many reconstruction auxiliaries.");
        HashSet<string> auxiliaryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Auxiliaries.Count; i++)
        {
            ChdManifestAuxiliary auxiliary = Auxiliaries[i] ?? throw new InvalidDataException("Null reconstruction auxiliary.");
            ValidateSafeName(auxiliary.Name, "auxiliary");
            if (string.IsNullOrWhiteSpace(auxiliary.Name) || !auxiliaryNames.Add(auxiliary.Name.Replace('\\', '/')))
                throw new InvalidDataException("Auxiliary names must be non-empty and unique.");
            if (string.IsNullOrWhiteSpace(auxiliary.Role))
                throw new InvalidDataException("Auxiliary role is required.");
            byte[] bytes = auxiliary.Bytes ?? Array.Empty<byte>();
            if (!RvrmWireFormat.IsSupportedAuxiliaryLength(bytes.Length))
                throw new InvalidDataException("Reconstruction auxiliary is empty or too large: " + auxiliary.Name);
            if (auxiliary.Sha256 == null || auxiliary.Sha256.Length != 32 ||
                !BytesEqual(auxiliary.Sha256, HashBytes(bytes, SHA256.Create())))
                throw new InvalidDataException("Reconstruction auxiliary checksum mismatch: " + auxiliary.Name);
        }
    }

    private static void ValidateDescriptor(byte[] bytes, byte[] hash, string label)
    {
        bytes = bytes ?? Array.Empty<byte>();
        hash = hash ?? Array.Empty<byte>();
        if (bytes.Length > MaxDescriptorBytes)
            throw new InvalidDataException(label + " is too large.");
        if (bytes.Length == 0)
        {
            if (hash.Length != 0)
                throw new InvalidDataException(label + " has a checksum without content.");
            return;
        }
        if (hash.Length != 32 || !BytesEqual(hash, HashBytes(bytes, SHA256.Create())))
            throw new InvalidDataException(label + " checksum mismatch.");
    }

    private static void ValidateTracks(List<ChdManifestTrack> tracks, string label, bool requireCompleteHashes)
    {
        tracks = tracks ?? new List<ChdManifestTrack>();
        if (tracks.Count > MaxTracks)
            throw new InvalidDataException("Too many tracks in " + label + ".");
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> numbers = new HashSet<int>();
        for (int i = 0; i < tracks.Count; i++)
        {
            ChdManifestTrack track = tracks[i] ?? throw new InvalidDataException("Null track in " + label + ".");
            ValidateSafeName(track.Name, "track");
            if (string.IsNullOrWhiteSpace(track.Name) || !names.Add(track.Name.Replace('\\', '/')))
                throw new InvalidDataException("Track names must be non-empty and unique in " + label + ".");
            if (track.Number <= 0 || !numbers.Add(track.Number) || track.Size < 0)
                throw new InvalidDataException("Invalid track number or size in " + label + ".");
            ValidateHash(track.Crc32, 4, "CRC32");
            ValidateHash(track.Sha1, 20, "SHA1");
            ValidateHash(track.Md5, 16, "MD5");
            ValidateHash(track.Sha256, 32, "SHA256");
            if (requireCompleteHashes &&
                (track.Crc32 == null || track.Crc32.Length != 4 ||
                 track.Md5 == null || track.Md5.Length != 16 ||
                 track.Sha1 == null || track.Sha1.Length != 20 ||
                 track.Sha256 == null || track.Sha256.Length != 32))
                throw new InvalidDataException("Canonical payload identities require CRC32, MD5, SHA1, and SHA256 in " + label + ".");
        }
    }

    private static void ValidateHash(byte[] value, int length, string name)
    {
        if (value != null && value.Length != 0 && value.Length != length)
            throw new InvalidDataException("Invalid " + name + " length in reconstruction metadata.");
    }

    private static void ValidateSafeName(string name, string label)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        string normalized = name.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || normalized.Split(Path.DirectorySeparatorChar).Any(part => part == ".." || part.Length == 0))
            throw new InvalidDataException("Unsafe " + label + " path: " + name);
    }

    private static string ResolveDatDialect(RvFile destination, ChdEncodingProfileSpec profile, ChdReconstructionManifest previous)
    {
        if (profile.Family != "gdi")
        {
            if (profile.Family == "raw") return "raw-exact";
            if (profile.Family == "hdd") return "hard-disk-exact";
            if (profile.Family == "laserdisc") return "canonical-avi-exact";
            return previous?.Dialect ?? (profile.Family == "cd" ? "cue-dat" : "iso");
        }

        string descriptorExtension = Path.GetExtension(previous?.DescriptorName ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(descriptorExtension) && destination != null)
        {
            for (int i = 0; i < destination.ChildCount; i++)
            {
                RvFile child = destination.Child(i);
                string extension = Path.GetExtension(child?.Name ?? "").ToLowerInvariant();
                if (extension == ".cue" || extension == ".gdi")
                {
                    descriptorExtension = extension;
                    break;
                }
            }
        }

        if (descriptorExtension == ".cue")
            return "redump-gdrom-cue";
        if (descriptorExtension == ".gdi")
            return "tosec-gdi";
        return previous?.Dialect ?? "gdrom-internal";
    }

    private static bool IsOpticalFamily(string family) =>
        string.Equals(family, "cd", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(family, "gdi", StringComparison.OrdinalIgnoreCase);

    private static string PreferredDescriptorExtension(string family) =>
        string.Equals(family, "gdi", StringComparison.OrdinalIgnoreCase) ? ".gdi" : ".cue";

    private static bool IsDescriptorExtensionCompatible(string extension, string family)
    {
        extension = (extension ?? "").ToLowerInvariant();
        if (string.Equals(family, "gdi", StringComparison.OrdinalIgnoreCase))
            return extension == ".gdi" || extension == ".cue";
        if (string.Equals(family, "cd", StringComparison.OrdinalIgnoreCase))
            return extension == ".cue" || extension == ".toc";
        return false;
    }

    private static List<RvFile> GetExpectedMembers(RvFile destination, params string[] extensions)
    {
        HashSet<string> wanted = new HashSet<string>(extensions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        List<RvFile> result = new List<RvFile>();
        if (destination == null)
            return result;
        for (int i = 0; i < destination.ChildCount; i++)
        {
            RvFile child = destination.Child(i);
            if (child?.Name != null && child.IsFile && wanted.Contains(Path.GetExtension(child.Name)))
                result.Add(child);
        }
        return result;
    }

    private static string BuildDefaultDescriptorName(RvFile destination, string extension)
    {
        string name = Path.GetFileName(destination?.Name ?? "disc.chd");
        if (name.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);
        if (!IsDescriptorExtensionCompatible(Path.GetExtension(name),
                extension == ".gdi" ? "gdi" : "cd"))
            name += extension;
        return string.IsNullOrWhiteSpace(name) ? "disc" + extension : name;
    }

    private static IEnumerable<string> GetSidecarCandidates(string sourceChdPath, string expectedName, string extension)
    {
        string directory = Path.GetDirectoryName(sourceChdPath ?? "") ?? Environment.CurrentDirectory;
        HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string expectedFileName = Path.GetFileName((expectedName ?? "").Replace('\\', '/'));
        if (!string.IsNullOrWhiteSpace(expectedFileName))
            candidates.Add(Path.Combine(directory, expectedFileName));

        string sourceName = Path.GetFileName(sourceChdPath ?? "");
        if (sourceName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            sourceName = sourceName.Substring(0, sourceName.Length - 4);
        if (!string.Equals(Path.GetExtension(sourceName), extension, StringComparison.OrdinalIgnoreCase))
        {
            string nestedExtension = Path.GetExtension(sourceName);
            if (nestedExtension == ".cue" || nestedExtension == ".gdi" || nestedExtension == ".toc")
                sourceName = Path.GetFileNameWithoutExtension(sourceName);
            sourceName += extension;
        }
        if (!string.IsNullOrWhiteSpace(sourceName))
            candidates.Add(Path.Combine(directory, sourceName));
        return candidates;
    }

    private static bool TryReadBoundedSidecar(string path, out byte[] bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        FileInfo info = new FileInfo(path);
        if (!RvrmWireFormat.IsSupportedAuxiliaryLength(info.Length))
            return false;
        bytes = File.ReadAllBytes(path);
        return RvrmWireFormat.IsSupportedAuxiliaryLength(bytes.Length);
    }

    private static bool DescriptorMatchesExpected(RvFile expected, byte[] bytes, out string error)
    {
        error = "";
        bytes = bytes ?? Array.Empty<byte>();
        if (expected == null)
            return bytes.Length > 0;
        if (expected.Size.HasValue && expected.Size.Value != 0 && expected.Size.Value != (ulong)bytes.LongLength)
        {
            error = "size mismatch";
            return false;
        }
        if (expected.CRC != null && !BytesEqual(expected.CRC, ComputeCrc32(bytes)))
        {
            error = "CRC32 mismatch";
            return false;
        }
        if (expected.SHA1 != null && !BytesEqual(expected.SHA1, HashBytes(bytes, SHA1.Create())))
        {
            error = "SHA-1 mismatch";
            return false;
        }
        if (expected.MD5 != null && !BytesEqual(expected.MD5, HashBytes(bytes, MD5.Create())))
        {
            error = "MD5 mismatch";
            return false;
        }
        return bytes.Length > 0;
    }

    private static bool HasExpectedContentHash(RvFile expected) =>
        expected != null &&
        ((expected.CRC != null && expected.CRC.Length == 4) ||
         (expected.SHA1 != null && expected.SHA1.Length == 20) ||
         (expected.MD5 != null && expected.MD5.Length == 16));

    private static byte[] ComputeCrc32(byte[] bytes)
    {
        uint crc = 0xffffffff;
        for (int i = 0; i < bytes.Length; i++)
            crc = UpdateCrc32(crc, bytes[i]);
        crc ^= 0xffffffff;
        return new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc };
    }

    private static bool DescriptorGraphMatches(string descriptorName, byte[] descriptorBytes, List<ChdManifestTrack> tracks, out string error)
    {
        error = "";
        List<(int number, string name)> members;
        try
        {
            members = GetDescriptorMembers(descriptorName, descriptorBytes);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        if (members.Count == 0)
        {
            error = "the descriptor contains no payload references";
            return false;
        }

        HashSet<string> trackNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tracks != null)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null && !string.IsNullOrWhiteSpace(tracks[i].Name))
                    trackNames.Add(NormalizeRelativeMemberName(tracks[i].Name).Replace('\\', '/'));
            }
        }
        HashSet<string> referencedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < members.Count; i++)
        {
            string member = NormalizeRelativeMemberName(members[i].name).Replace('\\', '/');
            if (!trackNames.Contains(member))
            {
                error = "descriptor payload is absent from the manifest: " + members[i].name;
                return false;
            }
            referencedNames.Add(member);
        }
        foreach (string trackName in trackNames)
        {
            if (!referencedNames.Contains(trackName))
            {
                error = "manifest payload is not referenced by the descriptor: " + trackName;
                return false;
            }
        }
        return true;
    }

    private static List<(int number, string name)> GetDescriptorMembers(string descriptorName, byte[] descriptorBytes)
    {
        string extension = Path.GetExtension(descriptorName ?? "").ToLowerInvariant();
        string text;
        using (MemoryStream memory = new MemoryStream(descriptorBytes ?? Array.Empty<byte>(), false))
        using (StreamReader reader = new StreamReader(memory, new UTF8Encoding(false, true), true))
            text = reader.ReadToEnd();
        return ParseDescriptorMembers(extension, Regex.Split(text, "\\r\\n|\\n|\\r"));
    }

    private static List<(int number, string name)> ParseDescriptorMembers(string extension, string[] lines)
    {
        List<(int number, string name)> members = new List<(int, string)>();
        if (extension == ".gdi")
        {
            for (int i = 1; i < lines.Length; i++)
            {
                Match match = Regex.Match(lines[i], @"^\s*(\d+)\s+\d+\s+\d+\s+\d+\s+(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    members.Add((number, match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value));
            }
            return members;
        }

        int nextNumber = 1;
        for (int i = 0; i < lines.Length; i++)
        {
            Match match = Regex.Match(lines[i], @"^\s*(?:FILE|DATAFILE|AUDIOFILE)\s+(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;
            string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!members.Any(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase)))
                members.Add((nextNumber++, name));
        }
        return members;
    }

    private static List<(int number, string name)> GetSourceMembers(string descriptorPath)
    {
        string extension = Path.GetExtension(descriptorPath).ToLowerInvariant();
        if (extension == ".iso")
            return new List<(int, string)> { (1, Path.GetFileName(descriptorPath)) };
        return ParseDescriptorMembers(extension, File.ReadAllLines(descriptorPath));
    }

    private static ChdManifestTrack HashTrack(string path, int number, string name)
    {
        uint crc = 0xffffffff;
        using (SHA1 sha1 = SHA1.Create())
        using (MD5 md5 = MD5.Create())
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] buffer = new byte[1024 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha1.TransformBlock(buffer, 0, read, null, 0);
                md5.TransformBlock(buffer, 0, read, null, 0);
                sha256.TransformBlock(buffer, 0, read, null, 0);
                for (int i = 0; i < read; i++)
                    crc = UpdateCrc32(crc, buffer[i]);
            }
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            crc ^= 0xffffffff;
            return new ChdManifestTrack
            {
                Number = number,
                Name = name,
                Size = stream.Length,
                Crc32 = new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc },
                Sha1 = sha1.Hash,
                Md5 = md5.Hash,
                Sha256 = sha256.Hash
            };
        }
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        return crc;
    }

    private static byte[] HashBytes(byte[] data, HashAlgorithm algorithm)
    {
        using (algorithm)
            return algorithm.ComputeHash(data ?? Array.Empty<byte>());
    }

    private static int ExtractTrackNumber(string name)
    {
        Match match = Regex.Match(name ?? "", @"(?:track|\(track)\s*[_ -]?(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out int number) ? number : -1;
    }

    private static bool IsPayloadName(string name, string family)
    {
        string extension = Path.GetExtension(name ?? "").ToLowerInvariant();
        switch ((family ?? "").ToLowerInvariant())
        {
            case "raw": return extension == ".raw";
            case "hdd": return extension == ".img" || extension == ".hdd" || extension == ".hd" || extension == ".raw";
            case "laserdisc": return extension == ".avi";
        }
        return extension == ".bin" || extension == ".raw" || extension == ".iso";
    }

    private static string ResolveCreateMode(string family)
    {
        switch ((family ?? "").ToLowerInvariant())
        {
            case "cd":
            case "gdi": return "createcd";
            case "raw": return "createraw";
            case "hdd": return "createhd";
            case "laserdisc": return "createld";
            default: return "createdvd";
        }
    }

    private static string ResolveExtractMode(string family)
    {
        switch ((family ?? "").ToLowerInvariant())
        {
            case "cd":
            case "gdi": return "extractcd-split";
            case "raw": return "extractraw";
            case "hdd": return "extracthd";
            case "laserdisc": return "extractld";
            default: return "extractdvd";
        }
    }

    private static string NormalizeRelativeMemberName(string name)
    {
        string normalized = (name ?? "").Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim().Trim('"');
        while (normalized.StartsWith("." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            normalized = normalized.Substring(2);
        if (Path.IsPathRooted(normalized) || normalized.Split(Path.DirectorySeparatorChar).Any(part => part == ".."))
            throw new InvalidDataException("Unsafe descriptor member path: " + name);
        return normalized;
    }

    private static bool IsWithinDirectory(string path, string directory)
    {
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Clone(byte[] value)
    {
        if (value == null)
            return Array.Empty<byte>();
        byte[] clone = new byte[value.Length];
        Buffer.BlockCopy(value, 0, clone, 0, value.Length);
        return clone;
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        int diff = 0;
        for (int i = 0; i < left.Length; i++)
            diff |= left[i] ^ right[i];
        return diff == 0;
    }
}
