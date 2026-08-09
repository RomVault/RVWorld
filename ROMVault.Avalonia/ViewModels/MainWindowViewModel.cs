using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ROMVault.Avalonia.ViewModels;

public sealed record MainWindowActions(
    Action UpdateDats,
    Action UpdateAllDats,
    Action<string?> ScanRoms,
    Action FindFixes,
    Action FixFiles,
    Func<Task> CreateFixDatReport,
    Func<Task> CreateFullReport,
    Func<Task> CreateFixReport,
    Func<Task> OpenSettings,
    Func<Task> OpenDirectorySettings,
    Func<Task> OpenDirectoryMappings,
    Func<Task> AddToSort,
    Action OpenTorrentZip,
    Action OpenWiki,
    Action OpenColorKey,
    Func<Task> OpenShortcuts,
    Action OpenWhatsNew,
    Func<Task> OpenAbout,
    Action<bool> SetCompactDensity);

/// <summary>
/// Composite state and commands for the application shell.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IReadOnlyList<Action> _notifyCommandState;
    private bool _isBusy;
    private bool _isCompact;
    private bool _isInspectorOpen = true;
    private string _activity = string.Empty;
    private string _status = string.Empty;

    public MainWindowViewModel(
        GameBrowserViewModel games,
        RomBrowserViewModel roms,
        MediaInspectorViewModel media,
        MainWindowActions actions)
    {
        Games = games ?? throw new ArgumentNullException(nameof(games));
        Roms = roms ?? throw new ArgumentNullException(nameof(roms));
        Media = media ?? throw new ArgumentNullException(nameof(media));
        DatDetails = new DatDetailsViewModel();
        GameDetails = new GameDetailsViewModel();
        ArgumentNullException.ThrowIfNull(actions);

        UpdateDatsCommand = new RelayCommand(actions.UpdateDats, CanRunOperation);
        UpdateAllDatsCommand = new RelayCommand(actions.UpdateAllDats, CanRunOperation);
        ScanRomsCommand = new RelayCommand<string>(actions.ScanRoms, _ => CanRunOperation());
        FindFixesCommand = new RelayCommand(actions.FindFixes, CanRunOperation);
        FixFilesCommand = new RelayCommand(actions.FixFiles, CanRunOperation);
        FixDatReportCommand = new AsyncRelayCommand(actions.CreateFixDatReport, CanRunOperation);
        FullReportCommand = new AsyncRelayCommand(actions.CreateFullReport, CanRunOperation);
        FixReportCommand = new AsyncRelayCommand(actions.CreateFixReport, CanRunOperation);
        SettingsCommand = new AsyncRelayCommand(actions.OpenSettings, CanRunOperation);
        DirectorySettingsCommand = new AsyncRelayCommand(actions.OpenDirectorySettings, CanRunOperation);
        DirectoryMappingsCommand = new AsyncRelayCommand(actions.OpenDirectoryMappings, CanRunOperation);
        AddToSortCommand = new AsyncRelayCommand(actions.AddToSort, CanRunOperation);
        TorrentZipCommand = new RelayCommand(actions.OpenTorrentZip);
        WikiCommand = new RelayCommand(actions.OpenWiki);
        ColorKeyCommand = new RelayCommand(actions.OpenColorKey);
        ShortcutsCommand = new AsyncRelayCommand(actions.OpenShortcuts);
        WhatsNewCommand = new RelayCommand(actions.OpenWhatsNew);
        AboutCommand = new AsyncRelayCommand(actions.OpenAbout);
        ToggleCompactDensityCommand = new RelayCommand(() =>
        {
            bool compact = !IsCompact;
            IsCompact = compact;
            actions.SetCompactDensity(compact);
        });
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);

        _notifyCommandState =
        [
            UpdateDatsCommand.NotifyCanExecuteChanged,
            UpdateAllDatsCommand.NotifyCanExecuteChanged,
            ScanRomsCommand.NotifyCanExecuteChanged,
            FindFixesCommand.NotifyCanExecuteChanged,
            FixFilesCommand.NotifyCanExecuteChanged,
            FixDatReportCommand.NotifyCanExecuteChanged,
            FullReportCommand.NotifyCanExecuteChanged,
            FixReportCommand.NotifyCanExecuteChanged,
            SettingsCommand.NotifyCanExecuteChanged,
            DirectorySettingsCommand.NotifyCanExecuteChanged,
            DirectoryMappingsCommand.NotifyCanExecuteChanged,
            AddToSortCommand.NotifyCanExecuteChanged
        ];
    }

    public GameBrowserViewModel Games { get; }
    public RomBrowserViewModel Roms { get; }
    public MediaInspectorViewModel Media { get; }
    public DatDetailsViewModel DatDetails { get; }
    public GameDetailsViewModel GameDetails { get; }

    public RelayCommand UpdateDatsCommand { get; }
    public RelayCommand UpdateAllDatsCommand { get; }
    public RelayCommand<string> ScanRomsCommand { get; }
    public RelayCommand FindFixesCommand { get; }
    public RelayCommand FixFilesCommand { get; }
    public AsyncRelayCommand FixDatReportCommand { get; }
    public AsyncRelayCommand FullReportCommand { get; }
    public AsyncRelayCommand FixReportCommand { get; }
    public AsyncRelayCommand SettingsCommand { get; }
    public AsyncRelayCommand DirectorySettingsCommand { get; }
    public AsyncRelayCommand DirectoryMappingsCommand { get; }
    public AsyncRelayCommand AddToSortCommand { get; }
    public RelayCommand TorrentZipCommand { get; }
    public RelayCommand WikiCommand { get; }
    public RelayCommand ColorKeyCommand { get; }
    public AsyncRelayCommand ShortcutsCommand { get; }
    public RelayCommand WhatsNewCommand { get; }
    public AsyncRelayCommand AboutCommand { get; }
    public RelayCommand ToggleCompactDensityCommand { get; }
    public RelayCommand ToggleInspectorCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            foreach (Action notify in _notifyCommandState)
            {
                notify();
            }
        }
    }

    public bool IsCompact
    {
        get => _isCompact;
        set => SetProperty(ref _isCompact, value);
    }

    public bool IsInspectorOpen
    {
        get => _isInspectorOpen;
        set => SetProperty(ref _isInspectorOpen, value);
    }

    public string Activity
    {
        get => _activity;
        set => SetProperty(ref _activity, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private bool CanRunOperation() => !IsBusy;
}
