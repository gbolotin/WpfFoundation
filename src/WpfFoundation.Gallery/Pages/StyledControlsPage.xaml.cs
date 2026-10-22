using System.Windows;
using System.Windows.Controls;

namespace WpfFoundation.Gallery.Pages;

public partial class StyledControlsPage : UserControl
{
    public StyledControlsPage()
    {
        InitializeComponent();
    }

    private void OnSave(object sender, RoutedEventArgs e) => ButtonResult.Text = "Save note clicked.";

    private void OnDiscard(object sender, RoutedEventArgs e) => ButtonResult.Text = "Discard clicked.";

    private void OnHoverAction(object sender, RoutedEventArgs e) =>
        HoverActionResult.Text = $"{System.Windows.Automation.AutomationProperties.GetName((Button)sender)} clicked.";
}
