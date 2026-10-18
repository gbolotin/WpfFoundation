using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace WpfFoundation.Behaviors;

/// <summary>A column's stable ID and explicit width. Null keeps its automatic or fill width.</summary>
public sealed record GridViewColumnLayout(string Id, double? Width);

/// <summary>Captures and restores column order and widths. The application owns storage of the bound layout.</summary>
public static class GridViewLayout
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(GridViewLayout), new PropertyMetadata(false, OnEnabledChanged));
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id", typeof(string), typeof(GridViewLayout));
    public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(IReadOnlyList<GridViewColumnLayout>), typeof(GridViewLayout),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));
    private static readonly DependencyProperty AttachmentProperty = DependencyProperty.RegisterAttached(
        "Attachment", typeof(Attachment), typeof(GridViewLayout));
    private static readonly DependencyProperty DefaultsProperty = DependencyProperty.RegisterAttached(
        "Defaults", typeof(DefaultLayout), typeof(GridViewLayout));
    private static readonly DependencyPropertyDescriptor WidthDescriptor =
        DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static string? GetId(DependencyObject element) => (string?)element.GetValue(IdProperty);
    public static void SetId(DependencyObject element, string? value) => element.SetValue(IdProperty, value);
    public static IReadOnlyList<GridViewColumnLayout>? GetState(DependencyObject element) => (IReadOnlyList<GridViewColumnLayout>?)element.GetValue(StateProperty);
    public static void SetState(DependencyObject element, IReadOnlyList<GridViewColumnLayout>? value) => element.SetValue(StateProperty, value);

    private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ListView list)
        {
            return;
        }

        Detach(list);
        list.Loaded -= OnLoaded;
        list.Unloaded -= OnUnloaded;
        if (e.NewValue is true)
        {
            list.Loaded += OnLoaded;
            list.Unloaded += OnUnloaded;
            if (list.IsLoaded)
            {
                OnLoaded(list, new RoutedEventArgs());
            }
        }
    }

    private static void OnStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender.GetValue(AttachmentProperty) is Attachment attachment)
        {
            attachment.Apply((IReadOnlyList<GridViewColumnLayout>?)e.NewValue);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var list = (ListView)sender;
        Detach(list);
        if (list.View is GridView grid)
        {
            list.SetValue(AttachmentProperty, new Attachment(list, grid));
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

    private sealed class DefaultLayout(GridView grid)
    {
        public List<GridViewColumn> Order { get; } = [.. grid.Columns];
        public Dictionary<GridViewColumn, double> Widths { get; } = grid.Columns.ToDictionary(column => column, column => column.Width);
    }

    private sealed class Attachment
    {
        private readonly ListView list;
        private readonly GridView grid;
        private readonly Dictionary<GridViewColumn, double> defaults;
        private readonly List<GridViewColumn> originalOrder;
        private bool updating;

        public Attachment(ListView list, GridView grid)
        {
            this.list = list;
            this.grid = grid;
            if (grid.GetValue(DefaultsProperty) is not DefaultLayout original)
            {
                original = new DefaultLayout(grid);
                grid.SetValue(DefaultsProperty, original);
            }
            originalOrder = original.Order;
            defaults = original.Widths;
            Apply(GetState(list));
            SubscribeColumns();
            ((INotifyCollectionChanged)grid.Columns).CollectionChanged += OnColumnsChanged;
        }

        public void Detach()
        {
            ((INotifyCollectionChanged)grid.Columns).CollectionChanged -= OnColumnsChanged;
            foreach (var column in defaults.Keys)
            {
                WidthDescriptor.RemoveValueChanged(column, OnWidthChanged);
            }
        }

        public void Apply(IReadOnlyList<GridViewColumnLayout>? state)
        {
            if (updating)
            {
                return;
            }

            updating = true;
            try
            {
                var available = grid.Columns.Where(column => !string.IsNullOrWhiteSpace(GetId(column)))
                    .GroupBy(column => GetId(column)!, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                var ordered = new List<GridViewColumn>();
                if (state is null)
                {
                    ordered.AddRange(originalOrder.Where(grid.Columns.Contains));
                    foreach (var column in ordered.Where(column => !GridViewSizing.GetIsFillColumn(column)))
                    {
                        column.SetCurrentValue(GridViewColumn.WidthProperty, defaults[column]);
                    }
                }
                else
                {
                    foreach (var saved in state)
                    {
                        if (saved is null || !available.TryGetValue(saved.Id ?? "", out var column) || ordered.Contains(column))
                        {
                            continue;
                        }

                        ordered.Add(column);
                        if (!GridViewSizing.GetIsFillColumn(column) && (saved.Width is null || saved.Width is >= 0 and <= 10000))
                        {
                            column.SetCurrentValue(GridViewColumn.WidthProperty, saved.Width ?? double.NaN);
                        }
                    }
                }

                ordered.AddRange(grid.Columns.Where(column => !ordered.Contains(column)));
                for (int index = 0; index < ordered.Count; index++)
                {
                    int current = grid.Columns.IndexOf(ordered[index]);
                    if (current != index)
                    {
                        grid.Columns.RemoveAt(current);
                        grid.Columns.Insert(index, ordered[index]);
                    }
                }
            }
            finally
            {
                updating = false;
            }
        }

        private void SubscribeColumns()
        {
            foreach (var column in defaults.Keys)
            {
                WidthDescriptor.RemoveValueChanged(column, OnWidthChanged);
            }
            foreach (var column in grid.Columns)
            {
                if (defaults.TryAdd(column, column.Width))
                {
                    originalOrder.Add(column);
                }
                WidthDescriptor.AddValueChanged(column, OnWidthChanged);
            }
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!updating)
            {
                SubscribeColumns();
                Capture();
            }
        }

        private void OnWidthChanged(object? sender, EventArgs e)
        {
            if (sender is GridViewColumn column && !GridViewSizing.GetIsFillColumn(column))
            {
                Capture();
            }
        }

        private void Capture()
        {
            if (updating)
            {
                return;
            }

            var state = grid.Columns.Where(column => !string.IsNullOrWhiteSpace(GetId(column)))
                .Select(column => new GridViewColumnLayout(GetId(column)!,
                    GridViewSizing.GetIsFillColumn(column) || !double.IsFinite(column.Width) ? null : column.Width)).ToArray();
            if (GetState(list) is { } previous && previous.SequenceEqual(state))
            {
                return;
            }

            updating = true;
            try
            {
                list.SetCurrentValue(StateProperty, Array.AsReadOnly(state));
            }
            finally
            {
                updating = false;
            }
        }
    }
}
