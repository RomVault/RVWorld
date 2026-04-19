using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RomVaultCore.RvDB;
using RomVaultCore;

namespace ROMVault.Avalonia.Converters
{
    /// <summary>
    /// Converts a Report Status (RepStatus) to a background Brush color.
    /// </summary>
    public class ReportStatusToBrushConverter : IValueConverter
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
                _cachedBrushes[i] = new SolidColorBrush(Down(RvColors.DisplayColor[i]));
            }
        }

        /// <summary>
        /// Dims the color if dark mode is enabled.
        /// </summary>
        /// <param name="c">The color to adjust.</param>
        /// <returns>The adjusted color.</returns>
        private static Color Down(Color c)
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

        /// <summary>
        /// Converts a RepStatus to a SolidColorBrush.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The parameter.</param>
        /// <param name="culture">The culture info.</param>
        /// <returns>A SolidColorBrush corresponding to the status.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RepStatus status)
            {
                int index = (int)status;
                if (index >= 0 && index < RvColors.DisplayColor.Length)
                {
                    EnsureBrushCache();
                    return _cachedBrushes![index];
                }
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a Report Status (RepStatus) to a foreground Brush color (text color).
    /// </summary>
    public class ReportStatusToForegroundConverter : IValueConverter
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
        /// Converts a RepStatus to a SolidColorBrush for text.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The parameter.</param>
        /// <param name="culture">The culture info.</param>
        /// <returns>A SolidColorBrush for the text.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RepStatus status)
            {
                int index = (int)status;
                if (index >= 0 && index < RvColors.FontColor.Length)
                {
                     EnsureBrushCache();
                     return _cachedBrushes![index];
                }
            }
            // Default to null to allow inheritance if no status matched, or Black if explicit
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
