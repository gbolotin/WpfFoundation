using System.Windows;
using WpfFoundation.Dialogs;
using WpfFoundation.Theming;

namespace WpfFoundation.Gallery;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Explicit composition: the library needs no DI container.
        var themes = new ThemeService(this);
        var dialogs = new DialogService(this);
        var files = new FileDialogService(this);
        MainWindow = new MainWindow(themes, dialogs, files);
        MainWindow.Show();
    }
}
