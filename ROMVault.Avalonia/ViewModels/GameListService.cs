using System;
using System.Collections.Generic;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.ViewModels;

public static class GameListService
{
    public static List<GameRowViewModel> CreateRows(RvFile directory)
    {
        var rows = new List<GameRowViewModel>();
        for (int index = 0; index < directory.ChildCount; index++)
        {
            RvFile child = directory.Child(index);
            if (child.IsDirectory)
            {
                rows.Add(new GameRowViewModel(child));
            }
        }

        return rows;
    }

    public static List<GameRowViewModel> FilterAndSort(
        IReadOnlyList<GameRowViewModel> rows,
        GameVisibilityOptions visibility,
        GameFilter filter,
        string? sortHeader,
        bool ascending)
    {
        var result = new List<GameRowViewModel>(rows.Count);
        foreach (GameRowViewModel row in rows)
        {
            if (GameFilterService.IsVisible(row.StatusFlags, visibility) &&
                GameFilterService.Matches(filter, row.SearchValues))
            {
                result.Add(row);
            }
        }

        Sort(result, sortHeader, ascending);
        return result;
    }

    public static void Sort(List<GameRowViewModel> rows, string? header, bool ascending)
    {
        if (string.IsNullOrWhiteSpace(header) || rows.Count <= 1)
        {
            return;
        }

        Comparison<GameRowViewModel> comparison = header switch
        {
            "Type" => (left, right) => left.FileType.CompareTo(right.FileType),
            "Game (Directory / Zip)" => (left, right) => CompareText(left.Name, right.Name),
            "Description" => (left, right) => CompareText(left.Description, right.Description),
            "Modified" => (left, right) => left.Source.FileModTimeStamp.CompareTo(right.Source.FileModTimeStamp),
            "ROM Status" => (left, right) => left.StatusSortWeight.CompareTo(right.StatusSortWeight),
            "Extras" => (left, right) => string.Compare(left.Extras, right.Extras, StringComparison.OrdinalIgnoreCase),
            _ => (left, right) => CompareText(left.Name, right.Name)
        };

        rows.Sort((left, right) => ascending
            ? comparison(left, right)
            : comparison(right, left));
    }

    private static int CompareText(string left, string right) =>
        string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
}
