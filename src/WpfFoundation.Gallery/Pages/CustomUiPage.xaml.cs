using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Controls;

namespace WpfFoundation.Gallery.Pages;

public partial class CustomUiPage : UserControl
{
    private int logViews;

    public CustomUiPage()
    {
        InitializeComponent();
        Status.ItemsSource = new[]
        {
            new StatusItem("12 notes", IsEmphasized: true),
            new StatusItem("Last sync 09:41", "Synced with the shared folder at 09:41"),
            new StatusItem("View log", Command: new RelayCommand(ViewLog))
        };
    }

    private void ViewLog()
    {
        logViews++;
        StatusResult.Text = $"View log ran {logViews} time(s).";
    }
}
