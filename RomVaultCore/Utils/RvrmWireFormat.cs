using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RomVaultCore.Utils;

// These are the only values persisted by the canonical RVRM schema 1.  The operational
// ChdReconstructionManifest also carries temporary names and writer details,
// but those deliberately never enter this model.
internal enum RvrmMediaFamily : byte
{
    Cd = 1,
    GdRom = 2,
    Dvd = 3,
    Psp = 4,
    Raw = 5,
    HardDisk = 6,
    LaserDisc = 7
}

internal enum RvrmRecipe : ushort
{
    RawExact = 1,
    HardDiskExact = 2,
    CanonicalAviExact = 3,
    TosecGdi = 4,
    TocExact = 5,
    PspIso = 6,
    Iso = 7,
    RedumpGdRomCue = 8,
    CueComplexLayout = 9,
    CueMixed = 10,
    CueMode1_2048Audio = 11,
    CueMode1_2048 = 12,
    CueMode2_2352Audio = 13,
    CueMode2_2352 = 14,
    CueMode1_2352Audio = 15,
    CueMode1_2352 = 16,
    CueAudio = 17,
    CueUnknown = 18,
    CueDat = 19,
    GdRomInternal = 20,
    IsoFromMode1_2048 = 21,
    IsoFromMode1_2352 = 22,
    CueUnicode = 23,
    CueExact = 24,
    Unknown = 25
}

internal enum RvrmAuxiliaryKind : ushort
{
    SbiSubchannelCorrection = 1
}

internal sealed class RvrmPayloadIdentity
{
    public int Number { get; set; }
    public long Size { get; set; }
    public byte[] Crc32 { get; set; }
    public byte[] Md5 { get; set; }
    public byte[] Sha1 { get; set; }
    public byte[] Sha256 { get; set; }
}

internal sealed class RvrmViewRecord
{
    public RvrmRecipe Recipe { get; set; }
    public List<RvrmPayloadIdentity> Payloads { get; } = new List<RvrmPayloadIdentity>();
}

internal sealed class RvrmAuxiliaryRecord
{
    public RvrmAuxiliaryKind Kind { get; set; }
    public byte[] Bytes { get; set; }
}

internal sealed class RvrmOpaqueSection
{
    public ushort Id { get; set; }
    public ushort Flags { get; set; }
    public byte[] Payload { get; set; }
}

internal sealed class RvrmRecord
{
    public RvrmMediaFamily Family { get; set; }
    public RvrmRecipe PrimaryRecipe { get; set; }
    public List<RvrmPayloadIdentity> Payloads { get; } = new List<RvrmPayloadIdentity>();
    public List<RvrmViewRecord> Views { get; } = new List<RvrmViewRecord>();
    public List<RvrmAuxiliaryRecord> Auxiliaries { get; } = new List<RvrmAuxiliaryRecord>();
    public List<RvrmOpaqueSection> OpaqueSections { get; } = new List<RvrmOpaqueSection>();
}

internal static class RvrmWireFormat
{
    internal const uint Magic = 0x5256524d; // RVRM
    internal const uint LayoutMarker = 0x31435652; // RVC1 on disk
    internal const int IntegrityBytes = 32;

    private const ulong FeatureTypedIdentifiers = 1UL << 0;
    private const ulong FeatureFixedHashSet = 1UL << 1;
    private const ulong FeatureLengthDelimitedSections = 1UL << 2;
    private const ulong KnownRequiredFeatures = FeatureTypedIdentifiers | FeatureFixedHashSet | FeatureLengthDelimitedSections;

    private const ushort SectionCore = 1;
    private const ushort SectionViews = 2;
    private const ushort SectionAuxiliaries = 3;
    private const ushort SectionFlagRequired = 1;
    private const ushort KnownSectionFlags = SectionFlagRequired;
    private const int MaxSections = 32;
    private const int MaxTracks = 1000;
    private const int MaxViews = 16;
    private const int MaxAuxiliaries = 64;
    // CHD metadata stores its byte length in 24 bits. Keep the complete RVRM,
    // including its integrity trailer, within that physical on-disk limit.
    internal const int MaxManifestBytes = 0x00ffffff;
    // Leave roughly one MiB for the RVRM header, payload identities, section
    // framing, other auxiliaries, and the integrity trailer.
    internal const int MaxAuxiliaryBytes = 15 * 1024 * 1024;

    internal static bool IsSupportedManifestLength(long length) => length >= 0 && length <= MaxManifestBytes;

