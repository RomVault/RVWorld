using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CHDSharpLib;

namespace RomVaultCore.Utils;

public static class ChdParentGraph
{
    public static int Verify(string childPath, string parentPath, out string report)
    {
        return VerifyWithTool(childPath, parentPath, ChdmanProcessTracker.FindExecutable(), out report);
    }

    private static int VerifyWithTool(string childPath, string parentPath, string tool, out string report)
    {
        report = "";
        if (!TryValidatePair(childPath, parentPath, out ChdContainerInfo child, out _, out report)) return 2;
        ChdmanRunResult result = ChdmanService.Run(tool,
            "verify -i " + ChdmanService.Quote(childPath) + " -ip " + ChdmanService.Quote(parentPath),
            Path.GetDirectoryName(childPath) ?? Environment.CurrentDirectory,
            600000);
        if (!ToolRunPassed(result, out string toolError))
        {
            report = "Parent graph verification failed: " + toolError;
            return 5;
        }
        report = "Parent graph verified: " + Hex(child.ParentSha1);
        return 0;
    }

    public static int CreateChild(string standalonePath, string parentPath, string outputPath, out string report)
    {
        return CreateChildWithTool(standalonePath, parentPath, outputPath, ChdmanProcessTracker.FindExecutable(), out report);
    }

    private static int CreateChildWithTool(string standalonePath, string parentPath, string outputPath, string tool, out string report)
    {
        report = "";
        if (!File.Exists(standalonePath) || !File.Exists(parentPath)) { report = "Standalone source or parent CHD was not found."; return 2; }
        if (File.Exists(outputPath)) { report = "Parented output already exists; refusing to overwrite it."; return 2; }
        if (!ChdMetadata.TryReadContainerInfo(standalonePath, out ChdContainerInfo source, out string sourceError) || source.RequiresParent)
        { report = "Source must be a readable standalone CHD: " + sourceError; return 2; }
        if (!ChdMetadata.TryReadContainerInfo(parentPath, out ChdContainerInfo parent, out string parentError) || parent.RequiresParent)
        { report = "Parent must be a readable standalone CHD: " + parentError; return 2; }
        if (Path.GetFullPath(standalonePath).Equals(Path.GetFullPath(parentPath), StringComparison.OrdinalIgnoreCase))
        { report = "A CHD cannot be its own parent."; return 2; }
        if (!TryGetNativeCopyOptions(standalonePath, source, out string copyCodecs, out string sourceProfileError))
        { report = "Source CHD encoding profile is invalid: " + sourceProfileError; return 2; }
        if (!TryGetNativeCopyOptions(parentPath, parent, out _, out string parentProfileError))
        { report = "Parent CHD encoding profile is invalid: " + parentProfileError; return 2; }
        if (!HasCompatibleParentGeometry(source, parent))
        {
            report = "Source and parent CHDs have incompatible hunk or unit geometry; materialize/recompress the parent first.";
            return 2;
        }

        outputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        if (!ChdArtifactPaths.TryAllocate(outputPath, out ChdArtifactPathSet artifacts, out string artifactError,
                ChdArtifactKind.Child, ChdArtifactKind.Standalone))
        { report = artifactError; return 5; }
        string childTemp = artifacts[ChdArtifactKind.Child];
        string materialized = artifacts[ChdArtifactKind.Standalone];
        if (!ChdmanService.TryValidateExternalPaths(out string pathError,
                standalonePath, parentPath, outputPath, childTemp, materialized))
        { report = pathError; return 5; }
        try
        {
            ChdmanRunResult create = ChdmanService.Run(tool,
                "copy -i " + ChdmanService.Quote(standalonePath) + " -o " + ChdmanService.Quote(childTemp) +
                " -op " + ChdmanService.Quote(parentPath) + " -c " + copyCodecs + " -hs " + source.HunkSize + " -f",
                directory,
                600000);
            if (!ToolRunPassed(create, out string createError))
            { report = "Could not create parented CHD: " + createError; return 5; }
            if (!PreservesNativeEncoding(standalonePath, childTemp, out string childNativeError))
            { report = childNativeError; return 5; }
            if (!PreservesRvrm(standalonePath, childTemp, out string childIdentityError))
            { report = childIdentityError; return 5; }
            if (!PreservesEncodingProfile(standalonePath, childTemp, out string childProfileError))
            { report = childProfileError; return 5; }
            if (VerifyWithTool(childTemp, parentPath, tool, out string verifyReport) != 0) { report = verifyReport; return 5; }
            int standaloneCode = MaterializeStandaloneInternal(childTemp, parentPath, materialized, tool, out string standaloneReport);
            if (standaloneCode != 0) { report = standaloneReport; return standaloneCode; }
            if (!ChdMetadata.TryReadContainerInfo(materialized, out ChdContainerInfo roundTrip, out _) ||
                !HashEqual(source.RawSha1 ?? source.Sha1, roundTrip.RawSha1 ?? roundTrip.Sha1) ||
                !string.Equals(LogicalSha256(standalonePath), LogicalSha256(materialized), StringComparison.OrdinalIgnoreCase))
            {
                report = "Parented CHD failed standalone materialization parity.";
                return 5;
            }
            File.Move(childTemp, outputPath);
            report = "Created reversible parented CHD; parentSha1=" + ChdParentResolver.EffectiveSha1(parent);
            return 0;
        }
        finally
        {
            Delete(childTemp);
            Delete(materialized);
        }
    }

