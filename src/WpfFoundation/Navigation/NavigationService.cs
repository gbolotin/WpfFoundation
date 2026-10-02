using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfFoundation.Navigation;

/// <summary>
/// Navigates between a fixed set of pages. Pages initialize once before they are first shown, refresh on every
/// activation when they ask to, and the application's <see cref="INavigationGuard"/> can block or refuse a change.
/// </summary>
/// <remarks>
/// Create it on the UI thread: when a navigation does not happen, the current-page change is raised again through
/// the thread's synchronization context, after the sidebar's own selection change has finished.
/// </remarks>
public sealed class NavigationService : ObservableObject, INavigationService, IDisposable
{
    private readonly INavigationGuard? guard;
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private readonly HashSet<INavigationPage> initialized = new(ReferenceEqualityComparer.Instance);
    private INavigationPage? currentPage;
    private bool isNavigating;

    /// <param name="pages">The pages, in sidebar order.</param>
    /// <param name="guard">The application's navigation rules, or <see langword="null"/> for none.</param>
    /// <param name="initialPage">
    /// A page that is current from the start, so a window shows it before startup work completes. Constructing the
    /// service starts no work: the page is treated as initialized and is not activated, so the application loads it
    /// during its own startup. Without it, <see cref="CurrentPage"/> stays <see langword="null"/> until the first navigation.
    /// </param>
    public NavigationService(IEnumerable<INavigationPage> pages, INavigationGuard? guard = null, INavigationPage? initialPage = null)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Pages = [.. pages];
        if (Pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        if (Pages.Distinct(ReferenceEqualityComparer.Instance).Count() != Pages.Count)
        {
            throw new ArgumentException("Each page can be added once.", nameof(pages));
        }

        if (initialPage is not null)
        {
            if (!Pages.Contains(initialPage, ReferenceEqualityComparer.Instance))
            {
                throw new ArgumentException("The initial page must be one of the pages.", nameof(initialPage));
            }

            currentPage = initialPage;
            initialized.Add(initialPage);
        }

        this.guard = guard;
        if (guard is not null)
        {
            guard.CanNavigateChanged += OnGuardChanged;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<INavigationPage> Pages { get; }

    /// <inheritdoc />
    public INavigationPage? CurrentPage
    {
        get => currentPage;
        set
        {
            if (value is not null && !ReferenceEquals(value, currentPage))
            {
                _ = NavigateAsync(value);
            }
        }
    }

    /// <inheritdoc />
    public bool CanNavigate => !isNavigating && (guard?.CanNavigate ?? true);

    /// <inheritdoc />
    public event EventHandler<NavigationFailedEventArgs>? NavigationFailed;

    /// <inheritdoc />
    public async Task<bool> NavigateAsync(INavigationPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!Pages.Contains(page, ReferenceEqualityComparer.Instance))
        {
            throw new ArgumentException("The page is not one of this service's pages.", nameof(page));
        }

        if (ReferenceEquals(page, currentPage))
        {
            return true;
        }

        if (!CanNavigate)
        {
            RestoreSelection();
            return false;
        }

        SetNavigating(true);
        Exception failure;
        try
        {
            if (currentPage is not null && guard is not null && !await guard.ConfirmNavigationAsync(currentPage, page, cancellationToken))
            {
                RestoreSelection();
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (page is IInitializeAsync initializable && !initialized.Contains(page))
            {
                await initializable.InitializeAsync(cancellationToken);
                initialized.Add(page);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SetProperty(ref currentPage, page, nameof(CurrentPage));
            if (page is IPageActivation activation)
            {
                await activation.OnActivatedAsync(cancellationToken);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            RestoreSelection();
            return ReferenceEquals(currentPage, page);
        }
        catch (Exception exception)
        {
            RestoreSelection();
            failure = exception;
        }
        finally
        {
            SetNavigating(false);
        }

        // Raised once navigation is available again, so a handler may show a dialog or navigate elsewhere.
        NavigationFailed?.Invoke(this, new NavigationFailedEventArgs(page, failure));
        return ReferenceEquals(currentPage, page);
    }

    public void Dispose()
    {
        if (guard is not null)
        {
            guard.CanNavigateChanged -= OnGuardChanged;
        }
    }

    private void SetNavigating(bool value)
    {
        isNavigating = value;
        OnPropertyChanged(nameof(CanNavigate));
    }

    // A ListBox ignores a change raised while it is still pushing its own selection, so raise it afterwards.
    private void RestoreSelection()
    {
        if (context is null)
        {
            OnPropertyChanged(nameof(CurrentPage));
        }
        else
        {
            context.Post(_ => OnPropertyChanged(nameof(CurrentPage)), null);
        }
    }

    private void OnGuardChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(CanNavigate));
}
