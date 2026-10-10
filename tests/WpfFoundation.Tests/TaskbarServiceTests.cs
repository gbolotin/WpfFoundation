using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using WpfFoundation.Taskbar;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class TaskbarServiceTests
{
    [TestMethod]
    public async Task ProgressAndStatesReachTheMainWindowsTaskbarItem()
    {
        await WithMainWindowAsync(window =>
        {
            var taskbar = new TaskbarService(Application.Current);

            taskbar.SetProgress(null);
            Assert.IsNotNull(window.TaskbarItemInfo, "The taskbar item is created on first use.");
            Assert.IsFalse(taskbar.IsWindowActive, "The test window is shown without being activated.");
            Assert.AreEqual(TaskbarItemProgressState.Indeterminate, window.TaskbarItemInfo.ProgressState);

            taskbar.SetProgress(0.4);
            Assert.AreEqual(TaskbarItemProgressState.Normal, window.TaskbarItemInfo.ProgressState);
            Assert.AreEqual(0.4, window.TaskbarItemInfo.ProgressValue);

            taskbar.SetProgress(null, TaskbarProgressState.Paused);
            Assert.AreEqual(TaskbarItemProgressState.Paused, window.TaskbarItemInfo.ProgressState);
            Assert.AreEqual(0.4, window.TaskbarItemInfo.ProgressValue, "A paused bar keeps the last value.");

            taskbar.SetProgress(null, TaskbarProgressState.Error);
            Assert.AreEqual(TaskbarItemProgressState.Error, window.TaskbarItemInfo.ProgressState);
            Assert.AreEqual(0.4, window.TaskbarItemInfo.ProgressValue);

            taskbar.ClearProgress();
            Assert.AreEqual(TaskbarItemProgressState.None, window.TaskbarItemInfo.ProgressState);

            taskbar.SetProgress(null);
            taskbar.SetProgress(null, TaskbarProgressState.Error);
            Assert.AreEqual(1, window.TaskbarItemInfo.ProgressValue, "A failure before any percentage fills the bar.");

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => taskbar.SetProgress(-0.1));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task OverlaysAreDrawnForTheWindowAndRedrawnWhenTheThemeChanges()
    {
        await WithMainWindowAsync(async window =>
        {
            var taskbar = new TaskbarService(Application.Current);

            taskbar.ShowOverlay(TaskbarOverlay.Error, "Conversion failed");
            var badge = window.TaskbarItemInfo.Overlay as BitmapSource;
            Assert.IsNotNull(badge);
            Assert.AreEqual(16, badge.Width, 0.01, "The badge is 16 × 16 device-independent pixels.");
            Assert.AreEqual("Conversion failed", window.TaskbarItemInfo.Description);

            try
            {
                Application.Current.ThemeMode = ThemeMode.Dark;
                await UiThread.IdleAsync();
                Assert.AreNotSame(badge, window.TaskbarItemInfo.Overlay, "A theme change redraws the badge in the new theme's color.");
            }
            finally
            {
                Application.Current.ThemeMode = ThemeMode.Light;
            }

            taskbar.ShowOverlay(TaskbarOverlay.None);
            Assert.IsNull(window.TaskbarItemInfo.Overlay);
            Assert.AreEqual(string.Empty, window.TaskbarItemInfo.Description);
        });
    }

    [TestMethod]
    public async Task CallsFromAnotherThreadRunOnTheUiThread()
    {
        await WithMainWindowAsync(async window =>
        {
            var taskbar = new TaskbarService(Application.Current);
            await Task.Run(() => taskbar.SetProgress(0.7, TaskbarProgressState.Paused));
            await UiThread.IdleAsync();

            Assert.AreEqual(TaskbarItemProgressState.Paused, window.TaskbarItemInfo.ProgressState);
            Assert.AreEqual(0.7, window.TaskbarItemInfo.ProgressValue);
        });
    }

    [TestMethod]
    public async Task WithoutAMainWindowNothingHappens()
    {
        await UiThread.RunAsync(() =>
        {
            var previous = Application.Current.MainWindow;
            Application.Current.MainWindow = null;
            try
            {
                var taskbar = new TaskbarService(Application.Current);
                taskbar.SetProgress(0.5);
                taskbar.ShowOverlay(TaskbarOverlay.Success);
                taskbar.FlashUntilActivated();
            }
            finally
            {
                Application.Current.MainWindow = previous;
            }
        });
    }

    private static Task WithMainWindowAsync(Func<Window, Task> test) => UiThread.RunAsync(async () =>
    {
        var previous = Application.Current.MainWindow;
        var window = UiThread.ShowWindow(new object());
        Application.Current.MainWindow = window;
        try
        {
            await test(window);
        }
        finally
        {
            Application.Current.MainWindow = previous;
            window.Close();
        }
    });
}
