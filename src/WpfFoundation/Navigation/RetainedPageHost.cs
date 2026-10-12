using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfFoundation.Navigation;

/// <summary>
/// Shows the current page of an <see cref="INavigationService"/>. Each page's view is created from its DataTemplate
/// on the first visit and kept, hidden, while other pages are shown, so scroll positions, selections and edits
/// survive navigation. When the service's <see cref="INavigationService.Pages"/> raises collection changes, as a
/// <see cref="Documents.DocumentWorkspace"/> does when a document closes, the views of removed pages are released;
/// all views are released when <see cref="Navigation"/> changes.
/// </summary>
public sealed class RetainedPageHost : FrameworkElement
{
    public static readonly DependencyProperty NavigationProperty = DependencyProperty.Register(
        nameof(Navigation), typeof(INavigationService), typeof(RetainedPageHost), new PropertyMetadata(null, OnNavigationChanged));

    private readonly Grid pages = new();
    private readonly Dictionary<INavigationPage, ContentPresenter> views = new(ReferenceEqualityComparer.Instance);
    private INavigationService? subscribed;
    private bool automationRefreshPending;

    public RetainedPageHost()
    {
        AddVisualChild(pages);
        AddLogicalChild(pages);
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
    }

    public INavigationService? Navigation
    {
        get => (INavigationService?)GetValue(NavigationProperty);
        set => SetValue(NavigationProperty, value);
    }

    /// <summary>The pages whose views are currently retained.</summary>
    public IReadOnlyCollection<INavigationPage> RetainedPages => views.Keys;

    protected override int VisualChildrenCount => 1;

    protected override System.Collections.IEnumerator LogicalChildren => new[] { pages }.GetEnumerator();

    protected override Visual GetVisualChild(int index) => index == 0 ? pages : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size availableSize)
    {
        pages.Measure(availableSize);
        return pages.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        pages.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HostAutomationPeer(this);

    // Exposes only the shown page to screen readers; the hidden, retained views stay out of the automation tree.
    private sealed class HostAutomationPeer(RetainedPageHost owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var current = owner.Navigation?.CurrentPage;
            return current is not null && owner.views.TryGetValue(current, out var view)
                ? new FrameworkElementAutomationPeer(view).GetChildren()
                : null;
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }

    private static void OnNavigationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var host = (RetainedPageHost)sender;
        host.Unsubscribe();
        host.pages.Children.Clear();
        host.views.Clear();
        if (host.IsLoaded)
        {
            host.Subscribe();
        }
    }

    // Subscribe only while loaded, so a long-lived navigation service never keeps a closed window alive.
    private void Subscribe()
    {
        Unsubscribe();
        if (Navigation is { } navigation)
        {
            subscribed = navigation;
            navigation.PropertyChanged += OnNavigationPropertyChanged;
            if (navigation.Pages is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += OnPagesChanged;
            }
        }

        ShowCurrentPage();
    }

    private void Unsubscribe()
    {
        if (subscribed is not null)
        {
            subscribed.PropertyChanged -= OnNavigationPropertyChanged;
            if (subscribed.Pages is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= OnPagesChanged;
            }

            subscribed = null;
        }
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(INavigationService.CurrentPage) or null)
        {
            ShowCurrentPage();
        }
    }

    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowCurrentPage();

    // Releases the views of pages the service no longer has, creates the current page's view on its first visit, and shows only it.
    private void ShowCurrentPage()
    {
        var navigation = Navigation;
        foreach (var page in views.Keys.ToArray())
        {
            if (navigation is null || !navigation.Pages.Contains(page, ReferenceEqualityComparer.Instance))
            {
                pages.Children.Remove(views[page]);
                views.Remove(page);
            }
        }

        var current = navigation?.CurrentPage;
        if (current is not null && !views.ContainsKey(current))
        {
            var view = new ContentPresenter { Content = current };
            views.Add(current, view);
            pages.Children.Add(view);
        }

        foreach (var (page, view) in views)
        {
            view.Visibility = ReferenceEquals(page, current) ? Visibility.Visible : Visibility.Collapsed;
        }

        InvalidateAutomationTree();
    }

    // Automation peers cache their children, and a TabControl's selected tab only refreshes them when its own content
    // changes. Once layout has built the shown page's view, refreshing this host's peer and the nearest peer above it lets screen
    // readers see it. Only existing peers are touched, so this costs nothing while no automation client is running.
    private void InvalidateAutomationTree()
    {
        if (automationRefreshPending)
        {
            return;
        }

        automationRefreshPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            automationRefreshPending = false;
            UIElementAutomationPeer.FromElement(this)?.ResetChildrenCache();
            for (DependencyObject? element = VisualTreeHelper.GetParent(this); element is not null; element = VisualTreeHelper.GetParent(element))
            {
                if (element is UIElement uiElement && UIElementAutomationPeer.FromElement(uiElement) is { } peer)
                {
                    // A TabControl's peer exposes the page through its selected tab's peer, which caches separately.
                    peer.ResetChildrenCache();
                    foreach (var child in peer.GetChildren() ?? [])
                    {
                        child.ResetChildrenCache();
                    }

                    return;
                }
            }
        });
    }
}
