using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class GridViewSortTests
{
    [TestMethod]
    public async Task HeaderClicksSortTheViewWithoutChangingTheSourceOrOtherLists()
    {
        await UiThread.RunAsync(async () =>
        {
            var source = new ObservableCollection<string> { "bb", "ccc", "a" };
            var sort = ColumnSortTests.CreateTextSort();
            var sorted = CreateList(source, sort);
            var unsorted = CreateList(source, sort: null);
            var window = UiThread.ShowWindow(new StackPanel { Children = { sorted, unsorted } });
            try
            {
                await UiThread.IdleAsync();
                var header = Header(sorted, 0);
                var indicator = Visuals.Descendants<TextBlock>(header).Single(text => text.Style == Application.Current.FindResource("WfColumnSortIndicatorStyle"));
                Assert.AreEqual(Visibility.Collapsed, indicator.Visibility);

                header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, header));
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "a", "bb", "ccc" }, Items(sorted));
                Assert.AreEqual(ListSortDirection.Ascending, GridViewSort.GetDirection(Grid(sorted).Columns[0]));
                Assert.AreEqual(Visibility.Visible, indicator.Visibility);
                Assert.AreEqual("", indicator.Text);

                header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, header));
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "ccc", "bb", "a" }, Items(sorted));
                Assert.AreEqual("", indicator.Text);
                Assert.IsNull(GridViewSort.GetDirection(Grid(sorted).Columns[1]), "Only the sorted column has a direction.");

                CollectionAssert.AreEqual(new[] { "bb", "ccc", "a" }, source, "The source order is unchanged.");
                CollectionAssert.AreEqual(new[] { "bb", "ccc", "a" }, Items(unsorted), "Another list of the same source keeps its own order.");

                source.Add("dddd");
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "dddd", "ccc", "bb", "a" }, Items(sorted), "Added items take their sorted place.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task AReplacedSourceIsSortedWithTheCurrentComparer()
    {
        await UiThread.RunAsync(async () =>
        {
            var holder = new SourceHolder { Items = ["bb", "a"] };
            var sort = ColumnSortTests.CreateTextSort();
            var list = CreateList(null, sort);
            list.DataContext = holder;
            list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(SourceHolder.Items)));
            var window = UiThread.ShowWindow(list);
            try
            {
                sort.SortBy("Text");
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "a", "bb" }, Items(list));

                holder.Items = ["zz", "y", "xxx"];
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "xxx", "y", "zz" }, Items(list));
                Assert.IsNotNull(BindingOperations.GetBindingExpression(list, ItemsControl.ItemsSourceProperty), "The items source binding is kept.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task AnUnloadedListIsReleasedWhileTheSortStateLivesOn()
    {
        var sort = ColumnSortTests.CreateTextSort();
        var source = new ObservableCollection<string> { "b", "a" };
        (WeakReference sorted, WeakReference plain) = await UiThread.RunAsync(() => ShowAndCloseListsAsync(source, sort));

        for (int attempt = 0; attempt < 10 && (sorted.IsAlive || plain.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await UiThread.RunAsync(UiThread.IdleAsync);
        }

        if (plain.IsAlive)
        {
            // WPF itself sometimes keeps a closed window's elements alive for a while, for example after a modal dialog.
            Assert.Inconclusive("A list without the sort behavior was not released either, so this run cannot judge the behavior.");
        }

        Assert.IsFalse(sorted.IsAlive, "Neither the sort state nor the source collection may keep an unloaded list alive.");
        GC.KeepAlive(sort);
        GC.KeepAlive(source);
    }

    [TestMethod]
    public async Task UnloadingAListDropsItsSubscriptionsToTheSortAndTheSource()
    {
        await UiThread.RunAsync(async () =>
        {
            var source = new CountingCollection { "b", "a" };
            var sort = new CountingSort(ColumnSortTests.CreateTextSort());
            var host = new Border();
            var window = UiThread.ShowWindow(host);
            try
            {
                var list = CreateList(source, sort);
                // WPF's shared default view of the source listens to it for any list bound to it.
                int baseline = source.Subscribers;
                host.Child = list;
                await UiThread.IdleAsync();
                sort.SortBy("Text");
                await UiThread.IdleAsync();
                Assert.AreEqual(1, sort.Subscribers);
                Assert.AreEqual(baseline + 1, source.Subscribers, "The loaded list's own view listens to the source.");

                host.Child = null;
                await UiThread.IdleAsync();
                Assert.AreEqual(0, sort.Subscribers, "An unloaded list stops listening to the sort.");
                Assert.AreEqual(baseline, source.Subscribers, "An unloaded list's own view stops listening to the source.");

                host.Child = list;
                await UiThread.IdleAsync();
                Assert.AreEqual(1, sort.Subscribers, "Loading again reattaches.");
                CollectionAssert.AreEqual(new[] { "a", "b" }, Items(list), "The sort is applied again.");
                host.Child = null;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Sorted, WeakReference Plain)> ShowAndCloseListsAsync(ObservableCollection<string> source, IColumnSort sort)
    {
        var sorted = CreateList(source, sort);
        var plain = CreateList(source, sort: null);
        var window = UiThread.ShowWindow(new StackPanel { Children = { sorted, plain } });
        await UiThread.IdleAsync();
        sort.SortBy("Text");
        await UiThread.IdleAsync();
        CollectionAssert.AreEqual(new[] { "a", "b" }, Items(sorted));
        window.Close();
        await UiThread.IdleAsync();
        return (new WeakReference(sorted), new WeakReference(plain));
    }

    private static ListView CreateList(IEnumerable<string>? source, IColumnSort? sort)
    {
        var grid = new GridView
        {
            ColumnHeaderContainerStyle = (Style)Application.Current.FindResource("WfSortableColumnHeaderContainerStyle"),
            ColumnHeaderTemplate = (DataTemplate)Application.Current.FindResource("WfSortableColumnHeaderTemplate")
        };
        var text = new GridViewColumn { Header = "Text", DisplayMemberBinding = new Binding() };
        GridViewSort.SetKey(text, "Text");
        var length = new GridViewColumn { Header = "Length", DisplayMemberBinding = new Binding(nameof(string.Length)) };
        GridViewSort.SetKey(length, "Length");
        grid.Columns.Add(text);
        grid.Columns.Add(length);
        var list = new ListView { View = grid, ItemsSource = source, Height = 160 };
        GridViewSort.SetSort(list, sort);
        return list;
    }

    private static GridView Grid(ListView list) => (GridView)list.View;

    private static GridViewColumnHeader Header(ListView list, int column) =>
        Visuals.Descendants<GridViewColumnHeader>(list).Single(header => ReferenceEquals(header.Column, Grid(list).Columns[column]));

    private static string[] Items(ListView list) => [.. list.Items.Cast<string>()];

    private sealed class CountingCollection : ObservableCollection<string>
    {
        private NotifyCollectionChangedEventHandler? handlers;

        public int Subscribers => handlers?.GetInvocationList().Length ?? 0;

        public override event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => handlers += value;
            remove => handlers -= value;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => handlers?.Invoke(this, e);
    }

    private sealed class CountingSort(IColumnSort inner) : IColumnSort
    {
        private PropertyChangedEventHandler? handlers;

        public int Subscribers => handlers?.GetInvocationList().Length ?? 0;

        public object? Column => inner.Column;

        public ListSortDirection Direction => inner.Direction;

        public System.Collections.IComparer? Comparer => inner.Comparer;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                handlers += value;
                inner.PropertyChanged += Forward;
            }
            remove
            {
                handlers -= value;
                inner.PropertyChanged -= Forward;
            }
        }

        public void SortBy(object column) => inner.SortBy(column);

        private void Forward(object? sender, PropertyChangedEventArgs e) => handlers?.Invoke(this, e);
    }

    private sealed class SourceHolder : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private ObservableCollection<string> items = [];

        public ObservableCollection<string> Items
        {
            get => items;
            set => SetProperty(ref items, value);
        }
    }
}
