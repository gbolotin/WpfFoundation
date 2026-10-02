using System.Windows;
using Microsoft.Win32;

namespace WpfFoundation.Dialogs;

/// <summary>Native Windows open, save and folder pickers.</summary>
public interface IFileDialogService
{
    /// <summary>Lets the user pick one existing file, or several when <c>allowMultiple</c> is set.</summary>
    /// <remarks>Filters use the WPF format, for example <c>"Text files|*.txt"</c>.</remarks>
    /// <returns>The picked paths, or an empty list when cancelled.</returns>
    IReadOnlyList<string> PickFiles(string filter, bool allowMultiple = true);

    /// <summary>Lets the user pick a folder.</summary>
    /// <returns>The picked folder, or <see langword="null"/> when cancelled.</returns>
    string? PickFolder();

    /// <summary>Lets the user choose where to save a file. The dialog asks before overwriting.</summary>
    /// <returns>The chosen path, or <see langword="null"/> when cancelled.</returns>
    string? PickSaveFile(string filter, string? fileName = null);
}

/// <summary>Shows the native pickers owned by the application's active window.</summary>
public sealed class FileDialogService(Application application) : IFileDialogService
{
    /// <inheritdoc />
    public IReadOnlyList<string> PickFiles(string filter, bool allowMultiple = true)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Multiselect = allowMultiple,
            CheckFileExists = true
        };
        return Show(dialog) ? dialog.FileNames : [];
    }

    /// <inheritdoc />
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog();
        return Show(dialog) ? dialog.FolderName : null;
    }

    /// <inheritdoc />
    public string? PickSaveFile(string filter, string? fileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = fileName ?? string.Empty,
            OverwritePrompt = true
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    private bool Show(CommonDialog dialog)
    {
        application.Dispatcher.VerifyAccess();
        var owner = application.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            ?? (application.MainWindow is { IsVisible: true } main ? main : null);
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
    }
}
