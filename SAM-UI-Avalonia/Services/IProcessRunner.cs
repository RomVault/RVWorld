namespace SAM_UI_Avalonia.Services;

/// <summary>
/// Owns the run/pause/stop lifecycle of the file processing work.
/// </summary>
public interface IProcessRunner
{
    bool IsRunning { get; }

    bool IsPaused { get; }

    /// <summary>
    /// Toggles the paused state. Returns the new paused state.
    /// </summary>
    bool TogglePause();

    /// <summary>
    /// Stops processing and clears the paused state.
    /// </summary>
    void Stop();
}
