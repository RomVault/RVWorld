using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.IO;

namespace ROMVault.Avalonia.Views;

/// <summary>
/// Avalonia report window for CHD verification and mapping diagnostics.
/// </summary>
/// <remarks>
/// Mirrors the WinForms CHD verify experience:
/// - tabular member hash view
/// - mapping view
/// - raw textual report for copy/save workflows
/// </remarks>
public partial class ChdVerifyWindow : Window
{
    /// <summary>
    /// Row model for CHD member hash output.
    /// </summary>
    public class Entry
    {
        public string? Name { get; set; }
        public string? Size { get; set; }
        public string? CRC { get; set; }
        public string? SHA1 { get; set; }
        public string? MD5 { get; set; }
    }

    /// <summary>
    /// Row model for CHD member mapping diagnostics.
    /// </summary>
    public class MapEntry
    {
        public string? Expected { get; set; }
        public string? Extracted { get; set; }
        public string? Reason { get; set; }
    }

    public ChdVerifyWindow()
    {
        InitializeComponent();
        btnClose.Click += BtnCloseClick;
        btnCopy.Click += BtnCopyClick;
        btnSave.Click += BtnSaveClick;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void BtnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Populates the window controls from a verification report.
    /// </summary>
    /// <param name="report">Report text.</param>
    /// <param name="status">Short status summary shown above the report.</param>
    public void SetReport(string report, string status)
    {
        txtReport.Text = report ?? "";
        string mode = TryFindMode(report);
        string desc = TryFindDescriptorSource(report);
        string extra = "";
        if (!string.IsNullOrWhiteSpace(mode))
            extra = mode;
        if (!string.IsNullOrWhiteSpace(desc))
            extra = extra.Length == 0 ? desc : (extra + ", " + desc);
        lblStatus.Text = string.IsNullOrWhiteSpace(extra) ? (status ?? "") : ((status ?? "") + " (" + extra + ")");
        try
        {
            var list = ParseEntries(report);
            grid.ItemsSource = list;
            var map = ParseMapping(report);
            mapGrid.ItemsSource = map;
        }
        catch
        {
        }
    }

    private async void BtnCopyClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;
        await topLevel.Clipboard.SetTextAsync(txtReport.Text ?? "");
    }

    private async void BtnSaveClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save CHD Report",
            SuggestedFileName = "chd-report.txt",
            DefaultExtension = "txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });
        if (file == null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(txtReport.Text ?? "");
    }

    /// <summary>
    /// Parses flat report lines into table rows for member hash display.
    /// </summary>
    private static System.Collections.Generic.List<Entry> ParseEntries(string? report)
    {
        var items = new System.Collections.Generic.List<Entry>();
        if (string.IsNullOrWhiteSpace(report))
            return items;
        var lines = report.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains(" size=") && line.Contains(" crc=") && line.Contains(" sha1="))
            {
                string name = line;
                int sp = name.IndexOf(" size=", System.StringComparison.OrdinalIgnoreCase);
                if (sp > 0) name = name.Substring(0, sp).Trim();
                string size = GetToken(line, "size=");
                string crc = GetToken(line, "crc=");
                string sha1 = GetToken(line, "sha1=");
                string md5 = GetToken(line, "md5=");
                items.Add(new Entry { Name = name, Size = size, CRC = crc, SHA1 = sha1, MD5 = md5 });
            }
        }
        return items;
    }

    /// <summary>
    /// Parses the "mapping:" section into mapping rows.
    /// </summary>
    private static System.Collections.Generic.List<MapEntry> ParseMapping(string? report)
    {
        var items = new System.Collections.Generic.List<MapEntry>();
        if (string.IsNullOrWhiteSpace(report))
            return items;
        var lines = report.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        bool inMap = false;
        foreach (var line in lines)
        {
            if (line.Trim().Equals("mapping:", System.StringComparison.OrdinalIgnoreCase))
            {
                inMap = true;
                continue;
            }
            if (!inMap)
                continue;
            var parts = line.Split('|');
            if (parts.Length >= 3)
            {
                items.Add(new MapEntry
                {
                    Expected = parts[0].Trim(),
                    Extracted = parts[1].Trim(),
                    Reason = parts[2].Trim()
                });
            }
        }
        return items;
    }

    private static string GetToken(string line, string token)
    {
        int i = line.IndexOf(token, System.StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        i += token.Length;
        int e = line.IndexOf(' ', i);
        if (e < 0) e = line.Length;
        return line.Substring(i, e - i).Trim();
    }

    private static string TryFindMode(string? report)
    {
        if (string.IsNullOrWhiteSpace(report))
            return "";
        var lines = report.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("mode=", System.StringComparison.OrdinalIgnoreCase))
                return lines[i].Substring(5).Trim();
        }
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("streamMode=", System.StringComparison.OrdinalIgnoreCase))
                return lines[i].Trim();
        }
        return "";
    }

    private static string TryFindDescriptorSource(string? report)
    {
        if (string.IsNullOrWhiteSpace(report))
            return "";
        var lines = report.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("descriptorSource=", System.StringComparison.OrdinalIgnoreCase))
                return lines[i].Trim();
        }
        return "";
    }
}
