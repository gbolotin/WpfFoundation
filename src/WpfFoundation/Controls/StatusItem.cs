using System.Windows.Input;

namespace WpfFoundation.Controls;

/// <summary>
/// One entry of a status bar styled with <c>WfStatusBarStyle</c>. Long text is trimmed and shown in full in
/// the tooltip; an item with a <see cref="Command"/> is shown as a link-style button.
/// </summary>
/// <param name="Text">The status text.</param>
/// <param name="ToolTip">The tooltip, or <see langword="null"/> to show <paramref name="Text"/>.</param>
/// <param name="IsEmphasized">Whether the text is shown in semibold.</param>
/// <param name="Command">The command the item runs when clicked, or <see langword="null"/> for plain text.</param>
public sealed record StatusItem(string Text, string? ToolTip = null, bool IsEmphasized = false, ICommand? Command = null)
{
    /// <summary>The tooltip to show: <see cref="ToolTip"/>, or the text itself.</summary>
    public string ToolTipText => ToolTip ?? Text;
}
