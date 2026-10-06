using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfFoundation.Controls;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class PathIconTests
{
    [TestMethod]
    public async Task EveryIconFitsTheSixteenPixelGrid()
    {
        await UiThread.RunAsync(() =>
        {
            var dictionary = new ResourceDictionary { Source = WpfFoundationResources.EntryDictionaryUri };
            string[] keys = [.. KeysOf(dictionary).OfType<string>().Where(key => key.StartsWith("WfIcon", StringComparison.Ordinal) && key != "WfIconContentTemplate")];
            Assert.IsNotEmpty(keys);

            var grid = new Rect(0, 0, PathIcon.GridSize, PathIcon.GridSize);
            foreach (string key in keys)
            {
                var geometry = (Geometry)Application.Current.FindResource(key);
                Assert.IsFalse(geometry.IsEmpty(), key);
                Assert.IsTrue(grid.Contains(geometry.Bounds), $"{key} is outside the grid: {geometry.Bounds}");
            }
        });
    }

    [TestMethod]
    public async Task TheIconIsSizeSquareAndDrawsWithItsForeground()
    {
        await UiThread.RunAsync(() =>
        {
            var icon = new PathIcon { Data = Icon("WfIconAddFiles"), Foreground = Brushes.Black };
            icon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.AreEqual(new Size(PathIcon.GridSize, PathIcon.GridSize), icon.DesiredSize);

            icon.Size = 32;
            icon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            icon.Arrange(new Rect(icon.DesiredSize));
            Assert.AreEqual(new Size(32, 32), icon.DesiredSize);

            var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(icon);
            var pixels = new byte[32 * 32 * 4];
            bitmap.CopyPixels(pixels, 32 * 4, 0);
            int drawn = Enumerable.Range(0, 32 * 32).Count(pixel => pixels[(pixel * 4) + 3] > 0);
            Assert.IsGreaterThan(32 * 32 / 10, drawn, "The icon draws its outline.");
        });
    }

    [TestMethod]
    public async Task TheSizeMustBePositive()
    {
        await UiThread.RunAsync(() =>
        {
            var icon = new PathIcon();
            Assert.ThrowsExactly<ArgumentException>(() => icon.Size = 0);
            Assert.ThrowsExactly<ArgumentException>(() => icon.Size = double.NaN);
        });
    }

    [TestMethod]
    public async Task IconContentTakesAVectorIconOrAGlyph()
    {
        await UiThread.RunAsync(async () =>
        {
            var template = (DataTemplate)Application.Current.FindResource("WfIconContentTemplate");
            var vector = new Button { Content = "Add files", Tag = Icon("WfIconAddFiles"), ContentTemplate = template };
            var glyph = new Button { Content = "Clear all", Tag = "", ContentTemplate = template };
            var window = UiThread.ShowWindow(new StackPanel { Children = { vector, glyph } });
            try
            {
                await UiThread.IdleAsync();

                var icon = Visuals.Descendants<PathIcon>(vector).Single();
                Assert.AreSame(Icon("WfIconAddFiles"), icon.Data);
                Assert.AreEqual(Application.Current.FindResource("DefaultIconFontSize"), icon.Size);
                Assert.IsEmpty(Visuals.Descendants<TextBlock>(vector).Where(text => text.Text.StartsWith("F1", StringComparison.Ordinal)), "The geometry isn't shown as text.");

                Assert.IsEmpty(Visuals.Descendants<PathIcon>(glyph));
                var glyphText = Visuals.Descendants<TextBlock>(glyph).Single(text => text.Text == "");
                Assert.AreEqual(Application.Current.FindResource("SymbolThemeFontFamily"), glyphText.FontFamily);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task TheIconFollowsTheButtonForegroundUnlessSet()
    {
        await UiThread.RunAsync(async () =>
        {
            var button = new Button
            {
                Content = "Add files",
                Tag = Icon("WfIconAddFiles"),
                ContentTemplate = (DataTemplate)Application.Current.FindResource("WfIconContentTemplate")
            };
            var window = UiThread.ShowWindow(button);
            try
            {
                await UiThread.IdleAsync();
                var icon = Visuals.Descendants<PathIcon>(button).Single();
                var label = Visuals.Descendants<TextBlock>(button).Single(text => text.Text == "Add files");
                Assert.IsNotNull(icon.Foreground);
                Assert.AreEqual(label.Foreground, icon.Foreground);

                button.IsEnabled = false;
                await UiThread.IdleAsync();
                Assert.AreEqual(label.Foreground, icon.Foreground, "A disabled button's icon uses its disabled text color.");

                var accent = (Brush)Application.Current.FindResource("SystemFillColorSuccessBrush");
                icon.Foreground = accent;
                Assert.AreSame(accent, icon.Foreground);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Geometry Icon(string key) => (Geometry)Application.Current.FindResource(key);

    private static IEnumerable<object> KeysOf(ResourceDictionary dictionary) =>
        dictionary.Keys.Cast<object>().Concat(dictionary.MergedDictionaries.SelectMany(KeysOf));
}
