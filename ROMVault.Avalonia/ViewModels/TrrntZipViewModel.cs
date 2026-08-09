using System.Collections.ObjectModel;

namespace ROMVault.Avalonia.ViewModels;

public sealed class TrrntZipItemViewModel : ObservableObject
{
    private string _fileName = string.Empty;
    private string _status = string.Empty;

    public TrrntZipItemViewModel(int id) => Id = id;

    public int Id { get; }
    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public readonly record struct TrrntZipItemUpdate(int Id, string? FileName, string? Status);

public sealed class TrrntZipViewModel : ObservableObject
{
    private string _totalStatus = "( 0 / 0 )";

    public ObservableCollection<TrrntZipItemViewModel> Items { get; } = [];
    public string TotalStatus { get => _totalStatus; private set => SetProperty(ref _totalStatus, value); }

    public void Reset()
    {
        Items.Clear();
        TotalStatus = "( 0 / 0 )";
    }

    public void SetProgress(int current, int total) => TotalStatus = $"( {current} / {total} )";

    public void Apply(TrrntZipItemUpdate update)
    {
        while (Items.Count <= update.Id)
        {
            Items.Add(new TrrntZipItemViewModel(Items.Count));
        }

        TrrntZipItemViewModel item = Items[update.Id];
        if (update.FileName is not null)
        {
            item.FileName = update.FileName;
        }

        if (update.Status is not null)
        {
            item.Status = update.Status;
        }
    }
}
