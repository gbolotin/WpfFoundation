using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Controls;

namespace WpfFoundation.Gallery.Pages;

public partial class CustomUiPage : UserControl
{
    private readonly RelayCommand startCommand;
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

        startCommand = new RelayCommand(Start, () => AlreadyRunning.IsChecked != true);
        StartButton.Command = startCommand;
        SelectProfile(OnlineProfile);
    }

    private void ViewLog()
    {
        logViews++;
        StatusResult.Text = $"View log ran {logViews} time(s).";
    }

    private void Start() => StartResult.Text = $"Started {StartButton.Content}.";

    private void OnProfileClick(object sender, RoutedEventArgs e) => SelectProfile((MenuItem)sender);

    private void OnProfileSettingsClick(object sender, RoutedEventArgs e) => StartResult.Text = "Opened the profile settings.";

    private void OnAlreadyRunningClick(object sender, RoutedEventArgs e) => startCommand.NotifyCanExecuteChanged();

    // Like the Visual Studio Start button, the checked profile is the one the button starts.
    private void SelectProfile(MenuItem profile)
    {
        OnlineProfile.IsChecked = profile == OnlineProfile;
        OfflineProfile.IsChecked = profile == OfflineProfile;
        StartButton.Content = profile.Header;
    }
}
