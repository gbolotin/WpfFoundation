using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using WpfFoundation.Documents;
using WpfFoundation.Navigation;
using static WpfFoundation.Tests.DocumentWorkspaceTests;
using static WpfFoundation.Tests.NavigationServiceTests;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class NavigationTabsTests
{
    [TestMethod]
    public async Task OpenDocumentsKeepTheirViewsAndClosedOnesReleaseThem()
    {
        await UiThread.RunAsync(async () =>
        {
            var workspace = new DocumentWorkspace();
            var tabs = CreateTabs(workspace);
            var window = UiThread.ShowWindow(tabs);
            try
            {
                var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
                await UiThread.IdleAsync();
                var host = Visuals.Descendants<RetainedPageHost>(tabs).Single();
                var ridgeBox = VisibleTextBox(host);
                ridgeBox.Text = "Granite Ridge draft";

                var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
                await UiThread.IdleAsync();
                Assert.HasCount(2, host.RetainedPages);
                Assert.IsFalse(ridgeBox.IsVisible, "Only the active document is shown.");

                tabs.SelectedItem = ridge;
                await UiThread.IdleAsync();
                Assert.AreSame(ridge, workspace.ActiveDocument, "Selecting a tab activates its document.");
                Assert.AreSame(ridgeBox, VisibleTextBox(host), "Returning shows the same view.");
                Assert.AreEqual("Granite Ridge draft", ridgeBox.Text);

                await workspace.CloseAsync(ridge);
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new INavigationPage[] { falls }, host.RetainedPages.ToArray(), "A closed document's view is released.");
                CollectionAssert.DoesNotContain(Visuals.Descendants<TextBox>(host).ToArray(), ridgeBox);
                Assert.AreSame(falls, tabs.SelectedItem);

                NavigationTabs.SetNavigation(tabs, new DocumentWorkspace());
                await UiThread.IdleAsync();
                Assert.IsEmpty(host.RetainedPages, "Views of the previous workspace are released.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task DocumentTabsAreNamedByTitleAndOnlyClosableOnesHaveACloseButton()
    {
        await UiThread.RunAsync(async () =>
        {
            var workspace = new DocumentWorkspace();
            var tabs = CreateTabs(workspace);
            var window = UiThread.ShowWindow(tabs);
            try
            {
                workspace.Open(new DocumentItem("trails", "Trails", new TrailViewModel("Trails")) { IsClosable = false });
                var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
                var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
                await UiThread.IdleAsync();

                CollectionAssert.AreEqual(
                    new[] { "Trails", "Granite Ridge", "Cedar Falls" },
                    TabItems(tabs).Select(AutomationProperties.GetName).ToArray(),
                    "Screen readers announce the document titles.");
                var closeButtons = CloseButtons(tabs);
                CollectionAssert.AreEqual(
                    new[] { "Close Granite Ridge", "Close Cedar Falls" },
                    closeButtons.Select(AutomationProperties.GetName).ToArray(),
                    "Only closable documents show a close button.");

                ridge.Title = "Granite Ridge (north)";
                await UiThread.IdleAsync();
                Assert.AreEqual("Granite Ridge (north)", AutomationProperties.GetName(TabItems(tabs)[1]));

                closeButtons[1].Command.Execute(null);
                await UiThread.IdleAsync();
                CollectionAssert.DoesNotContain(workspace.Documents.ToArray(), falls);
                Assert.AreSame(ridge, tabs.SelectedItem, "The left neighbor is selected.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task FixedPagesShowAsTabsWithoutCloseButtons()
    {
        await UiThread.RunAsync(async () =>
        {
            var general = new TestPage("General");
            var alerts = new TestPage("Alerts");
            var guard = new Guard { Allow = false };
            var navigation = new NavigationService([general, alerts], guard, initialPage: general);
            var tabs = CreateTabs(navigation);
            var window = UiThread.ShowWindow(tabs);
            try
            {
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(
                    new[] { "General", "Alerts" },
                    TabItems(tabs).Select(AutomationProperties.GetName).ToArray());
                Assert.IsEmpty(CloseButtons(tabs));

                tabs.SelectedItem = alerts;
                await UiThread.IdleAsync();
                Assert.AreSame(general, navigation.CurrentPage);
                Assert.AreSame(general, tabs.SelectedItem, "A refused navigation returns the tabs to the current page.");

                guard.Allow = true;
                tabs.SelectedItem = alerts;
                await UiThread.IdleAsync();
                Assert.AreSame(alerts, navigation.CurrentPage);
                Assert.AreEqual("Alerts", VisibleTextBox(tabs).Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ScreenReadersReachThePageInTheSelectedTab()
    {
        await UiThread.RunAsync(async () =>
        {
            var workspace = new DocumentWorkspace();
            workspace.Open(Trail("granite-ridge", "Granite Ridge"));
            var tabs = CreateTabs(workspace);
            var border = new Border();
            var window = UiThread.ShowWindow(border);
            try
            {
                await UiThread.IdleAsync();
                border.Child = tabs;
                tabs.UpdateLayout();
                // A screen reader that follows selection reads the tab's children before the page's view is built.
                _ = Peers(UIElementAutomationPeer.CreatePeerForElement(tabs)).ToArray();
                await UiThread.IdleAsync();
                Assert.AreEqual("Granite Ridge", PageText(tabs), "The selected tab exposes its page.");

                workspace.Open(Trail("cedar-falls", "Cedar Falls"));
                await UiThread.IdleAsync();
                Assert.AreEqual("Cedar Falls", PageText(tabs), "A newly opened tab exposes its page, and only it.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task SidebarItemsAreAnnouncedByPageName()
    {
        await UiThread.RunAsync(async () =>
        {
            var navigation = new NavigationService([new TestPage("Trails"), new TestPage("Notes")]);
            var sidebar = new ListBox
            {
                DataContext = navigation,
                Style = (Style)Application.Current.FindResource("WfNavigationSidebarStyle")
            };
            sidebar.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(INavigationService.Pages)));
            var window = UiThread.ShowWindow(sidebar);
            try
            {
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(
                    new[] { "Trails", "Notes" },
                    Visuals.Descendants<ListBoxItem>(sidebar).Select(AutomationProperties.GetName).ToArray());
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static TabControl CreateTabs(INavigationService navigation)
    {
        var tabs = new TabControl { Style = (Style)Application.Current.FindResource("WfNavigationTabControlStyle") };
        NavigationTabs.SetNavigation(tabs, navigation);
        // Every page shows an editable box, so retained state is visible.
        tabs.Resources.Add(new DataTemplateKey(typeof(TrailViewModel)), (DataTemplate)XamlReader.Parse(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding Name, Mode=OneTime}" />
            </DataTemplate>
            """));
        tabs.Resources.Add(new DataTemplateKey(typeof(TestPage)), (DataTemplate)XamlReader.Parse(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding NavigationName, Mode=OneTime}" />
            </DataTemplate>
            """));
        return tabs;
    }

    private static IEnumerable<AutomationPeer> Peers(AutomationPeer peer) =>
        (peer.GetChildren() ?? []).SelectMany(child => Peers(child).Prepend(child));

    // The text of the single page text box that UI Automation reaches through the tabs.
    private static string PageText(TabControl tabs)
    {
        var box = Peers(UIElementAutomationPeer.CreatePeerForElement(tabs)).OfType<TextBoxAutomationPeer>().Single();
        return ((System.Windows.Automation.Provider.IValueProvider)box.GetPattern(PatternInterface.Value)).Value;
    }

    // In item order; the visual order puts the selected tab last, because it is drawn above the others.
    private static TabItem[] TabItems(TabControl tabs) =>
        [.. tabs.Items.Cast<object>().Select(item => (TabItem)tabs.ItemContainerGenerator.ContainerFromItem(item))];

    private static Button[] CloseButtons(DependencyObject root) =>
        [.. Visuals.Descendants<Button>(root).Where(button => button.IsVisible && AutomationProperties.GetName(button).StartsWith("Close ", StringComparison.Ordinal))];

    private static TextBox VisibleTextBox(DependencyObject root) => Visuals.Descendants<TextBox>(root).Single(box => box.IsVisible);
}
