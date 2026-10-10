using System.Windows;
using WpfFoundation.Dialogs;
using WpfFoundation.Gallery.ViewModels;
using WpfFoundation.Navigation;
using WpfFoundation.Operations;
using WpfFoundation.Taskbar;
using WpfFoundation.Theming;

namespace WpfFoundation.Gallery;

public partial class App : System.Windows.Application
{
    private NavigationService? navigation;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Explicit composition: the library needs no DI container.
        var themes = new ThemeService(this);
        var dialogs = new DialogService(this);
        var files = new FileDialogService(this);
        var feedback = new OperationFeedback(new TaskbarService(this));
        var navigationDemo = new NavigationDemoViewModel(dialogs);
        navigation = new NavigationService(
            [
                new OverviewPageViewModel(),
                new StyledControlsPageViewModel(),
                new CustomUiPageViewModel(),
                navigationDemo,
                new DialogsViewModel(dialogs, files),
                new BehaviorsViewModel(),
                new TaskbarViewModel(feedback)
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
        base.OnExit(e);
    }
}
