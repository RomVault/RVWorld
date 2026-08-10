using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RomVaultCore.Utils;

public sealed class ChdToolchainInfo
{
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Pinned { get; set; }
    public string Capabilities { get; set; } = "";
    public string Error { get; set; } = "";
}

internal static class ChdToolchainRegistry
{
    public static List<ChdToolchainInfo> Discover(bool fullProbe)
    {
        List<ChdToolchainInfo> result = new List<ChdToolchainInfo>();
        string pinned = RomVaultCore.Settings.rvSettings?.ChdPinnedToolSha256 ?? "";
        List<string> candidates = ChdmanService.FindExecutableCandidates();
        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            try
            {
                if (!File.Exists(candidate))
                    continue;
                string full = Path.GetFullPath(candidate);
                if (result.Any(item => string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (ChdmanService.TryGetIdentity(full, fullProbe ? ChdmanProbeLevel.Full : ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out string error))
                {
                    result.Add(new ChdToolchainInfo
                    {
                        Path = full,
                        Version = identity.VersionText,
                        Sha256 = identity.BinarySha256,
                        Pinned = !string.IsNullOrWhiteSpace(pinned) && string.Equals(pinned, identity.BinarySha256, StringComparison.OrdinalIgnoreCase),
                        Capabilities = identity.Capabilities?.Fingerprint ?? ""
                    });
                }
                else
                {
                    result.Add(new ChdToolchainInfo { Path = full, Error = error ?? "Could not identify chdman." });
                }
            }
            catch (Exception ex)
            {
                result.Add(new ChdToolchainInfo { Path = candidate, Error = ex.Message });
            }
        }
        return result
            .OrderByDescending(item => item.Pinned)
            .ThenByDescending(item => ParseVersion(item.Version))
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string SelectDefault()
    {
        ChdToolchainInfo selected = Discover(false).FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Error));
        return selected?.Path ?? ChdmanService.FindExecutable();
    }

    public static string Select(string family, ChdStorageProfile storage, string dialect, out string error)
    {
        error = "";
        string pinned = RomVaultCore.Settings.rvSettings?.ChdPinnedToolSha256 ?? "";
        List<ChdmanIdentity> valid = new List<ChdmanIdentity>();
        List<string> failures = new List<string>();
        foreach (string candidate in ChdmanService.FindExecutableCandidates())
        {
            if (!File.Exists(candidate))
                continue;
            if (!ChdmanService.TryGetIdentity(candidate, ChdmanProbeLevel.Full, out ChdmanIdentity identity, out string identityError))
            {
                failures.Add(ChdDiagnosticFormatter.RedactPath(candidate) + ": " + identityError);
                continue;
            }
            bool capable = identity.Capabilities?.CanWriteProfile(family, storage) == true;
            if (capable && !string.IsNullOrWhiteSpace(dialect))
                capable = identity.Capabilities.CanWriteDialect(dialect, storage);
            if (capable)
                valid.Add(identity);
        }

        ChdmanIdentity selected = valid
            .OrderByDescending(item => !string.IsNullOrWhiteSpace(pinned) && string.Equals(pinned, item.BinarySha256, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Capabilities?.WriterRevision(family) ?? 0)
            .ThenByDescending(item => item.Version)
            .FirstOrDefault();
        if (selected != null)
            return selected.ExecutablePath;
        error = "No discovered chdman passed the exact " + family + " " + storage.ToString().ToLowerInvariant() +
                (string.IsNullOrWhiteSpace(dialect) ? "" : " " + dialect) + " fixture." +
                (failures.Count == 0 ? "" : " " + string.Join(" | ", failures));
        return "";
    }

    public static string BuildReport(bool fullProbe)
    {
        List<ChdToolchainInfo> tools = Discover(fullProbe);
        StringBuilder report = new StringBuilder();
        report.AppendLine("RomVault chdman toolchains");
        report.AppendLine("selection=pinned SHA-256, then highest validated writer revision/version");
        if (tools.Count == 0)
        {
            report.AppendLine("No chdman binaries were discovered.");
            return report.ToString().TrimEnd();
        }
        for (int i = 0; i < tools.Count; i++)
        {
            ChdToolchainInfo tool = tools[i];
            report.AppendLine((tool.Pinned ? "PINNED " : "") + ChdDiagnosticFormatter.RedactPath(tool.Path));
            if (!string.IsNullOrWhiteSpace(tool.Error))
                report.AppendLine("  error=" + tool.Error);
            else
            {
                report.AppendLine("  version=" + tool.Version);
                report.AppendLine("  sha256=" + tool.Sha256);
                if (fullProbe)
                    report.AppendLine("  capabilities=" + tool.Capabilities);
            }
        }
        return report.ToString().TrimEnd();
    }

    private static Version ParseVersion(string value)
    {
        if (System.Version.TryParse(value, out Version version))
            return version;
        return new Version(0, 0);
    }
}
