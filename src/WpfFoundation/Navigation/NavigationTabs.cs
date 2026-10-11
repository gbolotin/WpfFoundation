using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WpfFoundation.Documents;

namespace WpfFoundation.Navigation;

/// <summary>
/// Shows an <see cref="INavigationService"/> in a TabControl styled with <c>WfNavigationTabControlStyle</c>: one tab per
/// page, and a <see cref="RetainedPageHost"/> in place of the TabControl's content, so each page keeps its view while
/// other tabs are selected. Ctrl+Tab and Ctrl+Shift+Tab (the TabControl's own keys) and Ctrl+Page Down and
/// Ctrl+Page Up switch tabs; for a <see cref="DocumentWorkspace"/>, Ctrl+W also closes the active document.
/// </summary>
/// <example>
/// <code>
/// &lt;TabControl navigation:NavigationTabs.Navigation="{Binding Workspace}"
///             Style="{StaticResource WfNavigationTabControlStyle}" /&gt;
/// </code>
/// </example>
public static class NavigationTabs
{
    public static readonly DependencyProperty NavigationProperty = DependencyProperty.RegisterAttached(
        "Navigation", typeof(INavigationService), typeof(NavigationTabs), new PropertyMetadata(null, OnNavigationChanged));

    public static INavigationService? GetNavigation(TabControl element) => (INavigationService?)element.GetValue(NavigationProperty);

    public static void SetNavigation(TabControl element, INavigationService? value) => element.SetValue(NavigationProperty, value);

    private static void OnNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TabControl tabs)
        {
            return;
        }

        tabs.PreviewKeyDown -= OnPreviewKeyDown;
        if (e.NewValue is not INavigationService navigation)
        {
            BindingOperations.ClearBinding(tabs, ItemsControl.ItemsSourceProperty);
            BindingOperations.ClearBinding(tabs, System.Windows.Controls.Primitives.Selector.SelectedItemProperty);
            return;
        }

        tabs.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(INavigationService.Pages)) { Source = navigation });
        tabs.SetBinding(
            System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            new Binding(nameof(INavigationService.CurrentPage)) { Source = navigation, Mode = BindingMode.TwoWay });
        tabs.PreviewKeyDown += OnPreviewKeyDown;
    }

    private static async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tabs = (TabControl)sender;
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.Control || tabs.Items.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            // The TabControl handles Ctrl+Tab itself; Ctrl+Page Down and Ctrl+Page Up are the other Windows tab keys.
            case Key.PageDown or Key.PageUp:
                e.Handled = true;
                int step = e.Key == Key.PageDown ? 1 : tabs.Items.Count - 1;
                tabs.SelectedIndex = (Math.Max(tabs.SelectedIndex, 0) + step) % tabs.Items.Count;
                FocusSelectedTab(tabs);
                break;
            case Key.W when GetNavigation(tabs) is DocumentWorkspace workspace:
                e.Handled = true;
                if (await workspace.CloseActiveAsync())
                {
                    // The focused view was released with the document, so keep keyboard focus in the tabs.
                    FocusSelectedTab(tabs);
                }

                break;
        }
    }

    private static void FocusSelectedTab(TabControl tabs)
    {
        tabs.UpdateLayout();
        (tabs.ItemContainerGenerator.ContainerFromItem(tabs.SelectedItem) as TabItem)?.Focus();
    }
}

/// <summary>Picks a tab header: a document's title with a close button, or any other page's name.</summary>
public sealed class NavigationTabHeaderSelector : DataTemplateSelector
{
    /// <summary>The header of a page that is not a <see cref="DocumentItem"/>.</summary>
    public DataTemplate? PageTemplate { get; set; }

    /// <summary>The header of a <see cref="DocumentItem"/>.</summary>
    public DataTemplate? DocumentTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is DocumentItem ? DocumentTemplate : PageTemplate;
}
