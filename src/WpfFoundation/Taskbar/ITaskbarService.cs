namespace WpfFoundation.Taskbar;

/// <summary>The color of the taskbar progress bar.</summary>
public enum TaskbarProgressState
{
    /// <summary>Green: the operation is running.</summary>
    Normal,

    /// <summary>Yellow: the operation is paused.</summary>
    Paused,

    /// <summary>Red: the operation failed. Cleared when the window is next activated.</summary>
    Error
}

/// <summary>The badge drawn over the taskbar button.</summary>
public enum TaskbarOverlay
{
    /// <summary>No badge.</summary>
    None,

    /// <summary>A check mark in the success color.</summary>
    Success,

    /// <summary>A warning triangle in the caution color.</summary>
    Warning,

    /// <summary>An error mark in the critical color.</summary>
    Error
}

/// <summary>
/// Shows progress and outcomes on the taskbar button of the application's main window, so they can be seen while the
/// window is minimized or behind another app. Members may be called from any thread.
/// </summary>
public interface ITaskbarService
{
    /// <summary>Raised on the UI thread after the main window is activated and the overlay and error state have cleared.</summary>
    event EventHandler? WindowActivated;

    /// <summary>Shows the progress bar.</summary>
    /// <param name="value">
    /// The fraction done, from 0 to 1, or <see langword="null"/> while it is unknown. A running operation then shows an
    /// indeterminate bar; a paused or failed one keeps the last value shown.
    /// </param>
    /// <param name="state">The color of the bar.</param>
    void SetProgress(double? value, TaskbarProgressState state = TaskbarProgressState.Normal);

    /// <summary>Removes the progress bar.</summary>
    void ClearProgress();

    /// <summary>Shows a badge on the taskbar button until the window is next activated.</summary>
    /// <param name="overlay">The badge, or <see cref="TaskbarOverlay.None"/> to remove it.</param>
    /// <param name="description">
    /// Text for the thumbnail tooltip, so the outcome does not rely on color alone. Wording comes from the application.
    /// </param>
    void ShowOverlay(TaskbarOverlay overlay, string? description = null);

    /// <summary>Flashes the taskbar button until the window is activated. Does nothing while the window is active.</summary>
    void FlashUntilActivated();
}
