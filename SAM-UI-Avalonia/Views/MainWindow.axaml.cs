using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SAM_UI_Avalonia.ViewModels;
using TrrntZip;

namespace SAM_UI_Avalonia.Views;

public partial class MainWindow : Window
{
    private ObservableCollection<samFile>? _files;

    public MainWindow()
    {
        InitializeComponent();

        DropTarget.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropTarget.AddHandler(DragDrop.DropEvent, OnDrop);

        DataContextChanged += OnDataContextChanged;
        OnDataContextChanged(this, EventArgs.Empty);

        Closing += OnClosing;

        // Rows stay hit testable so the mouse wheel scrolls, so clear any
        // selection a click would otherwise make.
        FilesGrid.SelectionChanged += OnFilesGridSelectionChanged;
    }

    private void OnFilesGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FilesGrid.SelectedItem is not null)
            FilesGrid.SelectedItem = null;
    }

    /// <summary>
    /// The window may only be closed while nothing is being processed, i.e. no
    /// worker is busy, the queue is empty and no files are being added.
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (MainQueue.IsBusy)
        {
            e.Cancel = true;
            return;
        }

        Workers.RemoveAllWorks();
    }

    private static List<string> GetPaths(DragEventArgs e)
    {
        var files = e.DataTransfer?.TryGetFiles();

        if (files is null)
            return new List<string>();

        return files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToList();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer?.Contains(DataFormat.File) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainWindowViewModel vm)
            return;

        List<string> paths = GetPaths(e);

        if (paths.Count == 0)
            return;

        try
        {
           vm.FilesDroppedAsync(paths);
        }
        catch (OperationCanceledException)
        {
            // Scan was cancelled - nothing to report.
        }
    }

    /// <summary>
    /// Keeps the newest row visible: whenever an item is appended to the bound
    /// collection the grid is scrolled down to it.
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_files is not null)
            _files.CollectionChanged -= OnFilesChanged;

        _files = (DataContext as MainWindowViewModel)?.Files;

        if (_files is not null)
            _files.CollectionChanged += OnFilesChanged;
    }

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        if (_files is null || _files.Count == 0)
            return;

        object last = _files[_files.Count - 1];

        // Let the grid realise the new row before asking it to scroll.
        Dispatcher.UIThread.Post(() => FilesGrid.ScrollIntoView(last, null), DispatcherPriority.Background);
    }
}