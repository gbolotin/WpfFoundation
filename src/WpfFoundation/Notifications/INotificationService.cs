namespace WpfFoundation.Notifications;

/// <summary>What a notification reports.</summary>
public enum NotificationKind
{
    /// <summary>Neutral information.</summary>
    Information,

    /// <summary>Something finished successfully.</summary>
    Success,

    /// <summary>Something finished, but needs a look.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error
}

/// <summary>
/// Sends Windows notifications for the application. The WpfFoundation.Notifications package implements it; applications
/// that do not reference that package can still pass their own implementation to <c>OperationFeedback</c>.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Registers the application's name and icon with Windows so its notifications show them, and so a click on one
    /// brings the main window to the front. Call it at every start; it is idempotent.
    /// </summary>
    /// <param name="appId">A stable AppUserModelId, for example <c>"Contoso.PhotoImporter"</c>.</param>
    /// <param name="displayName">The name notifications and Windows Settings show.</param>
    /// <param name="iconPath">The full path of an .ico or .png file, or <see langword="null"/> for none.</param>
    void Register(string appId, string displayName, string? iconPath);

    /// <summary>Removes everything <see cref="Register"/> created, for example when the application is uninstalled.</summary>
    void Unregister();

    /// <summary>Sends a notification. Does nothing before <see cref="Register"/>.</summary>
    /// <param name="title">The first line. Wording comes from the application.</param>
    /// <param name="message">The text below the title.</param>
    /// <param name="kind">What the notification reports. Warnings and errors stay on screen longer.</param>
    void Show(string title, string message, NotificationKind kind = NotificationKind.Information);
}
