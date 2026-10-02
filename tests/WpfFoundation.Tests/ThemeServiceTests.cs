using System.Windows;
using WpfFoundation.Theming;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class ThemeServiceTests
{
    [TestMethod]
    public async Task ApplyingAThemeSwitchesTheApplicationImmediately()
    {
        await UiThread.RunAsync(() =>
        {
            var service = new ThemeService(Application.Current);
            try
            {
                Assert.AreEqual(ThemePreference.System, service.CurrentTheme);

                service.ApplyTheme(ThemePreference.Dark);
                Assert.AreEqual(ThemeMode.Dark, Application.Current.ThemeMode);
                Assert.AreEqual(ThemePreference.Dark, service.CurrentTheme);

                service.ApplyTheme(ThemePreference.System);
                Assert.AreEqual(ThemeMode.System, Application.Current.ThemeMode);

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.ApplyTheme((ThemePreference)42));
                Assert.AreEqual(ThemePreference.System, service.CurrentTheme, "An unknown theme changes nothing.");
            }
            finally
            {
                Application.Current.ThemeMode = ThemeMode.Light;
            }
        });
    }

    [TestMethod]
    public async Task ApplyingFromAnotherThreadRunsOnTheApplicationDispatcher()
    {
        var service = await UiThread.RunAsync(() => new ThemeService(Application.Current));
        try
        {
            await Task.Run(() => service.ApplyTheme(ThemePreference.Dark));
            Assert.AreEqual(ThemeMode.Dark, await UiThread.RunAsync(() => Application.Current.ThemeMode));
        }
        finally
        {
            await UiThread.RunAsync(() => Application.Current.ThemeMode = ThemeMode.Light);
        }
    }
}
