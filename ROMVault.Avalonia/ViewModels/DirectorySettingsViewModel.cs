using System.Collections.ObjectModel;

namespace ROMVault.Avalonia.ViewModels;

public sealed class DirectorySettingsViewModel : ObservableObject
{
    private string _rulePath = string.Empty;
    private bool _isSpecificEditor;

    public ObservableCollection<DatRuleRowViewModel> Rules { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];
    public string RulePath { get => _rulePath; set => SetProperty(ref _rulePath, value); }
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
