using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfFoundation.Notifications;

namespace WpfFoundation.Tests;

/// <summary>Registration against a temporary registry key and Start menu folder, so the user's settings stay untouched.</summary>
[TestClass]
public sealed class WindowsNotificationServiceTests
{
    private const string AppId = "WpfFoundation.Tests.TrailNotes";
    private readonly string keyPath = $@"Software\WpfFoundation.Tests.{Guid.NewGuid():N}";
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"WpfFoundation.Tests.{Guid.NewGuid():N}");
    private RegistryKey? root;

    [TestInitialize]
    public void CreateRoot() => root = Registry.CurrentUser.CreateSubKey(keyPath);

    [TestCleanup]
    public void DeleteRoot()
    {
        root?.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task RegisteringWritesTheNameIconActivatorAndShortcut()
    {
        await UiThread.RunAsync(() =>
        {
            var service = Service(@"C:\Apps\TrailNotes\TrailNotes.exe");
            service.Register(AppId, "Trail Notes", @"C:\Apps\TrailNotes\trail.ico");

            using var app = root!.OpenSubKey($@"AppUserModelId\{AppId}");
            Assert.IsNotNull(app);
            Assert.AreEqual("Trail Notes", app.GetValue("DisplayName"));
            Assert.AreEqual(@"C:\Apps\TrailNotes\trail.ico", app.GetValue("IconUri"));

            var clsid = WindowsNotificationService.ActivatorClsid(AppId);
            using var server = root.OpenSubKey($@"CLSID\{clsid:B}\LocalServer32");
            Assert.AreEqual("\"C:\\Apps\\TrailNotes\\TrailNotes.exe\"", server?.GetValue(string.Empty));

            var shortcut = ShellLink.Read(Path.Combine(folder, "Trail Notes.lnk"));
            Assert.IsNotNull(shortcut);
            Assert.AreEqual(AppId, shortcut.AppId);
            Assert.AreEqual(clsid, shortcut.ActivatorClsid);
            Assert.AreEqual(@"C:\Apps\TrailNotes\TrailNotes.exe", shortcut.TargetPath);
        });
    }

    [TestMethod]
    public async Task RegisteringAgainIsIdempotentAndFollowsAMovedFolder()
    {
        await UiThread.RunAsync(() =>
        {
            Service(@"C:\Apps\TrailNotes\TrailNotes.exe").Register(AppId, "Trail Notes", null);
            var path = Path.Combine(folder, "Trail Notes.lnk");
            var written = File.GetLastWriteTimeUtc(path);

            Service(@"C:\Apps\TrailNotes\TrailNotes.exe").Register(AppId, "Trail Notes", null);
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(path), "An up-to-date shortcut is left alone.");

            Service(@"D:\Portable\TrailNotes\TrailNotes.exe").Register(AppId, "Trail Notes", null);
            Assert.AreEqual(@"D:\Portable\TrailNotes\TrailNotes.exe", ShellLink.Read(path)?.TargetPath);
            Assert.HasCount(1, Directory.GetFiles(folder));

            var clsid = WindowsNotificationService.ActivatorClsid(AppId);
            using var server = root!.OpenSubKey($@"CLSID\{clsid:B}\LocalServer32");
            Assert.AreEqual("\"D:\\Portable\\TrailNotes\\TrailNotes.exe\"", server?.GetValue(string.Empty));
        });
    }

    [TestMethod]
    public async Task AnotherApplicationsShortcutIsNotReplaced()
    {
        await UiThread.RunAsync(() =>
        {
            var path = Path.Combine(folder, "Trail Notes.lnk");
            ShellLink.Write(path, @"C:\Other\Other.exe", "Contoso.Other", Guid.NewGuid());

            Assert.ThrowsExactly<InvalidOperationException>(() => Service(@"C:\Apps\TrailNotes\TrailNotes.exe").Register(AppId, "Trail Notes", null));
            Assert.AreEqual("Contoso.Other", ShellLink.Read(path)?.AppId);
        });
    }

    [TestMethod]
    public async Task UnregisteringRemovesEverything()
    {
        await UiThread.RunAsync(() =>
        {
            var service = Service(@"C:\Apps\TrailNotes\TrailNotes.exe");
            service.Unregister();
            service.Register(AppId, "Trail Notes", null);
            service.Unregister();

            Assert.IsNull(root!.OpenSubKey($@"AppUserModelId\{AppId}"));
            Assert.IsNull(root.OpenSubKey($@"CLSID\{WindowsNotificationService.ActivatorClsid(AppId):B}"));
            Assert.IsFalse(File.Exists(Path.Combine(folder, "Trail Notes.lnk")));
        });
    }

    [TestMethod]
    public void NotificationXmlEscapesTextAndKeepsWarningsLonger()
    {
        Assert.AreEqual(
            "<toast><visual><binding template=\"ToastGeneric\"><text>Import &amp; sort</text><text>48 photos &lt;new&gt;</text></binding></visual></toast>",
            WindowsNotificationService.ToastXml("Import & sort", "48 photos <new>", NotificationKind.Success));
        Assert.AreEqual(
            "<toast duration=\"long\"><visual><binding template=\"ToastGeneric\"><text>Import failed</text></binding></visual></toast>",
            WindowsNotificationService.ToastXml("Import failed", string.Empty, NotificationKind.Error));
    }

    [TestMethod]
    public void TheActivatorClsidIsStablePerApplication()
    {
        Assert.AreEqual(WindowsNotificationService.ActivatorClsid(AppId), WindowsNotificationService.ActivatorClsid(AppId));
        Assert.AreNotEqual(WindowsNotificationService.ActivatorClsid(AppId), WindowsNotificationService.ActivatorClsid("Contoso.Other"));
    }

    [TestMethod]
    public async Task ShowingBeforeRegisteringDoesNothing()
    {
        await UiThread.RunAsync(() => Service(@"C:\Apps\TrailNotes\TrailNotes.exe").Show("Import finished", "48 photos"));
    }

    private WindowsNotificationService Service(string executablePath) =>
        new(Application.Current, root!, folder, executablePath, activatesInProcess: false);
}
