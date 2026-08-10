using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace RomVaultCore.Utils;

internal enum ChdmanProbeLevel
{
    Banner,
    Full
}

internal sealed class ChdmanRunResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; } = -1;
    public bool Cancelled { get; set; }
    public bool TimedOut { get; set; }
    public string StandardOutput { get; set; } = "";
    public string StandardError { get; set; } = "";
    public string Output => (StandardOutput + Environment.NewLine + StandardError).Trim();
}

public sealed class ChdmanCapabilities
{
    public int ProbeSchema { get; set; } = 9;
    public bool SupportsCopy { get; set; }
    public bool SupportsMetadataNoChecksum { get; set; }
    public bool SupportsSplitBin { get; set; }
    public bool SupportsAddPregap { get; set; }
    public bool SupportsRemovePregap { get; set; }
    public bool CdRoundTrip { get; set; }
    public bool GdiRoundTrip { get; set; }
    public bool RedumpGdiRoundTrip { get; set; }
    public bool DvdRoundTrip { get; set; }
    public bool PspRoundTrip { get; set; }
    public bool CdPlaybackRoundTrip { get; set; }
    public bool CdArchiveRoundTrip { get; set; }
    public bool GdiPlaybackRoundTrip { get; set; }
    public bool GdiArchiveRoundTrip { get; set; }
    public bool DvdPlaybackRoundTrip { get; set; }
    public bool DvdArchiveRoundTrip { get; set; }
    public bool PspPlaybackRoundTrip { get; set; }
    public bool PspArchiveRoundTrip { get; set; }
    public bool RawPlaybackRoundTrip { get; set; }
    public bool RawArchiveRoundTrip { get; set; }
    public bool HddPlaybackRoundTrip { get; set; }
    public bool HddArchiveRoundTrip { get; set; }
    public bool LaserDiscPlaybackRoundTrip { get; set; }
    public bool LaserDiscArchiveRoundTrip { get; set; }
    public string ProbeError { get; set; } = "";
    public List<ChdDialectCapability> Dialects { get; set; } = new List<ChdDialectCapability>();

    [XmlIgnore]
    public string Fingerprint =>
        $"p{ProbeSchema}:copy={(SupportsCopy ? 1 : 0)};nocs={(SupportsMetadataNoChecksum ? 1 : 0)};sb={(SupportsSplitBin ? 1 : 0)};ap={(SupportsAddPregap ? 1 : 0)};rp={(SupportsRemovePregap ? 1 : 0)};cdp={(CdPlaybackRoundTrip ? 1 : 0)};cda={(CdArchiveRoundTrip ? 1 : 0)};gdip={(GdiPlaybackRoundTrip ? 1 : 0)};gdia={(GdiArchiveRoundTrip ? 1 : 0)};redump={(RedumpGdiRoundTrip ? 1 : 0)};dvdp={(DvdPlaybackRoundTrip ? 1 : 0)};dvda={(DvdArchiveRoundTrip ? 1 : 0)};pspp={(PspPlaybackRoundTrip ? 1 : 0)};pspa={(PspArchiveRoundTrip ? 1 : 0)};rawp={(RawPlaybackRoundTrip ? 1 : 0)};rawa={(RawArchiveRoundTrip ? 1 : 0)};hddp={(HddPlaybackRoundTrip ? 1 : 0)};hdda={(HddArchiveRoundTrip ? 1 : 0)};ldp={(LaserDiscPlaybackRoundTrip ? 1 : 0)};lda={(LaserDiscArchiveRoundTrip ? 1 : 0)};dialects={DialectFingerprint()}";

    public bool CanWriteDialect(string dialect, ChdStorageProfile storageProfile)
    {
        ChdDialectCapability item = (Dialects ?? new List<ChdDialectCapability>())
            .FirstOrDefault(value => string.Equals(value?.Id, dialect, StringComparison.OrdinalIgnoreCase));
        return item != null && (storageProfile == ChdStorageProfile.Playback ? item.Playback : item.Archive);
    }

    private string DialectFingerprint()
    {
        return string.Join(",", (Dialects ?? new List<ChdDialectCapability>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id))
            .OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase)
            .Select(value => value.Id + ":" + (value.Playback ? "p" : "-") + (value.Archive ? "a" : "-")));
    }

    public bool CanWriteProfile(string family, ChdStorageProfile storageProfile)
    {
        bool playback = storageProfile == ChdStorageProfile.Playback;
        switch ((family ?? "").Trim().ToLowerInvariant())
        {
            case "cd": return playback ? CdPlaybackRoundTrip : CdArchiveRoundTrip;
            case "gdi": return playback ? GdiPlaybackRoundTrip : GdiArchiveRoundTrip;
            case "psp": return playback ? PspPlaybackRoundTrip : PspArchiveRoundTrip;
            case "dvd": return playback ? DvdPlaybackRoundTrip : DvdArchiveRoundTrip;
            case "raw": return playback ? RawPlaybackRoundTrip : RawArchiveRoundTrip;
            case "hdd": return playback ? HddPlaybackRoundTrip : HddArchiveRoundTrip;
            case "laserdisc": return playback ? LaserDiscPlaybackRoundTrip : LaserDiscArchiveRoundTrip;
            default: return false;
        }
    }

    public bool CanWriteFamily(string family)
    {
        switch ((family ?? "").Trim().ToLowerInvariant())
        {
            case "cd": return CdRoundTrip;
            case "gdi": return GdiRoundTrip;
            case "psp": return PspRoundTrip;
            case "dvd": return DvdRoundTrip;
            case "raw": return RawPlaybackRoundTrip && RawArchiveRoundTrip;
            case "hdd": return HddPlaybackRoundTrip && HddArchiveRoundTrip;
            case "laserdisc": return LaserDiscPlaybackRoundTrip && LaserDiscArchiveRoundTrip;
            default: return false;
        }
    }

    public int WriterRevision(string family)
    {
        string normalized = (family ?? "").Trim().ToLowerInvariant();
        if (!CanWriteProfile(normalized, ChdStorageProfile.Playback) &&
            !CanWriteProfile(normalized, ChdStorageProfile.Archive))
            return 0;
        if (normalized == "gdi")
            return RedumpGdiRoundTrip ? 2 : 1;
        return 1;
    }
}

public sealed class ChdDialectCapability
{
    public string Id { get; set; } = "";
    public bool Playback { get; set; }
    public bool Archive { get; set; }
    public string Error { get; set; } = "";
}

internal sealed class ChdmanIdentity
{
    public string ExecutablePath { get; set; } = "";
    public string Banner { get; set; } = "";
    public string VersionText { get; set; } = "";
    public Version Version { get; set; }
    public string BinarySha256 { get; set; } = "";
    public long FileLength { get; set; }
    public long LastWriteUtcTicks { get; set; }
    public ChdmanCapabilities Capabilities { get; set; }

    public string EncoderIdentity => string.IsNullOrWhiteSpace(BinarySha256)
        ? VersionText
        : VersionText + ":" + BinarySha256;
}

public sealed class ChdmanCapabilityCacheEntry
{
    public string BinarySha256 { get; set; }
    public string VersionText { get; set; }
    public string Banner { get; set; }
    public ChdmanCapabilities Capabilities { get; set; }
}

[XmlRoot("ChdmanCapabilityCache")]
public sealed class ChdmanCapabilityCacheFile
{
    public int Schema { get; set; } = 1;
    public List<ChdmanCapabilityCacheEntry> Entries { get; set; } = new List<ChdmanCapabilityCacheEntry>();
}

internal static class ChdmanService
{
    private const int CapabilityProbeSchema = 9;
    private static readonly object IdentityGate = new object();
    private static readonly Dictionary<string, ChdmanIdentity> IdentityCache = new Dictionary<string, ChdmanIdentity>(StringComparer.OrdinalIgnoreCase);

    public static string FindExecutable()
    {
        List<string> candidates = FindExecutableCandidates();
        for (int i = 0; i < candidates.Count; i++)
        {
            try
            {
                if (File.Exists(candidates[i]))
                    return Path.GetFullPath(candidates[i]);
            }
            catch
            {
            }
        }
        return "chdman.exe";
    }

