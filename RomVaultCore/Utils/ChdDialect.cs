using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RomVaultCore.Utils;

internal static class ChdDialect
{
    public static string DetectSource(string inputPath, string family)
    {
        string normalizedFamily = (family ?? "").Trim().ToLowerInvariant();
        if (normalizedFamily == "raw") return "raw-exact";
        if (normalizedFamily == "hdd") return "hard-disk-exact";
        if (normalizedFamily == "laserdisc") return "canonical-avi-exact";

        string extension = Path.GetExtension(inputPath ?? "").ToLowerInvariant();
        if (extension == ".gdi") return "tosec-gdi";
        if (extension == ".toc") return "toc-exact";
        if (extension == ".iso") return normalizedFamily == "psp" ? "psp-iso" : "iso";
        if (extension != ".cue") return "unknown";
        if (normalizedFamily == "gdi") return "redump-gdrom-cue";

        string text = File.ReadAllText(inputPath);
        bool unicode = (inputPath ?? "").Any(value => value > 127) || text.Any(value => value > 127);
        bool complexLayout = Regex.IsMatch(text, @"^\s*(?:INDEX\s+00|PREGAP|POSTGAP|FLAGS\s+.*\bPRE\b)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (complexLayout) return "cue-complex-layout";
        if (unicode) return "cue-unicode";
        bool mode12048 = Regex.IsMatch(text, @"\bMODE1/2048\b", RegexOptions.IgnoreCase);
        bool mode12352 = Regex.IsMatch(text, @"\bMODE1/2352\b", RegexOptions.IgnoreCase);
        bool mode22352 = Regex.IsMatch(text, @"\bMODE2/2352\b", RegexOptions.IgnoreCase);
        bool audio = Regex.IsMatch(text, @"^\s*TRACK\s+\d+\s+AUDIO\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        int dataModes = (mode12048 ? 1 : 0) + (mode12352 ? 1 : 0) + (mode22352 ? 1 : 0);
        if (dataModes > 1) return "cue-mixed";
        if (mode12048) return audio ? "cue-mode1-2048-audio" : "cue-mode1-2048";
        if (mode22352) return audio ? "cue-mode2-2352-audio" : "cue-mode2-2352";
        if (mode12352) return audio ? "cue-mode1-2352-audio" : "cue-mode1-2352";
        if (audio) return "cue-audio";
        return "cue-unknown";
    }
}
