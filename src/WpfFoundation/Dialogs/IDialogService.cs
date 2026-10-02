namespace WpfFoundation.Dialogs;

/// <summary>Shows dialogs modally in the shared dialog window.</summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a dialog over the active window and completes when it is accepted or cancelled.
    /// The application's resources must hold an implicit DataTemplate for the dialog's ViewModel type.
    /// </summary>
    /// <returns><see langword="true"/> when accepted; <see langword="false"/> when cancelled, closed or dismissed with Escape.</returns>
    Task<bool> ShowAsync(DialogViewModel dialog);
}

/// <summary>Messages, confirmations and reviews built on <see cref="IDialogService.ShowAsync"/>.</summary>
public static class DialogServiceExtensions
{
    /// <summary>Shows a message with one button that closes it.</summary>
    public static Task ShowMessageAsync(this IDialogService dialogs, string title, string message, string closeLabel = "Close") =>
        dialogs.ShowAsync(new MessageDialogViewModel(title, message, closeLabel));

    /// <summary>
    /// Asks to confirm an action. Name the buttons after the choices, for example "Delete" and "Keep".
    /// A destructive confirmation makes the cancel button the default, so Enter keeps the safe choice.
    /// </summary>
    /// <returns>Whether the action was confirmed.</returns>
    public static Task<bool> ConfirmAsync(this IDialogService dialogs, string title, string message, string confirmLabel, string cancelLabel, bool isDestructive = false) =>
        dialogs.ShowAsync(new ConfirmationDialogViewModel(title, message, confirmLabel, cancelLabel, isDestructive));

    /// <summary>Shows long text to read before approving an action.</summary>
    /// <returns>Whether the action was approved.</returns>
    public static Task<bool> ReviewAsync(this IDialogService dialogs, string title, string content, string approveLabel, string cancelLabel = "Cancel") =>
        dialogs.ShowAsync(new ReviewDialogViewModel(title, content, approveLabel, cancelLabel));
}
