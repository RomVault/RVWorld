using System;
using System.Collections.Generic;
using System.Linq;
using Compress.StructuredZip;
using RomVaultCore;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.ViewModels;

public sealed record RomColumnVisibility(
    bool Merge,
    bool AlternateHashes,
    bool Status,
    bool Modified);

public sealed record RomListProjection(
    IReadOnlyList<RomRowViewModel> Rows,
    RomColumnVisibility Columns);

public static class RomListService
{
    public static RomListProjection Create(RvFile game, bool includeMerged)
    {
        ArgumentNullException.ThrowIfNull(game);

        var rows = new List<RomRowViewModel>();
        AddDirectory(game, string.Empty, includeMerged, rows);

        bool showMerge = rows.Any(row => !string.IsNullOrWhiteSpace(row.Merge));
        bool showAlternates = rows.Any(row =>
            row.AltSize is not null ||
            !string.IsNullOrWhiteSpace(row.AltCrc32) ||
            !string.IsNullOrWhiteSpace(row.AltSha1) ||
            !string.IsNullOrWhiteSpace(row.AltMd5));
        bool showStatus = rows.Any(row => !string.IsNullOrWhiteSpace(row.Status));
        bool showModified = rows.Any(row => IsMeaningfulTimestamp(row.Source.FileModTimeStamp));

        return new RomListProjection(
            rows,
            new RomColumnVisibility(showMerge, showAlternates, showStatus, showModified));
    }

    public static IReadOnlyList<RomRowViewModel> Sort(
        IReadOnlyList<RomRowViewModel> rows,
        string? header,
        bool ascending)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return rows;
        }

        Func<RomRowViewModel, object?> selector = header switch
        {
            "ROM (File)" => row => row.DisplayName,
            "Merge" => row => row.Merge,
            "Size" => row => row.Size,
            "CRC32" => row => row.Crc32,
            "SHA1" => row => row.Sha1,
            "MD5" => row => row.Md5,
            "Alt Size" => row => row.AltSize,
            "Alt CRC32" => row => row.AltCrc32,
            "Alt SHA1" => row => row.AltSha1,
            "Alt MD5" => row => row.AltMd5,
            "Status" => row => row.Status,
            "Modified Date/Time" => row => row.Source.FileModTimeStamp,
            "Zip Index" => row => row.ZipIndex,
            "Instance Count" => row => row.InstanceCount,
            _ => row => row.DisplayName
        };

        IOrderedEnumerable<RomRowViewModel> sorted = ascending
            ? rows.OrderBy(selector, PresentationValueComparer.Instance)
            : rows.OrderByDescending(selector, PresentationValueComparer.Instance);
        return sorted.ToArray();
    }

    private static void AddDirectory(
        RvFile directory,
        string pathPrefix,
        bool includeMerged,
        ICollection<RomRowViewModel> rows)
    {
        for (int index = 0; index < directory.ChildCount; index++)
        {
            RvFile child = directory.Child(index);
            if (child.IsFile && ShouldInclude(child, includeMerged))
            {
                rows.Add(new RomRowViewModel(child, pathPrefix + child.Name));
            }

            if (directory.Dat is not null && child.IsDirectory && child.Game is null)
            {
                AddDirectory(child, pathPrefix + child.Name + "/", includeMerged, rows);
            }
        }
    }

    private static bool ShouldInclude(RvFile file, bool includeMerged) =>
        includeMerged ||
        file.DatStatus != DatStatus.InDatMerged ||
        file.RepStatus != RepStatus.NotCollected;

    private static bool IsMeaningfulTimestamp(long timestamp) =>
        timestamp != 0 &&
        timestamp != long.MinValue &&
        timestamp != StructuredZip.TrrntzipDateTime &&
        timestamp != StructuredZip.TrrntzipDosDateTime;

    private sealed class PresentationValueComparer : IComparer<object?>
    {
        public static PresentationValueComparer Instance { get; } = new();

        public int Compare(object? left, object? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            if (left is string leftText && right is string rightText)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(leftText, rightText);
            }

            if (left is IComparable comparable && left.GetType() == right.GetType())
            {
                return comparable.CompareTo(right);
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.ToString(), right.ToString());
        }
    }
}
