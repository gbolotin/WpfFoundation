using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfFoundation.Navigation;

namespace WpfFoundation.Documents;

/// <summary>
/// The tabs of a <see cref="DocumentWorkspace"/>: a tab strip of document titles with close buttons above a
/// <see cref="RetainedPageHost"/> that keeps each open document's view. While focus is inside it, Ctrl+W closes the
/// active document, and Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+Page Down and Ctrl+Page Up move between documents.
/// </summary>
[TemplatePart(Name = TabStripPartName, Type = typeof(ListBox))]
public class DocumentTabs : Control
{
    private const string TabStripPartName = "PART_TabStrip";

    public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(
        nameof(Workspace), typeof(DocumentWorkspace), typeof(DocumentTabs), new PropertyMetadata(null));

    private ListBox? tabStrip;

    static DocumentTabs()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DocumentTabs), new FrameworkPropertyMetadata(typeof(DocumentTabs)));
        FocusableProperty.OverrideMetadata(typeof(DocumentTabs), new FrameworkPropertyMetadata(false));
    }

    public DocumentWorkspace? Workspace
    {
        get => (DocumentWorkspace?)GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        tabStrip = GetTemplateChild(TabStripPartName) as ListBox;
    }

    protected override async void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Workspace is not { } workspace || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        switch (e.Key)
        {
            case Key.W when !shift:
                e.Handled = true;
                if (await workspace.CloseActiveAsync())
                {
                    FocusActiveTab();
                }

                break;
            case Key.Tab:
                e.Handled = true;
                ActivateNext(workspace, !shift);
                break;
            case Key.PageDown or Key.PageUp when !shift:
                e.Handled = true;
                ActivateNext(workspace, e.Key == Key.PageDown);
                break;
        }
    }

    // Activates the next or previous document, wrapping around.
    private void ActivateNext(DocumentWorkspace workspace, bool forward)
    {
        var documents = workspace.Documents;
        if (documents.Count == 0)
        {
            return;
        }

        int index = workspace.ActiveDocument is { } active ? documents.IndexOf(active) : -1;
        int next = forward ? (index + 1) % documents.Count : (index - 1 + documents.Count) % documents.Count;
        workspace.CurrentPage = documents[next];
        FocusActiveTab();
    }

    // Keeps keyboard focus in the control when the focused view is hidden or released.
    private void FocusActiveTab()
    {
        if (tabStrip is null || Workspace?.ActiveDocument is not { } active)
        {
            return;
        }

        tabStrip.UpdateLayout();
        (tabStrip.ItemContainerGenerator.ContainerFromItem(active) as ListBoxItem)?.Focus();
    }
}
