using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using CHDSharpLib;

namespace RomVaultCore.Utils;

public sealed class ChdUpgradeJournalEntry
{
    public string Id { get; set; }
    public string SourcePath { get; set; }
    public string DestinationPath { get; set; }
    public string StagePath { get; set; }
    public string FinalPath { get; set; }
    public string BackupPath { get; set; }
    public string ManifestPath { get; set; }
    public string AuxiliaryPath { get; set; }
    public string ExpectedRvrmSha256 { get; set; }
    public string ExpectedContainerSha1 { get; set; }
    public string ExpectedRawSha1 { get; set; }
    public string OriginalRvrmSha256 { get; set; }
    public string OriginalContainerSha1 { get; set; }
    public string OriginalRawSha1 { get; set; }
    public string State { get; set; }
    public long StartedUtcTicks { get; set; }
    public int OwnerProcessId { get; set; }
    public long OwnerProcessStartUtcTicks { get; set; }
}

[XmlRoot("ChdUpgradeJournal")]
public sealed class ChdUpgradeJournalFile
{
    public int Schema { get; set; } = 1;
    public List<ChdUpgradeJournalEntry> Entries { get; set; } = new List<ChdUpgradeJournalEntry>();
}

public static class ChdUpgradeRecovery
{
    private const int CurrentJournalSchema = 1;
    private static readonly object Gate = new object();
    private static readonly string CrossProcessGateName = GetCrossProcessGateName();
    private static readonly Mutex CrossProcessGate = new Mutex(false, CrossProcessGateName);

