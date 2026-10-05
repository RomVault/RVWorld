using Compress.StructuredZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace SAM_UI_Avalonia.Models;



/// <summary>
/// The archive format produced by the conversion.
/// </summary>
public enum OutputType
{
    [Description("Zip-Torrent")]
    ZipTorrent,

    [Description("Zip-ZSTD")]
    ZipZstd,

    [Description("7Z-ZSTD")]
    SevenZipZstd,

    [Description("7Z-ZSTD-Solid")]
    SevenZipZstdSolid,

    [Description("7Z-LZMA")]
    SevenZipLzma,

    [Description("7Z-LZMA-Solid")]
    SevenZipLzmaSolid,

    [Description("Repair keep original")]
    RepairKeepOriginal
}


public class ComboOptions
{
    public static ZipStructure ZipStructureFromUIIndex(OutputType cboIndex)
    {
        switch (cboIndex)
        {
            case OutputType.ZipTorrent: return ZipStructure.ZipTrrnt;
            case OutputType.ZipZstd: return ZipStructure.ZipZSTD;
            case OutputType.SevenZipZstd: return ZipStructure.SevenZipNZSTD;
            case OutputType.SevenZipZstdSolid: return ZipStructure.SevenZipSZSTD;
            case OutputType.SevenZipLzma: return ZipStructure.SevenZipNLZMA;
            case OutputType.SevenZipLzmaSolid: return ZipStructure.SevenZipSLZMA;
            case OutputType.RepairKeepOriginal: return ZipStructure.None; // this is the repair option, and is not used as an output
            default: return ZipStructure.ZipTrrnt;
        }
    }
}

/// <summary>
/// Pairs an enum value with the text shown in the UI.
/// </summary>
public sealed class EnumOption<TEnum> where TEnum : struct, Enum
{
    public EnumOption(TEnum value)
    {
        Value = value;
        Display = GetDescription(value);
    }

    public TEnum Value { get; }

    public string Display { get; }

    /// <summary>
    /// Builds a display list covering every value of <typeparamref name="TEnum"/>.
    /// </summary>
    public static IReadOnlyList<EnumOption<TEnum>> CreateAll() =>
        Enum.GetValues<TEnum>().Select(v => new EnumOption<TEnum>(v)).ToList();

    private static string GetDescription(TEnum value)
    {
        MemberInfo? member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
    }
}
