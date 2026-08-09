using System;
using Compress;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.Services;

public static class RomIconService
{
    public static string? GetAssetName(RvFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string? assetName = GetAssetName(file.FileType, file.newZipStruct);
        if (assetName is null || file.GotStatus != GotStatus.NotGot || !HasMissingVariant(assetName))
        {
            return assetName;
        }

        return $"{assetName}Missing";
    }

    private static bool HasMissingVariant(string assetName) => assetName is
        "ZipTrrnt" or
        "ZipTDC" or
        "ZipZSTD" or
        "SevenZipTrrnt" or
        "SevenZipSLZMA" or
        "SevenZipNLZMA" or
        "SevenZipSZSTD" or
        "SevenZipNZSTD";

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
