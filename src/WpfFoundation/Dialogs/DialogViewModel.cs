using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WpfFoundation.Dialogs;

/// <summary>A footer button of a dialog.</summary>
/// <param name="Label">The button text, naming the action (for example "Delete" or "Keep") rather than Yes or No.</param>
/// <param name="Command">The command the button runs, usually the dialog's accept or cancel command.</param>
public sealed record DialogButton(string Label, ICommand Command)
{
    /// <summary>Whether Enter presses this button. Destructive confirmations make the safe choice the default.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Whether Escape presses this button.</summary>
    public bool IsCancel { get; init; }

    /// <summary>Whether the button uses the accent style.</summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// Base class of every dialog's ViewModel. <see cref="IDialogService"/> shows it in the shared dialog window,
/// where an implicit DataTemplate for the ViewModel type supplies the body and <see cref="Buttons"/> the footer.
/// The ViewModel never references the window: it finishes by accepting or cancelling, which completes
/// <see cref="Completion"/>, and the window closes when it observes that.
/// </summary>
/// <remarks>
/// Acceptance is validation-aware: <see cref="Accept"/> validates every property with data annotations and
/// refuses while there are errors or <see cref="CanAccept"/> returns <see langword="false"/>. Commit edits in
/// <see cref="OnAccepted"/>, so cancelling never commits them.
/// </remarks>
public abstract class DialogViewModel : ObservableValidator
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected DialogViewModel(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
        AcceptCommand = new RelayCommand(() => Accept());
        CancelCommand = new RelayCommand(Cancel);
    }

    /// <summary>The window title.</summary>
    public string Title { get; }

    /// <summary>
    /// Whether the user can resize the dialog, for example to read long text. Other dialogs size to their content.
    /// </summary>
    public bool IsResizable { get; init; }

    /// <summary>The footer buttons, in display order.</summary>
    public abstract IReadOnlyList<DialogButton> Buttons { get; }

    /// <summary>Accepts the dialog when it is valid.</summary>
    public IRelayCommand AcceptCommand { get; }

    /// <summary>Cancels the dialog without committing edits.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>Completes with <see langword="true"/> when accepted and <see langword="false"/> when cancelled or closed.</summary>
    public Task<bool> Completion => completion.Task;

    /// <summary>Whether the dialog was accepted or cancelled.</summary>
    public bool IsCompleted => completion.Task.IsCompleted;

    /// <summary>Validates and, when valid, commits and accepts the dialog.</summary>
    /// <returns>Whether the dialog was accepted.</returns>
    public bool Accept()
    {
        if (IsCompleted)
        {
            return false;
        }

        ValidateAllProperties();
        if (HasErrors || !CanAccept())
        {
            return false;
        }

        OnAccepted();
        completion.SetResult(true);
        OnPropertyChanged(nameof(IsCompleted));
        return true;
    }

    /// <summary>Cancels the dialog. Escape and closing the window also cancel it.</summary>
    public void Cancel()
    {
        if (completion.TrySetResult(false))
        {
            OnPropertyChanged(nameof(IsCompleted));
        }
    }

    /// <summary>Additional acceptance rules beyond data annotations, supplied by the application.</summary>
    protected virtual bool CanAccept() => true;

    /// <summary>Commits the dialog's edits. Runs only when the dialog is accepted.</summary>
    protected virtual void OnAccepted()
    {
    }
}
