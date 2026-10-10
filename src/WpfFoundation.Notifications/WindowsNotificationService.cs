using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace WpfFoundation.Notifications;

/// <summary>
/// Sends Windows notifications from an unpackaged WPF application with <c>Windows.UI.Notifications</c>.
/// <see cref="Register"/> gives Windows what it needs to show the application's name and icon and to route clicks:
/// the name and icon under <c>HKCU\Software\Classes\AppUserModelId</c>, a per-user Start menu shortcut carrying the
/// AppUserModelId and a toast activator CLSID, that CLSID's <c>LocalServer32</c> key, and the activator itself while
/// the application runs. A click brings the main window to the front; after exit, Windows starts the application.
/// Windows' Do not disturb and the application's switch under Settings &gt; System &gt; Notifications apply as usual.
/// </summary>
public sealed class WindowsNotificationService : INotificationService, IDisposable
{
    private readonly Application application;
    private readonly RegistryKey classesRoot;
    private readonly string startMenuFolder;
    private readonly string executablePath;
    private readonly bool activatesInProcess;
    private Registration? registration;
    private ToastActivator? activator;

    /// <param name="application">The application whose main window a click brings to the front.</param>
    public WindowsNotificationService(Application application)
        : this(
            application,
            Registry.CurrentUser.CreateSubKey(@"Software\Classes"),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unknown."),
            activatesInProcess: true)
    {
    }

    /// <summary>Uses another registry root, Start menu folder and executable, so tests leave the user's settings alone.</summary>
    internal WindowsNotificationService(Application application, RegistryKey classesRoot, string startMenuFolder, string executablePath, bool activatesInProcess)
    {
        ArgumentNullException.ThrowIfNull(application);
        this.application = application;
        this.classesRoot = classesRoot;
        this.startMenuFolder = startMenuFolder;
        this.executablePath = executablePath;
        this.activatesInProcess = activatesInProcess;
    }

    /// <summary>Raised on the UI thread after a click on one of the application's notifications brought the main window to the front.</summary>
    public event EventHandler? Activated;

    /// <inheritdoc />
    /// <remarks>
    /// Call it before the main window is shown: it also sets the process's AppUserModelId, so the taskbar groups the
    /// window with its Start menu shortcut. An existing shortcut with the same name is updated only when it carries the
    /// same AppUserModelId or points to an executable with the same file name, for example after the application's folder
    /// moved.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A different application's shortcut already has the display name.</exception>
    public void Register(string appId, string displayName, string? iconPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var next = new Registration(appId, ActivatorClsid(appId), ShortcutPath(displayName));
        using (var key = classesRoot.CreateSubKey($@"AppUserModelId\{appId}"))
        {
            key.SetValue("DisplayName", displayName);
            if (iconPath is null)
            {
                key.DeleteValue("IconUri", throwOnMissingValue: false);
            }
            else
            {
                key.SetValue("IconUri", Path.GetFullPath(iconPath));
            }
        }

        using (var key = classesRoot.CreateSubKey($@"CLSID\{next.Clsid:B}\LocalServer32"))
        {
            key.SetValue(string.Empty, $"\"{executablePath}\"");
        }

        EnsureShortcut(next);
        if (activatesInProcess)
        {
            SetCurrentProcessExplicitAppUserModelID(appId);
            if (registration?.Clsid != next.Clsid)
            {
                activator?.Dispose();
                activator = new ToastActivator(next.Clsid, OnActivated);
            }
        }

        registration = next;
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (registration is not { } current)
        {
            return;
        }

        activator?.Dispose();
        activator = null;
        if (activatesInProcess)
        {
            ToastNotificationManager.History.Clear(current.AppId);
        }

        classesRoot.DeleteSubKeyTree($@"AppUserModelId\{current.AppId}", throwOnMissingSubKey: false);
        classesRoot.DeleteSubKeyTree($@"CLSID\{current.Clsid:B}", throwOnMissingSubKey: false);
        if (ShellLink.Read(current.ShortcutPath)?.AppId == current.AppId)
        {
            File.Delete(current.ShortcutPath);
        }

        registration = null;
    }

    /// <inheritdoc />
    public void Show(string title, string message, NotificationKind kind = NotificationKind.Information)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown notification kind.");
        }

        if (registration is not { } current)
        {
            return;
        }

        var xml = new XmlDocument();
        xml.LoadXml(ToastXml(title, message, kind));
        ToastNotificationManager.CreateToastNotifier(current.AppId).Show(new ToastNotification(xml));
    }

    public void Dispose()
    {
        activator?.Dispose();
        activator = null;
    }

    /// <summary>The notification's XML: a title, an optional message, and a longer duration for warnings and errors.</summary>
    internal static string ToastXml(string title, string message, NotificationKind kind)
    {
        var duration = kind is NotificationKind.Warning or NotificationKind.Error ? " duration=\"long\"" : string.Empty;
        var text = new StringBuilder($"<text>{SecurityElement.Escape(title)}</text>");
        if (message.Length > 0)
        {
            text.Append($"<text>{SecurityElement.Escape(message)}</text>");
        }

        return $"<toast{duration}><visual><binding template=\"ToastGeneric\">{text}</binding></visual></toast>";
    }

    /// <summary>A stable CLSID for the application, so registering again at every start reuses the same keys.</summary>
    internal static Guid ActivatorClsid(string appId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("WpfFoundation.Notifications.ToastActivator:" + appId));
        return new Guid(hash.AsSpan(0, 16));
    }

    private string ShortcutPath(string displayName)
    {
        var name = string.Concat(displayName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(startMenuFolder, name + ".lnk");
    }

    private void EnsureShortcut(Registration next)
    {
        var existing = ShellLink.Read(next.ShortcutPath);
        if (existing is not null)
        {
            bool ours = existing.AppId == next.AppId
                || (existing.AppId is null && string.Equals(Path.GetFileName(existing.TargetPath), Path.GetFileName(executablePath), StringComparison.OrdinalIgnoreCase));
            if (!ours)
            {
                throw new InvalidOperationException($"The Start menu shortcut {next.ShortcutPath} belongs to another application.");
            }

            if (existing.AppId == next.AppId
                && existing.ActivatorClsid == next.Clsid
                && string.Equals(existing.TargetPath, executablePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        ShellLink.Write(next.ShortcutPath, executablePath, next.AppId, next.Clsid);
    }

    private void OnActivated(string arguments) => application.Dispatcher.InvokeAsync(() =>
    {
        if (application.MainWindow is { } window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Show();
            window.Activate();
        }

        Activated?.Invoke(this, EventArgs.Empty);
    });

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);

    private sealed record Registration(string AppId, Guid Clsid, string ShortcutPath);
}
