using System.Collections.ObjectModel;

namespace ROMVault.Avalonia.ViewModels;

public sealed class DirectoryMappingsViewModel : ObservableObject
{
    private string _rulePath = string.Empty;
    private string _directoryPath = string.Empty;
    private bool _isSpecificEditor;

    public ObservableCollection<DirectoryMappingRowViewModel> Items { get; } = [];
    public string RulePath { get => _rulePath; set => SetProperty(ref _rulePath, value); }
    public string DirectoryPath { get => _directoryPath; set => SetProperty(ref _directoryPath, value); }
    public bool IsSpecificEditor
    {
        get => _isSpecificEditor;
        set
        {
            if (SetProperty(ref _isSpecificEditor, value))
            {
                OnPropertyChanged(nameof(IsGlobalListVisible));
            }
        }
    }

    public bool IsGlobalListVisible => !IsSpecificEditor;
}
