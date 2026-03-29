using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Compress;
using RVIO;
using TrrntZip;
using RomVaultCore.Utils;
using ROMVault.Avalonia.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using Path = RVIO.Path;
using Directory = RVIO.Directory;

namespace ROMVault.Avalonia.Views;

/// <summary>
    /// A standalone window for TorrentZip operations (standardizing zip files).
    /// Supports drag-and-drop, multi-threaded processing, and various output formats.
    /// </summary>
    public partial class TrrntZipWindow : Window
    {
        private int _fileIndex;
        private int FileCount;
        private int FileCountProcessed;

        private BlockingCollection<cFile>? bccFile;

        private class ThreadProcess
        {
            public TextBlock? threadLabel;
            public ProgressBar? threadProgress;
            public string tLabel = "";
            public int tProgress;
            public CProcessZip? cProcessZip;
            public Thread? thread;
        }
        private readonly List<ThreadProcess> _threads;

        /// <summary>
        /// Represents a file item in the processing grid.
        /// </summary>
        public class GridItem
        {
            public int fileId { get; set; }
            public string? Filename { get; set; }
            public string? Status { get; set; }
        }

        private readonly ObservableCollection<GridItem> tGrid;
        // We use a separate list for thread-safe updates before syncing to ObservableCollection
        private readonly List<GridItem> _pendingGridUpdates = new List<GridItem>();
        
        private readonly PauseCancel pc;

        private bool _working;
        private int _threadCount;
        private global::Avalonia.Media.IBrush? _dropBorderBrush;
        private global::Avalonia.Media.IBrush? _dropBackgroundBrush;
        private readonly Dictionary<int, string> _filePathById = new();

        private bool UiUpdate = false;
        private bool scanningForFiles = false;
        private DispatcherTimer? _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrrntZipWindow"/> class.
        /// Sets up UI controls, event handlers, and loads settings.
        /// </summary>
        public TrrntZipWindow()
        {
            UiUpdate = true;
            InitializeComponent();
            
            DropBox.AddHandler(DragDrop.DragEnterEvent, PDragEnter);
            DropBox.AddHandler(DragDrop.DragLeaveEvent, PDragLeave);
            DropBox.AddHandler(DragDrop.DragOverEvent, PDragOver);
            DropBox.AddHandler(DragDrop.DropEvent, PDragDrop);
            _dropBorderBrush = DropBox.BorderBrush;
            _dropBackgroundBrush = DropBox.Background;

            // Init ComboBoxes
            cboInType.Items.Add("Zip");
            cboInType.Items.Add("7Zip");
            cboInType.Items.Add("All Archives");
            cboInType.Items.Add("Files");
            cboInType.Items.Add("Directory");
            
            cboOutType.Items.Add("Zip - TrrntZip");
            cboOutType.Items.Add("Zip - RVZSTD");
            cboOutType.Items.Add("7Zip - ZSTD");
            cboOutType.Items.Add("7Zip - ZSTD Solid");
            cboOutType.Items.Add("7Zip - LZMA");
            cboOutType.Items.Add("7Zip - LZMA Solid");

            string? sval = AppSettings.ReadSetting("InZip");
            if (!int.TryParse(sval, out int intVal)) intVal = 2;
            cboInType.SelectedIndex = intVal;

            sval = AppSettings.ReadSetting("OutZip");
            if (!int.TryParse(sval, out intVal)) intVal = 0;
            cboOutType.SelectedIndex = UIIndexFromZipStructure((ZipStructure)intVal);

            sval = AppSettings.ReadSetting("Force");
            chkForce.IsChecked = sval == "True";

            sval = AppSettings.ReadSetting("Fix");
            chkFix.IsChecked = sval != "False";

            tbProccessors.Minimum = 1;
            tbProccessors.Maximum = Environment.ProcessorCount;

            sval = AppSettings.ReadSetting("ProcCount");
            if (!int.TryParse(sval, out int procc)) procc = (int)tbProccessors.Maximum;
            if (procc > tbProccessors.Maximum) procc = (int)tbProccessors.Maximum;
            tbProccessors.Value = procc;

            _threads = new List<ThreadProcess>();
            tGrid = new ObservableCollection<GridItem>();
            dataGrid.ItemsSource = tGrid;
            pc = new PauseCancel();

            // Event handlers
            tbProccessors.ValueChanged += (s, e) => {
                if (UiUpdate) return;
                AppSettings.AddUpdateAppSettings("ProcCount", ((int)tbProccessors.Value).ToString());
                SetUpWorkerThreads();
            };
            chkFix.IsCheckedChanged += (s, e) => {
                if (UiUpdate) return;
                AppSettings.AddUpdateAppSettings("Fix", (chkFix.IsChecked == true).ToString());
            };
            chkForce.IsCheckedChanged += (s, e) => {
                if (UiUpdate) return;
                AppSettings.AddUpdateAppSettings("Force", (chkForce.IsChecked == true).ToString());
            };
            cboInType.SelectionChanged += (s, e) => {
                if (UiUpdate) return;
                AppSettings.AddUpdateAppSettings("InZip", cboInType.SelectedIndex.ToString());
            };
            cboOutType.SelectionChanged += (s, e) => {
                if (UiUpdate) return;
                AppSettings.AddUpdateAppSettings("OutZip", ((int)ZipStructureFromUIIndex(cboOutType.SelectedIndex)).ToString());
            };
            btnCancel.Click += (s, e) => {
                pc.Cancel();
                var img = this.FindControl<Image>("imgPause");
                if (img != null) img.Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://ROMVault.Avalonia/Assets/Pause.png")));
            };
            btnPause.Click += (s, e) => {
                var img = this.FindControl<Image>("imgPause");
                if (pc.Paused) {
                    pc.UnPause();
                    if (img != null) img.Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://ROMVault.Avalonia/Assets/Pause.png")));
                    DropBox.IsEnabled = true; // Resume enabled dropbox? WinForms says "Resume after a Pause ... DropBox.Enabled = true;"
                } else {
                    pc.Pause();
                    if (img != null) img.Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://ROMVault.Avalonia/Assets/Resume.png")));
                    DropBox.IsEnabled = false;
                }
            };

            btnAddFiles.Click += BtnAddFiles_Click;

            SetUpWorkerThreads();

            UiUpdate = false;
            
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(125);
            _timer.Tick += Timer_Tick;
        }

        private void OnDonateClick(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
             try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://www.patreon.com/romvault", UseShellExecute = true }); } catch { }
        }

        private void OnRomVaultClick(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
             try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "http://www.romvault.com", UseShellExecute = true }); } catch { }
        }

        private void SetDropHighlight(bool highlight)
        {
            if (highlight)
            {
                if (TryGetResource("AccentBrush", null, out var accent) && accent is global::Avalonia.Media.IBrush ab)
                    DropBox.BorderBrush = ab;
                if (TryGetResource("AccentWeakBrush", null, out var weak) && weak is global::Avalonia.Media.IBrush wb)
                    DropBox.Background = wb;
                return;
            }

            DropBox.BorderBrush = _dropBorderBrush;
            DropBox.Background = _dropBackgroundBrush;
        }

        private async void BtnAddFiles_Click(object? sender, RoutedEventArgs e)
        {
            if (_working) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = true,
                Title = "Select folder(s)"
            });

            if (folders.Count > 0)
            {
                var paths = folders.Select(f => f.Path.LocalPath).OrderBy(p => p).ToArray();
                StartProcessing(paths);
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = "Select file(s)"
            });

            if (files.Count == 0)
                return;

            var filePaths = files.Select(f => f.Path.LocalPath).OrderBy(p => p).ToArray();
            StartProcessing(filePaths);
        }

        private async void OnCopyFilenameClick(object? sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is not GridItem item)
                return;

            string path = item.Filename ?? (_filePathById.TryGetValue(item.fileId, out var p) ? p : "");
            if (string.IsNullOrWhiteSpace(path))
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard == null)
                return;

            await topLevel.Clipboard.SetTextAsync(path);
        }

        private void OnOpenSourceClick(object? sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is not GridItem item)
                return;

            string path = item.Filename ?? (_filePathById.TryGetValue(item.fileId, out var p) ? p : "");
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
                return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch { }
        }

        /// <summary>
        /// Initializes the worker threads based on the processor count setting.
        /// </summary>
        private void SetUpWorkerThreads()
        {
            _threadCount = (int)tbProccessors.Value;

            bccFile?.CompleteAdding();

            foreach (ThreadProcess tp in _threads)
            {
                if (tp.threadLabel != null) StatusPanel.Children.Remove(tp.threadLabel);
                if (tp.threadProgress != null) StatusPanel.Children.Remove(tp.threadProgress);

                if (tp.cProcessZip != null)
                {
                    tp.cProcessZip.ProcessFileStartCallBack = null;
                    tp.cProcessZip.StatusCallBack = null;
                    tp.cProcessZip.ErrorCallBack = null;
                    tp.cProcessZip.ProcessFileEndCallBack = null;
                }
                tp.thread?.Join();
            }

            bccFile?.Dispose();

            _threads.Clear();
            bccFile = new BlockingCollection<cFile>();

            int workers = (Environment.ProcessorCount - 1) / _threadCount;
            if (workers == 0) workers = 1;

            for (int i = 0; i < _threadCount; i++)
            {
                ThreadProcess threadProcess = new ThreadProcess();
                _threads.Add(threadProcess);

                TextBlock pLabel = new TextBlock
                {
                    Text = $"Processor {i + 1}",
                    Margin = new Thickness(0, 5, 0, 0)
                };
                StatusPanel.Children.Add(pLabel);
                threadProcess.threadLabel = pLabel;

                ProgressBar pProgress = new ProgressBar
                {
                    Height = 12,
                    Minimum = 0,
                    Maximum = 100,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
                };
                StatusPanel.Children.Add(pProgress);
                threadProcess.threadProgress = pProgress;

                threadProcess.cProcessZip = new CProcessZip
                {
                    ThreadId = i,
                    bcCfile = bccFile,
                    ProcessFileStartCallBack = ProcessFileStartCallback,
                    StatusCallBack = StatusCallBack,
                    ErrorCallBack = ErrorCallBack,
                    ProcessFileEndCallBack = ProcessFileEndCallback,
                    pauseCancel = pc,
                    workerCount = workers
                };
                threadProcess.thread = new Thread(threadProcess.cProcessZip.MigrateZip);
                threadProcess.thread.Start();
            }
        }

        /// <summary>
        /// Handles drag enter event on the DropBox.
        /// </summary>
        private void PDragEnter(object? sender, DragEventArgs e)
        {
            if (_working) return;
            #pragma warning disable CS0618 // Type or member is obsolete
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
                SetDropHighlight(true);
            }
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        private void PDragLeave(object? sender, DragEventArgs e)
        {
            SetDropHighlight(false);
        }

        private void PDragOver(object? sender, DragEventArgs e)
        {
            if (_working) return;
            #pragma warning disable CS0618 // Type or member is obsolete
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Handles drag drop event on the DropBox.
        /// Starts processing the dropped files.
        /// </summary>
        private void PDragDrop(object? sender, DragEventArgs e)
        {
            if (_working) return;

            #pragma warning disable CS0618 // Type or member is obsolete
            var files = e.Data.GetFiles();
            #pragma warning restore CS0618 // Type or member is obsolete
            if (files == null) return;
            
            var fileList = files.Select(f => f.Path.LocalPath).OrderBy(f => f).ToArray();
            StartProcessing(fileList);
        }

        private void StartProcessing(string[] fileList)
        {
            if (_working) return;

            SetDropHighlight(false);
            _filePathById.Clear();

            TrrntZip.Program.ForceReZip = chkForce.IsChecked == true;
            TrrntZip.Program.CheckOnly = chkFix.IsChecked != true;
            TrrntZip.Program.InZip = (zipType)cboInType.SelectedIndex;
            TrrntZip.Program.OutZip = ZipStructureFromUIIndex(cboOutType.SelectedIndex);

            tGrid.Clear();
            _pendingGridUpdates.Clear();
            
            StartWorking();

            FileCountProcessed = 0;
            scanningForFiles = true;
            
            FileAdder pm = new FileAdder(bccFile, fileList, UpdateFileCount, ProcessFileEndCallback);
            Thread procT = new Thread(pm.ProcFiles);
            procT.Start();

            _timer?.Start();
        }

        /// <summary>
        /// Sets UI state to working mode (disabled controls).
        /// </summary>
        private void StartWorking()
        {
            _working = true;
            DropBox.IsEnabled = false; // TODO: Show working image
            btnAddFiles.IsEnabled = false;
            cboInType.IsEnabled = false;
            cboOutType.IsEnabled = false;
            chkForce.IsEnabled = false;
            chkFix.IsEnabled = false;
            tbProccessors.IsEnabled = false;
            btnCancel.IsEnabled = true;
            btnPause.IsEnabled = true;
        }

        /// <summary>
        /// Resets UI state to idle mode.
        /// </summary>
        private void StopWorking()
        {
            _working = false;
            DropBox.IsEnabled = true;
            btnAddFiles.IsEnabled = true;
            cboInType.IsEnabled = true;
            cboOutType.IsEnabled = true;
            chkForce.IsEnabled = true;
        chkFix.IsEnabled = true;
        tbProccessors.IsEnabled = true;
        btnCancel.IsEnabled = false;
        btnPause.IsEnabled = false;
        SetDropHighlight(false);
        _timer?.Stop();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_working)
        {
            e.Cancel = true;
        }
        else
        {
            bccFile?.CompleteAdding();
            foreach (ThreadProcess tp in _threads)
            {
                if (tp.cProcessZip != null)
                {
                    tp.cProcessZip.ProcessFileStartCallBack = null;
                    tp.cProcessZip.StatusCallBack = null;
                    tp.cProcessZip.ErrorCallBack = null;
                    tp.cProcessZip.ProcessFileEndCallBack = null;
                }
                tp.thread?.Join();
            }
            bccFile?.Dispose();
        }
        base.OnClosing(e);
    }

    private static ZipStructure ZipStructureFromUIIndex(int cboIndex)
    {
        switch (cboIndex)
        {
            case 0: return ZipStructure.ZipTrrnt;
            case 1: return ZipStructure.ZipZSTD;
            case 2: return ZipStructure.SevenZipNZSTD;
            case 3: return ZipStructure.SevenZipSZSTD;
            case 4: return ZipStructure.SevenZipNLZMA;
            case 5: return ZipStructure.SevenZipSLZMA;
            default: return ZipStructure.ZipTrrnt;
        }
    }

    private static int UIIndexFromZipStructure(ZipStructure zipStructure)
    {
        switch (zipStructure)
        {
            case ZipStructure.ZipTrrnt: return 0;
            case ZipStructure.ZipZSTD: return 1;
            case ZipStructure.SevenZipNZSTD: return 2;
            case ZipStructure.SevenZipSZSTD: return 3;
            case ZipStructure.SevenZipNLZMA: return 4;
            case ZipStructure.SevenZipSLZMA: return 5;
            default: return 0;
        }
    }

    #region callbacks

    /// <summary>
        /// Updates the total file count to be processed.
        /// </summary>
        /// <param name="fileCount">The total number of files.</param>
        private void UpdateFileCount(int fileCount)
        {
            FileCount = fileCount;
        }

        /// <summary>
        /// Callback when a file processing starts.
        /// Updates the thread status label and adds the file to the grid.
        /// </summary>
        /// <param name="processId">The ID of the worker thread.</param>
        /// <param name="fileId">The ID of the file.</param>
        /// <param name="filename">The name of the file being processed.</param>
        private void ProcessFileStartCallback(int processId, int fileId, string filename)
        {
            _fileIndex = fileId + 1;
            _threads[processId].tLabel = Path.GetFileName(filename);
            _threads[processId].tProgress = 0;
            _filePathById[fileId] = filename;

            lock (_pendingGridUpdates)
            {
                _pendingGridUpdates.Add(new GridItem { fileId = fileId, Filename = filename, Status = "Processing....(" + processId + ")" });
            }
        }

        /// <summary>
        /// Callback when a file processing ends.
        /// Updates the file status in the grid and checks if all processing is complete.
        /// </summary>
        /// <param name="processId">The ID of the worker thread, or -1 if called from the scanner thread.</param>
        /// <param name="fileId">The ID of the file.</param>
        /// <param name="trrntZipStatus">The result status of the operation.</param>
        private void ProcessFileEndCallback(int processId, int fileId, TrrntZipStatus trrntZipStatus)
        {
            if (processId == -1)
            {
                scanningForFiles = false;
                if (FileCount == 0)
                {
                    Dispatcher.UIThread.Post(() => {
                        StopWorking();
                        if (pc.Cancelled) pc.ResetCancel();
                    });
                }
            }
            else
            {
                _threads[processId].tProgress = 100;
                
                string statusStr;
                switch (trrntZipStatus)
                {
                    case TrrntZipStatus.ValidTrrntzip: statusStr = "Valid Archive"; break;
                    case TrrntZipStatus.Trrntzipped: statusStr = "Re-Structured"; break;
                    default: statusStr = trrntZipStatus.ToString(); break;
                }

                lock (_pendingGridUpdates)
                {
                    _pendingGridUpdates.Add(new GridItem { fileId = fileId, Filename = null, Status = statusStr });
                }

                FileCountProcessed += 1;
                if (!scanningForFiles && FileCountProcessed == FileCount)
                {
                    Dispatcher.UIThread.Post(() => {
                        StopWorking();
                        if (pc.Cancelled) pc.ResetCancel();
                    });
                }
            }
        }

        /// <summary>
        /// Callback to update the progress percentage of a worker thread.
        /// </summary>
        /// <param name="processId">The ID of the worker thread.</param>
        /// <param name="percent">The progress percentage (0-100).</param>
        private void StatusCallBack(int processId, int percent)
        {
            _threads[processId].tProgress = percent;
        }

        /// <summary>
        /// Callback to report an error from a worker thread.
        /// </summary>
        /// <param name="processId">The ID of the worker thread.</param>
        /// <param name="message">The error message.</param>
        private void ErrorCallBack(int processId, string message)
        {
            // TODO: Implement error log
            System.Diagnostics.Debug.WriteLine($"Error {processId}: {message}");
        }

    #endregion

    private int uiFileCount = -1;
    private int uiFileIndex = -1;

    /// <summary>
    /// Timer tick event handler.
    /// Updates the UI with the current progress of the worker threads.
    /// Syncs thread progress and labels to UI controls.
    /// Processes pending grid updates to the ObservableCollection.
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_fileIndex != uiFileIndex || FileCount != uiFileCount)
        {
            uiFileIndex = _fileIndex;
            uiFileCount = FileCount;
            lblTotalStatus.Text = @"( " + uiFileIndex + @" / " + uiFileCount + @" )";
        }

        foreach (ThreadProcess tp in _threads)
        {
            if (tp.threadProgress != null && tp.tProgress != tp.threadProgress.Value)
                tp.threadProgress.Value = tp.tProgress;
            if (tp.threadLabel != null && tp.tLabel != tp.threadLabel.Text)
                tp.threadLabel.Text = tp.tLabel;
        }

        lock (_pendingGridUpdates)
        {
            foreach (var item in _pendingGridUpdates)
            {
                if (item.fileId >= tGrid.Count)
                {
                    // Add fillers if needed (shouldn't happen if sequential, but threading)
                    while (tGrid.Count <= item.fileId)
                    {
                        tGrid.Add(new GridItem());
                    }
                }
                
                var gridItem = tGrid[item.fileId];
                gridItem.fileId = item.fileId;
                if (item.Filename != null) gridItem.Filename = item.Filename;
                if (item.Status != null) gridItem.Status = item.Status;
                
                // Force refresh if needed, but ObservableCollection handles property changes if item implements INotifyPropertyChanged
                // Since GridItem doesn't, we might need to replace the item or use DynamicData.
                // For simplicity, we just replace the item in the collection to trigger update
                tGrid[item.fileId] = new GridItem 
                { 
                    fileId = gridItem.fileId, 
                    Filename = gridItem.Filename, 
                    Status = gridItem.Status 
                };
            }
            _pendingGridUpdates.Clear();
        }
    }

    // Local FileAdder implementation
    /// <summary>
    /// Helper class to scan for files and directories to process.
    /// Populates the blocking collection with files found.
    /// </summary>
    private class FileAdder
    {
        private readonly BlockingCollection<cFile>? _fileCollection;
        private readonly string[] _file;
        private int fileCount;
        private readonly Action<int>? _updateFileCount;
        private readonly ProcessFileEndCallback? _processFileEndCallBack;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileAdder"/> class.
        /// </summary>
        /// <param name="fileCollection">The collection to add found files to.</param>
        /// <param name="file">The list of initial files or directories to scan.</param>
        /// <param name="updateFileCount">Callback to update the total file count.</param>
        /// <param name="processFileEndCallBack">Callback to signal end of processing (if no files found).</param>
        public FileAdder(BlockingCollection<cFile>? fileCollection, string[] file, Action<int>? updateFileCount, ProcessFileEndCallback? processFileEndCallBack)
        {
            _fileCollection = fileCollection;
            _file = file;
            _updateFileCount = updateFileCount;
            _processFileEndCallBack = processFileEndCallBack;
        }

        /// <summary>
        /// Starts scanning for files and adds them to the collection.
        /// </summary>
        public void ProcFiles()
        {
            fileCount = 0;

            foreach (string t in _file)
            {
                if (System.IO.File.Exists(t) && AddFile(t))
                {
                    cFile cf = new cFile() { fileId = fileCount++, filename = t };
                    _fileCollection?.Add(cf);
                }
            }
            _updateFileCount?.Invoke(fileCount);

            foreach (string t in _file)
            {
                if (Directory.Exists(t))
                {
                    if (TrrntZip.Program.InZip == zipType.dir)
                    {
                        cFile cf = new cFile() { fileId = fileCount++, filename = t, isDir = true };
                        _fileCollection?.Add(cf);
                    }
                    else
                        AddDirectory(t);
                }
            }
            _processFileEndCallBack?.Invoke(-1, 0, TrrntZipStatus.Unknown);
        }

        /// <summary>
        /// Recursively adds files from a directory.
        /// </summary>
        /// <param name="dir">The directory path to scan.</param>
        private void AddDirectory(string dir)
        {
            string[] fileInfo;
            try
            {
                fileInfo = System.IO.Directory.GetFiles(dir);
            }
            catch
            {
                return;
            }

            foreach (string t in fileInfo)
            {
                if (AddFile(t))
                {
                    cFile cf = new cFile() { fileId = fileCount++, filename = t };
                    _fileCollection?.Add(cf);
                }
            }
            _updateFileCount?.Invoke(fileCount);

            string[] dirInfo;
            try
            {
                dirInfo = System.IO.Directory.GetDirectories(dir);
            }
            catch
            {
                return;
            }

            foreach (string t in dirInfo)
            {
                AddDirectory(t);
            }
        }

        /// <summary>
        /// Checks if a file should be added based on its extension and current settings.
        /// </summary>
        /// <param name="filename">The file path to check.</param>
        /// <returns>True if the file should be processed; otherwise, false.</returns>
        private static bool AddFile(string filename)
        {
            string ext = Path.GetExtension(filename).ToLower();

            if (TrrntZip.Program.InZip == zipType.all)
                return true;

            if (ext == ".zip" && (TrrntZip.Program.InZip == zipType.zip || TrrntZip.Program.InZip == zipType.archive))
                return true;

            if (ext == ".7z" && (TrrntZip.Program.InZip == zipType.sevenzip || TrrntZip.Program.InZip == zipType.archive))
                return true;

            return false;
        }
    }
}
