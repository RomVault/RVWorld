using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

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
    public string State { get; set; }
    public long StartedUtcTicks { get; set; }
}

[XmlRoot("ChdUpgradeJournal")]
public sealed class ChdUpgradeJournalFile
{
    public int Schema { get; set; } = 1;
    public List<ChdUpgradeJournalEntry> Entries { get; set; } = new List<ChdUpgradeJournalEntry>();
}

public static class ChdUpgradeRecovery
{
    private static readonly object Gate = new object();

    public static string Begin(string sourcePath, string destinationPath, string stagePath, string finalPath, string backupPath, string manifestPath, string auxiliaryPath = null)
    {
        string id = Guid.NewGuid().ToString("N");
        lock (Gate)
        {
            ChdUpgradeJournalFile journal = Load();
            journal.Entries.Add(new ChdUpgradeJournalEntry
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
                StartedUtcTicks = DateTime.UtcNow.Ticks
            });
            Save(journal);
        }
        return id;
    }

    public static void MarkInstalled(string id)
    {
        UpdateState(id, "InstalledPendingVerification");
    }

    public static void Complete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        lock (Gate)
        {
            ChdUpgradeJournalFile journal = Load();
            journal.Entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
            Save(journal);
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
            string root = Path.GetPathRoot(full);
            freeBytes = new DriveInfo(root).AvailableFreeSpace;
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
            ChdUpgradeJournalFile journal = Load();
            string executable = ChdmanService.FindExecutable();
            List<ChdUpgradeJournalEntry> remaining = new List<ChdUpgradeJournalEntry>();
            for (int i = 0; i < journal.Entries.Count; i++)
            {
                ChdUpgradeJournalEntry entry = journal.Entries[i];
                try
                {
                    if (RecoverEntry(entry, executable, out string message))
                    {
                        if (!string.IsNullOrWhiteSpace(message))
                            messages.Add(message);
                    }
                    else
                    {
                        remaining.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    remaining.Add(entry);
                    messages.Add("CHD recovery deferred for " + ChdDiagnosticFormatter.RedactPath(entry?.DestinationPath) + ": " + ex.Message);
                }
            }
            journal.Entries = remaining;
            Save(journal);
        }
        return string.Join(Environment.NewLine, messages);
    }

    private static bool RecoverEntry(ChdUpgradeJournalEntry entry, string executable, out string message)
    {
        string destinationDirectory = entry == null || string.IsNullOrWhiteSpace(entry.DestinationPath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(entry.DestinationPath) ?? Environment.CurrentDirectory;
        return RecoverEntry(entry, path => Verify(executable, path, destinationDirectory), out message);
    }

    private static bool RecoverEntry(ChdUpgradeJournalEntry entry, Func<string, bool> verifier, out string message)
    {
        message = "";
        if (entry == null || string.IsNullOrWhiteSpace(entry.DestinationPath))
            return true;

        if (File.Exists(entry.DestinationPath) && verifier(entry.DestinationPath))
        {
            Delete(entry.BackupPath);
            Delete(entry.StagePath);
            Delete(entry.FinalPath);
            Delete(entry.ManifestPath);
            Delete(entry.AuxiliaryPath);
            message = "Recovered completed CHD upgrade: " + ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(entry.BackupPath) && File.Exists(entry.BackupPath) && verifier(entry.BackupPath))
        {
            Delete(entry.DestinationPath);
            File.Move(entry.BackupPath, entry.DestinationPath);
            Delete(entry.StagePath);
            Delete(entry.FinalPath);
            Delete(entry.ManifestPath);
            Delete(entry.AuxiliaryPath);
            message = "Restored CHD backup after an interrupted upgrade: " + ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return true;
        }

        if (!File.Exists(entry.DestinationPath) && !string.IsNullOrWhiteSpace(entry.FinalPath) && File.Exists(entry.FinalPath) && verifier(entry.FinalPath))
        {
            File.Move(entry.FinalPath, entry.DestinationPath);
            Delete(entry.StagePath);
            Delete(entry.ManifestPath);
            Delete(entry.AuxiliaryPath);
            message = "Installed verified CHD output after an interrupted upgrade: " + ChdDiagnosticFormatter.RedactPath(entry.DestinationPath);
            return true;
        }

        return false;
    }

    public static bool RunFaultRecoverySelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-recovery-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Func<string, bool> valid = path => File.Exists(path) && File.ReadAllText(path).StartsWith("VALID:", StringComparison.Ordinal);

            // File.Replace completed: the verified destination wins and the
            // old backup is retired.
            ChdUpgradeJournalEntry replaced = CreateTestEntry(root, "replace");
            File.WriteAllText(replaced.DestinationPath, "VALID:new");
            File.WriteAllText(replaced.BackupPath, "VALID:old");
            if (!RecoverEntry(replaced, valid, out _) || File.Exists(replaced.BackupPath) || File.ReadAllText(replaced.DestinationPath) != "VALID:new")
                throw new InvalidDataException("Completed replacement recovery failed.");

            // Fallback move interrupted after preserving the original.
            ChdUpgradeJournalEntry backedUp = CreateTestEntry(root, "backup");
            File.WriteAllText(backedUp.BackupPath, "VALID:old");
            File.WriteAllText(backedUp.FinalPath, "VALID:new");
            if (!RecoverEntry(backedUp, valid, out _) || File.ReadAllText(backedUp.DestinationPath) != "VALID:old")
                throw new InvalidDataException("Backup restoration recovery failed.");

            // A brand-new output interrupted before installation can safely
            // install the already verified final artifact.
            ChdUpgradeJournalEntry fresh = CreateTestEntry(root, "fresh");
            File.WriteAllText(fresh.FinalPath, "VALID:new");
            if (!RecoverEntry(fresh, valid, out _) || File.ReadAllText(fresh.DestinationPath) != "VALID:new")
                throw new InvalidDataException("Final artifact installation recovery failed.");

            // A corrupt installed candidate must never displace a preserved
            // valid original.
            ChdUpgradeJournalEntry corrupt = CreateTestEntry(root, "corrupt");
            File.WriteAllText(corrupt.DestinationPath, "INVALID:new");
            File.WriteAllText(corrupt.BackupPath, "VALID:old");
            if (!RecoverEntry(corrupt, valid, out _) || File.ReadAllText(corrupt.DestinationPath) != "VALID:old")
                throw new InvalidDataException("Corrupt candidate rollback failed.");

            ChdUpgradeJournalEntry unavailable = CreateTestEntry(root, "unavailable");
            if (RecoverEntry(unavailable, valid, out _))
                throw new InvalidDataException("Recovery accepted an unverifiable transaction.");

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
        return new ChdUpgradeJournalEntry
        {
            Id = name,
            DestinationPath = Path.Combine(root, name + ".chd"),
            StagePath = Path.Combine(root, name + ".stage"),
            FinalPath = Path.Combine(root, name + ".final"),
            BackupPath = Path.Combine(root, name + ".backup"),
            ManifestPath = Path.Combine(root, name + ".manifest")
        };
    }

    private static bool Verify(string executable, string path, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(path))
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
            ChdUpgradeJournalFile journal = Load();
            ChdUpgradeJournalEntry entry = journal.Entries.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (entry != null)
            {
                entry.State = state;
                Save(journal);
            }
        }
    }

    private static ChdUpgradeJournalFile Load()
    {
        try
        {
            string path = GetPath();
            if (!File.Exists(path))
                return new ChdUpgradeJournalFile();
            XmlSerializer serializer = new XmlSerializer(typeof(ChdUpgradeJournalFile));
            using (FileStream stream = File.OpenRead(path))
                return serializer.Deserialize(stream) as ChdUpgradeJournalFile ?? new ChdUpgradeJournalFile();
        }
        catch
        {
            return new ChdUpgradeJournalFile();
        }
    }

    private static void Save(ChdUpgradeJournalFile journal)
    {
        string path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (journal == null || journal.Entries == null || journal.Entries.Count == 0)
        {
            Delete(path);
            Delete(path + ".tmp");
            return;
        }
        string temp = path + ".tmp";
        XmlSerializer serializer = new XmlSerializer(typeof(ChdUpgradeJournalFile));
        using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            serializer.Serialize(stream, journal);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }

    private static string GetPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();
        return Path.Combine(root, "RomVault", "chd-upgrade-journal-v1.xml");
    }

    private static void Delete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (left > long.MaxValue - right)
            return long.MaxValue;
        return left + right;
    }
}
