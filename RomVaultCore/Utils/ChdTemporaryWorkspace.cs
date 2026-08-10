using System;
using System.IO;

namespace RomVaultCore.Utils;

internal enum ChdWorkspacePurpose
{
    Scan,
    Verify,
    Hash
}

/// <summary>
/// Creates short-lived CHD workspaces.  Source-local storage is preferred so
/// large extraction preflights use the mapped volume, with an isolated
/// per-user temporary fallback for read-only or overly long source mappings.
/// </summary>
internal static class ChdTemporaryWorkspace
{
    public static bool TryCreateForSource(string sourcePath, ChdWorkspacePurpose purpose, out string workspace, out string error)
    {
        workspace = "";
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("The source CHD path is empty.", nameof(sourcePath));

            string fullSourcePath = Path.GetFullPath(sourcePath);
            string sourceDirectory = Path.GetDirectoryName(fullSourcePath);
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException("The source CHD directory was not found.");

            string fallbackRoot = Path.Combine(Path.GetTempPath(), "RomVault", "chd-workspaces");
            return TryCreateInRoots(sourceDirectory, fallbackRoot, purpose, out workspace, out error);
        }
        catch (Exception ex)
        {
            workspace = "";
            error = ex.Message;
            return false;
        }
    }

    public static bool TryCreateForRoot(string preferredRoot, ChdWorkspacePurpose purpose, out string workspace, out string error)
    {
        return TryCreateForRoot(preferredRoot, purpose, 0, out workspace, out error);
    }

    public static bool TryCreateForRoot(string preferredRoot, ChdWorkspacePurpose purpose, long minimumAvailableBytes, out string workspace, out string error)
    {
        workspace = "";
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(preferredRoot))
                throw new ArgumentException("The preferred CHD workspace root is empty.", nameof(preferredRoot));

            string fullPreferredRoot = Path.GetFullPath(preferredRoot);
            string fallbackRoot = Path.Combine(Path.GetTempPath(), "RomVault", "chd-workspaces");
            return TryCreateInRoots(fullPreferredRoot, fallbackRoot, purpose, minimumAvailableBytes, out workspace, out error);
        }
        catch (Exception ex)
        {
            workspace = "";
            error = ex.Message;
            return false;
        }
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-workspace-test-" + Guid.NewGuid().ToString("N"));
        string sourceWorkspace = "";
        string fallbackWorkspace = "";
        string hashWorkspace = "";
        try
        {
            string sourceRoot = Path.Combine(root, "source");
            string fallbackRoot = Path.Combine(root, "fallback");
            Directory.CreateDirectory(sourceRoot);
            string source = Path.Combine(sourceRoot, "mapped-suite.chd");
            File.WriteAllBytes(source, new byte[] { 0 });

            if (!TryCreateForSource(source, ChdWorkspacePurpose.Scan, out sourceWorkspace, out error))
                return false;
            if (!string.Equals(Path.GetDirectoryName(sourceWorkspace), sourceRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A writable CHD source did not receive a source-local workspace.");
            File.WriteAllText(Path.Combine(sourceWorkspace, "payload.tmp"), "test");
            Directory.Delete(sourceWorkspace, true);
            sourceWorkspace = "";

            if (!TryCreateForRoot(sourceRoot, ChdWorkspacePurpose.Hash, 1, out hashWorkspace, out error))
                return false;
            if (!string.Equals(Path.GetDirectoryName(hashWorkspace), sourceRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A writable hashing root did not receive its local CHD workspace.");
            if (!TryDelete(hashWorkspace, out error))
                return false;
            hashWorkspace = "";

            string blockedPreferred = Path.Combine(root, "blocked-preferred");
            File.WriteAllText(blockedPreferred, "not a directory");
            if (!TryCreateInRoots(blockedPreferred, fallbackRoot, ChdWorkspacePurpose.Verify, out fallbackWorkspace, out error))
                return false;
            if (!string.Equals(Path.GetDirectoryName(fallbackWorkspace), fallbackRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A blocked source-local workspace did not use the isolated fallback root.");
            if (Directory.GetDirectories(sourceRoot).Length != 0)
                throw new InvalidDataException("Fallback workspace allocation modified the source directory.");
            File.WriteAllText(Path.Combine(fallbackWorkspace, "payload.tmp"), "test");
            Directory.Delete(fallbackWorkspace, true);
            fallbackWorkspace = "";

            int longComponentLength = 185 - root.Length - 1;
            if (Path.DirectorySeparatorChar == '\\' && longComponentLength > 0 && longComponentLength <= 240)
            {
                string longPreferred = Path.Combine(root, new string('l', longComponentLength));
                Directory.CreateDirectory(longPreferred);
                if (!TryCreateInRoots(longPreferred, fallbackRoot, ChdWorkspacePurpose.Scan, out fallbackWorkspace, out error))
                    return false;
                if (!string.Equals(Path.GetDirectoryName(fallbackWorkspace), fallbackRoot, StringComparison.OrdinalIgnoreCase) ||
                    Directory.GetDirectories(longPreferred).Length != 0)
                    throw new InvalidDataException("An overly long source workspace did not route cleanly to the fallback root.");
                Directory.Delete(fallbackWorkspace, true);
                fallbackWorkspace = "";
            }

            if (TryCreateInRoots(sourceRoot, fallbackRoot, (ChdWorkspacePurpose)999, out _, out _))
                throw new InvalidDataException("An unknown CHD workspace purpose was accepted.");
            if (TryDelete(sourceRoot, out _) || !Directory.Exists(sourceRoot))
                throw new InvalidDataException("CHD workspace cleanup accepted a directory it does not own.");

            string blockedFallback = Path.Combine(root, "blocked-fallback");
            File.WriteAllText(blockedFallback, "not a directory");
            if (TryCreateInRoots(blockedPreferred, blockedFallback, ChdWorkspacePurpose.Scan, out _, out _))
                throw new InvalidDataException("CHD workspace allocation succeeded with both roots blocked.");

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            DeleteWorkspace(sourceWorkspace);
            DeleteWorkspace(fallbackWorkspace);
            DeleteWorkspace(hashWorkspace);
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static bool TryCreateInRoots(string preferredRoot, string fallbackRoot, ChdWorkspacePurpose purpose, out string workspace, out string error)
    {
        return TryCreateInRoots(preferredRoot, fallbackRoot, purpose, 0, out workspace, out error);
    }

    private static bool TryCreateInRoots(string preferredRoot, string fallbackRoot, ChdWorkspacePurpose purpose, long minimumAvailableBytes, out string workspace, out string error)
    {
        workspace = "";
        error = "";
        if (!TryGetPrefix(purpose, out string prefix))
        {
            error = "Unknown CHD workspace purpose.";
            return false;
        }

        if (TryCreateUnderRoot(preferredRoot, prefix, false, minimumAvailableBytes, out workspace, out string preferredError))
            return true;
        if (TryCreateUnderRoot(fallbackRoot, prefix, true, minimumAvailableBytes, out workspace, out string fallbackError))
            return true;

        error = "Could not create a writable CHD workspace in the preferred location or in the per-user temporary fallback. " +
                "Preferred-location error: " + preferredError + " Fallback error: " + fallbackError;
        return false;
    }

    private static bool TryCreateUnderRoot(string root, string prefix, bool createRoot, long minimumAvailableBytes, out string workspace, out string error)
    {
        workspace = "";
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new DirectoryNotFoundException("The workspace root is empty.");
            string fullRoot = Path.GetFullPath(root);
            if (createRoot)
                Directory.CreateDirectory(fullRoot);
            if (!Directory.Exists(fullRoot))
                throw new DirectoryNotFoundException("The workspace root is not an accessible directory.");

            for (int attempt = 0; attempt < 16; attempt++)
            {
                string candidate = Path.Combine(fullRoot, prefix + Guid.NewGuid().ToString("N"));
                string reservedOutput = Path.Combine(candidate, new string('x', 64));
                if (!ChdmanService.TryValidateExternalPaths(out string pathError, reservedOutput))
                    throw new PathTooLongException(pathError);
                if (Directory.Exists(candidate) || File.Exists(candidate))
                    continue;

                bool created = false;
                try
                {
                    Directory.CreateDirectory(candidate);
                    created = true;
                    string probe = Path.Combine(candidate, ".write-probe");
                    using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        stream.WriteByte(0x52);
                    File.Delete(probe);
                    if (minimumAvailableBytes > 0)
                    {
                        if (!ChdFreeSpace.TryGetAvailableBytes(candidate, out long availableBytes, out string spaceError))
                            throw new IOException(spaceError);
                        if (availableBytes < minimumAvailableBytes)
                            throw new IOException($"Insufficient free space for the CHD workspace. Required approximately {minimumAvailableBytes:N0} bytes; available {availableBytes:N0} bytes.");
                    }
                    workspace = candidate;
                    return true;
                }
                catch
                {
                    if (created)
                        DeleteWorkspace(candidate);
                    throw;
                }
            }

            throw new IOException("Could not allocate a unique workspace directory after repeated attempts.");
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryGetPrefix(ChdWorkspacePurpose purpose, out string prefix)
    {
        switch (purpose)
        {
            case ChdWorkspacePurpose.Scan:
                prefix = "__RomVault.chdscan.";
                return true;
            case ChdWorkspacePurpose.Verify:
                prefix = "__RomVault.chdverify.";
                return true;
            case ChdWorkspacePurpose.Hash:
                prefix = "__RomVault.chdhash.";
                return true;
            default:
                prefix = "";
                return false;
        }
    }

    private static void DeleteWorkspace(string workspace)
    {
        TryDelete(workspace, out _);
    }

    public static bool TryDelete(string workspace, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(workspace))
            return true;
        try
        {
            string fullPath = Path.GetFullPath(workspace);
            string name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!IsOwnedWorkspaceName(name))
                throw new InvalidOperationException("Refusing to delete a directory that is not an owned CHD workspace.");
            if (!Directory.Exists(fullPath))
                return true;
            if ((new DirectoryInfo(fullPath).Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing to recursively delete a reparse-point CHD workspace.");
            Directory.Delete(fullPath, true);
            return !Directory.Exists(fullPath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static bool IsOwnedWorkspaceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string[] prefixes = { "__RomVault.chdscan.", "__RomVault.chdverify.", "__RomVault.chdhash." };
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (!name.StartsWith(prefixes[i], StringComparison.Ordinal))
                continue;
            string token = name.Substring(prefixes[i].Length);
            return token.Length == 32 && Guid.TryParseExact(token, "N", out _);
        }
        return false;
    }
}
