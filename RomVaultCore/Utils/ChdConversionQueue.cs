using CHDSharpLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace RomVaultCore.Utils;

public sealed class ChdConversionJob
{
    public string Path { get; set; } = "";
    public long SourceLength { get; set; }
    public long SourceWriteUtcTicks { get; set; }
    public string SourceContainerSha1 { get; set; } = "";
    public string SourceRawSha1 { get; set; } = "";
    public string Family { get; set; } = "";
    public string Dialect { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public string LastError { get; set; } = "";
    public long EstimatedTemporaryBytes { get; set; }
}

[XmlRoot("ChdConversionQueue")]
public sealed class ChdConversionQueueFile
{
    public int Schema { get; set; } = 1;
    public string Target { get; set; } = "archive";
    public long CreatedUtcTicks { get; set; }
    public List<ChdConversionJob> Jobs { get; set; } = new List<ChdConversionJob>();
}

public static class ChdConversionQueue
{
    public static int Create(string root, ChdStorageProfile target, string queuePath, out string report)
    {
        if (!Directory.Exists(root)) { report = "CHD conversion root was not found."; return 2; }
        try
        {
            ChdConversionQueueFile queue = new ChdConversionQueueFile
            {
                Target = target == ChdStorageProfile.Playback ? "playback" : "archive",
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };
            foreach (string path in Directory.GetFiles(root, "*.chd", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                queue.Jobs.Add(Inspect(path, target));
            Save(queuePath, queue);
            report = BuildReport(queuePath, queue);
            return queue.Jobs.Any(job => job.Status == "blocked") ? 5 : 0;
        }
        catch (Exception ex)
        {
            report = "Could not create CHD conversion queue: " + ex.Message;
            return 5;
        }
    }

    public static int Status(string queuePath, out string report)
    {
        try
        {
            report = BuildReport(queuePath, Load(queuePath));
            return 0;
        }
        catch (Exception ex) { report = "Could not read CHD conversion queue: " + ex.Message; return 5; }
    }

    public static int Run(string queuePath, int limit, out string report)
    {
        try
        {
            ChdConversionQueueFile queue = Load(queuePath);
            ChdStorageProfile target = string.Equals(queue.Target, "playback", StringComparison.OrdinalIgnoreCase)
                ? ChdStorageProfile.Playback : ChdStorageProfile.Archive;
            if (limit < 1) limit = int.MaxValue;
            int processed = 0;
            foreach (ChdConversionJob job in queue.Jobs ?? new List<ChdConversionJob>())
            {
                if (processed >= limit) break;
                if (job == null || string.Equals(job.Status, "complete", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(job.Status, "current", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(job.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                    continue;
                processed++;
                job.Attempts++;
                int code;
                bool changed = false;
                string itemReport;
                if (!MatchesPlannedSource(job, out itemReport))
                {
                    code = 2;
                    job.Status = "blocked";
                }
                else
                {
                    code = ChdStandaloneConverter.Convert(job.Path, target, out changed, out itemReport);
                    job.Status = code == 0 ? (changed ? "complete" : "current") : "failed";
                }
                job.LastError = code == 0 ? "" : itemReport;
                if (code == 0)
                {
                    try
                    {
                        FileInfo info = new FileInfo(job.Path);
                        job.SourceLength = info.Length;
                        job.SourceWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                        if (ChdMetadata.TryReadContainerInfo(job.Path, out ChdContainerInfo updated, out _))
                        {
                            job.SourceContainerSha1 = Hex(updated.Sha1);
                            job.SourceRawSha1 = Hex(updated.RawSha1);
                        }
                    }
                    catch { }
                }
                Save(queuePath, queue);
            }
            report = BuildReport(queuePath, queue);
            return queue.Jobs.Any(job => job != null && (job.Status == "failed" || job.Status == "blocked")) ? 5 : 0;
        }
        catch (Exception ex) { report = "Could not run CHD conversion queue: " + ex.Message; return 5; }
    }

    private static ChdConversionJob Inspect(string path, ChdStorageProfile target)
    {
        FileInfo file = new FileInfo(path);
        ChdConversionJob job = new ChdConversionJob
        {
            Path = file.FullName,
            SourceLength = file.Length,
            SourceWriteUtcTicks = file.LastWriteTimeUtc.Ticks
        };
        if (!ChdReconstructionManifest.TryRead(path, out ChdReconstructionManifest manifest, out string manifestError))
        {
            job.Status = "blocked";
            job.LastError = "Integrity-checked reconstruction manifest required: " + manifestError;
            return job;
        }
        if (manifest.Schema != ChdReconstructionManifest.CurrentSchema)
        {
            job.Status = "blocked";
            job.LastError = "This CHD has legacy reconstruction metadata. Repair it through DAT-aware fixing to migrate identity safely before standalone conversion.";
            return job;
        }
        if (!ChdReconstructionManifest.HasCompleteOpticalIdentity(manifest, out string descriptorError))
        {
            job.Status = "blocked";
            job.LastError = descriptorError + " Repair this CHD through DAT-aware fixing before queue conversion.";
            return job;
        }
        job.Family = manifest.Family ?? "";
        job.Dialect = manifest.Dialect ?? "";
        if (!ChdEncodingProfile.TryDescribeExisting(path, false, target, out ChdEncodingProfileSpec spec, out ChdContainerInfo container, out string describeError))
        {
            job.Status = "blocked";
            job.LastError = describeError;
            return job;
        }
        ChdEncodingProfile.ApplyDialect(spec, manifest.Dialect);
        job.SourceContainerSha1 = Hex(container.Sha1);
        job.SourceRawSha1 = Hex(container.RawSha1);
        job.EstimatedTemporaryBytes = checked((long)Math.Min(long.MaxValue, container.LogicalSize + (ulong)Math.Max(0, file.Length) * 2UL + 512UL * 1024 * 1024));
        string tool = ChdToolchainRegistry.Select(spec.Family, target, manifest.Dialect, out string selectionError);
        string identityError = "";
        ChdmanIdentity identity = null;
        if (string.IsNullOrWhiteSpace(tool) || !ChdmanService.TryGetIdentity(tool, ChdmanProbeLevel.Full, out identity, out identityError))
        {
            job.Status = "blocked";
            job.LastError = string.IsNullOrWhiteSpace(selectionError) ? identityError : selectionError;
            return job;
        }
        job.Status = ChdEncodingProfile.NeedsRecompression(path, spec, container, identity, out string reason) ? "pending" : "current";
        job.LastError = reason;
        return job;
    }

    private static string BuildReport(string queuePath, ChdConversionQueueFile queue)
    {
        List<ChdConversionJob> jobs = queue?.Jobs ?? new List<ChdConversionJob>();
        StringBuilder text = new StringBuilder();
        text.AppendLine("RomVault CHD conversion queue");
        text.AppendLine("queue=" + ChdDiagnosticFormatter.RedactPath(queuePath));
        text.AppendLine("target=" + (queue?.Target ?? ""));
        foreach (string status in new[] { "pending", "failed", "blocked", "complete", "current" })
            text.AppendLine(status + "=" + jobs.Count(job => job != null && string.Equals(job.Status, status, StringComparison.OrdinalIgnoreCase)));
        long estimatedTemporaryBytes = 0;
        foreach (ChdConversionJob job in jobs.Where(item => item != null && item.Status == "pending"))
            estimatedTemporaryBytes = SaturatingAdd(estimatedTemporaryBytes, Math.Max(0, job.EstimatedTemporaryBytes));
        text.AppendLine("estimatedTemporaryBytes=" + estimatedTemporaryBytes);
        foreach (ChdConversionJob job in jobs.Where(item => item != null && (item.Status == "failed" || item.Status == "blocked")))
            text.AppendLine(ChdDiagnosticFormatter.RedactPath(job.Path) + " " + job.Status + ": " + job.LastError);
        return text.ToString().TrimEnd();
    }

    private static ChdConversionQueueFile Load(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        {
            ChdConversionQueueFile queue = (ChdConversionQueueFile)new XmlSerializer(typeof(ChdConversionQueueFile)).Deserialize(stream);
            if (queue == null || queue.Schema != 1) throw new InvalidDataException("Unsupported CHD conversion queue schema.");
            return queue;
        }
    }

    private static void Save(string path, ChdConversionQueueFile queue)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? Environment.CurrentDirectory);
        string temp = full + ".tmp." + Guid.NewGuid().ToString("N");
        string backup = full + ".bak." + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                new XmlSerializer(typeof(ChdConversionQueueFile)).Serialize(stream, queue);
                stream.Flush(true);
            }
            if (File.Exists(full))
            {
                try { File.Replace(temp, full, backup, true); }
                catch
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(full, backup);
                    try { File.Move(temp, full); }
                    catch
                    {
                        if (!File.Exists(full) && File.Exists(backup)) File.Move(backup, full);
                        throw;
                    }
                }
            }
            else File.Move(temp, full);
            try { if (File.Exists(backup)) File.Delete(backup); } catch { }
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right) return long.MaxValue;
        return left + right;
    }

    private static bool MatchesPlannedSource(ChdConversionJob job, out string error)
    {
        error = "";
        try
        {
            FileInfo file = new FileInfo(job.Path);
            if (!file.Exists || file.Length != job.SourceLength || file.LastWriteTimeUtc.Ticks != job.SourceWriteUtcTicks)
            {
                error = "Source CHD changed after this queue was planned; re-plan before conversion.";
                return false;
            }
            if (!ChdMetadata.TryReadContainerInfo(job.Path, out ChdContainerInfo current, out string readError))
            {
                error = "Could not validate the queued source CHD: " + readError;
                return false;
            }
            if ((!string.IsNullOrEmpty(job.SourceContainerSha1) && !string.Equals(job.SourceContainerSha1, Hex(current.Sha1), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(job.SourceRawSha1) && !string.Equals(job.SourceRawSha1, Hex(current.RawSha1), StringComparison.OrdinalIgnoreCase)))
            {
                error = "Source CHD identity changed after this queue was planned; re-plan before conversion.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "Could not validate the queued source CHD: " + ex.Message;
            return false;
        }
    }

    private static string Hex(byte[] value)
    {
        if (value == null || value.Length == 0) return "";
        StringBuilder result = new StringBuilder(value.Length * 2);
        for (int i = 0; i < value.Length; i++) result.Append(value[i].ToString("x2"));
        return result.ToString();
    }
}

internal static class ChdStandaloneConverter
{
    public static int Convert(string path, ChdStorageProfile target, out bool changed, out string report)
    {
        return Convert(path, target, null, out changed, out report);
    }

    internal static int Convert(string path, ChdStorageProfile target, string forcedTool, out bool changed, out string report)
    {
        changed = false;
        if (!File.Exists(path)) { report = "CHD was not found."; return 2; }
        if (!ChdReconstructionManifest.TryRead(path, out ChdReconstructionManifest manifest, out string manifestError) ||
            manifest.Schema != ChdReconstructionManifest.CurrentSchema)
        { report = "Integrity-checked current reconstruction manifest required: " + manifestError; return 2; }
        if (!ChdReconstructionManifest.HasCompleteOpticalIdentity(manifest, out string descriptorError))
        { report = descriptorError + " Repair this CHD through DAT-aware fixing before standalone conversion."; return 2; }
        byte[] canonicalManifest;
        try { canonicalManifest = manifest.Serialize(); }
        catch (Exception ex) { report = "Reconstruction identity is not canonical: " + ex.Message; return 2; }
        if (!ChdEncodingProfile.TryDescribeExisting(path, false, target, out ChdEncodingProfileSpec profile, out ChdContainerInfo before, out string describeError))
        { report = describeError; return 2; }
        ChdEncodingProfile.ApplyDialect(profile, manifest.Dialect);
        string selectionErrorValue = "";
        string tool = string.IsNullOrWhiteSpace(forcedTool)
            ? ChdToolchainRegistry.Select(profile.Family, target, manifest.Dialect, out selectionErrorValue)
            : forcedTool;
        string selectionError = string.IsNullOrWhiteSpace(forcedTool) ? selectionErrorValue : "";
        string identityError = "";
        ChdmanIdentity identity = null;
        if (string.IsNullOrWhiteSpace(tool) || !ChdmanService.TryGetIdentity(tool, ChdmanProbeLevel.Full, out identity, out identityError))
        { report = string.IsNullOrWhiteSpace(selectionError) ? identityError : selectionError; return 2; }
        if (!ChdEncodingProfile.NeedsRecompression(path, profile, before, identity, out string reason))
        { report = "CHD already matches " + profile.Storage + " v1."; return 0; }
        if (before.RequiresParent) { report = "Parented CHDs must be materialized before standalone conversion."; return 2; }
        if (!ChdBoundPayloadValidator.TryCaptureOpticalLayout(path, manifest,
                out ChdOpticalLayoutSnapshot expectedOpticalLayout, out string layoutSnapshotError))
        { report = layoutSnapshotError; return 2; }

        string full = Path.GetFullPath(path);
        long sourceLength;
        try { sourceLength = new FileInfo(full).Length; }
        catch (Exception ex) { report = "Could not inspect source CHD: " + ex.Message; return 2; }
        if (!ChdUpgradeRecovery.HasSufficientSpace(full, before.LogicalSize, sourceLength,
                out long requiredBytes, out long freeBytes, out string spaceError))
        {
            report = spaceError + $" Required={requiredBytes:N0}; available={freeBytes:N0}.";
            return 5;
        }
        string directory = Path.GetDirectoryName(full) ?? Environment.CurrentDirectory;
        if (!ChdArtifactPaths.TryAllocate(full, out ChdArtifactPathSet artifacts, out string artifactError,
                ChdArtifactKind.Stage, ChdArtifactKind.Final, ChdArtifactKind.Backup,
                ChdArtifactKind.Manifest, ChdArtifactKind.Raw))
        {
            report = artifactError;
            return 5;
        }
        string stage = artifacts[ChdArtifactKind.Stage];
        string final = artifacts[ChdArtifactKind.Final];
        string backup = artifacts[ChdArtifactKind.Backup];
        string metadata = artifacts[ChdArtifactKind.Manifest];
        string raw = artifacts[ChdArtifactKind.Raw];
        if (!ChdmanService.TryValidateExternalPaths(out string pathError, tool, directory, full, stage, final, metadata, raw))
        {
            report = pathError;
            return 5;
        }
        bool backupCreated = false;
        bool installedDestination = false;
        bool destinationVerified = false;
        bool recoveryNeeded = false;
        bool preserveInterruptedArtifacts = false;
        string journalId = null;
        try
        {
            journalId = ChdUpgradeRecovery.Begin(full, full, stage, final, backup, metadata, raw);
            ChdUpgradeRecovery.SetExpectedRvrm(journalId, canonicalManifest);
            ChdFaultInjection.Check(ChdFaultPoint.JournalCreated);
        }
        catch (Exception ex)
        {
            try { ChdUpgradeRecovery.ReleaseOwnership(journalId); } catch { }
            report = "Could not create the CHD conversion recovery journal: " + ex.Message;
            return 5;
        }
        try
        {
            int stageHunk = ChdEncodingProfile.GreatestCommonDivisor((int)before.HunkSize, profile.HunkSize);
            if (stageHunk < 16 || before.UnitSize == 0 || stageHunk % before.UnitSize != 0)
                throw new InvalidDataException("No safe intermediate hunk size exists for conversion.");
            ChdmanRunResult step;
            bool rebuildHdd = profile.Family == "hdd" && !ChdHddGeometry.Matches(full, profile);
            if (rebuildHdd)
            {
                step = ChdmanService.Run(tool, $"extracthd -i {ChdmanService.Quote(full)} -o {ChdmanService.Quote(raw)} -f", directory, 0);
                if (step.Success)
                    step = ChdmanService.Run(tool, $"createhd -i {ChdmanService.Quote(raw)} -o {ChdmanService.Quote(stage)} -c none -hs {stageHunk} -ss {profile.HddSectorSize} -chs {profile.HddCylinders},{profile.HddHeads},{profile.HddSectors} -f", directory, 0);
            }
            else
                step = ChdmanService.Run(tool, $"copy -i {ChdmanService.Quote(full)} -o {ChdmanService.Quote(stage)} -c none -hs {stageHunk} -f", directory, 0);
            if (!step.Success) throw new InvalidDataException(step.Output);
            ChdFaultInjection.Check(ChdFaultPoint.StageCreated);

            File.WriteAllBytes(metadata, canonicalManifest);
            step = ChdmanService.Run(tool, $"addmeta -i {ChdmanService.Quote(stage)} -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt {ChdmanService.Quote(profile.ToMetadata(identity))} -nocs", directory, 30000);
            if (step.Success)
                ChdFaultInjection.Check(ChdFaultPoint.ProfileMetadataWritten);
            if (step.Success)
                step = ChdmanService.Run(tool, $"addmeta -i {ChdmanService.Quote(stage)} -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf {ChdmanService.Quote(metadata)} -nocs", directory, 30000);
            if (step.Success)
                ChdFaultInjection.Check(ChdFaultPoint.ManifestMetadataWritten);
            if (step.Success)
                step = ChdmanService.Run(tool, $"copy -i {ChdmanService.Quote(stage)} -o {ChdmanService.Quote(final)} -c {profile.Codecs} -hs {profile.HunkSize} -f", directory, 0);
            if (step.Success)
                step = ChdmanService.Run(tool, $"verify -i {ChdmanService.Quote(final)}", directory, 0);
            if (!step.Success) throw new InvalidDataException(step.Output);
            if (!ValidateConvertedCandidate(final, profile, identity, manifest, out ChdContainerInfo after, out string finalError))
                throw new InvalidDataException("Converted CHD validation failed: " + finalError);
            if (!Equal(before.RawSha1 ?? before.Sha1, after.RawSha1 ?? after.Sha1)) throw new InvalidDataException("Logical CHD SHA-1 changed during conversion.");
            if (!ChdBoundPayloadValidator.TryValidate(final, tool, directory, manifest, expectedOpticalLayout, out string payloadError))
                throw new InvalidDataException("Converted CHD RVRM payload validation failed before installation: " + payloadError);
            ChdUpgradeRecovery.SetExpectedChdHashes(journalId, after.Sha1, after.RawSha1);
            ChdFaultInjection.Check(ChdFaultPoint.FinalCreated);

            try
            {
                File.Replace(final, full, backup, true);
                backupCreated = true;
                ChdFaultInjection.Check(ChdFaultPoint.BackupCreated);
            }
            catch (ChdInjectedCrashException) { throw; }
            catch
            {
                File.Move(full, backup);
                backupCreated = true;
                ChdFaultInjection.Check(ChdFaultPoint.BackupCreated);
                try { File.Move(final, full); }
                catch
                {
                    File.Move(backup, full);
                    backupCreated = false;
                    throw;
                }
            }
            installedDestination = true;
            recoveryNeeded = true;
            ChdUpgradeRecovery.MarkInstalled(journalId);
            ChdFaultInjection.Check(ChdFaultPoint.DestinationInstalled);

            step = ChdmanService.Run(tool, $"verify -i {ChdmanService.Quote(full)}", directory, 0);
            ChdContainerInfo installed = null;
            string installedError = "";
            if (!step.Success)
                throw new InvalidDataException(step.Output);
            if (!ValidateConvertedCandidate(full, profile, identity, manifest, out installed, out installedError))
                throw new InvalidDataException(installedError);
            if (!Equal(before.RawSha1 ?? before.Sha1, installed?.RawSha1 ?? installed?.Sha1))
                throw new InvalidDataException("Installed CHD logical SHA-1 changed during conversion.");
            if (!ChdBoundPayloadValidator.TryValidate(full, tool, directory, manifest, expectedOpticalLayout, out string installedPayloadError))
                throw new InvalidDataException("Installed CHD RVRM payload validation failed: " + installedPayloadError);
            ChdUpgradeRecovery.MarkVerified(journalId);
            destinationVerified = true;
            ChdFaultInjection.Check(ChdFaultPoint.DestinationVerified);

            bool cleanupComplete = TryCleanupArtifacts(stage, final, metadata, raw, backup);
            backupCreated = File.Exists(backup);
            recoveryNeeded = !cleanupComplete;
            preserveInterruptedArtifacts = !cleanupComplete;
            changed = true;
            report = "Converted CHD to " + profile.Storage + " v1; reason=" + reason + "; path=" + ChdDiagnosticFormatter.RedactPath(full) +
                     (cleanupComplete ? "" : "; temporary cleanup deferred to recovery");
            return 0;
        }
        catch (Exception ex)
        {
            if (destinationVerified || ex is ChdInjectedCrashException)
            {
                preserveInterruptedArtifacts = true;
                recoveryNeeded = true;
            }
            else if (backupCreated && File.Exists(backup))
            {
                TryRollback(journalId, tool);
                backupCreated = File.Exists(backup);
                recoveryNeeded = true;
                preserveInterruptedArtifacts = true;
            }
            else if (installedDestination)
            {
                recoveryNeeded = true;
            }
            report = "CHD conversion failed: " + ex.Message;
            return 5;
        }
        finally
        {
            if (!preserveInterruptedArtifacts)
            {
                bool cleanupComplete = backupCreated
                    ? TryCleanupArtifacts(stage, final, metadata, raw)
                    : TryCleanupArtifacts(stage, final, metadata, raw, backup);
                if (!cleanupComplete)
                    recoveryNeeded = true;
                if (!recoveryNeeded)
                {
                    try { ChdUpgradeRecovery.Complete(journalId); }
                    catch { recoveryNeeded = true; }
                }
            }
            try { ChdUpgradeRecovery.ReleaseOwnership(journalId); } catch { }
        }
    }

    private static bool TryRollback(string journalId, string executable)
    {
        try { return ChdUpgradeRecovery.TryCheckpointRollback(journalId, executable, out _); }
        catch { return false; }
    }

    private static bool ValidateConvertedCandidate(
        string path,
        ChdEncodingProfileSpec expected,
        ChdmanIdentity identity,
        ChdReconstructionManifest expectedManifest,
        out ChdContainerInfo container,
        out string error)
    {
        container = null;
        error = "";
        int expectedWriterRevision = identity?.Capabilities?.WriterRevision(expected?.Family) ?? 0;
        if (expected == null || !ChdEncodingProfile.TryRead(path, out ChdEncodingProfile profile))
        {
            error = "The requested RVEP encoder profile is missing or invalid.";
            return false;
        }
        if (!string.Equals(profile.Profile, ChdEncodingProfile.ProfileId, StringComparison.Ordinal) ||
            profile.Schema != ChdEncodingProfile.CurrentProfileSchema ||
            profile.ProfileRevision != expected.ProfileRevision ||
            profile.WriterRevision != expectedWriterRevision ||
            !string.Equals(profile.Storage, expected.Storage, StringComparison.Ordinal) ||
            !string.Equals(profile.Family, expected.Family, StringComparison.Ordinal) ||
            !ChdEncodingProfile.IsSha256(identity?.BinarySha256) ||
            !string.Equals(profile.ToolSha256 ?? "", identity?.BinarySha256 ?? "", StringComparison.OrdinalIgnoreCase))
        {
            error = "RVEP does not identify the requested storage profile, media family, writer revision, and tool.";
            return false;
        }
        if (!ChdMetadata.TryReadContainerInfo(path, out container, out string containerError) ||
            container.RequiresParent ||
            container.HunkSize != expected.HunkSize ||
            (expected.UnitSize > 0 && container.UnitSize != expected.UnitSize) ||
            !string.Equals(NormalizeCodecs(container?.Codecs), NormalizeCodecs(expected.Codecs), StringComparison.OrdinalIgnoreCase))
        {
            error = "Native CHD storage facts do not match the requested profile: " + containerError;
            return false;
        }
        if (expected.Family == "hdd" && !ChdHddGeometry.Matches(path, expected))
        {
            error = "Native hard-disk geometry does not match the requested profile.";
            return false;
        }
        if (!ChdReconstructionManifest.TryRead(path, out ChdReconstructionManifest manifest, out string manifestError) ||
            manifest.Schema != ChdReconstructionManifest.CurrentSchema ||
            !string.Equals(manifest.Family, expected.Family, StringComparison.Ordinal) ||
            !ChdReconstructionManifest.HasCompleteOpticalIdentity(manifest, out manifestError))
        {
            error = "RVRM is missing, invalid, or inconsistent with the requested media family: " + manifestError;
            return false;
        }
        if (!RvrmWireFormat.CanonicallyEquals(expectedManifest, manifest, out string identityError))
        {
            error = "RVRM reconstruction identity changed during conversion: " + identityError;
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

    private static bool TryCleanupArtifacts(params string[] paths)
    {
        bool cleaned = true;
        for (int i = 0; paths != null && i < paths.Length; i++)
        {
            try { if (File.Exists(paths[i])) File.Delete(paths[i]); } catch { }
            cleaned &= string.IsNullOrWhiteSpace(paths[i]) || !File.Exists(paths[i]);
        }
        return cleaned;
    }

    private static bool Equal(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        int difference = 0;
        for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
        return difference == 0;
    }
}
