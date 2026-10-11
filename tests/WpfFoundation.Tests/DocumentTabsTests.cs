using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using WpfFoundation.Documents;
using WpfFoundation.Navigation;
using static WpfFoundation.Tests.DocumentWorkspaceTests;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class DocumentTabsTests
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
                var host = Visuals.Descendants<DocumentHost>(tabs).Single();
                var ridgeBox = VisibleTextBox(host);
                ridgeBox.Text = "Granite Ridge draft";

                var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
                await UiThread.IdleAsync();
                Assert.HasCount(2, host.RetainedDocuments);
                Assert.IsFalse(ridgeBox.IsVisible, "Only the active document is shown.");

                workspace.Open(Trail("granite-ridge", "Granite Ridge"));
                await UiThread.IdleAsync();
                Assert.AreSame(ridgeBox, VisibleTextBox(host), "Reopening shows the same view.");
                Assert.AreEqual("Granite Ridge draft", ridgeBox.Text);

                await workspace.CloseAsync(ridge);
                await UiThread.IdleAsync();
                CollectionAssert.AreEqual(new[] { falls }, host.RetainedDocuments.ToArray(), "A closed document's view is released.");
                CollectionAssert.DoesNotContain(Visuals.Descendants<TextBox>(host).ToArray(), ridgeBox, "The released view leaves the visual tree.");

                tabs.Workspace = new DocumentWorkspace();
                await UiThread.IdleAsync();
                Assert.IsEmpty(host.RetainedDocuments, "Views of the previous workspace are released.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task TheTabStripFollowsTheWorkspaceAndSelectsDocuments()
    {
        await UiThread.RunAsync(async () =>
        {
            var workspace = new DocumentWorkspace();
            var tabs = CreateTabs(workspace);
            var window = UiThread.ShowWindow(tabs);
            try
            {
                var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
                var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
                workspace.Open(new DocumentItem("trails", "Trails", new TrailViewModel("Trails")) { IsClosable = false });
                await UiThread.IdleAsync();
                var strip = Visuals.Descendants<ListBox>(tabs).Single();

                strip.SelectedItem = ridge;
                Assert.AreSame(ridge, workspace.ActiveDocument, "Selecting a tab activates its document.");

                var items = Visuals.Descendants<ListBoxItem>(strip).ToArray();
                CollectionAssert.AreEqual(
                    new[] { "Granite Ridge", "Cedar Falls", "Trails" },
                    items.Select(AutomationProperties.GetName).ToArray(),
                    "Screen readers announce the document titles.");
                var closeButtons = Visuals.Descendants<Button>(strip).Where(button => button.IsVisible).ToArray();
                CollectionAssert.AreEqual(
                    new[] { "Close Granite Ridge", "Close Cedar Falls" },
                    closeButtons.Select(AutomationProperties.GetName).ToArray(),
                    "Only closable documents show a close button.");

                closeButtons[1].Command.Execute(null);
                await UiThread.IdleAsync();
                CollectionAssert.DoesNotContain(workspace.Documents.ToArray(), falls);
                Assert.AreSame(ridge, strip.SelectedItem);
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
            var navigation = new NavigationService([new NavigationServiceTests.TestPage("Trails"), new NavigationServiceTests.TestPage("Notes")]);
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

    private static DocumentTabs CreateTabs(DocumentWorkspace workspace)
    {
        var tabs = new DocumentTabs { Workspace = workspace };
        // Every document shows an editable box, so retained state is visible.
        tabs.Resources.Add(new DataTemplateKey(typeof(TrailViewModel)), (DataTemplate)XamlReader.Parse(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding Name, Mode=OneTime}" />
            </DataTemplate>
            """));
        return tabs;
    }

    private static TextBox VisibleTextBox(DependencyObject root) => Visuals.Descendants<TextBox>(root).Single(box => box.IsVisible);
}
