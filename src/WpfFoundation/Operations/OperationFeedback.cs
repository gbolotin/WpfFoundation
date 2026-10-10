using WpfFoundation.Notifications;
using WpfFoundation.Taskbar;

namespace WpfFoundation.Operations;

/// <summary>How an operation ended.</summary>
public enum OperationOutcome
{
    /// <summary>Everything succeeded.</summary>
    Succeeded,

    /// <summary>The operation finished, but some of its items failed.</summary>
    CompletedWithErrors,

    /// <summary>The operation failed.</summary>
    Failed,

    /// <summary>The user cancelled the operation, so nothing asks for attention.</summary>
    Cancelled
}

/// <summary>Reports long operations outside the window: on the taskbar button and, optionally, in Windows notifications.</summary>
public interface IOperationFeedback
{
    /// <summary>Starts reporting an operation. Its progress shows as indeterminate until the first fraction arrives.</summary>
    /// <param name="title">The operation's name, for example "Converting". Wording comes from the application.</param>
    IOperationActivity Start(string title);
}

/// <summary>One running operation started by <see cref="IOperationFeedback.Start"/>. Members may be called from any thread.</summary>
public interface IOperationActivity
{
    /// <summary>The operation's name.</summary>
    string Title { get; }

    /// <summary>Reports the fraction done, from 0 to 1, or <see langword="null"/> while it is unknown.</summary>
    void Report(double? fraction);

    /// <summary>Pauses or resumes the operation.</summary>
    void SetPaused(bool paused);

    /// <summary>Ends the operation. Later calls on this activity are ignored.</summary>
    /// <param name="outcome">How the operation ended.</param>
    /// <param name="summary">
    /// A one-line result, for example "12 files converted", shown in the taskbar thumbnail tooltip. The title is used
    /// when it is <see langword="null"/>.
    /// </param>
    void Complete(OperationOutcome outcome, string? summary = null);
}

/// <summary>
/// Drives <see cref="ITaskbarService"/> from running activities. With overlapping activities the taskbar shows the most
/// severe state (an unseen failure, then paused, then running) and the most recently started activity's progress.
/// Finishing shows the most severe outcome since the window was last activated as an overlay badge and flashes the
/// taskbar button when the window is not in front; activating the window clears both. Progress updates are throttled.
/// With an <see cref="INotificationService"/>, an outcome other than <see cref="OperationOutcome.Cancelled"/> is also
/// sent as a notification, but only while the main window is not the active window.
/// </summary>
public sealed class OperationFeedback : IOperationFeedback
{
    /// <summary>The shortest time between two progress updates sent to the taskbar.</summary>
    internal static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

    private readonly Lock gate = new();
    private readonly ITaskbarService taskbar;
    private readonly TimeProvider time;
    private readonly INotificationService? notifications;
    private readonly List<Activity> running = [];
    private bool failureUnseen;
    private TaskbarOverlay overlay;
    private bool hasPushed;
    private long lastPush;
    private ITimer? pendingPush;

    public OperationFeedback(ITaskbarService taskbar)
        : this(taskbar, TimeProvider.System)
    {
    }

    /// <param name="taskbar">The taskbar to drive.</param>
    /// <param name="timeProvider">The clock that throttles progress updates.</param>
    public OperationFeedback(ITaskbarService taskbar, TimeProvider timeProvider)
        : this(taskbar, null, timeProvider)
    {
    }

    /// <param name="taskbar">The taskbar to drive.</param>
    /// <param name="notifications">Sends a notification when an operation ends while the window is not active.</param>
    public OperationFeedback(ITaskbarService taskbar, INotificationService notifications)
        : this(taskbar, notifications, TimeProvider.System)
    {
        ArgumentNullException.ThrowIfNull(notifications);
    }

    /// <param name="taskbar">The taskbar to drive.</param>
    /// <param name="notifications">
    /// Sends a notification when an operation ends while the window is not active, or <see langword="null"/> for none.
    /// </param>
    /// <param name="timeProvider">The clock that throttles progress updates.</param>
    public OperationFeedback(ITaskbarService taskbar, INotificationService? notifications, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(taskbar);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.taskbar = taskbar;
        this.notifications = notifications;
        time = timeProvider;
        taskbar.WindowActivated += OnWindowActivated;
    }

