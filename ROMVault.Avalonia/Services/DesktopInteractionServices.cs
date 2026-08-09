using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.Services;

public interface IDesktopShellService
{
    void OpenPath(string path);
    void ShowInFolder(string path);
    void OpenUrl(string url);
    string ResolvePath(string path);
}

public sealed class DesktopShellService : IDesktopShellService
{
    public static DesktopShellService Instance { get; } = new();

    public void OpenPath(string path)
    {
        string resolved = ResolvePath(path);
        if (File.Exists(resolved))
        {
            ShowInFolder(resolved);
            return;
        }

        if (!Directory.Exists(resolved))
        {
            return;
        }

        Start(new ProcessStartInfo
        {
            FileName = resolved,
            UseShellExecute = true
        });
    }

    public void ShowInFolder(string path)
    {
        string resolved = ResolvePath(path);
        if (OperatingSystem.IsWindows())
        {
            string argument = File.Exists(resolved)
                ? $"/select,\"{resolved}\""
                : $"\"{resolved}\"";
            Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
            return;
        }

        string directory = File.Exists(resolved)
            ? Path.GetDirectoryName(resolved) ?? resolved
            : resolved;
        Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS URLs are supported.", nameof(url));
        }

        Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (path.StartsWith("RomRoot\\", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("ToSort\\", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("DatRoot\\", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        return Path.GetFullPath(path);
    }

    public void OpenContainer(RvFile container)
    {
        ArgumentNullException.ThrowIfNull(container);
        string path = ResolvePath(container.FullNameCase);
        if (File.Exists(path))
        {
            ShowInFolder(path);
        }
        else if (Directory.Exists(path))
        {
            OpenPath(path);
        }
    }

    private static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            FileNotFoundException)
        {
            Debug.WriteLine(exception);
        }
    }
}

public interface IClipboardService
{
    Task SetTextAsync(Control anchor, string text);
}

public sealed class AvaloniaClipboardService : IClipboardService
{
    public static AvaloniaClipboardService Instance { get; } = new();

    public async Task SetTextAsync(Control anchor, string text)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (TopLevel.GetTopLevel(anchor)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
