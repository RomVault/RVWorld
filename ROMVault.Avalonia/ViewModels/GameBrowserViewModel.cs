using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ROMVault.Avalonia.ViewModels;

/// <summary>
/// Owns the presentation state for the game browser while the legacy window
/// continues to coordinate ROMVault's domain operations.
/// </summary>
public sealed class GameBrowserViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<GameRowViewModel> _allRows = Array.Empty<GameRowViewModel>();
    private IReadOnlyList<GameRowViewModel> _items = Array.Empty<GameRowViewModel>();
    private string _countText = "0/0";
    private string _sortText = string.Empty;
    private bool _isEmpty = true;
    private string _emptyMessage = "Select a directory to browse its games.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<GameRowViewModel> AllRows => _allRows;
    public IReadOnlyList<GameRowViewModel> Items => _items;
    public string CountText => _countText;
    public string SortText => _sortText;
    public bool IsEmpty => _isEmpty;
    public string EmptyMessage => _emptyMessage;

    public string CompleteLabel { get; private set; } = "Complete 0";
    public string PartialLabel { get; private set; } = "Partial 0";
    public string EmptyLabel { get; private set; } = "Empty 0";
    public string FixesLabel { get; private set; } = "Fixable 0";
    public string MiaLabel { get; private set; } = "MIA 0";
    public string MergedLabel { get; private set; } = "Merged 0";

    public void SetRows(IReadOnlyList<GameRowViewModel> rows)
    {
        _allRows = rows;
        UpdateCategoryLabels();
        OnPropertyChanged(nameof(AllRows));
    }

    public void Clear()
    {
        _allRows = Array.Empty<GameRowViewModel>();
        _items = Array.Empty<GameRowViewModel>();
        _countText = "0/0";
        _sortText = string.Empty;
        _isEmpty = true;
        _emptyMessage = "Select a directory to browse its games.";
        UpdateCategoryLabels();
        NotifyPresentationChanged();
        OnPropertyChanged(nameof(AllRows));
    }

    public void Apply(
        GameVisibilityOptions visibility,
        GameFilter filter,
        string? sortHeader,
        bool sortAscending,
        bool hasSearchText)
    {
        _items = GameListService.FilterAndSort(
            _allRows,
            visibility,
            filter,
            sortHeader,
            sortAscending);
        _countText = $"{_items.Count:N0}/{_allRows.Count:N0}";
        _sortText = string.IsNullOrWhiteSpace(sortHeader)
            ? string.Empty
            : $"Sort: {sortHeader} {(sortAscending ? "↑" : "↓")}";
        _isEmpty = _items.Count == 0;
        _emptyMessage = hasSearchText
            ? "No games match the current search and status filters."
            : "No games are available in this directory.";
        NotifyPresentationChanged();
    }

    private void UpdateCategoryLabels()
    {
        int complete = 0;
        int partial = 0;
        int empty = 0;
        int fixes = 0;
        int mia = 0;
        int merged = 0;

        foreach (GameRowViewModel row in _allRows)
        {
            GameStatusFlags status = row.StatusFlags;
            if (GameFilterService.IsComplete(status)) complete++;
            if (GameFilterService.IsPartial(status)) partial++;
            if (GameFilterService.IsEmpty(status)) empty++;
            if ((status & GameStatusFlags.Fixes) != 0) fixes++;
            if ((status & GameStatusFlags.Mia) != 0) mia++;
            if ((status & GameStatusFlags.Merged) != 0) merged++;
        }

        CompleteLabel = $"Complete {complete:N0}";
        PartialLabel = $"Partial {partial:N0}";
        EmptyLabel = $"Empty {empty:N0}";
        FixesLabel = $"Fixable {fixes:N0}";
        MiaLabel = $"MIA {mia:N0}";
        MergedLabel = $"Merged {merged:N0}";

        OnPropertyChanged(nameof(CompleteLabel));
        OnPropertyChanged(nameof(PartialLabel));
        OnPropertyChanged(nameof(EmptyLabel));
        OnPropertyChanged(nameof(FixesLabel));
        OnPropertyChanged(nameof(MiaLabel));
        OnPropertyChanged(nameof(MergedLabel));
    }

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(SortText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
