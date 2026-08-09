using RomVaultCore;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.Services;

namespace ROMVault.Avalonia.ViewModels;

/// <summary>
/// Immutable presentation projection for one row in the selected game's ROM list.
/// </summary>
public sealed class RomRowViewModel
{
    public RomRowViewModel(RvFile source, string displayName)
    {
        Source = source;
        DisplayName = displayName;
        IconAssetName = RomIconService.GetAssetName(source);
        Merge = source.Merge ?? string.Empty;
        Size = source.Size;
        Crc32 = source.CRC32;
        Sha1 = source.SHA1Hex;
        Md5 = source.MD5Hex;
        AltSize = source.AltSize;
        AltCrc32 = source.AltCRC32;
        AltSha1 = source.AltSHA1Hex;
        AltMd5 = source.AltMD5Hex;
        Status = source.Status ?? string.Empty;
        Modified = source.FileModDate;
        ZipIndex = source.ZipIndex;
        InstanceCount = source.InstanceCount;
        RepStatus = source.RepStatus;
    }

    public RvFile Source { get; }
    public string DisplayName { get; }
    public string? IconAssetName { get; }
    public string Merge { get; }
    public ulong? Size { get; }
    public string Crc32 { get; }
    public string Sha1 { get; }
    public string Md5 { get; }
    public ulong? AltSize { get; }
    public string AltCrc32 { get; }
    public string AltSha1 { get; }
    public string AltMd5 { get; }
    public string Status { get; }
    public string Modified { get; }
    public int ZipIndex { get; }
    public int InstanceCount { get; }
    public RepStatus RepStatus { get; }
}
