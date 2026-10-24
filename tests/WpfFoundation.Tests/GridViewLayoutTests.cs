using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class GridViewLayoutTests
{
    [TestMethod]
    public async Task WidthAndOrderSurviveARecreatedViewWithoutPersistingFillWidths()
    {
        await UiThread.RunAsync(async () =>
        {
            var list = CreateList();
            var window = UiThread.ShowWindow(list);
            await UiThread.IdleAsync();
            var grid = (GridView)list.View;
            grid.Columns[0].Width = 220;
            grid.Columns.Move(1, 0);
            await UiThread.IdleAsync();
            var saved = GridViewLayout.GetState(list)!;
            CollectionAssert.AreEqual(new[] { "Result", "Name", "Actions" }, saved.Select(column => column.Id).ToArray());
            Assert.AreEqual(220, saved[1].Width);
            Assert.IsNull(saved[2].Width);
            window.Width += 100;
            await UiThread.IdleAsync();
            Assert.AreSame(saved, GridViewLayout.GetState(list), "Window resizing must not persist computed fill widths.");
            window.Close();

            var reopened = CreateList();
            GridViewLayout.SetState(reopened, saved);
            window = UiThread.ShowWindow(reopened);
            try
            {
                await UiThread.IdleAsync();
                var restored = (GridView)reopened.View;
                CollectionAssert.AreEqual(new[] { "Result", "Name", "Actions" }, restored.Columns.Select(GridViewLayout.GetId).ToArray());
                Assert.AreEqual(220, restored.Columns[1].Width);
                restored.Columns.Move(2, 0);
                await UiThread.IdleAsync();
                double width = restored.Columns[0].Width;
                window.Width += 100;
                await UiThread.IdleAsync();
                Assert.IsTrue(restored.Columns[0].Width > width, "The explicitly marked fill column keeps filling after reordering.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task InvalidAndRemovedColumnsAreIgnoredAndResetSurvivesUnload()
    {
        await UiThread.RunAsync(async () =>
        {
            var list = CreateList();
            GridViewLayout.SetState(list, new GridViewColumnLayout[]
            {
                new("Removed", 200), new("Result", double.PositiveInfinity), new("Result", 999), new("Name", -5)
            });
            var host = new ContentControl { Content = list };
            var window = UiThread.ShowWindow(host);
            try
            {
                await UiThread.IdleAsync();
                var grid = (GridView)list.View;
                Assert.AreEqual("Result", GridViewLayout.GetId(grid.Columns[0]));
                Assert.AreEqual(100, grid.Columns[0].Width);
                Assert.AreEqual(120, grid.Columns[1].Width);
                grid.Columns[1].Width = 240;
                var saved = GridViewLayout.GetState(list);
                host.Content = null;
                await UiThread.IdleAsync();
                grid.Columns[1].Width = 280;
                Assert.AreSame(saved, GridViewLayout.GetState(list), "Unloaded views must release width subscriptions.");
                host.Content = list;
                await UiThread.IdleAsync();
                GridViewLayout.SetState(list, null);
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "Name", "Result", "Actions" }, grid.Columns.Select(GridViewLayout.GetId).ToArray());
                Assert.AreEqual(120, grid.Columns[0].Width, "Reset restores the original XAML defaults, including after a reload.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ColumnMinimumWidthHoldsForResizedRestoredAndFillColumnsUntilUnloaded()
    {
        await UiThread.RunAsync(async () =>
        {
            var list = CreateList();
            GridViewSizing.SetColumnMinimumWidth(list, 110);
            GridViewLayout.SetState(list, new GridViewColumnLayout[] { new("Name", 50), new("Result", null) });
            var host = new ContentControl { Content = list };
            var window = UiThread.ShowWindow(host, width: 300);
            try
            {
                await UiThread.IdleAsync();
                var grid = (GridView)list.View;
                Assert.AreEqual(110, grid.Columns[0].Width, "A saved width below the minimum is raised.");
                Assert.IsTrue(double.IsNaN(grid.Columns[1].Width), "Automatic widths are left alone.");
                Assert.IsTrue(grid.Columns[2].Width >= 110, "The fill column respects the larger of the two minimums.");
                grid.Columns[0].Width = 40;
                Assert.AreEqual(110, grid.Columns[0].Width, "Resizing below the minimum stops at the minimum.");
                Assert.AreEqual(110, GridViewLayout.GetState(list)![0].Width, "The layout captures the raised width.");
                grid.Columns[0].Width = 160;
                Assert.AreEqual(160, grid.Columns[0].Width);

                host.Content = null;
                await UiThread.IdleAsync();
                grid.Columns[0].Width = 40;
                Assert.AreEqual(40, grid.Columns[0].Width, "Unloaded views must release width subscriptions.");
                host.Content = list;
                await UiThread.IdleAsync();
                grid.Columns[0].Width = 40;
                Assert.AreEqual(110, grid.Columns[0].Width, "Reloading applies the minimum again.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task WidthPercentSizesColumnsWithoutAnExplicitWidthAndAgainAfterReset()
    {
        await UiThread.RunAsync(async () =>
        {
            var list = CreateList();
            var grid = (GridView)list.View;
            var name = grid.Columns[0];
            name.ClearValue(GridViewColumn.WidthProperty);
            GridViewSizing.SetWidthPercent(name, 50);
            GridViewSizing.SetWidthPercent(grid.Columns[1], 25);
            var saved = Array.AsReadOnly(new GridViewColumnLayout[] { new("Result", 140) });
            GridViewLayout.SetState(list, saved);
            var window = UiThread.ShowWindow(list);
            try
            {
                await UiThread.IdleAsync();
                double half = Math.Round(FindScrollViewer(list)!.ViewportWidth / 2);
                Assert.AreEqual(half, name.Width, 1, "A column without a saved width takes its share of the list.");
                Assert.AreEqual(140, grid.Columns[0].Width, "A saved width wins over the percentage.");
                Assert.AreSame(saved, GridViewLayout.GetState(list), "Percentage widths are not saved until the user resizes.");

                name.Width = 200;
                Assert.AreEqual(200, GridViewLayout.GetState(list)!.Single(column => column.Id == "Name").Width);

                GridViewLayout.SetState(list, null);
                await UiThread.IdleAsync();
                Assert.AreEqual(half, name.Width, 1, "Reset gives the column its share again.");
                Assert.AreEqual(100, grid.Columns[1].Width, "An explicit XAML width wins over the percentage.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as ScrollViewer ?? FindScrollViewer(child)) is { } scroll)
            {
                return scroll;
            }
        }
        return null;
    }

    [TestMethod]
    public void RestoredSortSupportsDescendingAndResetWithoutChangingTheSource()
    {
        var sort = new ColumnSort<string>((_, direction) => Comparer<string>.Create((a, b) =>
            direction == ListSortDirection.Ascending ? StringComparer.Ordinal.Compare(a, b) : StringComparer.Ordinal.Compare(b, a)));
        var source = new[] { "b", "a", "c" };
        sort.Restore("Name", ListSortDirection.Descending);
        CollectionAssert.AreEqual(new[] { "c", "b", "a" }, sort.Order(source));
        sort.Restore(null);
        CollectionAssert.AreEqual(source, sort.Order(source));
        var guarded = new ColumnSort<string>((_, _) => StringComparer.Ordinal, () => false);
        guarded.Restore("Name");
        Assert.IsNull(guarded.Column);
    }

    private static ListView CreateList()
    {
        var grid = new GridView();
        foreach (var (id, width) in new[] { ("Name", 120d), ("Result", 100d), ("Actions", 80d) })
        {
            var column = new GridViewColumn { Header = id, Width = width };
            GridViewLayout.SetId(column, id);
            GridViewSizing.SetIsFillColumn(column, id == "Actions");
            grid.Columns.Add(column);
        }
        var list = new ListView { View = grid, ItemsSource = new ObservableCollection<string> { "Example" } };
        GridViewSizing.SetLastColumnMinimumWidth(list, 80);
        GridViewLayout.SetIsEnabled(list, true);
        return list;
    }
}
