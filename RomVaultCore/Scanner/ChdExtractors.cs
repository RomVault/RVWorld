using System;
using System.Collections.Generic;
using System.IO;
using RomVaultCore.Utils;

namespace RomVaultCore.Scanner;

public interface IChdExtractor
{
    bool ExtractDvd(string chdPath, string outIsoPath, out string error);
    bool ExtractCd(string chdPath, string outDescriptorPath, out string error);
    bool ExtractRaw(string chdPath, string outputPath, out string error);
    bool ExtractHardDisk(string chdPath, string outputPath, out string error);
    bool ExtractLaserDisc(string chdPath, string outputPath, out string error);
    bool Info(string chdPath, out string infoText);
}

public sealed class ChdmanChdExtractor : IChdExtractor
{
    private readonly string _chdman;
    private readonly string _workingDir;

    public ChdmanChdExtractor(string chdmanPath, string workingDir)
    {
        _chdman = chdmanPath;
        _workingDir = workingDir;
    }

    public bool ExtractDvd(string chdPath, string outIsoPath, out string error)
    {
        string absChd = Path.GetFullPath(NormalizePossiblyConcatenatedPath(chdPath));
        string absOut = Path.GetFullPath(outIsoPath);
        return Run($"extractdvd -i {ChdmanService.Quote(absChd)} -o {ChdmanService.Quote(absOut)} -f", out error);
    }

    public bool ExtractCd(string chdPath, string outDescriptorPath, out string error)
    {
        string absChd = Path.GetFullPath(NormalizePossiblyConcatenatedPath(chdPath));
        string absOut = Path.GetFullPath(outDescriptorPath);
        string outputDirectory = Path.GetDirectoryName(absOut) ?? _workingDir;
        string extension = Path.GetExtension(absOut);
        string trackPattern = string.Equals(extension, ".gdi", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(outputDirectory, "track%02t.bin")
            : Path.Combine(outputDirectory, "track (Track %02t).bin");

        // chdman 0.265 and newer can emit one file per track.  For GD-ROMs,
        // CUE output uses the Redump layout while GDI output uses the native
        // TOSEC layout.  The descriptor extension selects the same-family
        // round trip.  No explicit conversion flag is needed, which keeps this
        // compatible with both released chdman builds and current MAME master.
        // Split output is required because merged BIN output cannot be compared
        // directly with split-track DAT hashes.
        bool ok = Run($"extractcd -i {ChdmanService.Quote(absChd)} -o {ChdmanService.Quote(absOut)} -ob {ChdmanService.Quote(trackPattern)} -sb -f", out error);
        if (!ok)
        {
            error = (error ?? "").Trim() + Environment.NewLine +
                    "Split-track CHD extraction requires a current chdman (MAME 0.265 or newer).";
        }
        return ok;
    }

    public bool ExtractRaw(string chdPath, string outputPath, out string error) =>
        ExtractSingle("extractraw", chdPath, outputPath, out error);

    public bool ExtractHardDisk(string chdPath, string outputPath, out string error) =>
        ExtractSingle("extracthd", chdPath, outputPath, out error);

    public bool ExtractLaserDisc(string chdPath, string outputPath, out string error) =>
        ExtractSingle("extractld", chdPath, outputPath, out error);

    private bool ExtractSingle(string command, string chdPath, string outputPath, out string error)
    {
        string absChd = Path.GetFullPath(NormalizePossiblyConcatenatedPath(chdPath));
        string absOut = Path.GetFullPath(outputPath);
        return Run($"{command} -i {ChdmanService.Quote(absChd)} -o {ChdmanService.Quote(absOut)} -f", out error);
    }

    public bool Info(string chdPath, out string infoText)
    {
        string absChd = Path.GetFullPath(NormalizePossiblyConcatenatedPath(chdPath));
        return Run($"info -i {ChdmanService.Quote(absChd)}", out infoText);
    }

    private static string NormalizePossiblyConcatenatedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        string p = path.Trim();
        int last = -1;
        for (int i = 0; i + 2 < p.Length; i++)
        {
            char c0 = p[i];
            char c1 = p[i + 1];
            char c2 = p[i + 2];
            if (((c0 >= 'A' && c0 <= 'Z') || (c0 >= 'a' && c0 <= 'z')) && c1 == ':' && (c2 == '\\' || c2 == '/'))
                last = i;
        }
        if (last > 0)
            return p.Substring(last);
        return p;
    }

    private bool Run(string args, out string output)
    {
        ChdmanRunResult result = ChdmanService.Run(_chdman, args, _workingDir);
        output = result.Output;
        return result.Success;
    }
}
