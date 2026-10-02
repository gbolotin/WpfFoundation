using System.ComponentModel;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ColumnSortTests
{
    internal static ColumnSort<string> CreateTextSort(Func<bool>? canSort = null) => new((column, direction) => Comparer<string>.Create((x, y) =>
    {
        int result = (string)column == "Length" ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
        return direction == ListSortDirection.Descending ? -result : result;
    }), canSort);

    [TestMethod]
    public void SortingTheSameColumnTogglesDirectionAndAnotherColumnStartsAscending()
    {
        var sort = CreateTextSort();
        string[] items = ["ccc", "a", "bb"];

        CollectionAssert.AreEqual(items, sort.Order(items), "An unsorted list keeps its order.");
        sort.SortBy("Length");
        CollectionAssert.AreEqual(new[] { "a", "bb", "ccc" }, sort.Order(items));
        sort.SortBy("Length");
        Assert.AreEqual(ListSortDirection.Descending, sort.Direction);
        CollectionAssert.AreEqual(new[] { "ccc", "bb", "a" }, sort.Order(items));
        sort.SortBy("Text");
        Assert.AreEqual(ListSortDirection.Ascending, sort.Direction);
        CollectionAssert.AreEqual(new[] { "a", "bb", "ccc" }, sort.Order(items));
        CollectionAssert.AreEqual(new[] { "ccc", "a", "bb" }, items, "Ordering never changes the source.");
    }

    [TestMethod]
    public void RefreshPublishesANewComparerOnlyWhileSorted()
    {
        var sort = CreateTextSort();
        var changes = new List<string?>();
        sort.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        sort.Refresh();
        Assert.IsEmpty(changes);
        Assert.IsNull(((IColumnSort)sort).Comparer);

        sort.SortBy("Text");
        var comparer = sort.Comparer;
        changes.Clear();
        sort.Refresh();

        Assert.AreNotSame(comparer, sort.Comparer, "Views re-sort when they receive a new comparer.");
        CollectionAssert.AreEqual(new[] { nameof(ColumnSort<string>.Comparer) }, changes);
        Assert.AreSame<object>(sort.Comparer, ((IColumnSort)sort).Comparer);
    }

    [TestMethod]
    public void SortingIsIgnoredWhileTheGuardRejectsIt()
    {
        bool allowed = false;
        var sort = CreateTextSort(() => allowed);

        sort.SortBy("Text");
        Assert.IsNull(sort.Column);

        allowed = true;
        sort.SortBy("Text");
        Assert.AreEqual("Text", sort.Column);
    }
}
