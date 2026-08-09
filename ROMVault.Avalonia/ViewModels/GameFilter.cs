using System;
using System.Collections.Generic;

namespace ROMVault.Avalonia.ViewModels;

[Flags]
public enum GameStatusFlags
{
    None = 0,
    Correct = 1 << 0,
    Missing = 1 << 1,
    Fixes = 1 << 2,
    Mia = 1 << 3,
    Merged = 1 << 4,
    Unknown = 1 << 5,
    InToSort = 1 << 6,
    Corrupt = 1 << 7
}

public sealed class GameFilter
{
    private GameFilter(string? freeText, string? descriptionText, HashSet<string> statuses)
    {
        FreeText = freeText;
        DescriptionText = descriptionText;
        Statuses = statuses;
    }

    public string? FreeText { get; }

    public string? DescriptionText { get; }

    public IReadOnlySet<string> Statuses { get; }

    public static GameFilter Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new GameFilter(null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        string? descriptionText = null;
        var statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var freeTextParts = new List<string>();

        foreach (string part in text.Split(
                     ' ',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("desc:", StringComparison.OrdinalIgnoreCase))
            {
                string value = part[5..];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    descriptionText = value;
                }

                continue;
            }

            if (part.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
            {
                string value = part[7..];
                foreach (string status in value.Split(
                             ',',
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    statuses.Add(status);
                }

                continue;
            }

            freeTextParts.Add(part);
        }

        string? freeText = freeTextParts.Count == 0
            ? null
            : string.Join(' ', freeTextParts);

        return new GameFilter(freeText, descriptionText, statuses);
    }
}

public readonly record struct GameSearchValues(
    string Name,
    string Description,
    GameStatusFlags StatusFlags);

public readonly record struct GameVisibilityOptions(
    bool ShowComplete,
    bool ShowPartial,
    bool ShowEmpty,
    bool ShowFixes,
    bool ShowMia,
    bool ShowMerged);
