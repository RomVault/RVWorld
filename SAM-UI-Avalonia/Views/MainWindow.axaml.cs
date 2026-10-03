using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using SAM_UI_Avalonia.ViewModels;

namespace SAM_UI_Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DropTarget.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropTarget.AddHandler(DragDrop.DropEvent, OnDrop);
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

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainWindowViewModel vm)
            return;

        List<string> paths = GetPaths(e);

        if (paths.Count == 0)
            return;

        try
        {
            await vm.FilesDroppedAsync(paths);
        }
        catch (OperationCanceledException)
        {
            // Scan was cancelled - nothing to report.
        }
    }
}