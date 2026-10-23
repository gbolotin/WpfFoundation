using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WpfFoundation.Behaviors;

/// <summary>
/// Sizes the last <see cref="GridViewColumn"/> of a <see cref="ListView"/> to fill the remaining width,
/// but not below <c>LastColumnMinimumWidth</c>, and keeps explicitly sized columns at least <c>ColumnMinimumWidth</c> wide.
/// </summary>
public static class GridViewSizing
{
    /// <summary>The narrowest explicit width of every column, including while the user drags a header gripper. NaN applies no minimum.</summary>
    public static readonly DependencyProperty ColumnMinimumWidthProperty = DependencyProperty.RegisterAttached(
        "ColumnMinimumWidth", typeof(double), typeof(GridViewSizing),
        new PropertyMetadata(double.NaN, OnColumnMinimumWidthChanged),
        value => value is double width && (double.IsNaN(width) || double.IsFinite(width) && width >= 0));

    private static readonly DependencyProperty ColumnMinimumProperty = DependencyProperty.RegisterAttached(
        "ColumnMinimum", typeof(ColumnMinimum), typeof(GridViewSizing));
    private static readonly DependencyPropertyDescriptor WidthDescriptor =
        DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));

    public static double GetColumnMinimumWidth(DependencyObject element) => (double)element.GetValue(ColumnMinimumWidthProperty);
    public static void SetColumnMinimumWidth(DependencyObject element, double value) => element.SetValue(ColumnMinimumWidthProperty, value);

    /// <summary>Marks the fill column explicitly, so reordering columns does not change which column fills the list.</summary>
    public static readonly DependencyProperty IsFillColumnProperty = DependencyProperty.RegisterAttached(
        "IsFillColumn", typeof(bool), typeof(GridViewSizing), new PropertyMetadata(false));

    public static bool GetIsFillColumn(DependencyObject element) => (bool)element.GetValue(IsFillColumnProperty);
    public static void SetIsFillColumn(DependencyObject element, bool value) => element.SetValue(IsFillColumnProperty, value);
    public static readonly DependencyProperty LastColumnMinimumWidthProperty = DependencyProperty.RegisterAttached(
        "LastColumnMinimumWidth", typeof(double), typeof(GridViewSizing),
        new PropertyMetadata(double.NaN, OnMinimumWidthChanged),
        value => value is double width && (double.IsNaN(width) || double.IsFinite(width) && width >= 0));

    private static readonly DependencyProperty LayoutHandlerProperty = DependencyProperty.RegisterAttached(
        "LayoutHandler", typeof(EventHandler), typeof(GridViewSizing));

    public static double GetLastColumnMinimumWidth(DependencyObject element) => (double)element.GetValue(LastColumnMinimumWidthProperty);
    public static void SetLastColumnMinimumWidth(DependencyObject element, double value) => element.SetValue(LastColumnMinimumWidthProperty, value);

    private static void OnMinimumWidthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ListView list)
        {
            return;
        }

        list.Loaded -= OnLoaded;
        list.Unloaded -= OnUnloaded;
        OnUnloaded(list, new RoutedEventArgs());
        list.ClearValue(LayoutHandlerProperty);
        if (double.IsNaN((double)e.NewValue))
        {
            return;
        }

        // LayoutUpdated runs after every layout pass, which also catches auto-sized and reordered columns.
        // It suits a few lists per window, and the column width is only set when it changes.
        EventHandler handler = (_, _) => ResizeLastColumn(list);
        list.SetValue(LayoutHandlerProperty, handler);
        list.Loaded += OnLoaded;
        list.Unloaded += OnUnloaded;
        if (list.IsLoaded)
        {
            OnLoaded(list, new RoutedEventArgs());
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var list = (ListView)sender;
        var handler = (EventHandler)list.GetValue(LayoutHandlerProperty);
        list.LayoutUpdated -= handler;
        list.LayoutUpdated += handler;
        ResizeLastColumn(list);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var list = (ListView)sender;
        list.LayoutUpdated -= (EventHandler?)list.GetValue(LayoutHandlerProperty);
    }

    private static void ResizeLastColumn(ListView list)
    {
        if (!list.IsVisible || list.View is not GridView { Columns.Count: > 0 } grid
            || FindChild<ScrollViewer>(list) is not { ViewportWidth: > 0 } scroll)
        {
            return;
        }

        double available = scroll.ViewportWidth;
        if (scroll.Template.FindName("PART_ScrollContentPresenter", scroll) is ScrollContentPresenter viewport
            && scroll.Template.FindName("PART_VerticalScrollBar", scroll) is ScrollBar { IsVisible: true } bar)
        {
            // Fluent overlays its scrollbar; other themes already exclude it from the viewport.
            available = Math.Min(available, bar.TranslatePoint(new Point(), viewport).X);
        }

        double totalWidth = grid.Columns.Sum(column => column.ActualWidth);
        // GridView reserves two pixels for its trailing padding header.
        double spacing = 2;
        if (FindChild<ListViewItem>(list) is { } row)
        {
            spacing = Math.Max(spacing, row.DesiredSize.Width - totalWidth);
        }

        var lastColumn = grid.Columns.FirstOrDefault(GetIsFillColumn) ?? grid.Columns[^1];
        // Respect ColumnMinimumWidth too, so the two minimums never resize the fill column back and forth.
        double columnMinimum = GetColumnMinimumWidth(list);
        double minimum = Math.Max(GetLastColumnMinimumWidth(list), double.IsNaN(columnMinimum) ? 0 : columnMinimum);
        double width = Math.Max(minimum, available - spacing - (totalWidth - lastColumn.ActualWidth));
        if (double.IsNaN(lastColumn.Width) || Math.Abs(lastColumn.Width - width) > 0.1)
        {
            lastColumn.SetCurrentValue(GridViewColumn.WidthProperty, width);
        }
    }

    private static void OnColumnMinimumWidthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ListView list)
        {
            return;
        }

        DetachColumnMinimum(list);
        list.Loaded -= OnColumnMinimumLoaded;
        list.Unloaded -= OnColumnMinimumUnloaded;
        if (!double.IsNaN((double)e.NewValue))
        {
            list.Loaded += OnColumnMinimumLoaded;
            list.Unloaded += OnColumnMinimumUnloaded;
            if (list.IsLoaded)
            {
                OnColumnMinimumLoaded(list, new RoutedEventArgs());
            }
        }
    }

    private static void OnColumnMinimumLoaded(object sender, RoutedEventArgs e)
    {
        var list = (ListView)sender;
        DetachColumnMinimum(list);
        if (list.View is GridView grid)
        {
            list.SetValue(ColumnMinimumProperty, new ColumnMinimum(list, grid));
        }
    }

    private static void OnColumnMinimumUnloaded(object sender, RoutedEventArgs e) => DetachColumnMinimum((ListView)sender);

    private static void DetachColumnMinimum(ListView list)
    {
        if (list.GetValue(ColumnMinimumProperty) is ColumnMinimum attachment)
        {
            attachment.Detach();
            list.ClearValue(ColumnMinimumProperty);
        }
    }

    private sealed class ColumnMinimum
    {
        private readonly ListView list;
        private readonly GridView grid;
        private readonly List<GridViewColumn> columns = [];

        public ColumnMinimum(ListView list, GridView grid)
        {
            this.list = list;
            this.grid = grid;
            SubscribeColumns();
            ((INotifyCollectionChanged)grid.Columns).CollectionChanged += OnColumnsChanged;
        }

        public void Detach()
        {
            ((INotifyCollectionChanged)grid.Columns).CollectionChanged -= OnColumnsChanged;
            UnsubscribeColumns();
        }

        private void SubscribeColumns()
        {
            UnsubscribeColumns();
            columns.AddRange(grid.Columns);
            foreach (var column in columns)
            {
                WidthDescriptor.AddValueChanged(column, OnWidthChanged);
                Clamp(column);
            }
        }

        private void UnsubscribeColumns()
        {
            foreach (var column in columns)
            {
                WidthDescriptor.RemoveValueChanged(column, OnWidthChanged);
            }
            columns.Clear();
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SubscribeColumns();

        private void OnWidthChanged(object? sender, EventArgs e)
        {
            if (sender is GridViewColumn column)
            {
                Clamp(column);
            }
        }

        // Automatic (NaN) widths keep their measured width; only explicit widths, which header dragging sets, are raised.
        private void Clamp(GridViewColumn column)
        {
            double minimum = GetColumnMinimumWidth(list);
            if (double.IsFinite(column.Width) && column.Width < minimum)
            {
                column.SetCurrentValue(GridViewColumn.WidthProperty, minimum);
            }
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }
            if (FindChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }
        return null;
    }
}
