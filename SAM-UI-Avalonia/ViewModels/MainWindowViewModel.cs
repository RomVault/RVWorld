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

namespace SAM_UI_Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{

    public ObservableCollection<samFile> Files { get; } = new();
    public ObservableCollection<procStatus> ProcessStats { get; } = new();



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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _filesDone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _filesTotal;

    /// <summary>
    /// Progress shown next to the Dry Run check box, e.g. "3 / 12".
    /// </summary>
    public string ProgressText => $"{FilesDone} / {FilesTotal}";

    [ObservableProperty]
    private bool _isDryRun;

    partial void OnIsDryRunChanged(bool value)
    {
        DryRunChanged(value);
    }

    private void DryRunChanged(bool value)
    {
        // Dry Run toggled - react to the new setting here.
    }

    public IReadOnlyList<EnumOption<InputZipType>> InputTypes { get; } = EnumOption<InputZipType>.CreateAll();

    public IReadOnlyList<EnumOption<OutputType>> OutputTypes { get; } = EnumOption<OutputType>.CreateAll();

    [ObservableProperty]
    private InputZipType _selectedInputType = InputZipType.Zip;

    [ObservableProperty]
    private OutputType _selectedOutputType = OutputType.ZipTorrent;

    partial void OnSelectedInputTypeChanged(InputZipType value)
    {
        InputTypeChanged(value);
    }

    partial void OnSelectedOutputTypeChanged(OutputType value)
    {
        OutputTypeChanged(value);
    }

    private void InputTypeChanged(InputZipType value)
    {
        // Input selection changed - react to the new input type here.
    }

    private void OutputTypeChanged(OutputType value)
    {
        // Output selection changed - react to the new output type here.
    }

    partial void OnThreadCountChanged(int value)
    {
        ThreadCountChanged(value);
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
                    pauseCancel = null,
                    workerCount = 1
                };

                Thread thread = new Thread(newProc.MigrateZip);
                thread.Start();

                procStatus ps = new procStatus() { Name = $"Process {Workers.ThreadId}", Progress = 0, CProcessZip = newProc };

                ProcessStats.Add(ps);
                Workers.workers++;
                Workers.ThreadId++;

                SetWorkerCount();
            }

            int extraWorkers = Workers.workers - Workers.requestedWorkers;
            for (int i = 0; i < extraWorkers; i++)
                MainQueue.bccFile.Add(new cFile() { fileId = -1, filename = "Removing" });
        }
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
        foreach(procStatus ps in ProcessStats)
            ps.CProcessZip.workerCount = workers;
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        IsPaused = !IsPaused;
    }

    private bool CanPause() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {

        IsRunning = true;
        IsPaused = false;
        ClearFileQueue();

    }

    /// <summary>
    /// Empties the pending file queue. <see cref="BlockingCollection{T}"/> has no
    /// Clear method, so drain every item that can be taken without blocking.
    /// </summary>
    private static void ClearFileQueue()
    {
        BlockingCollection<cFile> queue = MainQueue.bccFile;

        if (queue is null)
            return;

        while (queue.TryTake(out _))
        {
        }
    }

    private bool CanStop() => IsRunning;

    /// <summary>
    /// Called when files and/or directories are dropped onto the drop target.
    /// Directories are expanded recursively on a background thread; the
    /// resulting files are then added to <see cref="Files"/> on the UI thread.
    /// </summary>
    /// <param name="paths">Full paths of the dropped files and directories.</param>
    public void FilesDroppedAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        Settings settings = new Settings()
        {
            Repair = SelectedOutputType == OutputType.RepairKeepOriginal,
            InZip = SelectedInputType,
            OutZip = ComboOptions.ZipStructureFromUIIndex(SelectedOutputType),
            DryRun = IsDryRun
        };

        string[] files = paths.ToArray();
        FileAdder pm = new FileAdder(MainQueue.bccFile, files, UpdateFileCount, ProcessFileEndCallback, settings, null);
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
                foreach (procStatus proc in ProcessStats)
                {
                    if (proc.CProcessZip.ThreadId == processId)
                    {
                        ProcessStats.Remove(proc);
                        break;
                    }
                }
                SetWorkerCount();
            }
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
                    status = $"Re Struc {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                case TrrntZipStatus.NeedsRepaired:
                    status = $"Needs Repair {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                case TrrntZipStatus.Trrntzipped | TrrntZipStatus.NeedsRepaired:
                    status = $"Repaired {StructuredArchive.GetZipStructureName(zipStruct)}";
                    break;
                default:
                    status = trrntZipStatus.ToString();
                    break;
            }

            Files[fileId].Status = status;


            foreach (procStatus proc in ProcessStats)
            {
                if (proc.CProcessZip.ThreadId == processId)
                {
                    proc.Progress = 0;
                    proc.Name = "";
                    return;
                }
            }

        });
    }

    private void ProcessFileStartCallback(int processId, int fileId, string filename)
    {
        OnUIThread(() =>
        {
            Files.Add(new samFile() { Name = filename, Status = "Processing" });

            foreach (procStatus proc in ProcessStats)
            {
                if (proc.CProcessZip.ThreadId == processId)
                {
                    proc.Progress = 0;
                    proc.Name = filename;
                    return;
                }
            }
        });
    }

    private void StatusCallBack(int processId, int percent)
    {
        OnUIThread(() =>
        {
            foreach (procStatus proc in ProcessStats)
            {
                if (proc.CProcessZip.ThreadId == processId)
                {
                    proc.Progress = percent;
                    return;
                }
            }
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
}

public partial class procStatus : ObservableObject
{
    public CProcessZipAv CProcessZip;

    [ObservableProperty]
    private string _name = "";
    [ObservableProperty]
    private int _progress = 0;

}