    internal static List<string> FindExecutableCandidates()
    {
        List<string> candidates = new List<string>();
        try
        {
            List<string> configured = RomVaultCore.Settings.rvSettings?.ChdmanPaths;
            if (configured != null)
            {
                for (int i = 0; i < configured.Count; i++)
                {
                    string item = configured[i];
                    if (string.IsNullOrWhiteSpace(item))
                        continue;
                    candidates.Add(Directory.Exists(item) ? Path.Combine(item, "chdman.exe") : item);
                }
            }
        }
        catch { }
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                candidates.Add(Path.Combine(baseDir, "chdman.exe"));
                candidates.Add(Path.Combine(baseDir, "tools", "chdman.exe"));
            }
        }
        catch
        {
        }

        try
        {
            string currentDir = Environment.CurrentDirectory;
            if (!string.IsNullOrWhiteSpace(currentDir))
                candidates.Add(Path.Combine(currentDir, "chdman.exe"));
        }
        catch
        {
        }

        try
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(path))
            {
                foreach (string directory in path.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = directory.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        candidates.Add(Path.Combine(trimmed, "chdman.exe"));
                }
            }
        }
        catch
        {
        }

        return candidates
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ChdmanRunResult Run(
        string executable,
        string arguments,
        string workingDirectory,
        int timeoutMilliseconds = 0,
        Func<bool> cancellationPending = null,
        Action<string> errorLine = null)
    {
        ChdmanRunResult result = new ChdmanRunResult();
        try
        {
            if (string.IsNullOrWhiteSpace(executable))
                executable = FindExecutable();
            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
                workingDirectory = Environment.CurrentDirectory;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments ?? "",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                StringBuilder stdout = new StringBuilder();
                StringBuilder stderr = new StringBuilder();
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data))
                        return;
                    stderr.AppendLine(e.Data);
                    try { errorLine?.Invoke(e.Data); } catch { }
                };

                process.Start();
                ChdmanProcessTracker.Register(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Stopwatch elapsed = Stopwatch.StartNew();
                while (!process.WaitForExit(200))
                {
                    if (cancellationPending != null && cancellationPending())
                    {
                        result.Cancelled = true;
                        ChdmanProcessTracker.Kill(process);
                        break;
                    }
                    if (timeoutMilliseconds > 0 && elapsed.ElapsedMilliseconds >= timeoutMilliseconds)
                    {
                        result.TimedOut = true;
                        ChdmanProcessTracker.Kill(process);
                        break;
                    }
                }

                if (!result.Cancelled && !result.TimedOut)
                {
                    process.WaitForExit();
                    result.ExitCode = process.ExitCode;
                    result.Success = process.ExitCode == 0;
                }
                result.StandardOutput = stdout.ToString().Trim();
                result.StandardError = stderr.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            result.StandardError = ex.Message;
        }
        return result;
    }

    /// <summary>
    /// Validates paths passed to external chdman builds on Windows.  RomVault
    /// deliberately stays within the traditional Win32 limit because a child
    /// executable must opt in to long paths independently of the host process.
    /// </summary>
    internal static bool TryValidateExternalPaths(out string error, params string[] paths)
    {
        error = "";
        if (Path.DirectorySeparatorChar != '\\' || paths == null)
            return true;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrWhiteSpace(path))
                continue;
            try
            {
                string full = Path.GetFullPath(path);
                if (full.Length < 260)
                    continue;
                error = $"A CHD operation path is {full.Length} characters long, but compatible Windows chdman paths must be shorter than 260 characters. " +
                        "Shorten the RomRoot or mapped directory path and try again.";
                return false;
            }
            catch (Exception ex)
            {
                error = "Could not validate a CHD operation path: " + ex.Message;
                return false;
            }
        }
        return true;
    }

    public static bool TryGetIdentity(string executable, ChdmanProbeLevel probeLevel, out ChdmanIdentity identity, out string error)
    {
        identity = null;
        error = "";
        if (string.IsNullOrWhiteSpace(executable))
            executable = FindExecutable();

        string resolved = executable;
        long length = 0;
        long timestamp = 0;
        string binarySha256 = "";
        try
        {
            if (File.Exists(executable))
            {
                resolved = Path.GetFullPath(executable);
                FileInfo fi = new FileInfo(resolved);
                length = fi.Length;
                timestamp = fi.LastWriteTimeUtc.Ticks;
                binarySha256 = HashFileSha256(resolved);
            }
        }
        catch (Exception ex)
        {
            error = "Could not identify chdman.exe: " + ex.Message;
            return false;
        }

        string key = resolved + "|" + length + "|" + timestamp + "|" + binarySha256 + "|" + (int)probeLevel;
        lock (IdentityGate)
        {
            if (IdentityCache.TryGetValue(key, out ChdmanIdentity cached))
            {
                identity = cached;
                return true;
            }
        }

        ChdmanRunResult bannerResult = Run(resolved, "", ResolveWorkingDirectory(resolved), 10000);
        string bannerOutput = bannerResult.Output;
        Match match = Regex.Match(bannerOutput ?? "", @"CHD\)?\s+manager\s+([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out Version parsed))
        {
            error = string.IsNullOrWhiteSpace(bannerOutput)
                ? "chdman did not report a version."
                : "Could not parse the chdman version from: " + bannerOutput;
            return false;
        }

        ChdmanIdentity found = new ChdmanIdentity
        {
            ExecutablePath = resolved,
            Banner = FirstBannerLine(bannerOutput),
            VersionText = match.Groups[1].Value,
            Version = parsed,
            BinarySha256 = binarySha256,
            FileLength = length,
            LastWriteUtcTicks = timestamp
        };

        if (probeLevel == ChdmanProbeLevel.Full)
        {
            if (!TryGetCachedCapabilities(found, out ChdmanCapabilities capabilities))
            {
                capabilities = ProbeCapabilities(found);
                SaveCachedCapabilities(found, capabilities);
            }
            found.Capabilities = capabilities;
        }

        lock (IdentityGate)
            IdentityCache[key] = found;
        identity = found;
        return true;
    }

    public static string Quote(string value)
    {
        return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
    }

    public static long? TryGetLogicalSize(string executable, string chdPath, string workingDirectory)
    {
        if (!TryValidateExternalPaths(out _, chdPath, workingDirectory))
            return null;
        ChdmanRunResult result = Run(executable, "info -i " + Quote(chdPath), workingDirectory, 30000);
        if (!result.Success)
            return null;
        Match match = Regex.Match(result.Output ?? "", @"Logical\s+size:\s*([0-9,._ ]+)\s+bytes", RegexOptions.IgnoreCase);
        if (!match.Success)
            match = Regex.Match(result.Output ?? "", @"\(([0-9]+)\s+bytes\)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;
        string digits = Regex.Replace(match.Groups[1].Value, "[^0-9]", "");
        return long.TryParse(digits, out long value) ? value : (long?)null;
    }

    public static void ClearCapabilityCache()
    {
        lock (IdentityGate)
            IdentityCache.Clear();
        try
        {
            string path = GetCapabilityCachePath();
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static ChdmanCapabilities ProbeCapabilities(ChdmanIdentity identity)
    {
        ChdmanCapabilities capabilities = new ChdmanCapabilities { ProbeSchema = CapabilityProbeSchema };
        string work = null;
        try
        {
            ChdmanRunResult help = Run(identity.ExecutablePath, "help", ResolveWorkingDirectory(identity.ExecutablePath), 10000);
            ChdmanRunResult extractHelp = Run(identity.ExecutablePath, "help extractcd", ResolveWorkingDirectory(identity.ExecutablePath), 10000);
            ChdmanRunResult createHelp = Run(identity.ExecutablePath, "help createcd", ResolveWorkingDirectory(identity.ExecutablePath), 10000);
            string helpText = (identity.Banner + Environment.NewLine + help.Output + Environment.NewLine + extractHelp.Output + Environment.NewLine + createHelp.Output).ToLowerInvariant();
            capabilities.SupportsCopy = helpText.Contains("copy") || identity.Version >= new Version(0, 238);
            capabilities.SupportsMetadataNoChecksum = helpText.Contains("nochecksum") || helpText.Contains("-nocs") || identity.Version >= new Version(0, 238);
            capabilities.SupportsSplitBin = helpText.Contains("splitbin") || helpText.Contains("-sb") || identity.Version >= new Version(0, 265);
            capabilities.SupportsAddPregap = helpText.Contains("addpregap") || helpText.Contains("-ap");
            capabilities.SupportsRemovePregap = helpText.Contains("removepregap") || helpText.Contains("-rp");

            work = Path.Combine(Path.GetTempPath(), "RomVault-chdprobe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            capabilities.CdPlaybackRoundTrip = ProbeCd(identity, work, false, ChdStorageProfile.Playback, out string cdPlaybackError);
            capabilities.CdArchiveRoundTrip = ProbeCd(identity, work, false, ChdStorageProfile.Archive, out string cdArchiveError);
            capabilities.GdiPlaybackRoundTrip = ProbeCd(identity, work, true, ChdStorageProfile.Playback, out string gdiPlaybackError);
            capabilities.GdiArchiveRoundTrip = ProbeCd(identity, work, true, ChdStorageProfile.Archive, out string gdiArchiveError);
            capabilities.CdRoundTrip = capabilities.CdPlaybackRoundTrip && capabilities.CdArchiveRoundTrip;
            capabilities.GdiRoundTrip = capabilities.GdiPlaybackRoundTrip && capabilities.GdiArchiveRoundTrip;
            string redumpError = "";
            capabilities.RedumpGdiRoundTrip = capabilities.GdiRoundTrip && ProbeRedumpGdi(identity, work, out redumpError);
            capabilities.DvdPlaybackRoundTrip = ProbeDvd(identity, work, "dvd", ChdStorageProfile.Playback, out string dvdPlaybackError);
            capabilities.DvdArchiveRoundTrip = ProbeDvd(identity, work, "dvd", ChdStorageProfile.Archive, out string dvdArchiveError);
            capabilities.PspPlaybackRoundTrip = ProbeDvd(identity, work, "psp", ChdStorageProfile.Playback, out string pspPlaybackError);
            capabilities.PspArchiveRoundTrip = ProbeDvd(identity, work, "psp", ChdStorageProfile.Archive, out string pspArchiveError);
            capabilities.DvdRoundTrip = capabilities.DvdPlaybackRoundTrip && capabilities.DvdArchiveRoundTrip;
            capabilities.PspRoundTrip = capabilities.PspPlaybackRoundTrip && capabilities.PspArchiveRoundTrip;
            capabilities.RawPlaybackRoundTrip = ProbeSingleFile(identity, work, "raw", ChdStorageProfile.Playback, out string rawPlaybackError);
            capabilities.RawArchiveRoundTrip = ProbeSingleFile(identity, work, "raw", ChdStorageProfile.Archive, out string rawArchiveError);
            capabilities.HddPlaybackRoundTrip = ProbeSingleFile(identity, work, "hdd", ChdStorageProfile.Playback, out string hddPlaybackError);
            capabilities.HddArchiveRoundTrip = ProbeSingleFile(identity, work, "hdd", ChdStorageProfile.Archive, out string hddArchiveError);
            capabilities.LaserDiscPlaybackRoundTrip = ProbeLaserDisc(identity, work, ChdStorageProfile.Playback, out string ldPlaybackError);
            capabilities.LaserDiscArchiveRoundTrip = ProbeLaserDisc(identity, work, ChdStorageProfile.Archive, out string ldArchiveError);
            bool mode12048Playback = ProbeCueDialect(identity, work, "cue-mode1-2048", "MODE1/2048", 2048, ChdStorageProfile.Playback, out string mode12048PlaybackError);
            bool mode12048Archive = ProbeCueDialect(identity, work, "cue-mode1-2048", "MODE1/2048", 2048, ChdStorageProfile.Archive, out string mode12048ArchiveError);
            bool mode22352Playback = ProbeCueDialect(identity, work, "cue-mode2-2352", "MODE2/2352", 2352, ChdStorageProfile.Playback, out string mode22352PlaybackError);
            bool mode22352Archive = ProbeCueDialect(identity, work, "cue-mode2-2352", "MODE2/2352", 2352, ChdStorageProfile.Archive, out string mode22352ArchiveError);
            bool tocPlayback = ProbeTocDialect(identity, work, ChdStorageProfile.Playback, out string tocPlaybackError);
            bool tocArchive = ProbeTocDialect(identity, work, ChdStorageProfile.Archive, out string tocArchiveError);
            bool complexCuePlayback = ProbeComplexCueDialect(identity, work, ChdStorageProfile.Playback, out string complexCuePlaybackError);
            bool complexCueArchive = ProbeComplexCueDialect(identity, work, ChdStorageProfile.Archive, out string complexCueArchiveError);
            bool unicodeCuePlayback = ProbeUnicodeCueDialect(identity, work, ChdStorageProfile.Playback, out string unicodeCuePlaybackError);
            bool unicodeCueArchive = ProbeUnicodeCueDialect(identity, work, ChdStorageProfile.Archive, out string unicodeCueArchiveError);

            AddDialect(capabilities, "cue-mode1-2352-audio", capabilities.CdPlaybackRoundTrip, capabilities.CdArchiveRoundTrip, cdPlaybackError, cdArchiveError);
            AddDialect(capabilities, "cue-mode1-2352", capabilities.CdPlaybackRoundTrip, capabilities.CdArchiveRoundTrip, cdPlaybackError, cdArchiveError);
            AddDialect(capabilities, "cue-audio", capabilities.CdPlaybackRoundTrip, capabilities.CdArchiveRoundTrip, cdPlaybackError, cdArchiveError);
            AddDialect(capabilities, "cue-mode1-2048", mode12048Playback, mode12048Archive, mode12048PlaybackError, mode12048ArchiveError);
            AddDialect(capabilities, "cue-mode1-2048-audio", mode12048Playback && capabilities.CdPlaybackRoundTrip, mode12048Archive && capabilities.CdArchiveRoundTrip, mode12048PlaybackError, mode12048ArchiveError);
            AddDialect(capabilities, "cue-mode2-2352", mode22352Playback, mode22352Archive, mode22352PlaybackError, mode22352ArchiveError);
            AddDialect(capabilities, "cue-mode2-2352-audio", mode22352Playback && capabilities.CdPlaybackRoundTrip, mode22352Archive && capabilities.CdArchiveRoundTrip, mode22352PlaybackError, mode22352ArchiveError);
            AddDialect(capabilities, "cue-mixed", mode12048Playback && mode22352Playback && capabilities.CdPlaybackRoundTrip, mode12048Archive && mode22352Archive && capabilities.CdArchiveRoundTrip, mode12048PlaybackError, mode22352ArchiveError);
            AddDialect(capabilities, "toc-exact", tocPlayback, tocArchive, tocPlaybackError, tocArchiveError);
            AddDialect(capabilities, "cue-complex-layout", complexCuePlayback, complexCueArchive, complexCuePlaybackError, complexCueArchiveError);
            AddDialect(capabilities, "cue-unicode", unicodeCuePlayback, unicodeCueArchive, unicodeCuePlaybackError, unicodeCueArchiveError);
            AddDialect(capabilities, "tosec-gdi", capabilities.GdiPlaybackRoundTrip, capabilities.GdiArchiveRoundTrip, gdiPlaybackError, gdiArchiveError);
            AddDialect(capabilities, "redump-gdrom-cue", capabilities.RedumpGdiRoundTrip, capabilities.RedumpGdiRoundTrip, redumpError, redumpError);
            AddDialect(capabilities, "iso", capabilities.DvdPlaybackRoundTrip, capabilities.DvdArchiveRoundTrip, dvdPlaybackError, dvdArchiveError);
            AddDialect(capabilities, "psp-iso", capabilities.PspPlaybackRoundTrip, capabilities.PspArchiveRoundTrip, pspPlaybackError, pspArchiveError);
            AddDialect(capabilities, "raw-exact", capabilities.RawPlaybackRoundTrip, capabilities.RawArchiveRoundTrip, rawPlaybackError, rawArchiveError);
            AddDialect(capabilities, "hard-disk-exact", capabilities.HddPlaybackRoundTrip, capabilities.HddArchiveRoundTrip, hddPlaybackError, hddArchiveError);
            AddDialect(capabilities, "canonical-avi-exact", capabilities.LaserDiscPlaybackRoundTrip, capabilities.LaserDiscArchiveRoundTrip, ldPlaybackError, ldArchiveError);

            capabilities.ProbeError = string.Join(" | ", new[] { cdPlaybackError, cdArchiveError, gdiPlaybackError, gdiArchiveError, redumpError, dvdPlaybackError, dvdArchiveError, pspPlaybackError, pspArchiveError, rawPlaybackError, rawArchiveError, hddPlaybackError, hddArchiveError, ldPlaybackError, ldArchiveError, mode12048PlaybackError, mode12048ArchiveError, mode22352PlaybackError, mode22352ArchiveError, tocPlaybackError, tocArchiveError, complexCuePlaybackError, complexCueArchiveError, unicodeCuePlaybackError, unicodeCueArchiveError }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch (Exception ex)
        {
            capabilities.ProbeError = ex.Message;
        }
        finally
        {
            TryDeleteDirectory(work);
        }
        return capabilities;
    }

    private static void AddDialect(ChdmanCapabilities capabilities, string id, bool playback, bool archive, string playbackError, string archiveError)
    {
        capabilities.Dialects.Add(new ChdDialectCapability
        {
            Id = id,
            Playback = playback,
            Archive = archive,
            Error = string.Join(" | ", new[] { playbackError, archiveError }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
        });
    }

    private static bool ProbeCd(ChdmanIdentity identity, string root, bool gdi, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string executable = identity.ExecutablePath;
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, (gdi ? "gdi-" : "cd-") + storage);
        Directory.CreateDirectory(dir);
        List<string> inputs = new List<string>();
        string descriptor;
        if (gdi)
        {
            string track1 = Path.Combine(dir, "track01.bin");
            string track2 = Path.Combine(dir, "track02.raw");
            string track3 = Path.Combine(dir, "track03.bin");
            WriteFixture(track1, 450 * 2352, 11);
            WriteFixture(track2, 450 * 2352, 37);
            WriteFixture(track3, 32 * 2352, 73);
            inputs.Add(track1);
            inputs.Add(track2);
            inputs.Add(track3);
            descriptor = Path.Combine(dir, "probe.gdi");
            File.WriteAllText(descriptor,
                "3" + Environment.NewLine +
                "1 0 4 2352 track01.bin 0" + Environment.NewLine +
                "2 450 0 2352 track02.raw 0" + Environment.NewLine +
                "3 45000 4 2352 track03.bin 0" + Environment.NewLine,
                new UTF8Encoding(false));
        }
        else
        {
            string track1 = Path.Combine(dir, "track01.bin");
            string track2 = Path.Combine(dir, "track02.bin");
            WriteFixture(track1, 512 * 2352, 19);
            WriteFixture(track2, 128 * 2352, 53);
            inputs.Add(track1);
            inputs.Add(track2);
            descriptor = Path.Combine(dir, "probe.cue");
            File.WriteAllText(descriptor,
                "FILE \"track01.bin\" BINARY" + Environment.NewLine +
                "  TRACK 01 MODE1/2352" + Environment.NewLine +
                "    INDEX 01 00:00:00" + Environment.NewLine +
                "FILE \"track02.bin\" BINARY" + Environment.NewLine +
                "  TRACK 02 AUDIO" + Environment.NewLine +
                "    INDEX 01 00:00:00" + Environment.NewLine,
                new UTF8Encoding(false));
        }

        string stage = Path.Combine(dir, "stage.chd");
        string output = Path.Combine(dir, "probe.chd");
        string manifest = Path.Combine(dir, "probe.meta");
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily(gdi ? "gdi" : "cd", storageProfile);
        if (!ChdReconstructionManifest.TryCreateFromSource(descriptor, profile, identity, out ChdReconstructionManifest reconstruction, out error))
            return false;
        File.WriteAllBytes(manifest, reconstruction.Serialize());
        ChdmanRunResult step = Run(executable, $"createcd -i {Quote(descriptor)} -o {Quote(stage)} -c none -hs {profile.HunkSize} -f", dir, 180000);
        if (!step.Success ||
            !(step = Run(executable, $"addmeta -i {Quote(stage)} -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt {Quote(profile.ToMetadata(identity))} -nocs", dir, 30000)).Success ||
            !(step = Run(executable, $"addmeta -i {Quote(stage)} -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf {Quote(manifest)} -nocs", dir, 30000)).Success ||
            !(step = Run(executable, $"copy -i {Quote(stage)} -o {Quote(output)} -c {profile.Codecs} -hs {profile.HunkSize} -f", dir, 180000)).Success ||
            !(step = Run(executable, $"verify -i {Quote(output)}", dir, 180000)).Success)
        {
            error = (gdi ? "GDI" : "CD") + " " + storage + " profile pipeline failed: " + step.Output;
            return false;
        }
        string metadataError = "";
        if (!ChdEncodingProfile.TryRead(output, out _) ||
            !ChdReconstructionManifest.TryRead(output, out _, out metadataError))
        {
            error = (gdi ? "GDI" : "CD") + " " + storage + " profile metadata failed: " + metadataError;
            return false;
        }

        string extractedDescriptor = Path.Combine(dir, gdi ? "out.gdi" : "out.cue");
        string pattern = Path.Combine(dir, "out-track%02t.bin");
        ChdmanRunResult extract = Run(executable,
            $"extractcd -i {Quote(output)} -o {Quote(extractedDescriptor)} -ob {Quote(pattern)} -sb -f",
            dir,
            180000);
        if (!extract.Success)
        {
            error = (gdi ? "GDI" : "CD") + " " + storage + " extraction failed: " + extract.Output;
            return false;
        }
        return CompareExtractedPayloads(inputs, dir, "out-track", out error);
    }

    private static bool ProbeCueDialect(ChdmanIdentity identity, string root, string dialect, string trackType, int sectorSize, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, dialect + "-" + storage);
        Directory.CreateDirectory(dir);
        string input = Path.Combine(dir, "track01.bin");
        string descriptor = Path.Combine(dir, "probe.cue");
        string output = Path.Combine(dir, "probe.chd");
        string extractedDescriptor = Path.Combine(dir, "out.cue");
        string pattern = Path.Combine(dir, "out-track%02t.bin");
        WriteFixture(input, 384 * sectorSize, sectorSize == 2048 ? 211 : 223);
        File.WriteAllText(descriptor,
            "FILE \"track01.bin\" BINARY" + Environment.NewLine +
            "  TRACK 01 " + trackType + Environment.NewLine +
            "    INDEX 01 00:00:00" + Environment.NewLine,
            new UTF8Encoding(false));
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("cd", storageProfile);
        ChdEncodingProfile.ApplyDialect(profile, dialect);
        ChdmanRunResult step = Run(identity.ExecutablePath,
            $"createcd -i {Quote(descriptor)} -o {Quote(output)} -c {profile.Codecs} -hs {profile.HunkSize} -f",
            dir,
            180000);
        if (step.Success)
            step = Run(identity.ExecutablePath,
                $"extractcd -i {Quote(output)} -o {Quote(extractedDescriptor)} -ob {Quote(pattern)} -sb -f",
                dir,
                180000);
        string compareError = "";
        if (!step.Success || !CompareExtractedPayloads(new List<string> { input }, dir, "out-track", out compareError))
        {
            error = dialect + " " + storage + " round trip failed: " + (step.Success ? compareError : step.Output);
            return false;
        }
        return true;
    }

    private static bool CompareLogicalPrefix(string expectedPath, string chdPath, out string error)
    {
        error = "";
        try
        {
            using (FileStream expected = File.OpenRead(expectedPath))
            using (Stream logical = CHDSharpLib.ChdLogicalStream.OpenRead(chdPath))
            {
                if (logical.Length < expected.Length)
                {
                    error = "CHD logical stream is shorter than the source payload.";
                    return false;
                }
                byte[] left = new byte[1024 * 1024];
                byte[] right = new byte[left.Length];
                long remaining = expected.Length;
                while (remaining > 0)
                {
                    int wanted = (int)Math.Min(left.Length, remaining);
                    int leftRead = ReadExactlyUpTo(expected, left, wanted);
                    int rightRead = ReadExactlyUpTo(logical, right, wanted);
                    if (leftRead != wanted || rightRead != wanted)
                    {
                        error = "Unexpected end of payload during native TOC extraction.";
                        return false;
                    }
                    for (int i = 0; i < wanted; i++)
                    {
                        if (left[i] != right[i])
                        {
                            error = "Native TOC payload differs at byte " + (expected.Length - remaining + i) + ".";
                            return false;
                        }
                    }
                    remaining -= wanted;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static int ReadExactlyUpTo(Stream stream, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static bool ProbeTocDialect(ChdmanIdentity identity, string root, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, "toc-rwraw-" + storage);
        Directory.CreateDirectory(dir);
        string input = Path.Combine(dir, "track01.bin");
        string descriptor = Path.Combine(dir, "probe.toc");
        string output = Path.Combine(dir, "probe.chd");
        string extractedDescriptor = Path.Combine(dir, "out.cue");
        string pattern = Path.Combine(dir, "out-track%02t.bin");
        WriteFixture(input, 300 * (2352 + 96), 239);
        File.WriteAllText(descriptor,
            "CD_ROM_XA\r\n" +
            "TRACK MODE2_RAW RW_RAW\r\n" +
            "NO COPY\r\n" +
            "DATAFILE \"track01.bin\" 00:00:00 00:04:00\r\n",
            new UTF8Encoding(false));
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("cd", storageProfile);
        ChdEncodingProfile.ApplyDialect(profile, "toc-exact");
        ChdmanRunResult step = Run(identity.ExecutablePath,
            $"createcd -i {Quote(descriptor)} -o {Quote(output)} -c {profile.Codecs} -hs {profile.HunkSize} -f",
            dir,
            180000);
        if (step.Success)
            step = Run(identity.ExecutablePath,
                $"extractcd -i {Quote(output)} -o {Quote(extractedDescriptor)} -ob {Quote(pattern)} -sb -f",
                dir,
                180000);
        string compareError = "";
        if (!step.Success || !CompareLogicalPrefix(input, output, out compareError))
        {
            error = "TOC MODE2_RAW/RW_RAW " + storage + " round trip failed: " + (step.Success ? compareError : step.Output);
            return false;
        }
        return true;
    }

    private static bool ProbeComplexCueDialect(ChdmanIdentity identity, string root, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, "cue-complex-" + storage);
        Directory.CreateDirectory(dir);
        string track1 = Path.Combine(dir, "track01.bin");
        string track2 = Path.Combine(dir, "track02.bin");
        string track3 = Path.Combine(dir, "track03.bin");
        WriteFixture(track1, 300 * 2352, 17);
        WriteFixture(track2, 300 * 2352, 29);
        WriteFixture(track3, 225 * 2352, 43);
        string cue = Path.Combine(dir, "probe.cue");
        File.WriteAllText(cue,
            "FILE \"track01.bin\" BINARY\r\n" +
            "  TRACK 01 MODE1/2352\r\n" +
            "    INDEX 01 00:00:00\r\n" +
            "FILE \"track02.bin\" BINARY\r\n" +
            "  TRACK 02 AUDIO\r\n" +
            "    FLAGS PRE DCP\r\n" +
            "    INDEX 00 00:00:00\r\n" +
            "    INDEX 01 00:02:00\r\n" +
            "FILE \"track03.bin\" BINARY\r\n" +
            "  TRACK 03 AUDIO\r\n" +
            "    PREGAP 00:02:00\r\n" +
            "    INDEX 01 00:00:00\r\n" +
            "    POSTGAP 00:01:00\r\n",
            new UTF8Encoding(false));
        return ProbeCueFiles(identity, dir, cue, new List<string> { track1, track2, track3 }, "cue-complex-layout", storageProfile, out error);
    }

    private static bool ProbeUnicodeCueDialect(ChdmanIdentity identity, string root, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, "cue-unicode-" + storage);
        Directory.CreateDirectory(dir);
        string input = Path.Combine(dir, "träck-日本.bin");
        WriteFixture(input, 225 * 2352, 61);
        string cue = Path.Combine(dir, "dïsc-日本.cue");
        File.WriteAllText(cue,
            "FILE \"träck-日本.bin\" BINARY\r\n" +
            "  TRACK 01 MODE1/2352\r\n" +
            "    INDEX 01 00:00:00\r\n",
            new UTF8Encoding(false));
        if (!ChdDescriptorStager.TryStageCueWithAsciiNames(cue, dir, out string stagedCue, out string stagedRoot, out error))
            return false;
        try
        {
            return ProbeCueFiles(identity, dir, stagedCue, new List<string> { input }, "cue-unicode", storageProfile, out error);
        }
        finally
        {
            ChdDescriptorStager.TryDelete(stagedRoot);
        }
    }

    private static bool ProbeCueFiles(ChdmanIdentity identity, string dir, string cue, List<string> inputs, string dialect, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string output = Path.Combine(dir, "probe.chd");
        string extractedDescriptor = Path.Combine(dir, "out.cue");
        string pattern = Path.Combine(dir, "out-track%02t.bin");
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("cd", storageProfile);
        ChdEncodingProfile.ApplyDialect(profile, dialect);
        ChdmanRunResult step = Run(identity.ExecutablePath,
            $"createcd -i {Quote(cue)} -o {Quote(output)} -c {profile.Codecs} -hs {profile.HunkSize} -f", dir, 180000);
        if (step.Success)
            step = Run(identity.ExecutablePath,
                $"extractcd -i {Quote(output)} -o {Quote(extractedDescriptor)} -ob {Quote(pattern)} -sb -f", dir, 180000);
        string compareError = "";
        if (!step.Success || !CompareExtractedPayloads(inputs, dir, "out-track", out compareError))
        {
            error = dialect + " " + (storageProfile == ChdStorageProfile.Playback ? "playback" : "archive") +
                    " round trip failed: " + (step.Success ? compareError : step.Output);
            return false;
        }
        return true;
    }

    private static bool ProbeRedumpGdi(ChdmanIdentity identity, string root, out string error)
    {
        error = "";
        string executable = identity.ExecutablePath;
        string dir = Path.Combine(root, "gdi-redump");
        Directory.CreateDirectory(dir);

        string track1 = Path.Combine(dir, "track01.bin");
        string track2 = Path.Combine(dir, "track02.raw");
        string track3 = Path.Combine(dir, "track03.bin");
        WriteFixture(track1, 450 * 2352, 101);
        WriteFixture(track2, 450 * 2352, 131);
        WriteFixture(track3, 32 * 2352, 151);
        string gdi = Path.Combine(dir, "source.gdi");
        File.WriteAllText(gdi,
            "3" + Environment.NewLine +
            "1 0 4 2352 track01.bin 0" + Environment.NewLine +
            "2 450 0 2352 track02.raw 0" + Environment.NewLine +
            "3 45000 4 2352 track03.bin 0" + Environment.NewLine,
            new UTF8Encoding(false));

        string seedChd = Path.Combine(dir, "seed.chd");
        string redumpCue = Path.Combine(dir, "redump.cue");
        string redumpPattern = Path.Combine(dir, "redump-track%02t.bin");
        string standardizedChd = Path.Combine(dir, "standard.chd");
        string outputCue = Path.Combine(dir, "output.cue");
        string outputPattern = Path.Combine(dir, "output-track%02t.bin");

        ChdmanRunResult createSeed = Run(executable,
            $"createcd -i {Quote(gdi)} -o {Quote(seedChd)} -c none -hs 19584 -f",
            dir,
            180000);
        ChdmanRunResult exportRedump = createSeed.Success
            ? Run(executable,
                $"extractcd -i {Quote(seedChd)} -o {Quote(redumpCue)} -ob {Quote(redumpPattern)} -sb -f",
                dir,
                180000)
            : createSeed;
        if (!createSeed.Success || !exportRedump.Success)
        {
            error = "Dreamcast Redump export capability probe failed: " + (createSeed.Success ? exportRedump.Output : createSeed.Output);
            return false;
        }

        List<string> redumpPayloads = Directory.GetFiles(dir)
            .Where(path => Path.GetFileName(path).StartsWith("redump-track", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (redumpPayloads.Count == 0)
        {
            error = "Dreamcast Redump export capability probe produced no tracks.";
            return false;
        }

        ChdmanRunResult createStandard = Run(executable,
            $"createcd -i {Quote(redumpCue)} -o {Quote(standardizedChd)} -c cdlz,cdzl,cdfl -hs 19584 -f",
            dir,
            180000);
        ChdmanRunResult extractStandard = createStandard.Success
            ? Run(executable,
                $"extractcd -i {Quote(standardizedChd)} -o {Quote(outputCue)} -ob {Quote(outputPattern)} -sb -f",
                dir,
                180000)
            : createStandard;
        if (!createStandard.Success || !extractStandard.Success)
        {
            error = "Dreamcast Redump import capability probe failed: " + (createStandard.Success ? extractStandard.Output : createStandard.Output);
            return false;
        }

        return CompareExtractedPayloads(redumpPayloads, dir, "output-track", out error);
    }

    private static bool ProbeDvd(ChdmanIdentity identity, string root, string name, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string executable = identity.ExecutablePath;
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, name + "-" + storage);
        Directory.CreateDirectory(dir);
        string input = Path.Combine(dir, "probe.iso");
        string stage = Path.Combine(dir, "stage.chd");
        string output = Path.Combine(dir, "probe.chd");
        string extracted = Path.Combine(dir, "out.iso");
        string manifest = Path.Combine(dir, "probe.meta");
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily(name, storageProfile);
        WriteFixture(input, 3 * 1024 * 1024, name == "psp" ? 91 : 67);
        if (!ChdReconstructionManifest.TryCreateFromSource(input, profile, identity, out ChdReconstructionManifest reconstruction, out error))
            return false;
        File.WriteAllBytes(manifest, reconstruction.Serialize());
        ChdmanRunResult step = Run(executable, $"createdvd -i {Quote(input)} -o {Quote(stage)} -c none -hs {profile.HunkSize} -f", dir, 180000);
        if (!step.Success ||
            !(step = Run(executable, $"addmeta -i {Quote(stage)} -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt {Quote(profile.ToMetadata(identity))} -nocs", dir, 30000)).Success ||
            !(step = Run(executable, $"addmeta -i {Quote(stage)} -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf {Quote(manifest)} -nocs", dir, 30000)).Success ||
            !(step = Run(executable, $"copy -i {Quote(stage)} -o {Quote(output)} -c {profile.Codecs} -hs {profile.HunkSize} -f", dir, 180000)).Success ||
            !(step = Run(executable, $"verify -i {Quote(output)}", dir, 180000)).Success ||
            !(step = Run(executable, $"extractdvd -i {Quote(output)} -o {Quote(extracted)} -f", dir, 180000)).Success)
        {
            error = name.ToUpperInvariant() + " " + storage + " profile pipeline failed: " + step.Output;
            return false;
        }
        string metadataError = "";
        if (!ChdEncodingProfile.TryRead(output, out _) ||
            !ChdReconstructionManifest.TryRead(output, out _, out metadataError) ||
            !string.Equals(HashFileSha256(input), HashFileSha256(extracted), StringComparison.OrdinalIgnoreCase))
        {
            error = name.ToUpperInvariant() + " " + storage + " profile round trip or metadata mismatch: " + metadataError;
            return false;
        }
        return true;
    }

    private static bool ProbeSingleFile(ChdmanIdentity identity, string root, string family, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, family + "-" + storage);
        Directory.CreateDirectory(dir);
        string extension = family == "hdd" ? ".img" : ".raw";
        string input = Path.Combine(dir, "probe" + extension);
        string stage = Path.Combine(dir, "stage.chd");
        string output = Path.Combine(dir, "probe.chd");
        string extracted = Path.Combine(dir, "out" + extension);
        string manifestPath = Path.Combine(dir, "probe.meta");
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily(family, storageProfile);
        int length = family == "hdd" ? 3 * 1024 * 1024 : 3 * 1024 * 1024 + 17;
        if (family == "hdd")
            ChdEncodingProfile.ApplyHddGeometry(profile, length, storageProfile, ChdHddGeometryMode.Auto);
        WriteFixture(input, length, family == "hdd" ? 109 : 101);
        if (!ChdReconstructionManifest.TryCreateFromSource(input, profile, identity, out ChdReconstructionManifest reconstruction, out error))
            return false;
        File.WriteAllBytes(manifestPath, reconstruction.Serialize());

        string createArgs = family == "hdd"
            ? $"createhd -i {Quote(input)} -o {Quote(stage)} -c none -hs {profile.HunkSize} -ss {profile.HddSectorSize} -chs {profile.HddCylinders},{profile.HddHeads},{profile.HddSectors} -f"
            : $"createraw -i {Quote(input)} -o {Quote(stage)} -c none -hs {profile.HunkSize} -us 1 -f";
        string extractCommand = family == "hdd" ? "extracthd" : "extractraw";
        ChdmanRunResult step = Run(identity.ExecutablePath, createArgs, dir, 180000);
        if (!step.Success ||
            !(step = Run(identity.ExecutablePath, $"addmeta -i {Quote(stage)} -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt {Quote(profile.ToMetadata(identity))} -nocs", dir, 30000)).Success ||
            !(step = Run(identity.ExecutablePath, $"addmeta -i {Quote(stage)} -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf {Quote(manifestPath)} -nocs", dir, 30000)).Success ||
            !(step = Run(identity.ExecutablePath, $"copy -i {Quote(stage)} -o {Quote(output)} -c {profile.Codecs} -hs {profile.HunkSize} -f", dir, 180000)).Success ||
            !(step = Run(identity.ExecutablePath, $"verify -i {Quote(output)}", dir, 180000)).Success ||
            !(step = Run(identity.ExecutablePath, $"{extractCommand} -i {Quote(output)} -o {Quote(extracted)} -f", dir, 180000)).Success)
        {
            error = family.ToUpperInvariant() + " " + storage + " profile pipeline failed: " + step.Output;
            return false;
        }

        string metadataError = "";
        if (!ChdEncodingProfile.TryRead(output, out _) ||
            !ChdReconstructionManifest.TryRead(output, out _, out metadataError) ||
            !string.Equals(HashFileSha256(input), HashFileSha256(extracted), StringComparison.OrdinalIgnoreCase))
        {
            error = family.ToUpperInvariant() + " " + storage + " profile round trip or metadata mismatch: " + metadataError;
            return false;
        }
        return true;
    }

    private static bool ProbeLaserDisc(ChdmanIdentity identity, string root, ChdStorageProfile storageProfile, out string error)
    {
        error = "";
        string storage = storageProfile == ChdStorageProfile.Playback ? "playback" : "archive";
        string dir = Path.Combine(root, "laserdisc-" + storage);
        Directory.CreateDirectory(dir);
        string seedAvi = Path.Combine(dir, "seed.avi");
        string seedChd = Path.Combine(dir, "seed.chd");
        string canonicalAvi = Path.Combine(dir, "canonical.avi");
        string encoded = Path.Combine(dir, "encoded.chd");
        string stage = Path.Combine(dir, "stage.chd");
        string output = Path.Combine(dir, "probe.chd");
        string extracted = Path.Combine(dir, "out.avi");
        string manifestPath = Path.Combine(dir, "probe.meta");

        WriteLaserDiscSeedAvi(seedAvi);
        ChdmanRunResult step = Run(identity.ExecutablePath, $"createld -i {Quote(seedAvi)} -o {Quote(seedChd)} -c avhu -f", dir, 180000);
        if (step.Success)
            step = Run(identity.ExecutablePath, $"extractld -i {Quote(seedChd)} -o {Quote(canonicalAvi)} -f", dir, 180000);
        if (step.Success)
            step = Run(identity.ExecutablePath, $"createld -i {Quote(canonicalAvi)} -o {Quote(encoded)} -c avhu -f", dir, 180000);
        string infoError = "";
        if (!step.Success || !CHDSharpLib.ChdMetadata.TryReadContainerInfo(encoded, out CHDSharpLib.ChdContainerInfo info, out infoError))
        {
            error = "LaserDisc " + storage + " seed pipeline failed: " + (step.Success ? infoError : step.Output);
            return false;
        }

        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("laserdisc", storageProfile, (int)info.HunkSize, (int)info.UnitSize);
        if (!ChdReconstructionManifest.TryCreateFromSource(canonicalAvi, profile, identity, out ChdReconstructionManifest reconstruction, out error))
            return false;
        File.WriteAllBytes(manifestPath, reconstruction.Serialize());
        if (!(step = Run(identity.ExecutablePath, $"copy -i {Quote(encoded)} -o {Quote(stage)} -c none -hs {profile.HunkSize} -f", dir, 180000)).Success ||
            !(step = Run(identity.ExecutablePath, $"addmeta -i {Quote(stage)} -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt {Quote(profile.ToMetadata(identity))} -nocs", dir, 30000)).Success ||
            !(step = Run(identity.ExecutablePath, $"addmeta -i {Quote(stage)} -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf {Quote(manifestPath)} -nocs", dir, 30000)).Success ||
            !(step = Run(identity.ExecutablePath, $"copy -i {Quote(stage)} -o {Quote(output)} -c avhu -hs {profile.HunkSize} -f", dir, 180000)).Success ||
            !(step = Run(identity.ExecutablePath, $"verify -i {Quote(output)}", dir, 180000)).Success ||
            !(step = Run(identity.ExecutablePath, $"extractld -i {Quote(output)} -o {Quote(extracted)} -f", dir, 180000)).Success)
        {
            error = "LaserDisc " + storage + " profile pipeline failed: " + step.Output;
            return false;
        }

        if (!string.Equals(HashFileSha256(canonicalAvi), HashFileSha256(extracted), StringComparison.OrdinalIgnoreCase))
        {
            error = "LaserDisc " + storage + " canonical AVI did not round trip byte-for-byte.";
            return false;
        }
        return true;
    }

    private static void WriteLaserDiscSeedAvi(string path)
    {
        // AVHUFF is designed for raster video and rejects degenerate tiny
        // frames on decode. 64x48 is small enough for a fast probe while
        // still exercising the real codec path.
        const int width = 64;
        const int height = 48;
        const int frames = 2;
        const int rate = 30;
        const int audioRate = 48000;
        const int channels = 2;
        const int blockAlign = channels * 2;
        const int samplesPerFrame = audioRate / rate;
        const int videoBytes = width * height * 2;
        const int audioBytes = samplesPerFrame * blockAlign;

        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            long riff = BeginAviChunk(writer, "RIFF", "AVI ");
            long hdrl = BeginAviChunk(writer, "LIST", "hdrl");
            long avih = BeginAviChunk(writer, "avih", null);
            writer.Write(1000000 / rate);
            writer.Write(audioRate * blockAlign + videoBytes * rate);
            writer.Write(0);
            writer.Write(0x10);
            writer.Write(frames);
            writer.Write(0);
            writer.Write(2);
            writer.Write(Math.Max(videoBytes, audioBytes));
            writer.Write(width);
            writer.Write(height);
            for (int i = 0; i < 4; i++) writer.Write(0);
            EndAviChunk(writer, avih);

            long videoList = BeginAviChunk(writer, "LIST", "strl");
            long videoHeader = BeginAviChunk(writer, "strh", null);
            WriteFourCc(writer, "vids"); WriteFourCc(writer, "YUY2");
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(1); writer.Write(rate);
            writer.Write(0); writer.Write(frames); writer.Write(videoBytes); writer.Write(-1); writer.Write(0);
            writer.Write((short)0); writer.Write((short)0); writer.Write((short)width); writer.Write((short)height);
            EndAviChunk(writer, videoHeader);
            long videoFormat = BeginAviChunk(writer, "strf", null);
            writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((short)1); writer.Write((short)16);
            WriteFourCc(writer, "YUY2"); writer.Write(videoBytes); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            EndAviChunk(writer, videoFormat);
            EndAviChunk(writer, videoList);

            long audioList = BeginAviChunk(writer, "LIST", "strl");
            long audioHeader = BeginAviChunk(writer, "strh", null);
            WriteFourCc(writer, "auds"); writer.Write(0);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(blockAlign); writer.Write(audioRate * blockAlign);
            writer.Write(0); writer.Write(frames * samplesPerFrame); writer.Write(audioBytes); writer.Write(-1); writer.Write(blockAlign);
            writer.Write(0); writer.Write(0);
            EndAviChunk(writer, audioHeader);
            long audioFormat = BeginAviChunk(writer, "strf", null);
            writer.Write((short)1); writer.Write((short)channels); writer.Write(audioRate); writer.Write(audioRate * blockAlign);
            writer.Write((short)blockAlign); writer.Write((short)16);
            EndAviChunk(writer, audioFormat);
            EndAviChunk(writer, audioList);
            EndAviChunk(writer, hdrl);

            List<Tuple<string, int, int, int>> index = new List<Tuple<string, int, int, int>>();
            long movi = BeginAviChunk(writer, "LIST", "movi");
            // AVI 1.0 idx1 offsets are relative to the LIST's 'movi' FourCC,
            // so the first child chunk begins at offset four.
            long moviData = movi + 4;
            for (int frame = 0; frame < frames; frame++)
            {
                int videoOffset = checked((int)(writer.BaseStream.Position - moviData));
                long video = BeginAviChunk(writer, "00dc", null);
                for (int pixel = 0; pixel < width * height / 2; pixel++)
                {
                    writer.Write((byte)(32 + frame * 24 + pixel));
                    writer.Write((byte)(96 + pixel));
                    writer.Write((byte)(64 + frame * 24 + pixel));
                    writer.Write((byte)(160 - pixel));
                }
                EndAviChunk(writer, video);
                index.Add(Tuple.Create("00dc", 0x10, videoOffset, videoBytes));

                int audioOffset = checked((int)(writer.BaseStream.Position - moviData));
                long audio = BeginAviChunk(writer, "01wb", null);
                for (int sample = 0; sample < samplesPerFrame; sample++)
                {
                    short value = (short)(((sample + frame * samplesPerFrame) * 97) % 20000 - 10000);
                    writer.Write(value);
                    writer.Write((short)-value);
                }
                EndAviChunk(writer, audio);
                index.Add(Tuple.Create("01wb", 0, audioOffset, audioBytes));
            }
            EndAviChunk(writer, movi);

            long idx1 = BeginAviChunk(writer, "idx1", null);
            for (int i = 0; i < index.Count; i++)
            {
                WriteFourCc(writer, index[i].Item1);
                writer.Write(index[i].Item2);
                writer.Write(index[i].Item3);
                writer.Write(index[i].Item4);
            }
            EndAviChunk(writer, idx1);
            EndAviChunk(writer, riff);
        }
    }

    private static long BeginAviChunk(BinaryWriter writer, string tag, string listType)
    {
        WriteFourCc(writer, tag);
        long sizePosition = writer.BaseStream.Position;
        writer.Write(0);
        if (listType != null)
            WriteFourCc(writer, listType);
        return sizePosition;
    }

    private static void EndAviChunk(BinaryWriter writer, long sizePosition)
    {
        long end = writer.BaseStream.Position;
        int size = checked((int)(end - sizePosition - 4));
        if ((size & 1) != 0)
        {
            writer.Write((byte)0);
            end++;
        }
        writer.BaseStream.Position = sizePosition;
        writer.Write(size);
        writer.BaseStream.Position = end;
    }

    private static void WriteFourCc(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value ?? "");
        if (bytes.Length != 4)
            throw new InvalidDataException("AVI FourCC must contain four characters.");
        writer.Write(bytes);
    }

    private static bool CompareExtractedPayloads(List<string> inputs, string directory, string outputPrefix, out string error)
    {
        error = "";
        List<string> outputHashes = Directory.GetFiles(directory)
            .Where(path => Path.GetFileName(path).StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(HashFileSha256)
            .ToList();
        for (int i = 0; i < inputs.Count; i++)
        {
            string expected = HashFileSha256(inputs[i]);
            int match = outputHashes.FindIndex(hash => string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase));
            if (match < 0)
            {
                error = "Extracted payload hash mismatch for " + Path.GetFileName(inputs[i]) + ".";
                return false;
            }
            outputHashes.RemoveAt(match);
        }
        return true;
    }

    private static void WriteFixture(string path, int length, int seed)
    {
        byte[] buffer = new byte[64 * 1024];
        int remaining = length;
        int position = 0;
        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            while (remaining > 0)
            {
                int count = Math.Min(buffer.Length, remaining);
                for (int i = 0; i < count; i++)
                    buffer[i] = (byte)((position + i) * 31 + seed);
                stream.Write(buffer, 0, count);
                remaining -= count;
                position += count;
            }
        }
    }

    private static bool TryGetCachedCapabilities(ChdmanIdentity identity, out ChdmanCapabilities capabilities)
    {
        capabilities = null;
        if (string.IsNullOrWhiteSpace(identity.BinarySha256))
            return false;
        try
        {
            string path = GetCapabilityCachePath();
            if (!File.Exists(path))
                return false;
            XmlSerializer serializer = new XmlSerializer(typeof(ChdmanCapabilityCacheFile));
            using (FileStream stream = File.OpenRead(path))
            {
                ChdmanCapabilityCacheFile cache = serializer.Deserialize(stream) as ChdmanCapabilityCacheFile;
                ChdmanCapabilityCacheEntry entry = cache?.Entries?.FirstOrDefault(item =>
                    string.Equals(item.BinarySha256, identity.BinarySha256, StringComparison.OrdinalIgnoreCase) &&
                    item.Capabilities?.ProbeSchema == CapabilityProbeSchema);
                if (entry?.Capabilities == null)
                    return false;
                capabilities = entry.Capabilities;
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void SaveCachedCapabilities(ChdmanIdentity identity, ChdmanCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(identity.BinarySha256) || capabilities == null)
            return;
        try
        {
            string path = GetCapabilityCachePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ChdmanCapabilityCacheFile cache = new ChdmanCapabilityCacheFile();
            if (File.Exists(path))
            {
                try
                {
                    XmlSerializer reader = new XmlSerializer(typeof(ChdmanCapabilityCacheFile));
                    using (FileStream input = File.OpenRead(path))
                        cache = reader.Deserialize(input) as ChdmanCapabilityCacheFile ?? cache;
                }
                catch
                {
                    cache = new ChdmanCapabilityCacheFile();
                }
            }
            cache.Entries.RemoveAll(item => string.Equals(item.BinarySha256, identity.BinarySha256, StringComparison.OrdinalIgnoreCase));
            cache.Entries.Add(new ChdmanCapabilityCacheEntry
            {
                BinarySha256 = identity.BinarySha256,
                VersionText = identity.VersionText,
                Banner = identity.Banner,
                Capabilities = capabilities
            });
            string temp = path + ".tmp";
            XmlSerializer writer = new XmlSerializer(typeof(ChdmanCapabilityCacheFile));
            using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                writer.Serialize(output, cache);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }
        catch
        {
        }
    }

    private static string GetCapabilityCachePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();
        return Path.Combine(root, "RomVault", "chdman-capabilities-v1.xml");
    }

    private static string HashFileSha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }

    private static string ResolveWorkingDirectory(string executable)
    {
        try
        {
            if (Path.IsPathRooted(executable))
            {
                string directory = Path.GetDirectoryName(executable);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    return directory;
            }
        }
        catch
        {
        }
        return Environment.CurrentDirectory;
    }

    private static string FirstBannerLine(string output)
    {
        string[] lines = (output ?? "").Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf("CHD", StringComparison.OrdinalIgnoreCase) >= 0 &&
                lines[i].IndexOf("manager", StringComparison.OrdinalIgnoreCase) >= 0)
                return lines[i].Trim();
        }
        return lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "";
    }

    private static void TryDeleteDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch
        {
        }
    }
}

public static class ChdmanDiagnostics
{
    public static string GetSummary(bool runCapabilityProbe)
    {
        string executable = ChdToolchainRegistry.SelectDefault();
        ChdmanProbeLevel level = runCapabilityProbe ? ChdmanProbeLevel.Full : ChdmanProbeLevel.Banner;
        if (!ChdmanService.TryGetIdentity(executable, level, out ChdmanIdentity identity, out string error))
            return "chdman unavailable: " + error;
        string summary = identity.Banner + Environment.NewLine + "SHA-256: " + identity.BinarySha256;
        if (identity.Capabilities != null)
            summary += Environment.NewLine + "Validated: " + identity.Capabilities.Fingerprint +
                       (string.IsNullOrWhiteSpace(identity.Capabilities.ProbeError) ? "" : Environment.NewLine + identity.Capabilities.ProbeError);
        return summary;
    }

    public static string GetToolchainReport(bool runCapabilityProbe)
    {
        return ChdToolchainRegistry.BuildReport(runCapabilityProbe);
    }

    public static bool TryGetBinarySha256(string executable, out string sha256, out string error)
    {
        sha256 = "";
        if (!ChdmanService.TryGetIdentity(executable, ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out error))
            return false;
        sha256 = identity.BinarySha256;
        return !string.IsNullOrWhiteSpace(sha256);
    }

    public static int RunCrossVersionMatrix(IEnumerable<string> executables, out string report)
    {
        List<string> lines = new List<string> { "RomVault cross-version CHD matrix" };
        int exitCode = 0;
        List<string> paths = (executables ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0)
            paths = ChdmanService.FindExecutableCandidates().Where(File.Exists).ToList();
        if (paths.Count == 0)
        {
            report = "No chdman binaries were supplied or discovered.";
            return 2;
        }
        for (int i = 0; i < paths.Count; i++)
        {
            int code = RunPreservationMatrix(paths[i], out string itemReport);
            lines.Add(Environment.NewLine + "=== " + ChdDiagnosticFormatter.RedactPath(paths[i]) + " ===");
            lines.Add(itemReport);
            if (code != 0)
                exitCode = code;
        }
        report = string.Join(Environment.NewLine, lines);
        return exitCode;
    }

    public static void ClearCapabilityCache()
    {
        ChdmanService.ClearCapabilityCache();
    }

    public static int RunPreservationMatrix(string executable, out string report)
    {
        ChdmanService.ClearCapabilityCache();
        if (string.IsNullOrWhiteSpace(executable))
            executable = ChdmanService.FindExecutable();
        if (!ChdmanService.TryGetIdentity(executable, ChdmanProbeLevel.Full, out ChdmanIdentity identity, out string error))
        {
            report = "CHD preservation matrix failed: " + error;
            return 2;
        }

        ChdmanCapabilities capabilities = identity.Capabilities ?? new ChdmanCapabilities { ProbeError = "No capability results." };
        bool manifestOk = ChdReconstructionManifest.RunSerializationSelfTest(out string manifestError);
        bool hardeningOk = ChdHardeningSelfTests.Run(out string hardeningError);
        bool recoveryOk = ChdUpgradeRecovery.RunFaultRecoverySelfTest(out string recoveryError);
        bool multiViewOk = ChdMultiView.RunSelfTest(out string multiViewError);
        bool healthOk = ChdHealthStore.RunSelfTest(out string healthError);
        bool geometryOk = ChdHddGeometry.RunSelfTest(out string geometryError);
        bool parityOk = ChdCollectionParity.RunSelfTest(out string parityError);
        bool v1ConformanceOk = ChdV1Conformance.Run(executable, out string v1ConformanceError);
        bool opticalHardeningOk = ChdOpticalAdversarialTests.Run(identity, out string opticalHardeningError);
        bool conversionOk = ChdConversionSelfTests.Run(executable, identity, out string conversionError);
        string parentError = capabilities.SupportsCopy ? "" : "chdman copy is unavailable";
        bool parentOk = capabilities.SupportsCopy && ChdParentGraph.RunFixture(executable, out parentError);
        bool mediaGraphOk = DATReader.DatClean.DatSetCompressionType.RunMediaGraphSelfTest(out string mediaGraphError);
        bool contentMatchOk = RomVaultCore.RvDB.DBHelper.RunChdContentIdentitySelfTest(out string contentMatchError);
        bool loggingDefaultOk = !new RomVaultCore.Settings().ChdDebug;
        bool redumpAdvertised = identity.Version >= new Version(0, 227);
        string redumpStatus = capabilities.RedumpGdiRoundTrip ? "PASS" : redumpAdvertised ? "FAIL" : "SKIP (unsupported)";
        List<string> lines = new List<string>
        {
            "RomVault CHD preservation matrix",
            identity.Banner,
            "binarySha256=" + identity.BinarySha256,
            "capabilities=" + capabilities.Fingerprint,
            "CD-Playback=" + (capabilities.CdPlaybackRoundTrip ? "PASS" : "FAIL"),
            "CD-Archive=" + (capabilities.CdArchiveRoundTrip ? "PASS" : "FAIL"),
            "GDI-Playback=" + (capabilities.GdiPlaybackRoundTrip ? "PASS" : "FAIL"),
            "GDI-Archive=" + (capabilities.GdiArchiveRoundTrip ? "PASS" : "FAIL"),
            "GDI-Redump=" + redumpStatus,
            "DVD-Playback=" + (capabilities.DvdPlaybackRoundTrip ? "PASS" : "FAIL"),
            "DVD-Archive=" + (capabilities.DvdArchiveRoundTrip ? "PASS" : "FAIL"),
            "PSP-Playback=" + (capabilities.PspPlaybackRoundTrip ? "PASS" : "FAIL"),
            "PSP-Archive=" + (capabilities.PspArchiveRoundTrip ? "PASS" : "FAIL"),
            "Raw-Playback=" + (capabilities.RawPlaybackRoundTrip ? "PASS" : "FAIL"),
            "Raw-Archive=" + (capabilities.RawArchiveRoundTrip ? "PASS" : "FAIL"),
            "HDD-Playback=" + (capabilities.HddPlaybackRoundTrip ? "PASS" : "FAIL"),
            "HDD-Archive=" + (capabilities.HddArchiveRoundTrip ? "PASS" : "FAIL"),
            "LaserDisc-Playback=" + (capabilities.LaserDiscPlaybackRoundTrip ? "PASS" : "FAIL"),
            "LaserDisc-Archive=" + (capabilities.LaserDiscArchiveRoundTrip ? "PASS" : "FAIL"),
            "Manifest=" + (manifestOk ? "PASS" : "FAIL"),
            "Parser-hardening=" + (hardeningOk ? "PASS" : "FAIL"),
            "Transaction-fault-recovery=" + (recoveryOk ? "PASS" : "FAIL"),
            "Multi-view-CUE-ISO=" + (multiViewOk ? "PASS" : "FAIL"),
            "Health-database-redaction=" + (healthOk ? "PASS" : "FAIL"),
            "HDD-exact-geometry=" + (geometryOk ? "PASS" : "FAIL"),
            "Collection-parity-recovery=" + (parityOk ? "PASS" : "FAIL"),
            "V1-determinism-differential-decode=" + (v1ConformanceOk ? "PASS" : "FAIL"),
            "Optical-adversarial-corpus=" + (opticalHardeningOk ? "PASS" : "FAIL"),
            "Transactional-profile-conversion=" + (conversionOk ? "PASS" : "FAIL"),
            "Parent-standalone-roundtrip=" + (parentOk ? "PASS" : "FAIL"),
            "Mixed-media-DAT=" + (mediaGraphOk ? "PASS" : "FAIL"),
            "Content-addressed-ToSort=" + (contentMatchOk ? "PASS" : "FAIL"),
            "Diagnostic-logging-default-off=" + (loggingDefaultOk ? "PASS" : "FAIL")
        };
        if (!string.IsNullOrWhiteSpace(capabilities.ProbeError))
            lines.Add("probeError=" + capabilities.ProbeError);
        if (!string.IsNullOrWhiteSpace(manifestError))
            lines.Add("manifestError=" + manifestError);
        if (!string.IsNullOrWhiteSpace(hardeningError))
            lines.Add("hardeningError=" + hardeningError);
        if (!string.IsNullOrWhiteSpace(recoveryError))
            lines.Add("recoveryError=" + recoveryError);
        if (!string.IsNullOrWhiteSpace(multiViewError))
            lines.Add("multiViewError=" + multiViewError);
        if (!string.IsNullOrWhiteSpace(healthError))
            lines.Add("healthError=" + healthError);
        if (!string.IsNullOrWhiteSpace(geometryError))
            lines.Add("geometryError=" + geometryError);
        if (!string.IsNullOrWhiteSpace(parityError))
            lines.Add("parityError=" + parityError);
        if (!string.IsNullOrWhiteSpace(v1ConformanceError))
            lines.Add("v1ConformanceError=" + v1ConformanceError);
        if (!string.IsNullOrWhiteSpace(opticalHardeningError))
            lines.Add("opticalHardeningError=" + opticalHardeningError);
        if (!string.IsNullOrWhiteSpace(conversionError))
            lines.Add("conversionError=" + conversionError);
        if (!string.IsNullOrWhiteSpace(parentError))
            lines.Add("parentError=" + parentError);
        if (!string.IsNullOrWhiteSpace(mediaGraphError))
            lines.Add("mediaGraphError=" + mediaGraphError);
        if (!string.IsNullOrWhiteSpace(contentMatchError))
            lines.Add("contentMatchError=" + contentMatchError);
        report = string.Join(Environment.NewLine, lines);
        return capabilities.CdPlaybackRoundTrip && capabilities.CdArchiveRoundTrip &&
               capabilities.GdiPlaybackRoundTrip && capabilities.GdiArchiveRoundTrip &&
               (!redumpAdvertised || capabilities.RedumpGdiRoundTrip) &&
               capabilities.DvdPlaybackRoundTrip && capabilities.DvdArchiveRoundTrip &&
               capabilities.PspPlaybackRoundTrip && capabilities.PspArchiveRoundTrip &&
               capabilities.RawPlaybackRoundTrip && capabilities.RawArchiveRoundTrip &&
               capabilities.HddPlaybackRoundTrip && capabilities.HddArchiveRoundTrip &&
               capabilities.LaserDiscPlaybackRoundTrip && capabilities.LaserDiscArchiveRoundTrip &&
               manifestOk && hardeningOk && recoveryOk && multiViewOk && healthOk && geometryOk && parityOk && v1ConformanceOk && opticalHardeningOk && conversionOk && parentOk && mediaGraphOk && contentMatchOk && loggingDefaultOk ? 0 : 5;
    }
}
