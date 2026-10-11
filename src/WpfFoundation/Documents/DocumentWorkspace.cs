using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfFoundation.Documents;

/// <summary>
/// Application rules for closing a document, for example asking to save its changes or refusing while it works.
/// </summary>
public interface IDocumentCloseGuard
{
    /// <summary>Decides whether <paramref name="document"/> may close; it may save, ask or refuse.</summary>
    Task<bool> ConfirmCloseAsync(DocumentItem document, CancellationToken cancellationToken);
}

public sealed class DocumentClosedEventArgs(DocumentItem document) : EventArgs
{
    /// <summary>The closed document. Its <see cref="DocumentItem.Content"/> is the application's to clean up.</summary>
    public DocumentItem Document { get; } = document;
}

/// <summary>
/// The open documents of a window, shown as tabs, and the active one. Opening a document whose key is already open
/// activates its tab. Closing asks the application's <see cref="IDocumentCloseGuard"/>; closing the active document
/// activates its left neighbor, then its right neighbor, and leaves no active document when it was the last one.
/// </summary>
/// <remarks>
/// The workspace never disposes document content; handle <see cref="DocumentClosed"/> to clean it up. Create it on
/// the UI thread: when a tab selection is refused, the active-document change is raised again through the thread's
/// synchronization context, after the tab strip's own selection change has finished.
/// </remarks>
public sealed class DocumentWorkspace : ObservableObject
{
    private readonly ObservableCollection<DocumentItem> documents = [];
    private readonly HashSet<DocumentItem> closing = new(ReferenceEqualityComparer.Instance);
    private readonly IDocumentCloseGuard? guard;
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private DocumentItem? activeDocument;

    /// <param name="guard">The application's close rules, or <see langword="null"/> to close documents without asking.</param>
    public DocumentWorkspace(IDocumentCloseGuard? guard = null)
    {
        this.guard = guard;
        Documents = new ReadOnlyObservableCollection<DocumentItem>(documents);
    }

    /// <summary>The open documents, in tab order.</summary>
    public ReadOnlyObservableCollection<DocumentItem> Documents { get; }

    /// <summary>
    /// The document whose tab is selected, or <see langword="null"/> when none is open. Setting it, as a bound tab
    /// strip does, activates an open document; any other value is ignored and the change is raised again so the
    /// tab strip returns to the active document.
    /// </summary>
    public DocumentItem? ActiveDocument
    {
        get => activeDocument;
        set
        {
            if (value is not null && documents.Contains(value))
            {
                SetProperty(ref activeDocument, value);
            }
            else if (!ReferenceEquals(value, activeDocument))
            {
                RestoreSelection();
            }
        }
    }

    /// <summary>Raised after a document closes, so the application can clean up its content.</summary>
    public event EventHandler<DocumentClosedEventArgs>? DocumentClosed;

    /// <summary>
    /// Opens <paramref name="document"/> after the open documents and activates it. When a document with the same key
    /// is already open, that document is activated instead and <paramref name="document"/> is not added.
    /// </summary>
    /// <returns>The open document for the key: <paramref name="document"/> or the one already open.</returns>
    public DocumentItem Open(DocumentItem document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Find(document.Key) is { } existing)
        {
            ActiveDocument = existing;
            return existing;
        }

        if (document.Workspace is not null)
        {
            throw new InvalidOperationException("The document is already open in another workspace.");
        }

        document.Workspace = this;
        documents.Add(document);
        ActiveDocument = document;
        return document;
    }

    /// <summary>The open document with <paramref name="key"/>, or <see langword="null"/>.</summary>
    public DocumentItem? Find(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return documents.FirstOrDefault(document => Equals(document.Key, key));
    }

    /// <summary>Closes <paramref name="document"/> when it is open, closable and the close guard allows it.</summary>
    /// <returns>
    /// <see langword="true"/> when the document closed; <see langword="false"/> when it is not open, not closable,
    /// already closing, or the guard refused.
    /// </returns>
    public async Task<bool> CloseAsync(DocumentItem document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!documents.Contains(document) || !document.IsClosable || !closing.Add(document))
        {
            return false;
        }

        try
        {
            if (guard is not null && !await guard.ConfirmCloseAsync(document, cancellationToken))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            int index = documents.IndexOf(document);
            if (index < 0)
            {
                return false;
            }

            if (ReferenceEquals(document, activeDocument))
            {
                // Prefer the left neighbor; if none, the right one; none leaves the workspace empty.
                var neighbor = index > 0 ? documents[index - 1] : index + 1 < documents.Count ? documents[index + 1] : null;
                SetProperty(ref activeDocument, neighbor, nameof(ActiveDocument));
            }

            documents.RemoveAt(index);
            document.Workspace = null;
        }
        finally
        {
            closing.Remove(document);
        }

        DocumentClosed?.Invoke(this, new DocumentClosedEventArgs(document));
        return true;
    }

    /// <summary>Closes the active document, as Ctrl+W does.</summary>
    public Task<bool> CloseActiveAsync(CancellationToken cancellationToken = default) =>
        activeDocument is null ? Task.FromResult(false) : CloseAsync(activeDocument, cancellationToken);

    /// <summary>Activates the next document, or the previous one when <paramref name="forward"/> is false, wrapping around, as Ctrl+Tab does.</summary>
    public void ActivateNext(bool forward = true)
    {
        if (documents.Count == 0)
        {
            return;
        }

        int index = activeDocument is null ? -1 : documents.IndexOf(activeDocument);
        int next = forward ? (index + 1) % documents.Count : (index - 1 + documents.Count) % documents.Count;
        ActiveDocument = documents[next];
    }

    // A ListBox ignores a change raised while it is still pushing its own selection, so raise it afterwards.
    private void RestoreSelection()
    {
        if (context is null)
        {
            OnPropertyChanged(nameof(ActiveDocument));
        }
        else
        {
            context.Post(_ => OnPropertyChanged(nameof(ActiveDocument)), null);
        }
    }
}
