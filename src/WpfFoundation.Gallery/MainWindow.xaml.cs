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
        foreach (var theme in Enum.GetValues<ThemePreference>())
        {
            var item = new MenuItem { Header = theme.ToString(), Tag = theme, IsCheckable = true };
            item.Click += (_, _) => Theme.SelectedItem = theme;
            ThemeMenu.Items.Add(item);
        }

        Theme.ItemsSource = Enum.GetValues<ThemePreference>();
        Theme.SelectedItem = themes.CurrentTheme;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Theme.SelectedItem is ThemePreference theme)
        {
            themes.ApplyTheme(theme);
            foreach (MenuItem item in ThemeMenu.Items)
            {
                item.IsChecked = Equals(item.Tag, theme);
            }
        }
    }
}
