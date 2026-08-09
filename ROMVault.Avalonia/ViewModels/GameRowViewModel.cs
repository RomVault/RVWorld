using System;
using System.Collections.Generic;
using Compress;
using RomVaultCore;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.Converters;

namespace ROMVault.Avalonia.ViewModels;

public sealed record StatusBadgeViewModel(string? IconAssetName, int Count);

/// <summary>
/// Immutable presentation data for a game-grid row.
/// </summary>
public sealed class GameRowViewModel
{
    public GameRowViewModel(RvFile source)
    {
        Source = source;
        Name = source.Name ?? string.Empty;
        Description = Normalize(source.Game?.GetData(RvGame.GameData.Description));
        Modified = source.DateTime;
        Extras = GetExtras(source);
        StatusFlags = GetStatusFlags(source);
        PrimaryStatus = GetPrimaryStatus(source);
        StatusBadges = GetStatusBadges(source);
        StatusSortWeight = source.DirStatus.CountMissing() * 100_000L +
                           source.DirStatus.CountCanBeFixed() * 1_000L +
                           source.DirStatus.CountUnknown();
    }

    public RvFile Source { get; }

    public FileType FileType => Source.FileType;

    public string Name { get; }

    public string Description { get; }

    public string Modified { get; }

    public string Extras { get; }

    public GameStatusFlags StatusFlags { get; }

    public RepStatus? PrimaryStatus { get; }

    public IReadOnlyList<StatusBadgeViewModel> StatusBadges { get; }

    public long StatusSortWeight { get; }

    public GameSearchValues SearchValues => new(Name, Description, StatusFlags);

    private static string Normalize(string? value) =>
        string.IsNullOrEmpty(value) || value == "¤" ? string.Empty : value;

    private static GameStatusFlags GetStatusFlags(RvFile source)
    {
        ReportStatus status = source.DirStatus;
        GameStatusFlags flags = GameStatusFlags.None;

        if (status.HasCorrect()) flags |= GameStatusFlags.Correct;
        if (status.HasMissing(false)) flags |= GameStatusFlags.Missing;
        if (status.HasFixesNeeded()) flags |= GameStatusFlags.Fixes;
        if (status.HasMIA()) flags |= GameStatusFlags.Mia;
        if (status.HasAllMerged()) flags |= GameStatusFlags.Merged;
        if (status.HasUnknown()) flags |= GameStatusFlags.Unknown;
        if (status.HasInToSort()) flags |= GameStatusFlags.InToSort;
        if (source.GotStatus == GotStatus.Corrupt) flags |= GameStatusFlags.Corrupt;

        return flags;
    }

    private static RepStatus? GetPrimaryStatus(RvFile source)
    {
        if (source.GotStatus == GotStatus.FileLocked)
        {
            return RepStatus.UnScanned;
        }

        EnsureDisplayOrder();
        if (RepairStatus.DisplayOrder is null)
        {
            return null;
        }

        foreach (RepStatus status in RepairStatus.DisplayOrder)
        {
            if (source.DirStatus.Get(status) > 0)
            {
                return status;
            }
        }

        return null;
    }

    private static IReadOnlyList<StatusBadgeViewModel> GetStatusBadges(RvFile source)
    {
        EnsureDisplayOrder();
        if (RepairStatus.DisplayOrder is null)
        {
            return Array.Empty<StatusBadgeViewModel>();
        }

        var badges = new List<StatusBadgeViewModel>();
        foreach (RepStatus status in RepairStatus.DisplayOrder)
        {
            int count = source.DirStatus.Get(status);
            if (count > 0)
            {
                badges.Add(new StatusBadgeViewModel(GetStatusIconAssetName(status), count));
            }
        }

        return badges;
    }

    private static void EnsureDisplayOrder()
    {
        if (RepairStatus.DisplayOrder is null)
        {
            RepairStatus.InitStatusCheck();
        }
    }

    private static string? GetStatusIconAssetName(RepStatus status)
    {
        string statusName = status.ToString();
        string[] candidates =
        {
            $"G_{statusName}",
            statusName,
            statusName.Replace("Dir", string.Empty, StringComparison.Ordinal)
        };

        foreach (string candidate in candidates)
        {
            if (AssetBitmapCache.Get(candidate) is not null)
            {
                return candidate;
            }
        }

        return status == RepStatus.DirCorrect ? "Dir" : null;
    }

    private static string GetExtras(RvFile source)
    {
        bool hasText = false;
        bool hasArtwork = false;
        int limit = Math.Min(source.ChildCount, 400);

        for (int index = 0; index < limit; index++)
        {
            RvFile child = source.Child(index);
            if (child.GotStatus != GotStatus.Got)
            {
                continue;
            }

            string name = child.Name ?? string.Empty;
            hasText |= name.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith(".diz", StringComparison.OrdinalIgnoreCase);
            hasArtwork |= name.StartsWith("Artwork/", StringComparison.OrdinalIgnoreCase) ||
                          name.StartsWith("Artwork\\", StringComparison.OrdinalIgnoreCase);

            if (hasText && hasArtwork)
            {
                break;
            }
        }

        return hasArtwork ? "ART" : hasText ? "TXT" : string.Empty;
    }
}
