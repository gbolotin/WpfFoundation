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