    internal static bool IsSupportedAuxiliaryLength(long length) => length > 0 && length <= MaxAuxiliaryBytes;

    internal static byte[] Serialize(ChdReconstructionManifest draft)
    {
        RvrmRecord record = CreateRecord(draft);
        List<EncodedSection> sections = new List<EncodedSection>
        {
            new EncodedSection(SectionCore, SectionFlagRequired, EncodeCore(record))
        };
        if (record.Views.Count > 0)
            sections.Add(new EncodedSection(SectionViews, SectionFlagRequired, EncodeViews(record.Views)));
        if (record.Auxiliaries.Count > 0)
            sections.Add(new EncodedSection(SectionAuxiliaries, SectionFlagRequired, EncodeAuxiliaries(record.Auxiliaries)));
        for (int i = 0; i < record.OpaqueSections.Count; i++)
        {
            RvrmOpaqueSection opaque = record.OpaqueSections[i];
            sections.Add(new EncodedSection(opaque.Id, opaque.Flags, Clone(opaque.Payload)));
        }

        using (MemoryStream memory = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memory, new UTF8Encoding(false), true))
        {
            writer.Write(Magic);
            writer.Write(ChdReconstructionManifest.CurrentSchema);
            writer.Write(LayoutMarker);
            writer.Write(KnownRequiredFeatures);
            writer.Write(sections.Count);
            for (int i = 0; i < sections.Count; i++)
            {
                writer.Write(sections[i].Id);
                writer.Write(sections[i].Flags);
                writer.Write(sections[i].Payload.Length);
                writer.Write(sections[i].Payload);
            }
            writer.Flush();
            if (!IsSupportedManifestLength(memory.Length + IntegrityBytes))
                throw new InvalidDataException("Reconstruction metadata is too large.");
            return AddIntegrityTrailer(memory.ToArray());
        }
    }

    internal static bool TryDeserialize(byte[] payload, out ChdReconstructionManifest manifest, out string error)
    {
        manifest = null;
        error = "";
        try
        {
            if (payload == null || payload.Length < 24 || !IsSupportedManifestLength((long)payload.Length + IntegrityBytes))
                throw new InvalidDataException("Reconstruction metadata is empty or truncated.");

            RvrmRecord record = new RvrmRecord();
            bool hasCore = false;
            bool hasViewsSection = false;
            bool hasAuxiliariesSection = false;
            ushort previousSectionId = 0;
            HashSet<ushort> seenSections = new HashSet<ushort>();
            using (MemoryStream memory = new MemoryStream(payload, false))
            using (BinaryReader reader = new BinaryReader(memory, Encoding.UTF8, true))
            {
                if (reader.ReadUInt32() != Magic || reader.ReadInt32() != ChdReconstructionManifest.CurrentSchema || reader.ReadUInt32() != LayoutMarker)
                    throw new InvalidDataException("Reconstruction metadata has an invalid schema or layout marker.");
                ulong requiredFeatures = reader.ReadUInt64();
                if ((requiredFeatures & ~KnownRequiredFeatures) != 0)
                    throw new InvalidDataException("Reconstruction metadata requires unsupported features.");
                if ((requiredFeatures & KnownRequiredFeatures) != KnownRequiredFeatures)
                    throw new InvalidDataException("Reconstruction metadata omits required schema-1 features.");
                int sectionCount = reader.ReadInt32();
                if (sectionCount <= 0 || sectionCount > MaxSections)
                    throw new InvalidDataException("Invalid reconstruction metadata section count.");

                for (int i = 0; i < sectionCount; i++)
                {
                    ushort id = reader.ReadUInt16();
                    ushort flags = reader.ReadUInt16();
                    int length = reader.ReadInt32();
                    if ((flags & ~KnownSectionFlags) != 0 || length < 0 || length > memory.Length - memory.Position)
                        throw new InvalidDataException("Invalid reconstruction metadata section header.");
                    if (id <= previousSectionId)
                        throw new InvalidDataException("Reconstruction metadata sections are not in canonical order.");
                    previousSectionId = id;
                    if (!seenSections.Add(id))
                        throw new InvalidDataException("Duplicate reconstruction metadata section: " + id + ".");
                    byte[] section = ReadFixedBytes(reader, length);
                    switch (id)
                    {
                        case SectionCore:
                            if (flags != SectionFlagRequired)
                                throw new InvalidDataException("The core reconstruction section must be required.");
                            DecodeCore(section, record);
                            hasCore = true;
                            break;
                        case SectionViews:
                            if (flags != SectionFlagRequired)
                                throw new InvalidDataException("The alternate-view reconstruction section must be required.");
                            DecodeViews(section, record.Views);
                            hasViewsSection = true;
                            break;
                        case SectionAuxiliaries:
                            if (flags != SectionFlagRequired)
                                throw new InvalidDataException("The auxiliary reconstruction section must be required.");
                            DecodeAuxiliaries(section, record.Auxiliaries);
                            hasAuxiliariesSection = true;
                            break;
                        default:
                            if ((flags & SectionFlagRequired) != 0)
                                throw new InvalidDataException("Unsupported required reconstruction metadata section: " + id + ".");
                            record.OpaqueSections.Add(new RvrmOpaqueSection { Id = id, Flags = flags, Payload = section });
                            break;
                    }
                }
                if (memory.Position != memory.Length)
                    throw new InvalidDataException("Unexpected trailing reconstruction metadata.");
            }
            if (!hasCore)
                throw new InvalidDataException("Reconstruction metadata has no core section.");
            if (hasViewsSection != (record.Views.Count > 0) || hasAuxiliariesSection != (record.Auxiliaries.Count > 0))
                throw new InvalidDataException("Reconstruction metadata contains a noncanonical empty section.");
            ValidateRecord(record);
            manifest = CreateRuntimeManifest(record);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            manifest = null;
            return false;
        }
    }

    internal static bool CanonicallyEquals(ChdReconstructionManifest left, ChdReconstructionManifest right, out string error)
    {
        error = "";
        try
        {
            if (!BytesEqual(Serialize(left), Serialize(right)))
            {
                error = "The conversion changed embedded reconstruction identity metadata.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "Could not compare reconstruction identity metadata: " + ex.Message;
            return false;
        }
    }

    internal static bool TryMapRecipe(string value, out RvrmRecipe recipe)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "raw-exact": recipe = RvrmRecipe.RawExact; return true;
            case "hard-disk-exact": recipe = RvrmRecipe.HardDiskExact; return true;
            case "canonical-avi-exact": recipe = RvrmRecipe.CanonicalAviExact; return true;
            case "tosec-gdi": recipe = RvrmRecipe.TosecGdi; return true;
            case "toc-exact": recipe = RvrmRecipe.TocExact; return true;
            case "psp-iso": recipe = RvrmRecipe.PspIso; return true;
            case "iso": recipe = RvrmRecipe.Iso; return true;
            case "redump-gdrom-cue": recipe = RvrmRecipe.RedumpGdRomCue; return true;
            case "cue-complex-layout": recipe = RvrmRecipe.CueComplexLayout; return true;
            case "cue-mixed": recipe = RvrmRecipe.CueMixed; return true;
            case "cue-mode1-2048-audio": recipe = RvrmRecipe.CueMode1_2048Audio; return true;
            case "cue-mode1-2048": recipe = RvrmRecipe.CueMode1_2048; return true;
            case "cue-mode2-2352-audio": recipe = RvrmRecipe.CueMode2_2352Audio; return true;
            case "cue-mode2-2352": recipe = RvrmRecipe.CueMode2_2352; return true;
            case "cue-mode1-2352-audio": recipe = RvrmRecipe.CueMode1_2352Audio; return true;
            case "cue-mode1-2352": recipe = RvrmRecipe.CueMode1_2352; return true;
            case "cue-audio": recipe = RvrmRecipe.CueAudio; return true;
            case "cue-unknown": recipe = RvrmRecipe.CueUnknown; return true;
            case "cue-dat": recipe = RvrmRecipe.CueDat; return true;
            case "gdrom-internal": recipe = RvrmRecipe.GdRomInternal; return true;
            case "iso-from-mode1-2048": recipe = RvrmRecipe.IsoFromMode1_2048; return true;
            case "iso-from-mode1-2352": recipe = RvrmRecipe.IsoFromMode1_2352; return true;
            case "cue-unicode": recipe = RvrmRecipe.CueUnicode; return true;
            case "cue-exact": recipe = RvrmRecipe.CueExact; return true;
            case "unknown": recipe = RvrmRecipe.Unknown; return true;
            default: recipe = default; return false;
        }
    }

    internal static string RecipeName(RvrmRecipe recipe)
    {
        switch (recipe)
        {
            case RvrmRecipe.RawExact: return "raw-exact";
            case RvrmRecipe.HardDiskExact: return "hard-disk-exact";
            case RvrmRecipe.CanonicalAviExact: return "canonical-avi-exact";
            case RvrmRecipe.TosecGdi: return "tosec-gdi";
            case RvrmRecipe.TocExact: return "toc-exact";
            case RvrmRecipe.PspIso: return "psp-iso";
            case RvrmRecipe.Iso: return "iso";
            case RvrmRecipe.RedumpGdRomCue: return "redump-gdrom-cue";
            case RvrmRecipe.CueComplexLayout: return "cue-complex-layout";
            case RvrmRecipe.CueMixed: return "cue-mixed";
            case RvrmRecipe.CueMode1_2048Audio: return "cue-mode1-2048-audio";
            case RvrmRecipe.CueMode1_2048: return "cue-mode1-2048";
            case RvrmRecipe.CueMode2_2352Audio: return "cue-mode2-2352-audio";
            case RvrmRecipe.CueMode2_2352: return "cue-mode2-2352";
            case RvrmRecipe.CueMode1_2352Audio: return "cue-mode1-2352-audio";
            case RvrmRecipe.CueMode1_2352: return "cue-mode1-2352";
            case RvrmRecipe.CueAudio: return "cue-audio";
            case RvrmRecipe.CueUnknown: return "cue-unknown";
            case RvrmRecipe.CueDat: return "cue-dat";
            case RvrmRecipe.GdRomInternal: return "gdrom-internal";
            case RvrmRecipe.IsoFromMode1_2048: return "iso-from-mode1-2048";
            case RvrmRecipe.IsoFromMode1_2352: return "iso-from-mode1-2352";
            case RvrmRecipe.CueUnicode: return "cue-unicode";
            case RvrmRecipe.CueExact: return "cue-exact";
            case RvrmRecipe.Unknown: return "unknown";
            default: throw new InvalidDataException("Unknown reconstruction recipe id: " + (ushort)recipe + ".");
        }
    }

    private static RvrmRecord CreateRecord(ChdReconstructionManifest draft)
    {
        if (draft == null)
            throw new InvalidDataException("Reconstruction metadata is unavailable.");
        RvrmRecord record = new RvrmRecord
        {
            Family = ParseFamily(draft.Family),
            PrimaryRecipe = ParseRecipe(draft.Dialect)
        };
        CopyPayloads(draft.Tracks, record.Payloads);

        List<ChdManifestView> views = draft.Views ?? new List<ChdManifestView>();
        for (int i = 0; i < views.Count; i++)
        {
            ChdManifestView source = views[i] ?? throw new InvalidDataException("Null reconstruction view.");
            RvrmViewRecord view = new RvrmViewRecord { Recipe = ParseRecipe(source.Dialect) };
            CopyPayloads(source.Tracks, view.Payloads);
            record.Views.Add(view);
        }
        record.Views.Sort((left, right) => ((ushort)left.Recipe).CompareTo((ushort)right.Recipe));

        List<ChdManifestAuxiliary> auxiliaries = draft.Auxiliaries ?? new List<ChdManifestAuxiliary>();
        for (int i = 0; i < auxiliaries.Count; i++)
        {
            ChdManifestAuxiliary source = auxiliaries[i] ?? throw new InvalidDataException("Null reconstruction auxiliary.");
            record.Auxiliaries.Add(new RvrmAuxiliaryRecord
            {
                Kind = ParseAuxiliaryKind(source.Role),
                Bytes = Clone(source.Bytes)
            });
        }
        record.Auxiliaries.Sort(CompareAuxiliaries);
        List<RvrmOpaqueSection> opaqueSections = draft.OpaqueSections ?? new List<RvrmOpaqueSection>();
        for (int i = 0; i < opaqueSections.Count; i++)
        {
            RvrmOpaqueSection source = opaqueSections[i] ?? throw new InvalidDataException("Null opaque reconstruction section.");
            record.OpaqueSections.Add(new RvrmOpaqueSection
            {
                Id = source.Id,
                Flags = source.Flags,
                Payload = Clone(source.Payload)
            });
        }
        record.OpaqueSections.Sort((left, right) => left.Id.CompareTo(right.Id));
        ValidateRecord(record);
        return record;
    }

    private static void CopyPayloads(List<ChdManifestTrack> source, List<RvrmPayloadIdentity> destination)
    {
        source = source ?? new List<ChdManifestTrack>();
        for (int i = 0; i < source.Count; i++)
        {
            ChdManifestTrack track = source[i] ?? throw new InvalidDataException("Null reconstruction payload.");
            destination.Add(new RvrmPayloadIdentity
            {
                Number = track.Number,
                Size = track.Size,
                Crc32 = Clone(track.Crc32),
                Md5 = Clone(track.Md5),
                Sha1 = Clone(track.Sha1),
                Sha256 = Clone(track.Sha256)
            });
        }
    }

    private static byte[] EncodeCore(RvrmRecord record)
    {
        return Encode(writer =>
        {
            writer.Write((byte)record.Family);
            writer.Write((ushort)record.PrimaryRecipe);
            WritePayloads(writer, record.Payloads);
        });
    }

    private static byte[] EncodeViews(List<RvrmViewRecord> views)
    {
        return Encode(writer =>
        {
            writer.Write(views.Count);
            for (int i = 0; i < views.Count; i++)
            {
                writer.Write((ushort)views[i].Recipe);
                WritePayloads(writer, views[i].Payloads);
            }
        });
    }

    private static byte[] EncodeAuxiliaries(List<RvrmAuxiliaryRecord> auxiliaries)
    {
        return Encode(writer =>
        {
            writer.Write(auxiliaries.Count);
            for (int i = 0; i < auxiliaries.Count; i++)
            {
                byte[] bytes = auxiliaries[i].Bytes ?? Array.Empty<byte>();
                writer.Write((ushort)auxiliaries[i].Kind);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        });
    }

    private static byte[] Encode(Action<BinaryWriter> action)
    {
        using (MemoryStream memory = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memory, new UTF8Encoding(false), true))
        {
            action(writer);
            writer.Flush();
            return memory.ToArray();
        }
    }

    private static void WritePayloads(BinaryWriter writer, List<RvrmPayloadIdentity> payloads)
    {
        writer.Write(payloads.Count);
        for (int i = 0; i < payloads.Count; i++)
        {
            RvrmPayloadIdentity payload = payloads[i];
            writer.Write(payload.Number);
            writer.Write(payload.Size);
            writer.Write(payload.Crc32);
            writer.Write(payload.Md5);
            writer.Write(payload.Sha1);
            writer.Write(payload.Sha256);
        }
    }

    private static void DecodeCore(byte[] section, RvrmRecord record)
    {
        ReadSection(section, reader =>
        {
            record.Family = (RvrmMediaFamily)reader.ReadByte();
            record.PrimaryRecipe = (RvrmRecipe)reader.ReadUInt16();
            ReadPayloads(reader, record.Payloads);
        });
    }

    private static void DecodeViews(byte[] section, List<RvrmViewRecord> views)
    {
        ReadSection(section, reader =>
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxViews)
                throw new InvalidDataException("Invalid reconstruction view count.");
            for (int i = 0; i < count; i++)
            {
                RvrmViewRecord view = new RvrmViewRecord { Recipe = (RvrmRecipe)reader.ReadUInt16() };
                ReadPayloads(reader, view.Payloads);
                views.Add(view);
            }
        });
    }

    private static void DecodeAuxiliaries(byte[] section, List<RvrmAuxiliaryRecord> auxiliaries)
    {
        ReadSection(section, reader =>
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxAuxiliaries)
                throw new InvalidDataException("Invalid reconstruction auxiliary count.");
            for (int i = 0; i < count; i++)
            {
                RvrmAuxiliaryKind kind = (RvrmAuxiliaryKind)reader.ReadUInt16();
                int length = reader.ReadInt32();
                if (!IsSupportedAuxiliaryLength(length))
                    throw new InvalidDataException("Invalid reconstruction auxiliary length.");
                auxiliaries.Add(new RvrmAuxiliaryRecord { Kind = kind, Bytes = ReadFixedBytes(reader, length) });
            }
        });
    }

    private static void ReadPayloads(BinaryReader reader, List<RvrmPayloadIdentity> payloads)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaxTracks)
            throw new InvalidDataException("Invalid reconstruction payload count.");
        for (int i = 0; i < count; i++)
        {
            payloads.Add(new RvrmPayloadIdentity
            {
                Number = reader.ReadInt32(),
                Size = reader.ReadInt64(),
                Crc32 = ReadFixedBytes(reader, 4),
                Md5 = ReadFixedBytes(reader, 16),
                Sha1 = ReadFixedBytes(reader, 20),
                Sha256 = ReadFixedBytes(reader, 32)
            });
        }
    }

    private static void ReadSection(byte[] section, Action<BinaryReader> action)
    {
        using (MemoryStream memory = new MemoryStream(section, false))
        using (BinaryReader reader = new BinaryReader(memory, Encoding.UTF8, true))
        {
            action(reader);
            if (memory.Position != memory.Length)
                throw new InvalidDataException("Unexpected bytes at the end of a reconstruction metadata section.");
        }
    }

    private static ChdReconstructionManifest CreateRuntimeManifest(RvrmRecord record)
    {
        ChdReconstructionManifest manifest = new ChdReconstructionManifest
        {
            Schema = ChdReconstructionManifest.CurrentSchema,
            Family = FamilyName(record.Family),
            Dialect = RecipeName(record.PrimaryRecipe)
        };
        CopyRuntimePayloads(record.Payloads, manifest.Tracks, "payload");
        for (int i = 0; i < record.Views.Count; i++)
        {
            RvrmViewRecord source = record.Views[i];
            ChdManifestView view = new ChdManifestView
            {
                Name = IsIsoView(source.Recipe) ? "iso" : "view-" + (i + 1).ToString("D4"),
                Dialect = RecipeName(source.Recipe)
            };
            CopyRuntimePayloads(source.Payloads, view.Tracks, "view-payload");
            manifest.Views.Add(view);
        }
        for (int i = 0; i < record.Auxiliaries.Count; i++)
        {
            byte[] bytes = Clone(record.Auxiliaries[i].Bytes);
            manifest.Auxiliaries.Add(new ChdManifestAuxiliary
            {
                Name = "auxiliary-" + (i + 1).ToString("D4"),
                Role = AuxiliaryRole(record.Auxiliaries[i].Kind),
                Bytes = bytes,
                Sha256 = Hash(bytes)
            });
        }
        for (int i = 0; i < record.OpaqueSections.Count; i++)
        {
            RvrmOpaqueSection source = record.OpaqueSections[i];
            manifest.OpaqueSections.Add(new RvrmOpaqueSection
            {
                Id = source.Id,
                Flags = source.Flags,
                Payload = Clone(source.Payload)
            });
        }
        return manifest;
    }

    private static void CopyRuntimePayloads(List<RvrmPayloadIdentity> source, List<ChdManifestTrack> destination, string prefix)
    {
        for (int i = 0; i < source.Count; i++)
        {
            RvrmPayloadIdentity payload = source[i];
            destination.Add(new ChdManifestTrack
            {
                Number = payload.Number,
                Name = prefix + "-" + (i + 1).ToString("D4"),
                Size = payload.Size,
                Crc32 = Clone(payload.Crc32),
                Md5 = Clone(payload.Md5),
                Sha1 = Clone(payload.Sha1),
                Sha256 = Clone(payload.Sha256)
            });
        }
    }

    private static void ValidateRecord(RvrmRecord record)
    {
        FamilyName(record.Family);
        RecipeName(record.PrimaryRecipe);
        if (!RecipeMatchesFamily(record.Family, record.PrimaryRecipe))
            throw new InvalidDataException("Reconstruction recipe does not match its media family.");
        ValidatePayloads(record.Payloads, "primary view");
        if (record.Views.Count > 1)
            throw new InvalidDataException("Schema 1 supports one proven alternate reconstruction view.");
        HashSet<RvrmRecipe> recipes = new HashSet<RvrmRecipe>();
        for (int i = 0; i < record.Views.Count; i++)
        {
            RvrmViewRecord view = record.Views[i] ?? throw new InvalidDataException("Null reconstruction view.");
            RecipeName(view.Recipe);
            if (!IsIsoView(view.Recipe))
                throw new InvalidDataException("Unsupported alternate reconstruction view recipe: " + RecipeName(view.Recipe) + ".");
            if (!recipes.Add(view.Recipe))
                throw new InvalidDataException("Reconstruction view recipes must be unique.");
            ValidatePayloads(view.Payloads, "alternate view");
        }
        if (record.Auxiliaries.Count > MaxAuxiliaries)
            throw new InvalidDataException("Too many reconstruction auxiliaries.");
        for (int i = 0; i < record.Auxiliaries.Count; i++)
        {
            RvrmAuxiliaryRecord auxiliary = record.Auxiliaries[i] ?? throw new InvalidDataException("Null reconstruction auxiliary.");
            AuxiliaryRole(auxiliary.Kind);
            if (auxiliary.Bytes == null || !IsSupportedAuxiliaryLength(auxiliary.Bytes.Length))
                throw new InvalidDataException("Invalid reconstruction auxiliary content.");
            if (i > 0 && CompareAuxiliaries(record.Auxiliaries[i - 1], auxiliary) > 0)
                throw new InvalidDataException("Reconstruction auxiliaries are not in canonical order.");
        }
        if (1 + (record.Views.Count > 0 ? 1 : 0) + (record.Auxiliaries.Count > 0 ? 1 : 0) + record.OpaqueSections.Count > MaxSections)
            throw new InvalidDataException("Too many reconstruction metadata sections.");
        HashSet<ushort> opaqueIds = new HashSet<ushort>();
        for (int i = 0; i < record.OpaqueSections.Count; i++)
        {
            RvrmOpaqueSection opaque = record.OpaqueSections[i] ?? throw new InvalidDataException("Null opaque reconstruction section.");
            if (opaque.Id <= SectionAuxiliaries ||
                !opaqueIds.Add(opaque.Id) || opaque.Flags != 0 || opaque.Payload == null)
                throw new InvalidDataException("Invalid opaque optional reconstruction section.");
        }
    }

    private static void ValidatePayloads(List<RvrmPayloadIdentity> payloads, string label)
    {
        if (payloads == null || payloads.Count == 0 || payloads.Count > MaxTracks)
            throw new InvalidDataException("Invalid payload count in " + label + ".");
        int previousNumber = 0;
        for (int i = 0; i < payloads.Count; i++)
        {
            RvrmPayloadIdentity payload = payloads[i] ?? throw new InvalidDataException("Null payload in " + label + ".");
            if (payload.Number <= previousNumber || payload.Size < 0 ||
                payload.Crc32 == null || payload.Crc32.Length != 4 ||
                payload.Md5 == null || payload.Md5.Length != 16 ||
                payload.Sha1 == null || payload.Sha1.Length != 20 ||
                payload.Sha256 == null || payload.Sha256.Length != 32)
                throw new InvalidDataException("Incomplete canonical payload identity in " + label + ".");
            previousNumber = payload.Number;
        }
    }

    private static RvrmMediaFamily ParseFamily(string family)
    {
        switch ((family ?? "").Trim().ToLowerInvariant())
        {
            case "cd": return RvrmMediaFamily.Cd;
            case "gdi": return RvrmMediaFamily.GdRom;
            case "dvd": return RvrmMediaFamily.Dvd;
            case "psp": return RvrmMediaFamily.Psp;
            case "raw": return RvrmMediaFamily.Raw;
            case "hdd": return RvrmMediaFamily.HardDisk;
            case "laserdisc": return RvrmMediaFamily.LaserDisc;
            default: throw new InvalidDataException("Unsupported reconstruction media family: " + family + ".");
        }
    }

    private static string FamilyName(RvrmMediaFamily family)
    {
        switch (family)
        {
            case RvrmMediaFamily.Cd: return "cd";
            case RvrmMediaFamily.GdRom: return "gdi";
            case RvrmMediaFamily.Dvd: return "dvd";
            case RvrmMediaFamily.Psp: return "psp";
            case RvrmMediaFamily.Raw: return "raw";
            case RvrmMediaFamily.HardDisk: return "hdd";
            case RvrmMediaFamily.LaserDisc: return "laserdisc";
            default: throw new InvalidDataException("Unknown reconstruction media family id: " + (byte)family + ".");
        }
    }

    private static RvrmRecipe ParseRecipe(string recipe)
    {
        if (TryMapRecipe(recipe, out RvrmRecipe value))
            return value;
        throw new InvalidDataException("Unsupported reconstruction recipe: " + recipe + ".");
    }

    private static RvrmAuxiliaryKind ParseAuxiliaryKind(string role)
    {
        if (string.Equals(role, "sbi-subchannel-correction", StringComparison.OrdinalIgnoreCase))
            return RvrmAuxiliaryKind.SbiSubchannelCorrection;
        throw new InvalidDataException("Unsupported reconstruction auxiliary role: " + role + ".");
    }

    private static string AuxiliaryRole(RvrmAuxiliaryKind kind)
    {
        if (kind == RvrmAuxiliaryKind.SbiSubchannelCorrection)
            return "sbi-subchannel-correction";
        throw new InvalidDataException("Unknown reconstruction auxiliary kind id: " + (ushort)kind + ".");
    }

    private static bool IsIsoView(RvrmRecipe recipe)
    {
        return recipe == RvrmRecipe.IsoFromMode1_2048 || recipe == RvrmRecipe.IsoFromMode1_2352;
    }

    private static bool RecipeMatchesFamily(RvrmMediaFamily family, RvrmRecipe recipe)
    {
        if (recipe == RvrmRecipe.Unknown)
            return true;
        switch (family)
        {
            case RvrmMediaFamily.Raw:
                return recipe == RvrmRecipe.RawExact;
            case RvrmMediaFamily.HardDisk:
                return recipe == RvrmRecipe.HardDiskExact;
            case RvrmMediaFamily.LaserDisc:
                return recipe == RvrmRecipe.CanonicalAviExact;
            case RvrmMediaFamily.Dvd:
                return recipe == RvrmRecipe.Iso;
            case RvrmMediaFamily.Psp:
                return recipe == RvrmRecipe.PspIso || recipe == RvrmRecipe.Iso;
            case RvrmMediaFamily.GdRom:
                return recipe == RvrmRecipe.TosecGdi || recipe == RvrmRecipe.RedumpGdRomCue || recipe == RvrmRecipe.GdRomInternal;
            case RvrmMediaFamily.Cd:
                return recipe == RvrmRecipe.TocExact || recipe == RvrmRecipe.Iso ||
                       recipe == RvrmRecipe.CueComplexLayout || recipe == RvrmRecipe.CueMixed ||
                       recipe == RvrmRecipe.CueMode1_2048Audio || recipe == RvrmRecipe.CueMode1_2048 ||
                       recipe == RvrmRecipe.CueMode2_2352Audio || recipe == RvrmRecipe.CueMode2_2352 ||
                       recipe == RvrmRecipe.CueMode1_2352Audio || recipe == RvrmRecipe.CueMode1_2352 ||
                       recipe == RvrmRecipe.CueAudio || recipe == RvrmRecipe.CueUnknown ||
                       recipe == RvrmRecipe.CueDat || recipe == RvrmRecipe.CueUnicode || recipe == RvrmRecipe.CueExact;
            default:
                return false;
        }
    }

    private static int CompareAuxiliaries(RvrmAuxiliaryRecord left, RvrmAuxiliaryRecord right)
    {
        int kind = ((ushort)left.Kind).CompareTo((ushort)right.Kind);
        if (kind != 0) return kind;
        byte[] leftBytes = left.Bytes ?? Array.Empty<byte>();
        byte[] rightBytes = right.Bytes ?? Array.Empty<byte>();
        int length = Math.Min(leftBytes.Length, rightBytes.Length);
        for (int i = 0; i < length; i++)
        {
            int comparison = leftBytes[i].CompareTo(rightBytes[i]);
            if (comparison != 0) return comparison;
        }
        return leftBytes.Length.CompareTo(rightBytes.Length);
    }

    private static byte[] AddIntegrityTrailer(byte[] payload)
    {
        byte[] digest = Hash(payload);
        byte[] result = new byte[payload.Length + digest.Length];
        Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
        Buffer.BlockCopy(digest, 0, result, payload.Length, digest.Length);
        return result;
    }

    private static byte[] Hash(byte[] bytes)
    {
        using (SHA256 algorithm = SHA256.Create())
            return algorithm.ComputeHash(bytes ?? Array.Empty<byte>());
    }

    private static byte[] ReadFixedBytes(BinaryReader reader, int length)
    {
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
            throw new EndOfStreamException("Reconstruction metadata is truncated.");
        return bytes;
    }

    private static byte[] Clone(byte[] value)
    {
        if (value == null || value.Length == 0)
            return Array.Empty<byte>();
        byte[] clone = new byte[value.Length];
        Buffer.BlockCopy(value, 0, clone, 0, value.Length);
        return clone;
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        int difference = 0;
        for (int i = 0; i < left.Length; i++)
            difference |= left[i] ^ right[i];
        return difference == 0;
    }

    private sealed class EncodedSection
    {
        public EncodedSection(ushort id, ushort flags, byte[] payload)
        {
            Id = id;
            Flags = flags;
            Payload = payload;
        }

        public ushort Id { get; }
        public ushort Flags { get; }
        public byte[] Payload { get; }
    }
}
