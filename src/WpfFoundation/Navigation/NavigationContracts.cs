using System.ComponentModel;

namespace WpfFoundation.Navigation;

/// <summary>A page ViewModel shown in the sidebar. An implicit DataTemplate for its type supplies the view.</summary>
public interface INavigationPage
{
    /// <summary>The name shown in the sidebar.</summary>
    string NavigationName { get; }

    /// <summary>A Segoe Fluent Icons glyph shown before the name, or <see langword="null"/> for none.</summary>
    string? NavigationIcon { get; }
}

/// <summary>A page that loads asynchronously before it is first shown.</summary>
/// <remarks>
/// The navigation service initializes the page once successfully. When initialization fails or is cancelled, the
/// navigation fails and the next navigation to the page tries again.
/// </remarks>
public interface IInitializeAsync
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

/// <summary>A page that refreshes every time it becomes the current page.</summary>
public interface IPageActivation
{
    /// <summary>Runs after the page becomes current, while further navigation is still unavailable.</summary>
    Task OnActivatedAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Application rules for leaving a page, for example while an operation runs or unsaved changes need saving.
/// </summary>
public interface INavigationGuard
{
    /// <summary>Whether navigation is currently possible. The sidebar is disabled while it is <see langword="false"/>.</summary>
    bool CanNavigate { get; }

    /// <summary>Raised when <see cref="CanNavigate"/> changes.</summary>
    event EventHandler? CanNavigateChanged;

    /// <summary>Decides whether to leave <paramref name="from"/> for <paramref name="to"/>; it may save, ask or refuse.</summary>
    Task<bool> ConfirmNavigationAsync(INavigationPage from, INavigationPage to, CancellationToken cancellationToken);
}

/// <summary>Navigates between a fixed set of retained pages.</summary>
public interface INavigationService : INotifyPropertyChanged
{
    IReadOnlyList<INavigationPage> Pages { get; }

    /// <summary>
    /// The current page, or <see langword="null"/> before the first navigation. Setting it, as a bound sidebar
    /// does, requests navigation; when that navigation does not happen, the change is raised again so the
    /// sidebar returns to the current page.
    /// </summary>
    INavigationPage? CurrentPage { get; set; }

    /// <summary>Whether a navigation can start: none is running and the guard allows it.</summary>
    bool CanNavigate { get; }

    /// <summary>Raised when initializing or activating a page throws.</summary>
    event EventHandler<NavigationFailedEventArgs>? NavigationFailed;

    /// <summary>Navigates to one of <see cref="Pages"/>.</summary>
    /// <returns>
    /// <see langword="true"/> when the page is current afterwards; <see langword="false"/> when the navigation was
    /// rejected by the guard, overlapped another navigation, failed or was cancelled. The current page is unchanged then.
    /// </returns>
    Task<bool> NavigateAsync(INavigationPage page, CancellationToken cancellationToken = default);
}

public sealed class NavigationFailedEventArgs(INavigationPage page, Exception exception) : EventArgs
{
    public INavigationPage Page { get; } = page;

    public Exception Exception { get; } = exception;
}
