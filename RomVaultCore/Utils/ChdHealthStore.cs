using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace RomVaultCore.Utils;

public sealed class ChdHealthRecord
{
    public string PathToken { get; set; } = "";
    public string FileName { get; set; } = "";
    public long SourceLength { get; set; }
    public long SourceWriteUtcTicks { get; set; }
    public string Family { get; set; } = "";
    public string Storage { get; set; } = "";
    public string ToolSha256 { get; set; } = "";
    public string ContainerSha1 { get; set; } = "";
    public string RawSha1 { get; set; } = "";
    public string ManifestSha256 { get; set; } = "";
    public string ProfileSha256 { get; set; } = "";
    public string NativeSha256 { get; set; } = "";
    public string ExternalSha256 { get; set; } = "";
    public string Method { get; set; } = "";
    public bool ParityPassed { get; set; }
    public long CheckedUtcTicks { get; set; }
    public long NextDueUtcTicks { get; set; }
    public int ConsecutiveFailures { get; set; }
}

[XmlRoot("ChdHealthDatabase")]
public sealed class ChdHealthDatabase
{
    public int Schema { get; set; } = 2;
    public List<ChdHealthRecord> Records { get; set; } = new List<ChdHealthRecord>();
}

public static class ChdHealthStore
{
    private static readonly object Gate = new object();

    public static bool HasCurrentExternalParity(string chdPath, int maxAgeDays)
    {
        if (maxAgeDays < 1) maxAgeDays = 1;
        try
        {
            FileInfo source = new FileInfo(chdPath);
            string token = Token(chdPath);
            if (!TryFingerprint(chdPath, out ChdHealthFingerprint fingerprint))
                return false;
            lock (Gate)
            {
                ChdHealthRecord record = Load().Records.FirstOrDefault(item => string.Equals(item.PathToken, token, StringComparison.Ordinal));
                return record != null && record.ParityPassed && record.SourceLength == source.Length &&
                       record.SourceWriteUtcTicks == source.LastWriteTimeUtc.Ticks &&
                       string.Equals(record.ContainerSha1, fingerprint.ContainerSha1, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(record.RawSha1, fingerprint.RawSha1, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(record.ManifestSha256, fingerprint.ManifestSha256, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(record.ProfileSha256, fingerprint.ProfileSha256, StringComparison.OrdinalIgnoreCase) &&
                       record.CheckedUtcTicks >= DateTime.UtcNow.AddDays(-maxAgeDays).Ticks;
            }
        }
        catch { return false; }
    }

    public static void RecordParity(string chdPath, string family, string storage, string toolSha256, string nativeSha256, string externalSha256, string method, bool passed)
    {
        if (string.IsNullOrWhiteSpace(chdPath) || !File.Exists(chdPath))
            return;
        try
        {
            FileInfo source = new FileInfo(chdPath);
            if (!TryFingerprint(chdPath, out ChdHealthFingerprint fingerprint))
                return;
            ChdHealthRecord previous;
            lock (Gate)
                previous = Load().Records.FirstOrDefault(item => string.Equals(item.PathToken, Token(chdPath), StringComparison.Ordinal));
            int failures = passed ? 0 : Math.Min(1000000, (previous?.ConsecutiveFailures ?? 0) + 1);
            ChdHealthRecord record = new ChdHealthRecord
            {
                PathToken = Token(chdPath),
                FileName = source.Name,
                SourceLength = source.Length,
                SourceWriteUtcTicks = source.LastWriteTimeUtc.Ticks,
                Family = family ?? "",
                Storage = storage ?? "",
                ToolSha256 = toolSha256 ?? "",
                ContainerSha1 = fingerprint.ContainerSha1,
                RawSha1 = fingerprint.RawSha1,
                ManifestSha256 = fingerprint.ManifestSha256,
                ProfileSha256 = fingerprint.ProfileSha256,
                NativeSha256 = nativeSha256 ?? "",
                ExternalSha256 = externalSha256 ?? "",
                Method = method ?? "",
                ParityPassed = passed,
                CheckedUtcTicks = DateTime.UtcNow.Ticks,
                NextDueUtcTicks = DateTime.UtcNow.AddDays(Math.Max(1, Settings.rvSettings?.ChdExternalParityDays ?? 30)).Ticks,
                ConsecutiveFailures = failures
            };
            lock (Gate)
            {
                ChdHealthDatabase database = Load();
                database.Records.RemoveAll(item => string.Equals(item.PathToken, record.PathToken, StringComparison.Ordinal));
                database.Records.Add(record);
                if (database.Records.Count > 100000)
                    database.Records = database.Records.OrderByDescending(item => item.CheckedUtcTicks).Take(100000).ToList();
                Save(database);
            }
        }
        catch { }
    }

    public static string BuildReport()
    {
        lock (Gate)
        {
            ChdHealthDatabase database = Load();
            int current = database.Records.Count(item => item.ParityPassed);
            int failed = database.Records.Count - current;
            int due = database.Records.Count(item => item.NextDueUtcTicks <= DateTime.UtcNow.Ticks || !item.ParityPassed);
            long oldest = database.Records.Count == 0 ? 0 : database.Records.Min(item => item.CheckedUtcTicks);
            return "CHD health database" + Environment.NewLine +
                   "records=" + database.Records.Count + Environment.NewLine +
                   "parityPassed=" + current + Environment.NewLine +
                   "failedOrIncomplete=" + failed + Environment.NewLine +
                   "due=" + due + Environment.NewLine +
                   "oldestCheckUtc=" + (oldest == 0 ? "" : new DateTime(oldest, DateTimeKind.Utc).ToString("O"));
        }
    }

    internal static ChdHealthRecord FindRecord(string chdPath)
    {
        string token = Token(chdPath);
        lock (Gate)
            return Load().Records.FirstOrDefault(item => string.Equals(item.PathToken, token, StringComparison.Ordinal));
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        try
        {
            string a = Token("C:\\private\\one\\disc.chd");
            string b = Token("C:\\private\\two\\disc.chd");
            if (a == b || a.Length != 32 || a.Contains("private"))
                throw new InvalidDataException("Health database path tokens are not safely content-addressed.");
            string root = Path.Combine(Path.GetTempPath(), "rv-health-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string target = Path.Combine(root, "health.xml");
            SaveToPath(new ChdHealthDatabase(), target);
            if (!File.Exists(target) || File.Exists(target + ".tmp"))
                throw new InvalidDataException("Health database atomic persistence failed.");
            Directory.Delete(root, true);
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    private static ChdHealthDatabase Load()
    {
        try
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                string legacy = GetLegacyPath();
                if (!File.Exists(legacy)) return new ChdHealthDatabase();
                path = legacy;
            }
            XmlSerializer serializer = new XmlSerializer(typeof(ChdHealthDatabase));
            using (FileStream stream = File.OpenRead(path))
                return serializer.Deserialize(stream) as ChdHealthDatabase ?? new ChdHealthDatabase();
        }
        catch { return new ChdHealthDatabase(); }
    }

    private static void Save(ChdHealthDatabase database)
    {
        database.Schema = 2;
        SaveToPath(database, GetPath());
    }

    private static void SaveToPath(ChdHealthDatabase database, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        string backup = path + ".bak";
        XmlSerializer serializer = new XmlSerializer(typeof(ChdHealthDatabase));
        using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            serializer.Serialize(stream, database);
            stream.Flush(true);
        }
        if (!File.Exists(path))
        {
            File.Move(temp, path);
            return;
        }
        try
        {
            File.Replace(temp, path, backup, true);
            if (File.Exists(backup)) File.Delete(backup);
        }
        catch
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            try
            {
                File.Move(temp, path);
                File.Delete(backup);
            }
            catch
            {
                if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
                throw;
            }
        }
    }

    private static string GetPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
        return Path.Combine(root, "RomVault", "chd-health-v2.xml");
    }

    private static string GetLegacyPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
        return Path.Combine(root, "RomVault", "chd-health-v1.xml");
    }

