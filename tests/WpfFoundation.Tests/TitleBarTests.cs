using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using WpfFoundation.Controls;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class TitleBarTests
{
    [TestMethod]
    public async Task WhileTheWindowShowsTheBackdropTheTitleBarReplacesTheSystemTitleBar()
    {
        await UiThread.RunAsync(async () =>
        {
            var menu = new Menu { Items = { new MenuItem { Header = "_File" } } };
            var titleBar = new TitleBar { Content = menu };
            var window = UiThread.ShowWindow(Layout(titleBar));
            try
            {
                window.Title = "Trail Notes";
                await UiThread.IdleAsync();
                if (window.Background is not SolidColorBrush { Color.A: 0 })
                {
                    Assert.Inconclusive("The Fluent backdrop needs Windows 11 without high contrast.");
                }

                Assert.IsTrue(titleBar.IsExtended);
                var chrome = WindowChrome.GetWindowChrome(window);
                Assert.IsNotNull(chrome, "The title bar replaces the system title bar through WindowChrome.");
                Assert.AreEqual(new Thickness(-1), chrome.GlassFrameThickness, "Windows draws the caption buttons on the backdrop.");
                Assert.IsTrue(chrome.UseAeroCaptionButtons, "The caption buttons take their own clicks, with Snap Layouts.");
                Assert.AreEqual(titleBar.ActualHeight - chrome.ResizeBorderThickness.Top, chrome.CaptionHeight, 0.5, "The row is the window's caption.");
                // WindowChrome asks the element under the pointer whether it takes clicks or is part of the caption.
                var title = Visuals.Descendants<TextBlock>(titleBar).Single(text => text.Text == "Trail Notes");
                Assert.IsTrue(InChrome(window, (MenuItem)menu.Items[0]), "The menu takes its own clicks.");
                Assert.IsFalse(InChrome(window, title), "The title drags the window.");
                Assert.IsTrue(title.IsVisible, "The row shows the window's title.");
                Assert.IsTrue(Left(title, titleBar) < Left(menu, titleBar), "The title comes before the menu.");
                var file = (MenuItem)menu.Items[0];
                Assert.IsTrue(TitleBar.GetIsExtended(file), "The content inherits IsExtended.");
                // Fluent's top-level items are 49 px; Windows 11 caption buttons are about 29 to 32 px. DWM's caption
                // bounds can't be the reference: for this off-screen window they follow another monitor's DPI.
                Assert.IsTrue(menu.ActualHeight < 32, "Top-level menu items fit the caption buttons' height.");
                var header = Visuals.Descendants<ContentPresenter>(file).First(presenter => presenter.Name == "HeaderPresenter");
                double labelSpacing = file.ActualWidth + file.Margin.Left + file.Margin.Right - header.ActualWidth;
                Assert.AreEqual(18, labelSpacing, 1, "Labels sit about 18 px apart, not Fluent's 30.");

                // The icon is inset equally from the row's left and top, and lines up with the capital letters of
                // the labels rather than their line box, which keeps room for descenders.
                window.Icon = System.Windows.Media.Imaging.BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, new byte[16 * 16 * 4], 64);
                await UiThread.IdleAsync();
                var icon = Part(titleBar, "PART_Icon");
                var iconBounds = icon.TransformToAncestor(titleBar).TransformBounds(new Rect(icon.RenderSize));
                Assert.AreEqual(iconBounds.Left, iconBounds.Top, 0.01, "Equal left and top insets.");
                var label = Visuals.Descendants<TextBlock>(file).First();
                var capitals = new FormattedText("F", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch), label.FontSize, Brushes.Black, 1)
                    .BuildGeometry(default).Bounds;
                double capitalsCenter = label.TransformToAncestor(titleBar).Transform(default).Y + capitals.Top + capitals.Height / 2;
                Assert.AreEqual(capitalsCenter, iconBounds.Top + iconBounds.Height / 2, 0.75, "The icon lines up with the labels' capital letters.");
                Assert.IsTrue(iconBounds.Bottom <= titleBar.ActualHeight, "The icon stays in the row.");

                // The first label sits as far after the title, or after the icon when there is no title, as the labels
                // sit from each other.
                Assert.AreEqual(labelSpacing, Left(header, titleBar) - (Left(title, titleBar) + title.ActualWidth), 0.5, "Title to first label.");
                window.Title = "";
                await UiThread.IdleAsync();
                Assert.IsFalse(title.IsVisible, "An empty title takes no space.");
                Assert.AreEqual(labelSpacing, Left(header, titleBar) - iconBounds.Right, 0.5, "Icon to first label.");
                window.Title = "Trail Notes";
                await UiThread.IdleAsync();

                // A narrow row trims the title, and the menu keeps its width clear of the caption buttons.
                window.Title = "Trail Notes for the long northern ridge loop past the quarry and the old fire tower";
                window.Width = 400;
                await UiThread.IdleAsync();
                var captionButtons = Part(titleBar, "PART_CaptionButtons");
                var untrimmed = new TextBlock { Text = title.Text, FontFamily = title.FontFamily, FontSize = title.FontSize };
                untrimmed.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.IsTrue(title.ActualWidth < untrimmed.DesiredSize.Width, "The title trims.");
                Assert.AreEqual(menu.DesiredSize.Width, menu.ActualWidth, 0.5, "The menu keeps its width.");
                Assert.IsTrue(Left(menu, titleBar) + menu.ActualWidth <= Left(captionButtons, titleBar) + 0.5, "The menu stays clear of the caption buttons.");
                Assert.IsTrue(Part(titleBar, "PART_CaptionButtons").ActualWidth > 0, "The row keeps the content clear of the caption buttons.");
                Assert.IsTrue(menu.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task RightHeaderEndsTheRowBeforeTheCaptionButtons()
    {
        await UiThread.RunAsync(async () =>
        {
            var menu = new Menu { Items = { new MenuItem { Header = "_File" } } };
            var settings = new Button
            {
                Content = "\uE713",
                FontFamily = (FontFamily)Application.Current.FindResource("SymbolThemeFontFamily"),
                Padding = new Thickness(10, 4, 10, 4),
                Style = (Style)Application.Current.FindResource("WfSubtleButtonStyle")
            };
            settings.SetBinding(ContentControl.ToolTipProperty, new System.Windows.Data.Binding("Name"));
            var titleBar = new TitleBar { Content = menu, DataContext = new { Name = "Settings" } };
            var window = UiThread.ShowWindow(Layout(titleBar));
            try
            {
                await UiThread.IdleAsync();
                double menuOnlyHeight = titleBar.ActualHeight;
                titleBar.RightHeader = settings;
                await UiThread.IdleAsync();
                Assert.AreSame(titleBar, LogicalTreeHelper.GetParent(settings), "The header belongs to the title bar's logical tree.");
                Assert.AreEqual("Settings", settings.ToolTip, "The header inherits the title bar's DataContext.");
                Assert.IsTrue(settings.IsVisible);
                if (window.Background is not SolidColorBrush { Color.A: 0 })
                {
                    Assert.Inconclusive("The Fluent backdrop needs Windows 11 without high contrast.");
                }

                var captionButtons = Part(titleBar, "PART_CaptionButtons");
                Assert.IsTrue(Left(settings, titleBar) + settings.ActualWidth <= Left(captionButtons, titleBar) + 0.5, "The header stays clear of the caption buttons.");
                Assert.IsTrue(Left(settings, titleBar) > Left(menu, titleBar) + menu.ActualWidth, "The header follows the menu.");
                Assert.IsTrue(InChrome(window, settings), "The header takes its own clicks.");
                Assert.AreEqual(menuOnlyHeight, titleBar.ActualHeight, 0.5, "A subtle icon button with 4 px vertical padding keeps the row's height.");

                titleBar.RightHeader = null;
                Assert.IsNull(LogicalTreeHelper.GetParent(settings));
                Assert.AreEqual(DependencyProperty.UnsetValue, settings.ReadLocalValue(WindowChrome.IsHitTestVisibleInChromeProperty), "A removed header is unmarked.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task WithoutTheBackdropTheRowShowsOnlyTheContentBelowTheSystemTitleBar()
    {
        await UiThread.RunAsync(async () =>
        {
            var menu = new Menu { Items = { new MenuItem { Header = "_File" } } };
            var titleBar = new TitleBar { Content = menu };
            var window = UiThread.ShowWindow(Layout(titleBar));
            try
            {
                window.Title = "Trail Notes";
                // The Fluent theme makes the background opaque in high contrast and before Windows 11.
                window.Background = Brushes.White;
                await UiThread.IdleAsync();

                Assert.IsFalse(titleBar.IsExtended);
                Assert.IsNull(WindowChrome.GetWindowChrome(window), "The window keeps its system title bar.");
                Assert.IsFalse(Visuals.Descendants<TextBlock>(titleBar).Single(text => text.Text == "Trail Notes").IsVisible, "The system title bar shows the title.");
                Assert.IsFalse(Part(titleBar, "PART_Icon").IsVisible);
                Assert.IsTrue(menu.IsVisible, "The content shows as a menu bar below the system title bar.");
                Assert.AreEqual(default, ((MenuItem)menu.Items[0]).Padding, "The menu bar keeps Fluent's size.");

                window.Background = Brushes.Transparent;
                await UiThread.IdleAsync();
                Assert.IsTrue(titleBar.IsExtended, "The title bar follows the window's Background.");
                Assert.IsNotNull(WindowChrome.GetWindowChrome(window));

                window.Background = Brushes.White;
                await UiThread.IdleAsync();
                Assert.IsFalse(titleBar.IsExtended);
                Assert.IsNull(WindowChrome.GetWindowChrome(window), "Losing the backdrop brings back the system title bar.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ExtendsIntoTitleBarFalseKeepsTheSystemTitleBarWithTheBackdrop()
    {
        await UiThread.RunAsync(async () =>
        {
            var menu = new Menu { Items = { new MenuItem { Header = "_File" } } };
            var titleBar = new TitleBar { Content = menu, ExtendsIntoTitleBar = false };
            var window = UiThread.ShowWindow(Layout(titleBar));
            try
            {
                window.Background = Brushes.Transparent;
                await UiThread.IdleAsync();
                Assert.IsFalse(titleBar.IsExtended);
                Assert.IsNull(WindowChrome.GetWindowChrome(window), "The window keeps its system title bar.");
                Assert.IsTrue(menu.IsVisible, "The menu shows below the system title bar.");

                titleBar.ExtendsIntoTitleBar = true;
                await UiThread.IdleAsync();
                Assert.IsTrue(titleBar.IsExtended, "Turning it on replaces the system title bar at once.");
                Assert.IsNotNull(WindowChrome.GetWindowChrome(window));

                titleBar.ExtendsIntoTitleBar = false;
                await UiThread.IdleAsync();
                Assert.IsFalse(titleBar.IsExtended);
                Assert.IsNull(WindowChrome.GetWindowChrome(window), "Turning it off brings back the system title bar.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task RemovingTheTitleBarBringsBackTheSystemTitleBar()
    {
        await UiThread.RunAsync(async () =>
        {
            var titleBar = new TitleBar { Content = new Menu() };
            var layout = Layout(titleBar);
            var window = UiThread.ShowWindow(layout);
            try
            {
                window.Background = Brushes.Transparent;
                await UiThread.IdleAsync();
                Assert.IsNotNull(WindowChrome.GetWindowChrome(window));

                layout.Children.Remove(titleBar);
                await UiThread.IdleAsync();
                Assert.IsNull(WindowChrome.GetWindowChrome(window));
                Assert.IsFalse(titleBar.IsExtended);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static DockPanel Layout(TitleBar titleBar)
    {
        DockPanel.SetDock(titleBar, Dock.Top);
        return new DockPanel { Children = { titleBar, new TextBlock { Text = "Ridge trail" } } };
    }

    private static double Left(FrameworkElement element, Visual ancestor) => element.TransformToAncestor(ancestor).Transform(default).X;

    private static bool InChrome(Window window, FrameworkElement element)
    {
        var center = element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), window);
        return window.InputHitTest(center) is { } hit && WindowChrome.GetIsHitTestVisibleInChrome(hit);
    }

    private static FrameworkElement Part(TitleBar titleBar, string name) =>
        (FrameworkElement)titleBar.Template.FindName(name, titleBar);
}
