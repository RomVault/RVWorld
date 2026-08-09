using System;
using Avalonia.Controls;
using Avalonia.Threading;
using RomVaultCore;
using ROMVault.Avalonia.Views;

namespace ROMVault.Avalonia.Services;

/// <summary>
/// Coordinates the common UI lifecycle for ROMVault background workers.
/// </summary>
public sealed class BackgroundOperationRunner
{
    private readonly Window _owner;
    private readonly Action<string> _onStarting;
    private readonly Action _onFinished;

    public BackgroundOperationRunner(
        Window owner,
        Action<string> onStarting,
        Action onFinished)
    {
        _owner = owner;
        _onStarting = onStarting;
        _onFinished = onFinished;
    }

    public void Run(
        string activity,
        string dialogTitle,
        ThreadWorker worker,
        Action? onCompleted = null)
    {
        _onStarting(activity);

        worker.wFinal += () => Dispatcher.UIThread.Post(() =>
        {
            try
            {
                onCompleted?.Invoke();
            }
            finally
            {
                _onFinished();
            }
        });

        var progressWindow = new ProgressWindow(worker)
        {
            Title = dialogTitle
        };

        _ = progressWindow.ShowDialog(_owner);

        try
        {
            worker.StartAsync();
        }
        catch
        {
            _onFinished();
            progressWindow.Close();
            throw;
        }
    }
}
