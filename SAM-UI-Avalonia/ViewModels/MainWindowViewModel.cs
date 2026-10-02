using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SAM_UI_Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<samFile> Files { get; } = new();

        public AddStyle[] AddStyleOptions { get; } = Enum.GetValues<AddStyle>();

        [ObservableProperty]
        private AddStyle _addStyle = AddStyle.Files;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        private samFile? _selectedFile;

        private int _stubCounter;

        [RelayCommand]
        private void AddFiles()
        {
            // Placeholder until the real file picker is wired up.
            _stubCounter++;
            Files.Add(new samFile
            {
                Name = $"File{_stubCounter}.zip",
                Status = "Pending"
            });
        }

        [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
        private void RemoveSelected()
        {
            if (SelectedFile is null)
                return;

            Files.Remove(SelectedFile);
            SelectedFile = null;
        }

        private bool CanRemoveSelected() => SelectedFile is not null;

        [RelayCommand]
        private void ClearAll()
        {
            if (Files.Count > 0)
            {
                Files[0].Name += " - Cleared";
            }
            SelectedFile = null;
        }
    }


    public enum AddStyle
    {
        Files,
        Zipped,
        SevenZipped
    }


    public partial class samFile : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _status = "";
    }
}
