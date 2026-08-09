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
    private const int MaxManifestBytes = 64 * 1024 * 1024;
    private const int MaxDescriptorBytes = 16 * 1024 * 1024;
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
    public string DescriptorName { get; set; } = "";
    public byte[] DescriptorBytes { get; set; } = Array.Empty<byte>();
    public byte[] DescriptorSha256 { get; set; } = Array.Empty<byte>();
    public List<ChdManifestTrack> Tracks { get; set; } = new List<ChdManifestTrack>();
    public List<ChdManifestView> Views { get; set; } = new List<ChdManifestView>();
    public List<ChdManifestAuxiliary> Auxiliaries { get; set; } = new List<ChdManifestAuxiliary>();

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
            ChdReconstructionManifest created = CreateBase(profile, identity);
            created.DescriptorName = extension == ".cue" || extension == ".gdi" || extension == ".toc"
                ? Path.GetFileName(fullInput)
                : "";
            created.Dialect = ChdDialect.DetectSource(fullInput, profile.Family);
            created.CreateMode = ResolveCreateMode(profile.Family);
            created.ExtractMode = ResolveExtractMode(profile.Family);

            if (!string.IsNullOrWhiteSpace(created.DescriptorName))
            {
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

        if (destination != null)
        {
            int fallbackTrack = 1;
            for (int i = 0; i < destination.ChildCount; i++)
            {
                RvFile child = destination.Child(i);
                if (child == null || !child.IsFile || !IsPayloadName(child.Name, profile.Family))
                    continue;
                int number = ExtractTrackNumber(child.Name);
                if (number <= 0)
                    number = fallbackTrack;
                fallbackTrack = Math.Max(fallbackTrack + 1, number + 1);
                manifest.Tracks.Add(new ChdManifestTrack
                {
                    Number = number,
                    Name = NormalizeRelativeMemberName(child.Name),
                    Size = child.Size.HasValue && child.Size.Value <= long.MaxValue ? (long)child.Size.Value : 0,
                    Crc32 = Clone(child.CRC),
                    Sha1 = Clone(child.SHA1),
                    Md5 = Clone(child.MD5),
                    Sha256 = Clone(previous?.Tracks?.FirstOrDefault(track =>
                        string.Equals(track?.Name, child.Name, StringComparison.OrdinalIgnoreCase))?.Sha256)
                });
            }
        }

        if (manifest.Tracks.Count == 0 && previous?.Tracks != null)
            manifest.Tracks.AddRange(previous.Tracks);
        manifest.Tracks.Sort((left, right) => left.Number.CompareTo(right.Number));
        return manifest;
    }

    public byte[] Serialize()
    {
        ValidateForSerialization();
        using (MemoryStream memory = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memory, new UTF8Encoding(false), true))
        {
            writer.Write(Magic);
            writer.Write(Schema);
            WriteString(writer, ProfileId);
            writer.Write(ProfileRevision);
            writer.Write(WriterRevision);
            WriteString(writer, Family);
            WriteString(writer, Storage);
            WriteString(writer, Dialect);
            WriteString(writer, CreateMode);
            WriteString(writer, ExtractMode);
            WriteString(writer, ChdmanVersion);
            WriteString(writer, ChdmanBanner);
            WriteString(writer, ChdmanSha256);
            WriteString(writer, CapabilityFingerprint);
            WriteString(writer, DescriptorName);
            WriteBytes(writer, DescriptorBytes);
            WriteBytes(writer, DescriptorSha256);
            writer.Write(Tracks?.Count ?? 0);
            if (Tracks != null)
            {
                for (int i = 0; i < Tracks.Count; i++)
                {
                    WriteTrack(writer, Tracks[i]);
                }
            }
            writer.Write(Views?.Count ?? 0);
            if (Views != null)
            {
                for (int i = 0; i < Views.Count; i++)
                {
                    ChdManifestView view = Views[i] ?? new ChdManifestView();
                    WriteString(writer, view.Name);
                    WriteString(writer, view.Dialect);
                    WriteString(writer, view.DescriptorName);
                    WriteBytes(writer, view.DescriptorBytes);
                    WriteBytes(writer, view.DescriptorSha256);
                    writer.Write(view.Tracks?.Count ?? 0);
                    if (view.Tracks != null)
                    {
                        for (int j = 0; j < view.Tracks.Count; j++)
                            WriteTrack(writer, view.Tracks[j]);
                    }
                }
            }
            writer.Write(Auxiliaries?.Count ?? 0);
            if (Auxiliaries != null)
            {
                for (int i = 0; i < Auxiliaries.Count; i++)
                {
                    ChdManifestAuxiliary auxiliary = Auxiliaries[i] ?? new ChdManifestAuxiliary();
                    WriteString(writer, auxiliary.Name);
                    WriteString(writer, auxiliary.Role);
                    WriteBytes(writer, auxiliary.Bytes);
                    WriteBytes(writer, auxiliary.Sha256);
                }
            }
            writer.Flush();
            if (memory.Length > MaxManifestBytes - IntegrityBytes)
                throw new InvalidDataException("Reconstruction metadata is too large.");
            byte[] payload = memory.ToArray();
            byte[] digest = HashBytes(payload, SHA256.Create());
            byte[] result = new byte[payload.Length + digest.Length];
            Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
            Buffer.BlockCopy(digest, 0, result, payload.Length, digest.Length);
            return result;
        }
    }

    public static bool TryDeserialize(byte[] data, out ChdReconstructionManifest manifest, out string error)
    {
        manifest = null;
        error = "";
        if (data == null || data.Length < 16 || data.Length > MaxManifestBytes)
        {
            error = "Reconstruction metadata is empty or truncated.";
            return false;
        }
        try
        {
            int payloadLength = data.Length;
            int declaredSchema;
            using (MemoryStream headerMemory = new MemoryStream(data, false))
            using (BinaryReader headerReader = new BinaryReader(headerMemory, Encoding.UTF8, true))
            {
                if (headerReader.ReadUInt32() != Magic)
                {
                    error = "Reconstruction metadata has an invalid signature.";
                    return false;
                }
                declaredSchema = headerReader.ReadInt32();
            }
            if (declaredSchema != CurrentSchema)
            {
                error = "Unsupported reconstruction metadata schema: " + declaredSchema;
                return false;
            }
            if (data.Length < 16 + IntegrityBytes)
                throw new InvalidDataException("Reconstruction metadata integrity trailer is truncated.");
            payloadLength -= IntegrityBytes;
            byte[] expected = new byte[IntegrityBytes];
            Buffer.BlockCopy(data, payloadLength, expected, 0, expected.Length);
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 0, payload, 0, payload.Length);
            if (!BytesEqual(expected, HashBytes(payload, SHA256.Create())))
                throw new InvalidDataException("Reconstruction metadata integrity checksum mismatch.");

            using (MemoryStream memory = new MemoryStream(data, 0, payloadLength, false))
            using (BinaryReader reader = new BinaryReader(memory, Encoding.UTF8, true))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    error = "Reconstruction metadata has an invalid signature.";
                    return false;
                }
                int schema = reader.ReadInt32();
                if (schema != CurrentSchema)
                {
                    error = "Unsupported reconstruction metadata schema: " + schema;
                    return false;
                }
                string profileId = ReadString(reader);
                int profileRevision = reader.ReadInt32();
                int writerRevision = reader.ReadInt32();
                string family = ReadString(reader);
                string storage = ReadString(reader);
                ChdReconstructionManifest parsed = new ChdReconstructionManifest
                {
                    Schema = schema,
                    ProfileId = profileId,
                    ProfileRevision = profileRevision,
                    WriterRevision = writerRevision,
                    Family = family,
                    Storage = storage,
                    Dialect = ReadString(reader),
                    CreateMode = ReadString(reader),
                    ExtractMode = ReadString(reader),
                    ChdmanVersion = ReadString(reader),
                    ChdmanBanner = ReadString(reader),
                    ChdmanSha256 = ReadString(reader),
                    CapabilityFingerprint = ReadString(reader),
                    DescriptorName = ReadString(reader),
                    DescriptorBytes = ReadBytes(reader, MaxDescriptorBytes),
                    DescriptorSha256 = ReadBytes(reader, 64)
                };
                int trackCount = reader.ReadInt32();
                if (trackCount < 0 || trackCount > MaxTracks)
                    throw new InvalidDataException("Invalid manifest track count.");
                for (int i = 0; i < trackCount; i++)
                    parsed.Tracks.Add(ReadTrack(reader));
                int viewCount = reader.ReadInt32();
                if (viewCount < 0 || viewCount > MaxViews)
                    throw new InvalidDataException("Invalid manifest view count.");
                for (int i = 0; i < viewCount; i++)
                {
                    ChdManifestView view = new ChdManifestView
                    {
                        Name = ReadString(reader),
                        Dialect = ReadString(reader),
                        DescriptorName = ReadString(reader),
                        DescriptorBytes = ReadBytes(reader, MaxDescriptorBytes),
                        DescriptorSha256 = ReadBytes(reader, 64)
                    };
                    int viewTrackCount = reader.ReadInt32();
                    if (viewTrackCount < 0 || viewTrackCount > MaxTracks)
                        throw new InvalidDataException("Invalid manifest view track count.");
                    for (int j = 0; j < viewTrackCount; j++)
                        view.Tracks.Add(ReadTrack(reader));
                    parsed.Views.Add(view);
                }
                int auxiliaryCount = reader.ReadInt32();
                if (auxiliaryCount < 0 || auxiliaryCount > MaxAuxiliaries)
                    throw new InvalidDataException("Invalid manifest auxiliary count.");
                for (int i = 0; i < auxiliaryCount; i++)
                {
                    parsed.Auxiliaries.Add(new ChdManifestAuxiliary
                    {
                        Name = ReadString(reader),
                        Role = ReadString(reader),
                        Bytes = ReadBytes(reader, MaxDescriptorBytes),
                        Sha256 = ReadBytes(reader, 64)
                    });
                }
                if (memory.Position != memory.Length)
                    throw new InvalidDataException("Unexpected trailing reconstruction metadata.");
                parsed.ValidateForSerialization();
                manifest = parsed;
                return true;
            }
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
            byte[] descriptor = Encoding.ASCII.GetBytes("FILE \"track01.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            ChdReconstructionManifest original = new ChdReconstructionManifest
            {
                ProfileId = ChdEncodingProfile.ProfileId,
                ProfileRevision = ChdEncodingProfile.CurrentProfileRevision,
                WriterRevision = 1,
                Family = "cd",
                Storage = "archive",
                Dialect = "cue-exact",
                CreateMode = "createcd",
                ExtractMode = "extractcd-split",
                ChdmanVersion = "test",
                ChdmanBanner = "test banner",
                ChdmanSha256 = new string('a', 64),
                CapabilityFingerprint = "test",
                DescriptorName = "disc.cue",
                DescriptorBytes = descriptor,
                DescriptorSha256 = HashBytes(descriptor, SHA256.Create()),
                Tracks = new List<ChdManifestTrack>
                {
                    new ChdManifestTrack
                    {
                        Number = 1,
                        Name = "track01.bin",
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
                        Name = "data-view",
                        Dialect = "iso",
                        Tracks = new List<ChdManifestTrack>
                        {
                            new ChdManifestTrack { Number = 1, Name = "disc.iso", Size = 2048, Sha1 = new byte[20], Sha256 = new byte[32] }
                        }
                    }
                },
                Auxiliaries = new List<ChdManifestAuxiliary>
                {
                    new ChdManifestAuxiliary
                    {
                        Name = "disc.sbi",
                        Role = "sbi-subchannel-correction",
                        Bytes = new byte[] { 0x53, 0x42, 0x49, 0x00 },
                        Sha256 = HashBytes(new byte[] { 0x53, 0x42, 0x49, 0x00 }, SHA256.Create())
                    }
                }
            };
            if (!TryDeserialize(original.Serialize(), out ChdReconstructionManifest roundTrip, out error))
                return false;
            if (roundTrip.Schema != CurrentSchema || roundTrip.Tracks.Count != 1 || roundTrip.Views.Count != 1 || roundTrip.Auxiliaries.Count != 1 ||
                !string.Equals(roundTrip.Storage, "archive", StringComparison.Ordinal) ||
                !string.Equals(roundTrip.Tracks[0].Name, "track01.bin", StringComparison.Ordinal) ||
                !BytesEqual(roundTrip.DescriptorBytes, descriptor))
            {
                error = "Manifest serialization round trip changed reconstruction data.";
                return false;
            }
            byte[] damaged = original.Serialize();
            damaged[damaged.Length / 2] ^= 0x40;
            if (TryDeserialize(damaged, out _, out _))
            {
                error = "Manifest integrity checksum accepted modified metadata.";
                return false;
            }
            byte[] obsoleteSchema = original.Serialize();
            obsoleteSchema[4] = 5;
            if (TryDeserialize(obsoleteSchema, out _, out _))
            {
                error = "Manifest parser accepted an unreleased migration schema.";
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

    private void ValidateForSerialization()
    {
        if (Schema != CurrentSchema)
            throw new InvalidDataException("Unsupported reconstruction metadata schema: " + Schema);
        ValidateSafeName(DescriptorName, "descriptor");
        ValidateDescriptor(DescriptorBytes, DescriptorSha256, "primary descriptor");
        ValidateTracks(Tracks, "primary view");
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
            ValidateTracks(view.Tracks, "view " + view.Name);
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
            if (bytes.Length > MaxDescriptorBytes)
                throw new InvalidDataException("Reconstruction auxiliary is too large: " + auxiliary.Name);
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

    private static void ValidateTracks(List<ChdManifestTrack> tracks, string label)
    {
        tracks = tracks ?? new List<ChdManifestTrack>();
        if (tracks.Count > MaxTracks)
            throw new InvalidDataException("Too many tracks in " + label + ".");
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < tracks.Count; i++)
        {
            ChdManifestTrack track = tracks[i] ?? throw new InvalidDataException("Null track in " + label + ".");
            ValidateSafeName(track.Name, "track");
            if (string.IsNullOrWhiteSpace(track.Name) || !names.Add(track.Name.Replace('\\', '/')))
                throw new InvalidDataException("Track names must be non-empty and unique in " + label + ".");
            if (track.Number <= 0 || track.Size < 0)
                throw new InvalidDataException("Invalid track number or size in " + label + ".");
            ValidateHash(track.Crc32, 4, "CRC32");
            ValidateHash(track.Sha1, 20, "SHA1");
            ValidateHash(track.Md5, 16, "MD5");
            ValidateHash(track.Sha256, 32, "SHA256");
            if (track.Sha256 == null || track.Sha256.Length != 32)
                throw new InvalidDataException("SHA256 is required for every payload in reconstruction schema 1.");
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

    private static void WriteTrack(BinaryWriter writer, ChdManifestTrack value)
    {
        ChdManifestTrack track = value ?? new ChdManifestTrack();
        writer.Write(track.Number);
        WriteString(writer, track.Name);
        writer.Write(track.Size);
        WriteBytes(writer, track.Crc32);
        WriteBytes(writer, track.Sha1);
        WriteBytes(writer, track.Md5);
        WriteBytes(writer, track.Sha256);
    }

    private static ChdManifestTrack ReadTrack(BinaryReader reader)
    {
        return new ChdManifestTrack
        {
            Number = reader.ReadInt32(),
            Name = ReadString(reader),
            Size = reader.ReadInt64(),
            Crc32 = ReadBytes(reader, 64),
            Sha1 = ReadBytes(reader, 64),
            Md5 = ReadBytes(reader, 64),
            Sha256 = ReadBytes(reader, 64)
        };
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

    private static List<(int number, string name)> GetSourceMembers(string descriptorPath)
    {
        string extension = Path.GetExtension(descriptorPath).ToLowerInvariant();
        if (extension == ".iso")
            return new List<(int, string)> { (1, Path.GetFileName(descriptorPath)) };
        string[] lines = File.ReadAllLines(descriptorPath);
        List<(int number, string name)> members = new List<(int, string)>();
        if (extension == ".gdi")
        {
            for (int i = 1; i < lines.Length; i++)
            {
                Match match = Regex.Match(lines[i], @"^\s*(\d+)\s+\d+\s+\d+\s+\d+\s+(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    members.Add((number, match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value));
            }
        }
        else
        {
            int number = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = Regex.Match(lines[i], @"^\s*(?:FILE|DATAFILE|AUDIOFILE)\s+(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;
                string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!members.Any(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase)))
                    members.Add((number++, name));
            }
        }
        return members;
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

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] data = Encoding.UTF8.GetBytes(value ?? "");
        if (data.Length > 1024 * 1024)
            throw new InvalidDataException("Manifest string is too large.");
        writer.Write(data.Length);
        writer.Write(data);
    }

    private static string ReadString(BinaryReader reader)
    {
        return Encoding.UTF8.GetString(ReadBytes(reader, 1024 * 1024));
    }

    private static void WriteBytes(BinaryWriter writer, byte[] value)
    {
        byte[] data = value ?? Array.Empty<byte>();
        writer.Write(data.Length);
        writer.Write(data);
    }

    private static byte[] ReadBytes(BinaryReader reader, int maxLength)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > maxLength)
            throw new InvalidDataException("Invalid manifest field length.");
        byte[] data = reader.ReadBytes(length);
        if (data.Length != length)
            throw new EndOfStreamException();
        return data;
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
