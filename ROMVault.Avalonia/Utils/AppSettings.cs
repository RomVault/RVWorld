using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ROMVault.Avalonia.Utils
{
    /// <summary>
    /// Manages application settings, persisting them to an XML file.
    /// This is a simple key-value store for saving user preferences like window positions.
    /// </summary>
    public static class AppSettings
    {
        private static Dictionary<string, string> _settings = new Dictionary<string, string>();
        private static string _filePath = "TrrntZipSettings.xml";

        /// <summary>
        /// Initializes static members of the <see cref="AppSettings"/> class.
        /// Loads settings from disk on startup.
        /// </summary>
        static AppSettings()
        {
            LoadSettings();
        }

        /// <summary>
        /// Loads the settings from the XML file.
        /// </summary>
        private static void LoadSettings()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<Entry>));
                    using (FileStream fs = new FileStream(_filePath, FileMode.Open))
                    {
                        var entries = serializer.Deserialize(fs) as List<Entry>;
                        if (entries != null)
                        {
                            foreach (var entry in entries)
                            {
                                if (entry.Key != null && entry.Value != null)
                                {
                                    _settings[entry.Key] = entry.Value;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors for now
                }
            }
        }

        /// <summary>
        /// Saves the current settings to the XML file.
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                var entries = new List<Entry>();
                foreach (var kvp in _settings)
                {
                    entries.Add(new Entry { Key = kvp.Key, Value = kvp.Value });
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<Entry>));
                using (FileStream fs = new FileStream(_filePath, FileMode.Create))
                {
                    serializer.Serialize(fs, entries);
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Reads a setting value by key.
        /// </summary>
        /// <param name="key">The key of the setting to retrieve.</param>
        /// <returns>The value if found, otherwise null.</returns>
        public static string? ReadSetting(string key)
        {
            if (_settings.ContainsKey(key))
            {
                return _settings[key];
            }
            return null;
        }

        /// <summary>
        /// Adds or updates a setting and saves changes to disk.
        /// </summary>
        /// <param name="key">The key of the setting.</param>
        /// <param name="value">The value to save.</param>
        public static void AddUpdateAppSettings(string key, string value)
        {
            if (value == null) return;
            _settings[key] = value;
            SaveSettings();
        }

        /// <summary>
        /// Represents a single key-value entry for XML serialization.
        /// </summary>
        public class Entry
        {
            public string? Key { get; set; }
            public string? Value { get; set; }
        }
    }
}
