using System.Windows;
using System.Windows.Controls;
using WpfFoundation.Navigation;
using WpfFoundation.Theming;

namespace WpfFoundation.Gallery;

public partial class MainWindow : Window
{
    private readonly IThemeService themes;

    public MainWindow(INavigationService navigation, IThemeService themes)
    {
        this.themes = themes;
        InitializeComponent();
        DataContext = navigation;
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