    /// <inheritdoc />
    public IOperationActivity Start(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var activity = new Activity(this, title);
        lock (gate)
        {
            running.Add(activity);
            PushNow();
        }

        return activity;
    }

    private void Report(Activity activity, double? fraction)
    {
        if (fraction is { } value && !(value >= 0 && value <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "Progress is a fraction from 0 to 1.");
        }

        lock (gate)
        {
            if (activity.IsCompleted || activity.Fraction == fraction)
            {
                return;
            }

            activity.Fraction = fraction;
            PushThrottled();
        }
    }

    private void SetPaused(Activity activity, bool paused)
    {
        lock (gate)
        {
            if (activity.IsCompleted || activity.IsPaused == paused)
            {
                return;
            }

            activity.IsPaused = paused;
            PushNow();
        }
    }

    private void Complete(Activity activity, OperationOutcome outcome, string? summary)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome.");
        }

        lock (gate)
        {
            if (activity.IsCompleted)
            {
                return;
            }

            activity.IsCompleted = true;
            running.Remove(activity);
            var badge = outcome switch
            {
                OperationOutcome.Succeeded => TaskbarOverlay.Success,
                OperationOutcome.CompletedWithErrors => TaskbarOverlay.Warning,
                OperationOutcome.Failed => TaskbarOverlay.Error,
                _ => TaskbarOverlay.None
            };

            failureUnseen |= outcome == OperationOutcome.Failed;
            PushNow();
            if (badge != TaskbarOverlay.None)
            {
                // A less severe outcome does not replace a badge that has not been seen yet.
                if (badge >= overlay)
                {
                    overlay = badge;
                    taskbar.ShowOverlay(badge, string.IsNullOrWhiteSpace(summary) ? activity.Title : summary);
                }

                taskbar.FlashUntilActivated();
            }
        }

        // Outside the lock: the notification service may be slow, and the user asked for a cancellation.
        if (notifications is not null && outcome != OperationOutcome.Cancelled && !taskbar.IsWindowActive)
        {
            var kind = outcome switch
            {
                OperationOutcome.Succeeded => NotificationKind.Success,
                OperationOutcome.CompletedWithErrors => NotificationKind.Warning,
                _ => NotificationKind.Error
            };
            notifications.Show(activity.Title, summary ?? string.Empty, kind);
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        lock (gate)
        {
            failureUnseen = false;
            overlay = TaskbarOverlay.None;
            PushNow();
        }
    }

    private void PushThrottled()
    {
        if (pendingPush is not null)
        {
            return;
        }

        var elapsed = time.GetElapsedTime(lastPush);
        if (!hasPushed || elapsed >= ProgressInterval)
        {
            PushNow();
            return;
        }

        pendingPush = time.CreateTimer(_ => OnPendingPush(), null, ProgressInterval - elapsed, Timeout.InfiniteTimeSpan);
    }

    private void OnPendingPush()
    {
        lock (gate)
        {
            if (pendingPush is not null)
            {
                PushNow();
            }
        }
    }

    private void PushNow()
    {
        pendingPush?.Dispose();
        pendingPush = null;
        hasPushed = true;
        lastPush = time.GetTimestamp();
        if (running.Count == 0)
        {
            if (failureUnseen)
            {
                taskbar.SetProgress(null, TaskbarProgressState.Error);
            }
            else
            {
                taskbar.ClearProgress();
            }

            return;
        }

        var state = failureUnseen ? TaskbarProgressState.Error
            : running.Exists(activity => activity.IsPaused) ? TaskbarProgressState.Paused
            : TaskbarProgressState.Normal;
        taskbar.SetProgress(running[^1].Fraction, state);
    }

    private sealed class Activity(OperationFeedback owner, string title) : IOperationActivity
    {
        public string Title { get; } = title;

        public double? Fraction { get; set; }

        public bool IsPaused { get; set; }

        public bool IsCompleted { get; set; }

        public void Report(double? fraction) => owner.Report(this, fraction);

        public void SetPaused(bool paused) => owner.SetPaused(this, paused);

        public void Complete(OperationOutcome outcome, string? summary = null) => owner.Complete(this, outcome, summary);
    }
}
