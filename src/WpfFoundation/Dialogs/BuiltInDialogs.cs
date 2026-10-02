namespace WpfFoundation.Dialogs;

/// <summary>A message with one button that closes it.</summary>
public sealed class MessageDialogViewModel : DialogViewModel
{
    public MessageDialogViewModel(string title, string message, string closeLabel = "Close")
        : base(title)
    {
        Message = message;
        // Escape also closes it; the window cancels dialogs without a cancel button.
        Buttons = [new DialogButton(closeLabel, AcceptCommand) { IsDefault = true, IsPrimary = true }];
    }

    public string Message { get; }

    public override IReadOnlyList<DialogButton> Buttons { get; }
}

/// <summary>
/// A question with a button that confirms an action and one that cancels it. A destructive confirmation makes
/// the cancel button the default, so Enter keeps the safe choice.
/// </summary>
public sealed class ConfirmationDialogViewModel : DialogViewModel
{
    public ConfirmationDialogViewModel(string title, string message, string confirmLabel, string cancelLabel, bool isDestructive = false)
        : base(title)
    {
        Message = message;
        IsDestructive = isDestructive;
        Buttons =
        [
            new DialogButton(confirmLabel, AcceptCommand) { IsDefault = !isDestructive, IsPrimary = !isDestructive },
            new DialogButton(cancelLabel, CancelCommand) { IsDefault = isDestructive, IsCancel = true }
        ];
    }

    public string Message { get; }

    public bool IsDestructive { get; }

    public override IReadOnlyList<DialogButton> Buttons { get; }
}

/// <summary>Long text to read before approving an action, in a resizable dialog.</summary>
public sealed class ReviewDialogViewModel : DialogViewModel
{
    public ReviewDialogViewModel(string title, string content, string approveLabel, string cancelLabel = "Cancel")
        : base(title)
    {
        Content = content;
        IsResizable = true;
        Buttons =
        [
            new DialogButton(approveLabel, AcceptCommand) { IsPrimary = true },
            new DialogButton(cancelLabel, CancelCommand) { IsCancel = true }
        ];
    }

    public string Content { get; }

    public override IReadOnlyList<DialogButton> Buttons { get; }
}
