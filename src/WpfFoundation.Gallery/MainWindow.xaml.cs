using System.Windows;
using System.Windows.Controls;
using WpfFoundation.Dialogs;
using WpfFoundation.Gallery.Pages;
using WpfFoundation.Gallery.ViewModels;
using WpfFoundation.Theming;

namespace WpfFoundation.Gallery;

public partial class MainWindow : Window
{
    private readonly IThemeService themes;

    public MainWindow(IThemeService themes, IDialogService dialogs, IFileDialogService files)
    {
        this.themes = themes;
        InitializeComponent();
        Pages.ItemsSource = new[]
        {
            new GalleryPage("Overview", new OverviewPage()),
            new GalleryPage("Styled controls", new StyledControlsPage()),
            new GalleryPage("Custom UI", new CustomUiPage()),
            new GalleryPage("Dialogs", new DialogsPage { DataContext = new DialogsViewModel(dialogs, files) }),
            new GalleryPage("Behaviors", new BehaviorsPage())
        };
        Pages.SelectedIndex = 0;
        Theme.ItemsSource = Enum.GetValues<ThemePreference>();
        Theme.SelectedItem = themes.CurrentTheme;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Theme.SelectedItem is ThemePreference theme)
        {
            themes.ApplyTheme(theme);
        }
    }
}

internal sealed record GalleryPage(string Name, FrameworkElement View);
