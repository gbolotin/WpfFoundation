using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfFoundation.Documents;

/// <summary>
/// Shows the active document of a <see cref="DocumentWorkspace"/>. Each document's view is created from the
/// DataTemplate of its content when the document is first shown and kept, hidden, while other documents are active,
/// so scroll positions, selections and edits survive switching tabs. A document's view is released when it closes,
/// and every view is released when <see cref="Workspace"/> changes.
/// </summary>
public sealed class DocumentHost : FrameworkElement
{
    public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(
        nameof(Workspace), typeof(DocumentWorkspace), typeof(DocumentHost), new PropertyMetadata(null, OnWorkspaceChanged));

    private readonly Grid views = new();
    private readonly Dictionary<DocumentItem, ContentPresenter> presenters = new(ReferenceEqualityComparer.Instance);
    private DocumentWorkspace? subscribed;

    public DocumentHost()
    {
        AddVisualChild(views);
        AddLogicalChild(views);
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
    }

    public DocumentWorkspace? Workspace
    {
        get => (DocumentWorkspace?)GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    /// <summary>The documents whose views are currently retained.</summary>
    public IReadOnlyCollection<DocumentItem> RetainedDocuments => presenters.Keys;

    protected override int VisualChildrenCount => 1;

    protected override System.Collections.IEnumerator LogicalChildren => new[] { views }.GetEnumerator();

    protected override Visual GetVisualChild(int index) => index == 0 ? views : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size availableSize)
    {
        views.Measure(availableSize);
        return views.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        views.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private static void OnWorkspaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (DocumentHost)d;
        host.Unsubscribe();
        host.views.Children.Clear();
        host.presenters.Clear();
        if (host.IsLoaded)
        {
            host.Subscribe();
        }
    }

    // Subscribe only while loaded, so a long-lived workspace never keeps a closed window alive.
    private void Subscribe()
    {
        Unsubscribe();
        if (Workspace is { } workspace)
        {
            subscribed = workspace;
            workspace.PropertyChanged += OnWorkspacePropertyChanged;
            ((INotifyCollectionChanged)workspace.Documents).CollectionChanged += OnDocumentsChanged;
        }

        Update();
    }

    private void Unsubscribe()
    {
        if (subscribed is not null)
        {
            subscribed.PropertyChanged -= OnWorkspacePropertyChanged;
            ((INotifyCollectionChanged)subscribed.Documents).CollectionChanged -= OnDocumentsChanged;
            subscribed = null;
        }
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentWorkspace.ActiveDocument) or null)
        {
            Update();
        }
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Update();

    // Releases the views of closed documents, creates the active document's view on first show, and shows only it.
    private void Update()
    {
        var workspace = Workspace;
        foreach (var document in presenters.Keys.ToArray())
        {
            if (workspace is null || !workspace.Documents.Contains(document))
            {
                views.Children.Remove(presenters[document]);
                presenters.Remove(document);
            }
        }

        var active = workspace?.ActiveDocument;
        if (active is not null && !presenters.ContainsKey(active))
        {
            var presenter = new ContentPresenter { Content = active.Content };
            presenters.Add(active, presenter);
            views.Children.Add(presenter);
        }

        foreach (var (document, presenter) in presenters)
        {
            presenter.Visibility = ReferenceEquals(document, active) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
