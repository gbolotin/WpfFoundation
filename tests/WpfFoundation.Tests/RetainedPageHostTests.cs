using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using WpfFoundation.Navigation;
using static WpfFoundation.Tests.NavigationServiceTests;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class RetainedPageHostTests
{
    [TestMethod]
    public async Task VisitedPagesKeepTheirViewsWhileHidden()
    {
        await UiThread.RunAsync(async () =>
        {
            var trails = new TestPage("Trails");
            var notes = new TestPage("Notes");
            var navigation = new NavigationService([trails, notes]);
            var host = CreateHost(navigation);
            var window = UiThread.ShowWindow(host);
            try
            {
                await navigation.NavigateAsync(trails);
                await UiThread.IdleAsync();
                Assert.HasCount(1, host.RetainedPages, "Views are created on first visit.");
                var trailsBox = VisibleTextBox(host);
                trailsBox.Text = "Granite Ridge draft";

                await navigation.NavigateAsync(notes);
                await UiThread.IdleAsync();
                Assert.HasCount(2, host.RetainedPages);
                Assert.IsFalse(trailsBox.IsVisible, "Only the current page is shown.");
                Assert.AreNotSame(trailsBox, VisibleTextBox(host));

                await navigation.NavigateAsync(trails);
                await UiThread.IdleAsync();
                Assert.AreSame(trailsBox, VisibleTextBox(host), "Returning shows the same view.");
                Assert.AreEqual("Granite Ridge draft", trailsBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task AnUnloadedHostStopsFollowingAndANewServiceReleasesTheViews()
    {
        await UiThread.RunAsync(async () =>
        {
            var trails = new TestPage("Trails");
            var notes = new TestPage("Notes");
            var navigation = new NavigationService([trails, notes]);
            var host = CreateHost(navigation);
            var container = new Border { Child = host };
            var window = UiThread.ShowWindow(container);
            try
            {
                await navigation.NavigateAsync(trails);
                await UiThread.IdleAsync();
                container.Child = null;
                await UiThread.IdleAsync();

                await navigation.NavigateAsync(notes);
                Assert.HasCount(1, host.RetainedPages, "An unloaded host does not follow navigation.");

                container.Child = host;
                await UiThread.IdleAsync();
                Assert.HasCount(2, host.RetainedPages, "Loading again shows the current page.");

                host.Navigation = new NavigationService([new TestPage("Maps")]);
                Assert.IsEmpty(host.RetainedPages, "Views of the previous service are released.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ARejectedSidebarSelectionReturnsToTheCurrentPage()
    {
        await UiThread.RunAsync(async () =>
        {
            var trails = new TestPage("Trails");
            var notes = new TestPage("Notes");
            var guard = new Guard { Allow = false };
            var navigation = new NavigationService([trails, notes], guard);
            await navigation.NavigateAsync(trails);
            var sidebar = new ListBox
            {
                DataContext = navigation,
                Style = (Style)Application.Current.FindResource("WfNavigationSidebarStyle")
            };
            sidebar.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(INavigationService.Pages)));
            sidebar.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding(nameof(INavigationService.CurrentPage)));
            var window = UiThread.ShowWindow(sidebar);
            try
            {
                await UiThread.IdleAsync();
                Assert.AreSame(trails, sidebar.SelectedItem);

                sidebar.SelectedItem = notes;
                await UiThread.IdleAsync();
                Assert.AreSame(trails, navigation.CurrentPage);
                Assert.AreSame(trails, sidebar.SelectedItem, "The sidebar returns to the current page.");

                guard.Allow = true;
                sidebar.SelectedItem = notes;
                await UiThread.IdleAsync();
                Assert.AreSame(notes, navigation.CurrentPage);
                Assert.IsTrue(Visuals.Descendants<TextBlock>(sidebar).Any(text => text.Text == "Notes"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static RetainedPageHost CreateHost(INavigationService navigation)
    {
        var host = new RetainedPageHost { Navigation = navigation };
        // Every page shows an editable box, so retained state is visible.
        host.Resources.Add(new DataTemplateKey(typeof(TestPage)), (DataTemplate)XamlReader.Parse(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding NavigationName, Mode=OneTime}" />
            </DataTemplate>
            """));
        return host;
    }

    private static TextBox VisibleTextBox(DependencyObject root) => Visuals.Descendants<TextBox>(root).Single(box => box.IsVisible);
}
