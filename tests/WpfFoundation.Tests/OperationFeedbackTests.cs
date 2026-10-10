using WpfFoundation.Operations;
using WpfFoundation.Taskbar;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class OperationFeedbackTests
{
    private readonly FakeTaskbar taskbar = new();
    private readonly ManualTime time = new();
    private readonly OperationFeedback feedback;

    public OperationFeedbackTests()
    {
        feedback = new OperationFeedback(taskbar, time);
    }

    [TestMethod]
    public void StartingShowsIndeterminateProgressUntilTheFirstFraction()
    {
        var activity = feedback.Start("Converting");
        Assert.AreEqual((null, TaskbarProgressState.Normal), taskbar.Progress);

        time.Advance(OperationFeedback.ProgressInterval);
        activity.Report(0.25);
        Assert.AreEqual((0.25, TaskbarProgressState.Normal), taskbar.Progress);
    }

    [TestMethod]
    public void ProgressIsThrottledAndTheLatestValueIsShownAfterTheInterval()
    {
        var activity = feedback.Start("Converting");
        int pushes = taskbar.ProgressCalls;

        activity.Report(0.1);
        activity.Report(0.2);
        activity.Report(0.3);
        Assert.AreEqual(pushes, taskbar.ProgressCalls, "Updates within the interval wait.");

        time.Advance(OperationFeedback.ProgressInterval);
        Assert.AreEqual(pushes + 1, taskbar.ProgressCalls);
        Assert.AreEqual((0.3, TaskbarProgressState.Normal), taskbar.Progress);

        time.Advance(OperationFeedback.ProgressInterval);
        activity.Report(0.4);
        Assert.AreEqual((0.4, TaskbarProgressState.Normal), taskbar.Progress, "An update after a quiet interval shows at once.");
    }

    [TestMethod]
    public void ChangingStateIsNotThrottled()
    {
        var activity = feedback.Start("Converting");
        activity.Report(0.5);
        activity.SetPaused(true);
        Assert.AreEqual((0.5, TaskbarProgressState.Paused), taskbar.Progress, "Pausing also sends the pending fraction.");

        activity.SetPaused(false);
        Assert.AreEqual((0.5, TaskbarProgressState.Normal), taskbar.Progress);
    }

    [TestMethod]
    public void OverlappingActivitiesShowTheMostRecentProgressAndTheMostSevereState()
    {
        var scan = feedback.Start("Scanning");
        time.Advance(OperationFeedback.ProgressInterval);
        scan.Report(0.8);
        var convert = feedback.Start("Converting");
        Assert.AreEqual((null, TaskbarProgressState.Normal), taskbar.Progress, "The newest activity's progress shows.");

        scan.SetPaused(true);
        Assert.AreEqual((null, TaskbarProgressState.Paused), taskbar.Progress, "Any paused activity turns the bar yellow.");

        convert.Complete(OperationOutcome.Failed, "Disk full");
        Assert.AreEqual((0.8, TaskbarProgressState.Error), taskbar.Progress, "A failure outranks the paused state.");

        scan.SetPaused(false);
        Assert.AreEqual((0.8, TaskbarProgressState.Error), taskbar.Progress, "The failure stays until the window is activated.");

        taskbar.Activate();
        Assert.AreEqual((0.8, TaskbarProgressState.Normal), taskbar.Progress);
    }

    [TestMethod]
    public void EachOutcomeShowsItsBadgeAndFlashes()
    {
        (OperationOutcome Outcome, TaskbarOverlay Badge)[] cases =
        [
            (OperationOutcome.Succeeded, TaskbarOverlay.Success),
            (OperationOutcome.CompletedWithErrors, TaskbarOverlay.Warning),
            (OperationOutcome.Failed, TaskbarOverlay.Error)
        ];

        foreach (var (outcome, badge) in cases)
        {
            taskbar.Activate();
            int flashes = taskbar.Flashes;
            feedback.Start("Converting").Complete(outcome, "12 files");

            Assert.AreEqual((badge, "12 files"), taskbar.Overlay, outcome.ToString());
            Assert.AreEqual(flashes + 1, taskbar.Flashes, outcome.ToString());
        }

        Assert.AreEqual((null, TaskbarProgressState.Error), taskbar.Progress, "A failure leaves the bar red.");
        taskbar.Activate();
        Assert.IsNull(taskbar.Progress, "Activating the window clears the failure.");
        Assert.AreEqual((TaskbarOverlay.None, null), taskbar.Overlay);
    }

    [TestMethod]
    public void SuccessClearsTheProgressAndUsesTheTitleWithoutASummary()
    {
        var activity = feedback.Start("Backing up");
        activity.Complete(OperationOutcome.Succeeded);

        Assert.IsNull(taskbar.Progress);
        Assert.AreEqual((TaskbarOverlay.Success, "Backing up"), taskbar.Overlay);
    }

    [TestMethod]
    public void CancellingClearsTheProgressWithoutBadgeOrFlash()
    {
        var activity = feedback.Start("Converting");
        activity.Report(0.4);
        activity.Complete(OperationOutcome.Cancelled);

        Assert.IsNull(taskbar.Progress);
        Assert.AreEqual((TaskbarOverlay.None, null), taskbar.Overlay);
        Assert.AreEqual(0, taskbar.Flashes);
    }

    [TestMethod]
    public void ALaterMilderOutcomeDoesNotReplaceAnUnseenBadge()
    {
        feedback.Start("Converting").Complete(OperationOutcome.Failed, "Disk full");
        feedback.Start("Scanning").Complete(OperationOutcome.Succeeded, "40 files");
        Assert.AreEqual((TaskbarOverlay.Error, "Disk full"), taskbar.Overlay);

        taskbar.Activate();
        feedback.Start("Scanning").Complete(OperationOutcome.Succeeded, "40 files");
        Assert.AreEqual((TaskbarOverlay.Success, "40 files"), taskbar.Overlay, "After activation the next outcome shows.");
    }

    [TestMethod]
    public void ACompletedActivityIgnoresLaterCalls()
    {
        var activity = feedback.Start("Converting");
        activity.Complete(OperationOutcome.Succeeded);
        int calls = taskbar.ProgressCalls;

        activity.Report(0.5);
        activity.SetPaused(true);
        activity.Complete(OperationOutcome.Failed);

        Assert.AreEqual(calls, taskbar.ProgressCalls);
        Assert.AreEqual((TaskbarOverlay.Success, "Converting"), taskbar.Overlay);
    }

    [TestMethod]
    public void InvalidArgumentsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => feedback.Start(" "));
        var activity = feedback.Start("Converting");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => activity.Report(1.5));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => activity.Report(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => activity.Complete((OperationOutcome)42));
    }

    /// <summary>Records the latest taskbar state.</summary>
    private sealed class FakeTaskbar : ITaskbarService
    {
        public event EventHandler? WindowActivated;

        public (double? Value, TaskbarProgressState State)? Progress { get; private set; }

        public int ProgressCalls { get; private set; }

        public (TaskbarOverlay Overlay, string? Description) Overlay { get; private set; } = (TaskbarOverlay.None, null);

        public int Flashes { get; private set; }

        public void SetProgress(double? value, TaskbarProgressState state = TaskbarProgressState.Normal)
        {
            ProgressCalls++;
            Progress = (value, state);
        }

        public void ClearProgress()
        {
            ProgressCalls++;
            Progress = null;
        }

        public void ShowOverlay(TaskbarOverlay overlay, string? description = null) => Overlay = (overlay, description);

        public void FlashUntilActivated() => Flashes++;

        /// <summary>Clears what the real service clears on activation, then raises the event.</summary>
        public void Activate()
        {
            Overlay = (TaskbarOverlay.None, null);
            if (Progress is { State: TaskbarProgressState.Error })
            {
                Progress = null;
            }

            WindowActivated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A clock that moves only when told to, firing due timers synchronously.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private long ticks = 1;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks;

        public void Advance(TimeSpan by)
        {
            ticks += by.Ticks;
            foreach (var timer in timers.Where(timer => timer.DueAt <= ticks).ToArray())
            {
                timers.Remove(timer);
                timer.Fire();
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, () => callback(state), ticks + dueTime.Ticks);
            timers.Add(timer);
            return timer;
        }

        private sealed class ManualTimer(ManualTime owner, Action fire, long dueAt) : ITimer
        {
            public long DueAt { get; } = dueAt;

            public void Fire() => fire();

            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

            public void Dispose() => owner.timers.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