    private static bool TryFingerprint(string path, out ChdHealthFingerprint fingerprint)
    {
        fingerprint = null;
        try
        {
            if (!CHDSharpLib.ChdMetadata.TryReadContainerInfo(path, out CHDSharpLib.ChdContainerInfo container, out _))
                return false;
            fingerprint = new ChdHealthFingerprint
            {
                ContainerSha1 = Hex(container.Sha1),
                RawSha1 = Hex(container.RawSha1),
                ManifestSha256 = MetadataHash(path, ChdReconstructionManifest.MetadataTag, true),
                ProfileSha256 = MetadataHash(path, ChdEncodingProfile.MetadataTag, false)
            };
            return !string.IsNullOrWhiteSpace(fingerprint.ContainerSha1) || !string.IsNullOrWhiteSpace(fingerprint.RawSha1);
        }
        catch { return false; }
    }

    private static string MetadataHash(string path, string tag, bool binary)
    {
        byte[] data;
        if (binary)
        {
            if (!CHDSharpLib.ChdMetadata.TryReadBinaryMetadata(path, tag, 0, out data, out _)) return "";
        }
        else
        {
            if (!CHDSharpLib.ChdMetadata.TryReadTextMetadata(path, tag, 0, out string text, out _)) return "";
            data = Encoding.UTF8.GetBytes(text ?? "");
        }
        using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(data));
    }

    private static string Hex(byte[] data)
    {
        if (data == null || data.Length == 0) return "";
        StringBuilder value = new StringBuilder(data.Length * 2);
        for (int i = 0; i < data.Length; i++) value.Append(data[i].ToString("x2"));
        return value.ToString();
    }

    private static string Token(string path)
    {
        string normalized;
        try { normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant(); }
        catch { normalized = path ?? ""; }
        byte[] data = Encoding.UTF8.GetBytes(normalized);
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(data);
            StringBuilder text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }

    private sealed class ChdHealthFingerprint
    {
        public string ContainerSha1 { get; set; }
        public string RawSha1 { get; set; }
        public string ManifestSha256 { get; set; }
        public string ProfileSha256 { get; set; }
    }
}
