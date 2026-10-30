using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compress.StructuredZip;
using SAM_UI_Avalonia.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using TrrntZip;
using TrrntZipUICore;

namespace SAM_UI_Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{

    public ObservableCollection<samFile> Files { get; } = new();
    public ObservableCollection<procStatus> ProcessStats { get; } = new();
    private Dictionary<int, procStatus> _processStats = new();

    private readonly Dictionary<int, samFile> _rowsByFileId = new();


    public MainWindowViewModel()
    {
        if (Design.IsDesignMode)
            return;

        var tZipSettings = TzipSettings.ReadConfig();

        SelectedInputType = (InputZipType)tZipSettings.InZip;
        SelectedOutputType = (OutputType)tZipSettings.OutZip;
        IsDryRun = tZipSettings.DryRun;

        MaxThreadCount = Environment.ProcessorCount;

        int procc = tZipSettings.ProcCount;
        if (procc <= 0)
        {
            procc = MaxThreadCount;
        }

        if (procc > MaxThreadCount)
        {
            procc = MaxThreadCount;
        }

        ThreadCount = procc;

        MainQueue.BusyChanged += OnQueueBusyChanged;
        IsRunning = MainQueue.IsBusy;
    }

    private void OnQueueBusyChanged(bool busy)
    {
        OnUIThread(() =>
        {
            IsRunning = busy;
            if (!busy && MainQueue.pc.Stopping)
                MainQueue.pc.ResetCancel();
        });
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(SoftStopCommand))]
    [NotifyCanExecuteChangedFor(nameof(HardStopCommand))]
    [NotifyPropertyChangedFor(nameof(IsSpinning))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseButtonText))]
    [NotifyPropertyChangedFor(nameof(IsSpinning))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool _isPaused;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SoftStopCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool _isSoftStopping;


    public bool IsSpinning => IsRunning && !IsPaused;

    /// <summary>
    /// Caption of the Pause button: "UnPause" while paused, otherwise "Pause".
    /// </summary>
    public string PauseButtonText => IsPaused ? "Resume" : "Pause";

    public string StateText
    {
        get
        {
            if (!IsRunning) return "Idle";
            if (IsSoftStopping) return "Stopping after current files...";
            if (IsPaused) return "Paused";
            return "Working";
        }
    }

    [ObservableProperty]
    private int _maxThreadCount = Environment.ProcessorCount;

    [ObservableProperty]
    private int _threadCount = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _filesDone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _filesTotal;

    /// <summary>
    /// Progress shown next to the Dry Run check box, e.g. "3 / 12".
    /// </summary>
    public string ProgressText => $"{FilesDone} of {FilesTotal}";

    [ObservableProperty]
    private bool _isDryRun;



    public IReadOnlyList<EnumOption<InputZipType>> InputTypes { get; } = EnumOption<InputZipType>.CreateAll();

    public IReadOnlyList<EnumOption<OutputType>> OutputTypes { get; } = EnumOption<OutputType>.CreateAll();

    [ObservableProperty]
    private InputZipType _selectedInputType = InputZipType.Zip;

    [ObservableProperty]
    private OutputType _selectedOutputType = OutputType.ZipTorrent;

    partial void OnThreadCountChanged(int value)
    {
        ThreadCountChanged(value);
        WriteSettings();
    }

    partial void OnIsDryRunChanged(bool value)
    {
        WriteSettings();
    }
    partial void OnSelectedOutputTypeChanged(OutputType value)
    {
        WriteSettings();
    }

    partial void OnSelectedInputTypeChanged(InputZipType value)
    {
        WriteSettings();
    }

    private void WriteSettings()
    {
        TzipSettings.WriteConfig(new TzipSettings()
        {
            InZip = (int)SelectedInputType,
            OutZip = (int)SelectedOutputType,
            DryRun = IsDryRun,
            ProcCount = ThreadCount
        });
    }

    private void ThreadCountChanged(int value)
    {
        lock (Workers.lockObj)
        {
            Workers.requestedWorkers = value;

            while (Workers.requestedWorkers > Workers.workers)
            {
                CProcessZipAv newProc = new CProcessZipAv
                {
                    ThreadId = Workers.ThreadId,
                    ProcessFileStartCallBack = ProcessFileStartCallback,
                    StatusCallBack = StatusCallBack,
                    ErrorCallBack = ErrorCallBack,
                    ProcessFileEndCallBack = ProcessFileEndCallback,
                    ProcessorClosingCallBack = ProcessorClosingCallback,
                    pauseCancel = MainQueue.pc,
                    workerCount = 1
                };

                Thread thread = new Thread(newProc.MigrateZip);
                thread.Start();

                procStatus ps = new procStatus() { Name = $"", Progress = 0, CProcessZip = newProc };

                ProcessStats.Add(ps);
                _processStats.Add(ps.CProcessZip.ThreadId, ps);
                Workers.workers++;
                Workers.ThreadId++;

                SetWorkerCount();
            }

            int extraWorkers = Workers.workers - Workers.requestedWorkers;
            for (int i = 0; i < extraWorkers; i++)
                MainQueue.bccFile.Add(new cFile() { fileId = -1, filename = "Removing" });
        }
    }

    partial void OnIsRunningChanged(bool value)
    {
        if (value)
            return;

        // Everything has finished (or been stopped).
        IsSoftStopping = false;
        IsPaused = false;
        if (MainQueue.pc.Paused)
            MainQueue.pc.UnPause();

        SetWorkerCount();
    }

    private void SetWorkerCount()
    {
        /*
         * this feels very non-MVVM because the main list of works is kept in the UI and not in the model
         * should look at moving this and just updating the UI as needed
         */

        if (ProcessStats.Count == 0)
            return;

        int workers = (Environment.ProcessorCount - 1) / ProcessStats.Count;
        if (workers == 0) workers = 1;
        foreach (procStatus ps in ProcessStats)
            if (ps.CProcessZip != null)
                ps.CProcessZip.workerCount = workers;

        if (!IsRunning)
        {
            int c = 1;
            foreach (procStatus ps in ProcessStats)
                ps.Name = $"Worker {c++}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        IsPaused = !IsPaused;
        if (IsPaused != MainQueue.pc.Paused)
        {
            if (IsPaused)
                MainQueue.pc.Pause();
            else
                MainQueue.pc.UnPause();
        }
    }


    private bool CanPause() => IsRunning && !IsSoftStopping;

    [RelayCommand(CanExecute = nameof(CanSoftStop))]
    private void SoftStop()
    {
        IsSoftStopping = true;
        MainQueue.pc.SoftCancel();
        IsPaused = false;

        ClearFileQueue();
    }

    private bool CanSoftStop() => IsRunning && !IsSoftStopping;

    [RelayCommand(CanExecute = nameof(CanHardStop))]
    private void HardStop()
    {
        ClearFileQueue();
        MainQueue.pc.Cancel();
        IsPaused = false;

        // clearing again just incase anything snuck in after the first clear.
        ClearFileQueue();
    }

    private bool CanHardStop() => IsRunning;
    /// <summary>
    /// Empties the pending file queue. <see cref="BlockingCollection{T}"/> has no
    /// Clear method, so drain every item that can be taken without blocking.
    /// </summary>
    private static void ClearFileQueue()
    {
        BlockingCollection<cFile> queue = MainQueue.bccFile;

        if (queue is null)
            return;


        int removeMarkers = 0;
        while (queue.TryTake(out cFile? item))
        {
            if (item != null && item.fileId == -1 && item.filename == "Removing")
                removeMarkers++;
        }

        for (int i = 0; i < removeMarkers; i++)
            queue.Add(new cFile() { fileId = -1, filename = "Removing" });

        // Draining bypasses the normal worker flow, so re-evaluate the busy state.
        MainQueue.QueueChanged();
    }


    /// <summary>
    /// Called when files and/or directories are dropped onto the drop target.
    /// Directories are expanded recursively on a background thread; the
    /// resulting files are then added to <see cref="Files"/> on the UI thread.
    /// </summary>
    /// <param name="paths">Full paths of the dropped files and directories.</param>
    public void FilesDropped(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        if (IsSoftStopping)
            return;

        if (!IsRunning)
        {
            MainQueue.fileCount = 0;
            FilesDone = 0;
            FilesTotal = 0;
            Files.Clear();
        }


        Settings settings = new Settings()
        {
            Repair = SelectedOutputType == OutputType.RepairKeepOriginal,
            InZip = SelectedInputType,
            OutZip = ComboOptions.ZipStructureFromUIIndex(SelectedOutputType),
            DryRun = IsDryRun
        };

        string[] files = paths.ToArray();
        FileAdder pm = new FileAdder(MainQueue.bccFile, files, UpdateFileCount, ProcessFileEndCallback, settings, MainQueue.pc);
        Thread procT = new Thread(pm.ProcFiles);
        procT.Start();
    }

    /// <summary>
    /// All of the callbacks below are raised from the TrrntZip worker threads.
    /// Avalonia only allows the UI thread to touch bound collections and
    /// observable properties, so every update is posted to the dispatcher.
    /// </summary>
    private static void OnUIThread(System.Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private void UpdateFileCount(int fileCount)
    {
        OnUIThread(() => FilesTotal = fileCount);
    }

    private void ProcessorClosingCallback(int processId)
    {
        if (processId == -1)
            return;
        OnUIThread(() =>
        {
            lock (Workers.lockObj)
            {
                ProcessStats.Remove(_processStats[processId]);
                _processStats.Remove(processId);
                SetWorkerCount();
            }
        });
    }


    private void ProcessFileStartCallback(int processId, int fileId, string filename, long size)
    {
        OnUIThread(() =>
        {
            samFile row = new samFile()
            {
                Name = filename,
                Size = size,
                Status = "Processing",
                Progress = 0,
                IsProcessing = true
            };
            _rowsByFileId[fileId] = row;
            Files.Add(row);

            procStatus proc = _processStats[processId];
            proc.Progress = 0;
            proc.Name = filename;
            return;
        });
    }
    private void ProcessFileEndCallback(int processId, int fileId, TrrntZipStatus trrntZipStatus, ZipStructure zipStruct)
    {
        if (processId == -1)
            return;

        OnUIThread(() =>
        {
            if (FilesDone < fileId + 1)
                FilesDone = fileId + 1;

            string status = "";
            switch (trrntZipStatus)
            {
                case TrrntZipStatus.ValidTrrntzip:
                    status = $"Valid {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                case TrrntZipStatus.Trrntzipped:
                    status = $"Structured to {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                case TrrntZipStatus.NeedsRepaired:
                    status = $"Needs RePacked to {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                case TrrntZipStatus.Trrntzipped | TrrntZipStatus.NeedsRepaired:
                    status = $"RePacked to {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                case TrrntZipStatus.Cancel:
                    status = "Stopped Hard. (File Unchanged";
                    break;
                default:
                    status = trrntZipStatus.ToString();
                    break;
            }

            if (_rowsByFileId.TryGetValue(fileId, out samFile? row))
            {
                row.Status = status;
                row.IsProcessing = false;
                _rowsByFileId.Remove(fileId);
            }

            procStatus proc = _processStats[processId];
            proc.Progress = 0;
            proc.Name = "";

        });
    }

    private void StatusCallBack(int processId, int fileId, int percent)
    {
        OnUIThread(() =>
        {
            procStatus proc = _processStats[processId];
            proc.Progress = percent;

            if (_rowsByFileId.TryGetValue(fileId, out samFile? row))
                row.Progress = percent;
        });
    }

    private void ErrorCallBack(int processId, string message)
    {
    }
}

public partial class samFile : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText))]
    private long _size = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotProcessing))]
    private bool _isProcessing;

    public bool IsNotProcessing => !IsProcessing;
    public string ProgressText => $"{Progress}%";
    public string SizeText => FormatSize(Size);

    private static string FormatSize(long bytes)
    {
        if (bytes < 0) return "";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.00} {units[unit]}";
    }
}

public partial class procStatus : ObservableObject
{
    public CProcessZipAv? CProcessZip;

    [ObservableProperty]
    private string _name = "";
    [ObservableProperty]
    private int _progress = 0;

}
