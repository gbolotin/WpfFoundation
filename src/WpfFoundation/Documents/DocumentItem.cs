using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Navigation;

namespace WpfFoundation.Documents;

/// <summary>
/// A document open in a <see cref="DocumentWorkspace"/>, shown as a tab. <see cref="Key"/> identifies the document,
/// so opening the same key again activates the existing tab. The view shows <see cref="Content"/> through the
/// implicit DataTemplate for its type.
/// </summary>
public sealed partial class DocumentItem : ObservableObject, INavigationPage
{
    /// <param name="key">What the document shows, for example a record's id. Keys are compared with <see cref="object.Equals(object?)"/>.</param>
    /// <param name="title">The tab header, which screen readers also announce.</param>
    /// <param name="content">The ViewModel shown in the tab. The application owns it and cleans it up when the document closes.</param>
    public DocumentItem(object key, string title, object content)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);
        Key = key;
        Title = title;
        Content = content;
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => IsClosable);
    }

    /// <summary>Identifies the document within its workspace.</summary>
    public object Key { get; }

    /// <summary>The ViewModel shown in the tab.</summary>
    public object Content { get; }

    /// <summary>The tab header, which screen readers also announce.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavigationName))]
    public partial string Title { get; set; }

    /// <summary>
    /// Whether the user can close the tab. A document that is always open, such as a list the other documents are
    /// opened from, shows no close button and ignores Ctrl+W.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial bool IsClosable { get; set; } = true;

    /// <summary>Closes the document in its workspace, asking the workspace's close guard first.</summary>
    public IAsyncRelayCommand CloseCommand { get; }

    /// <summary>The workspace the document is open in, or <see langword="null"/> before it opens and after it closes.</summary>
    public DocumentWorkspace? Workspace { get; internal set; }

    /// <inheritdoc />
    public string NavigationName => Title;

    /// <summary>A Segoe Fluent Icons glyph shown before the title in a sidebar, or <see langword="null"/> for none.</summary>
    public string? NavigationIcon { get; init; }

    private Task CloseAsync() => Workspace?.CloseAsync(this) ?? Task.CompletedTask;
}
