using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Behaviors;
using WpfFoundation.Controls;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class StyleTests
{
    [TestMethod]
    public async Task CardsUseTheFluentOverlayCornerRadius()
    {
        await UiThread.RunAsync(async () =>
        {
            var card = new Border { Style = (Style)Application.Current.FindResource("WfCardStyle") };
            var window = UiThread.ShowWindow(card);
            try
            {
                await UiThread.IdleAsync();
                Assert.AreEqual(Application.Current.FindResource("OverlayCornerRadius"), card.CornerRadius);
                Assert.AreNotEqual(default, card.CornerRadius);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ToggleSwitchesStayAccessibleCheckBoxes()
    {
        await UiThread.RunAsync(async () =>
        {
            var toggle = new CheckBox
            {
                Content = "Show hidden files",
                Style = (Style)Application.Current.FindResource("WfToggleSwitchStyle")
            };
            System.Windows.Automation.AutomationProperties.SetHelpText(toggle, "Lists files that start with a dot.");
            var window = UiThread.ShowWindow(toggle);
            try
            {
                await UiThread.IdleAsync();
                var state = Visuals.Descendants<TextBlock>(toggle).Single(text => text.Name == "State");
                var description = Visuals.Descendants<TextBlock>(toggle).Single(text => text.Name == "Description");
                Assert.AreEqual("Off", state.Text);
                Assert.AreEqual(Visibility.Visible, description.Visibility);

                var peer = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(toggle).GetPattern(PatternInterface.Toggle);
                peer.Toggle();
                await UiThread.IdleAsync();
                Assert.IsTrue(toggle.IsChecked);
                Assert.AreEqual("On", state.Text);

                Assert.IsTrue(toggle.Focusable && toggle.IsTabStop, "It stays in the tab order, where Space toggles it like any check box.");
                Assert.AreEqual(AutomationControlType.CheckBox, UIElementAutomationPeer.CreatePeerForElement(toggle).GetAutomationControlType());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task OnlyTheSelectedSidebarItemShowsTheSelectionPill()
    {
        await UiThread.RunAsync(async () =>
        {
            var sidebar = new ListBox
            {
                Style = (Style)Application.Current.FindResource("WfNavigationSidebarStyle"),
                ItemsSource = new[] { "Trails", "Maps" }
            };
            var window = UiThread.ShowWindow(sidebar);
            try
            {
                sidebar.SelectedIndex = 1;
                await UiThread.IdleAsync();
                var pills = Visuals.Descendants<System.Windows.Shapes.Rectangle>(sidebar)
                    .Where(rectangle => rectangle.Name == "SelectionPill")
                    .ToList();
                Assert.HasCount(2, pills);
                Assert.AreEqual(Visibility.Collapsed, pills[0].Visibility);
                Assert.AreEqual(Visibility.Visible, pills[1].Visibility);
                Assert.AreEqual(Application.Current.FindResource("ListViewItemPillFillBrush"), pills[1].Fill);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task StatusBarShowsTextAndCommandItems()
    {
        await UiThread.RunAsync(async () =>
        {
            int opened = 0;
            var statusBar = new StatusBar
            {
                Style = (Style)Application.Current.FindResource("WfStatusBarStyle"),
                ItemsSource = new[]
                {
                    new StatusItem("3 files", IsEmphasized: true),
                    new StatusItem("Open log", "Opens the log folder", Command: new RelayCommand(() => opened++))
                }
            };
            var window = UiThread.ShowWindow(statusBar);
            try
            {
                await UiThread.IdleAsync();
                var emphasized = Visuals.Descendants<TextBlock>(statusBar).Single(text => text.Text == "3 files");
                Assert.AreEqual(FontWeights.SemiBold, emphasized.FontWeight);
                Assert.AreEqual("3 files", emphasized.ToolTip);
                var link = Visuals.Descendants<Button>(statusBar).Single();
                Assert.AreEqual("Opens the log folder", link.ToolTip);
                link.Command.Execute(null);
                Assert.AreEqual(1, opened);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task TransparentButtonsFollowAThemeChangeAtRunTime()
    {
        await UiThread.RunAsync(async () =>
        {
            var button = new Button
            {
                Content = "Discard",
                Style = (Style)Application.Current.FindResource("WfTransparentButtonStyle")
            };
            var window = UiThread.ShowWindow(button);
            try
            {
                foreach (var theme in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.Light })
                {
                    Application.Current.ThemeMode = theme;
                    await UiThread.IdleAsync();
                    Assert.AreEqual(ThemeColor("TextFillColorPrimary"), ((SolidColorBrush)button.Foreground).Color, $"Text in {theme}.");
                }
            }
            finally
            {
                window.Close();
                Application.Current.ThemeMode = ThemeMode.Light;
            }
        });

        static Color ThemeColor(string key) => (Color)Application.Current.FindResource(key);
    }

    [TestMethod]
    public async Task GridSplitterGripShowsOnlyOnHoverDragOrFocus()
    {
        await UiThread.RunAsync(async () =>
        {
            var style = (Style)Application.Current.FindResource("WfGridSplitterStyle");
            var splitter = new GridSplitter { Width = 8, Height = 200, Style = style };
            var window = UiThread.ShowWindow(splitter);
            try
            {
                await UiThread.IdleAsync();
                var grip = Visuals.Descendants<System.Windows.Shapes.Rectangle>(splitter).Single(rectangle => rectangle.Name == "PART_Thumb");
                Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)grip.Fill).Color, "The grip is hidden at rest.");

                var shownBy = style.Triggers.OfType<Trigger>()
                    .Where(trigger => trigger.Setters.OfType<Setter>().Any(setter => setter.Property == Control.ForegroundProperty))
                    .Select(trigger => trigger.Property)
                    .ToList();
                CollectionAssert.AreEquivalent(
                    new[] { UIElement.IsMouseOverProperty, Thumb.IsDraggingProperty, UIElement.IsKeyboardFocusedProperty },
                    shownBy);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task TheLastGridViewColumnFillsTheListButKeepsItsMinimumWidth()
    {
        await UiThread.RunAsync(async () =>
        {
            var grid = new GridView();
            grid.Columns.Add(new GridViewColumn { Header = "Name", Width = 120, DisplayMemberBinding = new Binding() });
            grid.Columns.Add(new GridViewColumn { Header = "Notes", DisplayMemberBinding = new Binding() });
            var list = new ListView { View = grid, ItemsSource = new[] { "Alpha", "Beta" } };
            GridViewSizing.SetLastColumnMinimumWidth(list, 200);
            var window = UiThread.ShowWindow(list, width: 700);
            try
            {
                await UiThread.IdleAsync();
                double filled = grid.Columns[1].Width;
                Assert.IsGreaterThan(300, filled, "The last column takes the remaining width.");

                window.Width = 300;
                await UiThread.IdleAsync();
                Assert.AreEqual(200, grid.Columns[1].Width, 0.1, "The last column does not shrink below its minimum.");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
