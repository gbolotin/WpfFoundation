using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Dialogs;
using WpfFoundation.Documents;
using WpfFoundation.Navigation;

namespace WpfFoundation.Gallery.ViewModels;

/// <summary>
/// The Document tabs page. A trail list that is always open opens one document per trail; opening a trail again
/// activates its tab. As the workspace's close guard it asks before closing notes with unsaved changes, and it logs
/// the cleanup it runs when a document closes.
/// </summary>
public sealed partial class DocumentsViewModel : ObservableObject, INavigationPage, IDocumentCloseGuard
{
    private readonly IDialogService dialogs;

    public DocumentsViewModel(IDialogService dialogs)
    {
        this.dialogs = dialogs;
        Workspace = new DocumentWorkspace(this);
        Workspace.DocumentClosed += OnDocumentClosed;
        TrailList = new TrailListViewModel(
            ["Granite Ridge", "Cedar Falls", "Pine Loop", "Quarry Path"],
            new RelayCommand<string>(name => OpenTrail(name!)));
        Workspace.Open(new DocumentItem("trails", "Trails", TrailList) { IsClosable = false });
    }

    public string NavigationName => "Document tabs";

    public string? NavigationIcon => "\uE8A5";

    public DocumentWorkspace Workspace { get; }

    public TrailListViewModel TrailList { get; }

    /// <summary>What the application cleaned up as documents closed, newest first.</summary>
    public ObservableCollection<string> CleanupLog { get; } = [];

    public async Task<bool> ConfirmCloseAsync(DocumentItem document, CancellationToken cancellationToken)
    {
        if (document.Content is not TrailDocumentViewModel { HasUnsavedChanges: true } trail)
        {
            return true;
        }

        return await dialogs.ConfirmAsync($"Close {trail.Name}?", "Your trail notes have unsaved changes.", "Close", "Keep", isDestructive: true);
    }

    private void OpenTrail(string name)
    {
        // The key is the trail, so opening it again activates its tab instead of adding one.
        if (Workspace.Find(name) is null)
        {
            TrailList.OpenCount++;
        }

        Workspace.Open(new DocumentItem(name, name, new TrailDocumentViewModel(name)));
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        // The workspace never disposes content; the application cleans up its own ViewModels here.
        if (e.Document.Content is TrailDocumentViewModel trail)
        {
            trail.Release();
            CleanupLog.Insert(0, $"{trail.Name} closed and its notes were released.");
        }
    }
}

/// <summary>The trail list, the document that stays open.</summary>
public sealed partial class TrailListViewModel(IReadOnlyList<string> trails, IRelayCommand<string> openCommand) : ObservableObject
{
    public IReadOnlyList<string> Trails { get; } = trails;

    public IRelayCommand<string> OpenCommand { get; } = openCommand;

    /// <summary>How many trail documents were created, which reopening an open trail does not change.</summary>
    [ObservableProperty]
    public partial int OpenCount { get; set; }
}

/// <summary>One trail's notes, a closable document.</summary>
public sealed partial class TrailDocumentViewModel(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial string Notes { get; set; } = $"Notes for {name}: trailhead parking fills by eight.";

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    partial void OnNotesChanged(string value) => HasUnsavedChanges = true;

    public bool IsReleased { get; private set; }

    public void Release() => IsReleased = true;
}
