using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;

namespace ROMVault.Avalonia.Utils;

/// <summary>
/// Thread-safe UI preference storage with debounced, atomic persistence.
/// </summary>
public static class AppSettings
{
    private const int SaveDelayMilliseconds = 350;
    private const string LegacyFileName = "TrrntZipSettings.xml";
    private static readonly object Sync = new();
    private static readonly object PersistenceSync = new();
    private static readonly Dictionary<string, string> Settings = new(StringComparer.Ordinal);
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ROMVault",
        "ui-settings.xml");
    private static Timer? _saveTimer;

    static AppSettings()
    {
        LoadSettings();
    }

    public static string? ReadSetting(string key)
    {
        lock (Sync)
        {
            return Settings.TryGetValue(key, out string? value) ? value : null;
        }
    }

    public static void AddUpdateAppSettings(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (Sync)
        {
            Settings[key] = value;
            ScheduleSave();
        }
    }

    public static void AddUpdateAppSettings(IEnumerable<KeyValuePair<string, string>> values)
    {
        lock (Sync)
        {
            foreach ((string key, string value) in values)
            {
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    Settings[key] = value;
                }
            }

            ScheduleSave();
        }
    }

    /// <summary>
    /// Immediately persists pending changes. Call during a clean application shutdown.
    /// </summary>
    public static void Flush()
    {
        lock (Sync)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        SaveSettings();
    }

    private static void LoadSettings()
    {
        string legacyPath = Path.Combine(AppContext.BaseDirectory, LegacyFileName);
        string? sourcePath = File.Exists(FilePath)
            ? FilePath
            : File.Exists(legacyPath)
                ? legacyPath
                : File.Exists(LegacyFileName)
                    ? LegacyFileName
                    : null;

        if (sourcePath is null)
        {
            return;
        }

        try
        {
            var serializer = new XmlSerializer(typeof(List<Entry>));
            using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (serializer.Deserialize(stream) is not List<Entry> entries)
            {
                return;
            }

            lock (Sync)
            {
                foreach (Entry entry in entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value is not null)
                    {
                        Settings[entry.Key] = entry.Value;
                    }
                }

                if (!string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    ScheduleSave();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to load ROMVault UI settings: {ex}");
        }
    }

    private static void ScheduleSave()
    {
        _saveTimer ??= new Timer(_ => SaveSettings(), null, Timeout.Infinite, Timeout.Infinite);
        _saveTimer.Change(SaveDelayMilliseconds, Timeout.Infinite);
    }

    private static void SaveSettings()
    {
        List<Entry> snapshot;
        lock (Sync)
        {
            snapshot = Settings
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new Entry { Key = pair.Key, Value = pair.Value })
                .ToList();
        }

        lock (PersistenceSync)
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string temporaryPath = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                var serializer = new XmlSerializer(typeof(List<Entry>));
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    serializer.Serialize(stream, snapshot);
                    stream.Flush(true);
                }

                File.Move(temporaryPath, FilePath, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to save ROMVault UI settings: {ex}");
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception cleanupException)
                {
                    Debug.WriteLine($"Unable to clean up ROMVault UI settings temp file: {cleanupException}");
                }
            }
        }
    }

    public sealed class Entry
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
    }
}
