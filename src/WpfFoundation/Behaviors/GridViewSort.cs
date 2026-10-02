using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfFoundation.Behaviors;

/// <summary>
/// Makes a <see cref="GridView"/> list sortable: header clicks call <see cref="IColumnSort.SortBy"/> with the
/// column's <c>Key</c>, the comparer orders the list's own collection view (the source order is unchanged,
/// and other lists bound to the same source keep their own order), and each column's <c>Direction</c> is
/// updated for header templates to show.
/// </summary>
/// <remarks>
/// While the list is loaded, an <see cref="IList"/> items source is presented through a view owned by the list.
/// An items source that already is a <see cref="ListCollectionView"/> is sorted directly.
/// </remarks>
public static class GridViewSort
{
    /// <summary>The sort state of the list.</summary>
    public static readonly DependencyProperty SortProperty = DependencyProperty.RegisterAttached(
        "Sort", typeof(IColumnSort), typeof(GridViewSort), new PropertyMetadata(null, OnSortChanged));

    /// <summary>The key a <see cref="GridViewColumn"/> passes to <see cref="IColumnSort.SortBy"/>.</summary>
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(object), typeof(GridViewSort));

    /// <summary>The sort direction of a <see cref="GridViewColumn"/>, or <see langword="null"/> when it is not sorted.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.RegisterAttached(
        "Direction", typeof(ListSortDirection?), typeof(GridViewSort));

    /// <summary>
    /// Set on an element inside a column header (for example a sort indicator) to mirror that
    /// column's <c>Direction</c> onto the element, so styles can use plain property triggers.
    /// </summary>
    public static readonly DependencyProperty ShowsColumnDirectionProperty = DependencyProperty.RegisterAttached(
        "ShowsColumnDirection", typeof(bool), typeof(GridViewSort), new PropertyMetadata(false, OnShowsColumnDirectionChanged));

    private static readonly DependencyProperty AttachmentProperty = DependencyProperty.RegisterAttached(
        "Attachment", typeof(Attachment), typeof(GridViewSort));

    private static readonly DependencyPropertyDescriptor ItemsSourceDescriptor =
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListView));

    public static IColumnSort? GetSort(DependencyObject element) => (IColumnSort?)element.GetValue(SortProperty);

    public static void SetSort(DependencyObject element, IColumnSort? value) => element.SetValue(SortProperty, value);

    public static object? GetKey(DependencyObject element) => element.GetValue(KeyProperty);

    public static void SetKey(DependencyObject element, object? value) => element.SetValue(KeyProperty, value);

    public static ListSortDirection? GetDirection(DependencyObject element) => (ListSortDirection?)element.GetValue(DirectionProperty);

    public static void SetDirection(DependencyObject element, ListSortDirection? value) => element.SetValue(DirectionProperty, value);

    public static bool GetShowsColumnDirection(DependencyObject element) => (bool)element.GetValue(ShowsColumnDirectionProperty);

    public static void SetShowsColumnDirection(DependencyObject element, bool value) => element.SetValue(ShowsColumnDirectionProperty, value);

    private static void OnShowsColumnDirectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            BindingOperations.ClearBinding(sender, DirectionProperty);
            return;
        }

        // Built in code: an attached-property path with a namespace prefix inside a shared
        // resource-dictionary style is not resolved by WPF and silently binds to nothing.
        BindingOperations.SetBinding(sender, DirectionProperty, new Binding
        {
            Path = new PropertyPath("Column.(0)", DirectionProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(GridViewColumnHeader), 1)
        });
    }

    private static void OnSortChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ListView list)
        {
            return;
        }

        Detach(list);
        list.RemoveHandler(GridViewColumnHeader.ClickEvent, (RoutedEventHandler)OnHeaderClick);
        list.Loaded -= OnLoaded;
        list.Unloaded -= OnUnloaded;
        if (e.NewValue is null)
        {
            return;
        }

        list.AddHandler(GridViewColumnHeader.ClickEvent, (RoutedEventHandler)OnHeaderClick);
        list.Loaded += OnLoaded;
        list.Unloaded += OnUnloaded;
        if (list.IsLoaded)
        {
            OnLoaded(list, new RoutedEventArgs());
        }
    }

    // Attach only while loaded so a long-lived sort model or source collection does not keep unloaded lists alive.
    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var list = (ListView)sender;
        Detach(list);
        if (GetSort(list) is { } sort)
        {
            list.SetValue(AttachmentProperty, new Attachment(list, sort));
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e) => Detach((ListView)sender);

    private static void Detach(ListView list)
    {
        if (list.GetValue(AttachmentProperty) is Attachment attachment)
        {
            attachment.Detach();
            list.ClearValue(AttachmentProperty);
        }
    }

    private static void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        // Buttons inside headers and rows raise the same bubbling Click event.
        if (e.OriginalSource is not GridViewColumnHeader { Column: { } column }
            || GetKey(column) is not { } key
            || GetSort((DependencyObject)sender) is not { } sort)
        {
            return;
        }

        sort.SortBy(key);
        e.Handled = true;
    }

    /// <summary>Connects one loaded list to its sort state and owns the list's collection view.</summary>
    private sealed class Attachment
    {
        private readonly ListView list;
        private readonly IColumnSort sort;
        private ListCollectionView? ownedView;

        public Attachment(ListView list, IColumnSort sort)
        {
            this.list = list;
            this.sort = sort;
            sort.PropertyChanged += OnSortPropertyChanged;
            ItemsSourceDescriptor.AddValueChanged(list, OnItemsSourceChanged);
            PresentSource();
            Apply();
        }

        public void Detach()
        {
            sort.PropertyChanged -= OnSortPropertyChanged;
            ItemsSourceDescriptor.RemoveValueChanged(list, OnItemsSourceChanged);
            if (ownedView is not null)
            {
                var view = ownedView;
                ownedView = null;
                if (ReferenceEquals(list.ItemsSource, view))
                {
                    // Return the bound source so a later load presents a fresh view of it.
                    list.SetCurrentValue(ItemsControl.ItemsSourceProperty, view.SourceCollection);
                }

                view.DetachFromSourceCollection();
            }
        }

        private void OnSortPropertyChanged(object? sender, PropertyChangedEventArgs e) => Apply();

        private void OnItemsSourceChanged(object? sender, EventArgs e)
        {
            if (ownedView is not null && ReferenceEquals(list.ItemsSource, ownedView))
            {
                return;
            }

            // The binding replaced the source: release the view of the previous source.
            ownedView?.DetachFromSourceCollection();
            ownedView = null;
            PresentSource();
            Apply();
        }

        private void PresentSource()
        {
            if (list.ItemsSource is IList source and not ICollectionView)
            {
                ownedView = new ListCollectionView(source);
                list.SetCurrentValue(ItemsControl.ItemsSourceProperty, ownedView);
            }
        }

        private void Apply()
        {
            if (list.ItemsSource is ListCollectionView view && !ReferenceEquals(view.CustomSort, sort.Comparer))
            {
                view.CustomSort = sort.Comparer;
            }

            if (list.View is GridView grid)
            {
                foreach (var column in grid.Columns)
                {
                    bool sorted = sort.Column is not null && Equals(GetKey(column), sort.Column);
                    SetDirection(column, sorted ? sort.Direction : null);
                }
            }
        }
    }
}
