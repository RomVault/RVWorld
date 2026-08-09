using System;
using System.Globalization;
using Avalonia.Media.Imaging;
using Compress;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.Converters;

/// <summary>
/// Converts an asset name or ROMVault file into its packaged bitmap.
/// </summary>
public sealed class BitmapAssetValueConverter : OneWayValueConverter
{
    public override object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        string? assetName = value switch
        {
            string name => name,
            RvFile file => GetAssetName(file),
            _ => null
        };

        return string.IsNullOrWhiteSpace(assetName)
            ? null
            : AssetBitmapCache.Get(assetName);
    }

    private static string? GetAssetName(RvFile file)
    {
        string? assetName = GetAssetName(file.FileType, file.newZipStruct);
        if (assetName is null || file.GotStatus != GotStatus.NotGot || !IsArchiveAsset(assetName))
        {
            return assetName;
        }

        string missingAssetName = $"{assetName}Missing";
        return AssetBitmapCache.Get(missingAssetName) is null
            ? assetName
            : missingAssetName;
    }

    private static bool IsArchiveAsset(string assetName) =>
        assetName.StartsWith("Zip", StringComparison.Ordinal) ||
        assetName.StartsWith("SevenZip", StringComparison.Ordinal);

    private static string? GetAssetName(FileType fileType, ZipStructure zipStructure) =>
        (fileType, zipStructure) switch
        {
            (FileType.Zip, ZipStructure.ZipTrrnt) => "ZipTrrnt",
            (FileType.Zip, ZipStructure.ZipTDC) => "ZipTDC",
            (FileType.Zip, ZipStructure.ZipZSTD) => "ZipZSTD",
            (FileType.Zip, _) => "Zip",
            (FileType.SevenZip, ZipStructure.SevenZipTrrnt) => "SevenZipTrrnt",
            (FileType.SevenZip, ZipStructure.SevenZipSLZMA) => "SevenZipSLZMA",
            (FileType.SevenZip, ZipStructure.SevenZipNLZMA) => "SevenZipNLZMA",
            (FileType.SevenZip, ZipStructure.SevenZipSZSTD) => "SevenZipSZSTD",
            (FileType.SevenZip, ZipStructure.SevenZipNZSTD) => "SevenZipNZSTD",
            (FileType.SevenZip, _) => "SevenZip",
            (FileType.Dir, _) => "Dir",
            _ => null
        };
}
