using System;
using System.Collections.Generic;
using System.IO;

namespace RomVaultCore.Utils;

internal enum ChdArtifactKind
{
    Stage,
    Final,
    Backup,
    Manifest,
    Raw,
    Encoded,
    Child,
    Standalone
}

internal sealed class ChdArtifactPathSet
{
    private readonly Dictionary<ChdArtifactKind, string> _paths;

    internal ChdArtifactPathSet(string token, Dictionary<ChdArtifactKind, string> paths)
    {
        Token = token;
        _paths = paths;
    }

    public string Token { get; }

    public string this[ChdArtifactKind kind] => _paths[kind];

    public IEnumerable<string> Paths => _paths.Values;
}

/// <summary>
/// Allocates compact transaction artifacts beside a destination CHD.  Keeping
/// the artifacts in the destination directory preserves same-volume replace
/// semantics without repeating an arbitrarily long destination filename.
/// </summary>
internal static class ChdArtifactPaths
{
    internal static bool IsOwnedArtifactName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string[] prefixes = { ".__rvs.", ".__rvf.", ".__rvb.", ".__rvm.", ".__rvr.", ".__rve.", ".__rvc.", ".__rvx." };
        string[] suffixes = { ".chd", ".chd", ".chd", ".bin", ".img", ".chd", ".chd", ".chd" };
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (!name.StartsWith(prefixes[i], StringComparison.Ordinal) ||
                !name.EndsWith(suffixes[i], StringComparison.Ordinal) ||
                name.Length != prefixes[i].Length + 32 + suffixes[i].Length)
                continue;
            string token = name.Substring(prefixes[i].Length, 32);
            return token.Length == 32 && Guid.TryParseExact(token, "N", out _);
        }
        return false;
    }

    public static bool TryAllocate(string destinationPath, out ChdArtifactPathSet artifacts, out string error, params ChdArtifactKind[] kinds)
    {
        artifacts = null;
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("The CHD destination path is empty.", nameof(destinationPath));
            if (kinds == null || kinds.Length == 0)
                throw new ArgumentException("No CHD transaction artifacts were requested.", nameof(kinds));

            string fullDestination = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullDestination);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidDataException("The CHD destination directory could not be resolved.");

            HashSet<ChdArtifactKind> uniqueKinds = new HashSet<ChdArtifactKind>(kinds);
            if (uniqueKinds.Count != kinds.Length)
                throw new ArgumentException("A CHD transaction artifact kind was requested more than once.", nameof(kinds));

            for (int attempt = 0; attempt < 16; attempt++)
            {
                string token = Guid.NewGuid().ToString("N");
                Dictionary<ChdArtifactKind, string> paths = new Dictionary<ChdArtifactKind, string>();
                bool collision = false;
                foreach (ChdArtifactKind kind in uniqueKinds)
                {
                    string candidate = Path.Combine(directory, FileName(kind, token));
                    if (!ChdmanService.TryValidateExternalPaths(out error, candidate))
                        return false;
                    if (File.Exists(candidate) || Directory.Exists(candidate))
                    {
                        collision = true;
                        break;
                    }
                    paths.Add(kind, candidate);
                }

                if (collision)
                    continue;

                artifacts = new ChdArtifactPathSet(token, paths);
                return true;
            }

            error = "Could not allocate unique CHD transaction filenames after repeated attempts.";
            return false;
        }
        catch (Exception ex)
        {
            error = "Could not allocate CHD transaction filenames: " + ex.Message;
            return false;
        }
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        try
        {
            string root = Path.GetPathRoot(Path.GetTempPath());
            if (string.IsNullOrWhiteSpace(root))
                root = Path.DirectorySeparatorChar.ToString();
            int paddingLength = 175 - root.Length;
            if (paddingLength < 1 || paddingLength > 240)
                throw new InvalidDataException("Could not construct the long-path regression fixture.");

            string directory = Path.Combine(root, new string('d', paddingLength));
            string destination = Path.Combine(directory, new string('g', 60) + ".chd");
            if (directory.Length != 175 || destination.Length != 240)
                throw new InvalidDataException("The long-path regression fixture has unexpected dimensions.");
            if ((destination + ".__rvstage." + new string('a', 32) + ".chd").Length < 260)
                throw new InvalidDataException("The regression fixture does not exceed the legacy Windows path limit.");
            if (Path.DirectorySeparatorChar == '\\' &&
                ChdmanService.TryValidateExternalPaths(out _, destination + new string('x', 20)))
                throw new InvalidDataException("The external-tool path preflight accepted a 260-character Windows path.");

            ChdArtifactKind[] kinds = (ChdArtifactKind[])Enum.GetValues(typeof(ChdArtifactKind));
            if (!TryAllocate(destination, out ChdArtifactPathSet first, out error, kinds))
                return false;
            if (!TryAllocate(destination, out ChdArtifactPathSet second, out error, kinds))
                return false;

            HashSet<string> firstPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in first.Paths)
            {
                if (!string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A CHD transaction artifact left the destination directory.");
                if (path.Length >= 260)
                    throw new InvalidDataException("A compact CHD transaction artifact still exceeds the compatible Windows path limit.");
                if (Path.GetFileName(path).Contains(Path.GetFileName(destination)))
                    throw new InvalidDataException("A CHD transaction artifact repeats the destination filename.");
                if (!firstPaths.Add(path))
                    throw new InvalidDataException("CHD transaction artifact paths are not unique.");
            }
            foreach (string path in second.Paths)
                if (firstPaths.Contains(path))
                    throw new InvalidDataException("Independent CHD transactions reused an artifact path.");
            foreach (string path in first.Paths)
                if (!IsOwnedArtifactName(Path.GetFileName(path)))
                    throw new InvalidDataException("A compact CHD transaction artifact is not recognized as application-owned.");
            if (IsOwnedArtifactName(".__rvs.not-a-guid.chd") || IsOwnedArtifactName(Path.GetFileName(destination)))
                throw new InvalidDataException("The CHD artifact recognizer accepted an ordinary filename.");

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string FileName(ChdArtifactKind kind, string token)
    {
        switch (kind)
        {
            case ChdArtifactKind.Stage: return ".__rvs." + token + ".chd";
            case ChdArtifactKind.Final: return ".__rvf." + token + ".chd";
            case ChdArtifactKind.Backup: return ".__rvb." + token + ".chd";
            case ChdArtifactKind.Manifest: return ".__rvm." + token + ".bin";
            case ChdArtifactKind.Raw: return ".__rvr." + token + ".img";
            case ChdArtifactKind.Encoded: return ".__rve." + token + ".chd";
            case ChdArtifactKind.Child: return ".__rvc." + token + ".chd";
            case ChdArtifactKind.Standalone: return ".__rvx." + token + ".chd";
            default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown CHD artifact kind.");
        }
    }
}
