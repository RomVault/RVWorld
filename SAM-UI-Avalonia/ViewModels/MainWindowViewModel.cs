using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SAM_UI_Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<samFile> Files { get; } = new();
    public ObservableCollection<procStatis> ProcessStats { get; } = new();

    public MainWindowViewModel()
    {
        ThreadCountChanged(1);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning = true;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _threadCount = 1;

    partial void OnThreadCountChanged(int value)
    {
        ThreadCountChanged(value);
    }

    private void ThreadCountChanged(int value)
    {
        while (value > ProcessStats.Count)
        {
            ProcessStats.Add(new procStatis() { Name = $"Process {ProcessStats.Count + 1}", Progress = 0 });
        }

        while (value < ProcessStats.Count)
        {
            ProcessStats.RemoveAt(ProcessStats.Count - 1);
        }

    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        IsPaused = !IsPaused;

        Files.Add(new samFile() { Name = "File1", Status = "Running..." });

        ProcessStats.Add(new procStatis() { Name = "Process1", Progress = 50 });
    }

    private bool CanPause() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        IsRunning = false;
        IsPaused = false;

        foreach (samFile file in Files)
        {
            file.Status = "Cancelled";
        }


    }

    private bool CanStop() => IsRunning;


    /*
    public samFile _selectedFile;
    public samFile SelectedFile
    {
        get { return _selectedFile; }
        set { _selectedFile = value; }
    }
    */
}

public partial class samFile : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _status = "";
}

public partial class procStatis : ObservableObject
{
    [ObservableProperty]
    private string _name = "";
    [ObservableProperty]
    private int _progress = 0;
}
