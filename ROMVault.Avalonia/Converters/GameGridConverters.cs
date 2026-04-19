using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RomVaultCore;
using RomVaultCore.RvDB;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ROMVault.Avalonia.Converters;



/// <summary>
/// Defines color constants and helper methods for the ROMVault UI.
/// </summary>
public static class RvColors
{
    public static readonly Color CBlue = Color.FromRgb(214, 214, 255);
    public static readonly Color CGreyBlue = Color.FromRgb(214, 224, 255);
    public static readonly Color CRed = Color.FromRgb(255, 214, 214);
    public static readonly Color CBrightRed = Color.FromRgb(255, 0, 0);
    public static readonly Color CGreen = Color.FromRgb(214, 255, 214);
    public static readonly Color CNeonGreen = Color.FromRgb(100, 255, 100);
    public static readonly Color CLightRed = Color.FromRgb(255, 235, 235);
    public static readonly Color CSoftGreen = Color.FromRgb(150, 200, 150);
    public static readonly Color CGrey = Color.FromRgb(214, 214, 214);
    public static readonly Color CCyan = Color.FromRgb(214, 255, 255);
    public static readonly Color CCyanGrey = Color.FromRgb(214, 225, 225);
    public static readonly Color CMagenta = Color.FromRgb(255, 214, 255);
    public static readonly Color CBrown = Color.FromRgb(140, 80, 80);
    public static readonly Color CPurple = Color.FromRgb(214, 140, 214);
    public static readonly Color CYellow = Color.FromRgb(255, 255, 214);
    public static readonly Color CDarkYellow = Color.FromRgb(255, 255, 100);
    public static readonly Color COrange = Color.FromRgb(255, 214, 140);
    public static readonly Color CWhite = Color.FromRgb(255, 255, 255);

    /// <summary>
    /// dims the color if dark mode is enabled.
    /// </summary>
    /// <param name="c">The color to dim.</param>
    /// <returns>The adjusted color.</returns>
    public static Color Down(Color c)
    {
        if (Settings.rvSettings.Darkness)
        {
            return Color.FromRgb(
                (byte)(c.R * 0.8),
                (byte)(c.G * 0.8),
                (byte)(c.B * 0.8));
        }
        return c;
    }

    public static Color[] DisplayColor;
    public static Color[] FontColor;

    static RvColors()
    {
        DisplayColor = new Color[(int)RepStatus.EndValue];
        FontColor = new Color[(int)RepStatus.EndValue];

        DisplayColor[(int)RepStatus.UnScanned] = CBlue;

        DisplayColor[(int)RepStatus.DirCorrect] = CGreen;
        DisplayColor[(int)RepStatus.DirMissing] = CRed;
        DisplayColor[(int)RepStatus.DirCorrupt] = CBrightRed;

        DisplayColor[(int)RepStatus.Missing] = CRed;
        DisplayColor[(int)RepStatus.Correct] = CGreen;
        DisplayColor[(int)RepStatus.CorrectMIA] = CNeonGreen;
        DisplayColor[(int)RepStatus.NotCollected] = CGrey;
        DisplayColor[(int)RepStatus.UnNeeded] = CCyanGrey;
        DisplayColor[(int)RepStatus.Unknown] = CCyan;
        DisplayColor[(int)RepStatus.InToSort] = CMagenta;

        DisplayColor[(int)RepStatus.MissingMIA] = CSoftGreen;

        DisplayColor[(int)RepStatus.Corrupt] = CBrightRed;
        DisplayColor[(int)RepStatus.Ignore] = CGreyBlue;

        DisplayColor[(int)RepStatus.CanBeFixed] = CYellow;
        DisplayColor[(int)RepStatus.CanBeFixedMIA] = CDarkYellow;
        DisplayColor[(int)RepStatus.MoveToSort] = CPurple;
        DisplayColor[(int)RepStatus.Delete] = CBrown;
        DisplayColor[(int)RepStatus.NeededForFix] = COrange;
        DisplayColor[(int)RepStatus.Rename] = COrange;

        DisplayColor[(int)RepStatus.CorruptCanBeFixed] = CYellow;
        DisplayColor[(int)RepStatus.MoveToCorrupt] = CPurple;

        DisplayColor[(int)RepStatus.Incomplete] = CLightRed;

        DisplayColor[(int)RepStatus.Deleted] = CWhite;

        for (int i = 0; i < (int)RepStatus.EndValue; i++)
        {
            // Force Black for everything except specifically dark backgrounds
            // This is safer for pastel colors
            if (i == (int)RepStatus.DirCorrupt || 
                i == (int)RepStatus.Corrupt || 
                i == (int)RepStatus.Delete)
            {
                 FontColor[i] = Colors.White;
            }
            else
            {
                 FontColor[i] = Colors.Black;
            }
        }
    }

