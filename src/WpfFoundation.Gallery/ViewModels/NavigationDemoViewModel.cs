using CommunityToolkit.Mvvm.ComponentModel;
using WpfFoundation.Dialogs;
using WpfFoundation.Navigation;

namespace WpfFoundation.Gallery.ViewModels;

/// <summary>The Overview page. Pages without state still get their own type, so a DataTemplate can pick their view.</summary>
public sealed class OverviewPageViewModel : INavigationPage
{
    public string NavigationName => "Overview";

    public string? NavigationIcon => "\uE80F";
}

public sealed class StyledControlsPageViewModel : INavigationPage
{
    public string NavigationName => "Styled controls";

    public string? NavigationIcon => "\uE790";
}

public sealed class CustomUiPageViewModel : INavigationPage
{
    public string NavigationName => "Custom UI";

    public string? NavigationIcon => "\uE8A9";
}

/// <summary>
/// The Navigation page. Its first initialization fails on purpose to show the retry, it keeps a draft across visits,
/// and as the Gallery's navigation guard it disables the sidebar while busy and asks before leaving unsaved changes.
/// </summary>
public sealed partial class NavigationDemoViewModel(IDialogService dialogs) : ObservableObject, INavigationPage, IInitializeAsync, INavigationGuard
{
    private int initializationAttempts;

    public string NavigationName => "Navigation";

    public string? NavigationIcon => "\uE700";

    [ObservableProperty]
    public partial string Draft { get; set; } = "Fog lifted over the quarry by nine.";

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string InitializationSummary { get; set; } = string.Empty;

    public bool CanNavigate => !IsBusy;

    public event EventHandler? CanNavigateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        initializationAttempts++;
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        if (initializationAttempts == 1)
        {
            throw new InvalidOperationException("The trail index was offline on the first visit. Select Navigation again to retry.");
        }

        InitializationSummary = $"Loaded on attempt {initializationAttempts}. The first attempt failed on purpose, so the sidebar stayed on the previous page.";
    }

    public async Task<bool> ConfirmNavigationAsync(INavigationPage from, INavigationPage to, CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(from, this) || !HasUnsavedChanges)
        {
            return true;
        }

        bool leave = await dialogs.ConfirmAsync("Leave this page?", "Your draft note has unsaved changes.", "Leave", "Stay", isDestructive: true);
        if (leave)
        {
            HasUnsavedChanges = false;
        }

        return leave;
    }

    partial void OnIsBusyChanged(bool value) => CanNavigateChanged?.Invoke(this, EventArgs.Empty);
}
