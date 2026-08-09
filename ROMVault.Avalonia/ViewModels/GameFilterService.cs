using System;

namespace ROMVault.Avalonia.ViewModels;

public static class GameFilterService
{
    public static bool IsVisible(GameStatusFlags status, GameVisibilityOptions options)
    {
        if (options.ShowComplete && IsComplete(status) ||
            options.ShowPartial && IsPartial(status) ||
            options.ShowEmpty && IsEmpty(status) ||
            options.ShowFixes && Has(status, GameStatusFlags.Fixes) ||
            options.ShowMia && Has(status, GameStatusFlags.Mia) ||
            options.ShowMerged && Has(status, GameStatusFlags.Merged))
        {
            return true;
        }

        GameStatusFlags alwaysVisible =
            GameStatusFlags.Unknown |
            GameStatusFlags.InToSort |
            GameStatusFlags.Corrupt;

        if ((status & alwaysVisible) != 0)
        {
            return true;
        }

        GameStatusFlags categorized =
            GameStatusFlags.Correct |
            GameStatusFlags.Missing |
            GameStatusFlags.Fixes |
            GameStatusFlags.Mia |
            GameStatusFlags.Merged;

        return (status & categorized) == 0;
    }

    public static bool IsComplete(GameStatusFlags status) =>
        Has(status, GameStatusFlags.Correct) &&
        !Has(status, GameStatusFlags.Missing) &&
        !Has(status, GameStatusFlags.Fixes);

    public static bool IsPartial(GameStatusFlags status) =>
        Has(status, GameStatusFlags.Missing) && Has(status, GameStatusFlags.Correct);

    public static bool IsEmpty(GameStatusFlags status) =>
        Has(status, GameStatusFlags.Missing) && !Has(status, GameStatusFlags.Correct);

    public static bool Matches(GameFilter filter, GameSearchValues values)
    {
        if (!string.IsNullOrWhiteSpace(filter.FreeText) &&
            !values.Name.Contains(filter.FreeText, StringComparison.OrdinalIgnoreCase) &&
            !values.Description.Contains(filter.FreeText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.DescriptionText) &&
            !values.Description.Contains(filter.DescriptionText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Statuses.Count == 0)
        {
            return true;
        }

        foreach (string status in filter.Statuses)
        {
            if (MatchesStatus(status, values.StatusFlags))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesStatus(string status, GameStatusFlags flags)
    {
        if (status.Equals("complete", StringComparison.OrdinalIgnoreCase)) return IsComplete(flags);
        if (status.Equals("partial", StringComparison.OrdinalIgnoreCase)) return IsPartial(flags);
        if (status.Equals("empty", StringComparison.OrdinalIgnoreCase)) return IsEmpty(flags);
        if (status.Equals("missing", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.Missing);
        if (status.Equals("fixes", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.Fixes);
        if (status.Equals("mia", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.Mia);
        if (status.Equals("merged", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.Merged);
        if (status.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.Unknown);
        if (status.Equals("intosort", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.InToSort);
        if (status.Equals("corrupt", StringComparison.OrdinalIgnoreCase)) return Has(flags, GameStatusFlags.Corrupt);
        return false;
    }

    private static bool Has(GameStatusFlags value, GameStatusFlags flag) => (value & flag) != 0;
}