    private static Color Contrasty(Color a)
    {
        // Simple luminance check  
        return (a.R * 0.299 + a.G * 0.587 + a.B * 0.114) > 128 ? Colors.Black : Colors.White;
    }
}

/// <summary>
/// Converts a file/directory status to a background color for the game grid.
/// </summary>
public class GameGridBackgroundConverter : IValueConverter
{
    private static bool _cachedDarkness;
    private static SolidColorBrush[]? _cachedBrushes;

    private static void EnsureBrushCache()
    {
        bool darkness = Settings.rvSettings.Darkness;
        if (_cachedBrushes != null && _cachedDarkness == darkness)
            return;

        _cachedDarkness = darkness;
        _cachedBrushes = new SolidColorBrush[RvColors.DisplayColor.Length];
        for (int i = 0; i < RvColors.DisplayColor.Length; i++)
        {
            _cachedBrushes[i] = new SolidColorBrush(RvColors.Down(RvColors.DisplayColor[i]));
        }
    }

    /// <summary>
    /// Converts the value.
    /// </summary>
    /// <param name="value">The RvFile to evaluate.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional parameter.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>A SolidColorBrush based on the status.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        EnsureBrushCache();
        if (value is RvFile tRvDir)
        {
            if (tRvDir.GotStatus == GotStatus.FileLocked)
            {
                return _cachedBrushes![(int)RepStatus.UnScanned];
            }

            foreach (RepStatus t1 in RepairStatus.DisplayOrder)
            {
                if (tRvDir.DirStatus.Get(t1) <= 0)
                    continue;

                return _cachedBrushes![(int)t1];
            }
        }
        return Brushes.Transparent;
    }

    /// <summary>
    /// Converts back. Not implemented.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a file/directory status to a foreground (text) color for the game grid.
/// </summary>
public class GameGridForegroundConverter : IValueConverter
{
    private static SolidColorBrush[]? _cachedBrushes;

    private static void EnsureBrushCache()
    {
        if (_cachedBrushes != null)
            return;

        _cachedBrushes = new SolidColorBrush[RvColors.FontColor.Length];
        for (int i = 0; i < RvColors.FontColor.Length; i++)
        {
            _cachedBrushes[i] = new SolidColorBrush(RvColors.FontColor[i]);
        }
    }

    /// <summary>
    /// Converts the value.
    /// </summary>
    /// <param name="value">The RvFile to evaluate.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional parameter.</param>
    /// <param name="culture">Culture info.</param>
    /// <returns>A SolidColorBrush for the text.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        EnsureBrushCache();
        if (value is RvFile tRvDir)
        {
            if (tRvDir.GotStatus == GotStatus.FileLocked)
            {
                return _cachedBrushes![(int)RepStatus.UnScanned];
            }

            foreach (RepStatus t1 in RepairStatus.DisplayOrder)
            {
                if (tRvDir.DirStatus.Get(t1) <= 0)
                    continue;

                return _cachedBrushes![(int)t1];
            }
        }
        // If no status matched, assume default text color (likely white in dark theme)
        // But if background is transparent, white is fine.
        return null; 
    }

    /// <summary>
    /// Converts back. Not implemented.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Produces an extras badge for a game directory.
/// </summary>
/// <remarks>
/// Returns small labels like ART or TXT based on detected sidecar content.
/// </remarks>
public class GameExtrasBadgeConverter : IValueConverter
{
    private static readonly Dictionary<RvFile, string?> Cache = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RvFile dir)
            return null;

        if (Cache.TryGetValue(dir, out var cached))
            return cached;

        bool hasText = false;
        bool hasArt = false;

        int limit = Math.Min(dir.ChildCount, 400);
        for (int i = 0; i < limit; i++)
        {
            var child = dir.Child(i);
            if (child.GotStatus != GotStatus.Got)
                continue;

            string name = child.Name ?? "";
            if (!hasText)
            {
                if (name.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".diz", StringComparison.OrdinalIgnoreCase))
                    hasText = true;
            }

            if (!hasArt)
            {
                if (name.StartsWith("Artwork/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Artwork\\", StringComparison.OrdinalIgnoreCase))
                    hasArt = true;
            }

            if (hasText && hasArt)
                break;
        }

        string? result = null;
        if (hasArt) result = "ART";
        else if (hasText) result = "TXT";

        Cache[dir] = result;
        return result;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
