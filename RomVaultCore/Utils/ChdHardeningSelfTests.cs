using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CHDSharpLib;
using FileScanner;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;

namespace RomVaultCore.Utils;

internal static class ChdHardeningSelfTests
{
    public static bool Run(out string error)
    {
        error = "";
        try
        {
            Random random = new Random(0x524d4348);
            for (int i = 0; i < 512; i++)
            {
                byte[] input = new byte[random.Next(0, 8192)];
                random.NextBytes(input);
                ChdReconstructionManifest.TryDeserialize(input, out _, out _);
                using (MemoryStream stream = new MemoryStream(input, false))
                    CHD.CheckHeader(stream, out _, out _);
            }

            AssertInvalidHeader(0);
            AssertInvalidHeader(6);
            AssertInvalidHeader(uint.MaxValue);
            using (MemoryStream shortHeader = new MemoryStream(Encoding.ASCII.GetBytes("MComprHD"), false))
            {
                if (CHD.CheckHeader(shortHeader, out _, out _))
                    throw new InvalidDataException("A truncated CHD header was accepted.");
            }

            ChdReconstructionManifest unsafeManifest = MinimalManifest();
            unsafeManifest.Tracks[0].Name = "..\\outside.bin";
            if (!ChdReconstructionManifest.TryDeserialize(unsafeManifest.Serialize(), out ChdReconstructionManifest sanitized, out _) ||
                sanitized.Tracks[0].Name.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("An external track path entered serialized reconstruction metadata.");

            ChdReconstructionManifest duplicateManifest = MinimalManifest();
            duplicateManifest.Tracks.Add(new ChdManifestTrack { Number = 1, Name = "different.bin", Size = 1, Sha256 = new byte[32] });
            AssertSerializeRejected(duplicateManifest, "duplicate track number");

            ChdReconstructionManifest badHashManifest = MinimalManifest();
            badHashManifest.Tracks[0].Sha1 = new byte[19];
            AssertSerializeRejected(badHashManifest, "invalid hash length");

            ChdReconstructionManifest missingDescriptor = MinimalManifest();
            missingDescriptor.DescriptorName = "";
            missingDescriptor.DescriptorBytes = Array.Empty<byte>();
            missingDescriptor.DescriptorSha256 = Array.Empty<byte>();
            if (!ChdReconstructionManifest.HasCompleteOpticalIdentity(missingDescriptor, out _))
                throw new InvalidDataException("Content-complete optical metadata incorrectly required an embedded descriptor filename.");

            ChdReconstructionManifest incompleteGraph = MinimalManifest();
            incompleteGraph.Tracks.Add(new ChdManifestTrack
            {
                Number = 2,
                Name = "track02.bin",
                Size = 1,
                Sha256 = Array.Empty<byte>()
            });
            if (ChdReconstructionManifest.HasCompleteOpticalIdentity(incompleteGraph, out _))
                throw new InvalidDataException("An optical payload without SHA-256 was reported as complete.");

            RunFilenameIndependenceTests();
            RunInputRoundTripPolicyTests();
            RunNativeTrackLayoutGenerationTests();
            RunRoundTripDescriptorTests();
            RunParentResolverTests();

            if (!ChdParentGraph.RunPolicySelfTest(out string parentPolicyError))
                throw new InvalidDataException("CHD parent graph policy failed: " + parentPolicyError);

            if (!ChdTemporaryWorkspace.RunSelfTest(out string workspaceError))
                throw new InvalidDataException("CHD workspace routing failed: " + workspaceError);
            if (!ChdBoundPayloadValidator.RunSelfTest(out string boundPayloadError))
                throw new InvalidDataException("RVRM-bound payload validation failed: " + boundPayloadError);
            if (!ChdArtifactPaths.RunSelfTest(out string artifactPathError))
                throw new InvalidDataException("CHD transaction path allocation failed: " + artifactPathError);
            if (!ChdEncodingProfile.RunSelfTest(out string profileError))
                throw new InvalidDataException("RVEP encoder profile metadata failed: " + profileError);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ChdReconstructionManifest MinimalManifest()
    {
        byte[] descriptor = Encoding.ASCII.GetBytes(
            "FILE \"track01.bin\" BINARY\r\n" +
            "  TRACK 01 MODE1/2352\r\n" +
            "    INDEX 01 00:00:00\r\n");
        return new ChdReconstructionManifest
        {
            Family = "cd",
            Storage = "archive",
            Dialect = "cue-exact",
            DescriptorName = "disc.cue",
            DescriptorBytes = descriptor,
            DescriptorSha256 = Hash(descriptor, SHA256.Create()),
            Tracks = new List<ChdManifestTrack>
            {
                new ChdManifestTrack
                {
                    Number = 1,
                    Name = "track01.bin",
                    Size = 1,
                    Crc32 = new byte[4],
                    Md5 = new byte[16],
                    Sha1 = new byte[20],
                    Sha256 = new byte[32]
                }
            }
        };
    }

    private static void RunFilenameIndependenceTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-name-independent-" + Guid.NewGuid().ToString("N"));
        try
        {
            string first = Path.Combine(root, "first");
            string second = Path.Combine(root, "second");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            byte[] payload = new byte[2352 * 2];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i * 17);
            File.WriteAllBytes(Path.Combine(first, "Original Track Name.bin"), payload);
            File.WriteAllBytes(Path.Combine(second, "Rënamed Track-日本.bin"), payload);
            string firstCue = Path.Combine(first, "Original Disc.cue");
            string secondCue = Path.Combine(second, "Rënamed Disc-日本.cue");
            File.WriteAllText(firstCue, "FILE \"Original Track Name.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n", new UTF8Encoding(false));
            File.WriteAllText(secondCue, "FILE \"Rënamed Track-日本.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n", new UTF8Encoding(false));

            ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("cd", ChdStorageProfile.Archive);
            ChdmanIdentity identity = new ChdmanIdentity
            {
                VersionText = "test",
                Banner = "test",
                BinarySha256 = new string('a', 64),
                Capabilities = new ChdmanCapabilities()
            };
            bool firstCreated = ChdReconstructionManifest.TryCreateFromSource(firstCue, profile, identity, out ChdReconstructionManifest firstManifest, out string firstError);
            bool secondCreated = ChdReconstructionManifest.TryCreateFromSource(secondCue, profile, identity, out ChdReconstructionManifest secondManifest, out string secondError);
            if (!firstCreated || !secondCreated)
                throw new InvalidDataException("Rename-invariance fixture could not be created: " + firstError + secondError);
            if (!BytesEqual(firstManifest.Serialize(), secondManifest.Serialize()))
                throw new InvalidDataException("Renaming a CUE track changed serialized RVRM metadata.");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void RunInputRoundTripPolicyTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-input-policy-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, "track01.bin"), new byte[2352 * 200]);
            File.WriteAllBytes(Path.Combine(root, "track02.bin"), new byte[2352 * 200]);
            File.WriteAllBytes(Path.Combine(root, "track03.bin"), new byte[2352 * 200]);
            File.WriteAllBytes(Path.Combine(root, "track02.raw"), new byte[2352 * 200]);
            File.WriteAllBytes(Path.Combine(root, "shared.bin"), new byte[2352 * 200]);
            string cue = Path.Combine(root, "disc.cue");
            File.WriteAllText(cue,
                "REM SINGLE-DENSITY AREA\r\n" +
                "FILE \"track01.bin\" BINARY\r\n" +
                "  TRACK 01 MODE1/2352\r\n" +
                "    INDEX 01 00:00:00\r\n" +
                "FILE \"track02.bin\" BINARY\r\n" +
                "  TRACK 02 AUDIO\r\n" +
                "    INDEX 00 00:00:00\r\n" +
                "    INDEX 01 00:02:00\r\n" +
                "REM HIGH-DENSITY AREA\r\n" +
                "FILE \"track03.bin\" BINARY\r\n" +
                "  TRACK 03 MODE1/2352\r\n" +
                "    INDEX 01 00:00:00\r\n",
                new UTF8Encoding(false));
            if (!ChdRoundTripInputPolicy.Validate(cue, "gdi", out string cueError))
                throw new InvalidDataException("A filename-independent split CUE was rejected: " + cueError);

            File.WriteAllText(cue,
                "FILE \"track01.bin\" BINARY\r\n" +
                "  TRACK 01 AUDIO\r\n" +
                "    FLAGS PRE DCP\r\n" +
                "    INDEX 01 00:00:00\r\n",
                new UTF8Encoding(false));
            if (ChdRoundTripInputPolicy.Validate(cue, "cd", out _))
                throw new InvalidDataException("A CUE FLAGS layout that v1 cannot reconstruct was accepted.");

            File.WriteAllText(cue,
                "FILE \"shared.bin\" BINARY\r\n" +
                "  TRACK 01 MODE1/2352\r\n" +
                "    INDEX 01 00:00:00\r\n" +
                "  TRACK 02 AUDIO\r\n" +
                "    INDEX 01 01:00:00\r\n",
                new UTF8Encoding(false));
            if (ChdRoundTripInputPolicy.Validate(cue, "cd", out _))
                throw new InvalidDataException("A shared-BIN CUE without a payload assembly recipe was accepted.");

            File.WriteAllText(cue,
                "CATALOG 1234567890123\r\n" +
                "FILE \"track01.bin\" BINARY\r\n" +
                "  TRACK 01 AUDIO\r\n" +
                "    INDEX 01 00:00:00\r\n",
                new UTF8Encoding(false));
            if (ChdRoundTripInputPolicy.Validate(cue, "cd", out _))
                throw new InvalidDataException("Unrecoverable CUE catalog metadata was accepted.");

            File.WriteAllText(cue,
                "FILE \"track01.bin\" BINARY\r\n" +
                "  TRACK 01 AUDIO\r\n" +
                "    PREGAP 00:02:00\r\n" +
                "    INDEX 00 00:00:00\r\n" +
                "    INDEX 01 00:02:00\r\n",
                new UTF8Encoding(false));
            if (ChdRoundTripInputPolicy.Validate(cue, "cd", out _))
                throw new InvalidDataException("A CUE combining synthetic and stored pregap data was accepted.");

            string gdi = Path.Combine(root, "disc.gdi");
            File.WriteAllText(gdi,
                "3\r\n" +
                "1 0 4 2352 track01.bin 0\r\n" +
                "2 450 0 2352 track02.raw 0\r\n" +
                "3 45000 4 2352 track03.bin 0\r\n",
                new UTF8Encoding(false));
            if (!ChdRoundTripInputPolicy.Validate(gdi, "gdi", out string gdiError))
                throw new InvalidDataException("A canonical split GDI was rejected: " + gdiError);

            File.WriteAllText(gdi, "1\r\n1 0 4 2352 track01.bin 16\r\n", new UTF8Encoding(false));
            if (ChdRoundTripInputPolicy.Validate(gdi, "gdi", out _))
                throw new InvalidDataException("A GDI with an unreconstructable file offset was accepted.");

            File.WriteAllText(gdi, "1\r\n1 0 7 2352 track01.bin 0\r\n", new UTF8Encoding(false));
            if (ChdRoundTripInputPolicy.Validate(gdi, "gdi", out _))
                throw new InvalidDataException("A GDI with an unreconstructable mode was accepted.");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void RunNativeTrackLayoutGenerationTests()
    {
        List<ChdCdTrackInfo> cueTracks = new List<ChdCdTrackInfo>
        {
            new ChdCdTrackInfo
            {
                TrackNo = 1,
                TrackType = "MODE2_FORM2",
                TrackSubtype = "NONE",
                Frames = 300,
                PreGapFrames = 150,
                PreGapTrackType = "MODE2_FORM2",
                PreGapSubtype = "NONE",
                PreGapDataStored = true,
                PostGapFrames = 75,
                SectorSize = 2324
            }
        };
        string cue = ChdDescriptorGenerator.BuildCue(cueTracks, null);
        if (!cue.Contains("TRACK 01 MODE2/2324", StringComparison.Ordinal) ||
            !cue.Contains("INDEX 00 00:00:00", StringComparison.Ordinal) ||
            !cue.Contains("INDEX 01 00:02:00", StringComparison.Ordinal) ||
            !cue.Contains("POSTGAP 00:01:00", StringComparison.Ordinal))
            throw new InvalidDataException("Native CHT2 pregap/type metadata was not preserved in generated CUE text.");

        List<ChdCdTrackInfo> gdiTracks = new List<ChdCdTrackInfo>
        {
            new ChdCdTrackInfo { TrackNo = 1, TrackType = "MODE1_RAW", TrackSubtype = "NONE", StartFrame = 0, Frames = 450, SectorSize = 2352 },
            new ChdCdTrackInfo { TrackNo = 2, TrackType = "AUDIO", TrackSubtype = "NONE", StartFrame = 450, Frames = 44550, PadFrames = 44100, SectorSize = 2352 },
            new ChdCdTrackInfo { TrackNo = 3, TrackType = "MODE1_RAW", TrackSubtype = "NONE", StartFrame = 45000, Frames = 450, SectorSize = 2352 }
        };
        string gdi = ChdDescriptorGenerator.BuildGdi(gdiTracks, null);
        if (!gdi.Contains("2 450 0 2352", StringComparison.Ordinal) ||
            !gdi.Contains("3 45000 4 2352", StringComparison.Ordinal))
            throw new InvalidDataException("Native CHGD padding/LBA metadata was not preserved in generated GDI text.");

        string gdCue = ChdDescriptorGenerator.BuildCue(gdiTracks, null, true);
        int singleDensity = gdCue.IndexOf("REM SINGLE-DENSITY AREA", StringComparison.Ordinal);
        int trackTwo = gdCue.IndexOf("TRACK 02 AUDIO", StringComparison.Ordinal);
        int highDensity = gdCue.IndexOf("REM HIGH-DENSITY AREA", StringComparison.Ordinal);
        int trackThree = gdCue.IndexOf("TRACK 03 MODE1/2352", StringComparison.Ordinal);
        if (singleDensity != 0 || trackTwo < 0 || highDensity <= trackTwo || trackThree <= highDensity)
            throw new InvalidDataException("Native CHGD identity was not preserved as canonical Redump density markers.");

        cueTracks[0].TrackSubtype = "RW_RAW";
        try
        {
            ChdDescriptorGenerator.BuildCue(cueTracks, null);
            throw new InvalidDataException("A CUE generator silently discarded RW_RAW subcode metadata.");
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("subcode", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void RunRoundTripDescriptorTests()
    {
        byte[] descriptorBytes = Encoding.ASCII.GetBytes(
            "FILE \"track01.bin\" BINARY\r\n" +
            "  TRACK 01 MODE1/2352\r\n" +
            "    INDEX 01 00:00:00\r\n");
        byte[] descriptorSha1 = Hash(descriptorBytes, SHA1.Create());
        byte[] trackSha1 = new byte[20];

        RvFile destination = new RvFile(FileType.CHD) { Name = "disc.cue.chd" };
        destination.ChildAdd(new RvFile(FileType.FileCHD)
        {
            Name = "disc.cue",
            Size = (ulong)descriptorBytes.Length,
            SHA1 = descriptorSha1
        });
        destination.ChildAdd(new RvFile(FileType.FileCHD)
        {
            Name = "track01.bin",
            Size = 2352,
            SHA1 = trackSha1
        });

        ScannedFile extracted = new ScannedFile(FileType.CHD);
        extracted.Add(new ScannedFile(FileType.FileCHD)
        {
            Name = "track01.bin",
            Size = 2352,
            SHA1 = CloneBytes(trackSha1),
            ChdHashMatchMode = "Exact"
        });
        if (FixFileUtils.ValidateRoundTripPayload(destination, extracted, out _))
            throw new InvalidDataException("CHD round-trip validation accepted output that omitted the DAT descriptor.");

        extracted.Add(new ScannedFile(FileType.FileCHD)
        {
            Name = "disc.cue",
            Size = (ulong)descriptorBytes.Length,
            SHA1 = CloneBytes(descriptorSha1),
            ChdHashMatchMode = "Exact"
        });
        if (!FixFileUtils.ValidateRoundTripPayload(destination, extracted, out string roundTripError))
            throw new InvalidDataException("CHD round-trip validation rejected an exact descriptor and payload: " + roundTripError);

        ScannedFile regenerated = extracted[1];
        regenerated.Size += 7;
        regenerated.SHA1 = Hash(Encoding.ASCII.GetBytes("canonical descriptor with current names"), SHA1.Create());
        regenerated.ChdHashMatchMode = "Semantic";
        regenerated.ChdDescriptorMatch = "Semantic";
        if (!FixFileUtils.ValidateRoundTripPayload(destination, extracted, out string semanticError))
            throw new InvalidDataException("CHD round-trip validation rejected a semantic CUE view: " + semanticError);
    }

    private static void RunParentResolverTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-parent-resolver-test-" + Guid.NewGuid().ToString("N"));
        List<string> savedSearchPaths = Settings.rvSettings?.ChdParentSearchPaths;
        try
        {
            if (Settings.rvSettings != null)
                Settings.rvSettings.ChdParentSearchPaths = new List<string>();

            string childDirectory = Path.Combine(root, "children");
            string remoteDirectory = Path.Combine(root, "remote");
            Directory.CreateDirectory(childDirectory);
            Directory.CreateDirectory(remoteDirectory);

            byte[] parentRawSha1 = FilledSha1(0x11);
            byte[] parentSha1 = FilledSha1(0x22);
            byte[] childSha1 = FilledSha1(0x33);
            byte[] otherSha1 = FilledSha1(0x44);
            byte[] chainSha1 = FilledSha1(0x55);

            string childPath = Path.Combine(childDirectory, "a-child.chd");
            string siblingParentPath = Path.Combine(childDirectory, "z-parent.chd");
            string remoteParentPath = Path.Combine(remoteDirectory, "parent-moved.chd");
            string dataShaOnlyDecoyPath = Path.Combine(childDirectory, "data-sha-only-decoy.chd");
            string chainedParentPath = Path.Combine(remoteDirectory, "parented-parent.chd");
            string chainRootPath = Path.Combine(remoteDirectory, "chain-root.chd");
            string incompatibleGraphParentPath = Path.Combine(childDirectory, "0-incompatible-parent.chd");

            WriteSyntheticV5Chd(childPath, FilledSha1(0x31), childSha1, parentSha1);
            WriteSyntheticV5Chd(siblingParentPath, parentRawSha1, parentSha1, null);
            WriteSyntheticV5Chd(remoteParentPath, parentRawSha1, parentSha1, null);
            // A parent's Data SHA1 is not its V5 parent identity. This fixture
            // catches accidental RawSha1 indexing or pair validation.
            WriteSyntheticV5Chd(dataShaOnlyDecoyPath, parentSha1, otherSha1, null);
            WriteSyntheticV5Chd(chainedParentPath, parentRawSha1, parentSha1, chainSha1);
            WriteSyntheticV5Chd(chainRootPath, FilledSha1(0x54), chainSha1, null);

            ChdContainerInfo identity = new ChdContainerInfo
            {
                Sha1 = CloneBytes(parentSha1),
                RawSha1 = CloneBytes(parentRawSha1)
            };
            if (!string.Equals(ChdParentResolver.EffectiveSha1(identity), ChdParentResolver.Hex(parentSha1), StringComparison.Ordinal))
                throw new InvalidDataException("Parent resolution preferred Data SHA1 over the V5 SHA1 identity.");
            identity.Sha1 = new byte[20];
            if (!string.Equals(ChdParentResolver.EffectiveSha1(identity), ChdParentResolver.Hex(parentRawSha1), StringComparison.Ordinal))
                throw new InvalidDataException("Parent resolution did not fall back to Data SHA1 when SHA1 was absent.");

            ChdParentResolver.Reset();
            if (!ChdParentResolver.TryValidatePair(childPath, siblingParentPath, out string validPairError))
                throw new InvalidDataException("A matching standalone CHD parent was rejected: " + validPairError);
            if (ChdParentResolver.TryValidatePair(childPath, dataShaOnlyDecoyPath, out _))
                throw new InvalidDataException("A CHD parent was accepted by Data SHA1 instead of SHA1.");
            if (ChdParentResolver.TryValidatePair(childPath, chainedParentPath, out _))
                throw new InvalidDataException("A parented CHD was accepted as an immediate collection parent.");

            // Register the remote duplicate first; a matching sibling must
            // still be selected deterministically.
            ChdParentResolver.Reset();
            if (!ChdParentResolver.TryRegister(remoteParentPath, out _, out string remoteRegisterError))
                throw new InvalidDataException("Remote CHD parent could not be registered: " + remoteRegisterError);
            if (!ChdParentResolver.TryResolveParent(childPath, out string resolvedParent, out string resolveError) ||
                !string.Equals(Path.GetFullPath(resolvedParent), Path.GetFullPath(siblingParentPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A sibling CHD parent was not preferred deterministically: " + resolveError);

            // Simulate Fix ROMs moving the parent from ToSort to ROMRoot. A
            // fresh resolution must skip the stale path and find its new one.
            File.Delete(siblingParentPath);
            ChdParentResolver.Reset();
            if (!ChdParentResolver.TryRegister(remoteParentPath, out _, out remoteRegisterError))
                throw new InvalidDataException("Moved CHD parent could not be registered: " + remoteRegisterError);
            if (!ChdParentResolver.TryResolveParent(childPath, out resolvedParent, out resolveError) ||
                !string.Equals(Path.GetFullPath(resolvedParent), Path.GetFullPath(remoteParentPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A child CHD did not re-resolve its moved parent: " + resolveError);
            if (!ChdParentResolver.WouldOrphanExistingDependents(remoteParentPath))
                throw new InvalidDataException("The last standalone parent copy was considered safe to remove while a child still existed.");
            string alternateParentPath = Path.Combine(remoteDirectory, "parent-installed-copy.chd");
            WriteSyntheticV5Chd(alternateParentPath, parentRawSha1, parentSha1, null);
            if (!ChdParentResolver.TryRegister(alternateParentPath, out _, out string alternateRegisterError))
                throw new InvalidDataException("Alternate CHD parent could not be registered: " + alternateRegisterError);
            if (ChdParentResolver.WouldOrphanExistingDependents(remoteParentPath))
                throw new InvalidDataException("A source parent was retained even though another standalone copy was available.");

            ChdParentResolver.Reset();
            if (!ChdParentResolver.TryRegister(chainedParentPath, out _, out string chainRegisterError))
                throw new InvalidDataException("Chained parent fixture could not be registered: " + chainRegisterError);
            if (ChdParentResolver.TryResolveParent(childPath, out _, out string chainError) ||
                !chainError.Contains("also requires a parent", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A chained parent graph was not rejected explicitly: " + chainError);

            ChdParentResolver.Reset();
            if (ChdParentResolver.TryResolveParent(childPath, out _, out string missingError) ||
                !missingError.Contains(ChdParentResolver.Hex(parentSha1), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A missing CHD parent did not report its required SHA1: " + missingError);

            string selfPath = Path.Combine(childDirectory, "self.chd");
            byte[] selfSha1 = FilledSha1(0x66);
            WriteSyntheticV5Chd(selfPath, FilledSha1(0x65), selfSha1, selfSha1);
            ChdParentResolver.Reset();
            if (ChdParentResolver.TryResolveParent(selfPath, out _, out string selfError) ||
                !selfError.Contains("self-reference", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A self-parented CHD was not rejected explicitly: " + selfError);

            WriteSyntheticV5Chd(siblingParentPath, parentRawSha1, parentSha1, null);
            WriteSyntheticV5Chd(incompatibleGraphParentPath, parentRawSha1, parentSha1, null, 8192, 1);
            if (ChdParentGraph.ValidateGraph(new[] { siblingParentPath, childPath }, out string graphError) != 0)
                throw new InvalidDataException("A valid parent graph was rejected: " + graphError);
            if (ChdParentGraph.ValidateGraph(new[] { childPath }, out _) == 0)
                throw new InvalidDataException("A parent graph with a missing parent was accepted.");
            if (ChdParentGraph.ValidateGraph(new[] { dataShaOnlyDecoyPath, childPath }, out _) == 0)
                throw new InvalidDataException("A graph parent was matched by Data SHA1 instead of SHA1.");
            if (ChdParentGraph.ValidateGraph(new[] { chainedParentPath, siblingParentPath, childPath, chainRootPath }, out string duplicateGraphError) != 0)
                throw new InvalidDataException("A standalone duplicate was not selected over a parented candidate: " + duplicateGraphError);
            if (ChdParentGraph.ValidateGraph(new[] { incompatibleGraphParentPath, siblingParentPath, childPath }, out string geometryGraphError) != 0)
                throw new InvalidDataException("A geometry-compatible duplicate parent was not selected: " + geometryGraphError);
            if (ChdParentGraph.ValidateGraph(new[] { incompatibleGraphParentPath, childPath }, out string incompatibleGraphError) == 0 ||
                !incompatibleGraphError.Contains("geometry", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A graph with only an incompatible parent geometry was not rejected explicitly: " + incompatibleGraphError);
            if (ChdParentGraph.ValidateGraph(new[] { chainedParentPath, childPath, chainRootPath }, out string nestedGraphError) == 0 ||
                !nestedGraphError.Contains("nested", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An acyclic nested parent graph was not rejected explicitly: " + nestedGraphError);

            string cycleAPath = Path.Combine(root, "cycle-a.chd");
            string cycleBPath = Path.Combine(root, "cycle-b.chd");
            byte[] cycleASha1 = FilledSha1(0x71);
            byte[] cycleBSha1 = FilledSha1(0x72);
            WriteSyntheticV5Chd(cycleAPath, FilledSha1(0x73), cycleASha1, cycleBSha1);
            WriteSyntheticV5Chd(cycleBPath, FilledSha1(0x74), cycleBSha1, cycleASha1);
            if (ChdParentGraph.ValidateGraph(new[] { cycleAPath, cycleBPath }, out string cycleError) == 0 ||
                !cycleError.Contains("cycle", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A cyclic CHD parent graph was not rejected explicitly: " + cycleError);
        }
        finally
        {
            ChdParentResolver.Reset();
            if (Settings.rvSettings != null)
                Settings.rvSettings.ChdParentSearchPaths = savedSearchPaths ?? new List<string>();
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static byte[] FilledSha1(byte value)
    {
        byte[] hash = new byte[20];
        for (int i = 0; i < hash.Length; i++)
            hash[i] = value;
        return hash;
    }

    private static void WriteSyntheticV5Chd(string path, byte[] rawSha1, byte[] sha1, byte[] parentSha1,
        uint hunkSize = 4096, uint unitSize = 1)
    {
        if (rawSha1 == null || rawSha1.Length != 20 || sha1 == null || sha1.Length != 20 ||
            (parentSha1 != null && parentSha1.Length != 20))
            throw new ArgumentException("Synthetic CHD SHA1 values must contain exactly 20 bytes.");

        byte[] header = new byte[124];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("MComprHD"), 0, header, 0, 8);
        WriteBigEndian(header, 8, 124);
        WriteBigEndian(header, 12, 5);
        // Four zero codecs make this an uncompressed V5 image. Logical size
        // is zero, so no map entries or payload bytes are required.
        WriteBigEndian(header, 44, 124);
        WriteBigEndian(header, 56, hunkSize);
        WriteBigEndian(header, 60, unitSize);
        Buffer.BlockCopy(rawSha1, 0, header, 64, 20);
        Buffer.BlockCopy(sha1, 0, header, 84, 20);
        if (parentSha1 != null)
            Buffer.BlockCopy(parentSha1, 0, header, 104, 20);

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Path.GetTempPath());
        File.WriteAllBytes(path, header);
    }

    private static byte[] Hash(byte[] bytes, HashAlgorithm algorithm)
    {
        using (algorithm)
            return algorithm.ComputeHash(bytes);
    }

    private static byte[] CloneBytes(byte[] bytes)
    {
        byte[] clone = new byte[bytes.Length];
        Buffer.BlockCopy(bytes, 0, clone, 0, bytes.Length);
        return clone;
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i])
                return false;
        return true;
    }

    private static void AssertSerializeRejected(ChdReconstructionManifest manifest, string label)
    {
        try
        {
            manifest.Serialize();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidDataException("Manifest hardening did not reject " + label + ".");
    }

    private static void AssertInvalidHeader(uint version)
    {
        byte[] header = new byte[16];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("MComprHD"), 0, header, 0, 8);
        WriteBigEndian(header, 8, 124);
        WriteBigEndian(header, 12, version);
        using (MemoryStream stream = new MemoryStream(header, false))
        {
            if (CHD.CheckHeader(stream, out _, out _))
                throw new InvalidDataException("Unsupported CHD header version was accepted: " + version + ".");
        }
    }

    private static void WriteBigEndian(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
