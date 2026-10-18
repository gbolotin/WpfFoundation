using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
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
