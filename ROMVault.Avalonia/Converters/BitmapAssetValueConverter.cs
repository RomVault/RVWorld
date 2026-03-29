using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RomVaultCore.RvDB;
using Compress;

namespace ROMVault.Avalonia.Converters
{
    /// <summary>
    /// Converts a string asset name or an RvFile object into a Bitmap image.
    /// Used for displaying icons in the UI based on file type and status.
    /// </summary>
    public class BitmapAssetValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts the value to a Bitmap.
        /// </summary>
        /// <param name="value">The value to convert (string or RvFile).</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The parameter.</param>
        /// <param name="culture">The culture info.</param>
        /// <returns>A <see cref="Bitmap"/> if found, otherwise null.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? assetName = null;

            if (value is string name)
            {
                assetName = name;
            }
            else if (value is RvFile rvFile)
            {
                assetName = GetBitmapFromType(rvFile.FileType, rvFile.newZipStruct);
                
                // Handle "Missing" suffix logic if needed (simplified check based on assets)
                if (assetName != null && (assetName.StartsWith("Zip") || assetName.StartsWith("SevenZip")))
                {
                     if (rvFile.GotStatus == GotStatus.NotGot)
                     {
                         // Check if Missing asset exists
                         if (AssetLoader.Exists(new Uri($"avares://ROMVault.Avalonia/Assets/{assetName}Missing.png")))
                         {
                             assetName += "Missing";
                         }
                     }
                }
            }

            if (!string.IsNullOrEmpty(assetName))
            {
                try
                {
                    var uri = new Uri($"avares://ROMVault.Avalonia/Assets/{assetName}.png");
                    if (AssetLoader.Exists(uri))
                    {
                        return new Bitmap(AssetLoader.Open(uri));
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Determines the asset name suffix based on file type and zip structure.
        /// </summary>
        /// <param name="ft">The file type.</param>
        /// <param name="zs">The zip structure.</param>
        /// <returns>The asset name base string.</returns>
        private string? GetBitmapFromType(FileType ft, ZipStructure zs)
        {
            switch (ft)
            {
                case FileType.Zip:
                    if (zs == ZipStructure.None) { return "Zip"; }
                    if (zs == ZipStructure.ZipTrrnt) { return "ZipTrrnt"; }
                    if (zs == ZipStructure.ZipTDC) { return "ZipTDC"; }
                    if (zs == ZipStructure.ZipZSTD) { return "ZipZSTD"; }
                    return "Zip";
                case FileType.SevenZip:
                    if (zs == ZipStructure.None) { return "SevenZip"; }
                    if (zs == ZipStructure.SevenZipTrrnt) { return "SevenZipTrrnt"; }
                    if (zs == ZipStructure.SevenZipSLZMA) { return "SevenZipSLZMA"; }
                    if (zs == ZipStructure.SevenZipNLZMA) { return "SevenZipNLZMA"; }
                    if (zs == ZipStructure.SevenZipSZSTD) { return "SevenZipSZSTD"; }
                    if (zs == ZipStructure.SevenZipNZSTD) { return "SevenZipNZSTD"; }
                    return "SevenZip";
                case FileType.Dir:
                    return "Dir";
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
