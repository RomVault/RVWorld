using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RomVaultCore.Utils;

internal static class ChdDescriptorStager
{
    private static readonly Regex CueFileLine = new Regex(
        @"^(?<prefix>\s*FILE\s+)(?:""(?<quoted>[^""]+)""|(?<plain>\S+))(?<suffix>\s+.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryStageCueWithAsciiNames(string sourceCue, string workingRoot, out string stagedCue, out string stagedRoot, out string error)
    {
        stagedCue = "";
        stagedRoot = "";
        error = "";
        try
        {
            string source = Path.GetFullPath(sourceCue);
            string sourceRoot = Path.GetDirectoryName(source) ?? Environment.CurrentDirectory;
            stagedRoot = Path.Combine(Directory.Exists(workingRoot) ? workingRoot : Path.GetTempPath(), "rv-ascii-cue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagedRoot);
            string[] lines = File.ReadAllLines(source);
            Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int next = 1;
            int rewritten = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = CueFileLine.Match(lines[i] ?? "");
                if (!match.Success) continue;
                string referenced = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
                string full = Path.GetFullPath(Path.Combine(sourceRoot, referenced.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
                string boundedRoot = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(boundedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                    throw new InvalidDataException("Unsafe or missing CUE member: " + referenced);
                if (!aliases.TryGetValue(full, out string alias))
                {
                    string extension = Path.GetExtension(full).ToLowerInvariant();
                    if (!Regex.IsMatch(extension, @"^\.[a-z0-9]{1,8}$")) extension = ".bin";
                    alias = "track-" + next++.ToString("D3") + extension;
                    File.Copy(full, Path.Combine(stagedRoot, alias), false);
                    aliases.Add(full, alias);
                }
                lines[i] = match.Groups["prefix"].Value + "\"" + alias + "\"" + match.Groups["suffix"].Value;
                rewritten++;
            }
            if (rewritten == 0)
                throw new InvalidDataException("CUE contains no readable FILE statements.");
            stagedCue = Path.Combine(stagedRoot, "disc.cue");
            File.WriteAllLines(stagedCue, lines, new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            TryDelete(stagedRoot);
            stagedCue = "";
            stagedRoot = "";
            return false;
        }
    }

    public static void TryDelete(string path)
    {
        try { if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
