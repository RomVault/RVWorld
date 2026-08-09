using Avalonia.Media;
using Compress;
using Compress.ZipFile;
using DATReader.DatClean;
using RomVaultCore;

namespace ROMVault.Avalonia.ViewModels;

public sealed class DatRuleRowViewModel
{
    public DatRuleRowViewModel(DatRule rule) => Rule = rule;

    public DatRule Rule { get; }
    public string DirKey => Rule.DirKey;
    public ZipStructure CompressionSub => Rule.CompressionSub;
    public MergeType Merge => Rule.Merge;
    public bool SingleArchive => Rule.SingleArchive;
    public IBrush Background { get; set; } = Brushes.Transparent;
}

public sealed class DirectoryMappingRowViewModel
{
    public DirectoryMappingRowViewModel(DirMapping mapping)
    {
        Mapping = mapping;
    }

    public DirMapping Mapping { get; }
    public string DirKey => Mapping.DirKey;
    public string DirPath => Mapping.DirPath;
    public IBrush Background { get; set; } = Brushes.Transparent;
}
