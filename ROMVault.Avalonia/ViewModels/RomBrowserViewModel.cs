using System;
using System.Collections.Generic;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.ViewModels;

public sealed class RomBrowserViewModel : ObservableObject
{
    private IReadOnlyList<RomRowViewModel> _sourceRows = Array.Empty<RomRowViewModel>();
    private IReadOnlyList<RomRowViewModel> _items = Array.Empty<RomRowViewModel>();
    private RomRowViewModel? _selectedItem;
    private RomColumnVisibility _columns = new(false, false, false, false);
    private string? _sortHeader;
    private bool _sortAscending = true;

    public IReadOnlyList<RomRowViewModel> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public RomRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsEmpty => Items.Count == 0;
    public string EmptyMessage => IsEmpty ? "This game has no ROM entries to display." : string.Empty;
    public bool ShowMergeColumn => _columns.Merge;
    public bool ShowAlternateColumns => _columns.AlternateHashes;
    public bool ShowStatusColumn => _columns.Status;
    public bool ShowModifiedColumn => _columns.Modified;

    public void SetGame(RvFile? game, bool includeMerged)
    {
        if (game is null)
        {
            _sourceRows = Array.Empty<RomRowViewModel>();
            _columns = new RomColumnVisibility(false, false, false, false);
        }
        else
        {
            RomListProjection projection = RomListService.Create(game, includeMerged);
            _sourceRows = projection.Rows;
            _columns = projection.Columns;
        }

        ApplySort();
        NotifyColumnState();
    }

    public void SetSort(string? header, bool ascending)
    {
        _sortHeader = header;
        _sortAscending = ascending;
        ApplySort();
    }

    private void ApplySort()
    {
        Items = RomListService.Sort(_sourceRows, _sortHeader, _sortAscending);
        SelectedItem = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private void NotifyColumnState()
    {
        OnPropertyChanged(nameof(ShowMergeColumn));
        OnPropertyChanged(nameof(ShowAlternateColumns));
        OnPropertyChanged(nameof(ShowStatusColumn));
        OnPropertyChanged(nameof(ShowModifiedColumn));
    }
}
