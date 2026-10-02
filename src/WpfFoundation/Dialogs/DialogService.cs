using System.Windows;
using System.Windows.Input;

namespace WpfFoundation.Dialogs;

/// <summary>
/// Shows each dialog in a <see cref="DialogWindow"/> owned by the application's active window, and returns
/// keyboard focus to where it was when the dialog closes.
/// </summary>
public sealed class DialogService(Application application) : IDialogService
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The dialog has already completed, or no DataTemplate exists for its ViewModel type. A missing template is
    /// reported before anything is shown instead of displaying the ViewModel's type name.
    /// </exception>
    public Task<bool> ShowAsync(DialogViewModel dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        application.Dispatcher.VerifyAccess();
        if (dialog.IsCompleted)
        {
            throw new InvalidOperationException("The dialog has already been accepted or cancelled.");
        }

        if (!HasTemplate(dialog.GetType()))
        {
            throw new InvalidOperationException(
                $"No DataTemplate exists for {dialog.GetType().FullName}. Add an implicit DataTemplate for it to the application's resources.");
        }

        var focused = Keyboard.FocusedElement;
        var window = new DialogWindow(dialog);
        if (FindOwner() is { } owner)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        window.ShowDialog();
        if (focused is UIElement { IsVisible: true } element)
        {
            element.Focus();
        }

        return dialog.Completion;
    }

    private Window? FindOwner()
    {
        var windows = application.Windows.OfType<Window>().Where(window => window.IsVisible).ToArray();
        return windows.FirstOrDefault(window => window.IsActive)
            ?? (application.MainWindow is { IsVisible: true } main ? main : windows.LastOrDefault());
    }

    private bool HasTemplate(Type type)
    {
        // Implicit DataTemplates also apply to derived types, as WPF's own lookup does.
        for (var current = type; current is not null && current != typeof(DialogViewModel); current = current.BaseType)
        {
            if (application.TryFindResource(new DataTemplateKey(current)) is DataTemplate)
            {
                return true;
            }
        }

        return false;
    }
}
