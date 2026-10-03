namespace SAM_UI_Avalonia.Services;

/// <inheritdoc cref="IProcessRunner"/>
public sealed class ProcessRunner : IProcessRunner
{
    public bool IsRunning { get; private set; }

    public bool IsPaused { get; private set; }

    public bool TogglePause()
    {
        if (!IsRunning)
            return IsPaused;

        IsPaused = !IsPaused;
        return IsPaused;
    }

    public void Stop()
    {
        IsRunning = false;
        IsPaused = false;
    }
}
