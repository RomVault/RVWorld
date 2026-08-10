using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CHDSharpLib;

namespace RomVaultCore.Utils;

/// <summary>
/// Resolves a parented CHD against standalone CHDs discovered during the current
/// scan/fix session. CHD parent identities are native CHD SHA-1 values, not
/// hashes of the physical container file.
/// </summary>
public static class ChdParentResolver
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RegisteredChd> ByPath =
        new Dictionary<string, RegisteredChd>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> BySha1 =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ScannedDirectories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static int _generation;

    internal static int Generation
    {
        get { lock (Sync) return _generation; }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            ByPath.Clear();
            BySha1.Clear();
            ScannedDirectories.Clear();
            unchecked { _generation++; }
        }
    }

    public static bool TryRegister(string path, out ChdContainerInfo info, out string error)
    {
        info = null;
        error = "";
        string fullPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            lock (Sync)
            {
                if (ByPath.TryGetValue(fullPath ?? "", out RegisteredChd missing))
                {
                    RemoveIdentity(missing.Identity, fullPath);
                    ByPath.Remove(fullPath);
                }
            }
            error = "CHD file was not found.";
            return false;
        }

        long length;
        long timestamp;
        try
        {
            FileInfo file = new FileInfo(fullPath);
            length = file.Length;
            timestamp = file.LastWriteTimeUtc.Ticks;
        }
        catch (Exception ex)
        {
            error = "CHD file information could not be read: " + ex.Message;
            return false;
        }

        lock (Sync)
        {
            if (ByPath.TryGetValue(fullPath, out RegisteredChd cached) &&
                cached.Length == length && cached.LastWriteUtcTicks == timestamp)
            {
                info = cached.Info;
                return true;
            }
        }

        if (!ChdMetadata.TryReadContainerInfo(fullPath, out info, out error))
            return false;

        Register(fullPath, info);
        return true;
    }

    public static void Register(string path, ChdContainerInfo info)
    {
        string fullPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(fullPath) || info == null)
            return;

        string identity = EffectiveSha1(info);
        long length = -1;
        long timestamp = long.MinValue;
        try
        {
            FileInfo file = new FileInfo(fullPath);
            length = file.Length;
            timestamp = file.LastWriteTimeUtc.Ticks;
        }
        catch
        {
        }
        lock (Sync)
        {
            if (ByPath.TryGetValue(fullPath, out RegisteredChd previous))
                RemoveIdentity(previous.Identity, fullPath);

            ByPath[fullPath] = new RegisteredChd(fullPath, info, identity, length, timestamp);
            if (string.IsNullOrWhiteSpace(identity))
                return;

            if (!BySha1.TryGetValue(identity, out HashSet<string> paths))
            {
                paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                BySha1.Add(identity, paths);
            }
            paths.Add(fullPath);
        }
    }

    public static void RegisterDirectory(string directory, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        string root = NormalizePath(directory);
        string scanKey = (recursive ? "R|" : "T|") + root;
        lock (Sync)
        {
            if (!ScannedDirectories.Add(scanKey))
                return;
        }

        Stack<string> pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            try
            {
                lock (Sync)
                    ScannedDirectories.Add("T|" + NormalizePath(current));
                foreach (string file in Directory.EnumerateFiles(current, "*.chd", SearchOption.TopDirectoryOnly))
                    TryRegister(file, out _, out _);

                if (!recursive)
                    continue;

                foreach (string child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                            pending.Push(child);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }

    public static void RegisterConfiguredSearchPaths()
    {
        List<string> paths = Settings.rvSettings?.ChdParentSearchPaths;
        if (paths == null)
            return;
        for (int i = 0; i < paths.Count; i++)
            RegisterDirectory(paths[i], true);
    }

    /// <summary>
    /// Resolves the immediate standalone parent of <paramref name="childPath"/>.
    /// </summary>
    public static bool TryResolveParent(string childPath, out string parentPath, out string error)
    {
        parentPath = "";
        error = "";
        if (!TryRegister(childPath, out ChdContainerInfo child, out error))
            return false;
        if (!child.RequiresParent)
            return true;

        string wanted = Hex(child.ParentSha1);
        if (string.IsNullOrWhiteSpace(wanted))
        {
            error = "Parented CHD has no usable parent SHA1.";
            return false;
        }
        string childIdentity = EffectiveSha1(child);
        if (string.Equals(wanted, childIdentity, StringComparison.OrdinalIgnoreCase))
        {
            error = "CHD parent graph contains a self-reference.";
            return false;
        }

        string siblingDirectory = Path.GetDirectoryName(NormalizePath(childPath));
        RegisterDirectory(siblingDirectory, false);
        RegisterConfiguredSearchPaths();

        List<string> candidates;
        lock (Sync)
        {
            candidates = BySha1.TryGetValue(wanted, out HashSet<string> paths)
                ? paths.ToList()
                : new List<string>();
        }

        string normalizedChild = NormalizePath(childPath);
        candidates.RemoveAll(path => string.Equals(path, normalizedChild, StringComparison.OrdinalIgnoreCase));
        candidates.Sort((left, right) => CompareCandidate(left, right, siblingDirectory));

        bool foundParentedCandidate = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            if (!TryRegister(candidate, out ChdContainerInfo parent, out _))
                continue;
            if (!string.Equals(wanted, EffectiveSha1(parent), StringComparison.OrdinalIgnoreCase))
                continue;
            if (parent.RequiresParent)
            {
                foundParentedCandidate = true;
                continue;
            }

            parentPath = candidate;
            return true;
        }

        error = foundParentedCandidate
            ? "The matching CHD parent also requires a parent; chained parent graphs are not supported for collection scanning."
            : "No standalone CHD with SHA1 " + wanted + " was found in the ROM root, ToSort, or configured parent search paths.";
        return false;
    }

    public static bool TryValidatePair(string childPath, string parentPath, out string error)
    {
        error = "";
        if (!TryRegister(childPath, out ChdContainerInfo child, out error) || !child.RequiresParent)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "Input CHD does not require a parent.";
            return false;
        }
        if (!TryRegister(parentPath, out ChdContainerInfo parent, out error))
            return false;
        if (parent.RequiresParent)
        {
            error = "The selected CHD parent is not standalone.";
            return false;
        }
        if (!string.Equals(Hex(child.ParentSha1), EffectiveSha1(parent), StringComparison.OrdinalIgnoreCase))
        {
            error = "The selected parent SHA1 does not match the child CHD header.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Returns true when deleting <paramref name="parentPath"/> would leave a
    /// currently registered child without another standalone copy of the same
    /// parent identity.
    /// </summary>
    public static bool WouldOrphanExistingDependents(string parentPath)
    {
        if (!TryRegister(parentPath, out ChdContainerInfo parent, out _) || parent.RequiresParent)
            return false;
        string identity = EffectiveSha1(parent);
        if (string.IsNullOrWhiteSpace(identity))
            return false;

        string normalizedParent = NormalizePath(parentPath);
        List<RegisteredChd> registered;
        lock (Sync)
            registered = ByPath.Values.ToList();

        bool hasDependent = false;
        bool hasAlternate = false;
        for (int i = 0; i < registered.Count; i++)
        {
            RegisteredChd item = registered[i];
            if (!File.Exists(item.Path))
                continue;
            if (item.Info.RequiresParent &&
                string.Equals(Hex(item.Info.ParentSha1), identity, StringComparison.OrdinalIgnoreCase))
            {
                hasDependent = true;
                continue;
            }
            if (!item.Info.RequiresParent &&
                !string.Equals(item.Path, normalizedParent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Identity, identity, StringComparison.OrdinalIgnoreCase))
            {
                hasAlternate = true;
            }
        }
        return hasDependent && !hasAlternate;
    }

    internal static string EffectiveSha1(ChdContainerInfo info)
    {
        if (info == null)
            return "";
        string sha1 = Hex(info.Sha1);
        return !string.IsNullOrWhiteSpace(sha1) ? sha1 : Hex(info.RawSha1);
    }

    internal static string Hex(byte[] value)
    {
        if (value == null || value.Length == 0)
            return "";
        bool any = false;
        for (int i = 0; i < value.Length; i++)
            any |= value[i] != 0;
        return any ? string.Concat(value.Select(item => item.ToString("x2"))) : "";
    }

    private static int CompareCandidate(string left, string right, string preferredDirectory)
    {
        bool leftPreferred = string.Equals(Path.GetDirectoryName(left), preferredDirectory, StringComparison.OrdinalIgnoreCase);
        bool rightPreferred = string.Equals(Path.GetDirectoryName(right), preferredDirectory, StringComparison.OrdinalIgnoreCase);
        if (leftPreferred != rightPreferred)
            return leftPreferred ? -1 : 1;
        return StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    private static void RemoveIdentity(string identity, string path)
    {
        if (string.IsNullOrWhiteSpace(identity) || !BySha1.TryGetValue(identity, out HashSet<string> paths))
            return;
        paths.Remove(path);
        if (paths.Count == 0)
            BySha1.Remove(identity);
    }

    private sealed class RegisteredChd
    {
        public RegisteredChd(string path, ChdContainerInfo info, string identity, long length, long lastWriteUtcTicks)
        {
            Path = path;
            Info = info;
            Identity = identity;
            Length = length;
            LastWriteUtcTicks = lastWriteUtcTicks;
        }

        public string Path { get; }
        public ChdContainerInfo Info { get; }
        public string Identity { get; }
        public long Length { get; }
        public long LastWriteUtcTicks { get; }
    }
}
