using System.Collections.ObjectModel;
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
        WeakReference list = null!;
        await UiThread.RunAsync(async () =>
        {
            list = await ShowAndCloseListAsync(source, sort);
        });

        for (int attempt = 0; attempt < 10 && list.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await UiThread.RunAsync(UiThread.IdleAsync);
        }

        Assert.IsFalse(list.IsAlive, "Neither the sort state nor the source collection may keep an unloaded list alive.");
        sort.SortBy("Text");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ShowAndCloseListAsync(ObservableCollection<string> source, IColumnSort sort)
    {
        var list = CreateList(source, sort);
        var window = UiThread.ShowWindow(list);
        await UiThread.IdleAsync();
        sort.SortBy("Text");
        await UiThread.IdleAsync();
        CollectionAssert.AreEqual(new[] { "a", "b" }, Items(list));
        window.Close();
        await UiThread.IdleAsync();
        return new WeakReference(list);
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
