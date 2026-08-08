using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CHDSharpLib;
using RomVaultCore.Scanner;

namespace RomVaultCore.Utils;

public static class ChdScrubber
{
    public static int Scrub(string path, ChdScrubMode mode, out string report)
    {
        List<string> files = new List<string>();
        try
        {
            if (File.Exists(path)) files.Add(Path.GetFullPath(path));
            else if (Directory.Exists(path)) files.AddRange(Directory.GetFiles(path, "*.chd", SearchOption.AllDirectories));
            else
            {
                report = "CHD scrub target was not found.";
                return 2;
            }
        }
        catch (Exception ex)
        {
            report = "Could not enumerate CHD scrub target: " + ex.Message;
            return 2;
        }

        StringBuilder output = new StringBuilder();
        output.AppendLine("RomVault CHD scrub");
        output.AppendLine("mode=" + mode.ToString().ToLowerInvariant());
        output.AppendLine("containers=" + files.Count);
        int failed = 0;
        for (int i = 0; i < files.Count; i++)
        {
            ChdOperationResult result = ScrubOne(files[i], mode);
            output.AppendLine(ChdDiagnosticFormatter.RedactPath(files[i]) + " " + result);
            if (!result.Success) failed++;
        }
        output.AppendLine("passed=" + (files.Count - failed));
        output.AppendLine("failed=" + failed);
        report = output.ToString().TrimEnd();
        return failed == 0 ? 0 : 5;
    }

    private static ChdOperationResult ScrubOne(string path, ChdScrubMode mode)
    {
        string chdman = ChdmanProcessTracker.FindExecutable();
        string work = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        ChdmanRunResult verify = ChdmanService.Run(chdman, "verify -i " + ChdmanService.Quote(path), work, 600000);
        if (!verify.Success)
            return FromRun(verify, "container", "chdman container verification failed");
        if (!ChdMetadata.TryReadContainerInfo(path, out _, out string metadataError))
            return ChdOperationResult.Fail(ChdErrorCode.InvalidContainer, "metadata", "CHD metadata traversal failed.", metadataError);
        if (mode == ChdScrubMode.Container)
            return ChdOperationResult.Ok("container", "verified");

        string nativeHash;
        try
        {
            using (Stream logical = ChdLogicalStream.OpenRead(path))
                nativeHash = HashSha256(logical);
        }
        catch (Exception ex)
        {
            return ChdOperationResult.Fail(ChdErrorCode.InvalidContainer, "native", "Native logical-stream verification failed.", ex.Message);
        }
        if (mode == ChdScrubMode.Native)
        {
            ChdHealthStore.RecordParity(path, "", "", ToolSha(chdman), nativeHash, "", "native-scrub", false);
            return ChdOperationResult.Ok("native", "logical stream read successfully");
        }

        string family = "";
        string storage = "";
        if (ChdEncodingProfile.TryRead(path, out ChdEncodingProfile embedded))
        {
            family = embedded.Family ?? "";
            storage = embedded.Storage ?? "";
        }
        string temp = Path.Combine(Path.GetTempPath(), "rv-chd-scrub-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            if (family == "dvd" || family == "psp" || family == "raw" || family == "hdd")
            {
                IChdExtractor extractor = new ChdmanChdExtractor(chdman, temp);
                string extracted = Path.Combine(temp, family == "dvd" || family == "psp" ? "image.iso" : family == "hdd" ? "image.img" : "image.raw");
                bool ok;
                string error;
                if (family == "dvd" || family == "psp") ok = extractor.ExtractDvd(path, extracted, out error);
                else if (family == "hdd") ok = extractor.ExtractHardDisk(path, extracted, out error);
                else ok = extractor.ExtractRaw(path, extracted, out error);
                if (!ok || !File.Exists(extracted))
                    return ChdOperationResult.Fail(ChdErrorCode.ToolFailed, "external", "External extraction failed.", error);
                string externalHash;
                using (Stream stream = File.OpenRead(extracted)) externalHash = HashSha256(stream);
                bool parity = string.Equals(nativeHash, externalHash, StringComparison.OrdinalIgnoreCase);
                ChdHealthStore.RecordParity(path, family, storage, ToolSha(chdman), nativeHash, externalHash, "native-external-scrub", parity);
                return parity
                    ? ChdOperationResult.Ok("full", "native and external payloads match")
                    : ChdOperationResult.Fail(ChdErrorCode.PayloadMismatch, "parity", "Native and external payload hashes differ.");
            }

            int reportCode = ChdVerify.TryGenerateReport(path, out string verifyReport);
            ChdHealthStore.RecordParity(path, family, storage, ToolSha(chdman), nativeHash, "", "external-structural-scrub", reportCode == 0);
            return reportCode == 0
                ? ChdOperationResult.Ok("full", "native verification and canonical external extraction passed")
                : ChdOperationResult.Fail(ChdErrorCode.PayloadMismatch, "external", "Canonical extraction verification failed.", verifyReport);
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
    }

    private static ChdOperationResult FromRun(ChdmanRunResult result, string phase, string message)
    {
        ChdErrorCode code = result.Cancelled ? ChdErrorCode.Cancelled : result.TimedOut ? ChdErrorCode.Timeout : ChdErrorCode.ToolFailed;
        return ChdOperationResult.Fail(code, phase, message, result.Output);
    }

    private static string ToolSha(string executable)
    {
        return ChdmanService.TryGetIdentity(executable, ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out _) ? identity.BinarySha256 : "";
    }

    internal static string HashSha256(Stream stream)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            return string.Concat(hash.Select(value => value.ToString("x2")));
        }
    }
}
