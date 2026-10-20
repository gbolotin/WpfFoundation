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
    [DataRow("WfSubtleButtonStyle")]
    [DataRow("WfTransparentButtonStyle")]
    [DataRow("WfItemHoverButtonStyle")]
    public async Task SubtleButtonsFillOnlyWhileHoveredOrPressed(string styleKey)
    {
        await UiThread.RunAsync(async () =>
        {
            var button = new Button
            {
                Content = "\uE711",
                Style = (Style)Application.Current.FindResource(styleKey)
            };
            // The item hover style hides its button outside a hovered item; this test checks only the fills.
            button.Visibility = Visibility.Visible;
            var window = UiThread.ShowWindow(button);
            try
            {
                await UiThread.IdleAsync();
                Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)Visuals.Descendants<Border>(button).First().Background).Color, "No fill at rest.");
                Assert.AreEqual(new Thickness(0), button.BorderThickness);

                string? Fill(DependencyProperty state) => button.Template.Triggers.OfType<Trigger>()
                    .Single(trigger => trigger.Property == state)
                    .Setters.OfType<Setter>()
                    .Where(setter => setter.Property == Border.BackgroundProperty)
                    .Select(setter => (setter.Value as DynamicResourceExtension)?.ResourceKey as string)
                    .Single();
                Assert.AreEqual("SubtleFillColorSecondaryBrush", Fill(UIElement.IsMouseOverProperty), "Hover shows the Fluent subtle fill.");
                Assert.AreEqual("SubtleFillColorTertiaryBrush", Fill(ButtonBase.IsPressedProperty), "Press shows the stronger subtle fill.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ItemHoverButtonsShowOnlyOverTheirItemWhereAvailable()
    {
        await UiThread.RunAsync(async () =>
        {
            var style = (Style)Application.Current.FindResource("WfItemHoverButtonStyle");
            var button = new Button { Content = "\uE74D", Style = style };
            var list = new ListBox();
            list.Items.Add(new ListBoxItem { Content = button });
            var window = UiThread.ShowWindow(list);
            try
            {
                await UiThread.IdleAsync();
                Assert.IsTrue(HoverAction.GetIsAvailable(button), "Actions apply unless the application says otherwise.");
                Assert.AreEqual(Visibility.Hidden, button.Visibility, "Hidden while the pointer is elsewhere, keeping its slot.");
                Assert.IsTrue(button.ActualWidth > 0, "A hidden button keeps its slot.");

                // The pointer cannot be moved in a test, so check the rule that shows the button.
                var show = style.Triggers.OfType<MultiDataTrigger>().Single();
                var conditions = show.Conditions.Select(condition => (Binding)condition.Binding).ToArray();
                Assert.AreEqual(typeof(ListBoxItem), conditions[0].RelativeSource.AncestorType, "Any ListBox or ListView item.");
                Assert.AreEqual(nameof(UIElement.IsMouseOver), conditions[0].Path.Path);
                Assert.AreEqual(RelativeSourceMode.Self, conditions[1].RelativeSource.Mode);
                StringAssert.Contains(conditions[1].Path.Path, "HoverAction.IsAvailable", "The application decides where the action applies.");
                Assert.IsTrue(show.Conditions.All(condition => bool.Parse(condition.Value.ToString()!)), "Both hovered and available.");
                Assert.AreEqual(Visibility.Visible, show.Setters.OfType<Setter>().Single().Value);
                var disabled = style.Triggers.OfType<Trigger>().Single(trigger => trigger.Property == UIElement.IsEnabledProperty);
                Assert.AreEqual(Visibility.Hidden, disabled.Setters.OfType<Setter>().Single().Value, "A disabled action stays hidden.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task NavigationButtonsLookLikeSidebarItems()
    {
        await UiThread.RunAsync(async () =>
        {
            var sidebar = new ListBox
            {
                Style = (Style)Application.Current.FindResource("WfNavigationSidebarStyle"),
                ItemsSource = new[] { new NavigationServiceTests.TestPage("Trails") }
            };
            var button = new Button
            {
                Content = "Settings",
                Tag = "\uE713",
                Style = (Style)Application.Current.FindResource("WfNavigationButtonStyle")
            };
            var window = UiThread.ShowWindow(new StackPanel { Children = { sidebar, button } });
            try
            {
                await UiThread.IdleAsync();
                var item = (ListBoxItem)sidebar.ItemContainerGenerator.ContainerFromIndex(0);
                Assert.AreEqual(item.Padding, button.Padding);
                Assert.AreEqual(Visuals.Descendants<Border>(item).First().CornerRadius, Visuals.Descendants<Border>(button).First().CornerRadius);
                Assert.AreEqual(Application.Current.FindResource("ControlCornerRadius"), Visuals.Descendants<Border>(button).First().CornerRadius);
                Assert.AreEqual(HorizontalAlignment.Left, button.HorizontalContentAlignment);
                var icon = Visuals.Descendants<TextBlock>(button).Single(text => text.Text == "\uE713");
                Assert.AreEqual(Application.Current.FindResource("SymbolThemeFontFamily"), icon.FontFamily);
                Assert.IsTrue(Visuals.Descendants<TextBlock>(button).Any(text => text.Text == "Settings"), "Content is the name after the icon.");
                Assert.IsTrue(button.Focusable && button.IsTabStop, "It stays a keyboard-reachable button.");
                Assert.AreSame(Application.Current.FindResource("WfSubtleButtonStyle"), button.Style.BasedOn, "It fills on hover like a subtle button.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task GridSplittersShowTheirGripOnlyOnHoverDragOrFocusByDefault()
    {
        await UiThread.RunAsync(async () =>
        {
            var style = (Style)Application.Current.FindResource("WfGridSplitterStyle");
            var splitter = new GridSplitter { Width = 8, Height = 200 };
            var window = UiThread.ShowWindow(splitter);
            try
            {
                await UiThread.IdleAsync();
                var grip = Visuals.Descendants<System.Windows.Shapes.Rectangle>(splitter).Single(rectangle => rectangle.Name == "PART_Thumb");
                Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)grip.Fill).Color, "A plain splitter gets the style, and its grip is hidden at rest.");

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
