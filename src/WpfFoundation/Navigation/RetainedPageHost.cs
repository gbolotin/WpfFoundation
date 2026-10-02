using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfFoundation.Navigation;

/// <summary>
/// Shows the current page of an <see cref="INavigationService"/>. Each page's view is created from its DataTemplate
/// on the first visit and kept, hidden, while other pages are shown, so scroll positions, selections and edits
/// survive navigation. The views are released when <see cref="Navigation"/> changes.
/// </summary>
public sealed class RetainedPageHost : FrameworkElement
{
    public static readonly DependencyProperty NavigationProperty = DependencyProperty.Register(
        nameof(Navigation), typeof(INavigationService), typeof(RetainedPageHost), new PropertyMetadata(null, OnNavigationChanged));

    private readonly Grid pages = new();
    private readonly Dictionary<INavigationPage, ContentPresenter> views = new(ReferenceEqualityComparer.Instance);
    private INavigationService? subscribed;

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
        }

        ShowCurrentPage();
    }

    private void Unsubscribe()
    {
        if (subscribed is not null)
        {
            subscribed.PropertyChanged -= OnNavigationPropertyChanged;
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

    private void ShowCurrentPage()
    {
        var current = Navigation?.CurrentPage;
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
    }
}