    public static string Begin(string sourcePath, string destinationPath, string stagePath, string finalPath, string backupPath, string manifestPath, string auxiliaryPath = null)
    {
        string id = Guid.NewGuid().ToString("N");
        GetCurrentProcessIdentity(out int ownerProcessId, out long ownerProcessStartUtcTicks);
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                if (HasJournalCandidate(GetDevelopmentV2Path()))
                    throw new InvalidDataException("A development CHD recovery journal is pending. Resolve it with every older RomVault copy closed before starting a new CHD transaction.");
                ChdUpgradeJournalFile journal = Load();
                ChdUpgradeJournalEntry entry = new ChdUpgradeJournalEntry
                {
                    Id = id,
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    StagePath = stagePath,
                    FinalPath = finalPath,
                    BackupPath = backupPath,
                    ManifestPath = manifestPath,
                    AuxiliaryPath = auxiliaryPath,
                    State = "Preparing",
                    StartedUtcTicks = DateTime.UtcNow.Ticks,
                    OwnerProcessId = ownerProcessId,
                    OwnerProcessStartUtcTicks = ownerProcessStartUtcTicks
                };
                CaptureOriginalIdentity(entry);
                if (!ValidateEntry(entry, CurrentJournalSchema, out string validationError))
                    throw new InvalidDataException(validationError);
                journal.Entries.Add(entry);
                Save(journal);
            }
        }
        return id;
    }

    public static void MarkInstalled(string id)
    {
        UpdateState(id, "InstalledPendingVerification");
    }

    public static void SetExpectedRvrm(string id, byte[] canonicalRvrm)
    {
        if (string.IsNullOrWhiteSpace(id) || canonicalRvrm == null || canonicalRvrm.Length == 0)
            throw new ArgumentException("A journal id and canonical RVRM payload are required.");
        string expected;
        using (SHA256 sha256 = SHA256.Create())
            expected = ToHex(sha256.ComputeHash(canonicalRvrm));
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ChdUpgradeJournalFile journal = Load();
                ChdUpgradeJournalEntry entry = journal.Entries.FirstOrDefault(item => string.Equals(item?.Id, id, StringComparison.Ordinal));
                if (entry == null)
                    throw new InvalidDataException("CHD recovery journal entry was not found.");
                if (!string.Equals(entry.State, "Preparing", StringComparison.Ordinal))
                    throw new InvalidDataException("Canonical RVRM identity must be bound before CHD installation.");
                entry.ExpectedRvrmSha256 = expected;
                Save(journal);
            }
        }
    }

    public static void SetExpectedChdHashes(string id, byte[] containerSha1, byte[] rawSha1)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsSha1(containerSha1) || !IsSha1(rawSha1))
            throw new ArgumentException("A journal id and complete CHD SHA-1 values are required.");
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ChdUpgradeJournalFile journal = Load();
                ChdUpgradeJournalEntry entry = journal.Entries.FirstOrDefault(item => string.Equals(item?.Id, id, StringComparison.Ordinal));
                if (entry == null)
                    throw new InvalidDataException("CHD recovery journal entry was not found.");
                if (!string.Equals(entry.State, "Preparing", StringComparison.Ordinal))
                    throw new InvalidDataException("Expected CHD hashes must be bound before CHD installation.");
                entry.ExpectedContainerSha1 = ToHex(containerSha1);
                entry.ExpectedRawSha1 = ToHex(rawSha1);
                Save(journal);
            }
        }
    }

    public static bool TryCheckpointRollback(string id, string executable, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "The CHD rollback journal id is missing.";
            return false;
        }
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ChdUpgradeJournalFile journal = Load();
                ChdUpgradeJournalEntry entry = journal.Entries.FirstOrDefault(item => string.Equals(item?.Id, id, StringComparison.Ordinal));
                if (entry == null || !ValidateEntry(entry, CurrentJournalSchema, out error))
                    return false;
                if (!IsValidTransition(entry.State, "RolledBack"))
                {
                    error = "The CHD rollback journal is not in a rollback-capable state.";
                    return false;
                }
                string work = Path.GetDirectoryName(entry.DestinationPath) ?? Environment.CurrentDirectory;
                Func<string, bool> verifier = path => Verify(executable, path, work);
                if (!File.Exists(entry.BackupPath) || !verifier(entry.BackupPath) ||
                    !MatchesOriginalIdentity(entry, entry.BackupPath))
                {
                    error = "The preserved CHD backup could not be verified against the original identity.";
                    return false;
                }
                if (!TryCopyBackup(entry, verifier, true))
                {
                    error = "The CHD destination is a valid file outside this transaction, or the original backup could not be restored safely; rollback was deferred for manual review.";
                    return false;
                }
                entry.State = "RolledBack";
                Save(journal);
                return true;
            }
        }
    }

    public static void MarkVerified(string id)
    {
        UpdateState(id, "Verified");
    }

    public static void MarkRolledBack(string id)
    {
        UpdateState(id, "RolledBack");
    }

    public static void Complete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ChdUpgradeJournalFile journal = Load();
                journal.Entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
                Save(journal);
            }
        }
    }

    public static void ReleaseOwnership(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        GetCurrentProcessIdentity(out int ownerProcessId, out long ownerProcessStartUtcTicks);
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ChdUpgradeJournalFile journal = Load();
                if (ReleaseOwnership(journal, id, ownerProcessId, ownerProcessStartUtcTicks))
                    Save(journal);
            }
        }
    }

    public static bool HasSufficientSpace(string destinationPath, ulong logicalSize, long sourceLength, out long requiredBytes, out long freeBytes, out string error)
    {
        requiredBytes = 0;
        freeBytes = 0;
        error = "";
        try
        {
            long logical = logicalSize > long.MaxValue ? long.MaxValue : (long)logicalSize;
            // The temporary uncompressed CHD and the final compressed CHD can
            // coexist.  Treat the final as potentially as large as the full
            // logical payload; incompressible media must not make the
            // preflight underestimate by several gigabytes.
            long finalAllowance = Math.Max(sourceLength, logical);
            long margin = 256L * 1024 * 1024;
            requiredBytes = SaturatingAdd(SaturatingAdd(logical, finalAllowance), margin);
            string full = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(full) ?? Path.GetPathRoot(full);
            if (!ChdFreeSpace.TryGetAvailableBytes(directory, out freeBytes, out error))
                return false;
            if (freeBytes < requiredBytes)
            {
                error = $"Insufficient free space for safe CHD recompression. Required approximately {requiredBytes:N0} bytes; available {freeBytes:N0} bytes.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "Could not determine free space for CHD recompression: " + ex.Message;
            return false;
        }
    }

    public static string RecoverPending()
    {
        List<string> messages = new List<string>();
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ReportDevelopmentJournal(GetDevelopmentV2Path(), messages);
                RecoverPendingJournal(GetPath(), CurrentJournalSchema, messages);
            }
        }
        return string.Join(Environment.NewLine, messages);
    }

    private static void ReportDevelopmentJournal(string path, List<string> messages)
    {
        if (HasJournalCandidate(path))
            messages.Add("A development CHD recovery journal was preserved without automatic changes. " +
                         "New CHD transactions are blocked until every older RomVault copy is closed and the journal is resolved manually.");
    }

    private static void RecoverPendingJournal(string path, int schema, List<string> messages)
    {
        ChdUpgradeJournalFile journal;
        try
        {
            journal = Load(path, schema);
        }
        catch (Exception ex)
        {
            messages.Add("CHD recovery journal schema " + schema + " is unreadable and was preserved for manual recovery: " + ex.Message);
            return;
        }

        string executable = ChdmanService.FindExecutable();
        for (int pass = 0; pass < 2; pass++)
        {
            bool checkpointedRollback = false;
            List<ChdUpgradeJournalEntry> remaining = new List<ChdUpgradeJournalEntry>();
            for (int i = 0; i < journal.Entries.Count; i++)
            {
                ChdUpgradeJournalEntry entry = journal.Entries[i];
                string priorState = entry?.State;
                try
                {
                    if (!ValidateEntry(entry, schema, out string validationError))
                    {
                        remaining.Add(entry);
                        messages.Add("CHD recovery rejected an unsafe journal entry: " + validationError);
                        continue;
                    }
                    if (IsOwnerAlive(entry))
                    {
                        remaining.Add(entry);
                        messages.Add("CHD recovery skipped an active transaction: " +
                                     ChdDiagnosticFormatter.RedactPath(entry?.DestinationPath));
                        continue;
                    }
                    if (RecoverEntry(entry, executable, schema, out string message))
                    {
                        if (!string.IsNullOrWhiteSpace(message))
                            messages.Add(message);
                    }
                    else
                    {
                        remaining.Add(entry);
                        bool transitioned = !string.Equals(priorState, "RolledBack", StringComparison.Ordinal) &&
                                            string.Equals(entry?.State, "RolledBack", StringComparison.Ordinal);
                        checkpointedRollback |= transitioned;
                        if (!string.IsNullOrWhiteSpace(message))
                            messages.Add(message);
                        else if (!transitioned || pass > 0)
                            messages.Add("CHD recovery deferred; source or recovery artifacts are unavailable: " +
                                         ChdDiagnosticFormatter.RedactPath(entry?.DestinationPath));
                    }
                }
                catch (Exception ex)
                {
                    remaining.Add(entry);
                    messages.Add("CHD recovery deferred for " + ChdDiagnosticFormatter.RedactPath(entry?.DestinationPath) + ": " + ex.Message);
                }
            }
            journal.Entries = remaining;
            try
            {
                Save(journal, path);
            }
            catch (Exception ex)
            {
                messages.Add("CHD recovery could not checkpoint journal schema " + schema + "; it was preserved for retry: " + ex.Message);
                return;
            }
            if (!checkpointedRollback)
                break;
        }
    }

    private static bool RecoverEntry(ChdUpgradeJournalEntry entry, string executable, int schema, out string message)
    {
        string destinationDirectory = entry == null || string.IsNullOrWhiteSpace(entry.DestinationPath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(entry.DestinationPath) ?? Environment.CurrentDirectory;
        if (schema != CurrentJournalSchema)
        {
            message = "Unsupported CHD recovery journal schema.";
            return false;
        }
        return RecoverEntry(entry, path => Verify(executable, path, destinationDirectory), Delete, true, out message);
    }

    private static bool RecoverEntry(ChdUpgradeJournalEntry entry, Func<string, bool> verifier, out string message)
    {
        return RecoverEntry(entry, verifier, Delete, false, out message);
    }

    private static bool RecoverEntry(ChdUpgradeJournalEntry entry, Func<string, bool> verifier, Func<string, bool> deleter, out string message)
    {
        return RecoverEntry(entry, verifier, deleter, false, out message);
    }

    private static bool RecoverEntry(ChdUpgradeJournalEntry entry, Func<string, bool> verifier, Func<string, bool> deleter, bool strictIdentityBinding, out string message)
    {
        message = "";
        if (entry == null || string.IsNullOrWhiteSpace(entry.DestinationPath))
            return true;
        if (!ValidateEntry(entry, CurrentJournalSchema, strictIdentityBinding, out string validationError))
        {
            message = validationError;
            return false;
        }

        bool destinationVerified = string.Equals(entry.State, "Verified", StringComparison.Ordinal);
        if (destinationVerified && File.Exists(entry.DestinationPath) && verifier(entry.DestinationPath) &&
            MatchesExpectedIdentity(entry, entry.DestinationPath))
        {
            bool cleaned = deleter(entry.BackupPath);
            cleaned = deleter(entry.StagePath) && cleaned;
            cleaned = deleter(entry.FinalPath) && cleaned;
            cleaned = deleter(entry.ManifestPath) && cleaned;
            cleaned = deleter(entry.AuxiliaryPath) && cleaned;
            if (!cleaned)
                return false;
            message = "Recovered completed CHD upgrade: " + ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return true;
        }

        if (string.Equals(entry.State, "RolledBack", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(entry.BackupPath) && File.Exists(entry.BackupPath))
            {
                if (!TryCopyBackup(entry, verifier, strictIdentityBinding))
                    return false;
            }
            else if (!File.Exists(entry.DestinationPath) || !verifier(entry.DestinationPath) ||
                     (strictIdentityBinding &&
                      (!HasOriginalChdIdentity(entry) || !MatchesOriginalIdentity(entry, entry.DestinationPath))))
            {
                return false;
            }
            bool cleaned = deleter(entry.BackupPath);
            cleaned = deleter(entry.StagePath) && cleaned;
            cleaned = deleter(entry.FinalPath) && cleaned;
            cleaned = deleter(entry.ManifestPath) && cleaned;
            cleaned = deleter(entry.AuxiliaryPath) && cleaned;
            if (!cleaned)
                return false;
            message = "Completed CHD rollback after an interrupted upgrade: " + ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(entry.BackupPath) && File.Exists(entry.BackupPath) && verifier(entry.BackupPath) &&
            MatchesOriginalIdentity(entry, entry.BackupPath))
        {
            if (!TryCopyBackup(entry, verifier, strictIdentityBinding))
                return false;
            entry.State = "RolledBack";
            message = "Checkpointed CHD rollback after an interrupted upgrade: " + ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return false;
        }

        // A source-to-new-destination transaction never modifies its source.
        // Until the journal records full DAT-aware verification, preserve that
        // source and discard any installed or staged candidate.  Structural
        // chdman verification alone is not authority to finish the repair.
        if (!string.IsNullOrWhiteSpace(entry.SourcePath) &&
            !PathsEqual(entry.SourcePath, entry.DestinationPath) &&
            (string.IsNullOrWhiteSpace(entry.BackupPath) || !File.Exists(entry.BackupPath)) &&
            File.Exists(entry.SourcePath) && verifier(entry.SourcePath))
        {
            bool preparing = string.Equals(entry.State, "Preparing", StringComparison.Ordinal);
            bool installed = string.Equals(entry.State, "InstalledPendingVerification", StringComparison.Ordinal) ||
                             string.Equals(entry.State, "Verified", StringComparison.Ordinal);
            if (!preparing && !installed)
                return false;
            bool destinationExists = File.Exists(entry.DestinationPath);
            if (destinationExists)
            {
                // Preparing includes the narrow crash window after installation
                // but before MarkInstalled.  Only the fully bound transaction
                // candidate may be removed; an unrelated/pre-existing file must
                // keep the journal pending for manual review.
                if (!HasExpectedIdentity(entry) || !MatchesExpectedIdentity(entry, entry.DestinationPath))
                    return false;
            }

            // Preparing means installation was never durably recorded. A file
            // now present at the destination may belong to another operation,
            // so only a fingerprint-bound Installed/Verified candidate may be
            // deleted here.
            bool cleaned = !destinationExists || deleter(entry.DestinationPath);
            cleaned = deleter(entry.StagePath) && cleaned;
            cleaned = deleter(entry.FinalPath) && cleaned;
            cleaned = deleter(entry.ManifestPath) && cleaned;
            cleaned = deleter(entry.AuxiliaryPath) && cleaned;
            if (!cleaned)
                return false;
            message = "Discarded an interrupted CHD candidate; the verified source was preserved: " +
                      ChdDiagnosticFormatter.RedactPath(entry.SourcePath);
            return true;
        }

        // With no backup and an in-place source still present, a Preparing
        // journal refers to the untouched original.  It is safe to retire the
        // abandoned artifacts, but never to treat an InstalledPendingVerification
        // destination as complete.
        if (!destinationVerified &&
            PathsEqual(entry.SourcePath, entry.DestinationPath) &&
            (string.IsNullOrWhiteSpace(entry.BackupPath) || !File.Exists(entry.BackupPath)) &&
            File.Exists(entry.DestinationPath) && verifier(entry.DestinationPath) &&
            string.Equals(entry.State, "Preparing", StringComparison.Ordinal) &&
            (!strictIdentityBinding ||
             (HasOriginalChdIdentity(entry) && MatchesOriginalIdentity(entry, entry.DestinationPath))))
        {
            if (!deleter(entry.StagePath) || !deleter(entry.FinalPath) ||
                !deleter(entry.ManifestPath) || !deleter(entry.AuxiliaryPath))
                return false;
            message = "Discarded an interrupted CHD upgrade; the original destination was preserved: " +
                      ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return true;
        }

        return false;
    }

    private static bool TryCopyBackup(
        ChdUpgradeJournalEntry entry,
        Func<string, bool> verifier,
        bool strictIdentityBinding,
        Func<ChdUpgradeJournalEntry, string, bool> originalMatcher = null,
        Func<ChdUpgradeJournalEntry, string, bool> expectedMatcher = null)
    {
        originalMatcher = originalMatcher ?? MatchesOriginalIdentity;
        expectedMatcher = expectedMatcher ?? MatchesExpectedIdentity;
        if (entry == null || string.IsNullOrWhiteSpace(entry.BackupPath) || !File.Exists(entry.BackupPath) ||
            string.IsNullOrWhiteSpace(entry.DestinationPath) || !verifier(entry.BackupPath) ||
            (strictIdentityBinding && !HasOriginalChdIdentity(entry)) ||
            !originalMatcher(entry, entry.BackupPath))
            return false;
        try
        {
            bool destinationExists = File.Exists(entry.DestinationPath);
            if (destinationExists && verifier(entry.DestinationPath))
            {
                if ((strictIdentityBinding || HasAnyOriginalIdentity(entry)) &&
                    originalMatcher(entry, entry.DestinationPath))
                    return true;
                if (strictIdentityBinding &&
                    (!HasExpectedIdentity(entry) || !expectedMatcher(entry, entry.DestinationPath)))
                    return false;
            }
            if (destinationExists)
                File.SetAttributes(entry.DestinationPath, FileAttributes.Normal);
            File.Copy(entry.BackupPath, entry.DestinationPath, true);
            return File.Exists(entry.DestinationPath) && verifier(entry.DestinationPath) &&
                   originalMatcher(entry, entry.DestinationPath);
        }
        catch { return false; }
    }

    private static bool MatchesExpectedIdentity(ChdUpgradeJournalEntry entry, string chdPath)
    {
        if (entry == null)
            return false;
        if (!MatchesManifestHash(entry.ExpectedRvrmSha256, chdPath))
            return false;
        if (string.IsNullOrWhiteSpace(entry.ExpectedContainerSha1) && string.IsNullOrWhiteSpace(entry.ExpectedRawSha1))
            return true; // Strict journal validation binds all installed candidates before this helper is reached.
        if (!ChdMetadata.TryReadContainerInfo(chdPath, out ChdContainerInfo info, out _))
            return false;
        return MatchesSha1(entry.ExpectedContainerSha1, info?.Sha1) &&
               MatchesSha1(entry.ExpectedRawSha1, info?.RawSha1);
    }

    private static bool HasExpectedIdentity(ChdUpgradeJournalEntry entry)
    {
        return entry != null &&
               ValidateHex(entry.ExpectedRvrmSha256, 64, true) &&
               ValidateHex(entry.ExpectedContainerSha1, 40, true) &&
               ValidateHex(entry.ExpectedRawSha1, 40, true);
    }

    private static bool HasOriginalChdIdentity(ChdUpgradeJournalEntry entry)
    {
        return entry != null &&
               ValidateHex(entry.OriginalContainerSha1, 40, true) &&
               ValidateHex(entry.OriginalRawSha1, 40, true);
    }

    private static bool HasAnyOriginalIdentity(ChdUpgradeJournalEntry entry)
    {
        return entry != null &&
               (!string.IsNullOrWhiteSpace(entry.OriginalRvrmSha256) ||
                !string.IsNullOrWhiteSpace(entry.OriginalContainerSha1) ||
                !string.IsNullOrWhiteSpace(entry.OriginalRawSha1));
    }

    private static bool MatchesOriginalIdentity(ChdUpgradeJournalEntry entry, string chdPath)
    {
        if (entry == null)
            return false;
        if (!MatchesManifestHash(entry.OriginalRvrmSha256, chdPath))
            return false;
        if (string.IsNullOrWhiteSpace(entry.OriginalContainerSha1) && string.IsNullOrWhiteSpace(entry.OriginalRawSha1))
            return true;
        if (!ChdMetadata.TryReadContainerInfo(chdPath, out ChdContainerInfo info, out _))
            return false;
        return MatchesSha1(entry.OriginalContainerSha1, info?.Sha1) &&
               MatchesSha1(entry.OriginalRawSha1, info?.RawSha1);
    }

    private static bool MatchesManifestHash(string expected, string chdPath)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return true;
        if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest manifest, out _))
            return false;
        try
        {
            byte[] canonical = manifest.Serialize();
            using (SHA256 sha256 = SHA256.Create())
                return string.Equals(expected, ToHex(sha256.ComputeHash(canonical)), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void CaptureOriginalIdentity(ChdUpgradeJournalEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.DestinationPath) || !File.Exists(entry.DestinationPath))
            return;
        if (ChdMetadata.TryReadContainerInfo(entry.DestinationPath, out ChdContainerInfo info, out _))
        {
            if (IsSha1(info?.Sha1)) entry.OriginalContainerSha1 = ToHex(info.Sha1);
            if (IsSha1(info?.RawSha1)) entry.OriginalRawSha1 = ToHex(info.RawSha1);
        }
        if (!ChdReconstructionManifest.TryRead(entry.DestinationPath, out ChdReconstructionManifest manifest, out _))
            return;
        try
        {
            using (SHA256 sha256 = SHA256.Create())
                entry.OriginalRvrmSha256 = ToHex(sha256.ComputeHash(manifest.Serialize()));
        }
        catch
        {
            entry.OriginalRvrmSha256 = "";
        }
    }

    private static bool MatchesSha1(string expected, byte[] actual)
    {
        return string.IsNullOrWhiteSpace(expected) ||
               (IsSha1(actual) && string.Equals(expected, ToHex(actual), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSha1(byte[] value) => value != null && value.Length == 20;

    private static string ToHex(byte[] value)
    {
        if (value == null)
            return "";
        StringBuilder text = new StringBuilder(value.Length * 2);
        for (int i = 0; i < value.Length; i++)
            text.Append(value[i].ToString("x2"));
        return text.ToString();
    }

    private static bool ValidateJournal(ChdUpgradeJournalFile journal, int expectedSchema, out string error)
    {
        error = "";
        if (journal == null || journal.Entries == null || journal.Schema != expectedSchema ||
            expectedSchema != CurrentJournalSchema)
        {
            error = "Unsupported or incomplete CHD recovery journal schema.";
            return false;
        }
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        List<(string source, string destination)> protectedPaths = new List<(string source, string destination)>();
        for (int i = 0; i < journal.Entries.Count; i++)
        {
            ChdUpgradeJournalEntry entry = journal.Entries[i];
            if (!ValidateEntry(entry, expectedSchema, out error))
                return false;
            if (!ids.Add(entry.Id))
            {
                error = "The CHD recovery journal contains a duplicate transaction id.";
                return false;
            }
            protectedPaths.Add((Path.GetFullPath(entry.SourcePath), Path.GetFullPath(entry.DestinationPath)));
        }
        for (int i = 0; i < protectedPaths.Count; i++)
        {
            for (int j = i + 1; j < protectedPaths.Count; j++)
            {
                // Multiple read-only transactions may share a source.  Every
                // destination is exclusive, however, and no transaction may
                // write a path another pending transaction reads or writes.
                if (PathsEqual(protectedPaths[i].destination, protectedPaths[j].destination) ||
                    PathsEqual(protectedPaths[i].destination, protectedPaths[j].source) ||
                    PathsEqual(protectedPaths[j].destination, protectedPaths[i].source))
                {
                    error = "The CHD recovery journal contains conflicting source/destination paths.";
                    return false;
                }
            }
        }
        return true;
    }

    private static bool ValidateEntry(ChdUpgradeJournalEntry entry, int schema, out string error)
    {
        return ValidateEntry(entry, schema, true, out error);
    }

    private static bool ValidateEntry(ChdUpgradeJournalEntry entry, int schema, bool strictIdentityBinding, out string error)
    {
        error = "";
        if (schema != CurrentJournalSchema)
        {
            error = "Unsupported CHD recovery journal schema.";
            return false;
        }
        if (entry == null || !Guid.TryParseExact(entry.Id, "N", out _))
        {
            error = "The CHD recovery transaction id is invalid.";
            return false;
        }
        bool knownState = string.Equals(entry.State, "Preparing", StringComparison.Ordinal) ||
                          string.Equals(entry.State, "InstalledPendingVerification", StringComparison.Ordinal) ||
                          string.Equals(entry.State, "Verified", StringComparison.Ordinal) ||
                          string.Equals(entry.State, "RolledBack", StringComparison.Ordinal);
        bool validOwner = (entry.OwnerProcessId == 0 && entry.OwnerProcessStartUtcTicks == 0) ||
                          (entry.OwnerProcessId > 0 && entry.OwnerProcessStartUtcTicks > 0);
        if (!knownState || !validOwner)
        {
            error = "The CHD recovery transaction state or owner lease is invalid.";
            return false;
        }
        if (entry.StartedUtcTicks <= 0)
        {
            error = "The CHD recovery transaction has no valid start time.";
            return false;
        }

        string source;
        string destination;
        try
        {
            source = Path.GetFullPath(entry.SourcePath ?? "");
            destination = Path.GetFullPath(entry.DestinationPath ?? "");
        }
        catch (Exception ex)
        {
            error = "The CHD recovery source or destination path is invalid: " + ex.Message;
            return false;
        }
        if (string.IsNullOrWhiteSpace(entry.SourcePath) || string.IsNullOrWhiteSpace(entry.DestinationPath) ||
            !string.Equals(Path.GetExtension(source), ".chd", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(destination), ".chd", StringComparison.OrdinalIgnoreCase))
        {
            error = "The CHD recovery source and destination must be explicit CHD files.";
            return false;
        }

        string token = null;
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source, destination };
        if (!ValidateArtifactPath(entry.StagePath, destination, "stage", ref token, paths, out error) ||
            !ValidateArtifactPath(entry.FinalPath, destination, "final", ref token, paths, out error) ||
            !ValidateArtifactPath(entry.BackupPath, destination, "backup", ref token, paths, out error) ||
            !ValidateArtifactPath(entry.ManifestPath, destination, "manifest", ref token, paths, out error) ||
            (!string.IsNullOrWhiteSpace(entry.AuxiliaryPath) &&
             !ValidateArtifactPath(entry.AuxiliaryPath, destination, "raw", ref token, paths, out error)))
            return false;

        if (!ValidateHex(entry.ExpectedRvrmSha256, 64, false) ||
            !ValidateHex(entry.ExpectedContainerSha1, 40, false) ||
            !ValidateHex(entry.ExpectedRawSha1, 40, false) ||
            !ValidateHex(entry.OriginalRvrmSha256, 64, false) ||
            !ValidateHex(entry.OriginalContainerSha1, 40, false) ||
            !ValidateHex(entry.OriginalRawSha1, 40, false))
        {
            error = "The CHD recovery transaction contains a malformed identity fingerprint.";
            return false;
        }
        if (strictIdentityBinding &&
            (string.Equals(entry.State, "InstalledPendingVerification", StringComparison.Ordinal) ||
             string.Equals(entry.State, "Verified", StringComparison.Ordinal)) &&
            (!ValidateHex(entry.ExpectedRvrmSha256, 64, true) ||
             !ValidateHex(entry.ExpectedContainerSha1, 40, true) ||
             !ValidateHex(entry.ExpectedRawSha1, 40, true)))
        {
            error = "The installed CHD recovery candidate is not bound to complete RVRM and CHD fingerprints.";
            return false;
        }
        return true;
    }

    private static bool ValidateArtifactPath(string value, string destination, string kind,
        ref string commonToken, HashSet<string> paths, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "The CHD recovery " + kind + " artifact path is missing.";
            return false;
        }
        string full;
        try { full = Path.GetFullPath(value); }
        catch (Exception ex)
        {
            error = "The CHD recovery " + kind + " artifact path is invalid: " + ex.Message;
            return false;
        }
        if (!paths.Add(full) || !string.Equals(Path.GetDirectoryName(full), Path.GetDirectoryName(destination), StringComparison.OrdinalIgnoreCase) ||
            !TryGetArtifactToken(full, kind, out string token))
        {
            error = "The CHD recovery " + kind + " path is not an owned transaction artifact.";
            return false;
        }
        if (commonToken == null)
            commonToken = token;
        else if (!string.Equals(commonToken, token, StringComparison.Ordinal))
        {
            error = "The CHD recovery artifact paths do not belong to one transaction.";
            return false;
        }
        return true;
    }

    private static bool TryGetArtifactToken(string full, string kind, out string token)
    {
        token = "";
        string compactPrefix;
        string compactSuffix;
        switch (kind)
        {
            case "stage": compactPrefix = ".__rvs."; compactSuffix = ".chd"; break;
            case "final": compactPrefix = ".__rvf."; compactSuffix = ".chd"; break;
            case "backup": compactPrefix = ".__rvb."; compactSuffix = ".chd"; break;
            case "manifest": compactPrefix = ".__rvm."; compactSuffix = ".bin"; break;
            case "raw": compactPrefix = ".__rvr."; compactSuffix = ".img"; break;
            default: return false;
        }
        string name = Path.GetFileName(full);
        if (name.StartsWith(compactPrefix, StringComparison.Ordinal) && name.EndsWith(compactSuffix, StringComparison.Ordinal) &&
            name.Length == compactPrefix.Length + 32 + compactSuffix.Length)
        {
            token = name.Substring(compactPrefix.Length, 32);
            return Guid.TryParseExact(token, "N", out _);
        }
        return false;
    }

    private static bool ValidateHex(string value, int length, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
            return !required;
        if (value.Length != length)
            return false;
        for (int i = 0; i < value.Length; i++)
            if (!Uri.IsHexDigit(value[i]))
                return false;
        return true;
    }

    public static bool RunFaultRecoverySelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-recovery-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            if (CurrentJournalSchema != 1 || new ChdUpgradeJournalFile().Schema != 1 ||
                !GetPath().EndsWith("chd-upgrade-journal-v1.xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The current CHD recovery journal is not schema 1.");
            Func<string, bool> valid = path => File.Exists(path) && File.ReadAllText(path).StartsWith("VALID:", StringComparison.Ordinal);

            // File.Replace completed: the verified destination wins and the
            // old backup is retired.
            ChdUpgradeJournalEntry replaced = CreateTestEntry(root, "replace");
            replaced.State = "Verified";
            File.WriteAllText(replaced.DestinationPath, "VALID:new");
            File.WriteAllText(replaced.BackupPath, "VALID:old");
            if (!RecoverEntry(replaced, valid, out _) || File.Exists(replaced.BackupPath) || File.ReadAllText(replaced.DestinationPath) != "VALID:new")
                throw new InvalidDataException("Completed replacement recovery failed.");

            // Fallback move interrupted after preserving the original.
            ChdUpgradeJournalEntry backedUp = CreateTestEntry(root, "backup");
            backedUp.State = "Preparing";
            File.WriteAllText(backedUp.BackupPath, "VALID:old");
            File.WriteAllText(backedUp.FinalPath, "VALID:new");
            if (RecoverEntry(backedUp, valid, out _) || backedUp.State != "RolledBack" ||
                File.ReadAllText(backedUp.DestinationPath) != "VALID:old" || !File.Exists(backedUp.BackupPath))
                throw new InvalidDataException("Backup restoration recovery failed.");
            if (!RecoverEntry(backedUp, valid, out _) || File.Exists(backedUp.BackupPath) ||
                !RecoverEntry(backedUp, valid, out _))
                throw new InvalidDataException("Backup restoration recovery was not replay-safe.");

            // A structurally valid installed candidate is not enough: before
            // DAT-aware verification, the preserved backup must win.
            ChdUpgradeJournalEntry pending = CreateTestEntry(root, "pending");
            pending.State = "InstalledPendingVerification";
            File.WriteAllText(pending.DestinationPath, "VALID:new-but-unproven");
            File.WriteAllText(pending.BackupPath, "VALID:old");
            if (RecoverEntry(pending, valid, out _) || pending.State != "RolledBack" ||
                !RecoverEntry(pending, valid, out _) || File.ReadAllText(pending.DestinationPath) != "VALID:old")
                throw new InvalidDataException("An unverified installed CHD displaced its preserved backup.");

            // An orphaned final without integrity-checked source context is not
            // sufficient authority to install a new destination.
            ChdUpgradeJournalEntry fresh = CreateTestEntry(root, "fresh");
            File.WriteAllText(fresh.FinalPath, "VALID:new");
            if (RecoverEntry(fresh, valid, out _) || File.Exists(fresh.DestinationPath) || !File.Exists(fresh.FinalPath))
                throw new InvalidDataException("Recovery installed an orphaned structural-only final.");

            // A fix sourced from a different path must keep the integrity-checked
            // source. An installed candidate without its complete transaction
            // fingerprints is preserved for manual review rather than deleted.
            ChdUpgradeJournalEntry separate = CreateTestEntry(root, "separate");
            separate.SourcePath = Path.Combine(root, "separate-source.chd");
            separate.State = "InstalledPendingVerification";
            File.WriteAllText(separate.SourcePath, "VALID:source");
            File.WriteAllText(separate.StagePath, "INVALID:stage");
            File.WriteAllText(separate.FinalPath, "VALID:candidate");
            File.WriteAllText(separate.DestinationPath, "VALID:installed-but-unproven");
            if (RecoverEntry(separate, valid, out _) || !File.Exists(separate.DestinationPath) ||
                !File.Exists(separate.StagePath) || !File.Exists(separate.FinalPath) ||
                File.ReadAllText(separate.SourcePath) != "VALID:source")
                throw new InvalidDataException("Unbound separate-destination recovery did not preserve its source and candidates for review.");

            // Preparing is ambiguous: installation may not have started, or it
            // may have completed immediately before the state checkpoint.  A
            // legitimate pre-existing destination that is not fingerprint-bound
            // to this transaction must remain untouched and journaled.
            ChdUpgradeJournalEntry separatePreparing = CreateTestEntry(root, "separate-preparing");
            separatePreparing.SourcePath = Path.Combine(root, "separate-preparing-source.chd");
            File.WriteAllText(separatePreparing.SourcePath, "VALID:source");
            File.WriteAllText(separatePreparing.DestinationPath, "VALID:pre-existing");
            File.WriteAllText(separatePreparing.StagePath, "VALID:stage");
            if (RecoverEntry(separatePreparing, valid, out _) ||
                !File.Exists(separatePreparing.DestinationPath) ||
                File.ReadAllText(separatePreparing.DestinationPath) != "VALID:pre-existing" ||
                !File.Exists(separatePreparing.StagePath))
                throw new InvalidDataException("Separate-destination Preparing recovery retired or changed an unbound destination.");

            // A valid backup is not authority to overwrite a valid CHD that
            // matches neither the original nor this transaction's candidate.
            ChdUpgradeJournalEntry foreignDestination = CreateTestEntry(root, "foreign-destination");
            foreignDestination.ExpectedRvrmSha256 = new string('a', 64);
            foreignDestination.ExpectedContainerSha1 = new string('b', 40);
            foreignDestination.ExpectedRawSha1 = new string('c', 40);
            foreignDestination.OriginalContainerSha1 = new string('d', 40);
            foreignDestination.OriginalRawSha1 = new string('e', 40);
            File.WriteAllText(foreignDestination.BackupPath, "VALID:original");
            File.WriteAllText(foreignDestination.DestinationPath, "VALID:foreign");
            Func<ChdUpgradeJournalEntry, string, bool> fakeOriginal = (_, path) =>
                File.Exists(path) && File.ReadAllText(path) == "VALID:original";
            Func<ChdUpgradeJournalEntry, string, bool> fakeExpected = (_, path) =>
                File.Exists(path) && File.ReadAllText(path) == "VALID:candidate";
            if (TryCopyBackup(foreignDestination, valid, true, fakeOriginal, fakeExpected) ||
                File.ReadAllText(foreignDestination.DestinationPath) != "VALID:foreign")
                throw new InvalidDataException("Recovery overwrote a valid foreign CHD with its transaction backup.");
            File.WriteAllText(foreignDestination.DestinationPath, "VALID:candidate");
            if (!TryCopyBackup(foreignDestination, valid, true, fakeOriginal, fakeExpected) ||
                File.ReadAllText(foreignDestination.DestinationPath) != "VALID:original")
                throw new InvalidDataException("Recovery did not restore the original over its bound transaction candidate.");

            // A corrupt installed candidate must never displace a preserved
            // valid original.
            ChdUpgradeJournalEntry corrupt = CreateTestEntry(root, "corrupt");
            File.WriteAllText(corrupt.DestinationPath, "INVALID:new");
            File.WriteAllText(corrupt.BackupPath, "VALID:old");
            if (RecoverEntry(corrupt, valid, out _) || corrupt.State != "RolledBack" ||
                !RecoverEntry(corrupt, valid, out _) || File.ReadAllText(corrupt.DestinationPath) != "VALID:old")
                throw new InvalidDataException("Corrupt candidate rollback failed.");

            ChdUpgradeJournalEntry unavailable = CreateTestEntry(root, "unavailable");
            if (RecoverEntry(unavailable, valid, out _))
                throw new InvalidDataException("Recovery accepted an unverifiable transaction.");

            ChdUpgradeJournalEntry preparing = CreateTestEntry(root, "preparing");
            preparing.SourcePath = preparing.DestinationPath;
            preparing.State = "Preparing";
            File.WriteAllText(preparing.DestinationPath, "VALID:original");
            File.WriteAllText(preparing.StagePath, "VALID:stage");
            if (!RecoverEntry(preparing, valid, out _) || !File.Exists(preparing.DestinationPath) || File.Exists(preparing.StagePath))
                throw new InvalidDataException("In-place Preparing recovery did not preserve the original and retire its stage.");

            // If the original source is temporarily unavailable, a merely
            // structural final must remain pending rather than being installed.
            ChdUpgradeJournalEntry offline = CreateTestEntry(root, "offline");
            offline.SourcePath = Path.Combine(root, "offline-source.chd");
            File.WriteAllText(offline.FinalPath, "VALID:unproven");
            if (RecoverEntry(offline, valid, out _) || File.Exists(offline.DestinationPath) || !File.Exists(offline.FinalPath))
                throw new InvalidDataException("Recovery installed an unverified final while its source was unavailable.");

            // A failed deletion must keep the transaction pending rather than
            // dropping its journal while an unverified candidate remains.
            ChdUpgradeJournalEntry locked = CreateTestEntry(root, "locked");
            locked.SourcePath = Path.Combine(root, "locked-source.chd");
            locked.State = "InstalledPendingVerification";
            File.WriteAllText(locked.SourcePath, "VALID:source");
            File.WriteAllText(locked.DestinationPath, "VALID:unproven");
            Func<string, bool> refuseDestinationDelete = path =>
                string.Equals(path, locked.DestinationPath, StringComparison.OrdinalIgnoreCase) ? false : Delete(path);
            if (RecoverEntry(locked, valid, refuseDestinationDelete, out _) || !File.Exists(locked.DestinationPath))
                throw new InvalidDataException("Recovery retired a transaction whose unverified destination could not be deleted.");

            ChdUpgradeJournalEntry cleanupLocked = CreateTestEntry(root, "cleanup-locked");
            cleanupLocked.State = "Verified";
            File.WriteAllText(cleanupLocked.DestinationPath, "VALID:new");
            File.WriteAllText(cleanupLocked.StagePath, "VALID:stage");
            Func<string, bool> refuseStageDelete = path =>
                string.Equals(path, cleanupLocked.StagePath, StringComparison.OrdinalIgnoreCase) ? false : Delete(path);
            if (RecoverEntry(cleanupLocked, valid, refuseStageDelete, out _) || !File.Exists(cleanupLocked.StagePath) ||
                !RecoverEntry(cleanupLocked, valid, out _))
                throw new InvalidDataException("Recovery retired a verified transaction before owned artifacts were cleaned.");

            if (!IsValidTransition("Preparing", "InstalledPendingVerification") ||
                !IsValidTransition("InstalledPendingVerification", "Verified") ||
                !IsValidTransition("Preparing", "RolledBack") ||
                !IsValidTransition("InstalledPendingVerification", "RolledBack") ||
                IsValidTransition("Preparing", "Verified") || IsValidTransition("Verified", "Preparing"))
                throw new InvalidDataException("CHD recovery journal state transitions are not strict.");

            GetCurrentProcessIdentity(out int currentPid, out long currentStart);
            ChdUpgradeJournalEntry liveOwner = new ChdUpgradeJournalEntry
            {
                OwnerProcessId = currentPid,
                OwnerProcessStartUtcTicks = currentStart
            };
            if (currentPid <= 0 || currentStart <= 0 || !IsOwnerAlive(liveOwner))
                throw new InvalidDataException("A live CHD transaction owner was not recognized.");
            if (Path.DirectorySeparatorChar == '\\' && !CrossProcessGateName.StartsWith(@"Global\RomVault.ChdUpgradeRecovery.v1.", StringComparison.Ordinal))
                throw new InvalidDataException("The CHD recovery lock is not shared across Windows sessions.");
            liveOwner.OwnerProcessStartUtcTicks++;
            if (IsOwnerAlive(liveOwner))
                throw new InvalidDataException("A reused process identifier was mistaken for the CHD transaction owner.");

            ChdUpgradeJournalFile ownershipJournal = new ChdUpgradeJournalFile();
            liveOwner.Id = "owned";
            liveOwner.OwnerProcessStartUtcTicks = currentStart;
            ownershipJournal.Entries.Add(liveOwner);
            if (!ReleaseOwnership(ownershipJournal, liveOwner.Id, currentPid, currentStart) ||
                liveOwner.OwnerProcessId != 0 || liveOwner.OwnerProcessStartUtcTicks != 0 || IsOwnerAlive(liveOwner))
                throw new InvalidDataException("A finished CHD transaction retained its live-owner lease.");

            string journalPath = Path.Combine(root, "journal.xml");
            ChdUpgradeJournalFile stored = new ChdUpgradeJournalFile();
            ChdUpgradeJournalEntry storedEntry = CreateTestEntry(root, "store");
            stored.Entries.Add(storedEntry);
            Save(stored, journalPath);
            ChdUpgradeJournalFile loaded = Load(journalPath);
            if (loaded.Entries.Count != 1 || loaded.Entries[0].State != "Preparing")
                throw new InvalidDataException("CHD recovery journal initial persistence failed.");
            loaded.Entries[0].State = "InstalledPendingVerification";
            loaded.Entries[0].ExpectedRvrmSha256 = new string('a', 64);
            loaded.Entries[0].ExpectedContainerSha1 = new string('b', 40);
            loaded.Entries[0].ExpectedRawSha1 = new string('c', 40);
            Save(loaded, journalPath);
            if (Load(journalPath).Entries[0].State != "InstalledPendingVerification" || !File.Exists(journalPath + ".bak"))
                throw new InvalidDataException("CHD recovery journal atomic replacement failed.");
            File.WriteAllText(journalPath, "corrupt");
            if (Load(journalPath).Entries[0].State != "Preparing")
                throw new InvalidDataException("CHD recovery journal did not fall back to its valid backup.");
            File.Delete(journalPath + ".bak");
            bool unreadableRejected = false;
            try { Load(journalPath); }
            catch (InvalidDataException) { unreadableRejected = true; }
            if (!unreadableRejected || !File.Exists(journalPath))
                throw new InvalidDataException("An unreadable CHD recovery journal was not preserved.");

            string incompatiblePath = Path.Combine(root, "incompatible-v1.xml");
            ChdUpgradeJournalFile incompatible = new ChdUpgradeJournalFile();
            ChdUpgradeJournalEntry incompatibleEntry = CreateTestEntry(root, "incompatible-v1");
            incompatibleEntry.StartedUtcTicks = 0; // Simulates the incompatible development-era schema-1 shape.
            incompatible.Entries.Add(incompatibleEntry);
            XmlSerializer incompatibleSerializer = new XmlSerializer(typeof(ChdUpgradeJournalFile));
            using (FileStream stream = new FileStream(incompatiblePath, FileMode.Create, FileAccess.Write, FileShare.None))
                incompatibleSerializer.Serialize(stream, incompatible);
            bool incompatibleRejected = false;
            try { Load(incompatiblePath); }
            catch (InvalidDataException) { incompatibleRejected = true; }
            if (!incompatibleRejected || !File.Exists(incompatiblePath))
                throw new InvalidDataException("An incompatible same-path schema-1 journal was not preserved and rejected.");

            ChdUpgradeJournalFile unsupported = new ChdUpgradeJournalFile { Schema = 99 };
            if (ValidateJournal(unsupported, 99, out _))
                throw new InvalidDataException("An unsupported CHD recovery journal schema was accepted.");
            ChdUpgradeJournalEntry aliased = CreateTestEntry(root, "aliased");
            aliased.StagePath = aliased.SourcePath;
            if (ValidateEntry(aliased, CurrentJournalSchema, out _))
                throw new InvalidDataException("A recovery artifact was allowed to alias the protected source.");

            ChdUpgradeJournalEntry firstTarget = CreateTestEntry(root, "first-target");
            ChdUpgradeJournalEntry duplicateTarget = CreateTestEntry(root, "duplicate-target");
            duplicateTarget.DestinationPath = firstTarget.DestinationPath;
            ChdUpgradeJournalFile duplicateTargets = new ChdUpgradeJournalFile
            {
                Entries = new List<ChdUpgradeJournalEntry> { firstTarget, duplicateTarget }
            };
            if (ValidateJournal(duplicateTargets, CurrentJournalSchema, out _))
                throw new InvalidDataException("Two recovery transactions were allowed to write the same normalized destination.");

            ChdUpgradeJournalEntry readWriteConflict = CreateTestEntry(root, "read-write-conflict");
            readWriteConflict.SourcePath = firstTarget.DestinationPath;
            ChdUpgradeJournalFile conflictingPaths = new ChdUpgradeJournalFile
            {
                Entries = new List<ChdUpgradeJournalEntry> { firstTarget, readWriteConflict }
            };
            if (ValidateJournal(conflictingPaths, CurrentJournalSchema, out _))
                throw new InvalidDataException("A recovery destination was allowed to alias another pending source.");

            ChdUpgradeJournalEntry sharedSourceA = CreateTestEntry(root, "shared-source-a");
            ChdUpgradeJournalEntry sharedSourceB = CreateTestEntry(root, "shared-source-b");
            string commonReadSource = Path.Combine(root, "shared-read-source.chd");
            sharedSourceA.SourcePath = commonReadSource;
            sharedSourceB.SourcePath = commonReadSource;
            ChdUpgradeJournalFile sharedRead = new ChdUpgradeJournalFile
            {
                Entries = new List<ChdUpgradeJournalEntry> { sharedSourceA, sharedSourceB }
            };
            if (!ValidateJournal(sharedRead, CurrentJournalSchema, out string sharedReadError))
                throw new InvalidDataException("Independent read-only use of one CHD source was rejected: " + sharedReadError);

            foreach (ChdFaultPoint point in Enum.GetValues(typeof(ChdFaultPoint)))
            {
                if (point == ChdFaultPoint.None)
                    continue;
                ChdFaultInjection.Arm(point);
                bool fired = false;
                try { ChdFaultInjection.Check(point); }
                catch (ChdInjectedCrashException ex) { fired = ex.Point == point; }
                if (!fired)
                    throw new InvalidDataException("Fault hook did not fire at " + point + ".");
                ChdFaultInjection.Check(point); // one-shot
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            ChdFaultInjection.Clear();
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch { }
        }
    }

    private static ChdUpgradeJournalEntry CreateTestEntry(string root, string name)
    {
        string token = Guid.NewGuid().ToString("N");
        string destination = Path.Combine(root, name + ".chd");
        return new ChdUpgradeJournalEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            SourcePath = destination,
            DestinationPath = destination,
            StagePath = Path.Combine(root, ".__rvs." + token + ".chd"),
            FinalPath = Path.Combine(root, ".__rvf." + token + ".chd"),
            BackupPath = Path.Combine(root, ".__rvb." + token + ".chd"),
            ManifestPath = Path.Combine(root, ".__rvm." + token + ".bin"),
            State = "Preparing",
            StartedUtcTicks = DateTime.UtcNow.Ticks
        };
    }

    private static bool Verify(string executable, string path, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(path))
            return false;
        if (!ChdmanService.TryValidateExternalPaths(out _, path, workingDirectory))
            return false;
        ChdmanRunResult result = ChdmanService.Run(executable, "verify -i " + ChdmanService.Quote(path), workingDirectory, 300000);
        return result.Success;
    }

    private static void UpdateState(string id, string state)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        lock (Gate)
        {
            using (AcquireCrossProcessGate())
            {
                ChdUpgradeJournalFile journal = Load();
                ChdUpgradeJournalEntry entry = journal.Entries.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
                if (entry == null)
                    throw new InvalidDataException("The CHD upgrade recovery journal entry could not be found.");
                if (!IsValidTransition(entry.State, state))
                    throw new InvalidDataException("Invalid CHD upgrade recovery transition from " + (entry.State ?? "<null>") + " to " + state + ".");
                entry.State = state;
                if (!ValidateEntry(entry, CurrentJournalSchema, out string validationError))
                    throw new InvalidDataException(validationError);
                Save(journal);
            }
        }
    }

    private static bool ReleaseOwnership(ChdUpgradeJournalFile journal, string id, int ownerProcessId, long ownerProcessStartUtcTicks)
    {
        ChdUpgradeJournalEntry entry = journal?.Entries?.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (entry == null || entry.OwnerProcessId <= 0 || entry.OwnerProcessStartUtcTicks <= 0)
            return false;
        if (ownerProcessId <= 0 || ownerProcessStartUtcTicks <= 0 ||
            entry.OwnerProcessId != ownerProcessId || entry.OwnerProcessStartUtcTicks != ownerProcessStartUtcTicks)
            throw new InvalidDataException("Only the active CHD transaction owner may release its recovery lease.");
        entry.OwnerProcessId = 0;
        entry.OwnerProcessStartUtcTicks = 0;
        return true;
    }

    private static bool IsValidTransition(string current, string next)
    {
        return (string.Equals(current, "Preparing", StringComparison.Ordinal) &&
                string.Equals(next, "InstalledPendingVerification", StringComparison.Ordinal)) ||
               (string.Equals(current, "InstalledPendingVerification", StringComparison.Ordinal) &&
                string.Equals(next, "Verified", StringComparison.Ordinal)) ||
               ((string.Equals(current, "Preparing", StringComparison.Ordinal) ||
                 string.Equals(current, "InstalledPendingVerification", StringComparison.Ordinal)) &&
                string.Equals(next, "RolledBack", StringComparison.Ordinal));
    }

    private static ChdUpgradeJournalFile Load()
    {
        return Load(GetPath(), CurrentJournalSchema);
    }

    private static ChdUpgradeJournalFile Load(string path)
    {
        return Load(path, CurrentJournalSchema);
    }

    private static ChdUpgradeJournalFile Load(string path, int expectedSchema)
    {
        string[] candidates = { path, path + ".tmp", path + ".bak" };
        for (int i = 0; i < candidates.Length; i++)
        {
            if (TryLoad(candidates[i], expectedSchema, out ChdUpgradeJournalFile journal))
                return journal;
        }

        bool anyCandidateExists = candidates.Any(File.Exists);
        if (anyCandidateExists)
            throw new InvalidDataException("The CHD upgrade recovery journal is present but unreadable; it was preserved for manual recovery.");
        return new ChdUpgradeJournalFile { Schema = expectedSchema };
    }

    private static bool TryLoad(string path, int expectedSchema, out ChdUpgradeJournalFile journal)
    {
        journal = null;
        try
        {
            if (!File.Exists(path))
                return false;
            XmlSerializer serializer = new XmlSerializer(typeof(ChdUpgradeJournalFile));
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                journal = serializer.Deserialize(stream) as ChdUpgradeJournalFile;
            if (!ValidateJournal(journal, expectedSchema, out _))
                return false;
            return true;
        }
        catch { return false; }
    }

    private static void Save(ChdUpgradeJournalFile journal)
    {
        Save(journal, GetPath());
    }

    private static void Save(ChdUpgradeJournalFile journal, string path)
    {
        if (!ValidateJournal(journal, journal?.Schema ?? 0, out string validationError))
            throw new InvalidDataException("Refusing to save an unsafe CHD recovery journal: " + validationError);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        string backup = path + ".bak";
        if (journal == null || journal.Entries == null || journal.Entries.Count == 0)
        {
            if (File.Exists(temp)) File.Delete(temp);
            if (File.Exists(backup)) File.Delete(backup);
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        XmlSerializer serializer = new XmlSerializer(typeof(ChdUpgradeJournalFile));
        using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            serializer.Serialize(stream, journal);
            stream.Flush(true);
        }
        if (File.Exists(path))
        {
            if (File.Exists(backup))
                File.Delete(backup);
            File.Replace(temp, path, backup, true);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    private static string GetPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();
        return Path.Combine(root, "RomVault", "chd-upgrade-journal-v1.xml");
    }

    private static string GetDevelopmentV2Path()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();
        return Path.Combine(root, "RomVault", "chd-upgrade-journal-v2.xml");
    }

    private static bool HasJournalCandidate(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               (File.Exists(path) || File.Exists(path + ".tmp") || File.Exists(path + ".bak"));
    }

    private static string GetCrossProcessGateName()
    {
        if (Path.DirectorySeparatorChar != '\\')
            return "RomVault.ChdUpgradeRecovery.v1";

        string journalIdentity = Path.GetFullPath(GetPath()).ToUpperInvariant();
        byte[] identityBytes = Encoding.UTF8.GetBytes(journalIdentity);
        byte[] digest;
        using (SHA256 sha256 = SHA256.Create())
            digest = sha256.ComputeHash(identityBytes);
        string token = BitConverter.ToString(digest, 0, 16).Replace("-", string.Empty);
        return @"Global\RomVault.ChdUpgradeRecovery.v1." + token;
    }

    private static bool Delete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            return !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void GetCurrentProcessIdentity(out int processId, out long startUtcTicks)
    {
        processId = 0;
        startUtcTicks = 0;
        try
        {
            using (Process process = Process.GetCurrentProcess())
            {
                processId = process.Id;
                startUtcTicks = process.StartTime.ToUniversalTime().Ticks;
            }
        }
        catch
        {
            processId = 0;
            startUtcTicks = 0;
        }
    }

    private static bool IsOwnerAlive(ChdUpgradeJournalEntry entry)
    {
        if (entry == null || entry.OwnerProcessId <= 0 || entry.OwnerProcessStartUtcTicks <= 0)
            return false;
        try
        {
            using (Process process = Process.GetProcessById(entry.OwnerProcessId))
            {
                if (process.HasExited)
                    return false;
                return process.StartTime.ToUniversalTime().Ticks == entry.OwnerProcessStartUtcTicks;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            // If the OS will not disclose the process start token, avoid
            // mutating a transaction that may still be live.
            return true;
        }
    }

    private static IDisposable AcquireCrossProcessGate()
    {
        bool acquired = false;
        try
        {
            acquired = CrossProcessGate.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }
        if (!acquired)
            throw new IOException("Could not acquire the CHD recovery journal lock.");
        return new MutexLease(CrossProcessGate);
    }

    private sealed class MutexLease : IDisposable
    {
        private Mutex _mutex;

        public MutexLease(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose()
        {
            Mutex mutex = Interlocked.Exchange(ref _mutex, null);
            if (mutex != null)
                mutex.ReleaseMutex();
        }
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (left > long.MaxValue - right)
            return long.MaxValue;
        return left + right;
    }
}
