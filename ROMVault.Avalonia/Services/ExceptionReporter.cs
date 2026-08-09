using System;
using System.Diagnostics;
using System.IO;

namespace ROMVault.Avalonia.Services;

/// <summary>
/// Best-effort process-wide exception logging that never masks the original failure.
/// </summary>
public static class ExceptionReporter
{
    private static readonly object Sync = new();

    public static void Write(Exception exception, string source)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RomVault",
                "Logs");
            Directory.CreateDirectory(directory);
            string entry = $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(Path.Combine(directory, "crash.log"), entry);
            }
        }
        catch (Exception loggingException)
        {
            Debug.WriteLine($"Unable to write RomVault crash log: {loggingException}");
        }
    }
}