    public static int MaterializeStandalone(string childPath, string parentPath, string outputPath, out string report)
    {
        if (File.Exists(outputPath)) { report = "Standalone output already exists; refusing to overwrite it."; return 2; }
        string tool = ChdmanProcessTracker.FindExecutable();
        outputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        if (!ChdArtifactPaths.TryAllocate(outputPath, out ChdArtifactPathSet artifacts, out string artifactError,
                ChdArtifactKind.Standalone))
        { report = artifactError; return 5; }
        string temp = artifacts[ChdArtifactKind.Standalone];
        if (!ChdmanService.TryValidateExternalPaths(out string pathError, childPath, parentPath, outputPath, temp))
        { report = pathError; return 5; }
        try
        {
            int code = MaterializeStandaloneInternal(childPath, parentPath, temp, tool, out report);
            if (code == 0) File.Move(temp, outputPath);
            return code;
        }
        finally { Delete(temp); }
    }

    public static int ValidateGraph(IEnumerable<string> paths, out string report)
    {
        Dictionary<string, List<Node>> byId = new Dictionary<string, List<Node>>(StringComparer.OrdinalIgnoreCase);
        List<Node> nodes = new List<Node>();
        IEnumerable<string> orderedPaths = (paths ?? Array.Empty<string>())
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal);
        foreach (string path in orderedPaths)
        {
            if (!ChdMetadata.TryReadContainerInfo(path, out ChdContainerInfo info, out string error))
            { report = ChdDiagnosticFormatter.RedactPath(path) + ": " + error; return 5; }
            Node node = new Node { Path = path, Info = info };
            nodes.Add(node);
            AddId(byId, ChdParentResolver.EffectiveSha1(info), node);
        }
        List<string> resolutionErrors = new List<string>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!nodes[i].Info.RequiresParent) continue;
            string parentId = Hex(nodes[i].Info.ParentSha1);
            if (!byId.TryGetValue(parentId, out List<Node> candidates))
            {
                resolutionErrors.Add(ChdDiagnosticFormatter.RedactPath(nodes[i].Path) + ": missing parent " + parentId);
                continue;
            }
            List<Node> compatible = candidates
                .Where(candidate => HasCompatibleParentGeometry(nodes[i].Info, candidate.Info))
                .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
                .ToList();
            if (compatible.Count == 0)
            {
                resolutionErrors.Add(ChdDiagnosticFormatter.RedactPath(nodes[i].Path) +
                                     ": no parent with matching hunk and unit geometry was found for " + parentId);
                continue;
            }

            // A content identity can have several physical representations.
            // Prefer a standalone geometry-compatible representation, with a
            // stable path tie-breaker, instead of trusting discovery order.
            nodes[i].Parent = compatible.FirstOrDefault(candidate => !candidate.Info.RequiresParent) ?? compatible[0];
        }
        HashSet<Node> complete = new HashSet<Node>();
        for (int i = 0; i < nodes.Count; i++)
        {
            HashSet<Node> active = new HashSet<Node>();
            Node current = nodes[i];
            while (current != null && !complete.Contains(current))
            {
                if (!active.Add(current))
                { report = "Parent graph contains a cycle at " + ChdDiagnosticFormatter.RedactPath(current.Path); return 5; }
                current = current.Parent;
            }
            foreach (Node node in active) complete.Add(node);
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Parent?.Info.RequiresParent != true) continue;
            report = ChdDiagnosticFormatter.RedactPath(nodes[i].Path) +
                     ": nested parent chains are unsupported; materialize the supplied parent first.";
            return 5;
        }
        if (resolutionErrors.Count > 0)
        {
            report = resolutionErrors[0];
            return 5;
        }
        report = "Parent graph valid; nodes=" + nodes.Count + "; parented=" + nodes.Count(node => node.Info.RequiresParent);
        return 0;
    }

    internal static bool RunPolicySelfTest(out string error)
    {
        error = "";
        ChdmanRunResult verified = new ChdmanRunResult
        {
            Success = true,
            ExitCode = 0,
            StandardOutput = "Raw SHA1 verification successful!" + Environment.NewLine +
                             "Overall SHA1 verification successful!"
        };
        if (!ToolRunPassed(verified, out string verifiedError))
        {
            error = "A successful chdman verification was rejected: " + verifiedError;
            return false;
        }

        ChdmanRunResult mismatch = new ChdmanRunResult
        {
            Success = true,
            ExitCode = 0,
            StandardError = "Error: Raw SHA1 in header = 0000" + Environment.NewLine +
                            "        actual SHA1 = ffff"
        };
        if (ToolRunPassed(mismatch, out _))
        {
            error = "A checksum mismatch reported with exit code zero was accepted.";
            return false;
        }

        string futureText = "schema=" + ChdEncodingProfile.CurrentProfileSchema +
                            ";profile=" + ChdEncodingProfile.ProfileId +
                            ";revision=" + (ChdEncodingProfile.CurrentProfileRevision + 1) +
                            ";storage=archive";
        if (!ChdEncodingProfile.TryParseMetadata(futureText, out ChdEncodingProfile future) ||
            ChdEncodingProfile.CanUseCurrentPhysicalPolicy(future, out _))
        {
            error = "A future RVEP revision was allowed to use the current physical policy.";
            return false;
        }
        return true;
    }

    internal static bool RunFixture(string executable, out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-parent-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string parentRaw = Path.Combine(root, "parent.raw");
            string childRaw = Path.Combine(root, "child.raw");
            byte[] parentBytes = new byte[64 * 1024];
            byte[] childBytes = new byte[parentBytes.Length];
            for (int i = 0; i < parentBytes.Length; i++) { parentBytes[i] = (byte)i; childBytes[i] = (byte)i; }
            for (int i = 0; i < 128; i++) childBytes[4096 + i] ^= 0x5a;
            File.WriteAllBytes(parentRaw, parentBytes); File.WriteAllBytes(childRaw, childBytes);
            string parent = Path.Combine(root, "parent.chd"); string source = Path.Combine(root, "source.chd"); string child = Path.Combine(root, "child.chd"); string standalone = Path.Combine(root, "standalone.chd");
            string incompatibleParent = Path.Combine(root, "incompatible-parent.chd"); string rejectedChild = Path.Combine(root, "rejected-child.chd");
            ChdmanRunResult a = ChdmanService.Run(executable, "createraw -i " + ChdmanService.Quote(parentRaw) + " -o " + ChdmanService.Quote(parent) + " -us 1 -hs 4096 -c zstd -f", root, 180000);
            ChdmanRunResult b = ChdmanService.Run(executable, "createraw -i " + ChdmanService.Quote(childRaw) + " -o " + ChdmanService.Quote(source) + " -us 1 -hs 4096 -c zstd -f", root, 180000);
            ChdmanRunResult incompatible = ChdmanService.Run(executable, "createraw -i " + ChdmanService.Quote(parentRaw) + " -o " + ChdmanService.Quote(incompatibleParent) + " -us 1 -hs 8192 -c zstd -f", root, 180000);
            if (!a.Success || !b.Success || !incompatible.Success)
                throw new InvalidDataException(!a.Success ? a.Output : (!b.Success ? b.Output : incompatible.Output));
            if (!ChdmanService.TryGetIdentity(executable, ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out string identityError))
                throw new InvalidDataException(identityError);
            ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("raw", ChdStorageProfile.Playback);
            if (!ChdReconstructionManifest.TryCreateFromSource(childRaw, profile, identity, out ChdReconstructionManifest manifest, out string manifestError))
                throw new InvalidDataException(manifestError);
            string manifestPath = Path.Combine(root, "source.rvrm");
            File.WriteAllBytes(manifestPath, manifest.Serialize());
            ChdmanRunResult profileStep = ChdmanService.Run(executable,
                "addmeta -i " + ChdmanService.Quote(source) + " -t " + ChdEncodingProfile.MetadataTag + " -ix 0 -vt " +
                ChdmanService.Quote(profile.ToMetadata(identity)) + " -nocs", root, 30000);
            ChdmanRunResult manifestStep = profileStep.Success
                ? ChdmanService.Run(executable,
                    "addmeta -i " + ChdmanService.Quote(source) + " -t " + ChdReconstructionManifest.MetadataTag + " -ix 0 -vf " +
                    ChdmanService.Quote(manifestPath) + " -nocs", root, 30000)
                : profileStep;
            if (!manifestStep.Success)
                throw new InvalidDataException(manifestStep.Output);
            if (CreateChildWithTool(source, parent, child, executable, out string createReport) != 0) throw new InvalidDataException(createReport);
            if (MaterializeStandaloneInternal(child, parent, standalone, executable, out string materializeReport) != 0) throw new InvalidDataException(materializeReport);
            if (CreateChildWithTool(source, incompatibleParent, rejectedChild, executable, out _) == 0 || File.Exists(rejectedChild))
                throw new InvalidDataException("Parent graph accepted incompatible source/parent hunk geometry.");
            if (ValidateGraph(new[] { parent, child }, out string graphReport) != 0) throw new InvalidDataException(graphReport);
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    private static int MaterializeStandaloneInternal(string childPath, string parentPath, string outputPath, string tool, out string report)
    {
        report = "";
        if (!TryValidatePair(childPath, parentPath, out ChdContainerInfo child, out ChdContainerInfo parent, out report)) return 2;
        if (!HasCompatibleParentGeometry(child, parent))
        {
            report = "Child and parent CHDs have incompatible hunk or unit geometry.";
            return 2;
        }
        if (!TryGetNativeCopyOptions(childPath, child, out string copyCodecs, out string childProfileError))
        { report = "Child CHD encoding profile is invalid: " + childProfileError; return 2; }
        if (!TryGetNativeCopyOptions(parentPath, parent, out _, out string parentProfileError))
        { report = "Parent CHD encoding profile is invalid: " + parentProfileError; return 2; }
        string work = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        ChdmanRunResult copy = ChdmanService.Run(tool,
            "copy -i " + ChdmanService.Quote(childPath) + " -ip " + ChdmanService.Quote(parentPath) +
            " -o " + ChdmanService.Quote(outputPath) + " -c " + copyCodecs + " -hs " + child.HunkSize + " -f",
            work,
            600000);
        if (!ToolRunPassed(copy, out string copyError))
        { report = "Standalone conversion failed: " + copyError; return 5; }
        ChdmanRunResult verify = ChdmanService.Run(tool, "verify -i " + ChdmanService.Quote(outputPath), work, 600000);
        if (!ToolRunPassed(verify, out string verifyError))
        { report = "Standalone verification failed: " + verifyError; return 5; }
        if (!ChdMetadata.TryReadContainerInfo(outputPath, out ChdContainerInfo standalone, out string error) || standalone.RequiresParent ||
            !HashEqual(child.RawSha1 ?? child.Sha1, standalone.RawSha1 ?? standalone.Sha1))
        { report = "Standalone header parity failed: " + error; return 5; }
        if (!PreservesNativeEncoding(childPath, outputPath, out string nativeError))
        { report = nativeError; return 5; }
        if (!PreservesRvrm(childPath, outputPath, out string identityError))
        { report = identityError; return 5; }
        if (!PreservesEncodingProfile(childPath, outputPath, out string profileError))
        { report = profileError; return 5; }
        report = "Materialized verified standalone CHD.";
        return 0;
    }

    private static bool PreservesRvrm(string sourcePath, string destinationPath, out string error)
    {
        error = "";
        if (!ChdMetadata.TryReadBinaryMetadata(sourcePath, ChdReconstructionManifest.MetadataTag, 0, out byte[] sourceBytes, out string sourceReadError))
        {
            if (string.Equals(sourceReadError, "Metadata not found.", StringComparison.Ordinal))
                return true;
            error = "CHD representation conversion could not inspect source reconstruction metadata: " + sourceReadError;
            return false;
        }
        if (!ChdReconstructionManifest.TryDeserialize(sourceBytes, out ChdReconstructionManifest source, out string sourceManifestError))
        {
            error = "CHD representation conversion refused malformed source reconstruction metadata: " + sourceManifestError;
            return false;
        }
        if (!ChdReconstructionManifest.TryRead(destinationPath, out ChdReconstructionManifest destination, out string readError))
        {
            error = "CHD representation conversion dropped reconstruction identity metadata: " + readError;
            return false;
        }
        if (!RvrmWireFormat.CanonicallyEquals(source, destination, out error))
        {
            error = "CHD representation conversion changed reconstruction identity metadata: " + error;
            return false;
        }
        return true;
    }

    private static bool TryGetNativeCopyOptions(string path, ChdContainerInfo container, out string codecs, out string error)
    {
        codecs = "";
        error = "";
        if (container == null || container.HunkSize == 0 || container.UnitSize == 0)
        {
            error = "native CHD hunk/unit geometry is missing";
            return false;
        }

        codecs = NormalizeCodecs(container.Codecs);
        if (codecs.Length == 0)
            codecs = "none";

        if (!ChdMetadata.TryReadTextMetadata(path, ChdEncodingProfile.MetadataTag, 0,
                out string profileText, out string readError))
        {
            if (string.Equals(readError, "Metadata not found.", StringComparison.Ordinal))
                return true;
            error = "could not inspect RVEP metadata: " + readError;
            return false;
        }
        if (!ChdEncodingProfile.TryParseMetadata(profileText, out ChdEncodingProfile profile))
        {
            error = "RVEP metadata is malformed or unsupported";
            return false;
        }
        if (!ProfileMatchesNative(path, profile, out error))
            return false;
        return true;
    }

    private static bool HasCompatibleParentGeometry(ChdContainerInfo source, ChdContainerInfo parent)
    {
        return source != null && parent != null &&
               source.HunkSize == parent.HunkSize && source.UnitSize == parent.UnitSize;
    }

    private static bool PreservesNativeEncoding(string sourcePath, string destinationPath, out string error)
    {
        error = "";
        if (!ChdMetadata.TryReadContainerInfo(sourcePath, out ChdContainerInfo source, out string sourceError))
        {
            error = "CHD representation conversion could not inspect source native encoding: " + sourceError;
            return false;
        }
        if (!ChdMetadata.TryReadContainerInfo(destinationPath, out ChdContainerInfo destination, out string destinationError))
        {
            error = "CHD representation conversion could not inspect converted native encoding: " + destinationError;
            return false;
        }
        if (source.HunkSize != destination.HunkSize || source.UnitSize != destination.UnitSize ||
            !string.Equals(NormalizeCodecs(source.Codecs), NormalizeCodecs(destination.Codecs), StringComparison.OrdinalIgnoreCase))
        {
            error = "CHD representation conversion changed native codecs, hunk size, or unit size.";
            return false;
        }
        return true;
    }

    private static bool PreservesEncodingProfile(string sourcePath, string destinationPath, out string error)
    {
        error = "";
        if (!ChdMetadata.TryReadTextMetadata(sourcePath, ChdEncodingProfile.MetadataTag, 0,
                out string sourceText, out string sourceReadError))
        {
            if (string.Equals(sourceReadError, "Metadata not found.", StringComparison.Ordinal))
                return true;
            error = "CHD representation conversion could not inspect source encoder-profile metadata: " + sourceReadError;
            return false;
        }
        if (!ChdEncodingProfile.TryParseMetadata(sourceText, out ChdEncodingProfile sourceProfile))
        {
            error = "CHD representation conversion refused malformed or unsupported source RVEP metadata.";
            return false;
        }
        if (!ChdMetadata.TryReadTextMetadata(destinationPath, ChdEncodingProfile.MetadataTag, 0,
                out string destinationText, out string destinationReadError) ||
            !ChdEncodingProfile.TryParseMetadata(destinationText, out ChdEncodingProfile destinationProfile))
        {
            error = "CHD representation conversion dropped or corrupted RVEP metadata: " + destinationReadError;
            return false;
        }
        if (!string.Equals(sourceText, destinationText, StringComparison.Ordinal) ||
            sourceProfile.Schema != destinationProfile.Schema ||
            sourceProfile.ProfileRevision != destinationProfile.ProfileRevision ||
            sourceProfile.WriterRevision != destinationProfile.WriterRevision ||
            !string.Equals(sourceProfile.Storage, destinationProfile.Storage, StringComparison.Ordinal) ||
            !string.Equals(sourceProfile.ToolSha256 ?? "", destinationProfile.ToolSha256 ?? "", StringComparison.OrdinalIgnoreCase))
        {
            error = "CHD representation conversion changed RVEP profile identity metadata.";
            return false;
        }
        if (!ProfileMatchesNative(sourcePath, sourceProfile, out string sourceNativeError))
        {
            error = "Source RVEP is inconsistent with its native CHD encoding: " + sourceNativeError;
            return false;
        }
        if (!ProfileMatchesNative(destinationPath, destinationProfile, out string destinationNativeError))
        {
            error = "Converted RVEP is inconsistent with its native CHD encoding: " + destinationNativeError;
            return false;
        }
        return true;
    }

    private static bool ProfileMatchesNative(string path, ChdEncodingProfile parsed, out string error)
    {
        error = "";
        if (!ChdEncodingProfile.CanUseCurrentPhysicalPolicy(parsed, out error))
            return false;
        if (!ChdMetadata.TryReadContainerInfo(path, out ChdContainerInfo container, out error) ||
            !ChdEncodingProfile.TryRead(path, out ChdEncodingProfile hydrated))
            return false;

        string expectedCodecs;
        int expectedHunk;
        int expectedUnit;
        if (parsed.Schema == ChdEncodingProfile.CurrentProfileSchema)
        {
            ChdStorageProfile storage = string.Equals(parsed.Storage, "playback", StringComparison.Ordinal)
                ? ChdStorageProfile.Playback
                : ChdStorageProfile.Archive;
            if (string.IsNullOrWhiteSpace(hydrated.Family))
            {
                error = "media family could not be determined";
                return false;
            }
            ChdEncodingProfileSpec expected = ChdEncodingProfile.ForFamily(
                hydrated.Family, storage, (int)container.HunkSize, (int)container.UnitSize);
            if ((hydrated.Family == "cd" || hydrated.Family == "gdi") &&
                ChdReconstructionManifest.TryRead(path, out ChdReconstructionManifest manifest, out _))
                ChdEncodingProfile.ApplyDialect(expected, manifest.Dialect);
            expectedCodecs = expected.Codecs;
            expectedHunk = expected.HunkSize;
            expectedUnit = expected.UnitSize;
        }
        else
        {
            expectedCodecs = parsed.Codecs;
            expectedHunk = parsed.HunkSize;
            expectedUnit = parsed.UnitSize;
        }

        if (expectedHunk <= 0 || container.HunkSize != expectedHunk ||
            (expectedUnit > 0 && container.UnitSize != expectedUnit) ||
            !string.Equals(NormalizeCodecs(container.Codecs), NormalizeCodecs(expectedCodecs), StringComparison.OrdinalIgnoreCase))
        {
            error = "hunk size, unit size, or codecs do not match the recorded storage profile";
            return false;
        }
        return true;
    }

    private static string NormalizeCodecs(IEnumerable<string> codecs)
    {
        return string.Join(",", codecs ?? Array.Empty<string>()).Replace(" ", "");
    }

    private static string NormalizeCodecs(string codecs)
    {
        return (codecs ?? "").Replace(" ", "");
    }

    private static bool TryValidatePair(string childPath, string parentPath, out ChdContainerInfo child, out ChdContainerInfo parent, out string error)
    {
        child = null; parent = null; error = "";
        if (!File.Exists(childPath) || !File.Exists(parentPath)) { error = "Child or parent CHD was not found."; return false; }
        if (!ChdMetadata.TryReadContainerInfo(childPath, out child, out error) || !child.RequiresParent) { error = "Child is not a readable parented CHD. " + error; return false; }
        if (!ChdMetadata.TryReadContainerInfo(parentPath, out parent, out error)) return false;
        if (parent.RequiresParent) { error = "The supplied CHD parent is not standalone."; return false; }
        byte[] effectiveParentSha1 = HasHash(parent.Sha1) ? parent.Sha1 : parent.RawSha1;
        if (!HashEqual(child.ParentSha1, effectiveParentSha1)) { error = "The supplied parent SHA1 does not match the child header."; return false; }
        if (!HasCompatibleParentGeometry(child, parent)) { error = "Child and parent CHDs have incompatible hunk or unit geometry."; return false; }
        return true;
    }

    private static bool ToolRunPassed(ChdmanRunResult result, out string error)
    {
        error = result?.Output ?? "chdman returned no result";
        if (result == null || !result.Success)
            return false;

        string output = result.Output ?? "";
        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimStart();
            if (line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Fatal error occurred", StringComparison.OrdinalIgnoreCase) ||
                line.IndexOf("checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("SHA1 mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("SHA-1 mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.StartsWith("No verification to be done", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        error = "";
        return true;
    }

    private static bool HasHash(byte[] value)
    {
        if (value == null || value.Length == 0) return false;
        for (int i = 0; i < value.Length; i++) if (value[i] != 0) return true;
        return false;
    }

    private static string LogicalSha256(string path)
    {
        using (Stream stream = ChdLogicalStream.OpenRead(path)) return ChdScrubber.HashSha256(stream);
    }

    private static bool HashEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0) return false;
        int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0;
    }

    private static string Hex(byte[] value)
    {
        return value == null ? "" : string.Concat(value.Select(item => item.ToString("x2")));
    }

    private static void AddId(Dictionary<string, List<Node>> map, string id, Node node)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!map.TryGetValue(id, out List<Node> nodes))
        {
            nodes = new List<Node>();
            map.Add(id, nodes);
        }
        nodes.Add(node);
    }

    private static void Delete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private sealed class Node { public string Path; public ChdContainerInfo Info; public Node Parent; }
}
