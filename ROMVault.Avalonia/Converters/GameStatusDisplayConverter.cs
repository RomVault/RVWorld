using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RomVaultCore;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.Converters
{
    /// <summary>
    /// Represents a single status item to be displayed (Icon + Count).
    /// </summary>
    public class GameStatusItem
    {
        /// <summary>
        /// The icon representing the status.
        /// </summary>
        public Bitmap? Icon { get; set; }

        /// <summary>
        /// The count of items with this status.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Converts an RvFile (Directory) into a list of GameStatusItems for display in the UI.
    /// Used to show icons and counts for missing, correct, corrupt, etc. items.
    /// </summary>
    public class GameStatusDisplayConverter : IValueConverter
    {
        /// <summary>
        /// Converts the value.
        /// </summary>
        /// <param name="value">The value to convert (RvFile).</param>
        /// <param name="targetType">The type of the target.</param>
        /// <param name="parameter">The parameter.</param>
        /// <param name="culture">The culture info.</param>
        /// <returns>A list of <see cref="GameStatusItem"/>.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RvFile tRvDir)
            {
                if (tRvDir.DirStatus == null) return null;

                var items = new List<GameStatusItem>();

                // Ensure RepairStatus is initialized
                if (RepairStatus.DisplayOrder == null)
                {
                    RepairStatus.InitStatusCheck();
                }

                if (RepairStatus.DisplayOrder == null) return null;

                foreach (RepStatus status in RepairStatus.DisplayOrder)
                {
                    int count = tRvDir.DirStatus.Get(status);
                    
                    if (count <= 0) continue;

                    Bitmap? icon = null;
                    
                    // Try different naming conventions for the asset
                    string[] assetNames = new[] 
                    { 
                        $"G_{status}",      // Standard (G_Missing, G_Correct)
                        $"{status}",        // Direct (DirMissing)
                        status.ToString().Replace("Dir", "") // Fallback (DirCorrect -> Correct?) - risky, maybe manual map better
                    };

                    foreach (string name in assetNames)
                    {
                        try
                        {
                            var uri = new Uri($"avares://ROMVault.Avalonia/Assets/{name}.png");
                            if (AssetLoader.Exists(uri))
                            {
                                icon = new Bitmap(AssetLoader.Open(uri));
                                break;
                            }
                        }
                        catch { }
                    }

                    // Manual overrides if still null
                    if (icon == null && status.ToString() == "DirCorrect")
                    {
                         try { icon = new Bitmap(AssetLoader.Open(new Uri("avares://ROMVault.Avalonia/Assets/Dir.png"))); } catch {}
                    }

                    if (icon != null)
                    {
                        items.Add(new GameStatusItem { Icon = icon, Count = count });
                    }
                    else
                    {
                        // Fallback: Add item without icon if count > 0, so at least the number shows up
                        items.Add(new GameStatusItem { Icon = null, Count = count });
                    }
                }
                return items;
            }
            return null;
        }

        /// <summary>
        /// Converts the value back. Not implemented.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
