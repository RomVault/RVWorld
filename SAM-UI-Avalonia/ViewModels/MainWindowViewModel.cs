using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAM_UI_Avalonia.Services;

namespace SAM_UI_Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileScanService _fileScanService;
    private readonly IProcessRunner _processRunner;

    public ObservableCollection<samFile> Files { get; } = new();
    public ObservableCollection<procStatis> ProcessStats { get; } = new();

    public MainWindowViewModel()
        : this(new FileScanService(), new ProcessRunner())
    {
    }

    public MainWindowViewModel(IFileScanService fileScanService, IProcessRunner processRunner)
    {
        _fileScanService = fileScanService;
        _processRunner = processRunner;

        IsRunning = _processRunner.IsRunning;
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
        IsPaused = _processRunner.TogglePause();
    }

    private bool CanPause() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _processRunner.Stop();

        IsRunning = _processRunner.IsRunning;
        IsPaused = _processRunner.IsPaused;

        foreach (samFile file in Files)
        {
            file.Status = "Cancelled";
        }
    }

    private bool CanStop() => IsRunning;

    /// <summary>
    /// Called when files and/or directories are dropped onto the drop target.
    /// Directories are expanded recursively on a background thread; the
    /// resulting files are then added to <see cref="Files"/> on the UI thread.
    /// </summary>
    /// <param name="paths">Full paths of the dropped files and directories.</param>
    public async Task FilesDroppedAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScannedFile> scanned =
            await _fileScanService.ExpandAsync(paths, cancellationToken).ConfigureAwait(true);

        foreach (ScannedFile file in scanned)
        {
            Files.Add(new samFile
            {
                Name = file.FullPath,
                Status = "Pending"
            });
        }
    }
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
