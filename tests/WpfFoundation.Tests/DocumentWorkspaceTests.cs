using WpfFoundation.Documents;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class DocumentWorkspaceTests
{
    [TestMethod]
    public void OpeningAnOpenKeyActivatesItsExistingTab()
    {
        var workspace = new DocumentWorkspace();
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
        Assert.AreSame(falls, workspace.ActiveDocument, "Opening activates the new document.");

        var again = workspace.Open(Trail("granite-ridge", "Granite Ridge (again)"));

        Assert.AreSame(ridge, again, "The open document is returned.");
        Assert.AreSame(ridge, workspace.ActiveDocument);
        Assert.HasCount(2, workspace.Documents, "No second tab opens for the same key.");
        Assert.AreSame(workspace, ridge.Workspace);
        Assert.AreSame(ridge, workspace.Find("granite-ridge"));
        Assert.IsNull(workspace.Find("pine-loop"));
    }

    [TestMethod]
    public async Task ClosingTheActiveDocumentActivatesItsLeftThenRightNeighbor()
    {
        var workspace = new DocumentWorkspace();
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
        var loop = workspace.Open(Trail("pine-loop", "Pine Loop"));

        workspace.ActiveDocument = falls;
        Assert.IsTrue(await workspace.CloseAsync(falls));
        Assert.AreSame(ridge, workspace.ActiveDocument, "The left neighbor is preferred.");

        Assert.IsTrue(await workspace.CloseAsync(ridge));
        Assert.AreSame(loop, workspace.ActiveDocument, "Without a left neighbor, the right one is activated.");

        Assert.IsTrue(await workspace.CloseAsync(loop));
        Assert.IsNull(workspace.ActiveDocument, "Closing the last document leaves the workspace empty.");
        Assert.IsEmpty(workspace.Documents);
        Assert.IsNull(loop.Workspace);
    }

    [TestMethod]
    public async Task ClosingAnInactiveDocumentKeepsTheActiveOne()
    {
        var workspace = new DocumentWorkspace();
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
        var changes = 0;
        workspace.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(DocumentWorkspace.ActiveDocument) ? 1 : 0;

        Assert.IsTrue(await workspace.CloseAsync(ridge));

        Assert.AreSame(falls, workspace.ActiveDocument);
        Assert.AreEqual(0, changes);
        CollectionAssert.AreEqual(new[] { falls }, workspace.Documents.ToArray());
    }

    [TestMethod]
    public async Task TheGuardCanRefuseAClose()
    {
        var guard = new Guard { Allow = false };
        var workspace = new DocumentWorkspace(guard);
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var closed = new List<DocumentItem>();
        workspace.DocumentClosed += (_, e) => closed.Add(e.Document);

        Assert.IsFalse(await workspace.CloseAsync(ridge));
        Assert.HasCount(1, workspace.Documents);
        Assert.AreSame(ridge, workspace.ActiveDocument);
        Assert.IsEmpty(closed, "A refused document is not reported as closed.");
        CollectionAssert.AreEqual(new[] { ridge }, guard.Asked);

        guard.Allow = true;
        Assert.IsTrue(await workspace.CloseAsync(ridge));
        CollectionAssert.AreEqual(new[] { ridge }, closed, "The application cleans up after the close.");
    }

    [TestMethod]
    public async Task ADocumentIsClosedOnceWhileTheGuardIsAsking()
    {
        var guard = new Guard { Allow = true, Pending = new TaskCompletionSource() };
        var workspace = new DocumentWorkspace(guard);
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var closedCount = 0;
        workspace.DocumentClosed += (_, _) => closedCount++;

        var first = workspace.CloseAsync(ridge);
        Assert.IsFalse(await workspace.CloseAsync(ridge), "A second close while the guard asks is ignored.");
        guard.Pending.SetResult();

        Assert.IsTrue(await first);
        Assert.AreEqual(1, closedCount);
        Assert.HasCount(1, guard.Asked);
    }

    [TestMethod]
    public async Task ACancelledCloseKeepsTheDocument()
    {
        var guard = new Guard { Allow = true };
        var workspace = new DocumentWorkspace(guard);
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => workspace.CloseAsync(ridge, cancellation.Token));
        Assert.HasCount(1, workspace.Documents);
        Assert.IsTrue(await workspace.CloseAsync(ridge), "The document can close after a cancelled attempt.");
    }

    [TestMethod]
    public async Task ADocumentThatIsNotClosableStaysOpen()
    {
        var workspace = new DocumentWorkspace();
        var list = workspace.Open(new DocumentItem("trails", "Trails", new object()) { IsClosable = false });

        Assert.IsFalse(list.CloseCommand.CanExecute(null));
        Assert.IsFalse(await workspace.CloseAsync(list));
        Assert.IsFalse(await workspace.CloseActiveAsync());
        Assert.HasCount(1, workspace.Documents);

        list.IsClosable = true;
        Assert.IsTrue(list.CloseCommand.CanExecute(null));
        await list.CloseCommand.ExecuteAsync(null);
        Assert.IsEmpty(workspace.Documents, "The close command closes the document in its workspace.");
    }

    [TestMethod]
    public void OnlyAnOpenDocumentCanBeActivated()
    {
        var workspace = new DocumentWorkspace();
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var changes = 0;
        workspace.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(DocumentWorkspace.ActiveDocument) ? 1 : 0;

        workspace.ActiveDocument = Trail("pine-loop", "Pine Loop");
        Assert.AreSame(ridge, workspace.ActiveDocument);
        workspace.ActiveDocument = null;
        Assert.AreSame(ridge, workspace.ActiveDocument, "A tab strip clearing its selection does not deactivate the document.");
        Assert.AreEqual(2, changes, "The change is raised again so a bound tab strip returns to the active document.");
    }

    [TestMethod]
    public void ADocumentOpensInOneWorkspace()
    {
        var ridge = Trail("granite-ridge", "Granite Ridge");
        new DocumentWorkspace().Open(ridge);

        Assert.ThrowsExactly<InvalidOperationException>(() => new DocumentWorkspace().Open(ridge));
    }

    [TestMethod]
    public void ActivatingTheNextDocumentWrapsAround()
    {
        var workspace = new DocumentWorkspace();
        var ridge = workspace.Open(Trail("granite-ridge", "Granite Ridge"));
        var falls = workspace.Open(Trail("cedar-falls", "Cedar Falls"));
        var loop = workspace.Open(Trail("pine-loop", "Pine Loop"));

        workspace.ActivateNext();
        Assert.AreSame(ridge, workspace.ActiveDocument);
        workspace.ActivateNext(forward: false);
        Assert.AreSame(loop, workspace.ActiveDocument);
        workspace.ActivateNext(forward: false);
        Assert.AreSame(falls, workspace.ActiveDocument);
    }

    internal static DocumentItem Trail(string key, string title) => new(key, title, new TrailViewModel(title));

    internal sealed record TrailViewModel(string Name);

    private sealed class Guard : IDocumentCloseGuard
    {
        public bool Allow { get; set; }

        public TaskCompletionSource? Pending { get; init; }

        public List<DocumentItem> Asked { get; } = [];

        public async Task<bool> ConfirmCloseAsync(DocumentItem document, CancellationToken cancellationToken)
        {
            Asked.Add(document);
            if (Pending is not null)
            {
                await Pending.Task;
            }

            return Allow;
        }
    }
}
