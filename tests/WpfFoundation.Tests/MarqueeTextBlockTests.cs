using System.Windows;
using System.Windows.Controls;
using WpfFoundation.Controls;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class MarqueeTextBlockTests
{
    private const string LongText = "A long fictional album title that cannot fit in a narrow space at all";

    [TestMethod]
    public async Task OnlyOverflowingTextScrolls()
    {
        await UiThread.RunAsync(async () =>
        {
            var marquee = new MarqueeTextBlock { Text = "Short", Width = 150 };
            var window = UiThread.ShowWindow(marquee);
            try
            {
                await UiThread.IdleAsync();
                Assert.IsFalse(marquee.IsScrolling, "Text that fits stays still.");

                marquee.Text = LongText;
                await UiThread.IdleAsync();
                Assert.AreEqual(SystemParameters.ClientAreaAnimation, marquee.IsScrolling, "Overflowing text scrolls when Windows animations are on.");

                marquee.Width = 2000;
                await UiThread.IdleAsync();
                Assert.IsFalse(marquee.IsScrolling, "Widening the control so the text fits stops scrolling.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ScrollingStopsWhenDisabledHiddenOrUnloaded()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Assert.Inconclusive("Windows animation effects are off on this machine, so nothing scrolls.");
        }

        await UiThread.RunAsync(async () =>
        {
            var marquee = new MarqueeTextBlock { Text = LongText, Width = 120 };
            var host = new Border { Child = marquee };
            var window = UiThread.ShowWindow(host);
            try
            {
                await UiThread.IdleAsync();
                Assert.IsTrue(marquee.IsScrolling);

                marquee.IsAnimationEnabled = false;
                await UiThread.IdleAsync();
                Assert.IsFalse(marquee.IsScrolling, "Disabling animation stops scrolling.");
                marquee.IsAnimationEnabled = true;
                await UiThread.IdleAsync();
                Assert.IsTrue(marquee.IsScrolling);

                host.Visibility = Visibility.Collapsed;
                await UiThread.IdleAsync();
                Assert.IsFalse(marquee.IsScrolling, "Hidden text does not scroll.");
                host.Visibility = Visibility.Visible;
                await UiThread.IdleAsync();
                Assert.IsTrue(marquee.IsScrolling, "Scrolling resumes when shown again.");

                host.Child = null;
                await UiThread.IdleAsync();
                Assert.IsFalse(marquee.IsScrolling, "Unloaded text does not scroll.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task GapAndSpeedRejectInvalidValues()
    {
        await UiThread.RunAsync(() =>
        {
            var marquee = new MarqueeTextBlock();

            Assert.ThrowsExactly<ArgumentException>(() => marquee.Gap = -1);
            Assert.ThrowsExactly<ArgumentException>(() => marquee.PixelsPerSecond = 0);
            Assert.ThrowsExactly<ArgumentException>(() => marquee.PixelsPerSecond = double.NaN);
            marquee.Gap = 0;
            marquee.PixelsPerSecond = 80;
            Assert.AreEqual(80, marquee.PixelsPerSecond);
        });
    }
}
