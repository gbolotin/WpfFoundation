using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Navigation;
using WpfFoundation.Notifications;
using WpfFoundation.Operations;

namespace WpfFoundation.Gallery.ViewModels;

/// <summary>
/// Drives a simulated photo import on the taskbar and in notifications, so each state can be watched with the window
/// minimized.
/// </summary>
public sealed partial class TaskbarViewModel(IOperationFeedback feedback, INotificationService notifications) : ObservableObject, INavigationPage
{
    private static readonly TimeSpan FinishDelay = TimeSpan.FromSeconds(5);
    private IOperationActivity? activity;

    public string NavigationName => "Taskbar and notifications";

    public string? NavigationIcon => "\uEA8F";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    public partial double? Progress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseLabel))]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial bool IsFinishing { get; set; }

    [ObservableProperty]
    public partial bool DelayFinish { get; set; } = true;

    [ObservableProperty]
    public partial string Status { get; set; } = "No import running.";

    public bool IsRunning => activity is not null;

    public bool IsIndeterminate => IsRunning && Progress is null;

    public string PauseLabel => IsPaused ? "Resume" : "Pause";

    private bool CanStart => activity is null && !IsFinishing;

    private bool CanChange => activity is not null && !IsFinishing;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        activity = feedback.Start("Photo import");
        IsPaused = false;
        Progress = null;
        Refresh();
        Status = "Importing photos. The taskbar button shows an indeterminate bar until the first step.";
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private void Advance()
    {
        Progress = Math.Min(1, Math.Round((Progress ?? 0) + 0.1, 1));
        activity!.Report(Progress);
        Status = $"Imported {Progress:P0} of the photos.";
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        activity!.SetPaused(IsPaused);
        Status = IsPaused ? "The import is paused, so the bar turns yellow." : "The import resumed.";
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task FinishAsync(OperationOutcome outcome)
    {
        IsFinishing = true;
        Refresh();
        if (DelayFinish)
        {
            Status = "Finishing in 5 seconds. Minimize the window or switch to another app to watch the taskbar button.";
            await Task.Delay(FinishDelay);
        }

        activity!.Complete(outcome, outcome switch
        {
            OperationOutcome.Succeeded => "Imported 48 photos from Granite Ridge.",
            OperationOutcome.CompletedWithErrors => "Imported 45 photos; 3 could not be read.",
            OperationOutcome.Failed => "The import failed: the memory card was removed.",
            _ => null
        });
        activity = null;
        IsFinishing = false;
        IsPaused = false;
        Progress = null;
        Refresh();
        Status = outcome switch
        {
            OperationOutcome.Cancelled => "The import was cancelled, so the taskbar button cleared without a badge.",
            _ => "The import finished. The badge, flashing and any red bar clear when the window is next activated."
        };
    }

    [RelayCommand]
    private async Task SendTestNotificationAsync()
    {
        if (DelayFinish)
        {
            Status = "Sending a test notification in 5 seconds.";
            await Task.Delay(FinishDelay);
        }

        notifications.Show("Granite Ridge photos", "This is a test notification from the WpfFoundation Gallery.");
        Status = "A test notification was sent. Click it to bring the Gallery to the front.";
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsIndeterminate));
        StartCommand.NotifyCanExecuteChanged();
        AdvanceCommand.NotifyCanExecuteChanged();
        TogglePauseCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }
}
