using System.Windows;
using WpfFoundation.Dialogs;
using WpfFoundation.Gallery.ViewModels;
using WpfFoundation.Navigation;
using WpfFoundation.Notifications;
using WpfFoundation.Operations;
using WpfFoundation.Taskbar;
using WpfFoundation.Theming;

namespace WpfFoundation.Gallery;

public partial class App : System.Windows.Application
{
    private NavigationService? navigation;
    private WindowsNotificationService? notifications;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Explicit composition: the library needs no DI container.
        var themes = new ThemeService(this);
        var dialogs = new DialogService(this);
        var files = new FileDialogService(this);
        // Register before the main window is shown, so the taskbar groups the window with its Start menu shortcut.
        notifications = new WindowsNotificationService(this);
        notifications.Register("gbolotin.WpfFoundation.Gallery", "WpfFoundation Gallery", iconPath: null);
        var feedback = new OperationFeedback(new TaskbarService(this), notifications);
        var navigationDemo = new NavigationDemoViewModel(dialogs);
        navigation = new NavigationService(
            [
                new OverviewPageViewModel(),
                new StyledControlsPageViewModel(),
                new CustomUiPageViewModel(),
                navigationDemo,
                new DialogsViewModel(dialogs, files),
                new BehaviorsViewModel(),
                new TaskbarViewModel(feedback, notifications)
            ],
            guard: navigationDemo);
        navigation.NavigationFailed += async (_, failure) =>
            await dialogs.ShowMessageAsync($"{failure.Page.NavigationName} could not open", failure.Exception.Message);

        MainWindow = new MainWindow(navigation, themes);
        MainWindow.Show();
        await navigation.NavigateAsync(navigation.Pages[0]);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        navigation?.Dispose();
        notifications?.Dispose();
        base.OnExit(e);
    }
}
