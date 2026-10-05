using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ListFilterTests
{
    [TestMethod]
    public async Task APredicateFiltersTheListWithoutChangingTheSourceOrOtherLists()
    {
        await UiThread.RunAsync(async () =>
        {
            var source = new ObservableCollection<Entry> { new("bb", "x"), new("a", "y"), new("ccc", "x") };
            var filtered = new ListBox { ItemsSource = source };
            var plain = new ListBox { ItemsSource = source };
            ListFilter.SetPredicate(filtered, InGroup("x"));
            var window = UiThread.ShowWindow(new StackPanel { Children = { filtered, plain } });
            try
            {
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "bb", "ccc" }, Names(filtered));
                CollectionAssert.AreEqual(new[] { "bb", "a", "ccc" }, Names(plain), "Another list of the same source shows every item.");

                ListFilter.SetPredicate(filtered, InGroup("y"));
                CollectionAssert.AreEqual(new[] { "a" }, Names(filtered), "A new predicate filters the list again.");

                source.Add(new("dddd", "y"));
                CollectionAssert.AreEqual(new[] { "a", "dddd" }, Names(filtered), "Added items are filtered.");

                ListFilter.SetPredicate(filtered, null);
                CollectionAssert.AreEqual(new[] { "bb", "a", "ccc", "dddd" }, Names(filtered), "No predicate shows every item.");
                CollectionAssert.AreEqual(new[] { "bb", "a", "ccc", "dddd" }, source.Select(entry => entry.Name).ToArray());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ALivePropertyChangeFiltersTheItemAgain()
    {
        await UiThread.RunAsync(async () =>
        {
            var moving = new Entry("a", "x");
            var source = new ObservableCollection<Entry> { moving, new("b", "y") };
            var list = new ListBox { ItemsSource = source };
            ListFilter.SetPredicate(list, InGroup("x"));
            ListFilter.SetLiveProperty(list, nameof(Entry.Group));
            var window = UiThread.ShowWindow(list);
            try
            {
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "a" }, Names(list));

                moving.Group = "y";
                await UiThread.IdleAsync();
                Assert.IsEmpty(list.Items, "An item leaves when its live property no longer matches.");

                source[1].Group = "x";
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "b" }, Names(list), "An item enters when its live property starts to match.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AFilteredListKeepsItsColumnSortAndItsSourceBinding(bool filterFirst)
    {
        await UiThread.RunAsync(async () =>
        {
            var holder = new SourceHolder { Items = ["ccc", "a", "dddd", "bb"] };
            var sort = ColumnSortTests.CreateTextSort();
            var text = new GridViewColumn { Header = "Text", DisplayMemberBinding = new Binding() };
            GridViewSort.SetKey(text, "Text");
            var list = new ListView
            {
                View = new GridView
                {
                    ColumnHeaderContainerStyle = (Style)Application.Current.FindResource("WfSortableColumnHeaderContainerStyle"),
                    Columns = { text }
                },
                DataContext = holder,
                Height = 160
            };
            list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(SourceHolder.Items)));
            Predicate<object> shortText = item => ((string)item).Length < 4;
            if (filterFirst)
            {
                ListFilter.SetPredicate(list, shortText);
                GridViewSort.SetSort(list, sort);
            }
            else
            {
                GridViewSort.SetSort(list, sort);
                ListFilter.SetPredicate(list, shortText);
            }

            var host = new Border { Child = list };
            var window = UiThread.ShowWindow(host);
            try
            {
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "ccc", "a", "bb" }, Strings(list));

                var header = Visuals.Descendants<GridViewColumnHeader>(list).Single(item => ReferenceEquals(item.Column, text));
                header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, header));
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "a", "bb", "ccc" }, Strings(list), "Sorting keeps the filter.");

                ListFilter.SetPredicate(list, item => ((string)item).Length > 1);
                CollectionAssert.AreEqual(new[] { "bb", "ccc", "dddd" }, Strings(list), "Filtering again keeps the sort.");

                holder.Items = ["zz", "y", "xxx"];
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "xxx", "zz" }, Strings(list), "A replaced source is sorted and filtered.");

                host.Child = null;
                await UiThread.IdleAsync();
                Assert.AreSame(holder.Items, list.ItemsSource, "Unloading returns the bound source.");
                Assert.IsNotNull(BindingOperations.GetBindingExpression(list, ItemsControl.ItemsSourceProperty), "The items source binding is kept.");

                host.Child = list;
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "xxx", "zz" }, Strings(list), "Loading again sorts and filters again.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task UnloadingAListDropsItsSubscriptionToTheSource()
    {
        await UiThread.RunAsync(async () =>
        {
            var source = new CountingCollection { "b", "a" };
            var list = new ListBox { ItemsSource = source };
            ListFilter.SetPredicate(list, item => (string)item == "a");
            // WPF's shared default view of the source listens to it for any list bound to it.
            int baseline = source.Subscribers;
            var host = new Border();
            var window = UiThread.ShowWindow(host);
            try
            {
                host.Child = list;
                await UiThread.IdleAsync();
                Assert.AreEqual(baseline + 1, source.Subscribers, "The loaded list's own view listens to the source.");
                CollectionAssert.AreEqual(new[] { "a" }, Strings(list));

                host.Child = null;
                await UiThread.IdleAsync();
                Assert.AreEqual(baseline, source.Subscribers, "An unloaded list's own view stops listening to the source.");
                Assert.AreSame(source, list.ItemsSource);

                host.Child = list;
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { "a" }, Strings(list), "Loading again filters again.");
                host.Child = null;
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Predicate<object> InGroup(string group) => item => ((Entry)item).Group == group;

    private static string[] Names(ItemsControl list) => [.. list.Items.Cast<Entry>().Select(entry => entry.Name)];

    private static string[] Strings(ItemsControl list) => [.. list.Items.Cast<string>()];

    private sealed class Entry(string name, string group) : ObservableObject
    {
        private string group = group;

        public string Name { get; } = name;

        public string Group
        {
            get => group;
            set => SetProperty(ref group, value);
        }
    }

    private sealed class SourceHolder : ObservableObject
    {
        private ObservableCollection<string> items = [];

        public ObservableCollection<string> Items
        {
            get => items;
            set => SetProperty(ref items, value);
        }
    }

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
}
